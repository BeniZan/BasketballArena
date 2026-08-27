using UnityEngine;

public class ValidateDrillXRPosition : MonoBehaviour
{    
    void Update()
    {
        if (Calibration.Instance.IsDoneCalibration && 
            NetSpawnedXRData.Local &&
            XRDrillActivator.Instance && XRDrillActivator.Instance.DrillOrigin) {

            var XRdrill = XRDrillActivator.Instance;
            var drill = XRdrill.CurrentDrill;
            var startPos = XRdrill.DrillOrigin.TransformPoint(drill.PlayerStartPosition);
            var startPosXZ = startPos.XZ();
            var posXZ = transform.position.XZ();
            var distance = startPosXZ.Distance(posXZ);
            NetSpawnedXRData.Local.IsInStartingPosition.Value = distance < 1f;
        }    
    }
}
