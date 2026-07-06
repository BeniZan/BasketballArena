using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
 
public class TeamManeuverPlacer : MonoBehaviour { 
    [SerializeField] CharComponent _templateChar; 
    [SerializeField] Transform _courtTf;
    [SerializeField] SurfaceHandler _courtSurface;
    [SerializeField] NetTeamManeuverManager _manager;
    [SerializeField, ReadOnly] List<CharComponent> _placedChars = new List<CharComponent>();
    [ShowInInspector, ReadOnly, HideInEditorMode] TeamManeuverData _currentActive;
    
    public IReadOnlyList<CharComponent> PlacedChars => _placedChars;

    private void Awake() {
        _manager.ActiveManeuver.Sub(OnTeamManeverChange);
    }

    void OnTeamManeverChange(TeamManeuverData data)  => Activate(data); 

    public void Activate(TeamManeuverData move) {
        if (_currentActive)
            Deactivate();
        if (move) 
            _currentActive = move;
        UpdateChars(); 
    }

    public void UpdateChars() {
        if (!_currentActive)
            return;

        var pos = _currentActive.OriginPoint;
        var rot = Quaternion.Euler(0f, _currentActive.OriginYRotation, 0f);
        _courtSurface.Place(_courtTf);
        _courtTf.SetLocalPositionAndRotation(pos, rot);

        int i = 0;
        for (; i < _currentActive.CharsData.Count; i++) {
            if (_placedChars.Count <= i) {
                var spawned = Instantiate(_templateChar);
                spawned.gameObject.SetActive(true);
                _courtSurface.Place(spawned.transform);
                _placedChars.Add(spawned);
            }
            _placedChars[i].SetData(_currentActive.CharsData[i]);
        }
        while(i < _placedChars.Count) {
            if (_placedChars[i])
                _placedChars[i].gameObject.SafeDestroy();
            _placedChars.RemoveAt(i);
        }
    } 

    public void Deactivate() {
        foreach (var placedChar in _placedChars)
            if(placedChar)
                placedChar.gameObject.SafeDestroy();
        _placedChars.Clear();
        _currentActive = null; 
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
