using Sirenix.OdinInspector;
using System.Collections.Generic; 
using System.Linq;
using TMPro;
using Unity.XR;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class TestMovesUI : MonoBehaviour {
    [SerializeField] Canvas _mainCanvas;
    [SerializeField, GetParent] XRDeviceInstance _xrDevice;
    [SerializeField] GameObject _debugCanvas;
    [SerializeField] Image _loadingImg;
    [SerializeField] TMPro.TMP_Dropdown _dropdown;
    [SerializeField] XRHandPose _handShape;
    [SerializeField] Calibration _calib;
    [SerializeField] Button _play, _pause, _restart;
    [SerializeField] RealTimer _timeHandShapeActive = new(3f);
    bool _poseDetected;
    bool _poseDetectionApplied;
    CustomLogger _logger;
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
            _play.onClick.AddListener(() => DrillPlayer.Instance.ResetTimeAndPlay());
            UpdateDebugUIActivation();
        }
        else gameObject.SafeDestroy();
    }

    private void OnEnable() {
        SetDropdownValue();
        _poseDetected = false;
        _xrDevice.LeftTracking.jointsUpdated.AddListener(OnJointsUpdated);
    }

    private void OnDisable() {
        _poseDetected = false;
        _xrDevice.LeftTracking.jointsUpdated.RemoveListener(OnJointsUpdated);
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs update) {
        var detected = _handShape.CheckConditions(update);
        if (detected != _poseDetected)
            OnPoseDetection(detected);
    }

    void OnPoseDetection(bool detected) {
        _poseDetected = detected;
        if (detected)
            _timeHandShapeActive.Restart();

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
        _mainCanvas.enabled = _xrDevice.IsLeftTracked;
        UpdatePoseDetectionAppliance();
    } 

    void UpdatePoseDetectionAppliance() {
        if (_poseDetectionApplied) {
            _loadingImg.gameObject.SetActive(false);
            return;
        }

        if (_poseDetected && _timeHandShapeActive.TimerOver) {
            ToggleCoachHostState();
        }

        if (_poseDetected) {
            _logger.Log("Thumbs up for: " + _timeHandShapeActive.TimeRunning.ToString2Digits());
        }

        const float headStart = 0.15f;
        var enableLoading = _poseDetected &&
                                 _timeHandShapeActive.TimeRunning > headStart;
        _loadingImg.gameObject.SetActive(enableLoading);
        if (enableLoading)
            _loadingImg.fillAmount = _timeHandShapeActive.NormalizedTime;
    }

    [Button, HideInEditorMode]
    void ToggleCoachHostState() {
        _poseDetectionApplied = true;
        _loadingImg.gameObject.SetActive(false);
        _timeHandShapeActive.Restart();
        NetBoot.Instance.SetupPlayerType(true, !NetBoot.Instance.IsCoach);
        UpdateDebugUIActivation();
    }

    void UpdateDebugUIActivation() {
        _debugCanvas.SetActive(NetBoot.Instance.IsCoach); 
    }

}
