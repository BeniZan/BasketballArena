using UnityEngine;
using UnityEngine.UIElements;

// WHOOP-style radial gauge: a rounded arc drawn with Painter2D, a large percentage in the
// middle and a small caption under it. The arc tweens to new values and its color follows
// the score band (green / amber / red). Pure UI Toolkit; no textures or GameObjects.
public class RingGauge : VisualElement {
    private const float StartAngle = -90f;            // 12 o'clock
    private const float TweenSeconds = 0.9f;

    private readonly Color TrackColor = new Color(1f, 1f, 1f, 0.08f);
    private readonly Color HighColor = new Color(0.10f, 0.83f, 0.56f);   // ≥ 6.7
    private readonly Color MidColor = new Color(0.98f, 0.75f, 0.14f);    // 3.4–6.6
    private readonly Color LowColor = new Color(0.94f, 0.27f, 0.27f);    // < 3.4
    private readonly float StrokeWidth = 9f;

    private readonly Label _valueLabel;
    private readonly Label _captionLabel;
    private float _target, _displayed, _tweenFrom, _tweenStart = -1f;
    private bool _hasValue;

    public float MaxValue = 10f;
    public string Format = "0.0";

    // 0–MaxValue.
    public float Value {
        get { return _target; }
        set {
            _target = Mathf.Clamp(value, 0f, MaxValue);
            _tweenFrom = _displayed;
            _tweenStart = Time.realtimeSinceStartup;
            _hasValue = true;
            schedule.Execute(Tick).Every(16).Until(TweenFinished);
        }
    }

    public string Caption {
        set { _captionLabel.text = value; }
    }

    // Empty state: no arc, a dash, and an optional caption override.
    public void Clear(string caption = null) {
        _hasValue = false;
        _displayed = _target = 0f;
        _tweenStart = -1f;
        _valueLabel.text = "--";
        if (caption != null) _captionLabel.text = caption;
        MarkDirtyRepaint();
    }

    public RingGauge() {
        style.flexShrink = 0;
        style.justifyContent = Justify.Center;
        style.alignItems = Align.Center;
        pickingMode = PickingMode.Ignore;

        _valueLabel = new Label("--") { name = "ringValue" };
        _valueLabel.AddToClassList("ring-value");
        _captionLabel = new Label("") { name = "ringCaption" };
        _captionLabel.AddToClassList("ring-caption");
        var stack = new VisualElement { pickingMode = PickingMode.Ignore };
        stack.style.alignItems = Align.Center;
        stack.Add(_valueLabel);
        stack.Add(_captionLabel);
        Add(stack);

        generateVisualContent += OnGenerateVisualContent;
    }

    private bool TweenFinished() {
        return _tweenStart < 0f;
    }

    private void Tick() {
        if (_tweenStart < 0f) return;
        var t = Mathf.Clamp01((Time.realtimeSinceStartup - _tweenStart) / TweenSeconds);
        var eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
        _displayed = Mathf.Lerp(_tweenFrom, _target, eased);
        _valueLabel.text = _displayed.ToString(Format);
        MarkDirtyRepaint();
        if (t >= 1f) _tweenStart = -1f;
    }

    private Color ColorFor(float v) { float f = v / MaxValue; return f >= 0.67f ? HighColor : f >= 0.34f ? MidColor : LowColor; }

    private void OnGenerateVisualContent(MeshGenerationContext ctx) {
        var r = contentRect;
        var radius = Mathf.Min(r.width, r.height) * 0.5f - StrokeWidth * 0.5f;
        if (radius <= 0f) return;
        var center = r.center;
        var p = ctx.painter2D;
        p.lineWidth = StrokeWidth;
        p.lineCap = LineCap.Round;

        // Track
        p.strokeColor = TrackColor;
        p.BeginPath();
        p.Arc(center, radius, StartAngle, StartAngle + 360f);
        p.Stroke();

        // Value arc
        if (!_hasValue) return;
        var sweep = 360f * (_displayed / MaxValue);
        if (sweep < 0.5f) return;
        p.strokeColor = ColorFor(_displayed);
        p.BeginPath();
        p.Arc(center, radius, StartAngle, StartAngle + sweep);
        p.Stroke();
    }
}
