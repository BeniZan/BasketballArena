using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-1000)]
public class NetTeamManeuverManager : NetworkBehaviour
{
    static public NetTeamManeuverManager Instance { get; private set; }
    [SerializeField, Sirenix.OdinInspector.ReadOnly] List<TeamManeuverData> _allTeamManeuvers;
    NetworkVariable<FixedString512Bytes> _syncActiveManeuver 
        = new(new FixedString512Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Notifier<TeamManeuverData> _activeManeuver = new();
    public ReadOnlyNotifier<TeamManeuverData> ActiveManeuver => _activeManeuver;
    public IReadOnlyList<TeamManeuverData> AllTeamManeuvers => _allTeamManeuvers;
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
        OnSyncManeuverChange(new FixedString512Bytes(), _syncActiveManeuver.Value);
    }

    void OnSyncManeuverChange(FixedString512Bytes _, FixedString512Bytes cur) {
        var name = cur.ToString();
        _activeManeuver.Value = string.IsNullOrEmpty(name) ? null : GetTeamManeuver(name);
    }

    public void Server_SetTeamManeuver(int i) => _syncActiveManeuver.Value = _allTeamManeuvers[i].name;
    public void Server_SetTeamManeuver(TeamManeuverData teamManeuver) => _syncActiveManeuver.Value = teamManeuver ? teamManeuver.name : "";
    public TeamManeuverData GetTeamManeuver(string teamManeuverName) => _allTeamManeuvers.Find(tm => tm.name == teamManeuverName);
}
