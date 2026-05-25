using Meta.XR; 
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class CalibrationUI : MonoBehaviour {
    [SerializeField] Calibration _calibration;
    [SerializeField] GameObject _throwlineTab, _basketTab; 
    [SerializeField] Button _next, _previous;
     
    private void Awake() {
        _next.onClick.AddListener(OnNext);
        _previous.onClick.AddListener(OnPrevious);
        _calibration.CalibrationState.Sub(OnState);
    }
    void OnState(Calibration.State state) {  
        gameObject.SetActive(_calibration.IsCalibrating);
        _throwlineTab.SetActive(state == Calibration.State.CalibratingThrowLine);
        _basketTab.SetActive(state == Calibration.State.CalibratingBasket); 
        _previous.gameObject.SetActive(state == Calibration.State.CalibratingBasket);
        _next.gameObject.SetActive(_calibration.IsCalibrating);
    }
    void OnNext() { _calibration.OnConfirmed(); }
    void OnPrevious() {
        if(_calibration.CalibrationState.Value == Calibration.State.CalibratingBasket)
            _calibration.CalibrateThrowline(); 
    }
}


public class Calibration : MonoBehaviour {
    public enum State { NotCalibrated, CalibratingThrowLine, CalibratingBasket, Calibrated }
    public Notifier<State> CalibrationState = new Notifier<State>();
    [SerializeField] PlaceWithPinch _placeThrowline, _placeBasket;
    [ShowInInspector] bool _confirmedPlacement;

    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;

    [SerializeField] PlaceWithPinch[] _placePins;
    [SerializeField] Button _previous, _next; 
    [SerializeField] GameObject _calibrationConfirmGUI;   

    public float MinPinchForLine = 0.2f;
    public float PinchThreshold;
    Awaitable _calibrationAwait;
    [ShowInInspector]
    public bool IsCalibrating => CalibrationState.Value == State.CalibratingThrowLine || CalibrationState.Value == State.CalibratingBasket;
    CustomLogger _logger;
    private async Awaitable Awake() {
        CalibrationState.Value = State.NotCalibrated;
        _placeThrowline.gameObject.SetActive(false);
        _placeBasket.gameObject.SetActive(false);
        _logger = new CustomLogger(this, Color.green);  
        await (_calibrationAwait = BeginCalibration());
    }

    async Awaitable BeginCalibration() {
        if (_calibrationAwait != null && !_calibrationAwait.IsCompleted) {
            Debug.LogError("tried calibrating while already calibrating", this);
            return;
        }
        CalibrationState.Value = State.CalibratingThrowLine;
        try {
            _placeThrowline.gameObject.SetActive(true);

            _logger.Log("Calibration Started");
            _calibrationConfirmGUI.SetActive(false); 
            while (IsCalibrating) {
                CalibrationUpdate();
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

    bool _castedThrowLine;

    void CalibrationUpdate() {  
        if(!UpdateIsPinching(out bool isRightMaxPinching)) {
            _logger.Log("Update: No pinch");
            return;
        }
        var rayInt = isRightMaxPinching ? _xrPlayer.RightRay : _xrPlayer.LeftRay;
        var pinchRay = rayInt.Ray;
        var hit = _raycastManager.Raycast(pinchRay,  out EnvironmentRaycastHit rayHit, 100f);
        if (!hit) {
            _pinchLine.SetPosition(1, pinchRay.origin + (pinchRay.direction * 100f));
            _logger.Log("Update: Pinched but no ray hit detected");
            return;
        }
        var setTf = _castedThrowLine ? BasketPosition : ThrowLine;
        setTf.position = rayHit.point;
        _pinchLine.SetPosition(1, rayHit.point); 
        _calibrationConfirmGUI.SetActive(true);
        _logger.Log("Update: Pinched and set transform " + setTf.gameObject);
    }

    bool UpdateIsPinching(out bool isRightHandMaxPinch) {
        var leftPinch  = _xrPlayer.LocalLeftHand.GetFingerPinchStrength(0);
        var rightPinch = _xrPlayer.LocalRightHand.GetFingerPinchStrength(0);
        isRightHandMaxPinch = rightPinch > PinchThreshold;
        var maxPinchedValue = isRightHandMaxPinch ? rightPinch : leftPinch;  
        var isPinching = maxPinchedValue >= PinchThreshold;
        var ray = isRightHandMaxPinch ? _xrPlayer.RightRay : _xrPlayer.LeftRay;
        var color = _pinchLine.material.color;
        color.a = Mathf.InverseLerp(0.3f, 0.8f, maxPinchedValue);
        _pinchLine.material.color = color; 
        _pinchLine.SetPosition(0, ray.Origin);
        _logger.Log($"Pinch: {maxPinchedValue:f2} by {(isRightHandMaxPinch ? "left" : "right")} hand");

        return isPinching;
    }

    public void CalibrateThrowline() {
        CalibrationState.Value = State.CalibratingThrowLine;
    }

    public void OnConfirmed() {
        _confirmedPlacement = true;
        _logger.Log("Placement Confirmed"); 
    }
} 
