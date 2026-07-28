using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Concurrent;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Sirenix.OdinInspector;
using System.Collections.Generic;

public enum WebRTCState { Disconnected, Connecting, Connected } 
public class WebRTCVideoSender : MonoBehaviour
{
    public const string TYPE_OFFER = "offer";

    public Camera vrCamera; 
    
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    [ShowInInspector, HideInEditorMode] private RenderTexture _copiedCameraRT;

    CustomLogger _logger;
    Queue<RTCIceCandidateInit> _pendingCandidates = new Queue<RTCIceCandidateInit>();
    bool _setupRemoteDescription;

    private void Awake() {
        WebRTCHandshakeManager.Instance.OnServerHandshakeResponse += Handshake_OnServerHandshakeResponse;
        WebRTCHandshakeManager.Instance.OnICECandidateReceived += Instance_OnICECandidateReceived;
    }

    private void Handshake_OnServerHandshakeResponse(string sdp) {
        StartCoroutine(ReceiveAnswer(sdp));
    }
    IEnumerator ReceiveAnswer(string sdp) {
        var desc = new RTCSessionDescription {
            type = RTCSdpType.Answer,
            sdp = sdp
        };
        _logger.Log("Received answer..."); 
        yield return peerConnection.SetRemoteDescription(ref desc);
        _setupRemoteDescription = true;
        while (_pendingCandidates.TryDequeue(out var candidate)){
            peerConnection.AddIceCandidate(new RTCIceCandidate(candidate));
        }
    }

    void AddIceCandidate(RTCIceCandidateInit candInit) {
        if (_setupRemoteDescription) {
            peerConnection.AddIceCandidate(new RTCIceCandidate(candInit));
        }
        else _pendingCandidates.Enqueue(candInit);
    }

    private void Instance_OnICECandidateReceived(WebRTCHandshakeManager.IceCandidateData data) {
        AddIceCandidate(new RTCIceCandidateInit 
        { candidate = data.candidate, sdpMid = data.sdpMid, sdpMLineIndex = data.sdpMLineIndex });
    } 

    private void OnEnable() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] ");
        var netMnger = NetBoot.Instance.NetMnger;
        netMnger.OnConnectionEvent += Singleton_OnConnectionEvent;
        if (netMnger.IsConnectedClient && !netMnger.IsServer)
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

    void StartConnection() => StartCoroutine(SetupConnection()); 

    private IEnumerator SetupConnection()
    {
        _logger.Log("Setting up socket");

        // Use local-only ICE candidates (no STUN/TURN) so peers attempt direct LAN connections.
        var config = new RTCConfiguration
        {
            iceServers = new RTCIceServer[0],
            iceTransportPolicy = RTCIceTransportPolicy.All
        };

        peerConnection = new RTCPeerConnection(ref config) {
            OnConnectionStateChange = state => _logger.Log($"Connection state: {state}"),
            OnIceConnectionChange = state => _logger.Log($"ICE state: {state}"),
             
            OnIceCandidate = candidate => { 
                var c = new WebRTCHandshakeManager.IceCandidateData {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                };
                WebRTCHandshakeManager.Instance.SendICEData(c, NetworkManager.ServerClientId);
            }
        };

        CreateAndAddVideoTrack(); 

        _logger.Log("Waiting for socket to be ready...");

        yield return CreateAndSendOffer();
    }

    void CreateAndAddVideoTrack() {
        if(_copiedCameraRT && _copiedCameraRT.IsCreated())
            _copiedCameraRT.Release();

        int width = 1920, height = 1080;
        int depthValue = (int)RenderTextureDepth.Depth24;
        var format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
        _copiedCameraRT = new RenderTexture(width, height, depthValue, format);
        _copiedCameraRT.Create(); 
        videoTrack = new VideoStreamTrack(_copiedCameraRT, Graphics.Blit);
        peerConnection.AddTrack(videoTrack);
    }
    private void LateUpdate() {
        if(_copiedCameraRT && _copiedCameraRT.IsCreated())
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_copiedCameraRT);
    }

    IEnumerator CreateAndSendOffer() { 
        _logger.Log("Creating peer session offer...");
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;

        _logger.Log("Setting peer description...");
        var desc = offerOp.Desc;
        yield return peerConnection.SetLocalDescription(ref desc);
        if (!NetBoot.Instance.IsConnected)
            _logger.Log("Waiting for network boot...");
        yield return new WaitUntil(() => NetBoot.Instance.IsConnected);
        WebRTCHandshakeManager.Instance.Client_BeginSendHandshake(desc.sdp);
    } 

    private void OnDisable() {
        CloseConnection();
        if (NetBoot.HasInstance) {
            var netMnger = NetBoot.Instance.NetMnger;
            netMnger.OnConnectionEvent -= Singleton_OnConnectionEvent;
        }
    }

    void CloseConnection() {
        _setupRemoteDescription = false;
        vrCamera.targetTexture = null;
        videoTrack?.Dispose();
        peerConnection?.Dispose(); 
        StopAllCoroutines();
    }
}