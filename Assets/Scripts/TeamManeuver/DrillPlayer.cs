using SingletonBehaviors;
using System;
using UnityEngine;

public class DrillPlayer : SingletonMono<DrillPlayer> {
    public TeamManeuverData CurrentPlayingManeuverData => NetTeamManeuverManager.Instance.ActiveManeuver;
    [SerializeField] DrillActivator _maneuverPlacer;
     
    public bool IsPlaying; // PLAY OR PAUSE
    public float AnimationTime;
    protected override void Awake() { 
        base.Awake();
        _maneuverPlacer.OnManuverPlaced += ManeuverPlacer_OnManuverPlaced;
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
            foreach (var c in _maneuverPlacer.PlacedChars)
                c.SetAnimationTime(AnimationTime);
        }
    }

}
