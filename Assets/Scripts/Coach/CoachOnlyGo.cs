using UnityEngine;

public class CoachOnlyGo : MonoBehaviour
{
    void Awake() {
        NetBoot.Instance.PlayType.Sub(OnPlayerType);
    }
    void OnPlayerType(NetBoot.PlayerType playerType) {
         gameObject.SetActive(playerType == NetBoot.PlayerType.Coach);
    }
}
