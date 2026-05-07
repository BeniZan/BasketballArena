using NUnit.Framework;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class ScenarioPlayer : MonoBehaviour {
    [field: SerializeField, Get] public NetworkObject NetObj { get; private set; }
    public void OnScenarioInit() {

    }

}

public class CoachScenarioManager : NetworkBehaviour
{
    [SerializeField] NetworkFieldData _field;
    List<ScenarioPlayer> SpawnedScenarioObjects = new(); 
    public void SpawnScenario(ScenarioData scenario) { 
        var prefab = scenario.ScenarioPrefab; 
        var worldPos = _field.ConvertFieldPositionToWorld(scenario.FieldPositionXZ);
        var worldRot = Quaternion.Euler(0, scenario.YRotation, 0);
        worldRot = _field.ConvertFieldRotationToWorld(worldRot);
        var spawned = 
            NetworkManager.SpawnManager.InstantiateAndSpawn(scenario.ScenarioPrefab.NetObj, NetworkManager.ServerClientId,
            true, false, position: worldPos, rotation: worldRot);

        if(spawned.TryGetComponent(out ScenarioPlayer scenarioPlayer)) {
            SpawnedScenarioObjects.Add(scenarioPlayer);
        }
        else {
            Debug.LogError("Spawned scenario but failed to find ScenarioPlayer", spawned);
        }

    }

}
