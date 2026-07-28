using Sirenix.OdinInspector;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using static UnityEngine.Audio.ProcessorInstance;

public class NetDiscovery : MonoBehaviour { 
    // ── Serializable message types ──────────────────────────────────────────── 
    enum MessageType : byte { BroadCast, Response }

    // Sent by the client (XR headset) to locate a server on the LAN
    struct DiscoveryBroadcastData : INetworkSerializeByMemcpy {
        public FixedString64Bytes AppName;
        public FixedString64Bytes Version;
    }

    // Sent by the server (Coach) in reply; contains the address the client should connect to
    struct DiscoveryResponseData : INetworkSerializeByMemcpy {
        public FixedString64Bytes ServerIP;
        public ushort Port; 
    }

    // Magic bytes that guard every packet so stray UDP traffic is ignored
    const ulong DISCOVERY_MAGIC = 0x42415342414C4C44UL; 

    // ── Fields ────────────────────────────────────────────────────────────────

    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField, Get] UnityTransport _transport;
    [SerializeField, Get] NetBoot _netBoot;
    [SerializeField] ushort _discoveryPort = 47777;

    UdpClient _client;
    CustomLogger _logger;
    [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    bool _isDiscoveryServer;

    // Cached at Awake — Application.* is not safe to read from background threads
    string _appName;
    string _appVersion;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Start() {
        _logger = new CustomLogger(this, Color.cyan);
        _appName = Application.productName;
        _appVersion = Application.version;

        _netBoot.OnPlayerTypeSetup += OnPlayerTypeSetup;
        _netMng.OnConnectionEvent += OnConnectionEvent;
        RevalidateDiscoveryStatus();
    }


    private void OnPlayerTypeSetup(NetBoot boot) => RevalidateDiscoveryStatus(); 

    Awaitable _recieveBroadcastAwait;
    Awaitable _recieveResponseAwait;
    Awaitable _sendBroadcastAwait;
    Awaitable _setupNetworkAwait;
    DiscoveryResponseData? _recievedResponse;
    bool _isRunningDiscovery;

    private void OnDestroy() {
        if(_isRunningDiscovery)
            StopDiscovery();
        _netBoot.OnPlayerTypeSetup -= OnPlayerTypeSetup;
        _netMng.OnConnectionEvent -= OnConnectionEvent;
    }


    // ── Discovery enable/disable logic ────────────────────────────────────────
    private void OnConnectionEvent(NetworkManager netMng, ConnectionEventData data) {
        RevalidateDiscoveryStatus();
    }
    void RevalidateDiscoveryStatus() {
        bool shouldDiscover = _netBoot &&
                              _netBoot.PlayerTypeReady && _netMng;

        if (_netMng.IsClient && !_netMng.IsConnectedClient)
            shouldDiscover = true;

        if (_netMng.IsConnectedClient)
            shouldDiscover = false;

        if(shouldDiscover == _isRunningDiscovery)
            return; 

        _logger.Log($"Revalidating discovery — enabled: {shouldDiscover}");
        enabled = shouldDiscover;

        if (shouldDiscover) { StartDiscovery(); } 
        else { StopDiscovery(); }
    }

    // ── Discovery start / stop ────────────────────────────────────────────────

    public void StartDiscovery() {
        if(_isRunningDiscovery)
            StopDiscovery();
        _logger.Log("Discovery starting...");
        _recievedResponse = null;
        _isRunningDiscovery = true;
        _isDiscoveryServer = _netBoot.IsCoach;

        // Server binds to _port; client lets the OS pick a free port
        var bindPort = _isDiscoveryServer ? _discoveryPort : (ushort)0;
        _client = new UdpClient(bindPort) { EnableBroadcast = true, MulticastLoopback = false };

        _setupNetworkAwait = AwaitTrySetupNetwork();
        if (_isDiscoveryServer) {
            _recieveBroadcastAwait = ListenLoopAsync(ReceiveBroadcastAsync);
        } else {
            _recieveResponseAwait = ListenLoopAsync(ReceiveResponseAsync);
            _sendBroadcastAwait = ListenLoopAsync(SendBroadcastLoopAsync); 
        }
    }

    void StopDiscovery() {
        _isRunningDiscovery = false;
        _logger.Log("Discovery stopping...");
        _recieveBroadcastAwait?.Cancel();
        _sendBroadcastAwait?.Cancel();
        _recieveResponseAwait?.Cancel();
        _setupNetworkAwait?.Cancel();
        _client?.Close(); 
        _isDiscoveryServer = false;
        _client = null;
    }

    // ── Shared async infrastructure ───────────────────────────────────────────

    async Awaitable ListenLoopAsync(Func<Awaitable> onReceiveTask) {
        while (_isRunningDiscovery) {
            try {
                await onReceiveTask();
            } catch (ObjectDisposedException) {
                // Socket was closed — exit the loop gracefully
                _logger.Log("Socket closed");
                return;
            } catch (OperationCanceledException) {
                return;
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }
    }

    // ── Header helpers ────────────────────────────────────────────────────────

    void WriteHeader(FastBufferWriter writer, MessageType type) {
        writer.WriteValueSafe(DISCOVERY_MAGIC);
        writer.WriteValueSafe((byte)type);
    }

    bool ReadAndCheckHeader(FastBufferReader reader, MessageType expectedType) {
        reader.ReadValueSafe(out ulong magic);
        if (magic != DISCOVERY_MAGIC)
            return false;
        reader.ReadValueSafe(out byte typeByte);
        return (MessageType)typeByte == expectedType;
    }

    // ── Client-side: broadcast + receive response ─────────────────────────────

    // Called by ListenAsync in a loop: sends one broadcast then waits 2 seconds
    async Awaitable SendBroadcastLoopAsync() {
        if (_client == null) return;
        byte[] data; 
        using (var writer = new FastBufferWriter(256, Allocator.Temp)) {
            WriteHeader(writer, MessageType.BroadCast);
            writer.WriteValueSafe(new DiscoveryBroadcastData {
                AppName = new FixedString64Bytes(_appName),
                Version = new FixedString64Bytes(_appVersion),
            });
            data = writer.ToArray();
        }
        var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, _discoveryPort); 
        _logger.Log("Sending discovery broadcast...");
        await _client.SendAsync(data, data.Length, broadcastEndPoint);
        await Awaitable.WaitForSecondsAsync(2f); 
    }

    async Awaitable ReceiveResponseAsync() {
        if (_client == null) return; 

        UdpReceiveResult result = await _client.ReceiveAsync();
        using var reader = new FastBufferReader(result.Buffer, Allocator.Temp);

        try {
            if (!ReadAndCheckHeader(reader, MessageType.Response))
                return;

            reader.ReadValueSafe(out DiscoveryResponseData response);

            _logger.Log($"Server response received from {result.RemoteEndPoint} — IP: {response.ServerIP}, Port: {response.Port}");
            await Awaitable.MainThreadAsync();
            _recievedResponse = response; 
        } catch (Exception e) {
            Debug.LogException(e);
        }
    } 
     
    // ── Server-side: receive broadcast + send response ────────────────────────
    public event Action<(string recievedAppName, string recievedAppVer)> OnVersionMismatch;
    async Awaitable ReceiveBroadcastAsync() {
        if (_client == null) return;

        _logger.Log("Waiting for clients broadcasts...");
        UdpReceiveResult result = await _client.ReceiveAsync();
        using var reader = new FastBufferReader(result.Buffer, Allocator.Temp);

        try {
            if (!ReadAndCheckHeader(reader, MessageType.BroadCast))
                return;

            reader.ReadValueSafe(out DiscoveryBroadcastData broadcast);
            _logger.Log($"Broadcast received from {result.RemoteEndPoint} — App: {broadcast.AppName}, Version: {broadcast.Version}");

            if (!ProcessBroadcast(broadcast, out DiscoveryResponseData response)) {
                _logger.LogWarning($"Broadcast rejected: app/version mismatch (expected {_appName} {_appVersion})");
                OnVersionMismatch?.Invoke((broadcast.AppName.ToString(), broadcast.Version.ToString())); 
                return;
            }

            byte[] data;
            using (var writer = new FastBufferWriter(256, Allocator.Temp)) {
                WriteHeader(writer, MessageType.Response);
                writer.WriteValueSafe(response);
                data = writer.ToArray();
            }

            _logger.Log($"Sending discovery response to {result.RemoteEndPoint} — IP: {response.ServerIP}, Port: {response.Port}");
            await _client.SendAsync(data, data.Length, result.RemoteEndPoint);
        } catch (Exception e) {
            Debug.LogException(e);
        }
    }

    // Validates the broadcast and builds a response with this machine's IP and port
    bool ProcessBroadcast(DiscoveryBroadcastData broadcast, out DiscoveryResponseData response) {
        response = default;

        if (broadcast.AppName != _appName || broadcast.Version != _appVersion)
            return false;

        var localIP = GetLocalIPAddress();
        if (localIP == null)
            return false;

        response = new DiscoveryResponseData {
            ServerIP = new FixedString64Bytes(localIP),
            Port = _transport.ConnectionData.Port,
        };
        return true;
    }

    // Returns the machine's outbound LAN IP without sending any actual traffic
    static string GetLocalIPAddress() {
        try {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)socket.LocalEndPoint)?.Address.ToString();
        } catch {
            return null;
        }
    }
     
    async Awaitable AwaitTrySetupNetwork() {
        await Awaitable.MainThreadAsync();

        if (_netMng.IsClient && ! _netMng.IsServer)
            _netMng.Shutdown();

        if (_netMng.ShutdownInProgress) {
            await Awaitable.NextFrameAsync();
            return;
        }

        if (!_netBoot.PlayerTypeReady) {
            await Awaitable.NextFrameAsync();
            return;
        }

        bool TrySetup(bool isXR, bool isCoach) {
            if (isXR && isCoach)
                return _netMng.StartHost();
            if (isCoach)
                return _netMng.StartServer();
            if (isXR && _recievedResponse.HasValue) {
                var res = _recievedResponse.Value;
                _logger.Log($"Connecting to server at {res.ServerIP}:{res.Port}...");
                _transport.SetConnectionData(res.ServerIP.ToString(), res.Port);
                var succsuss = _netMng.StartClient();
                if ( !succsuss)  _logger.LogError("Failed to connect client!");
                return succsuss;
            }
            return false;
        }

        while(!TrySetup(_netBoot.IsXR, _netBoot.IsCoach)) {
            await Awaitable.WaitForSecondsAsync(0.15f);
        }

        if (!_netMng.IsServer) {
            _logger.Log("Connected via discovery");
            StopDiscovery();
        }
    }


}
