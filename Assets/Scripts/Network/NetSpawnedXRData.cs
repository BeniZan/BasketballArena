using System;
using Unity.Netcode;
using UnityEngine;
using DecisionEngine.Model.Rating;
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
    // Coach-controlled: draw the decision-engine hex grid on the player's court.
    public NetworkVariable<bool> ShowHexGrid = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Raised on the server whenever a player submits a decision-quality report.
    public static event Action<NetSpawnedXRData, ScoringReport> ScoringReportReceived;
    // Last report this player submitted, as seen on the server.
    public ScoringReport LastScoringReport { get; private set; }

    protected override void OnNetworkPostSpawn() {
        base.OnNetworkPostSpawn(); 
        if (IsOwner)
            Local = this;
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        Local = null;
    }

    // Owner-side entry point: ship a finished report to the coach.
    public void SubmitScoringReport(ScoringReport report) {
        if (!IsSpawned || report == null) return;
        var json = report.ToJson();
        if (IsServer) ReceiveReport(json);
        else SubmitScoringReportRpc(json);
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
    void SubmitScoringReportRpc(string json) {
        ReceiveReport(json);
    }

    void ReceiveReport(string json) {
        try {
            LastScoringReport = ScoringReport.FromJson(json);
        } catch (Exception e) {
            Debug.LogError($"[NetSpawnedXRData] Bad scoring report from client {OwnerClientId}: {e.Message}", this);
            return;
        }
        Debug.Log($"[NetSpawnedXRData] Scoring report from client {OwnerClientId}: {LastScoringReport}", this);
        ScoringReportReceived?.Invoke(this, LastScoringReport);
    }
}
