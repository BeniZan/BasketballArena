using Sirenix.OdinInspector;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

public class CalibrationUI : MonoBehaviour {
    [SerializeField] Calibration _calibration; 
    [SerializeField] GameObject[] _tabs;
    [SerializeField] Button _next, _previous;
    [SerializeField] Camera _cam;
    [SerializeField] XRHandDevice _hand;
    [SerializeField, Get] Canvas _canvas;
    private void Start() {
        _next.onClick.AddListener(OnNext);
        _previous.onClick.AddListener(OnPrevious);
        _calibration.CalibrationStep.Sub(OnState);
        foreach(var placer in _calibration.Placers) {
            placer.OnPlaced += Placer_OnPlaced;
        }
    }
    private void Placer_OnPlaced() => OnState(_calibration.CalibrationStep.Value);
    private void OnDestroy() => _calibration.CalibrationStep.Unsub(OnState); 
    void OnState(Calibration.Step step) {
        _previous.interactable = step > Calibration.Step.NotCalibrated + 1;
        _next.interactable = true;

        gameObject.SetActive(_calibration.IsCalibrating);
        for(int i=0; i< _tabs.Length; i++) {
            _tabs[i].SetActive((int)_calibration.CalibrationStep.Value == i);
        }
        _previous.interactable = step > (Calibration.Step.NotCalibrated+1);
        _next.interactable = step < Calibration.Step.Calibrated && _calibration.CurrentPlacer && _calibration.CurrentPlacer.WasPlaced;
    }
    [Button, HorizontalGroup]
    void OnNext() => _calibration.OnConfirmedCalibrationStep();
    [Button, HorizontalGroup]
    void OnPrevious() => _calibration.Backtrack();   
}
