using SingletonBehaviors;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
public class NetBoot : SingletonMono<NetBoot> {
    Notifier<bool> _isCoach = new();
    [SerializeField, Get] NetworkManager _netMng;
    [SerializeField] NetworkObject _CoachHostPrefab, _XRClientPrefab;
    public ReadOnlyNotifier<bool> IsCoach => _isCoach;
    public bool PlayerTypeSetup { get; private set; }
    protected override void Awake() {
        base.Awake();
        _netMng.OnConnectionEvent += NetMng_OnConnectionEvent;
        DontDestroyOnLoad(gameObject);
    }

    public void Setup(bool isCoach) { 
        PlayerTypeSetup = true;
        _isCoach.Value = isCoach; 
    }

    private void NetMng_OnConnectionEvent(NetworkManager nm, ConnectionEventData data) {
        var isLocalClientEvent = data.ClientId == nm.LocalClientId;
        if (data.EventType == ConnectionEvent.ClientDisconnected && isLocalClientEvent) {
            PlayerTypeSetup = false;
            _isCoach.Value = false;
            _isCoach.InvokeChanged();
            return;
        }
         
        if(data.EventType == ConnectionEvent.ClientConnected) {
            var isCoach = data.ClientId == NetworkManager.ServerClientId; // coach only if we're setup as server
            if (nm.IsServer) {
                var prefab = isCoach ? _CoachHostPrefab : _XRClientPrefab;
                nm.SpawnManager.InstantiateAndSpawn(prefab, data.ClientId, true, true);
            }

            if (isLocalClientEvent)
                Setup(isCoach);
        }
    } 

    void ConnectAs(bool isHost) {
        if (isHost)
            _netMng.StartHost();
        else _netMng.StartClient();
    }

    public bool IsConnectionAwaiting => _netMng.IsListening && _netMng.IsClient && !_netMng.IsConnectedClient;
    public bool IsConnected => _netMng.IsListening && _netMng.IsConnectedClient;

#if UNITY_EDITOR
    private void OnGUI() { 
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try {
            if (IsConnectionAwaiting)   { ConnectionAwaitingGUI(); }
            else if (IsConnected)       { ConnectedGUI(); }
            else                        { NotConnectedGUI(); }
        } catch(System.Exception ex) { Debug.LogException(ex); } 
        finally  { EditorGUILayout.EndVertical();  }
    }

    void ConnectionAwaitingGUI() {
        GUILayout.Label("Connecting...");
        if(GUILayout.Button("Cancel"))
            _netMng.Shutdown();
    }

    void ConnectedGUI() {
        var lbl = $"Connected as: {(_netMng.IsHost ? "Host" : (_netMng.IsServer ? "Server" : "Client") )}";
        if (PlayerTypeSetup)
            lbl += "\nPlaying as " + (_isCoach.Value ? "Coach" : "XRPlayer");
        else lbl += "Error: Connected but player type not setup";
        GUILayout.Label(lbl);
    }

    void NotConnectedGUI() {
        if (GUILayout.Button("Setup As Coach"))
            ConnectAs(true);

        if (GUILayout.Button("Setup As XR Player"))
            ConnectAs(false);
    }
#endif
}