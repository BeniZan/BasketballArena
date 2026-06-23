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
            _placers[i].gameObject.SetActive(i == stepInt);
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
                var canShowPreviewSurface = CalibrationStep.Value > lastStep;
                var lastStepPlacer = GetPlacer(lastStep);
                var canShowSurface = canShowPreviewSurface && lastStepPlacer.WasPlaced;
                _courtSurface.gameObject.SetActive(canShowSurface);
                _courtSurfacePreview.gameObject.SetActive(canShowPreviewSurface);
                if(canShowSurface)
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
        if(_inBetweenPlacersLine.positionCount != (int)CalibrationStep.Value)
            _inBetweenPlacersLine.positionCount = (int)CalibrationStep.Value;
        _inBetweenPlacersLine.enabled = _inBetweenPlacersLine.positionCount > 1;
        for (int i = 0; i < (int)CalibrationStep.Value; i++) {
            _tempLine[i] =
                 _placers[i].WasPlaced ? _placers[i].PlacedObj.position :
                 _placers[i].PreviewObj.position;
        } 
        _inBetweenPlacersLine.SetPositions(_tempLine);
    }

    void UpdateSurface(Transform surface, Vector3 basketCornerPos) {
        var canShowSurface = CalibrationStep.Value >= (Step.Calibrated - 1);
        _courtSurface.gameObject.SetActive(canShowSurface);
        _courtSurfacePreview.gameObject.SetActive(canShowSurface);
        if (!canShowSurface)
            return;
         
        _courtSurface.gameObject.SetActive(CalibrationStep.Value == Step.Calibrated);
        _courtSurfacePreview.gameObject.SetActive(CalibrationStep.Value != Step.Calibrated);

        var center = GetPlacer(Step.CalibratingCenter).PlacedObj.position;
        var centerCorner = GetPlacer(Step.CalibratingCenterCorner).PlacedObj.position;
        basketCornerPos.y = centerCorner.y = center.y;
        surface.position = center;

        Vector3 basketPos = FindFourthVertex(center, centerCorner, basketCornerPos); 

        var forw = center.DirectionTo(basketPos);
        var right = center.DirectionTo(centerCorner);
        var up = Vector3.Cross(forw, right);

        var length = Vector3.Distance(basketCornerPos, centerCorner);
        var width = Vector3.Distance(center, centerCorner); 
        surface.LookAt(forw, up);
        surface.localScale = new Vector3(width, 0f, length);  
    }

    public static Vector3 FindFourthVertex(Vector3 a, Vector3 b, Vector3 c) {
        // Calculate the vectors between the points
        Vector3 ab = b - a;
        Vector3 bc = c - b;
        Vector3 ca = a - c;
        if (Mathf.Abs(Vector3.Dot(ab, ca)) < 1e-5f)  
            return b + c - a; 
        if (Mathf.Abs(Vector3.Dot(ab, bc)) < 1e-5f)  
            return a + c - b;
         return a + b - c;
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
