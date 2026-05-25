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
    [SerializeField] PlaceWithPinch[] _placePins; 
    [SerializeField] LineRenderer _pinchLine;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    public float MinPinchForLine = 0.2f, PinchThreshold = 0.85f;
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
        _placeThrowline.gameObject.SetActive(state == State.CalibratingThrowLine);
        _placeBasket.gameObject.SetActive(state == State.CalibratingBasket);
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
            _logger.Log("Confirmed throwline");
            CalibrationState.Value = State.Calibrated;
        } 
    }
} 
