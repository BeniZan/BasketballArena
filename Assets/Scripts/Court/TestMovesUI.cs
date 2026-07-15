using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Oculus.Interaction.PoseDetection;
using Sirenix.OdinInspector;
using Oculus.Interaction.Input;

public class TestMovesUI : MonoBehaviour {
    [SerializeField] Canvas _mainCanvas;
    [SerializeField] OVRHand _leftHand;
    [SerializeField] GameObject _debugCanvas;
    [SerializeField] Image _loadingImg;
    [SerializeField] TMPro.TMP_Dropdown _dropdown;
    [SerializeField] ShapeRecognizerActiveState _handShape;
    [SerializeField] Calibration _calib;
    [SerializeField] Button _play, _pause, _restart;
    [SerializeField] RealTimer _timeHandShapeActive = new(3f);
    [SerializeField] Camera _centerEye;
    CustomLogger _logger;
    bool _wasToggled;
    bool _wasShapeActive; 
    void Start() {
        _logger = new CustomLogger(this, Color.green);
        if (Debug.isDebugBuild) {
            _calib.CalibrationStep.Sub(OnCalibrationState);
            var optionLst = new Dropdown.OptionData();
            var manager = NetDrillsActivator.Instance;
            var options = new List<TMP_Dropdown.OptionData>
                (manager.AllTeamManeuvers
                .Select(m => new TMP_Dropdown.OptionData(m.name)));
            _dropdown.options = options;
            _dropdown.onValueChanged.AddListener(OnDropdown);
            SetDropdownValue();
            manager.ActiveManeuver.Sub(OnActiveManeuver);

            _play.onClick.AddListener(() => DrillPlayer.Instance.Play());
            _pause.onClick.AddListener(() => DrillPlayer.Instance.Pause());
            _play.onClick.AddListener(() => DrillPlayer.Instance.RestartAndPause());
            UpdateDebugUIActivation();
        }
        else gameObject.SafeDestroy();
    }

    private void OnEnable() {
        SetDropdownValue();
    }

    void OnDropdown(int i) {
        var manager = NetDrillsActivator.Instance;
        manager.Server_SetActiveDrill(i);
    }

    void OnActiveManeuver(DrillData _) => SetDropdownValue();

    void SetDropdownValue() {
        _dropdown.SetValueWithoutNotify(NetDrillsActivator.Instance.ActiveManeuverIdx);
    }

    void OnCalibrationState(Calibration.Step step) {
        gameObject.SetActive(step == Calibration.Step.Calibrated);
    }


    private void LateUpdate() {
        _mainCanvas.enabled = _leftHand.IsTracked;

        if (_handShape.Active && _wasToggled) {
            return;
        }

        if (!_handShape.Active)
            _wasToggled = false;

        if (_wasShapeActive != _handShape.Active) {
            _wasShapeActive = _handShape.Active;
            _timeHandShapeActive.Restart();
        }

        if (_handShape.Active && _timeHandShapeActive.TimerOver) {
            ToggleCoachHostState();
            return;
        }

        if (_handShape.Active) {
            _logger.Log("Thumbs up for: " + _timeHandShapeActive.TimeRunning.ToString2Digits());
        }

        const float headStart = 0.15f;
        _loadingImg.gameObject.SetActive(_handShape.Active && _timeHandShapeActive.TimeRunning > headStart);
        _loadingImg.fillAmount = _timeHandShapeActive.NormalizedTime;
    } 

    [Button, HideInEditorMode]
    void ToggleCoachHostState() {
        _wasToggled = true;
        _loadingImg.gameObject.SetActive(false);
        _timeHandShapeActive.Restart();
        NetBoot.Instance.SetupPlayerType(true, !NetBoot.Instance.IsCoach);
        UpdateDebugUIActivation();
    }

    void UpdateDebugUIActivation() {
        _debugCanvas.SetActive(NetBoot.Instance.IsCoach); 
    }

}
