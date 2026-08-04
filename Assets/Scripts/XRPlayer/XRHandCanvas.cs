using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

public class XRHandCanvas : MonoBehaviour {
    [SerializeField] Canvas _canvas;
    [SerializeField] CanvasGroup _group;
    [SerializeField] Camera _cam;
    [SerializeField, GetParent] XRDeviceInstance _xrDevice;
    [SerializeField] float _smoothAlphaSpeed = 0.2f;
    [SerializeField] float _lookAtThreshold = 0.7f;
    [SerializeField] bool _rightHand = false;
    [SerializeField] Pose _pose;
    XRHandTrackingEvents Track => _rightHand ? _xrDevice.RightTracking : _xrDevice.LeftTracking; 
    private void LateUpdate() { 

        var lookAtDot = Vector3.Dot(_cam.transform.forward, transform.forward);
        var enableCanvas = Track.handIsTracked && lookAtDot > _lookAtThreshold;

        var deltaAlphaTime = Time.deltaTime / _smoothAlphaSpeed;
        if (!enableCanvas)
            deltaAlphaTime = -deltaAlphaTime;
        _group.alpha = Mathf.Clamp01(_group.alpha + deltaAlphaTime);
        _canvas.enabled = _group.alpha > 0f; 
    }
}
