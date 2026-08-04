using Sirenix.OdinInspector;
using System;
using UnityEngine;
using Unity.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.AR;

public class PlaceWithPinch : MonoBehaviour {
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _lineRend;
    [SerializeField] Transform _preview;
    [SerializeField] Transform _placeObj;
    public string Description;
    public float PinchThreshold = 0.7f;
    public event Action OnPlaced;   
    [NonSerialized, ShowInInspector] public bool WasPlaced;
    XRRayInteractor _raycaster => _xrPlayer.RightRay;
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
        var pinchValue = _xrPlayer.RighPinchValue;  
        var isPinching = pinchValue >= PinchThreshold; 

        var rayHit = _raycaster.TryGetCurrent3DRaycastHit(out var arHit);
        _raycaster.GetLineOriginAndDirection(out var origin, out var direction);

        var hitPoint = rayHit ? arHit.point : origin + direction * 100f;
        UpdateRay(origin, direction, isPinching, rayHit, hitPoint);
        _preview.gameObject.SetActive(rayHit); 
        _preview.position = hitPoint;
        if (isPinching && rayHit) {
            _placeObj.gameObject.SetActive(true); 
            _placeObj.transform.position = hitPoint;
            WasPlaced = true;
            OnPlaced?.Invoke();
        } 
    } 


    void UpdateRay(Vector3 origin, Vector3 dir, bool isPinch, bool rayHit, Vector3 hitPoint) {
        _lineRend.SetPosition(0, origin);
        var dest = rayHit ? hitPoint : origin + dir * 100f;
        _lineRend.SetPosition(1, dest);
        var color = rayHit ?
            (isPinch ? Color.green : Color.blue) :
            Color.red;
        _lineRend.startColor = color;
        _lineRend.endColor = color;
    }

}
