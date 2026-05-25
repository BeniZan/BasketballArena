using Meta.XR;
using Meta.XR.MRUtilityKit.BuildingBlocks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class TestScript : MonoBehaviour
{
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    public PointAndLocate _locate; 
    // Update is called once per frame
    void Update()
    { 
        var isHit = _raycastManager.Raycast(new Ray(transform.position, transform.forward), out var hit);
        if (isHit) {
            transform.position = hit.point;
            transform.forward = hit.normal;
        }
        else Debug.LogError("no hit");
    }
}
