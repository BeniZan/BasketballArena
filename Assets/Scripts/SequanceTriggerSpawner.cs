using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class SequanceTriggerSpawner : MonoBehaviour
{
    [field: SerializeField] public TriggerSpawner[] Spawners { get; private set; }
    [SerializeField] float _delayAfterSpawn; //todo disable spawn for a short time after placing to avoid accidentally placing multiple spawns in the same place
    public event Action OnSpawnerChanged, OnDonePlacing;
    public int CurrentSpawnerIdx { get; private set; }
    [ShowInInspector, ReadOnly] public bool IsPlanning => CurrentSpawnerIdx >= 0;
    private void Start() {
        for(int i = 0; i < Spawners.Length; i++) 
            DisableSpawner(i);
        _ = SetSpawner(0);
    }
    async Awaitable SetSpawner(int i) { 
        DisableSpawner(CurrentSpawnerIdx);
        await Awaitable.WaitForSecondsAsync(_delayAfterSpawn);
        CurrentSpawnerIdx = i;
        if (Spawners.ValidIndex(CurrentSpawnerIdx)) {
            Spawners[CurrentSpawnerIdx].gameObject.SetActive(true);
            Spawners[CurrentSpawnerIdx].OnSpawned += OnSpawned;
        }
        OnSpawnerChanged?.Invoke();
    } 
    void OnSpawned() { 
        _ = SetSpawner(CurrentSpawnerIdx + 1);

        if ( ! Spawners.ValidIndex(CurrentSpawnerIdx) ) {
            OnDonePlacing?.Invoke();
        }
    }
    void DisableSpawner(int i) {
        if (!Spawners.ValidIndex(i))
            return;
        Spawners[CurrentSpawnerIdx].gameObject.SetActive(false);
        Spawners[CurrentSpawnerIdx].OnSpawned -= OnSpawned;
    }
    

}
