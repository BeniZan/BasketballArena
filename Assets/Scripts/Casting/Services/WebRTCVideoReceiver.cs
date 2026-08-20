using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class WebRTCVideoReceiver : MonoBehaviour {   
    private RTCPeerConnection peerConnection;
    Queue<RTCIceCandidateInit> _pendingCandidates = new Queue<RTCIceCandidateInit>();
    bool _setupRemoteDescription;
    CustomLogger _logger;
    [ShowInInspector, HideInEditorMode, ReadOnly] bool _isConnectionActive;
    [ShowInInspector, HideInEditorMode, ReadOnly] Texture _recievedVideo;
    [SerializeField] RawImage _testImg;
    public Texture VideoTexture { get; private set; }
    public event Action<Texture> OnVideoTextureChanged;
    private void Awake() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] "); 
    } 
    private void LateUpdate() {
        var shouldConnect = NetworkManager.Singleton && NetworkManager.Singleton.IsServer;
        if (_isConnectionActive != shouldConnect) {
            if (shouldConnect)
                StartConnection();
            else CloseConnection();
        }
    }
    public void StartConnection()
    {
        _isConnectionActive = true;
        _logger = new CustomLogger(this, Color.cyan, "[WebRTC-Receiver] ");
        _logger.Log("Starting WebRTC connection...");

        // Use local-only ICE candidates (no STUN/TURN).
        var config = new RTCConfiguration {
            iceServers = WebRTCHandshakeManager.GetIceServers(),
            iceTransportPolicy = RTCIceTransportPolicy.All
        }; 
        peerConnection = new RTCPeerConnection(ref config) {
            OnConnectionStateChange = state => _logger.Log($"Connection change: {state}"),
            OnIceConnectionChange = state => _logger.Log($"ICE Connection change: {state}"),
            OnTrack = e => {
                _logger.Log($"Track received: {e.Track.Kind}");
                if (e.Track is VideoStreamTrack videoTrack) {
                    videoTrack.OnVideoReceived += tex => {
                        _logger.Log("Received video track");
                        InitializeTexture(tex); 
                    };
                }
                else
                    _logger.Log("Received unused track of type: " + e.Track.Kind);
            }
        };

        WebRTCHandshakeManager.Instance.OnClientHandshakeReceived += Instance_OnOfferReceived; 
        WebRTCHandshakeManager.Instance.OnICECandidateReceived += OnReceivedICE;
    }
 
    private void InitializeTexture(Texture tex) { 
        VideoTexture = tex;
        _recievedVideo = tex;
        OnVideoTextureChanged?.Invoke(tex); 
        if (_testImg)
            _testImg.texture = _recievedVideo; // for testing   
    }

    void SetupCodecs() {
        var codecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;

        // Log available codecs for debugging
        foreach (var c in codecs)
            _logger.Log($"Available codec: {c.mimeType}");

        var preferredCodecs = codecs.Where(c => c.mimeType == "video/H264").ToList();

        if (preferredCodecs.Count == 0) {
            _logger.Log("H264 not available, falling back to VP8");
            preferredCodecs = codecs.Where(c => c.mimeType == "video/VP8").ToList();
        }

        RTCRtpTransceiver transceiver = peerConnection.AddTransceiver(TrackKind.Video);
        transceiver.SetCodecPreferences(preferredCodecs.ToArray()); 
    }


    private void Instance_OnOfferReceived(WebRTCHandshakeManager.Handshake handshake) {
        StartCoroutine(RecieveOffer(handshake));
    }

    IEnumerator RecieveOffer(WebRTCHandshakeManager.Handshake handshake) {
        var desc = new RTCSessionDescription {
            type = RTCSdpType.Offer,
            sdp = handshake.SDP
        };

        var remoteDesc = peerConnection.SetRemoteDescription(ref desc);
        yield return remoteDesc;
        if (remoteDesc.IsError) {
            _logger.LogError($"Failed to set remote description: {remoteDesc.Error.message}");
            yield break;
        }

        _setupRemoteDescription = true;
        while (_pendingCandidates.TryDequeue(out var candidate)) {
            if( ! peerConnection.AddIceCandidate(new RTCIceCandidate(candidate))) {
                _logger.LogError("Failed to add ICE candidate");
            }
        }

        var answer = peerConnection.CreateAnswer();

        yield return answer;
        if (answer.IsError) {
            LogRTCError(answer.Error);
            yield break;
        }

        var answerDesc = answer.Desc;
        var localDescriptionSet = peerConnection.SetLocalDescription(ref answerDesc);
        yield return localDescriptionSet;

        if (localDescriptionSet.IsError) {
            LogRTCError(localDescriptionSet.Error);
            yield break;
        }

        _logger.Log("Sending answer...");

        WebRTCHandshakeManager.Instance.Server_SendAnswer(answerDesc.sdp, handshake.SenderNetID);
    } 

    void LogRTCError(RTCError error) {
        _logger.LogError(error.errorType + ":" +error.message);
    }

    void OnReceivedICE(WebRTCHandshakeManager.IceCandidateData c) {
        RTCIceCandidateInit candInit = new RTCIceCandidateInit {
            candidate = c.candidate,
            sdpMid = c.sdpMid,
            sdpMLineIndex = c.sdpMLineIndex
        };
        AddIceCandidate(candInit);
    }

    void AddIceCandidate(RTCIceCandidateInit candInit) {
        if (_setupRemoteDescription) {
            peerConnection.AddIceCandidate(new RTCIceCandidate(candInit));
        }
        else _pendingCandidates.Enqueue(candInit);
    } 
 
    void CloseConnection() {  
        if (VideoTexture != null) {
            VideoTexture = null;
            OnVideoTextureChanged?.Invoke(null);
        } 
        _isConnectionActive = false;  
        peerConnection?.Dispose(); 
        StopAllCoroutines();
    }
    private void OnDisable() { 
        CloseConnection();
    } 
}