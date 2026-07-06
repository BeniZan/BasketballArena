using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Concurrent;
using WebSocketSharp;

public class WebRTCVideoSender : MonoBehaviour
{
    public Camera vrCamera;
    public string signalingServerUrl = "ws://127.0.0.1:8080/";
    
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    private WebSocket ws;

    // הודעות מה-WebSocket מגיעות ב-thread רקע; נצבור אותן כאן ונעבד ב-thread הראשי דרך Update
    private readonly ConcurrentQueue<string> signalingQueue = new ConcurrentQueue<string>();

    void Start()
    {
        // הפעלת מנוע ה-WebRTC - חובה כדי שהווידאו יקודד ויישלח
        StartCoroutine(WebRTC.Update());

        // התחברות לשרת האיתות
        ws = new WebSocket(signalingServerUrl);
        ws.OnMessage += OnSignalingMessage;
        ws.OnOpen += (s, e) =>
        {
            Debug.Log("[Sender] Connected to signaling server");
            // הכרזת נוכחות - מודיעים לצד השני שאנחנו כאן
            SignalingMessage hello = new SignalingMessage { type = "hello", data = "sender" };
            ws.Send(JsonUtility.ToJson(hello));
        };
        ws.OnError += (s, e) => Debug.LogWarning($"[Sender] WebSocket error: {e.Message}");
        ws.OnClose += (s, e) => Debug.Log($"[Sender] WebSocket closed ({e.Code})");

        // ניסיון חוזר עד שמתחברים (מטפל גם במקרה שהשרת עוד לא מוכן)
        StartCoroutine(KeepConnected());

        StartCoroutine(SetupConnection());
    }

    private IEnumerator KeepConnected()
    {
        while (ws != null)
        {
            if (ws.ReadyState != WebSocketState.Open && ws.ReadyState != WebSocketState.Connecting)
            {
                Debug.Log("[Sender] Connecting to signaling server...");
                ws.ConnectAsync();
            }
            yield return new WaitForSeconds(2f);
        }
    }

    void Update()
    {
        while (signalingQueue.TryDequeue(out string data))
        {
            ProcessSignalingMessage(data);
        }
    }

    private IEnumerator SetupConnection()
    {
        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };
        
        peerConnection = new RTCPeerConnection(ref config);

        peerConnection.OnConnectionStateChange = state => Debug.Log($"[Sender] Connection state: {state}");
        peerConnection.OnIceConnectionChange = state => Debug.Log($"[Sender] ICE state: {state}");

        // שליחת ICE Candidates דרך ה-WebSocket
        peerConnection.OnIceCandidate = candidate =>
        {
            var c = new IceCandidateData
            {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            };
            SignalingMessage msg = new SignalingMessage { type = "candidate", data = JsonUtility.ToJson(c) };
            if (ws != null && ws.ReadyState == WebSocketState.Open)
            {
                ws.Send(JsonUtility.ToJson(msg));
            }
        };

        // לכידת הוידאו
        RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        rt.Create();
        vrCamera.targetTexture = rt;
        videoTrack = new VideoStreamTrack(rt);
        peerConnection.AddTrack(videoTrack);

        // ממתינים שה-WebSocket יהיה פתוח לפני שמתחילים משא ומתן,
        // כדי שה-Offer וה-ICE candidates (שנוצרים ב-SetLocalDescription) אכן יישלחו
        yield return new WaitUntil(() => ws != null && ws.ReadyState == WebSocketState.Open);

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
        // רץ ב-thread רקע - רק דוחפים לתור, העיבוד נעשה ב-Update
        signalingQueue.Enqueue(e.Data);
    }

    private void ProcessSignalingMessage(string data)
    {
        SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(data);

        if (msg.type == "hello")
        {
            Debug.Log("[Sender] Receiver connected");
            // משיבים ack כדי שגם המקבל ידע שאנחנו כאן
            SignalingMessage ack = new SignalingMessage { type = "hello-ack", data = "sender" };
            ws.Send(JsonUtility.ToJson(ack));
        }
        else if (msg.type == "hello-ack")
        {
            Debug.Log("[Sender] Receiver connected");
        }
        else if (msg.type == "answer")
        {
            // קבלת Answer מהטאבלט
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            peerConnection.SetRemoteDescription(ref desc);
        }
        else if (msg.type == "candidate")
        {
            IceCandidateData c = JsonUtility.FromJson<IceCandidateData>(msg.data);
            RTCIceCandidateInit init = new RTCIceCandidateInit
            {
                candidate = c.candidate,
                sdpMid = c.sdpMid,
                sdpMLineIndex = c.sdpMLineIndex
            };
            peerConnection.AddIceCandidate(new RTCIceCandidate(init));
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