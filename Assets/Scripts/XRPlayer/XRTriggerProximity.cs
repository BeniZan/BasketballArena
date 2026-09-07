using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Watches the headset against every trigger of the active drill and reports the first entry
/// into each one to the server, which is what opens that trigger's gate. Distance is compared
/// on XZ only, like the start position check, and the exit radius is wider than the entry
/// radius so that standing on the edge of a trigger cannot flicker it on and off.
///
/// Lives on the Local XR Device prefab next to <see cref="XRDrillActivator"/>, so it only runs
/// on headsets and never on the coach's device.
/// </summary>
public class XRTriggerProximity : MonoBehaviour {
    [SerializeField, Min(0.1f)] float _enterRadius = 0.75f;
    [SerializeField, Min(0.1f)] float _exitRadius = 1.1f;
    [SerializeField] bool _showMarkers = true;

    static readonly Color _ClosedColor = new Color(0.55f, 0.55f, 0.6f);
    static readonly Color _WaitingColor = new Color(1f, 0.8f, 0.15f);
    static readonly Color _OpenColor = new Color(0.25f, 0.85f, 0.4f);

    [ShowInInspector, ReadOnly, HideInEditorMode] DrillData _trackedDrill;
    readonly List<bool> _isInside = new();
    readonly List<Transform> _markers = new();
    readonly List<Renderer> _markerRenderers = new();

    private void Update() {
        var activator = XRDrillActivator.Instance;
        var drill = activator ? activator.CurrentDrill : null;
        var origin = activator ? activator.DrillOrigin : null;
        var calibration = Calibration.Instance;

        if (!drill || !origin || !calibration || !calibration.IsDoneCalibration) {
            if (_trackedDrill)
                Track(null, null);
            return;
        }

        if (drill != _trackedDrill)
            Track(drill, origin);

        var drillPlayer = DrillPlayer.Instance;
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

            PaintMarker(i, drillPlayer);
        }
    }

    void Track(DrillData drill, Transform origin) {
        _trackedDrill = drill;
        _isInside.Clear();
        ClearMarkers();

        if (!drill)
            return;

        for (int i = 0; i < drill.Triggers.Count; i++) {
            _isInside.Add(false);
            if (_showMarkers)
                CreateMarker(drill.Triggers[i], origin);
        }
    }

    void CreateMarker(DrillTrigger trigger, Transform origin) {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Trigger Marker - " + trigger.Name;
        if (marker.TryGetComponent<Collider>(out var markerCollider))
            markerCollider.SafeDestroy();

        var tf = marker.transform;
        tf.parent = origin;
        tf.SetLocalPositionAndRotation(trigger.LocalPosition, Quaternion.identity);
        // A cylinder is 1 unit across and 2 units tall at scale 1, so this is a disc of
        // entry radius, a centimeter thick, lying on the floor.
        tf.localScale = new Vector3(_enterRadius * 2f, 0.01f, _enterRadius * 2f);

        _markers.Add(tf);
        _markerRenderers.Add(marker.GetComponent<Renderer>());
    }

    void PaintMarker(int idx, DrillPlayer drillPlayer) {
        if (idx >= _markerRenderers.Count)
            return;

        var markerRenderer = _markerRenderers[idx];
        if (!markerRenderer)
            return;

        if (drillPlayer && drillPlayer.IsGateOpen(idx))
            markerRenderer.material.color = _OpenColor;
        else if (drillPlayer && drillPlayer.WaitingGateIndex == idx)
            markerRenderer.material.color = _WaitingColor;
        else
            markerRenderer.material.color = _ClosedColor;
    }

    void ClearMarkers() {
        foreach (var marker in _markers)
            if (marker)
                marker.SafeDestroy();
        _markers.Clear();
        _markerRenderers.Clear();
    }

    private void OnDisable() {
        Track(null, null);
    }
}
