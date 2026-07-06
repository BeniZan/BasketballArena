using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// פאנל סטטוס שנבנה בזמן ריצה ומציג את שלבי החיבור על המסך.
// אירועים מגיעים גם מ-thread רקע (WebSocket) וגם מה-thread הראשי, לכן Report בטוח ל-thread
// והעדכון בפועל ל-Text נעשה רק ב-Update (ב-thread הראשי).
public class ConnectionStatusUI : MonoBehaviour
{
    private const int MaxLines = 10;

    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private readonly List<string> lines = new List<string>();
    private Text text;

    void Awake()
    {
        BuildUI();
    }

    // ניתן לקרוא מכל thread
    public void Report(string line)
    {
        incoming.Enqueue(line);
    }

    void Update()
    {
        bool changed = false;
        while (incoming.TryDequeue(out string line))
        {
            lines.Add(line);
            if (lines.Count > MaxLines)
            {
                lines.RemoveAt(0);
            }
            changed = true;
        }

        if (changed && text != null)
        {
            text.text = string.Join("\n", lines);
        }
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("ConnectionStatusCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // מעל הווידאו
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // רקע כהה חצי-שקוף בפינה השמאלית-עליונה
        var panelGo = new GameObject("StatusPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(10f, -10f);
        panelRect.sizeDelta = new Vector2(560f, 280f);

        var textGo = new GameObject("StatusText");
        textGo.transform.SetParent(panelGo.transform, false);
        text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 12f);
        textRect.offsetMax = new Vector2(-12f, -12f);

        text.text = "ממתין...";
    }
}
