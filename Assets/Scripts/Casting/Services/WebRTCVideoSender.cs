#if UNITY_ANDROID && !UNITY_EDITOR
#define UNITY_BUILD_ANDROID
#endif 
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
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
using System.Reflection;
using Unity.XR.Oculus.Input;
using UnityEngine.XR.ARFoundation;
#endif


public enum WebRTCState { Disconnected, Connecting, Connected }

/// <summary>
/// In editor copy the screen texture
/// In quest use WebCam to get the camera
/// in both cases we need to copy the texture to render texture
/// </summary>
public class WebRTCVideoSender : MonoBehaviour
{
    public const string TYPE_OFFER = "offer";
    [SerializeField] Camera _cam;
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    CustomLogger _logger;
    Queue<RTCIceCandidateInit> _pendingCandidates = new Queue<RTCIceCandidateInit>();
    bool _setupRemoteDescription;
    [SerializeField] RawImage _previewSentTexture;
    [ShowInInspector, HideInEditorMode, ReadOnly] RenderTexture _webRTCSubmitTexture;
    UniversalRenderPipeline.SingleCameraRequest _camRequest;
#if UNITY_BUILD_ANDROID
    [ShowInInspector, HideInEditorMode, ReadOnly] WebCamTexture _questWebCamTexture;
#endif
    [SerializeField, GetParent] AROcclusionManager _arOcclusion;
    [SerializeField] Material _combineTexturesMaterial;
    Awaitable _awaitGettingWebcamTexture; 
    private void Awake() {
        if (!_logger.PingObj)
            _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender]");
        WebRTCHandshakeManager.Instance.OnServerHandshakeResponse += Handshake_OnServerHandshakeResponse;
        WebRTCHandshakeManager.Instance.OnICECandidateReceived += Instance_OnICECandidateReceived;
        _previewSentTexture.texture = Texture2D.normalTexture;
        _logger.Log("Starting WebRTCVideoSender...");
        _awaitGettingWebcamTexture = AwaitGetQuestWebcam(); 
    }

#if UNITY_ANDROID
	async Awaitable AwaitAndroidPermission(string permission){
        if (Permission.HasUserAuthorizedPermission(permission))
            return;
        Permission.RequestUserPermission(permission);
        await Awaitable.WaitForSecondsAsync(10f);
        while (!Permission.HasUserAuthorizedPermission(permission)){
            _logger.LogError("Webcam permission denied on device.");
            await Awaitable.WaitForSecondsAsync(10f);
            Permission.RequestUserPermission(permission);
        }
        _logger.Log($"Recieved permission [{permission}].");
	}
#endif
    [Button] private async Awaitable AwaitGetQuestWebcam() {
        if (_webRTCSubmitTexture) {
            try {Destroy(_webRTCSubmitTexture);  }
            catch {  }
            _webRTCSubmitTexture = null;
        }
        int width = 1280, height = 720; int depth = 24;
        var format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
        _camRequest = new UniversalRenderPipeline.SingleCameraRequest() {
            destination = new RenderTexture(width, height, depth, format),
        };
        _camRequest.destination.Create();

        _webRTCSubmitTexture = new RenderTexture(width, height, depth, format);
        _webRTCSubmitTexture.Create();

#if UNITY_BUILD_ANDROID 
        _logger.Log("Getting Android Permissions...");
        await AwaitAndroidPermission("com.oculus.permission.USE_SCENE");
        await AwaitAndroidPermission("horizonos.permission.HEADSET_CAMERA");
        await AwaitAndroidPermission("android.permission.CHANGE_WIFI_MULTICAST_STATE");
        await Awaitable.NextFrameAsync();
        await Awaitable.WaitForSecondsAsync(2f);

        //_arOcclusion.TryGetEnvironmentDepthTexture(out var depthTex);
        //var gotDepthTex = _arOcclusion.TryGetEnvironmentDepthTexture(out depthTex) && depthTex;
        //_combineTexturesMaterial.SetTexture("_RealDepthTex", depthTex);
        //if (gotDepthTex) {
        //    _logger.Log("Got depth texture from AR Occlusion Manager: " + depthTex.dimension);
        //} else _logger.LogError("Failed to get depth texture from AR Occlusion Manager.");

        while (! _questWebCamTexture) {
            _logger.Log("Searching for XR WebCam...");
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length > 0) {
                string log = "Found Webcams: ";
                foreach (var cam in devices) {
                    log += $"[camName:{cam.name} depthName:{cam.depthCameraName} kind:{cam.kind}] | ";
                }
                _logger.Log(log);
                int camIdx = 1;
                var chosenCam = devices[camIdx];
                _logger.Log($"Chose Camera[{camIdx}]: " + chosenCam.name);
                _questWebCamTexture = new WebCamTexture(devices[camIdx].name, 1280, 720, 60);
                if(!_questWebCamTexture.isPlaying)
                    _questWebCamTexture.Play(); 
            }
            if(!_questWebCamTexture)
                await Awaitable.WaitForSecondsAsync(1f);
        }
#endif
    }

    private void LateUpdate() {
        bool hasWebRTCSubmitTex = _webRTCSubmitTexture && _webRTCSubmitTexture.IsCreated();
        if (!hasWebRTCSubmitTex)
            return;
        var hasCamTex = _camRequest != null && _camRequest.destination && _camRequest.destination.IsCreated();
        if (hasCamTex) {
            RenderPipeline.SubmitRenderRequest(_cam, _camRequest);
            Graphics.Blit(_camRequest.destination, _webRTCSubmitTexture);
        }
#if UNITY_BUILD_ANDROID
        var hasQuestWebCamTex = _questWebCamTexture != null && _questWebCamTexture.isPlaying;
        if (hasQuestWebCamTex) {
            if (hasCamTex) {
                Graphics.Blit(_questWebCamTexture , _webRTCSubmitTexture.graphicsTexture, _combineTexturesMaterial);
            } else {
                Graphics.Blit(_questWebCamTexture, _webRTCSubmitTexture);
            }
        }
#endif
    }

    private void OnEnable() {
        if(!_logger.PingObj)
            _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender]");
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
    IEnumerator CreateAndAddVideoTrack() {
        yield return new WaitUntil(() => _webRTCSubmitTexture);  
        if (_previewSentTexture)
            _previewSentTexture.texture = _webRTCSubmitTexture;
        videoTrack = new VideoStreamTrack(_webRTCSubmitTexture);
        peerConnection.AddTrack(videoTrack);
        _logger.Log("Created and added video track to peer connection.");
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
    void CloseConnection() {
        _setupRemoteDescription = false; 
        videoTrack?.Dispose();
        peerConnection?.Dispose(); 
        StopAllCoroutines();
    }
}