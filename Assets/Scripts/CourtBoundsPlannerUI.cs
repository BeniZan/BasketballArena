using TMPro;
using UnityEngine;

public class CourtBoundsPlannerUI : MonoBehaviour { 
    [SerializeField] TextMeshProUGUI _txt;
    [SerializeField] SequanceTriggerSpawner _boundsPlanner;
    private void Awake() {
        _boundsPlanner.OnSpawnerChanged += BoundsPlanner_OnSpawnerChanged;
    }
    private void OnDestroy() {
        _boundsPlanner.OnSpawnerChanged -= BoundsPlanner_OnSpawnerChanged;
    }
    private void BoundsPlanner_OnSpawnerChanged() {
        gameObject.SetActive(_boundsPlanner.IsPlanning);
        if (_boundsPlanner.IsPlanning) {
            _txt.text = "Placing " + _boundsPlanner.Spawners[_boundsPlanner.CurrentSpawnerIdx].SpawnerUILabel;
        }
    }
}
