using SingletonBehaviors;
using UnityEngine;

public class ManuverPlayer : SingletonMono<ManuverPlayer> {
    [SerializeField] TeamManeuverPlacer _maneuverPlacer;
    public bool IsPlaying;
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
