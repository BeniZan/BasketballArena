using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;
using WebSocketSharp;

public class WebRTCVideoReceiver : MonoBehaviour
{
    public RawImage displayImage;
    public string signalingServerUrl = "ws://127.0.0.1:8080/"; // או ה-IP של השרת
    
    private RTCPeerConnection peerConnection;
    private WebSocket ws;

    void Start()
    {
        

        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };
        peerConnection = new RTCPeerConnection(ref config);

        // קבלת וידאו והצגתו על המסך
        peerConnection.OnTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                videoTrack.OnVideoReceived += tex =>
                {
                    displayImage.texture = tex;
                };
            }
        };

        peerConnection.OnIceCandidate = candidate =>
        {
            SignalingMessage msg = new SignalingMessage { type = "candidate", data = JsonUtility.ToJson(candidate) };
            ws.Send(JsonUtility.ToJson(msg));
        };

        ws = new WebSocket(signalingServerUrl);
        ws.OnMessage += OnSignalingMessage;
        ws.Connect();
    }

    private void OnSignalingMessage(object sender, MessageEventArgs e)
    {
        SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(e.Data);
        
        if (msg.type == "offer")
        {
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            StartCoroutine(HandleOffer(desc));
        }
        else if (msg.type == "candidate")
        {
            RTCIceCandidateInit candInit = JsonUtility.FromJson<RTCIceCandidateInit>(msg.data);
            RTCIceCandidate candidate = new RTCIceCandidate(candInit);
            peerConnection.AddIceCandidate(candidate);
        }
    }

    private IEnumerator HandleOffer(RTCSessionDescription offerDesc)
    {
        yield return peerConnection.SetRemoteDescription(ref offerDesc);
        
        var answerOp = peerConnection.CreateAnswer();
        yield return answerOp;

        var answerDesc = answerOp.Desc;
        yield return peerConnection.SetLocalDescription(ref answerDesc);

        SignalingMessage msgAnswer = new SignalingMessage { type = "answer", data = JsonUtility.ToJson(answerDesc) };
        ws.Send(JsonUtility.ToJson(msgAnswer));
    }

    void OnDestroy()
    {
        peerConnection?.Dispose();
        ws?.Close();
        
    }
}