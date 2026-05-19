using Meta.XR;
using Meta.XR.BuildingBlocks;
using Oculus.Interaction;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Calibration : MonoBehaviour {
    public NetworkVariable<bool> IsCalibrated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer; 
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] GameObject _calibrationConfirmGUI;
    [SerializeField] Button _confirmCalibBtn;
    [SerializeField] LineRenderer _pinchLine;
    [SerializeField] Transform ThrowLine, BasketPosition;

    public float MinPinchForLine = 0.2f;
    public float PinchThreshold;
    Awaitable _calibrationAwait;

    private void Awake() {
        _confirmCalibBtn.onClick.AddListener(OnCalibrationConfirmed);
        _calibrationAwait = BeginCalibration();
    } 

    async Awaitable BeginCalibration() {
        if (_calibrationAwait != null && !_calibrationAwait.IsCompleted) {
            Debug.LogError("tried calibrating while already calibrating", this);
        }
        IsCalibrated.Value = false;
        _calibrationConfirmGUI.SetActive(false);
        _pinchLine.enabled = true;
        while (!IsCalibrated.Value) {
            await Awaitable.NextFrameAsync();
            CalibrationUpdate();
        }
        _pinchLine.enabled = false;
    }

    void CalibrationUpdate() {
        var floorRay = new Ray(_xrPlayer.CenterEyes.position, Vector3.down);
        var hasFloor = _raycastManager.Raycast(floorRay, out var throwLinePoint);
        _pinchLine.enabled = hasFloor;
        if (!hasFloor)
            return;

        var rightPinch = _xrPlayer.LocalLeftHand.GetFingerPinchStrength(0);
        var leftPinch = _xrPlayer.LocalRightHand.GetFingerPinchStrength(0);
        var maxedPinch = Mathf.Max(rightPinch, leftPinch);
        var pinchPose = leftPinch == maxedPinch ? 
                                  _xrPlayer.LocalLeftHand.PointerPose : _xrPlayer.LocalRightHand.PointerPose;
        var pinchOrigin = pinchPose.position;
        var pinchDirection = pinchPose.forward; 

        var color = _pinchLine.material.color;
        color.a = maxedPinch;
        _pinchLine.enabled = maxedPinch < MinPinchForLine;
        _pinchLine.material.color = color;
        _pinchLine.SetPosition(0, pinchOrigin);
        if (maxedPinch < PinchThreshold) 
            return;

        var basketRay = new Ray(pinchOrigin, pinchDirection);
        var hitBasket = _raycastManager.Raycast(basketRay, out var basketHit);
        if (!hitBasket) {
            _pinchLine.SetPosition(1, pinchOrigin + pinchDirection * 100f);
            return;
        }

        ThrowLine.position = throwLinePoint.point;
        var basketFloorPos = basketHit.point;
        basketFloorPos.y = throwLinePoint.point.y;
        ThrowLine.forward = (basketFloorPos - throwLinePoint.point).normalized;
        BasketPosition.position = basketFloorPos;
        _calibrationConfirmGUI.SetActive(true);
    }

    public void OnCalibrationConfirmed() {
        IsCalibrated.Value = true;
    }
} 
