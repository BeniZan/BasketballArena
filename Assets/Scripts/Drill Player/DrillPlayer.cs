using SingletonBehaviors;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class DrillPlayer : NetworkBehaviour {
    static DrillPlayer _instance;
    static public DrillPlayer Instance {
        get => _instance = _instance ? _instance : FindFirstObjectByType<DrillPlayer>();
    }
    public DrillData CurrentPlayingDrillData => NetDrillsActivator.Instance.ActiveManeuver;
    public struct AnimationPlaybackState : INetworkSerializeByMemcpy {
        public float Time;          // animation time at SyncTime
        public float Speed;         // 0 = paused
        public double SyncTime;     // NetworkManager.ServerTime.Time
    }

    NetworkVariable<AnimationPlaybackState> SyncState 
        = new NetworkVariable<AnimationPlaybackState>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
    public AnimationPlaybackState PlaybackState { get => SyncState.Value;
        set {
            if(IsServer)
                SyncState.Value = value;
            else
                _logger.LogError("Attempted to set PlaybackState on client. This is not allowed.");
        }
    }

    [SerializeField, Min(1f)] float _countdownDuration = 3f;
    readonly NetworkVariable<double> _syncCountdownEndTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>True on every peer while the server-authoritative pre-drill countdown is running.</summary>
    public bool IsCountdownActive => _syncCountdownEndTime.Value > 0d;

    /// <summary>The synchronized digit the XR HUD should currently display, or zero when hidden.</summary>
    public int CountdownNumber {
        get {
            if (!IsCountdownActive)
                return 0;

            var remaining = _syncCountdownEndTime.Value - CurrentNetworkTime;
            return Mathf.Clamp(Mathf.CeilToInt((float)remaining), 1, Mathf.CeilToInt(_countdownDuration));
        }
    }

    double CurrentNetworkTime =>
        NetworkManager.Singleton ? NetworkManager.Singleton.ServerTime.Time : 0d;

    // One entry per trigger of the active drill, holding the animation time the gate opened
    // at, or DrillSegmentResolver.GATE_CLOSED while it is still shut. Server authoritative:
    // clients report that they reached a trigger, the server decides when a gate opens.
    readonly NetworkList<float> _syncGateOpenTimes = new();
    readonly List<float> _mirroredGateOpenTimes = new();

    /// <summary>
    /// Open time per drill trigger, or <see cref="DrillSegmentResolver.GATE_CLOSED"/> for a gate
    /// that has not fired. Feed straight into <see cref="DrillSegmentResolver"/>.
    /// </summary>
    public IReadOnlyList<float> GateOpenTimes => _mirroredGateOpenTimes;

    /// <summary>Index of the first gate still waiting, or -1 when all gates are open.</summary>
    [ShowInInspector, HideInEditorMode]
    public int WaitingGateIndex {
        get {
            for (int i = 0; i < _mirroredGateOpenTimes.Count; i++)
                if (_mirroredGateOpenTimes[i] < 0f)
                    return i;
            return -1;
        }
    }

    public bool IsGateOpen(int idx) =>
        idx >= 0 && idx < _mirroredGateOpenTimes.Count && _mirroredGateOpenTimes[idx] >= 0f;

    bool _serverWaitingForStartPosition;
    bool _serverDrillEndedRaised;

    /// <summary>Server side: the drill is rewound and waiting for a headset to take its spot.</summary>
    [ShowInInspector, HideInEditorMode]
    public bool IsWaitingForStartPosition => _serverWaitingForStartPosition;

    /// <summary>
    /// Server side: raised once when the running drill passes its last frame. Whoever owns the
    /// order of the drills listens to this and activates the next one.
    /// </summary>
    public event Action Server_OnDrillEnded;

    CustomLogger _logger;
    [SerializeField, Get] NetDrillsActivator _netDrillActivator;
    [ShowInInspector]
    public float AnimationTime { 
        get {
            if (PlaybackState.Speed == 0f)
                return PlaybackState.Time; 

            double elapsed = NetworkManager.Singleton ? 
                NetworkManager.Singleton.ServerTime.Time - PlaybackState.SyncTime : 0;
            return PlaybackState.Time + (float)(elapsed * PlaybackState.Speed);
        }
        set {
            var state = PlaybackState;
            state.Time = value;
            state.SyncTime = NetworkManager.Singleton ? NetworkManager.Singleton.ServerTime.Time : 0;
            PlaybackState = state;
        }
    }
    [ShowInInspector, HideInEditorMode]
    public bool IsPlaying {  // PLAY OR PAUSE
        get => PlaybackState.Speed != 0f; 
        set {
            if (value)
                Play(); 
            else Pause();
        }
    }
    public void Play() => SetSpeed(1f);
    public void Pause() => SetSpeed(0f);

    /// <summary>
    /// Coach override: starts the synchronized countdown whether or not a headset has taken
    /// its spot. Calling this on an already-running drill keeps the old resume behaviour.
    /// </summary>
    public void Server_StartNow() {
        if (!IsServer) {
            _logger.LogError("Attempted to start the drill on a client. This is not allowed.");
            return;
        }

        if (IsCountdownActive)
            return;

        // Debug controls also use this API to resume a paused drill. Only frame zero is a
        // pre-drill start and therefore needs the countdown.
        if (AnimationTime > 0f) {
            Play();
            return;
        }

        _serverWaitingForStartPosition = false;
        Pause();
        _syncCountdownEndTime.Value = CurrentNetworkTime + _countdownDuration;
        _logger.Log($"Starting drill countdown ({_countdownDuration:0.#} seconds)");
    }
    void SetSpeed(float speed) {
        if (!IsServer) {
            _logger.LogError("Attempted to set DrillPlayer Speed on client. This is not allowed.");
            return;
        }

        _logger.Log("Setting DrillPlayer Speed: " + speed);
        var state = PlaybackState;
        state.Time = AnimationTime;
        state.Speed = speed;
        state.SyncTime = NetworkManager.Singleton.ServerTime.Time;
        PlaybackState = state;
    }

    private void Awake() {
        _logger = new CustomLogger(this, Color.magenta);
        _instance = this;
        _syncGateOpenTimes.OnListChanged += OnSyncGateOpenTimesChanged;
        RebuildGateMirror();
    }

    void OnSyncGateOpenTimesChanged(NetworkListEvent<float> _) => RebuildGateMirror();

    void RebuildGateMirror() {
        _mirroredGateOpenTimes.Clear();
        for (int i = 0; i < _syncGateOpenTimes.Count; i++)
            _mirroredGateOpenTimes.Add(_syncGateOpenTimes[i]);
    }

    private void Update() {
        if (!IsServer || !IsSpawned)
            return;

        var drill = _netDrillActivator ? _netDrillActivator.ActiveManeuver.Value : null;
        var wantedCount = drill ? drill.Triggers.Count : 0;
        if (_syncGateOpenTimes.Count != wantedCount)
            Server_ResetGates();

        if (!drill)
            return;

        // Keep animation time and trigger gates frozen until the shared server timestamp is
        // reached. Clients derive their displayed digit from this same timestamp.
        if (IsCountdownActive) {
            if (CurrentNetworkTime < _syncCountdownEndTime.Value)
                return;

            _syncCountdownEndTime.Value = 0d;
            Play();
            return;
        }

        // Hold the drill on frame zero until a headset is standing on the start position.
        // Reaching it begins the same countdown used by the coach override.
        if (_serverWaitingForStartPosition) {
            if (!Server_AnyXRPlayerInStartingPosition())
                return;

            Server_StartNow();
            return;
        }

        // A gate that the player never walked into still has to open, otherwise the drill
        // stalls forever. NominalTime is the latest a gate can fire.
        var animationTime = AnimationTime;
        for (int i = 0; i < _syncGateOpenTimes.Count && i < drill.Triggers.Count; i++) {
            if (_syncGateOpenTimes[i] >= 0f)
                continue;

            var nominalTime = drill.Triggers[i].NominalTime;
            if (animationTime >= nominalTime)
                Server_OpenGate(i, nominalTime);
        }

        // The drill freezes on its last frame and reports that it is done exactly once, so a
        // listener that moves on to the next drill cannot be called again while it does so.
        if (IsPlaying && !_serverDrillEndedRaised && ReachedMaxAnimationTime()) {
            _serverDrillEndedRaised = true;
            Pause();
            _logger.Log($"Drill ended at {AnimationTime:0.00}");
            Server_OnDrillEnded?.Invoke();
        }
    }

    /// <summary>
    /// Latches a gate open at <paramref name="openTime"/>. A gate that is already open is left
    /// alone, so a player walking back and forth over a trigger cannot restart a segment.
    /// </summary>
    public void Server_OpenGate(int idx, float openTime) {
        if (!IsServer) {
            _logger.LogError("Attempted to open a drill gate on a client. This is not allowed.");
            return;
        }

        if (idx < 0 || idx >= _syncGateOpenTimes.Count)
            return;

        if (_syncGateOpenTimes[idx] >= 0f)
            return;

        _syncGateOpenTimes[idx] = Mathf.Max(0f, openTime);
        _logger.Log($"Drill gate [{idx}] opened at {openTime:0.00}");
    }

    public void Server_OpenGate(int idx) => Server_OpenGate(idx, AnimationTime);

    /// <summary>Coach override: opens the gate the drill is currently waiting on.</summary>
    [Button, ShowIf(nameof(IsServer))]
    public void Server_ForceOpenNextGate() {
        var idx = WaitingGateIndex;
        if (idx >= 0)
            Server_OpenGate(idx);
    }

    /// <summary>Shuts every gate and resizes the list to the active drill's trigger count.</summary>
    public void Server_ResetGates() {
        if (!IsServer || !IsSpawned)
            return;

        var drill = _netDrillActivator ? _netDrillActivator.ActiveManeuver.Value : null;
        var triggerCount = drill ? drill.Triggers.Count : 0;

        _syncGateOpenTimes.Clear();
        for (int i = 0; i < triggerCount; i++)
            _syncGateOpenTimes.Add(DrillSegmentResolver.GATE_CLOSED);
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if(IsServer)
            _netDrillActivator.ActiveManeuver.Sub(Server_DrillActivator_OnDrillChange);
    }

    public bool ReachedMaxAnimationTime() {
        if (!IsSpawned)
            return false;

        var activeDrill = _netDrillActivator.ActiveManeuver.Value;
        if (!activeDrill)
            return false;

        var maxAnimationLength = Mathf.Max(0.01f,
            DrillSegmentResolver.CalculateMaxAnimationTime(activeDrill, GateOpenTimes));

        return AnimationTime >= maxAnimationLength;
    } 

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        if (IsServer)
            _netDrillActivator.ActiveManeuver.Unsub(Server_DrillActivator_OnDrillChange);
    }
    private void Server_DrillActivator_OnDrillChange(DrillData _) => DrillActivator_OnDrillChange(); 
    private void DrillActivator_OnDrillChange() => ResetTimeAndPlay();

    /// <summary>
    /// Rewinds the drill and holds it until a headset reports it is on the start position.
    /// With no headset connected there is nobody to wait for and it starts immediately.
    /// </summary>
    public void ResetTimeAndPlay() {
        Server_ResetGates();
        if (IsServer)
            _syncCountdownEndTime.Value = 0d;
        AnimationTime = 0;
        _serverWaitingForStartPosition = true;
        _serverDrillEndedRaised = false;
        IsPlaying = false;
    }

    /// <summary>
    /// Whether any headset is on the start position. Returns true when no headset has spawned,
    /// so a coach testing on their own is never blocked.
    /// </summary>
    bool Server_AnyXRPlayerInStartingPosition() {
        var netManager = NetworkManager.Singleton;
        if (!netManager)
            return true;

        var anyXRPlayer = false;
        foreach (var client in netManager.ConnectedClientsList) {
            var playerObject = client.PlayerObject;
            if (!playerObject || !playerObject.TryGetComponent<NetSpawnedXRData>(out var xrData))
                continue;
            if (!xrData.IsXRPlayer.Value)
                continue;

            anyXRPlayer = true;
            if (xrData.IsInStartingPosition.Value)
                return true;
        }
        return !anyXRPlayer;
    }
     
    public override void OnDestroy() {
        base.OnDestroy();
        _syncGateOpenTimes.OnListChanged -= OnSyncGateOpenTimesChanged;
        if (_instance == this)
            _instance = null;
    }

}
