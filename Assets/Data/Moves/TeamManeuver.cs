using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class TeamManeuver : ScriptableObject {
    [ShowInInspector] public TeamManeuver Reference => this;
    [PropertyOrder(100), ListDrawerSettings(OnBeginListElementGUI = nameof(OnBeginItemGUI), ShowFoldout = false)] 
    public List<CharData> CharsData = new List<CharData>();


    void OnBeginItemGUI(int idx){
#if UNITY_EDITOR
        EditorGUILayout.LabelField( "Player: " + idx.ToString());
#endif
    }

}