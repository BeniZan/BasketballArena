using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-1000)]
public class NetTeamManeuverManager : NetworkBehaviour
{
    static public NetTeamManeuverManager Instance { get; private set; }
    [SerializeField, ReadOnly] List<TeamManeuverData> _allTeamManeuvers;
    NetworkVariable<string> _syncActiveManeuver 
        = new(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Notifier<TeamManeuverData> _activeManeuver = new();
    public ReadOnlyNotifier<TeamManeuverData> ActiveManeuver => _activeManeuver;

#if UNITY_EDITOR
    private void OnValidate() {
        _allTeamManeuvers =
            AssetDatabase.FindAssets("t:" + nameof(TeamManeuverData))
            .Select(tms => AssetDatabase.LoadAssetByGUID<TeamManeuverData>(new GUID(tms))).ToList();
        _allTeamManeuvers.RemoveDestroyed();
    }
#endif
    void Awake() {
        if (Instance) {
            Debug.LogError("Two " + nameof(NetTeamManeuverManager) + " exists");
            Destroy(this);
            return;
        }
        Instance = this;
        _syncActiveManeuver.OnValueChanged += OnSyncManeuverChange;
        OnSyncManeuverChange(default, _syncActiveManeuver.Value);
    }

    void OnSyncManeuverChange(string _, string cur) {
        _activeManeuver.Value = GetTeamManeuver(cur);
    }

    public void Server_SetTeamManeuver(TeamManeuverData teamManeuver) => _syncActiveManeuver.Value = teamManeuver.name;
    public TeamManeuverData GetTeamManeuver(string teamManeuverName) => _allTeamManeuvers.Find(tm => tm.name == teamManeuverName);
}
