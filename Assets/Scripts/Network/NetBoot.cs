using SingletonBehaviors;
using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
[DefaultExecutionOrder(-1000)]
public class NetBoot : SingletonMono<NetBoot> { 
    [Flags] enum PlayerType { NotSetup = 1 << 0, XRPlayer = 1 << 1, Coach = 1 << 2 }
    PlayerType _playerType;
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField] NetworkObject  _XRClientPrefab;
    [SerializeField] GameObject LocalXRDeviceToggle, _localCoachHostToggle;
    public NetworkManager NetMnger => _netMng;
    public bool IsXR => _playerType.HasFlag(PlayerType.XRPlayer);
    public bool IsCoach => _playerType.HasFlag(PlayerType.Coach);
    public bool PlayerTypeReady => IsXR || IsCoach;
    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    public bool IsConnected =>  _netMng.IsServer || _netMng.IsConnectedClient;
    CustomLogger _logger;
    public event Action<NetBoot> OnPlayerTypeSetup;
#if UNITY_EDITOR  
    [SerializeField] PlayerType _editorAutoSetupConfig = PlayerType.NotSetup; 
#endif
    bool IsOnXRDevice() {
        var deviceModel = SystemInfo.deviceModel.ToLower();
        return deviceModel.Contains("quest") || deviceModel.Contains("oculus");
    }
    private void Start() {
        _logger = new CustomLogger(this, Color.softBlue);
        _netMng.OnConnectionEvent += NetMng_OnConnectionEvent; 
        _netMng.OnServerStarted += NetMng_OnServerStarted;
        _netMng.OnPreShutdown += NetMng_OnShutdown;   
        DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR 
        SetupPlayerType(_editorAutoSetupConfig); 
#else
        SetupPlayerType(IsOnXRDevice());
#endif
    }

    private void NetMng_OnServerStarted() {
        _logger.Log("Server started");
    }
    private void NetMng_OnShutdown() {
        _logger.Log("Server shutdown"); 
    }
#if UNITY_EDITOR
    void UnsetPlayerType() {
        _logger.Log("Unset player type");
        if(!_netMng.ShutdownInProgress)
            _netMng.Shutdown();
        _playerType = PlayerType.NotSetup; 
        OnSetPlayerType();
    }
#endif
    void SetupPlayerType(PlayerType player) => SetupPlayerType(player.HasFlag(PlayerType.XRPlayer), player.HasFlag(PlayerType.Coach));
    public void SetupPlayerType(bool isXR) => SetupPlayerType(isXR, !isXR);
    public void SetupPlayerType(bool isXR, bool isCoach) {
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
            }
        }
        else {
            _logger.Log($"ConnectionEvent: client[{data.ClientId}] -> {data.EventType}");
        } 
    }

    Awaitable _awaitingShutdown;

    async Awaitable SetupNetwork() {
        while (_awaitingShutdown != null && !_awaitingShutdown.IsCompleted)
            await Awaitable.NextFrameAsync();
        _awaitingShutdown = AwaitSetupNetwork();
    }

    async Awaitable AwaitSetupNetwork() {
        try {
            if (_netMng.IsServer || _netMng.IsClient)
                _netMng.Shutdown();

            if (!PlayerTypeReady) {
                _logger.Log("Player type not setup, ignoring SetupNetwork call...");
                return;
            }

            bool TrySetup(bool isXR, bool isCoach) {
                if (isXR && isCoach)
                    return _netMng.StartHost();
                if (isCoach)
                    return _netMng.StartServer();
                if (isXR)
                    return _netMng.StartClient();
                return false;
            }

            while (!TrySetup(IsXR, IsCoach)) {
                await Awaitable.NextFrameAsync();
            }
        } catch(Exception ex) { Debug.LogException(ex); }
    }

 

    protected override void OnDestroy() {
        base.OnDestroy();
        _netMng.OnConnectionEvent -= NetMng_OnConnectionEvent;
        _netMng.OnServerStarted -= NetMng_OnServerStarted;
        _netMng.OnPreShutdown -= NetMng_OnShutdown;
        if(_awaitingShutdown != null && !_awaitingShutdown.IsCompleted)
            _awaitingShutdown.Cancel();
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
                    GUILayout.Label($"Player type setup (xr:{IsXR}, coach:{IsCoach}) but network manager connection not setup");
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
        if (IsXR)
            lbl += "\nXR Role set";
        if (IsCoach)
            lbl += "\nCoach Role set";
        if (!PlayerTypeReady)
            lbl += "\nWarning: Connected but player type not setup"; 
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