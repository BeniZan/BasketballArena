using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.XR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.AR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static UnityEngine.XR.OpenXR.Features.Interactions.PalmPoseInteraction;

public class PlaceWithPinch : MonoBehaviour {
    [SerializeField, GetParent] XRDeviceInstance _xrPlayer;
    [SerializeField] LineRenderer _lineRend;
    [SerializeField] Transform _preview;
    [SerializeField] Transform _placeObj;
    public string Description;
    public float PinchThreshold = 0.7f;
    public event Action OnPlaced;   
    [NonSerialized, ShowInInspector] public bool WasPlaced; 
    public Transform PreviewObj => _preview;
    public Transform PlacedObj => _placeObj; 
    public Vector3 PreviewOrPlacedPosition => WasPlaced ? _placeObj.position : _preview.position;
    CustomLogger _logger;
    public bool IsPlacing {
        get => isActiveAndEnabled;
        set {
            enabled = value;
            OnIsPlacing(value);
        }
    }
    private void Awake() {
        _logger = new(this, Color.blueViolet);
    }

    private void OnEnable()  => OnIsPlacing(true); 
    private void OnDisable() => OnIsPlacing(false); 

    void OnIsPlacing(bool enable) {
        enabled = enable;
        _lineRend.enabled = enable;
        _preview.gameObject.SetActive(enable);
    }
    List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    [SerializeField] Transform _aimPose;
    bool _logRayHits;
    bool _logPinch = false;
    void TryRaycast(out Ray? ray, out ARRaycastHit? hit, out bool isPinching) {
        hit = null;
        ray = default;
        var pinchReady =
            _xrPlayer.TryGetRightPinchValues(out var pinchValue);
        isPinching = pinchValue >= PinchThreshold;
        if (!pinchReady) {
            if(_logPinch)
                _logger.Log($"Pinch not ready ({pinchValue})"); 
            return;
        }
        if (_logPinch)
            _logger.Log($"Pinching ({pinchValue})");

        //ray = new Ray(pinchWorldPos, pinchWorldRot * Vector3.forward); 
        ray = new Ray(_aimPose.position, _aimPose.forward);
        _hits.Clear();
        var rayHit = _xrPlayer.Raycaster.Raycast(ray.Value, _hits, UnityEngine.XR.ARSubsystems.TrackableType.AllTypes);

        if (_logRayHits) {
            if (_hits.Count == 0) {
                _logger.Log("Raycast hit nothing");
            }

            foreach (var h in _hits) {
                _logger.Log($"Raycast hit: {h.trackableId} at {h.pose.position}");
            }
        }

        if (rayHit && _hits.ValidIndex(0))
            hit = _hits[0];

        return;
    }

    private void Update() {  
        TryRaycast(out var ray, out var arHit, out var isPinching);
        var rayHit = ray.HasValue && arHit.HasValue;
        UpdateRay(ray, isPinching, arHit);
        _preview.gameObject.SetActive(rayHit);
        if (rayHit)
            _preview.position = arHit.Value.pose.position;
        if (isPinching && rayHit) {
            _placeObj.gameObject.SetActive(true); 
            _placeObj.transform.position = _preview.position;
            WasPlaced = true;
            OnPlaced?.Invoke();
        } 
    } 


    void UpdateRay(Ray? ray, bool isPinch, ARRaycastHit? hit) {
        _lineRend.enabled = ray.HasValue;
        if (!ray.HasValue) {
            return;
        }
        var hitPoint = hit.HasValue ? hit.Value.pose.position : ray.Value.GetPoint(100f);   
        var origin = ray.Value.origin;
        _lineRend.SetPosition(0, origin);
        _lineRend.SetPosition(1, hitPoint);
        var color = hit.HasValue ?
            (isPinch ? Color.green : Color.blue) :
            (isPinch ? Color.orange : Color.red);
        _lineRend.startColor = color;
        _lineRend.endColor = color;
    }

}
