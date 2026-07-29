using SingletonBehaviors;
using Sirenix.OdinInspector;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode; 
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
[DefaultExecutionOrder(-1000)]
public class NetBoot : SingletonMono<NetBoot> { 
    [Flags] enum PlayerType { NotSetup = 1 << 0, XRPlayer = 1 << 1, Coach = 1 << 2 }
    [ShowInInspector, HideInEditorMode, ReadOnly] PlayerType _playerType;
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField] NetworkObject  _XRClientPrefab;
    [SerializeField] GameObject LocalXRDevice, LocalCoachDevice; 
    public NetworkManager NetMnger => _netMng; 
    public bool IsXR => _playerType.HasFlag(PlayerType.XRPlayer); 
    public bool IsCoach => _playerType.HasFlag(PlayerType.Coach);
    public bool PlayerTypeReady => IsXR || IsCoach;
    [ShowInInspector, HideInEditorMode]
    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    [ShowInInspector, HideInEditorMode]
    public bool IsConnected =>  _netMng.IsServer || _netMng.IsConnectedClient;
    CustomLogger _logger;
    public event Action<NetBoot> OnPlayerTypeChange;
#if UNITY_EDITOR  
    [SerializeField] PlayerType _editorAutoSetupConfig = PlayerType.NotSetup; 
#endif 

    protected override void Awake() {
        _logger = new CustomLogger(this, Color.softBlue);
        if (HasInstance && Instance != this) {
            gameObject.SafeDestroy();
            _logger.Log(this, "Multiple instances of NetBoot detected, destroying self duplicate");
            return;
        }
        base.Awake();
    } 

    private void Start() {
        _netMng.OnConnectionEvent += NetMng_OnConnectionEvent; 
        _netMng.OnServerStarted += NetMng_OnServerStarted;
        _netMng.OnPreShutdown += NetMng_OnShutdown;   
        DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR 
        SetupPlayerType(_editorAutoSetupConfig);
#else
        var deviceModel = SystemInfo.deviceModel.ToLower();
        var isOnXRDevice = deviceModel.Contains("quest") || deviceModel.Contains("oculus");
        SetupPlayerType(isOnXRDevice);
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
        LocalCoachDevice.SetActive(IsCoach);
        LocalXRDevice.SetActive(IsXR);
        OnPlayerTypeChange?.Invoke(this);
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