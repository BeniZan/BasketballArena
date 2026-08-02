using UnityEngine;
using UnityEngine.XR.Hands;

public class XRHandCanvas : MonoBehaviour {
    [SerializeField] Canvas _canvas;
    [SerializeField] CanvasGroup _group;
    [SerializeField] Camera _cam;
    [SerializeField] XRHandDevice _hand;
    [SerializeField] float _smoothAlphaSpeed = 0.2f;
    [SerializeField] float _lookAtThreshold = 0.7f;
    private void LateUpdate() {  
        var lookAtDot = Vector3.Dot(_cam.transform.forward, transform.forward);
        var enableCanvas = _hand.isTracked.isPressed && lookAtDot > _lookAtThreshold;
        

        var deltaAlphaTime = Time.deltaTime / _smoothAlphaSpeed;
        if (!enableCanvas)
            deltaAlphaTime = -deltaAlphaTime;
        _group.alpha = Mathf.Clamp01(_group.alpha + deltaAlphaTime);
        _canvas.enabled = _group.alpha > 0f; 
    }
}
