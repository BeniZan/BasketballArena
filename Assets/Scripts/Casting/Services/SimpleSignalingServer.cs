using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

// התנהגות הערוץ - כל הודעה שמתקבלת נשלחת לכל שאר המחוברים
public class SignalingBehavior : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        // מעביר את ההודעה לכל מי שמחובר, חוץ ממי ששלח אותה
        Sessions.Broadcast(e.Data);
    }
}

public class SimpleSignalingServer : MonoBehaviour
{
    public int port = 8080;
    private WebSocketServer wss;

    void Start()
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