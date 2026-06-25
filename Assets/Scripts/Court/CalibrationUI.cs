using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class CalibrationUI : MonoBehaviour {
    [SerializeField] Calibration _calibration;
    [SerializeField] GameObject[] _tabs;
    [SerializeField] Button _next, _previous;
    [SerializeField] Camera _cam;
    [SerializeField] OVRHand _hand;
    [SerializeField, Get] Canvas _canvas;
    private void Awake() {
        _next.onClick.AddListener(OnNext);
        _previous.onClick.AddListener(OnPrevious);
        _calibration.CalibrationStep.Sub(OnState);
    }
    private void OnDestroy() => _calibration.CalibrationStep.Unsub(OnState); 
    void OnState(Calibration.Step state) {
        _previous.interactable = state > Calibration.Step.NotCalibrated + 1;
        _next.interactable = true;

        gameObject.SetActive(_calibration.IsCalibrating);
        for(int i=0; i< _tabs.Length; i++) {
            _tabs[i].SetActive((int)_calibration.CalibrationStep.Value == i);
        }
        _previous.gameObject.SetActive(state > Calibration.Step.NotCalibrated);
        _next.gameObject.SetActive(state < Calibration.Step.Calibrated);
    }
    [Button, HorizontalGroup]
    void OnNext() => _calibration.OnConfirmedCalibrationStep();
    [Button, HorizontalGroup]
    void OnPrevious() => _calibration.Backtrack();  
    private void Update() {
        var dirToCam = -transform.DirectionTo(_cam.transform);
        transform.rotation = Quaternion.LookRotation(dirToCam, _cam.transform.up);
        transform.localPosition = dirToCam * 0.1f;
        _canvas.enabled = _hand.IsTracked;
    }

}
