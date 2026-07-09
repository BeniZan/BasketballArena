using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Concurrent;
using WebSocketSharp;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Sirenix.OdinInspector;

public enum WebRTCState { Disconnected, Connecting, Connected } 
public class WebRTCVideoSender : MonoBehaviour
{
    public const string TYPE_OFFER = "offer";

    public Camera vrCamera; 
    
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack; 
    private WebSocketHandler _socket;

    // הודעות מה-WebSocket מגיעות ב-thread רקע; נצבור אותן כאן ונعבד ב-thread הראשי דרך Update
    private readonly ConcurrentQueue<string> signalingQueue = new ConcurrentQueue<string>();
    CustomLogger _logger;

    [ShowInInspector]
    public bool IsSendingVideo { get; private set; }

    [ShowInInspector]
    public WebSocketState SocketState => _socket?.ReadyState ?? WebSocketState.Closed;
     
    private void OnEnable() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] ");
        NetworkManager.Singleton.OnConnectionEvent += Singleton_OnConnectionEvent;
        if (NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsServer)
            StartConnection();
    }

    private void Singleton_OnConnectionEvent(NetworkManager nm, ConnectionEventData data) {
        if(data.ClientId == nm.LocalClientId && ! nm.IsServer) {
            if(data.EventType == ConnectionEvent.ClientConnected) {
                StartConnection();
            } else if(data.EventType == ConnectionEvent.ClientDisconnected) {
                CloseConnection();
            }
        }
    } 

    void StartConnection()
    {
        _logger.Log("Starting WebRTC connection...");

        // הפעלת מנוע ה-WebRTC - חובה כדי שהווידאו יקודד ויישלח
        StartCoroutine(WebRTC.Update());

        // התחברות לשרת האיתות
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        var address = transport.ConnectionData.Address;
        if(!address.StartsWith("//"))
            address = "//" + address;
        var urlAddress = "ws:" + address + ":8080/";
        _socket = new WebSocketHandler();
        _socket.Connect(urlAddress);
        _socket.Socket.OnMessage += OnSignalingMessage;

        // ניסיון חוזר עד שמתחברים (מטפל גם במקרה שהשרת עוד לא מוכן)
        StartCoroutine(KeepConnected());

        StartCoroutine(SetupConnection());
    }

    private IEnumerator KeepConnected()
    {
        while (_socket != null)
        {
            if (_socket.ReadyState != WebSocketState.Open && _socket.ReadyState != WebSocketState.Connecting)
            {
                _logger.Log("Connecting to signaling server...");
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

    private IEnumerator SetupConnection()
    {
        _logger.Log("Setting up socket");

        // Use local-only ICE candidates (no STUN/TURN) so peers attempt direct LAN connections.
        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }, 
            iceTransportPolicy = RTCIceTransportPolicy.All
        };

        peerConnection = new RTCPeerConnection(ref config) {
            OnConnectionStateChange = state => _logger.Log($"Connection state: {state}"),
            OnIceConnectionChange = state => _logger.Log($"ICE state: {state}"),

            // שליחת ICE Candidates דרך ה-WebSocket
            OnIceCandidate = candidate => {
                var c = new IceCandidateData {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                };
                SignalingMessage msg = new SignalingMessage { type = WebRTCVideoReceiver.TYPE_CANDIDATE, data = JsonUtility.ToJson(c) };
                if (_socket != null && _socket.ReadyState == WebSocketState.Open) {
                    _socket.Send(JsonUtility.ToJson(msg));
                }
            }
        };

        // לכידת הוידאו
        RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        rt.Create();
        vrCamera.targetTexture = rt;
        videoTrack = new VideoStreamTrack(rt);
        peerConnection.AddTrack(videoTrack);

        _logger.Log("Waiting for socket to be ready...");
        // ממתינים שה-WebSocket יהיה פתוח לפני שמתחילים משא ומתן,
        // כדי שה-Offer וה-ICE candidates (שנוצרים ב-SetLocalDescription) אכן יישלחו
        yield return new WaitUntil(() => _socket != null && _socket.ReadyState == WebSocketState.Open);

        // יצירת Offer
        _logger.Log("Creating peer session offer...");
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;

        _logger.Log("Setting peer description...");
        var desc = offerOp.Desc;
        yield return peerConnection.SetLocalDescription(ref desc);

        // שליחת ה-Offer
        _logger.Log("Sending peer session offer...");
        SignalingMessage msgOffer = new SignalingMessage { type = TYPE_OFFER, data = JsonUtility.ToJson(desc) };
        _socket.Send(JsonUtility.ToJson(msgOffer));
    }

    private void OnSignalingMessage(object sender, MessageEventArgs e)
    {
        // רץ ב-thread רקע - רק דוחפים לתור, העיבוד נעשה ב-Update
        signalingQueue.Enqueue(e.Data);
    }

    private void ProcessSignalingMessage(in string data)
    {
        _logger.Log($"Received message :\n{data}");
        SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(data);
        if (msg.type == WebRTCVideoReceiver.TYPE_ANSWER)
        {
            _logger.Log("Received answer");
            // קבלת Answer מהטאבלט
            RTCSessionDescription desc = JsonUtility.FromJson<RTCSessionDescription>(msg.data);
            peerConnection.SetRemoteDescription(ref desc);
        }
        else if (msg.type == WebRTCVideoReceiver.TYPE_CANDIDATE)
        {
            _logger.Log("Received candidate");
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

    private void OnDisable() {
        CloseConnection();
    }

    void CloseConnection() {
        vrCamera.targetTexture = null;
        videoTrack?.Dispose();
        peerConnection?.Dispose();
        _socket?.Close();
        _socket = null;
        StopAllCoroutines();
    }
}