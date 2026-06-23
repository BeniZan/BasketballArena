using Sirenix.OdinInspector;
using System;
using System.Security.Cryptography.X509Certificates;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
 
[SelectionBase]
public class CharComponent : MonoBehaviour {
    [SerializeField] Animator _anim;
    [ShowInInspector, NonSerialized] CharData _data;
    public CharData Data => _data;
    public Transform Head;
    [ShowInInspector]
    public AnimationClip Clip => _clipPlayable.IsValid() ? _clipPlayable.GetAnimationClip() : null;
    PlayableGraph _graph;
    AnimationClipPlayable _clipPlayable;

    public void SetData(CharData data) {
        _data = data;
        if(data != null) { 
            var pos = new Vector3(data.FieldStandardPosition.y,0,data.FieldStandardPosition.x);
            var rot = Quaternion.Euler(0, data.yRotation, 0);
            transform.SetLocalPositionAndRotation(pos, rot);
            SetAnim(data.Animation);
        }
    } 


    private void OnEnable() {
        _anim.fireEvents = false;
        _graph = PlayableGraph.Create("SingleAnimationGraph");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        SetAnim(null); 
        _graph.Play();
    }

    private void OnDisable() {
        if(_graph.IsValid())
            _graph.Destroy();
    } 

    public void SetAnimationTime(float time) {
        if (_clipPlayable.IsValid())
            _clipPlayable.SetTime(time + (_data?.AnimationTimeOffset ?? 0f) );
        _graph.Evaluate();
    }

    private void Update() {
        SetData(_data); 
    }  

    void SetAnim(AnimationClip clip) {
        if (clip == Clip)
            return;
        var playableOutput = AnimationPlayableOutput.Create(_graph, "AnimationOutput", _anim);
        _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
        playableOutput.SetSourcePlayable(_clipPlayable);
    }  

}