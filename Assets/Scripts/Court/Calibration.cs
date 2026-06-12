using Meta.XR; 
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class Calibration : MonoBehaviour {
    public enum State { NotCalibrated, CalibratingThrowLine, CalibratingBasket, Calibrated }
    public Notifier<State> CalibrationState = new Notifier<State>();
    [SerializeField] PlaceWithPinch _placeThrowline, _placeBasket; 
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _pinchLine;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] Transform _courtSurface, _courtSurfacePreview;
    public float MinPinchForLine = 0.2f, PinchThreshold = 0.85f;
    public float ThrowlineWidth = 5f;
    Awaitable _calibrationAwait;
    CustomLogger _logger;
    [ShowInInspector]
    public bool IsCalibrating => CalibrationState.Value == State.CalibratingThrowLine || CalibrationState.Value == State.CalibratingBasket;
    public PlaceWithPinch CurrentPlacer {
        get {
            if (CalibrationState == State.CalibratingBasket)
                return _placeBasket;
            if (CalibrationState == State.CalibratingThrowLine)
                return _placeThrowline;
            return null;
        }
    }
    private async Awaitable Awake() {
        CalibrationState.Sub(OnCalibrationStateChange);
        CalibrationState.Value = State.NotCalibrated; 
        _logger = new CustomLogger(this, Color.green);  
        await (_calibrationAwait = BeginCalibration());
    }

    void OnCalibrationStateChange(State state) {
        _placeThrowline.enabled = state == State.CalibratingThrowLine;
        _placeBasket.enabled = state == State.CalibratingBasket; 
    }

    async Awaitable BeginCalibration() {
        if (_calibrationAwait != null && !_calibrationAwait.IsCompleted) {
            Debug.LogError("tried calibrating while already calibrating", this);
            return;
        }
        CalibrationState.Value = State.CalibratingThrowLine;
        try { 
            _logger.Log("Calibration Started"); 
            while (IsCalibrating) {
                var showPreviewSurface = CalibrationState == State.CalibratingBasket;
                _courtSurface.gameObject.SetActive(_placeBasket.WasPlaced);

                var canPreview = _placeThrowline.WasPlaced && _placeBasket.isActiveAndEnabled;
                _courtSurfacePreview.gameObject.SetActive(canPreview);
                if(canPreview)
                    SetSurface(_courtSurfacePreview, _placeThrowline.PlacedObj.position, _placeBasket.PreviewObj.position);

                var fullPlacement = _placeThrowline.WasPlaced && _placeBasket.WasPlaced;
                _courtSurface.gameObject.SetActive(fullPlacement);
                if (fullPlacement)  
                    SetSurface( _courtSurface , _placeThrowline.PlacedObj.position, _placeBasket.PlacedObj.position);
                await Awaitable.EndOfFrameAsync();
                await Awaitable.NextFrameAsync();
            } 
        } 
        catch(System.Exception ex) { Debug.LogException(ex); } 
        finally {
            CalibrationState.Value = State.Calibrated;
            _calibrationAwait = null;
            _logger.Log("Calibration Done");
        }
    }   

    void SetSurface(Transform surface, Vector3 bottomCenter, Vector3 corner) {
        corner.y = bottomCenter.y;
        surface.position = bottomCenter;
        var width = ThrowlineWidth;
        var length = Vector3.Distance(bottomCenter, corner); 
        surface.rotation = bottomCenter.DirectionToAsRotation(corner);
        surface.localScale = new Vector3(width, 0f, length);  
    }

    public void CalibrateThrowline() {
        if(_calibrationAwait == null) {
            _logger.LogError("Tried to start calibrating throwline but not calibration await found");
            return;
        }
        CalibrationState.Value = State.CalibratingThrowLine;
    }

    public void OnConfirmedCalibrationStep() {
        if(CalibrationState == State.CalibratingThrowLine) {
            _logger.Log("Confirmed throwline");
            CalibrationState.Value = State.CalibratingBasket;
        }
        else if(CalibrationState == State.CalibratingBasket) {
            _logger.Log("Confirmed Basket");
            CalibrationState.Value = State.Calibrated;
        }
    }
} 
