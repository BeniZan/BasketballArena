using UnityEngine;

public class  CourtBoundsSetter : MonoBehaviour{
    [SerializeField] Transform _worldAnchor;
    [SerializeField] SequanceTriggerSpawner _seqTriggerSpawner; 
    [SerializeField] LineRenderer _lineRendererPrefab;
    public Rect LocalCourtBounds { get; private set; }
    void Awake() {
        _seqTriggerSpawner.OnDonePlacing += SeqTriggerSpawner_OnDonePlacing;
    }
    void OnDestroy() {
        _seqTriggerSpawner.OnDonePlacing -= SeqTriggerSpawner_OnDonePlacing;
    }
    void SeqTriggerSpawner_OnDonePlacing() {
        // in local space of world origin
        var centerPos = _seqTriggerSpawner.Spawners[0].Spawned.localPosition; 
        var cornerPos = _seqTriggerSpawner.Spawners[1].Spawned.localPosition;
        cornerPos.y = centerPos.y; 
        var halfWidth = cornerPos.z - centerPos.z;
        var halfLength = cornerPos.x - centerPos.x;
        var size = new Vector2(halfWidth * 2f, halfLength * 2f);
        LocalCourtBounds = new Rect( centerPos , size);

        CreateCourtLines();
    }

    void CreateCourtLines() {
        var outerLine = Instantiate(_lineRendererPrefab, _worldAnchor);
        var topX = LocalCourtBounds.xMax; var topY = LocalCourtBounds.yMax;
        var minX = LocalCourtBounds.xMin; var minY = LocalCourtBounds.yMin;
        outerLine.SetPositions(new Vector3[] {
            new(minX, 0f, minY),
            new(topX, 0f, minX),
            new(topX, 0f, topY),
            new(minX, 0f, topY),
            new(minX, 0f, minY),
            new(minX, 0f, minY)
        });
    }

}
