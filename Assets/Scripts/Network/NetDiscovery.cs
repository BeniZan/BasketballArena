using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class NetDiscovery : MonoBehaviour {
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField, Get] NetworkTransport _transport;

    private void Awake() {
        _netMng.OnClientStarted += RevalidateDiscoveryStatus;
        _netMng.OnClientStopped += RevalidateDiscoveryStatus;

        _netMng.OnServerStarted += RevalidateDiscoveryStatus;
        _netMng.OnServerStopped += RevalidateDiscoveryStatus;
    }

    private void OnDestroy() {
        _netMng.OnClientStarted -= RevalidateDiscoveryStatus;
        _netMng.OnClientStopped -= RevalidateDiscoveryStatus;
        _netMng.OnConnectionEvent += RevalidateDiscoveryStatus;

        _netMng.OnServerStarted -= RevalidateDiscoveryStatus;
        _netMng.OnServerStopped -= RevalidateDiscoveryStatus;
    }
    void RevalidateDiscoveryStatus(NetworkManager m, ConnectionEventData _) => RevalidateDiscoveryStatus();
    void RevalidateDiscoveryStatus(bool _) => RevalidateDiscoveryStatus();
    void RevalidateDiscoveryStatus() {
        if (!_netMng) {
            enabled = false;
            return;
        }

        if (_netMng.IsClient) {
            enabled = !_netMng.IsConnectedClient;
            return;
        } 

        enabled = _netMng.IsServer;
    }  
    void StartDiscovery(bool isServer) {
        StopDiscovery();

        IsServer = isServer;
        IsClient = !isServer;

        // If we are not a server we use the 0 port (let udp client assign a free port to us)
        var port = isServer ? m_Port : 0;

        m_Client = new UdpClient(port) { EnableBroadcast = true, MulticastLoopback = false };

        _ = ListenAsync(isServer ? ReceiveBroadcastAsync : new Func<Task>(ReceiveResponseAsync));

        IsRunning = true;
    }

}
