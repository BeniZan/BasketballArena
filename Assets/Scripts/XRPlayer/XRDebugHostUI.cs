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

    bool _isActive;

    void Start()
    {
        NetBoot.Instance.OnPlayerTypeSetup += Instance_OnPlayerTypeSetup;
        Instance_OnPlayerTypeSetup(NetBoot.Instance);
    }

    private void Instance_OnPlayerTypeSetup(NetBoot boot) {
        gameObject.SetActive(boot.IsCoach);
    } 

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(_lookAt, _lookAt.up);
        if (_handShape.Active != _isActive) {
            _isActive = _handShape.Active;
            if(_isActive)
                _timeHandShapeActive.Restart();
        }

        if(_isActive && _timeHandShapeActive.TimerOver) {
            _timeHandShapeActive.Restart();
            NetBoot.Instance.SetupAsXRCoachDebug();
        }

        const float headStart = 0.15f; 
        _loadingImg.gameObject.SetActive(_timeHandShapeActive.TimeRunning > headStart);
        _loadingImg.fillAmount = _timeHandShapeActive.NormalizedTime;
    }
}
