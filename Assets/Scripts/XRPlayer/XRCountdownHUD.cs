using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Head-locked XR display for the server-synchronized pre-drill countdown.
/// The canvas stays disabled outside the short 3-2-1 window.
/// </summary>
public sealed class XRCountdownHUD : MonoBehaviour {
    [SerializeField] Canvas _canvas;
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] TMP_Text _countdownText;
    [SerializeField] RectTransform _content;
    [SerializeField, Min(0f)] float _digitPulseDuration = 0.25f;
    [SerializeField, Range(1f, 1.5f)] float _digitStartScale = 1.15f;

    int _lastNumber;
    float _digitShownAt;

    private void Awake() {
        SetVisible(false);
    }

    private void Update() {
        if (!TryInitializeForLocalHeadset())
            return;

        var drillPlayer = DrillPlayer.Instance;
        var number = drillPlayer && drillPlayer.IsSpawned
            ? drillPlayer.CountdownNumber
            : 0;

        if (number <= 0) {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        if (number != _lastNumber) {
            _lastNumber = number;
            _digitShownAt = Time.unscaledTime;
            _countdownText.SetText(number.ToString());
        }

        if (_content) {
            var pulseProgress = _digitPulseDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.unscaledTime - _digitShownAt) / _digitPulseDuration);
            var scale = Mathf.Lerp(_digitStartScale, 1f, pulseProgress);
            _content.localScale = Vector3.one * scale;
        }
    }

    bool TryInitializeForLocalHeadset() {
        if (_canvas && _countdownText && _content)
            return true;

        var headCamera = GetComponent<Camera>();
        if (!headCamera)
            return false;

        var hudObject = new GameObject(
            "XR Countdown Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup));
        hudObject.transform.SetParent(transform, false);

        var hudRect = (RectTransform)hudObject.transform;
        hudRect.localPosition = new Vector3(0f, -0.08f, 1.2f);
        hudRect.localRotation = Quaternion.identity;
        hudRect.localScale = Vector3.one * 0.001f;
        hudRect.sizeDelta = new Vector2(180f, 180f);

        _canvas = hudObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = headCamera;
        _canvas.sortingOrder = 100;

        _canvasGroup = hudObject.GetComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.ignoreParentGroups = true;

        var panelObject = new GameObject(
            "Countdown Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(hudObject.transform, false);

        var panelRect = (RectTransform)panelObject.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(150f, 150f);

        var panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.055f, 0.82f);
        panelImage.raycastTarget = false;

        var textObject = new GameObject(
            "Countdown Number",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _countdownText = textObject.GetComponent<TextMeshProUGUI>();
        _countdownText.text = "3";
        _countdownText.alignment = TextAlignmentOptions.Center;
        _countdownText.fontSize = 104f;
        _countdownText.fontStyle = FontStyles.Bold;
        _countdownText.color = new Color(1f, 0.55f, 0.08f, 1f);
        _countdownText.raycastTarget = false;
        _countdownText.enableAutoSizing = false;

        _content = panelRect;
        SetVisible(false);
        return true;
    }

    private void SetVisible(bool visible) {
        if (_canvas)
            _canvas.enabled = visible;
        if (_canvasGroup)
            _canvasGroup.alpha = visible ? 1f : 0f;

        if (!visible) {
            _lastNumber = 0;
            if (_content)
                _content.localScale = Vector3.one;
        }
    }
}
