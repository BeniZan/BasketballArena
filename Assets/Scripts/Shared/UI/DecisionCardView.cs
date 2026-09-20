using UnityEngine;
using UnityEngine.UIElements;
using DecisionEngine.Model.Rating;

// The Decision Quality card: two ring gauges — RATING (decision quality, 0–10) and
// SHOT QUALITY (qSQ at release, 0–10) — plus an optional row of pill toggles.
// Styled by the .decision-* rules in CoachDashboard.uss.
public class DecisionCardView : VisualElement {
    public RingGauge Ring { get; }
    private RingGauge ShotRing { get; }
    private VisualElement Toggles { get; }

    public DecisionCardView(Font valueFont = null, Font captionFont = null, Font textFont = null) {
        AddToClassList("decision-card");
        pickingMode = PickingMode.Ignore;

        var rings = new VisualElement();
        rings.AddToClassList("decision-rings");
        Ring = MakeRing("RATING", rings);
        ShotRing = MakeRing("SHOT QUALITY", rings);
        Add(rings);

        Toggles = new VisualElement();
        Toggles.AddToClassList("decision-toggles");
        Add(Toggles);

        _textFont = textFont;
        foreach (var ring in new[] { Ring, ShotRing }) {
            var value = ring.Q<Label>("ringValue");
            var caption = ring.Q<Label>("ringCaption");
            if (valueFont != null && value != null) value.style.unityFontDefinition = new StyleFontDefinition(valueFont);
            if (captionFont != null && caption != null) caption.style.unityFontDefinition = new StyleFontDefinition(captionFont);
        }
    }

    private readonly Font _textFont;

    private static RingGauge MakeRing(string caption, VisualElement parent) {
        var host = new VisualElement();
        host.AddToClassList("decision-ring");
        var ring = new RingGauge { Caption = caption };
        ring.style.width = Length.Percent(100);
        ring.style.height = Length.Percent(100);
        host.Add(ring);
        parent.Add(host);
        return ring;
    }

    public Button AddToggle(string text, System.Action onClick) {
        var btn = new Button(onClick) { text = text };
        btn.AddToClassList("decision-pill");
        if (_textFont != null) btn.style.unityFontDefinition = new StyleFontDefinition(_textFont);
        Toggles.Add(btn);
        return btn;
    }

    public static void SetToggle(Button btn, bool on, string offText, string onText) {
        if (btn == null) return;
        btn.text = on ? onText : offText;
        btn.EnableInClassList("on", on);
    }

    public void SetReport(ScoringReport report) {
        Ring.Value = report.Score;
        if (report.shotTaken) ShotRing.Value = report.ShotQualityScore;
        else ShotRing.Clear("NO SHOT");
    }
}
