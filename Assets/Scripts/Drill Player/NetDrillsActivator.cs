using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-1000)]
public class NetDrillsActivator : NetworkBehaviour
{
    static public NetDrillsActivator Instance { get; private set; }
    [SerializeField, Sirenix.OdinInspector.ReadOnly] List<DrillData> _allTeamManeuvers;
    NetworkVariable<FixedString512Bytes> _syncActiveManeuver 
        = new(new FixedString512Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [ShowInInspector]
    readonly Notifier<DrillData> _activeManeuver =  new();
    public ReadOnlyNotifier<DrillData> ActiveManeuver => _activeManeuver;
    public IReadOnlyList<DrillData> AllTeamManeuvers => _allTeamManeuvers;
    public int ActiveManeuverIdx => _activeManeuver.Value ? _allTeamManeuvers.IndexOf(_activeManeuver.Value) : -1;
#if UNITY_EDITOR
    private void OnValidate() {
        _allTeamManeuvers =
            AssetDatabase.FindAssets("t:" + nameof(DrillData))
            .Select(tms => AssetDatabase.LoadAssetByGUID<DrillData>(new GUID(tms))).ToList();
        _allTeamManeuvers.RemoveDestroyed();
    }
#endif

    private void Awake() {
        if (Instance) {
            Debug.LogError("Two " + nameof(NetDrillsActivator) + " exists");
            Destroy(this);
            return;
        }
        Instance = this;
        _syncActiveManeuver.OnValueChanged -= OnSyncManeuverChange;
        _syncActiveManeuver.OnValueChanged += OnSyncManeuverChange;
        OnSyncManeuverChange(new FixedString512Bytes(), _syncActiveManeuver.Value);
    } 

    void OnSyncManeuverChange(FixedString512Bytes _, FixedString512Bytes cur) {
        var name = cur.ToString();
        _activeManeuver.Value = string.IsNullOrEmpty(name) ? null : GetDrill(name); 
    }

    public void Server_SetActiveDrill(int i) => _syncActiveManeuver.Value = _allTeamManeuvers[i].name;
    public void Server_SetActiveDrill(DrillData teamManeuver) => _syncActiveManeuver.Value = teamManeuver ? teamManeuver.name : "";
    public DrillData GetDrill(string teamManeuverName) => _allTeamManeuvers.Find(tm => tm.name == teamManeuverName);
}
