using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    static public ValueDropdownList<AnimationClip> _animationDropdown = new();
    static public AnimationClip GetAnimation(string name) {
        foreach(var animPair in _animationDropdown) { 
            if (animPair.Value.name.Equals(name, System.StringComparison.CurrentCultureIgnoreCase))
                return animPair.Value;
        }
        return null;
    }

#if UNITY_EDITOR   

    static CharData() {
        EditorApplication.delayCall += LoadAnimations;
        EditorApplication.projectChanged += LoadAnimations;
    }
    static void LoadAnimations() {
        var animationFolder = "Assets/Data/Character/Animations";
        var animations = AssetDatabase.FindAssets("t:" + nameof(AnimationClip) 
                                , new string[] { animationFolder });
        foreach(var guidStr in animations) {
            if (!GUID.TryParse(guidStr, out var guid))
                continue;
            var clip = AssetDatabase.LoadAssetByGUID<AnimationClip>(guid);
            var clipPath = AssetDatabase.GUIDToAssetPath(guid);
            clipPath = clipPath[animationFolder.Length..]; 
            if (clip) 
                _animationDropdown.Add(clipPath, clip);
        }  
    }
#endif


    public Vector2 FieldStandardPosition;
    [PropertyRange(0f, 360f)]
    public float yRotation;
    [ValueDropdown(nameof(_animationDropdown), AppendNextDrawer = true)]
    public AnimationClip Animation; 
    public float AnimationTimeOffset;
    public bool IsFriendly;

} 
