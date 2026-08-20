using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.WebRTC;

public class WebRTCHandshakeManager : NetworkBehaviour {
    static readonly bool USE_GOOGLE_STUN_SERVERS = false;
    static public RTCIceServer[] GetIceServers() {
        if (USE_GOOGLE_STUN_SERVERS) {
            return new RTCIceServer[] {
                    new() {
                        urls = new string[] {
                            "stun:stun.l.google.com:19302",
                            "stun:stun1.l.google.com:19302"
                        }
                    }
                };
        }
        return new RTCIceServer[0];
    }

    static WebRTCHandshakeManager _instance;
    public static WebRTCHandshakeManager Instance {
        get {
            if (_instance)
                return _instance;
            return _instance = FindFirstObjectByType<WebRTCHandshakeManager>();
        }
    }
    [Serializable]
    public struct Handshake : INetworkSerializable {
        public string SDP;
        public ulong SenderNetID;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref SDP);
            serializer.SerializeValue(ref SenderNetID);
        }
    }


    [Serializable]
    public struct IceCandidateData : INetworkSerializable {
        public ulong SenderSourceNetID;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref SenderSourceNetID);
            serializer.SerializeValue(ref candidate);
            serializer.SerializeValue(ref sdpMid);
            serializer.SerializeValue(ref sdpMLineIndex);
        }
    } 

    public event Action<Handshake> OnClientHandshakeReceived; 
    public event Action<string> OnServerHandshakeResponse;
    public event Action<IceCandidateData> OnICECandidateReceived;

    private void OnEnable() {
        WebRTC.ConfigureNativeLogging(true, NativeLoggingSeverity.Info); 
        StartCoroutine(WebRTC.Update()); 
    }

    private void OnDisable() {
        StopAllCoroutines();
    } 

    // 1. create offer from XR client
    public void Client_BeginSendHandshake(string sdp) {
        var handShake = new Handshake() 
        { SDP = sdp, SenderNetID = NetworkManager.Singleton.LocalClientId };
        ReceiveHandshakeRPC(handShake);
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
    void ReceiveHandshakeRPC(Handshake handshake) => OnClientHandshakeReceived?.Invoke(handshake); 

    public void Server_SendAnswer(string sdp, ulong destinationClient) {
        var target = RpcTarget.Single(destinationClient, RpcTargetUse.Temp);
        Client_ReceiveAnswerRPC(sdp, target);
    }

    [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
    public void Client_ReceiveAnswerRPC(string sdp, RpcParams param = default) {
        OnServerHandshakeResponse?.Invoke(sdp);
    }
    public void SendICEData(IceCandidateData data, ulong destination) {
        ReceiveICEDataRPC(data, RpcTarget.Single(destination, RpcTargetUse.Temp));
    }
    [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
    void ReceiveICEDataRPC(IceCandidateData data, RpcParams p = default) {
        OnICECandidateReceived?.Invoke(data);
    }

}