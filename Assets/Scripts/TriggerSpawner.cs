using Meta.XR;
using Meta.XR.MRUtilityKit.BuildingBlocks;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerSpawner : MonoBehaviour {
    [SerializeField] Transform _worldAnchor;
    [SerializeField] Transform _previewObject;
    [SerializeField] Transform _objectToSpawn;
    [SerializeField] EnvironmentRaycastManager _raycastManager;
    [SerializeField] Transform _controllerOrigin;
    [SerializeField] LineRenderer _lineRenderer;
    [SerializeField] InputActionProperty _spawnInput;
    [SerializeField] bool _replaceSpawnedObject;
    [field: SerializeField] public string SpawnerUILabel { get; private set; }
    public Transform Spawned { get; internal set; }

    public event Action OnSpawned, OnPlaced;

    private void Awake() {
        _spawnInput.action.Enable();
    }

    private void Update() {
        var sceneRay = new Ray(_controllerOrigin.position, _controllerOrigin.forward);
        var rayHit = _raycastManager.Raycast(sceneRay, out var hit);

        _previewObject.gameObject.SetActive(rayHit);
        if (rayHit) {
            _previewObject.position = hit.point;
            _previewObject.localRotation = Quaternion.identity;
        }

        DrawRay(sceneRay, rayHit ? hit : null);
        if (rayHit && _spawnInput.action.WasPressedThisFrame())
            Spawn();
    }

    void Spawn() {
        _previewObject.GetPositionAndRotation(out var pos, out var rot);
        if ( _replaceSpawnedObject && Spawned) {
            // use spawned object  
            Spawned.SetPositionAndRotation(pos, rot);
        } else {  // always instanties
            Spawned = Instantiate(_objectToSpawn, pos, rot, _worldAnchor);
            OnSpawned?.Invoke();
        }
        OnPlaced?.Invoke();
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

    private void OnDestroy() {
        _spawnInput.action.Dispose();
    }

}
