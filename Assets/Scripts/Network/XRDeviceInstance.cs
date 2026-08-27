using Sirenix.OdinInspector; 
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Management;

public class XRDeviceInstance : SingletonBehaviors.SingletonMono<XRDeviceInstance> {
    const float RetryConnectXRDelay = 5f;
    private static WaitForSeconds _waitForRetryConnectXR = new WaitForSeconds(RetryConnectXRDelay);
    static public bool ENABLE_SIMULATED_ROOM => Application.isEditor;
    [field: SerializeField] public XROrigin Origin { get; private set; }
    [field: SerializeField] public ARAnchorManager AnchorManager { get; private set; }
    [field: SerializeField] public Camera HeadCam { get; private set; }
    [field: SerializeField] public ARRaycastManager Raycaster { get; private set; }
    [field: SerializeField] public XRHandDevice TrackedRightHand { get; private set; }
	[field: SerializeField] public XRHandDevice TrackedLeftHand { get; private set; }
    [field: SerializeField] public Transform CenterEyes { get; private set; }
    [field: SerializeField] public XRHandTrackingEvents LeftTracking { get; private set; }
    [field: SerializeField] public XRHandTrackingEvents RightTracking { get; private set; }
    [ShowInInspector] XRLoader CurrentLoader =>  XRGeneralSettings.Instance?.Manager?.activeLoader;
    public bool IsLeftTracked => LeftTracking.handIsTracked;

    readonly List<XRHandSubsystem> _reuseSubsystems = new();

    bool TryGetHandSubsystem(out XRHandSubsystem xrHandSubSys) {
        if (_reuseSubsystems.Count > 0) {
            xrHandSubSys = _reuseSubsystems[0];
            return true;
        } 
        SubsystemManager.GetSubsystems(_reuseSubsystems);
        xrHandSubSys = _reuseSubsystems.Count > 0 ? _reuseSubsystems[0] : null;
        return xrHandSubSys != null;
    }
    RealTimer _logNoXRFoundTimer = new RealTimer(2f, true);
    public bool TryGetRightPinchValues(out float pinchValue) { 
        pinchValue = default;
        if (!TryGetHandSubsystem(out var xrHandSubSys) && Time.realtimeSinceStartup > 8f) {  
            if (_logNoXRFoundTimer.TimerOver) {
                _logNoXRFoundTimer.Restart();
                _logger.LogWarning("No XR hand subsystem found. Ensure XR is properly configured in Project Settings.");
            }
            return false;
        }
        if (!Origin) {
            _logger.LogError("No XR origin");
            return false;
        }

        if (!xrHandSubSys.rightHand.isTracked) {
            return false;
        }

        var idx = 
            xrHandSubSys.rightHand.CalculateFingerShape(XRHandFingerID.Index,  XRFingerShapeTypes.Pinch);
        idx.TryGetPinch(out pinchValue);
        return true;
    }


    CustomLogger _logger;
    protected override void Awake() {
        base.Awake(); 
        _logger = new CustomLogger(this, Color.green);
        LeftTracking.trackingChanged.AddListener(_ => OnTrackingChanged());
        RightTracking.trackingChanged.AddListener(_ => OnTrackingChanged());
    }
    private void OnEnable() => StartXR(); 
    private void OnDisable() => StopXR(); 
    void OnTrackingChanged() { 
        TrackedLeftHand = InputSystem.GetDevice<XRHandDevice>(CommonUsages.LeftHand);
        TrackedRightHand = InputSystem.GetDevice<XRHandDevice>(CommonUsages.RightHand);
    }
    [SerializeField] ARSession _arSession;
    void StartXR() {
        StopAllCoroutines();
        StartCoroutine(StartXRCoroutine());
        _arSession.enabled = true;
    }

    void StopXR() {
        _logger.Log("XR Disabled");
        StopAllCoroutines();
        var mnger = XRGeneralSettings.Instance.Manager;
        if (mnger && mnger.activeLoader) {
            _logger.Log("Shutting down xr...");
            mnger.DeinitializeLoader();
        }
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
}