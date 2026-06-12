using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting; 

#if UNITY_EDITOR
[InitializeOnLoad]
#endif
[ExecuteInEditMode]
[System.Serializable]
public class CharData {
    static readonly Vector2 FieldStandardSize = new Vector2(28f, 15f);
    static ValueDropdownList<AnimationClip> _animationDropdown = new();
#if UNITY_EDITOR   
    static CharData() {
        EditorApplication.delayCall += LoadAnimations;
        EditorApplication.projectChanged += LoadAnimations;
    }
    static void LoadAnimations() {
        var animations = AssetDatabase.FindAssets("t:" + nameof(AnimationClip));
        foreach(var guidStr in animations) {
            if (!GUID.TryParse(guidStr, out var guid))
                continue;
            var clip = AssetDatabase.LoadAssetByGUID<AnimationClip>(guid);
            if(clip)
                _animationDropdown.Add(clip.name, clip);
        }  
    }  
#endif 

    public Vector2 FieldStandardPosition;
    public float yRotation;
    [ValueDropdown(nameof(_animationDropdown), AppendNextDrawer = true)]
    public AnimationClip Animation;
    public bool IsFriendly;

} 
