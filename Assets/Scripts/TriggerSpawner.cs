using Meta.XR;
using Meta.XR.MRUtilityKit.BuildingBlocks;
using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerSpawnerManager : MonoBehaviour {
    [SerializeField] Transform _worldAnchor;
    [SerializeField] Transform _previewObject;
    [SerializeField] Transform _objectToSpawn;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] Transform _controllerOrigin;
    [SerializeField] LineRenderer _lineRenderer;
    [SerializeField] InputActionProperty _spawnInput;
    private void Update() {
        var sceneRay = new Ray(_controllerOrigin.position, _controllerOrigin.forward);
        var rayHit = _raycastManager.Raycast(sceneRay, out var hit) || hit.status == EnvironmentRaycastHitStatus.HitPointOccluded;

        _previewObject.gameObject.SetActive(rayHit);
        if (rayHit) {
            _previewObject.position = hit.point;
            //var worldUp = _worldAnchor.rotation * Vector3.up;
            _previewObject.rotation = Quaternion.LookRotation(hit.normal);
        }

        DrawRay(sceneRay, rayHit ? hit : null);

        if (rayHit && _spawnInput.action.WasPerformedThisFrame()) { 
            Instantiate(_objectToSpawn, _previewObject.position, _previewObject.rotation, _worldAnchor);
        }
    }

    void DrawRay(Ray ray, EnvironmentRaycastHit? hit) {  
        var hasHit = hit.HasValue;
        _lineRenderer.gameObject.SetActive(hasHit);
        if (!hasHit)
            return;

        var origin = ray.origin;
        _lineRenderer.SetPosition(0, origin);
        var dest = hit.HasValue ? hit.Value.point : ray.origin + ray.direction * 100f;
        _lineRenderer.SetPosition(1, dest);
        _lineRenderer.startColor = hit.HasValue ? Color.red : Color.green;
    }

}
