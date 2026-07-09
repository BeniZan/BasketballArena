using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using WebSocketSharp;
using Sirenix.OdinInspector;
using Unity.Netcode;

public class WebRTCVideoReceiver : MonoBehaviour
{
    public const string TYPE_ANSWER = "answer";
    public const string TYPE_CANDIDATE = "candidate";
    public const string REQUEST_VIDEO = "start";
    public const string STOP_VIDEO = "stop";

    public RawImage displayImage;
    public string signalingServerUrl = "ws://127.0.0.1:8080/"; // או ה-IP של השרת

    [SerializeField] WebSocketHandler _socket = new WebSocketHandler();

    private RTCPeerConnection peerConnection; 

    // הודעות מה-WebSocket מגיעות ב-thread רקע; נצבור אותן כאן ונעבד ב- או ה-IP של השרתthread הראשי דרך Update
    private readonly ConcurrentQueue<string> signalingQueue = new ConcurrentQueue<string>();

    // candidates שהגיעו לפני שנקבע ה-remote description, נשמרים עד שאפשר להוסיף אותם
    private readonly List<RTCIceCandidateInit> pendingCandidates = new List<RTCIceCandidateInit>();
    private bool remoteDescriptionSet = false;
    CustomLogger _logger;
    [ShowInInspector]
    public WebSocketState SocketState => _socket?.ReadyState ?? WebSocketState.Closed;
    private void OnEnable() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] ");
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        if (NetworkManager.Singleton.IsServer) {
            StartConnection();
        }
    }
    void OnServerStarted() => StartConnection();
    void OnServerStopped(bool _) => CloseConnection();
    public void StartConnection()
    { 
        _logger = new CustomLogger(this, Color.cyan, "[WebRTC-Receiver] ");
        _logger.Log("Starting WebRTC connection...");
        // The WebRTC update loop must run on the main thread
        StartCoroutine(WebRTC.Update());

        // Use local-only ICE candidates (no STUN/TURN).
        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } },
            iceTransportPolicy = RTCIceTransportPolicy.All
        };
        _socket = new WebSocketHandler();
        peerConnection = new RTCPeerConnection(ref config) {
            OnConnectionStateChange = state => _logger.Log($"Connection change: {state}"),
            OnIceConnectionChange = state => _logger.Log($"ICE Connection change: {state}"),
            
            // קבלת וידאו והצגתו על המסך
            OnTrack = e => {
                _logger.Log($"Track received: {e.Track.Kind}");
                if (e.Track is VideoStreamTrack videoTrack) {
                    videoTrack.OnVideoReceived += tex => {
                        _logger.Log("Received video track");
                        displayImage.texture = tex;
                    };
                }
                else _logger.Log("Received unused track of type: " + e.Track.Kind);
            },

            OnIceCandidate = candidate => {
                var c = new IceCandidateData {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                };
                SignalingMessage msg = new SignalingMessage { type = TYPE_CANDIDATE, data = JsonUtility.ToJson(c) };
                if (_socket != null && _socket.ReadyState == WebSocketState.Open) {
                    _socket.Send(JsonUtility.ToJson(msg));
                }
            }
        };

        _socket.Connect(signalingServerUrl);
        _socket.Socket.OnMessage += OnSignalingMessage;
        // ניסיון חוזר עד שמתחברים (מטפל גם במקרה שהשרת עוד לא מוכן)
        StartCoroutine(KeepConnected());
    }

    private IEnumerator KeepConnected()
    {
        while (_socket != null)
        {
            if (_socket.ReadyState != WebSocketState.Open && _socket.ReadyState != WebSocketState.Connecting)
            {
                _logger.Log("Connecting...");
                _socket.ConnectAsync();
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
        _logger.Log($"Received message :\n{data}");
        SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(data); 
        if (msg.type == WebRTCVideoSender.TYPE_OFFER)
        {
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            StartCoroutine(HandleOffer(desc));
        }
        else if (msg.type ==  TYPE_CANDIDATE)
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
        _logger.Log("Handling offer...");
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

        SignalingMessage msgAnswer = new SignalingMessage { type = TYPE_ANSWER, data = JsonUtility.ToJson(answerDesc) };
        _socket.Send(JsonUtility.ToJson(msgAnswer));
    }

    void CloseConnection() { 
        if(SocketState == WebSocketState.Open) {
            _logger.Log("Closing connection...");
        }
        peerConnection?.Dispose();
        _socket?.Close();
        _socket = null;
        StopAllCoroutines();
    }
    private void OnDisable() {
        CloseConnection();
    } 
}