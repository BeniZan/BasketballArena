using Unity.Netcode;
using UnityEngine;


public class NetVisibility : NetworkBehaviour
{
    public enum Visibility { CoachOnly, XRPlayerOnly }
    public Visibility visibility;
    [SerializeField, Get] NetworkObject netObj;


    protected override void OnNetworkPreSpawn(ref NetworkManager networkManager) {
        base.OnNetworkPreSpawn(ref networkManager);
        gameObject.SetActive(IsVisible(ref networkManager));
    }
     
    bool IsVisible(ref NetworkManager nm) {
        var isServer = nm.IsServer;
        if(visibility == Visibility.CoachOnly)
            return isServer;
        else if(visibility == Visibility.XRPlayerOnly)
            return !isServer;
        Debug.LogError("Not implemented visibility for " + visibility, this);
        return true;
    }
     
}
