using SingletonBehaviors;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
 
public class DrillActivator : SingletonMono<DrillActivator> { 
    [SerializeField] CharComponent _templateChar; 
    [SerializeField] Transform _courtTf;
    [SerializeField] SurfaceHandler _courtSurface;
    [SerializeField] NetDrillsActivator _manager;
    [SerializeField, ReadOnly] List<CharComponent> _spawnedChars = new List<CharComponent>();
    [ShowInInspector, ReadOnly, HideInEditorMode] DrillData _currentActive;
    public event Action OnDrillChange;
    public IReadOnlyList<CharComponent> PlacedChars => _spawnedChars;
    protected override void Awake() {
        base.Awake();
        _manager.ActiveManeuver.Sub(OnTeamManeverChange);
    }
    void OnTeamManeverChange(DrillData data)  => Activate(data); 
    public void Activate(DrillData move) {
        if (_currentActive)
            Deactivate();
        if (move) 
            _currentActive = move;
        UpdateChars();
        OnDrillChange?.Invoke();
    }
    public void UpdateChars() {
        if (!_currentActive)
            return;

        var pos = _currentActive.OriginPoint;
        var rot = Quaternion.Euler(0f, _currentActive.OriginYRotation, 0f);
        _courtSurface.ParentAndPlace(_courtTf);
        _courtTf.SetLocalPositionAndRotation(pos, rot);

        int i = 0;
        for (; i < _currentActive.CharsData.Count; i++) {
            if (_spawnedChars.Count <= i) {
                var spawned = Instantiate(_templateChar, _courtTf);
                spawned.gameObject.SetActive(true); 
                _spawnedChars.Add(spawned);
            }
            _spawnedChars[i].SetData(_currentActive.CharsData[i], _currentActive.MirrorLeftRight);
        }
        while(i < _spawnedChars.Count) {
            if (_spawnedChars[i])
                _spawnedChars[i].gameObject.SafeDestroy();
            _spawnedChars.RemoveAt(i);
        }
    } 
    public void Deactivate() {
        foreach (var placedChar in _spawnedChars)
            if(placedChar)
                placedChar.gameObject.SafeDestroy();
        _spawnedChars.Clear();
        _currentActive = null;
        OnDrillChange?.Invoke();
    }
    private void OnEnable() {
        Activate(_currentActive);
    }
    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        GizmosU.GizmosArrow(transform.position, transform.rotation.EulerSeperateY() * Vector3.forward);
    } 
}
