using Meta.XR;
using Meta.XR.MRUtilityKit;
using Meta.XR.MRUtilityKit.BuildingBlocks;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class PlaceWithPinch : MonoBehaviour {
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _lineRend;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] Transform _preview;
    [SerializeField] Transform _placeObj;
    public string Description;
    public float PinchThreshold = 0.7f;
    public event Action OnPlaced;   
    [NonSerialized, ShowInInspector] public bool WasPlaced; 
    public Transform PreviewObj => _preview;
    public Transform PlacedObj => _placeObj; 
    public Vector3 PreviewOrPlacedPosition => WasPlaced ? _placeObj.position : _preview.position;
    public bool IsPlacing {
        get => isActiveAndEnabled;
        set {
            enabled = value;
            OnIsPlacing(value);
        }
    }

    private void OnEnable()  => OnIsPlacing(true); 
    private void OnDisable() => OnIsPlacing(false); 

    void OnIsPlacing(bool enable) {
        enabled = enable;
        _lineRend.enabled = enable;
        _preview.gameObject.SetActive(enable);
    }


    private void Update() { 
        var pinchValue = _xrPlayer.LocalRightHand.GetFingerPinchStrength(0);  
        var isPinching = pinchValue >= PinchThreshold; 
        var ray = _xrPlayer.RightRay.Ray; 

        var rayHit = Raycast(ray, out var hitPoint);
        UpdateRay(ray, isPinching, rayHit, hitPoint);

        _preview.gameObject.SetActive(rayHit); 
        _preview.position = hitPoint;
        if (isPinching && rayHit) {
            _placeObj.gameObject.SetActive(true); 
            _placeObj.transform.position = hitPoint;
            WasPlaced = true;
            OnPlaced?.Invoke();
        } 
    }

    bool Raycast(Ray ray, out Vector3 hit) {
        if (XRDeviceInstance.ENABLE_MRUK) {
            if (!MRUK.Instance) {
                Debug.LogWarning("MRUK enabled but no instance found");
                hit = default;
                return false;
            }

            var room = MRUK.Instance.GetCurrentRoom();
            if (!room) {
                hit = default;
                return false;
            }
            var isRayHit = room.Raycast(ray, 100f, out var rhit);
            hit = rhit.point;
            return isRayHit;
        } 

        var isHit = _raycastManager.Raycast(ray, out var envHit);
        hit = envHit.point;
        return isHit; 
    }


    void UpdateRay(Ray ray, bool isPinch, bool rayHit, Vector3 hitPoint) { 
        var origin = ray.origin;
        _lineRend.SetPosition(0, origin);
        var dest = rayHit ? hitPoint : ray.origin + ray.direction * 100f;
        _lineRend.SetPosition(1, dest);
        var color = rayHit ?
            (isPinch ? Color.green : Color.blue) :
            Color.red;
        _lineRend.startColor = color;
        _lineRend.endColor = color;
    }

}
