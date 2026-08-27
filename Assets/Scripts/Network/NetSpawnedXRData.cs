using Unity.Netcode;
#if UNITY_EDITOR
#endif
public class NetSpawnedXRData : NetworkBehaviour {
    static public NetSpawnedXRData Local { get; private set; }
    static public NetSpawnedXRData GetDataFor(ulong clientID) {
        var netManager = NetworkManager.Singleton;
        if (!netManager)
            return null;
        return netManager.SpawnManager.GetPlayerNetworkObject(clientID).GetComponent<NetSpawnedXRData>();
    }
    public NetworkVariable<float> FPS = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> IsInStartingPosition = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected override void OnNetworkPostSpawn() {
        base.OnNetworkPostSpawn(); 
        if (IsOwner)
            Local = this;
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        Local = null;
    }
}
