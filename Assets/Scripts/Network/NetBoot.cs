using NUnit.Framework;
using SingletonBehaviors;
using System;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
[DefaultExecutionOrder(-1000)]
public class NetBoot : SingletonMono<NetBoot> {
    public enum PlayerType { NotSetup, XRPlayer, Coach }
    Notifier<PlayerType> _playerType = new Notifier<PlayerType>(PlayerType.NotSetup);
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField] NetworkObject  _XRClientPrefab;
    [SerializeField] GameObject LocalXRDeviceToggle, _localCoachHostToggle;
    public ReadOnlyNotifier<PlayerType> PlayType => _playerType;
    public bool IsXR => _playerType.Value == PlayerType.XRPlayer;
    public bool IsCoach => _playerType.Value == PlayerType.Coach;
    public bool PlayerTypeReady => _playerType.Value != PlayerType.NotSetup;
    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    public bool IsConnected => _netMng.IsListening && (_netMng.IsConnectedClient || _netMng.IsServer);
    CustomLogger _logger;
#if UNITY_EDITOR
    enum AutoSetupConfig { None, XRClient, CoachHost}
    [SerializeField] AutoSetupConfig _editorAutoSetupConfig;
#endif
    private void Start() {
        _logger = new CustomLogger(this, Color.softBlue);
        _netMng.OnConnectionEvent += NetMng_OnConnectionEvent; 
        _netMng.OnServerStarted += NetMng_OnServerStarted;
        _netMng.OnPreShutdown += NetMng_OnShutdown; 
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        if (_editorAutoSetupConfig != AutoSetupConfig.None)
            SetupPlayerType(_editorAutoSetupConfig == AutoSetupConfig.XRClient);
#else
        var deviceModel = SystemInfo.deviceModel.ToLower();
        var isXR = deviceModel.Contains("quest") || deviceModel.Contains("oculus");
        SetupPlayerType(isXR);
#endif
    }

    private void NetMng_OnServerStarted() {
        _logger.Log("Server started");
    }
    private void NetMng_OnShutdown() {
        _logger.Log("Server shutdown");
        UnsetPlayerType();
    }

    void UnsetPlayerType() {
        _logger.Log("Unset player type");
        if(!_netMng.ShutdownInProgress)
            _netMng.Shutdown();
        _playerType.Value = PlayerType.NotSetup;
        LocalXRDeviceToggle.SetActive(false);
        _localCoachHostToggle.SetActive(false);
    }

    public void SetupPlayerType(bool isXR) {
        if (PlayerTypeReady) {
            _logger.LogWarning("Player type already setup, ignoring another setup call");
            return;
        }
        var type = isXR ? PlayerType.XRPlayer : PlayerType.Coach;
        _logger.Log("setting up player type as: " + type);
        _playerType.Value = type;
        StartNetwork(isXR);
        LocalXRDeviceToggle.SetActive(isXR);
        _localCoachHostToggle.SetActive(!isXR);
    }

    private void NetMng_OnConnectionEvent(NetworkManager nm, ConnectionEventData data) {
        var isLocalClientEvent = data.ClientId == nm.LocalClientId;
        if (isLocalClientEvent) {
            if (data.EventType == ConnectionEvent.ClientConnected)
                _logger.Log("Connected");
            if(data.EventType == ConnectionEvent.ClientDisconnected) {
                _logger.Log("Disconnected");
                UnsetPlayerType();
            }
        }
        else {
            _logger.Log($"ConnectionEvent: client[{data.ClientId}] -> {data.EventType}");
        } 
    }

    void StartNetwork(bool isClient) {
        if (!PlayerTypeReady) {
            _logger.LogError("Player type not setup, cannot connect");
            return;
        }
        if (isClient)
            _netMng.StartClient();
        else _netMng.StartServer();
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        _netMng.OnConnectionEvent -= NetMng_OnConnectionEvent;
        _netMng.OnServerStarted -= NetMng_OnServerStarted;
        _netMng.OnPreShutdown -= NetMng_OnShutdown;
    }

#if UNITY_EDITOR

    private void OnGUI() {
        if (!Application.isEditor)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try {
            if (PlayerTypeReady) { 
                if (IsConnectionAwaiting) { 
                    ConnectionAwaitingGUI(); 
                }
                else if (IsConnected) 
                    { 
                    ConnectedGUI(); 
                } 
                else {
                    GUILayout.Label("Player type setup but network manager connection not setup");
                }
            }
            else {
                PlayerTypeSetupGUI();
            }
        } catch(System.Exception ex) { Debug.LogException(ex); } 
        finally  { EditorGUILayout.EndVertical();  }
    } 

    void ConnectionAwaitingGUI() {
        GUILayout.Label("Connecting...");
        if (GUILayout.Button("Cancel")) 
            UnsetPlayerType();
    }

    void ConnectedGUI() {
        var lbl = $"Connected as: {(_netMng.IsHost ? "Host" : (_netMng.IsServer ? "Server" : "Client") )}";
        if (PlayerTypeReady)
            lbl += "\nPlaying as " + (IsXR ? "XRPlayer" : "Coach");
        else lbl += "\nError: Connected but player type not setup";
        GUILayout.Label(lbl);
    }
    
    void PlayerTypeSetupGUI() {
        if (GUILayout.Button("Setup As Coach")) {
            SetupPlayerType(false);
            GUIUtility.keyboardControl = 0;
        }

        if (GUILayout.Button("Setup As XR Player")) {
            SetupPlayerType(true);
            GUIUtility.keyboardControl = 0;
        }

    }
#endif
}