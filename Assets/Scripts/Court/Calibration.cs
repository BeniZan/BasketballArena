using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Calibration : SingletonBehaviors.SingletonMono<Calibration> {
    public enum Step { NotCalibrated = -1, 
        CalibratingCenter, CalibratingCenterCorner, CalibratingBasketCorner,
        Calibrated 
    }
    [NonSerialized, ShowInInspector, HideReferenceObjectPicker] 
    public Notifier<Step> CalibrationStep = new Notifier<Step>(Step.NotCalibrated);
    [SerializeField] PlaceWithPinch[] _placers;
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _pinchLine;
    [SerializeField] SurfaceHandler _courtHalfSurface, _courtHalfSurfacePreview;
    [SerializeField] LineRenderer _inBetweenPlacersLine;
    public SurfaceHandler CourtHalfSurface => _courtHalfSurface;
    public float MinPinchForLine = 0.2f, PinchThreshold = 0.85f;
    Awaitable _calibrationAwait;
    CustomLogger _logger; 
    public IReadOnlyList<PlaceWithPinch> Placers => _placers;

    PlaceWithPinch GetPlacer(Step s) {
        var idx = (int)s;
        if (_placers.ValidIndex(idx))
            return _placers[idx];
        return null;
    }
    [ShowInInspector, PropertyOrder(-100)]
    public bool IsCalibrating => 
        (int)CalibrationStep.Value > (int)Step.NotCalibrated && (int)CalibrationStep.Value < (int)Step.Calibrated;
    public bool IsDoneCalibration => CalibrationStep.Value == Step.Calibrated;

    public PlaceWithPinch CurrentPlacer {
        get {
            if (IsCalibrating) {
                return _placers[(int)CalibrationStep.Value];
            }
            return null;
        }
    }
    protected override void Awake() {
        base.Awake();
        _logger = new CustomLogger(this, Color.green);  
        _calibrationAwait = BeginCalibration();
#if UNITY_EDITOR
        CreateSurface(_test_p1, _test_p2, _test_p3);
#endif
    }

    public const int LENGTH_AXIS = 0; //x
    public const int UNUSED_AXIS = 1; //y
    public const int WIDTH_AXIS = 2;  //z
    static public Vector3 WIDTH_AXIS_V3 => new Vector3 { [WIDTH_AXIS] = 1f };
    static public Vector3 LENGTH_AXIS_V3 => new Vector3 { [LENGTH_AXIS] = 1f };
#if UNITY_EDITOR
    [SerializeField, BoxGroup("Auto Calibrate")] bool _editorAutoCalibrate;
    [SerializeField, BoxGroup("Auto Calibrate")] float _editorCalibrateWidth = 15f;
    [SerializeField, BoxGroup("Auto Calibrate")] float _editorCalibrateLength = 28f;

    [SerializeField, BoxGroup("Surface Test")] 
    Vector3 _test_p1, _test_p2, _test_p3;

    private async Awaitable Start() {
        if (!_editorAutoCalibrate)
            return;

        await Awaitable.EndOfFrameAsync();
        await Awaitable.NextFrameAsync();
        await Awaitable.EndOfFrameAsync();
        AutoCalibration();
    }

    [Button("Auto Calibrate"), HideInEditorMode]
    void AutoCalibration() {
        var playerPos = _xrPlayer.HeadCam.transform.position - (Vector3.up * 1.2f);

        var rndRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
        var bottomCenter = playerPos + (rndRotation * WIDTH_AXIS_V3 * _editorCalibrateWidth /2f);
        var userPoint = bottomCenter + (rndRotation * LENGTH_AXIS_V3 * _editorCalibrateLength /2f);
        var surface = CreateSurface(playerPos, bottomCenter, userPoint);
        _courtHalfSurface.SetSurface(surface);
        SetState(Step.Calibrated);
    }

#endif

    void SetState(Step step) {
        _logger.Log($"changing calibration step {CalibrationStep.Value}->{step}");
        var stepInt = (int)step;
        for (int i = 0; i < _placers.Length; i++) {
            _placers[i].IsPlacing = (i == stepInt);
        }
        _courtHalfSurface.gameObject.SetActive(IsDoneCalibration);
        CalibrationStep.Value = step;
    }
    async Awaitable BeginCalibration() {
        if (_calibrationAwait != null && !_calibrationAwait.IsCompleted) {
            Debug.LogError("tried calibrating while already calibrating", this);
            return;
        }
        await Awaitable.EndOfFrameAsync();
        SetState(Step.NotCalibrated + 1); 
        try { 
            _logger.Log("Calibration Started"); 
            while (IsCalibrating) { 
                UpdateInBetweenLine();

                var lastStep = Step.Calibrated - 1;
                var canShowPreviewSurface = CalibrationStep.Value >= lastStep;
                var lastStepPlacer = GetPlacer(lastStep);
                var canShowSurface = canShowPreviewSurface && lastStepPlacer.WasPlaced;
                _courtHalfSurface.gameObject.SetActive(canShowSurface);
                _courtHalfSurfacePreview.gameObject.SetActive(canShowPreviewSurface); 

                if (canShowSurface)
                    UpdateSurface(_courtHalfSurface, lastStepPlacer.PlacedObj.position); 
                if(canShowPreviewSurface)
                    UpdateSurface(_courtHalfSurfacePreview, lastStepPlacer.PreviewObj.position);
                await Awaitable.NextFrameAsync();
            } 
        } 
        catch(System.Exception ex) { Debug.LogException(ex); } 
        finally {
            _courtHalfSurfacePreview.gameObject.SetActive(IsCalibrating);
            CalibrationStep.Value = Step.Calibrated;
            _calibrationAwait = null;
            _logger.Log("Calibration Done");
        }
    }

    Vector3[] _tempLine = new Vector3[(int)Step.Calibrated];
    void UpdateInBetweenLine() {
        var calibStep = (int)CalibrationStep.Value;
        _inBetweenPlacersLine.enabled = calibStep >= 1;
        if (!_inBetweenPlacersLine.enabled)  
            return;
        var pointCount = calibStep + 1;
        if (_inBetweenPlacersLine.positionCount != pointCount)
            _inBetweenPlacersLine.positionCount = pointCount; 
        for (int i = 0; i < pointCount; i++) {
            _tempLine[i] = _placers[i].PreviewOrPlacedPosition;
        } 

        if(pointCount == 3) {
            _tempLine[2] = _placers[2].PreviewOrPlacedPosition;
            var surface = 
                CreateSurface(_tempLine[0], _tempLine[1], _tempLine[2]);
            var topCorner =
                surface.Center + (surface.Rotation * surface.Size / 2f);
            _tempLine[2] = topCorner;
            _tempLine[2].y = _tempLine[1].y = _tempLine[0].y;
        }

        _inBetweenPlacersLine.SetPositions(_tempLine);
    }   
    void UpdateSurface(SurfaceHandler surface, Vector3 basketCornerPos) { 
        var center = GetPlacer(Step.CalibratingCenter).PlacedObj.position;
        var centerCorner = GetPlacer(Step.CalibratingCenterCorner).PlacedObj.position;
        //basketCornerPos.y = centerCorner.y = center.y;
        var surfaceDat = CreateSurface(center, centerCorner, basketCornerPos);
        surface.SetSurface(surfaceDat);
    }

    public static SurfaceData CreateSurface(
        Vector3 p1,
        Vector3 p2,
        Vector3 p3) {
        // Length axis
        Vector3 forw =  - (p2 - p1).normalized;

        // Width
        float width = Vector3.Distance(p1, p2) * 2f;

        // Perpendicular axis on floor
        Vector3 right = Vector3.Cross(forw, Vector3.up).normalized;

        Debug.DrawRay(p2, right);
        var rawP1toP3 = p3 - p1;

        // Ensure forward points toward p3, not away from it
        if (Vector3.Dot(rawP1toP3, right) < 0f)
            right = -right;

        float length = Vector3.Project(rawP1toP3, right).magnitude;

        // Center of rectangle
        Vector3 center = p1 + right * (length * 0.5f);
        var rotation = Quaternion.LookRotation(forw, Vector3.up);

        return new SurfaceData {
            Center = center,
            Size = new Vector3() { [LENGTH_AXIS] = length, [WIDTH_AXIS] = width, [UNUSED_AXIS] = 1f },
            Forward = right,
            Rotation = rotation
        };

    } 

    public void OnConfirmedCalibrationStep() {
        _logger.Log("Confirmed " + CalibrationStep.Value);
        SetState(CalibrationStep.Value + 1);
    }

    public void Backtrack() {
        _logger.Log("Backtracking " + CalibrationStep.Value);
        SetState(CalibrationStep.Value - 1);
    } 
} 
