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
    [SerializeField] SurfaceHandler _courtSurface, _courtSurfacePreview;
    [SerializeField] LineRenderer _inBetweenPlacersLine;
    public SurfaceHandler CourtSurface => _courtSurface;
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
    } 

#if UNITY_EDITOR
    [SerializeField, BoxGroup("Auto Calibrate")] bool _editorAutoCalibrate;
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
        var surface = CreateSurface(playerPos, playerPos + Vector3.forward * 10, playerPos + Vector3.right * 7);
        _courtSurface.SetSurface(surface);
        SetState(Step.Calibrated);
    }

#endif

    void SetState(Step step) {
        _logger.Log($"changing calibration step {CalibrationStep.Value}->{step}");
        var stepInt = (int)step;
        for (int i = 0; i < _placers.Length; i++) {
            _placers[i].IsPlacing = (i == stepInt);
        }
        _courtSurface.gameObject.SetActive(step == Step.Calibrated);
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
                _courtSurface.gameObject.SetActive(canShowSurface);
                _courtSurfacePreview.gameObject.SetActive(canShowPreviewSurface); 

                if (canShowSurface)
                    UpdateSurface(_courtSurface, lastStepPlacer.PlacedObj.position); 
                if(canShowPreviewSurface)
                    UpdateSurface(_courtSurfacePreview, lastStepPlacer.PreviewObj.position);
                await Awaitable.NextFrameAsync();
            } 
        } 
        catch(System.Exception ex) { Debug.LogException(ex); } 
        finally {
            _courtSurfacePreview.gameObject.SetActive(IsCalibrating);
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
            _tempLine[2].y = _tempLine[1].y = _tempLine[0].y;
            var surface = 
                CreateSurface(_tempLine[0], _tempLine[1], _tempLine[2]);
            var topCorner =
                surface.Center + (surface.Rotation * surface.SizeXZ.XZToXYZ() / 2f);
            _tempLine[2] = topCorner;
        }

        _inBetweenPlacersLine.SetPositions(_tempLine);
    }   
    void UpdateSurface(SurfaceHandler surface, Vector3 basketCornerPos) { 
        var center = GetPlacer(Step.CalibratingCenter).PlacedObj.position;
        var centerCorner = GetPlacer(Step.CalibratingCenterCorner).PlacedObj.position;
        basketCornerPos.y = centerCorner.y = center.y;
        var surfaceDat = CreateSurface(center, centerCorner, basketCornerPos);
        surface.SetSurface(surfaceDat);
    }

    public static SurfaceData CreateSurface(
        Vector3 centerBottom,
        Vector3 bottomCorner,
        Vector3 userPoint) {
        // Width axis
        Vector3 right = (bottomCorner - centerBottom).normalized;

        // Width
        float width = Vector3.Distance(centerBottom, bottomCorner) * 2f;

        // Perpendicular axis on floor
        Vector3 forward = Vector3.Cross(Vector3.up, right).normalized;

        // Vector to user's third point
        Vector3 toUser = userPoint - centerBottom;

        // Remove any width component
        Vector3 projected =
            Vector3.ProjectOnPlane(toUser, right);

        // Determine side
        if (Vector3.Dot(projected, forward) < 0f)
            forward = -forward;

        // Length is distance along forward axis only
        float length =
            Mathf.Abs(Vector3.Dot(toUser, forward));

        // Center of rectangle
        Vector3 center =
            centerBottom +
            forward * (length * 0.5f);

        return new SurfaceData {
            Center = center,
            SizeXZ = new Vector2(width, length),
            Forward = forward,
            Rotation = Quaternion.LookRotation(forward, Vector3.up)
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
