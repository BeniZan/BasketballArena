using UnityEngine;
using WebSocketSharp;
using Sirenix.OdinInspector;

[System.Serializable]
public class WebSocketHandler {
    public WebSocket Socket;
    [ShowInInspector, ReadOnly] public string URL;
    [ShowInInspector, HideInEditorMode]
    public WebSocketState ReadyState => Socket?.ReadyState ?? WebSocketState.Closed;
    readonly CustomLogger _logger = new CustomLogger(null, Color.yellow, "[WebSocket] ");
    public void Connect(string url) { 
        if (Socket != null && Socket.IsAlive) {
            _logger.LogWarning("Tried connecting when socket is already connected, closing");
            Close();
        }
        URL = url; 

        _logger.Log("WebSocket connecting to: " + url);
        Socket = new WebSocket(URL);
        Socket.WaitTime = 
        Socket.OnOpen += (s, e) => _logger.Log($"Connected websocket ({url})");
        Socket.OnMessage += (s, e) => _logger.Log("Received message: " + e.Data);
        Socket.OnError += (s, e) => _logger.LogError("WebSocket error: " + e.Message);
        Socket.OnClose += (s, e) => _logger.Log("WebSocket closed: " + e.Code);
        Socket.ConnectAsync();
    }
    public void ConnectAsync() => Socket.ConnectAsync();
    public void Send(string message) {
        if (Socket != null && Socket.ReadyState == WebSocketState.Open)
            Socket.Send(message); 
        else  _logger.LogError("WebSocket is not open. Cannot send message.");
    }
    public void Close() {
        _logger.Log("Closing WebSocket connection");
        Socket?.Close();
        Socket = null;
        URL = null;
    } 
    ~WebSocketHandler() { 
        if(Socket != null && Socket.IsAlive) {
            _logger.LogError("Forgot to close socket?");
            Close();
        }
    }
}
