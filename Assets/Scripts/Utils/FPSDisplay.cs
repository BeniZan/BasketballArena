using TMPro;
using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text _text;
    [SerializeField, Min(0.05f)] float _updateInterval = 0.5f;
    [SerializeField] bool _showFrameTime;
    [SerializeField] bool _onlyInDebugBuild;
    
    int _frames;
    float _lastRealtime;

    void Awake() {
        if (!_text) {
            Debug.LogWarning($"{nameof(FPSDisplay)} on {name} has no TMP_Text assigned.", this);
            enabled = false;
            return;
        }
        if (_onlyInDebugBuild && !Debug.isDebugBuild)
            gameObject.SetActive(false);
    }

    private void OnEnable() {
        _lastRealtime = Time.realtimeSinceStartup;
    }

    void Update() {
        _frames++;
        var realTime = Time.realtimeSinceStartup;
        var realtimeSinceLastInternal = realTime - _lastRealtime; 
        if (realtimeSinceLastInternal < _updateInterval)
            return;
        _lastRealtime = realTime;

        var fps = _frames / realtimeSinceLastInternal;
        if(NetSpawnedXRData.Local && NetSpawnedXRData.Local.IsSpawned)
            NetSpawnedXRData.Local.FPS.Value = fps;

        if (_showFrameTime)
            _text.SetText("{0:1} FPS ({1:1} ms)", fps, 1000f / fps);
        else
            _text.SetText("{0:1} FPS", fps);

        _frames = 0; 
    }
}
