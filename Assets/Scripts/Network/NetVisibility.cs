using Unity.Netcode;
using UnityEngine;


public class NetVisibility : NetworkBehaviour
{
    public enum Visibility { CoachOnly, XRPlayersOnly, OwnerXRPlayerOnly, LocalXROnly }
    public Visibility visibility;
    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        gameObject.SetActive(IsVisible());  
    }
     
    bool IsVisible() {
        var isXR = !IsServer;
        if (visibility == Visibility.CoachOnly)
            return !isXR;
        else if (visibility == Visibility.XRPlayersOnly)
            return isXR;
        else if (visibility == Visibility.OwnerXRPlayerOnly)
            return isXR && IsOwner; 
        Debug.LogError("Not implemented visibility for " + visibility, this);
        return false;
    }
     
}
