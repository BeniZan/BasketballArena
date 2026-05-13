using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using WebSocketSharp;

public class WebRTCVideoSender : MonoBehaviour
{
    public Camera vrCamera;
    public string signalingServerUrl = "ws://[TABLET_IP]:8080/";
    
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    private WebSocket ws;

    void Start()
    {
        
        
        // התחברות לשרת האיתות
        ws = new WebSocket(signalingServerUrl);
        ws.OnMessage += OnSignalingMessage;
        ws.Connect();

        StartCoroutine(SetupConnection());
    }

    private IEnumerator SetupConnection()
    {
        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };
        
        peerConnection = new RTCPeerConnection(ref config);
        
        // שליחת ICE Candidates דרך ה-WebSocket
        peerConnection.OnIceCandidate = candidate =>
        {
            SignalingMessage msg = new SignalingMessage { type = "candidate", data = JsonUtility.ToJson(candidate) };
            ws.Send(JsonUtility.ToJson(msg));
        };

        // לכידת הוידאו
        RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        rt.Create();
        vrCamera.targetTexture = rt;
        videoTrack = new VideoStreamTrack(rt);
        peerConnection.AddTrack(videoTrack);

        // יצירת Offer
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;
        
        var desc = offerOp.Desc;
        yield return peerConnection.SetLocalDescription(ref desc);

        // שליחת ה-Offer
        SignalingMessage msgOffer = new SignalingMessage { type = "offer", data = JsonUtility.ToJson(desc) };
        ws.Send(JsonUtility.ToJson(msgOffer));
    }

    private void OnSignalingMessage(object sender, MessageEventArgs e)
    {
        // קבלת Answer מהטאבלט
        SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(e.Data);
        if (msg.type == "answer")
        {
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            peerConnection.SetRemoteDescription(ref desc);
        }
    }

    void OnDestroy()
    {
        vrCamera.targetTexture = null;
        videoTrack?.Dispose();
        peerConnection?.Dispose();
        ws?.Close();
       
    }
}