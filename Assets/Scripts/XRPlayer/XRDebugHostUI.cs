using Oculus.Interaction.HandGrab;
using Oculus.Interaction.PoseDetection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Hands.Gestures;

public class XRDebugHostUI : MonoBehaviour
{
    [SerializeField] Transform _lookAt;
    [SerializeField] UIDocument _uiDoc;
    [SerializeField] UnityEngine.UI.Image _loadingImg;
    [SerializeField] ShapeRecognizerActiveState _handShape;
    [SerializeField] RealTimer _timeHandShapeActive = new(3f);
    [SerializeField] Calibration _calib;
    bool _isActive;
    bool _wasToggled;
    void Awake()
    {
        _uiDoc.enabled = false;
        _calib.CalibrationStep.Sub(OnCalibStep);
        var boot = NetBoot.Instance;
        boot.OnPlayerTypeChange += ShouldToggle;
        ShouldToggle(boot);
    }
    void OnCalibStep(Calibration.Step step) => ShouldToggle(NetBoot.Instance);
    void ShouldToggle(NetBoot boot) {
        gameObject.SetActive(Debug.isDebugBuild && _calib.IsDoneCalibration);
    } 
     
    private void OnDestroy() {
        _calib.CalibrationStep.Unsub(OnCalibStep);
        if (NetBoot.Instance)
            NetBoot.Instance.OnPlayerTypeChange -= ShouldToggle;
    }
     
    void LateUpdate()
    {
        transform.LookAt( - transform.DirectionTo(_lookAt), _lookAt.up);

        if(_handShape.Active && _wasToggled) {
            return;
        }

        if (!_handShape.Active)
            _wasToggled = false;

        if (_handShape.Active != _isActive) {
            _isActive = _handShape.Active;
            if(_isActive)
                _timeHandShapeActive.Restart();
        }

        if(_isActive && _timeHandShapeActive.TimerOver) {
            _wasToggled = true;
            _loadingImg.gameObject.SetActive(false);
            _timeHandShapeActive.Restart();
            NetBoot.Instance.SetupAsXRCoachDebug();
            _uiDoc.enabled = NetBoot.Instance.IsCoach;
        }

        const float headStart = 0.15f; 
        _loadingImg.gameObject.SetActive(_timeHandShapeActive.TimeRunning > headStart);
        _loadingImg.fillAmount = _timeHandShapeActive.NormalizedTime;
    }
}
