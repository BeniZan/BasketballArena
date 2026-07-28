using Unity.WebRTC;
using Unity.Netcode;
using System;
using NUnit.Framework;
using System.Collections.Generic;

public class WebRTCHandshakeManager : NetworkBehaviour {
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
        WebRTC.ConfigureNativeLogging(true, NativeLoggingSeverity.Warning);
        StartCoroutine(WebRTC.Update()); 
    }

    public void Client_BeginSendHandshake(string sdp) {
        var handShake = new Handshake() 
        { SDP = sdp, SenderNetID = NetworkManager.Singleton.LocalClientId };
        ReceiveHandshakeRPC(handShake);
    }

    [Rpc(SendTo.Server)]
    void ReceiveHandshakeRPC(Handshake handshake) => OnClientHandshakeReceived?.Invoke(handshake); 

    public void Server_SendAnswer(string sdp, ulong destinationClient) {
        var target = RpcTarget.Single(destinationClient, RpcTargetUse.Temp);
        Client_ReceiveAnswerRPC(sdp, target);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void Client_ReceiveAnswerRPC(string sdp, RpcParams param = default) {
        OnServerHandshakeResponse?.Invoke(sdp);
    }
    public void SendICEData(IceCandidateData data, ulong destination) {
        ReceiveICEDataRPC(data, RpcTarget.Single(destination, RpcTargetUse.Temp));
    }
    [Rpc(SendTo.SpecifiedInParams)]
    void ReceiveICEDataRPC(IceCandidateData data, RpcParams p = default) {
        OnICECandidateReceived?.Invoke(data);
    }

}