using SingletonBehaviors;
using System;
using UnityEngine;

public class DrillPlayer : SingletonMono<DrillPlayer> {
    public DrillData CurrentPlayingDrillData => NetTeamManeuverManager.Instance.ActiveManeuver;
    [SerializeField] DrillSurfaceActivator _drillActivator;
     
    public bool IsPlaying; // PLAY OR PAUSE
    public float AnimationTime;
    protected override void Awake() { 
        base.Awake();
        _drillActivator.OnDrillChange += ManeuverPlacer_OnManuverPlaced;
    }
    private void ManeuverPlacer_OnManuverPlaced() { 
        AnimationTime = 0;
        IsPlaying = false;
    }

    public void RestartAndPause() {
        AnimationTime = 0;
        IsPlaying = false;
    }

    private void Update() {
        if (IsPlaying) {
            AnimationTime += Time.deltaTime;
            foreach (var c in _drillActivator.PlacedChars)
                c.SetAnimationTime(AnimationTime);
        }
    }

}
