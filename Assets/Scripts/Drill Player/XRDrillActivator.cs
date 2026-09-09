using SingletonBehaviors;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

public class XRDrillActivator : SingletonMono<XRDrillActivator> { 
    [SerializeField] CharComponent _templateChar;
    [SerializeField] Transform _templateTriggerMarker;
    [SerializeField, ReadOnly] List<CharComponent> _spawnedChars = new List<CharComponent>();
    [ShowInInspector, ReadOnly, HideInEditorMode] DrillData _currentActive;
    [ShowInInspector, HideInEditorMode, ReadOnly] Transform _courtCenter, _drillOrigin;

    static readonly Color _MarkerClosedColor = new Color(0.55f, 0.55f, 0.6f);
    static readonly Color _MarkerWaitingColor = new Color(1f, 0.8f, 0.15f);
    static readonly Color _MarkerOpenColor = new Color(0.25f, 0.85f, 0.4f);
    static readonly Color _StartMarkerReachedColor = new Color(0.2f, 0.55f, 1f);

    readonly List<Transform> _triggerMarkers = new();
    readonly List<Renderer> _triggerMarkerRenderers = new();
    Transform _startMarker;
    Renderer _startMarkerRenderer;

    public Transform DrillOrigin => _drillOrigin;
    public DrillData CurrentDrill => _currentActive;

    public IReadOnlyList<CharComponent> PlacedChars => _spawnedChars;
    public IReadOnlyList<Transform> TriggerMarkers => _triggerMarkers;
    protected override void Awake() {
        base.Awake(); 
    } 
    public void Activate(DrillData move) {
        if (_currentActive)
            Deactivate();
        if (move) 
            _currentActive = move;
        UpdateChars();
    }
    public void UpdateChars() {
        if (!_currentActive)
            return;

        var calib = Calibration.Instance;
        if (!calib) {
            Debug.LogError("Calibration instance is null. Make sure Calibration script is present in the scene.");
            return;
        } 

        if (!_courtCenter) { 
            _courtCenter = new GameObject("CourtCenter").transform; 
            _courtCenter.parent = transform;  
        }

        if (!_drillOrigin) {
            _drillOrigin = new GameObject("Drill Origin").transform;
            _drillOrigin.parent = _courtCenter;
        }

        var courtHalfSurface = calib.CourtHalfSurface.ScalingTransform;
        var courtCenter = courtHalfSurface.TransformPoint(Calibration.LENGTH_AXIS_V3 * -0.5f);
        var courtRotation = calib.CourtHalfSurface.Surface.Rotation;
        _courtCenter.SetPositionAndRotation(courtCenter, courtRotation);

        var localOriginPoint = _currentActive.OriginPoint;
        var localOriginRotation = Quaternion.Euler(0f, _currentActive.OriginYRotation, 0f);
        // Matches the exporter, which spins the court around so a mirrored drill lands where
        // it was authored. Without it the characters and trigger points drift apart.
        if (_currentActive.MirrorLeftRight)
            localOriginRotation *= Quaternion.Euler(0f, 180f, 0f);
        _drillOrigin.SetLocalPositionAndRotation(localOriginPoint, localOriginRotation);

        int i = 0;
        for (; i < _currentActive.CharsData.Count; i++) {
            if (_spawnedChars.Count <= i) {
                var spawned = Instantiate(_templateChar, _drillOrigin);
                spawned.gameObject.SetActive(true); 
                _spawnedChars.Add(spawned);
            }
            _spawnedChars[i].SetData(_currentActive.CharsData[i], _currentActive.MirrorLeftRight);
        }
        while(i < _spawnedChars.Count) {
            if (_spawnedChars[i])
                _spawnedChars[i].gameObject.SafeDestroy();
            _spawnedChars.RemoveAt(i);
        }

        UpdateTriggerMarkers();
    }

    // Markers are spawned and placed exactly like the characters: instantiated from a template
    // under the drill origin, then given their authored position in that origin's local space.
    // That way they inherit whatever the court transform does to the drill, at the same ratio
    // as the players standing next to them.
    void UpdateTriggerMarkers() {
        if (!_templateTriggerMarker) {
            Debug.LogError("Trigger marker template is not assigned on " + nameof(XRDrillActivator), this);
            return;
        }

        var triggers = _currentActive.Triggers;

        int i = 0;
        for (; i < triggers.Count; i++) {
            if (_triggerMarkers.Count <= i)
                SpawnTriggerMarker();

            var marker = _triggerMarkers[i];
            if (!marker)
                continue;

            marker.name = "Trigger Marker - " + triggers[i].Name;
            marker.SetLocalPositionAndRotation(triggers[i].LocalPosition, Quaternion.identity);
        }
        while (i < _triggerMarkers.Count) {
            if (_triggerMarkers[i])
                _triggerMarkers[i].SafeDestroy();
            _triggerMarkers.RemoveAt(i);
            _triggerMarkerRenderers.RemoveAt(i);
        }

        UpdateStartMarker();
    }

    void SpawnTriggerMarker() {
        var marker = Instantiate(_templateTriggerMarker, _drillOrigin);
        marker.gameObject.SetActive(true);

        _triggerMarkers.Add(marker);
        _triggerMarkerRenderers.Add(marker.GetComponentInChildren<Renderer>());
    }

    // The drill is held on frame zero until a headset stands on the player's start position,
    // so that spot gets a marker of its own, from the same template as the triggers.
    void UpdateStartMarker() {
        if (!_startMarker) {
            _startMarker = Instantiate(_templateTriggerMarker, _drillOrigin);
            _startMarker.gameObject.SetActive(true);
            _startMarkerRenderer = _startMarker.GetComponentInChildren<Renderer>();
        }

        _startMarker.name = "Player Start Marker";
        _startMarker.SetLocalPositionAndRotation(_currentActive.LocalPlayerStartPosition, Quaternion.identity);
    }

    void PaintTriggerMarkers(DrillPlayer drillPlayer) {
        var waitingIdx = drillPlayer ? drillPlayer.WaitingGateIndex : -1;

        for (int i = 0; i < _triggerMarkerRenderers.Count; i++) {
            var markerRenderer = _triggerMarkerRenderers[i];
            if (!markerRenderer)
                continue;

            var color = _MarkerClosedColor;
            if (drillPlayer && drillPlayer.IsGateOpen(i))
                color = _MarkerOpenColor;
            else if (i == waitingIdx)
                color = _MarkerWaitingColor;

            if (markerRenderer.material.color != color)
                markerRenderer.material.color = color;
        }

        if (_startMarkerRenderer) {
            var startColor = drillPlayer && drillPlayer.IsWaitingForStartPosition
                ? _MarkerWaitingColor
                : _StartMarkerReachedColor;
            if (_startMarkerRenderer.material.color != startColor)
                _startMarkerRenderer.material.color = startColor;
        }
    }

    private void Update() {
        var drillPlayer = DrillPlayer.Instance;
        if (!drillPlayer) {
            Debug.LogError("DrillPlayer instance is null. Make sure DrillPlayer script is present in the scene.");
            return;
        }

        var netDrillActivator = NetDrillsActivator.Instance;
        if(!netDrillActivator) {
            Debug.LogError("NetDrillsActivator instance is null. Make sure NetDrillsActivator script is present in the scene.");
            return;
        }

        if(netDrillActivator.ActiveManeuver.Value != _currentActive) {
            Activate(netDrillActivator.ActiveManeuver.Value);
        }

        if (!_currentActive)
            return;

        PaintTriggerMarkers(drillPlayer);

        var gateOpenTimes = drillPlayer.GateOpenTimes;
        var animationTime = drillPlayer.AnimationTime;
        foreach (var c in PlacedChars) {
            if (!c || c.Data == null)
                continue;

            DrillSegmentResolver.ResolveSegment(_currentActive, c.Data, animationTime, gateOpenTimes,
                                                out var clip, out var localTime);
            c.SetSegment(clip, localTime);
        }
    }
    public void Deactivate() {
        foreach (var placedChar in _spawnedChars)
            if(placedChar)
                placedChar.gameObject.SafeDestroy();
        _spawnedChars.Clear();

        foreach (var marker in _triggerMarkers)
            if (marker)
                marker.SafeDestroy();
        _triggerMarkers.Clear();
        _triggerMarkerRenderers.Clear();

        if (_startMarker)
            _startMarker.SafeDestroy();
        _startMarker = null;
        _startMarkerRenderer = null;

        _currentActive = null;
    }
    private void OnEnable() {
        Activate(_currentActive);
    }
    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        GizmosU.GizmosArrow(transform.position, transform.rotation.EulerSeperateY() * Vector3.forward);
    } 
}
