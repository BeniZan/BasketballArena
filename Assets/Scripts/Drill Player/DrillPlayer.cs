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
    [SerializeField] DrillActivator _drillActivator;

    public struct AnimationPlaybackState : INetworkSerializeByMemcpy {
        public float Time;          // animation time at SyncTime
        public float Speed;         // 0 = paused
        public double SyncTime;     // NetworkManager.ServerTime.Time
    }

    NetworkVariable<AnimationPlaybackState> SyncState 
        = new NetworkVariable<AnimationPlaybackState>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
    public AnimationPlaybackState PlaybackState { get => SyncState.Value; set => SyncState.Value = value; }
    CustomLogger _logger;
    [ShowInInspector]
    public float AnimationTime { 
        get {
            if (PlaybackState.Speed == 0f)
                return PlaybackState.Time;

            double elapsed = NetworkManager.Singleton.ServerTime.Time - PlaybackState.SyncTime;
            return PlaybackState.Time + (float)(elapsed * PlaybackState.Speed);
        }
        set {
            var state = PlaybackState;
            state.Time = value;
            state.SyncTime = NetworkManager.Singleton.ServerTime.Time;
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
        _drillActivator.OnDrillChange += DrillActivator_OnDrillChange;
    }

    private void DrillActivator_OnDrillChange() { 
        AnimationTime = 0;
        IsPlaying = true;
    }

    public void RestartAndPause() {
        AnimationTime = 0;
        IsPlaying = false;
    }

    private void Update() {
        foreach (var c in _drillActivator.PlacedChars)
            c.SetAnimationTime(AnimationTime); 
    }

    public override void OnDestroy() {
        base.OnDestroy();
        if (_instance == this)
            _instance = null;
    }

}
