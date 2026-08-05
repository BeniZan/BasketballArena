using Unity.Netcode;
using UnityEngine;

public class NetSpawnToggle : NetworkBehaviour
{
    private void Awake() {
        ToggleChilds(false);
    }

    void ToggleChilds(bool active) {
        foreach (var child in transform.LoopChildren())
            child.gameObject.SetActive(active);
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        ToggleChilds(true);
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        ToggleChilds(false);
    }

}
