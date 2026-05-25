using UnityEngine;
using UnityEngine.UI;

public class CalibrationUI : MonoBehaviour {
    [SerializeField] Calibration _calibration;
    [SerializeField] GameObject _throwlineTab, _basketTab; 
    [SerializeField] Button _next, _previous;
    [SerializeField] Camera _cam;
    [SerializeField] OVRHand _hand;
    [SerializeField, Get] Canvas _canvas;
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
    void OnNext() { _calibration.OnConfirmedCalibrationStep(); }
    void OnPrevious() {
        if(_calibration.CalibrationState.Value == Calibration.State.CalibratingBasket)
            _calibration.CalibrateThrowline(); 
    }

    private void Update() {
        var dirToCam = -transform.DirectionTo(_cam.transform);
        transform.rotation = Quaternion.LookRotation(dirToCam, _cam.transform.up);
        transform.localPosition = dirToCam * 0.1f;
        _canvas.enabled = _hand.IsTracked;
    }

}
