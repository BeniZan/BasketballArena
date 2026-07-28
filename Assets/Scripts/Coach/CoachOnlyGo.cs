using UnityEngine;

public class CoachOnlyGo : MonoBehaviour
{
    void Awake() {
        NetBoot.Instance.OnPlayerTypeChange += Instance_OnPlayerTypeSetup; 
    }

    private void Instance_OnPlayerTypeSetup(NetBoot obj) {
        gameObject.SetActive(obj.IsCoach);
    } 
}
