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
    [SerializeField, ReadOnly] List<CharComponent> _spawnedChars = new List<CharComponent>();
    [ShowInInspector, ReadOnly, HideInEditorMode] DrillData _currentActive;
    [ShowInInspector, HideInEditorMode, ReadOnly] Transform _courtCenter, _drillOrigin;

    public Transform DrillOrigin => _drillOrigin;
    public DrillData CurrentDrill => _currentActive;

    public IReadOnlyList<CharComponent> PlacedChars => _spawnedChars;
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
