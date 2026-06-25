using Meta.XR; 
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; 

public class Calibration : MonoBehaviour {
    public enum Step { NotCalibrated = -1, 
        CalibratingCenter, CalibratingCenterCorner, CalibratingBasketCorner,
        Calibrated 
    }
    public Notifier<Step> CalibrationStep = new Notifier<Step>();
    [SerializeField] PlaceWithPinch[] _placers;
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _pinchLine;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] Transform _courtSurface, _courtSurfacePreview;
    [SerializeField] LineRenderer _inBetweenPlacersLine;
    public float MinPinchForLine = 0.2f, PinchThreshold = 0.85f;
    Awaitable _calibrationAwait;
    CustomLogger _logger;
    PlaceWithPinch GetPlacer(Step s) {
        var idx = (int)s;
        if (_placers.ValidIndex(idx))
            return _placers[idx];
        return null;
    }
    [ShowInInspector]
    public bool IsCalibrating => 
        (int)CalibrationStep.Value > (int)Step.NotCalibrated && (int)CalibrationStep.Value < (int)Step.Calibrated;

    public PlaceWithPinch CurrentPlacer {
        get {
            if (IsCalibrating) {
                return _placers[(int)CalibrationStep.Value];
            }
            return null;
        }
    }
    private async Awaitable Awake() {  
        _logger = new CustomLogger(this, Color.green);  
        await (_calibrationAwait = BeginCalibration());
    }

    void SetState(Step step) {
        _logger.Log($"changing calibration step {CalibrationStep}->{step}");
        var stepInt = (int)step;
        for (int i = 0; i < _placers.Length; i++) {
            _placers[i].IsPlacing = (i == stepInt);
        }
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
            var placedThirdPoint = _placers[2].PreviewOrPlacedPosition;
            _tempLine[2] = _placers[2].PreviewObj.position;
            _tempLine[2].y = _tempLine[1].y = _tempLine[0].y;
            var surface = 
                CreateSurface(_tempLine[0], _tempLine[1], _tempLine[2]);
            var topCorner =
                surface.Center + (surface.Rotation * surface.Size.XZToXYZ() / 2f);
            _tempLine[2] = topCorner;
        }

        _inBetweenPlacersLine.SetPositions(_tempLine);
    }   
    void UpdateSurface(Transform surface, Vector3 basketCornerPos) { 
        var center = GetPlacer(Step.CalibratingCenter).PlacedObj.position;
        var centerCorner = GetPlacer(Step.CalibratingCenterCorner).PlacedObj.position;
        basketCornerPos.y = centerCorner.y = center.y;
        var surfaceDat = CreateSurface(center, centerCorner, basketCornerPos);

        surface.SetPositionAndRotation(surfaceDat.Center, surfaceDat.Rotation);   
        surface.localScale = surfaceDat.Size.XZToXYZ();  
    }
    public struct SurfaceData {
        public Vector3 Center;
        public Vector2 Size;
        public Vector3 Forward;
        public Quaternion Rotation;
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
            Size = new Vector2(width, length),
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

    private void OnDisable() {
        foreach (var placer in _placers)
            placer.gameObject.SetActive(false);
    }
} 
