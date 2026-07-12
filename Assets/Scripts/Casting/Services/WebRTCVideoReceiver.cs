using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using WebSocketSharp;
using Sirenix.OdinInspector;
using Unity.Netcode;
using System;
using UnityEngine.UIElements;

public class WebRTCVideoReceiver : MonoBehaviour {   
    private RTCPeerConnection peerConnection;
    Queue<RTCIceCandidateInit> _pendingCandidates = new Queue<RTCIceCandidateInit>();
    bool _setupRemoteDescription;
    CustomLogger _logger; 
    private void OnEnable() {
        _logger = new CustomLogger(this, Color.green, "[WebRTC-Sender] ");
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        if (NetworkManager.Singleton.IsServer) 
            StartConnection(); 
    }
    void OnServerStarted() => StartConnection();
    void OnServerStopped(bool _) => CloseConnection();
    public void StartConnection()
    { 
        _logger = new CustomLogger(this, Color.cyan, "[WebRTC-Receiver] ");
        _logger.Log("Starting WebRTC connection..."); 

        // Use local-only ICE candidates (no STUN/TURN).
        var config = new RTCConfiguration
        {
            iceServers = new RTCIceServer[0],
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
        //todo
    }

    private void Instance_OnOfferReceived(WebRTCHandshakeManager.Handshake handshake) {
        StartCoroutine(RecieveOffer(handshake));
    }

    IEnumerator RecieveOffer(WebRTCHandshakeManager.Handshake handshake) { 
        var desc = new RTCSessionDescription {
            type = RTCSdpType.Offer,
            sdp = handshake.SDP
        };

        yield return peerConnection.SetRemoteDescription(ref desc);
        _setupRemoteDescription = true;
        while (_pendingCandidates.TryDequeue(out var candidate)) {
            peerConnection.AddIceCandidate(new RTCIceCandidate(candidate));
        }

        var answer = peerConnection.CreateAnswer();

        yield return answer;

        var answerDesc = answer.Desc;
        yield return peerConnection.SetLocalDescription(ref answerDesc); 
        WebRTCHandshakeManager.Instance.Server_SendAnswer(answerDesc.sdp, handshake.SenderNetID);
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
        peerConnection?.Dispose(); 
        StopAllCoroutines();
    }
    private void OnDisable() {
        CloseConnection();
    } 
}