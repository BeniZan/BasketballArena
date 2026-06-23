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
    [SerializeField] NetworkObject _CoachHostPrefab, _XRClientPrefab;
    [SerializeField] GameObject LocalXRDeviceToggle;
    GameObject _spawnedXRDevice;
    public ReadOnlyNotifier<PlayerType> Type => _playerType;
    public bool IsXR => _playerType.Value == PlayerType.XRPlayer;
    public bool IsCoach => _playerType.Value == PlayerType.Coach;
    public bool PlayerTypeReady => _playerType.Value != PlayerType.NotSetup;
    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    public bool IsConnected => _netMng.IsListening && _netMng.IsConnectedClient;  
    protected override void Awake() {
        base.Awake();

        _netMng.OnConnectionEvent += NetMng_OnConnectionEvent;
        DontDestroyOnLoad(gameObject);

#if !UNITY_EDITOR 
        var deviceModel = SystemInfo.deviceModel.ToLower();
        var isXR = deviceModel.Contains("quest") || deviceModel.Contains("oculus");
        SetupPlayerType(isXR);
#endif
    }

    void UnsetPlayerType() {
        _netMng.Shutdown();
        _playerType.Value = PlayerType.NotSetup;
        LocalXRDeviceToggle.SetActive(false);
    }

    public void SetupPlayerType(bool isXR) {
        if (PlayerTypeReady) {
            Debug.LogWarning("Player type already setup, ignoring LocalSetup call");
            return;
        }
        var type = isXR ? PlayerType.XRPlayer : PlayerType.Coach;
        _playerType.Value = type;
        StartNetwork(isXR);
        LocalXRDeviceToggle.SetActive(isXR);
    } 

    private void NetMng_OnConnectionEvent(NetworkManager nm, ConnectionEventData data) {
        var isLocalClientEvent = data.ClientId == nm.LocalClientId; 
        if (data.EventType == ConnectionEvent.ClientDisconnected && isLocalClientEvent) {
            _playerType.Value = PlayerType.NotSetup;
            return;
        }
         
        if(data.EventType == ConnectionEvent.ClientConnected) {
            var isCoach = data.ClientId == NetworkManager.ServerClientId; // coach only if we're setup as server
            if (nm.IsServer) {
                var prefab = isCoach ? _CoachHostPrefab : _XRClientPrefab;
                nm.SpawnManager.InstantiateAndSpawn(prefab, data.ClientId, true, true);
            }  
        } 
    }

    void StartNetwork(bool isClient) {
        if (!PlayerTypeReady) {
            Debug.LogError("Player type not setup, cannot connect");
            return;
        }
        if (isClient)
            _netMng.StartClient();
        else _netMng.StartServer();
    }

#if UNITY_EDITOR
    private void OnGUI() {
        if (!Application.isEditor)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try {
            if (PlayerTypeReady) {
                if (IsConnectionAwaiting) { ConnectionAwaitingGUI(); }
                else if (IsConnected) { ConnectedGUI(); }
                else { GUILayout.Label("Player type setup but not conneting"); }
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
        if (GUILayout.Button("Setup As Coach"))
            SetupPlayerType(false);

        if (GUILayout.Button("Setup As XR Player"))
            SetupPlayerType(true);
    }
#endif
}