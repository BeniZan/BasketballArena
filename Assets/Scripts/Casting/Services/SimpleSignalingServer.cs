using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

// התנהגות הערוץ - כל הודעה שמתקבלת נשלחת לכל שאר המחוברים
public class SignalingBehavior : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        // מעביר את ההודעה לכל מי שמחובר, חוץ ממי ששלח אותה (Broadcast היה שולח גם לשולח עצמו)
        foreach (var id in Sessions.IDs)
        {
            if (id != ID)
            {
                Sessions.SendTo(e.Data, id);
            }
        }
    }
}

public class SimpleSignalingServer : MonoBehaviour
{
    public int port = 8080;
    private WebSocketServer wss;

    // מורם ב-Awake (ולא ב-Start) כדי שהשרת יאזין לפני שכל ה-clients מנסים להתחבר ב-Start.
    // Unity מבטיח שכל ה-Awake רצים לפני כל ה-Start.
    void Awake()
    {
        wss = new WebSocketServer(port);
        wss.AddWebSocketService<SignalingBehavior>("/");
        wss.Start();
        Debug.Log($"Signaling Server started on ws://127.0.0.1:{port}");
    }

    void OnDestroy()
    {
        if (wss != null)
        {
            wss.Stop();
            wss = null;
        }
    }
}