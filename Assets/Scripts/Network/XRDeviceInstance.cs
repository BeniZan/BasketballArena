using Meta.XR.BuildingBlocks;
using Meta.XR.MRUtilityKit;
using Oculus.Interaction;
using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class XRDeviceInstance : SingletonBehaviors.SingletonMono<XRDeviceInstance> {
    static public bool ENABLE_MRUK => Application.isEditor;
	[field: SerializeField] public OVRHand LocalRightHand { get; private set; }
	[field: SerializeField] public OVRHand LocalLeftHand { get; private set; }
	[field: SerializeField] public RayInteractor RightRay { get; private set; }
	[field: SerializeField] public RayInteractor LeftRay { get; private set; }
    [field: SerializeField] public Transform CenterEyes { get; private set; }
    [SerializeField] MRUK _mruk;
    [SerializeField] EffectMesh _efMesh;
    protected override void Awake() {
        base.Awake();
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
        if (_mruk) {
            _mruk.ClearScene();
            if(_awaitingRoom == null || _awaitingRoom.IsCompleted)
            _awaitingRoom = _mruk.LoadSceneFromPrefab(_mruk.SceneSettings.RoomPrefabs[0], true);
        }
    }

}