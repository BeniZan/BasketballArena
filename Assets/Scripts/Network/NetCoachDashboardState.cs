using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative bridge that broadcasts the Coach Dashboard's live state
/// (Training Flow drills, active drill, timer, stream mode, H1/H2 offense-defense)
/// to every connected client. Mirrors the <see cref="NetTeamManeuverManager"/> pattern:
/// server-write <see cref="NetworkVariable{T}"/>/<see cref="NetworkList{T}"/> plus a manual
/// (non-<c>SingletonMono</c>) <see cref="Instance"/>, since <see cref="NetworkBehaviour"/>
/// already derives from <see cref="MonoBehaviour"/>.
///
/// <see cref="CoachDashboardUIToolkitController"/> is the only writer (it always runs on the
/// Coach, which is always the Netcode server/host), so all Server_* setters are one-directional
/// broadcasts — no ServerRpcs are needed. <see cref="Instance"/> stays null when this component
/// isn't present/spawned (e.g. editing the dashboard standalone without networking), so all
/// call sites must use the null-conditional operator.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class NetCoachDashboardState : NetworkBehaviour
{
    public static NetCoachDashboardState Instance { get; private set; }

    readonly NetworkList<FixedString128Bytes> _syncDrills = new();
    readonly NetworkVariable<int> _syncActiveIndex =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _syncIsTimerRunning =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> _syncElapsedTime =
        new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _syncIsRealisticStream =
        new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _syncH1Offense =
        new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _syncH2Offense =
        new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    readonly List<string> _mirroredDrills = new();

    readonly Notifier<int> _activeIndex = new(-1);
    readonly Notifier<bool> _isTimerRunning = new(false);
    readonly Notifier<float> _elapsedTime = new(0f);
    readonly Notifier<bool> _isRealisticStream = new(true);
    readonly Notifier<bool> _h1Offense = new(true);
    readonly Notifier<bool> _h2Offense = new(true);

    public ReadOnlyNotifier<int> ActiveIndex => _activeIndex;
    public ReadOnlyNotifier<bool> IsTimerRunning => _isTimerRunning;
    public ReadOnlyNotifier<float> ElapsedTime => _elapsedTime;
    public ReadOnlyNotifier<bool> IsRealisticStream => _isRealisticStream;
    public ReadOnlyNotifier<bool> H1Offense => _h1Offense;
    public ReadOnlyNotifier<bool> H2Offense => _h2Offense;
    public IReadOnlyList<string> Drills => _mirroredDrills;

    /// <summary>Raised whenever the replicated drill list changes (add/remove/reorder).</summary>
    public event Action OnDrillsChanged;

    void Awake()
    {
        if (Instance)
        {
            Debug.LogError("Two " + nameof(NetCoachDashboardState) + " exist");
            Destroy(this);
            return;
        }
        Instance = this;

        _syncDrills.OnListChanged += OnSyncDrillsChanged;
        _syncActiveIndex.OnValueChanged += (_, cur) => _activeIndex.Value = cur;
        _syncIsTimerRunning.OnValueChanged += (_, cur) => _isTimerRunning.Value = cur;
        _syncElapsedTime.OnValueChanged += (_, cur) => _elapsedTime.Value = cur;
        _syncIsRealisticStream.OnValueChanged += (_, cur) => _isRealisticStream.Value = cur;
        _syncH1Offense.OnValueChanged += (_, cur) => _h1Offense.Value = cur;
        _syncH2Offense.OnValueChanged += (_, cur) => _h2Offense.Value = cur;

        RebuildMirroredDrills();
        _activeIndex.Value = _syncActiveIndex.Value;
        _isTimerRunning.Value = _syncIsTimerRunning.Value;
        _elapsedTime.Value = _syncElapsedTime.Value;
        _isRealisticStream.Value = _syncIsRealisticStream.Value;
        _h1Offense.Value = _syncH1Offense.Value;
        _h2Offense.Value = _syncH2Offense.Value;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnSyncDrillsChanged(NetworkListEvent<FixedString128Bytes> _)
    {
        RebuildMirroredDrills();
        OnDrillsChanged?.Invoke();
    }

    void RebuildMirroredDrills()
    {
        _mirroredDrills.Clear();
        foreach (var drill in _syncDrills)
            _mirroredDrills.Add(drill.ToString());
    }

    // ---- Server-only setters (called from CoachDashboardUIToolkitController) ----

    public void Server_SetDrills(IReadOnlyList<string> drills)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetDrills called on non-server"); return; }

        _syncDrills.Clear();
        for (int i = 0; i < drills.Count; i++)
            _syncDrills.Add(new FixedString128Bytes(drills[i]));
    }

    public void Server_SetActiveIndex(int index)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetActiveIndex called on non-server"); return; }
        _syncActiveIndex.Value = index;
    }

    public void Server_SetTimerRunning(bool running)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetTimerRunning called on non-server"); return; }
        _syncIsTimerRunning.Value = running;
    }

    public void Server_SetElapsedTime(float elapsed)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetElapsedTime called on non-server"); return; }
        _syncElapsedTime.Value = elapsed;
    }

    public void Server_SetStreamMode(bool isRealistic)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetStreamMode called on non-server"); return; }
        _syncIsRealisticStream.Value = isRealistic;
    }

    public void Server_SetOffense(bool isH1, bool isOffense)
    {
        if (!IsServer) { Debug.LogWarning("Server_SetOffense called on non-server"); return; }
        if (isH1) _syncH1Offense.Value = isOffense;
        else _syncH2Offense.Value = isOffense;
    }
}
