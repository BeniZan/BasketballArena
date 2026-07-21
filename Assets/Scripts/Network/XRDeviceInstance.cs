using Meta.XR.BuildingBlocks;
using Meta.XR.MRUtilityKit;
using Oculus.Interaction;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Management;

public class XRDeviceInstance : SingletonBehaviors.SingletonMono<XRDeviceInstance> {
    const float RetryConnectXRDelay = 5f;
    private static WaitForSeconds _waitForRetryConnectXR = new WaitForSeconds(RetryConnectXRDelay);

    static public bool ENABLE_MRUK => Application.isEditor;
	[field: SerializeField] public OVRHand LocalRightHand { get; private set; }
	[field: SerializeField] public OVRHand LocalLeftHand { get; private set; }
	[field: SerializeField] public RayInteractor RightRay { get; private set; }
	[field: SerializeField] public RayInteractor LeftRay { get; private set; }
    [field: SerializeField] public Transform CenterEyes { get; private set; }
    [SerializeField] MRUK _mruk;
    [SerializeField] EffectMesh _efMesh;
    CustomLogger _logger;
    protected override void Awake() {
        base.Awake();
        _logger = new CustomLogger(this, Color.green);
        var enableMRUK = Application.isEditor;
        if(_mruk)
            _mruk.gameObject.SetActive(ENABLE_MRUK);
        if(_efMesh)
            _efMesh.gameObject.SetActive(ENABLE_MRUK);
        if (!ENABLE_MRUK) {
            _mruk.ClearScene();
            _mruk.gameObject.SafeDestroy();
            _efMesh.gameObject.SafeDestroy();
        }
    }


    Task<MRUK.LoadDeviceResult> _awaitingRoom;
    void OnEnable() {
        StartXR();
        if (_mruk) {
            _mruk.ClearScene();
            if(_awaitingRoom == null || _awaitingRoom.IsCompleted)
                _awaitingRoom = _mruk.LoadSceneFromPrefab(_mruk.SceneSettings.RoomPrefabs[0], true);
        }
    }

    void StartXR() {
        StopAllCoroutines();
       // StartCoroutine(StartXRCoroutine());
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
             
            mnger.StopSubsystems();
             
            mnger.DeinitializeLoader();

            _logger.Log("XR shutdown complete.");
        }
    }
}