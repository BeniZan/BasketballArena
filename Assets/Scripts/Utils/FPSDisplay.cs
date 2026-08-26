using TMPro;
using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text _text;
    [SerializeField, Min(0.05f)] float _updateInterval = 0.5f;
    [SerializeField] bool _showFrameTime;
    [SerializeField] bool _onlyInDebugBuild;

    int _frames;
    float _elapsed;

    void Awake() {
        if (!_text) {
            Debug.LogWarning($"{nameof(FPSDisplay)} on {name} has no TMP_Text assigned.", this);
            enabled = false;
            return;
        }

        if (_onlyInDebugBuild && !Debug.isDebugBuild)
            gameObject.SetActive(false);
    }

    void Update() {
        _frames++;
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < _updateInterval)
            return;

        var fps = _frames / _elapsed;
        if (_showFrameTime)
            _text.SetText("{0:1} FPS ({1:1} ms)", fps, 1000f / fps);
        else
            _text.SetText("{0:1} FPS", fps);

        _frames = 0;
        _elapsed = 0f;
    }
}
