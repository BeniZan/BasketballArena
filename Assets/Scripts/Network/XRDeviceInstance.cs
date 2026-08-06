using Sirenix.OdinInspector; 
using System.Collections; 
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Hands; 
using UnityEngine.XR.Management;

public class XRDeviceInstance : SingletonBehaviors.SingletonMono<XRDeviceInstance> {
    const float RetryConnectXRDelay = 5f;
    private static WaitForSeconds _waitForRetryConnectXR = new WaitForSeconds(RetryConnectXRDelay);
    static public bool ENABLE_SIMULATED_ROOM => Application.isEditor;
    [SerializeField] GameObject _simulatedEnviorment;
    [field: SerializeField] public XROrigin Origin { get; private set; }
    [field: SerializeField] public ARRaycastManager Raycaster { get; private set; }
    [field: SerializeField] public XRHandDevice TrackedRightHand { get; private set; }
	[field: SerializeField] public XRHandDevice TrackedLeftHand { get; private set; }
    [field: SerializeField] public Transform CenterEyes { get; private set; }
    [field: SerializeField] public XRHandTrackingEvents LeftTracking { get; private set; }
    [field: SerializeField] public XRHandTrackingEvents RightTracking { get; private set; }
    [ShowInInspector] XRLoader CurrentLoader =>  XRGeneralSettings.Instance?.Manager?.activeLoader;
    public bool IsLeftTracked => LeftTracking.handIsTracked; 
    public bool TryGetRightPinchValues(out Vector3 pinchWorldPos, out Quaternion pinchWorldRot, out float pinchValue) {
        pinchWorldPos = default;
        pinchWorldRot = default;
        pinchValue = default;

        if (!Origin) {
            Debug.LogError("No XR origin" ,this);
            return false;
        }

        if (TrackedRightHand == null || !TrackedRightHand.added)
            return false; 
         
        pinchValue = TrackedRightHand.pinchValue.ReadValue();
        pinchWorldPos = TrackedRightHand.pinchPosition.ReadValue();
        pinchWorldRot = TrackedRightHand.pinchRotation.ReadValue(); 
         
        pinchWorldPos = Origin.transform.TransformPoint(pinchWorldPos);
        pinchWorldRot = Origin.transform.rotation * pinchWorldRot;

        return true;
    }


    CustomLogger _logger;
    bool _wasInit;
     
    void Init() {
        _wasInit = true;
        _logger = new CustomLogger(this, Color.green); 
    }

    void OnEnable() {
        if (!_wasInit)
            Init();
        _simulatedEnviorment.SetActive(Application.isEditor);
        LeftTracking.trackingChanged.AddListener(_ =>OnTrackingChanged());
        RightTracking.trackingChanged.AddListener(_ => OnTrackingChanged());

        StartXR(); 
    } 

    void OnTrackingChanged() { 
        TrackedLeftHand = InputSystem.GetDevice<XRHandDevice>(CommonUsages.LeftHand);
        TrackedRightHand = InputSystem.GetDevice<XRHandDevice>(CommonUsages.RightHand);
    } 

    void StartXR() {
        StopAllCoroutines();
        StartCoroutine(StartXRCoroutine());
    }

    IEnumerator StartXRCoroutine() {
        _logger.Log("Initializing XR...");

        var mnger = XRGeneralSettings.Instance.Manager;
        if (!mnger) {
            _logger.LogError("XR Manager not found. Ensure XR is properly configured in Project Settings.");
            yield break;
        }
        if (!mnger.activeLoader) {
            yield return mnger.InitializeLoader();
        }

        if (!mnger.activeLoader) {
            _logger.LogError("XR Initialization failed. Ensure your headset is connected."
                                + "\n Retrying in " + RetryConnectXRDelay + " seconds...");
            yield return _waitForRetryConnectXR;
            StartCoroutine(StartXRCoroutine());
            yield break;
        }
        _logger.Log("XR initialized successfully. Starting subsystems..."); 
        mnger.StartSubsystems(); 
    }
    void OnDisable() { 
        StopXR(); 
    }
    void StopXR() {
        StopAllCoroutines();
        var mnger = XRGeneralSettings.Instance.Manager;
        if (mnger && mnger.isInitializationComplete) {
            _logger.Log("Stopping XR Subsystems...");

            mnger.DeinitializeLoader();
        }
        _logger.Log("XR shutdown complete.");
    }
}