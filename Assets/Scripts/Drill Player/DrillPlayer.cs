using SingletonBehaviors;
using Sirenix.OdinInspector;
using System;
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
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if(IsServer)
            _netDrillActivator.ActiveManeuver.Sub(Server_DrillActivator_OnDrillChange);
    }

    public bool ReachedMaxAnimationTime() {
        if (!IsSpawned)
            return false;

        var maxAnimationLength = 0.01f;
        var activeDrill = _netDrillActivator.ActiveManeuver.Value;
        foreach (var data in activeDrill.CharsData) {
            maxAnimationLength = Mathf.Max(maxAnimationLength, data.Animation.length);
        }

        return AnimationTime >= maxAnimationLength;
    } 

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        if (IsServer)
            _netDrillActivator.ActiveManeuver.Sub(Server_DrillActivator_OnDrillChange);
    }
    private void Server_DrillActivator_OnDrillChange(DrillData _) => DrillActivator_OnDrillChange(); 
    private void DrillActivator_OnDrillChange() => ResetTimeAndPlay();

    public void ResetTimeAndPlay() {
        AnimationTime = 0;
        IsPlaying = true;
    }
     
    public override void OnDestroy() {
        base.OnDestroy();
        if (_instance == this)
            _instance = null;
    }

}
