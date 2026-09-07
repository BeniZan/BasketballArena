using Unity.Collections;
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
    /// <summary>
    /// Separates a headset from the coach's tablet. The coach never moves on the court, so
    /// only XR clients count when the server decides whether the drill may start.
    /// </summary>
    public NetworkVariable<bool> IsXRPlayer = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected override void OnNetworkPostSpawn() {
        base.OnNetworkPostSpawn(); 
        if (IsOwner) {
            Local = this;
            IsXRPlayer.Value = NetBoot.HasInstance && NetBoot.Instance.IsXR;
        }
    }

    /// <summary>
    /// Owner reports that it walked into one of the active drill's triggers. The drill name
    /// guards a report that was still in flight while the coach switched drills, which would
    /// otherwise open a gate of the drill that just started.
    /// </summary>
    [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
    public void ReportReachedTrigger_Rpc(int triggerIdx, FixedString512Bytes drillName) {
        var drillPlayer = DrillPlayer.Instance;
        var netDrillsActivator = NetDrillsActivator.Instance;
        if (!drillPlayer || !netDrillsActivator)
            return;

        var activeDrill = netDrillsActivator.ActiveManeuver.Value;
        if (!activeDrill || activeDrill.name != drillName.ToString())
            return;

        drillPlayer.Server_OpenGate(triggerIdx);
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        Local = null;
    }
}
