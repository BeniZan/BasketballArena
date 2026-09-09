using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Watches the headset against every trigger of the active drill and reports the first entry
/// into each one to the server, which is what opens that trigger's gate. Distance is compared
/// on XZ only, like the start position check, and the exit radius is wider than the entry
/// radius so that standing on the edge of a trigger cannot flicker it on and off.
///
/// Lives on the headset's camera so <c>transform.position</c> is the player's position. The
/// visible markers belong to <see cref="XRDrillActivator"/>, which spawns them under the drill
/// origin alongside the characters.
/// </summary>
public class XRTriggerProximity : MonoBehaviour {
    [SerializeField, Min(0.1f)] float _enterRadius = 0.75f;
    [SerializeField, Min(0.1f)] float _exitRadius = 1.1f;

    [ShowInInspector, ReadOnly, HideInEditorMode] DrillData _trackedDrill;
    readonly List<bool> _isInside = new();

    private void Update() {
        var activator = XRDrillActivator.Instance;
        var drill = activator ? activator.CurrentDrill : null;
        var origin = activator ? activator.DrillOrigin : null;
        var calibration = Calibration.Instance;

        if (!drill || !origin || !calibration || !calibration.IsDoneCalibration) {
            if (_trackedDrill)
                ResetTracking(null);
            return;
        }

        if (drill != _trackedDrill)
            ResetTracking(drill);

        var netData = NetSpawnedXRData.Local;
        var positionXZ = transform.position.XZ();

        for (int i = 0; i < drill.Triggers.Count && i < _isInside.Count; i++) {
            var worldPosXZ = origin.TransformPoint(drill.Triggers[i].LocalPosition).XZ();
            var distance = worldPosXZ.Distance(positionXZ);

            var wasInside = _isInside[i];
            var isInside = distance <= (wasInside ? _exitRadius : _enterRadius);
            _isInside[i] = isInside;

            if (isInside && !wasInside && netData)
                netData.ReportReachedTrigger_Rpc(i, drill.name);
        }
    }

    void ResetTracking(DrillData drill) {
        _trackedDrill = drill;
        _isInside.Clear();

        if (!drill)
            return;

        for (int i = 0; i < drill.Triggers.Count; i++)
            _isInside.Add(false);
    }

    private void OnDisable() {
        ResetTracking(null);
    }
}
