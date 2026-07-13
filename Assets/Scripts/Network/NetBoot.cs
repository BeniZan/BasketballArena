using NUnit.Framework;
using SingletonBehaviors;
using System;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
[DefaultExecutionOrder(-1000)]
public class NetBoot : SingletonMono<NetBoot> {
    [Flags] enum PlayerType { NotSetup = 1 << 0, XRPlayer = 1 << 1, Coach = 1 << 2 }
    PlayerType _playerType;
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField] NetworkObject  _XRClientPrefab;
    [SerializeField] GameObject LocalXRDeviceToggle, _localCoachHostToggle;
    public bool IsXR => _playerType.HasFlag(PlayerType.XRPlayer);
    public bool IsCoach => _playerType.HasFlag(PlayerType.Coach);
    public bool PlayerTypeReady => IsXR || IsCoach;
    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    public bool IsConnected => _netMng.IsListening && (_netMng.IsConnectedClient || _netMng.IsServer);
    CustomLogger _logger;
    public event Action<NetBoot> OnPlayerTypeSetup;
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
        _playerType = PlayerType.NotSetup; 
        OnSetPlayerType();
    }

    public void SetupPlayerType(bool isXR) => SetupPlayerType(isXR, !isXR);
    public void SetupPlayerType(bool isXR, bool isCoach) {
        if (PlayerTypeReady) {
            _logger.LogWarning("Player type already setup, ignoring another setup call");
            return;
        }
        PlayerType type = 0;
        if (isXR)
            type |= PlayerType.XRPlayer;
        if (isCoach)
            type |= PlayerType.Coach;
         
        _logger.Log($"setting up player type as: [{type}]");
        _playerType = type;
        OnSetPlayerType();
    }

    void OnSetPlayerType() {
        SetupNetwork();
        LocalXRDeviceToggle.SetActive(IsXR);
        _localCoachHostToggle.SetActive(!IsXR && IsCoach);
        OnPlayerTypeSetup?.Invoke(this);
    }

    public void SetupAsXRCoachDebug() => SetupPlayerType(true, true);

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

    void SetupNetwork() {
        if (!PlayerTypeReady) {
            _logger.Log("Player type not setup, shutting down...");
            _netMng.Shutdown();
            return;
        }

        if (IsCoach) {
            if (IsXR)
                _netMng.StartHost();
            else
                _netMng.StartServer();
        }
        else _netMng.StartClient(); 
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