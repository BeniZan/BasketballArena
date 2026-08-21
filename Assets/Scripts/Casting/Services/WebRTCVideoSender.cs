using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;  

#if UNITY_ANDROID
using UnityEngine.Android;
#endif
public enum WebRTCState { Disconnected, Connecting, Connected } 
public class WebRTCVideoSender : MonoBehaviour
{
    public const string TYPE_OFFER = "offer";
     
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    [ShowInInspector, HideInEditorMode] private RenderTexture _copiedCameraRT;
    CustomLogger _logger;
    Queue<RTCIceCandidateInit> _pendingCandidates = new Queue<RTCIceCandidateInit>();
    bool _setupRemoteDescription;
    [ShowInInspector, HideInEditorMode, ReadOnly] WebCamTexture _questCamTexture;
    Awaitable _awaitGettingWebcamTexture;
    private void Awake() {
        WebRTCHandshakeManager.Instance.OnServerHandshakeResponse += Handshake_OnServerHandshakeResponse;
        WebRTCHandshakeManager.Instance.OnICECandidateReceived += Instance_OnICECandidateReceived;
        _awaitGettingWebcamTexture = AwaitGetQuestWebcam();
    }

    [Button]
    private async Awaitable AwaitGetQuestWebcam() {

#if UNITY_EDITOR
        _logger.Log("Requesting webcam permission in editor..."); 
        await Application.RequestUserAuthorization(UserAuthorization.WebCam);
        while(!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            _logger.LogError("Webcam permission denied in editor.");
            await Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#else
        Permission.RequestUserPermission(Permission.Camera);
        await Awaitable.WaitForSecondsAsync(10f);
        while (!Permission.HasUserAuthorizedPermission(Permission.Camera)) {
            _logger.LogError("Webcam permission denied on device.");
            await Awaitable.WaitForSecondsAsync(10f);
            Permission.RequestUserPermission(Permission.Camera);
        } 
#endif

        _logger.Log("Searching for XR WebCam...");

        while (! _questCamTexture) {
            await Awaitable.WaitForSecondsAsync(0.5f);
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length > 0) {
                string log = "Found Webcams: ";
                foreach (var cams in devices) {
                    log += cams.name + " | ";
                }
                _logger.Log(log);
                // Meta maps the passthrough cameras through standard WebCamTexture interfaces
                _questCamTexture = new WebCamTexture(devices[0].name, 1280, 720, 60);
                if(!_questCamTexture.isPlaying)
                    _questCamTexture.Play();
            }
        }
    } 

    private void OnEnable() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] ");
        var netMnger = NetBoot.Instance.NetMnger;
        netMnger.OnConnectionEvent += Singleton_OnConnectionEvent;
        if (netMnger.IsConnectedClient && !netMnger.IsServer)
            StartConnection();
    }
    private void OnDisable() {
        CloseConnection();
        if (NetBoot.HasInstance) {
            var netMnger = NetBoot.Instance.NetMnger;
            netMnger.OnConnectionEvent -= Singleton_OnConnectionEvent;
        }
    }
    void StartConnection() => StartCoroutine(SetupConnection());
    private IEnumerator SetupConnection() {
        _logger.Log("Setting up socket");

        // Use local-only ICE candidates (no STUN/TURN) so peers attempt direct LAN connections.
        var config = new RTCConfiguration {
            iceServers = WebRTCHandshakeManager.GetIceServers(),
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

        yield return CreateAndAddVideoTrack();

        _logger.Log("Waiting for socket to be ready...");

        yield return CreateAndSendOffer();
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
    private void Handshake_OnServerHandshakeResponse(string sdp) {
        StartCoroutine(ReceiveAnswer(sdp));
    }
    IEnumerator ReceiveAnswer(string sdp) {
        var desc = new RTCSessionDescription {
            type = RTCSdpType.Answer,
            sdp = sdp
        };
        _logger.Log("Received answer..."); 
        var remoteDesc = peerConnection.SetRemoteDescription(ref desc);
        yield return remoteDesc;
        if (remoteDesc.IsError) {
            _logger.LogError($"Failed to set remote description: {remoteDesc.Error.message}");
        }
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


    private void Singleton_OnConnectionEvent(NetworkManager nm, ConnectionEventData data) {
        if(data.ClientId == nm.LocalClientId && ! nm.IsServer) {
            if(data.EventType == ConnectionEvent.ClientConnected) {
                StartConnection();
            } else if(data.EventType == ConnectionEvent.ClientDisconnected) {
                CloseConnection();
            }
        }
    }


    UniversalRenderPipeline.SingleCameraRequest _camRequest;

    IEnumerator CreateAndAddVideoTrack() { 
        if (_copiedCameraRT && _copiedCameraRT.IsCreated())
            _copiedCameraRT.Release();

        yield return new WaitUntil(() => _questCamTexture);

        int width = 1280, height = 720;
        //int depthValue = (int)RenderTextureDepth.Depth24;  
        var format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
        _copiedCameraRT = new RenderTexture(width, height, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_SRGB);
        _copiedCameraRT.Create();
        _camRequest = new UniversalRenderPipeline.SingleCameraRequest() { destination = _copiedCameraRT };
        videoTrack = new VideoStreamTrack(_questCamTexture);
        peerConnection.AddTrack(videoTrack);
    }
     
     

    void CloseConnection() {
        _setupRemoteDescription = false; 
        videoTrack?.Dispose();
        peerConnection?.Dispose(); 
        StopAllCoroutines();
    }
}