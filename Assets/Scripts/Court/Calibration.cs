using Meta.XR; 
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI; 


public class Calibration : MonoBehaviour {

    [SerializeField] PlaceWithPinch _placeThrowline, _placeBasket;
    [ShowInInspector] public bool IsCalibrated { get; private set; }
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;

    [SerializeField] PlaceWithPinch[] _placePins;
    [SerializeField] Button _previous, _next; 
    [SerializeField] GameObject _calibrationConfirmGUI;   

    public float MinPinchForLine = 0.2f;
    public float PinchThreshold;
    Awaitable _calibrationAwait;
    [ShowInInspector]
    public bool IsCalibrating => _calibrationAwait != null;
    CustomLogger _logger;
    private async Awaitable Awake() {
        _logger = new CustomLogger(this, Color.green); 
        _confirmCalibBtn.onClick.AddListener(OnCalibrationConfirmed);
        await (_calibrationAwait = BeginCalibration());
    }

    async Awaitable BeginCalibration() {
        try {
            if (_calibrationAwait != null && !_calibrationAwait.IsCompleted) {
                Debug.LogError("tried calibrating while already calibrating", this);
            }
            _logger.Log("Calibration Started");
            _calibrationConfirmGUI.SetActive(false);
            _pinchLine.enabled = true;
            IsCalibrated = false;
            while (!IsCalibrated) {
                CalibrationUpdate();
                await Awaitable.EndOfFrameAsync();
                await Awaitable.NextFrameAsync();
            } 
        } 
        catch(System.Exception ex) { Debug.LogException(ex); } 
        finally {
            _pinchLine.enabled = false;
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

    public void OnCalibrationConfirmed() { 
        IsCalibrated = true;
        _logger.Log("Calibration Confirmed");
        _calibrationConfirmGUI.SetActive(false);
    }
} 
