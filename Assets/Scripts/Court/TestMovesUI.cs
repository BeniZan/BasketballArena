using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TestMovesUI : MonoBehaviour {
    [SerializeField] TMPro.TMP_Dropdown _dropdown;
    [SerializeField] Calibration _calib;
    void Start() {
        if (Debug.isDebugBuild) {
            _calib.CalibrationStep.Sub(OnCalibrationState);
            var optionLst = new Dropdown.OptionData();
            _dropdown.options = new List<TMP_Dropdown.OptionData>
                (NetTeamManeuverManager.Instance.AllTeamManeuvers
                .Select(m => new TMP_Dropdown.OptionData(m.name)));
            _dropdown.onValueChanged.AddListener(OnDropdown);
        }
        else gameObject.SafeDestroy();
    }

    void OnDropdown(int i) {
        NetTeamManeuverManager.Instance.Server_SetTeamManeuver(i);
    }
     
    void OnCalibrationState(Calibration.Step step) {
        gameObject.SetActive(step == Calibration.Step.Calibrated);
    }

}
