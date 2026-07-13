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
            var manager = NetDrillsActivator.Instance;
            var options = new List<TMP_Dropdown.OptionData>
                (manager.AllTeamManeuvers
                .Select(m => new TMP_Dropdown.OptionData(m.name)));
            _dropdown.options = options;
            _dropdown.onValueChanged.AddListener(OnDropdown);
            SetDropdownValue();
            manager.ActiveManeuver.Sub(OnActiveManeuver);
        }
        else gameObject.SafeDestroy();
    }

    private void OnEnable() {
        SetDropdownValue();
    }

    void OnDropdown(int i) {
        var manager = NetDrillsActivator.Instance;
        manager.Server_SetActiveDrill(i);
    }

    void OnActiveManeuver(DrillData _) => SetDropdownValue();

    void SetDropdownValue() {
        _dropdown.SetValueWithoutNotify(NetDrillsActivator.Instance.ActiveManeuverIdx);
    }

    void OnCalibrationState(Calibration.Step step) {
        gameObject.SetActive(step == Calibration.Step.Calibrated);
    }

}
