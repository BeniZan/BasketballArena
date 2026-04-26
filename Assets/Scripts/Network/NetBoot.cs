using SingletonBehaviors;
using Unity.Netcode;
using UnityEngine;
public class NetBoot : SingletonMono<NetBoot> {
    public Notifier<bool> IsCoach = new();
    public bool PlayerTypeSetup;
    [SerializeField, Get] NetworkManager _netMng; 

    public void Setup(bool isCoach) {
        if (isCoach)
            _netMng.StartHost();
        else _netMng.StartClient();
        PlayerTypeSetup = true;
        IsCoach.Value = isCoach;
    }

    private void OnGUI() {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
         
        if (!PlayerTypeSetup) {
            if (GUILayout.Button("Setup As Coach"))
                Setup(true);

            if (GUILayout.Button("Setup As XR Player"))
                Setup(false); 
        }
        else {
            var lbl = $"Mode: {(_netMng.IsHost ? "Host" : _netMng.IsServer ? "Server" : "Client")}";
            lbl += "\nPlaying as " + (IsCoach.Value ? "Coach" : "Player");
            GUILayout.Label(lbl); 
        }

        GUILayout.EndArea();
    }
}