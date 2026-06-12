using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using WebSocketSharp;

public class WebRTCVideoReceiver : MonoBehaviour
{
    public RawImage displayImage;
    public string signalingServerUrl = "ws://127.0.0.1:8080/"; // או ה-IP של השרת
    
    private RTCPeerConnection peerConnection;
    private WebSocket ws;
    private ConnectionStatusUI statusUI;

    // הודעות מה-WebSocket מגיעות ב-thread רקע; נצבור אותן כאן ונעבד ב-thread הראשי דרך Update
    private readonly ConcurrentQueue<string> signalingQueue = new ConcurrentQueue<string>();

    // candidates שהגיעו לפני שנקבע ה-remote description, נשמרים עד שאפשר להוסיף אותם
    private readonly List<RTCIceCandidateInit> pendingCandidates = new List<RTCIceCandidateInit>();
    private bool remoteDescriptionSet = false;

    void Start()
    {
        // פאנל הסטטוס נוצר אוטומטית - אין צורך בחיווט ב-Inspector
        statusUI = gameObject.AddComponent<ConnectionStatusUI>();

        // הפעלת מנוע ה-WebRTC - חובה כדי שהווידאו יפוענח ויוצג
        StartCoroutine(WebRTC.Update());

        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };
        peerConnection = new RTCPeerConnection(ref config);

        peerConnection.OnConnectionStateChange = state => statusUI.Report($"חיבור WebRTC: {state}");
        peerConnection.OnIceConnectionChange = state => statusUI.Report($"ICE: {state}");

        // קבלת וידאו והצגתו על המסך
        peerConnection.OnTrack = e =>
        {
            statusUI.Report($"התקבל Track: {e.Track.Kind}");
            if (e.Track is VideoStreamTrack videoTrack)
            {
                videoTrack.OnVideoReceived += tex =>
                {
                    statusUI.Report("וידאו מתקבל ✓");
                    displayImage.texture = tex;
                };
            }
        };

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

        ws = new WebSocket(signalingServerUrl);
        ws.OnMessage += OnSignalingMessage;
        ws.OnOpen += (s, e) =>
        {
            statusUI.Report("מחובר לשרת האיתות ✓");
            // הכרזת נוכחות - מודיעים לצד השני שאנחנו כאן
            SignalingMessage hello = new SignalingMessage { type = "hello", data = "receiver" };
            ws.Send(JsonUtility.ToJson(hello));
        };
        ws.OnError += (s, e) => statusUI.Report($"שגיאת WebSocket: {e.Message}");
        ws.OnClose += (s, e) => statusUI.Report($"החיבור נסגר ({e.Code})");

        // ניסיון חוזר עד שמתחברים (מטפל גם במקרה שהשרת עוד לא מוכן)
        StartCoroutine(KeepConnected());
    }

    private IEnumerator KeepConnected()
    {
        while (ws != null)
        {
            if (ws.ReadyState != WebSocketState.Open && ws.ReadyState != WebSocketState.Connecting)
            {
                statusUI.Report("מתחבר לשרת...");
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
            statusUI.Report("השולח נכנס לשרת ✓");
            // משיבים ack כדי שגם השולח ידע שאנחנו כאן
            SignalingMessage ack = new SignalingMessage { type = "hello-ack", data = "receiver" };
            ws.Send(JsonUtility.ToJson(ack));
        }
        else if (msg.type == "hello-ack")
        {
            statusUI.Report("השולח נכנס לשרת ✓");
        }
        else if (msg.type == "offer")
        {
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            StartCoroutine(HandleOffer(desc));
        }
        else if (msg.type == "candidate")
        {
            IceCandidateData c = JsonUtility.FromJson<IceCandidateData>(msg.data);
            RTCIceCandidateInit candInit = new RTCIceCandidateInit
            {
                candidate = c.candidate,
                sdpMid = c.sdpMid,
                sdpMLineIndex = c.sdpMLineIndex
            };

            // אם ה-remote description עדיין לא נקבע, נשמור את ה-candidate לטיפול מאוחר יותר
            if (remoteDescriptionSet)
            {
                peerConnection.AddIceCandidate(new RTCIceCandidate(candInit));
            }
            else
            {
                pendingCandidates.Add(candInit);
            }
        }
    }

    private IEnumerator HandleOffer(RTCSessionDescription offerDesc)
    {
        yield return peerConnection.SetRemoteDescription(ref offerDesc);
        remoteDescriptionSet = true;

        // שפיכת ה-candidates שהמתינו עד שנקבע ה-remote description
        foreach (var candInit in pendingCandidates)
        {
            peerConnection.AddIceCandidate(new RTCIceCandidate(candInit));
        }
        pendingCandidates.Clear();

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