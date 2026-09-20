using UnityEngine;
using Field.Hexagons;
using DecisionEngine.Model.OptimalPath;

// Renders the hex grid on the physical court inside the headset. Created on demand by
// DecisionEngineRunner when its ShowHexagons flag is on; costs nothing
// otherwise. Two meshes: a line outline and an optional per-cell fill tinted by qSQ.
public class HexagonGridVisualizer : MonoBehaviour {
    private const string MaterialResource = "HexagonGrid";
    private const float FloorOffset = 0.02f;

    [SerializeField] private Color lineColor = new Color(0f, 0.83f, 1f, 0.9f);
    [SerializeField] private Color lowQsqColor = new Color(1f, 0.25f, 0.1f, 0.35f);
    [SerializeField] private Color highQsqColor = new Color(0.1f, 1f, 0.3f, 0.35f);
    [SerializeField] private Color currentCellColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private Color bestCellColor = new Color(0.2f, 1f, 0.2f, 0.8f);

    private HexagonGrid _grid;
    private CalibratedCourtFrame _frame;
    private MeshFilter _lines, _fill;
    private Mesh _linesMesh, _fillMesh;
    private Color[] _fillColors;
    private Material _material;

    public bool ColorByQsq { get; set; }

    public void Show(HexagonGrid grid, CalibratedCourtFrame frame) {
        _grid = grid;
        _frame = frame;
        EnsureObjects();
        BuildMeshes();
        gameObject.SetActive(true);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    // Tints every cell by its qSQ at frame; call at most once per engine frame.
    public void SetHeat(ValueTable table, int frame, HexagonCell? current, HexagonCell? best) {
        if (_fill == null || _grid == null) return;
        _fill.gameObject.SetActive(ColorByQsq);
        if (!ColorByQsq) return;

        var min = float.MaxValue;
        var max = float.MinValue;
        for (var c = 0; c < _grid.CellCount; c++) { float v = table[frame, c]; if (v < min) min = v; if (v > max) max = v; }
        var span = Mathf.Max(1e-4f, max - min);

        for (var c = 0; c < _grid.CellCount; c++) {
            var cell = _grid.CellAtIndex(c);
            var col = Color.Lerp(lowQsqColor, highQsqColor, (table[frame, c] - min) / span);
            if (best.HasValue && cell == best.Value) col = bestCellColor;
            if (current.HasValue && cell == current.Value) col = currentCellColor;
            for (var i = 0; i < 7; i++) _fillColors[c * 7 + i] = col;
        }
        _fillMesh.colors = _fillColors;
    }

    private void EnsureObjects() {
        if (_material == null) {
            _material = Resources.Load<Material>(MaterialResource);
            if (_material == null) {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                _material = new Material(shader);
            }
        }
        if (_lines == null) _lines = CreateChild("Hex Outline", out _linesMesh);
        if (_fill == null) { _fill = CreateChild("Hex Fill", out _fillMesh); _fill.gameObject.SetActive(false); }
        transform.SetParent(_frame.Root, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private MeshFilter CreateChild(string name, out Mesh mesh) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;
        return mf;
    }

    private void BuildMeshes() {
        var n = _grid.CellCount;
        var lineVerts = new Vector3[n * 6];
        var lineIdx = new int[n * 12];
        var lineCols = new Color[n * 6];
        var fillVerts = new Vector3[n * 7];
        var fillIdx = new int[n * 18];
        _fillColors = new Color[n * 7];

        var corners = new Vector2[6];
        for (var c = 0; c < n; c++) {
            var cell = _grid.CellAtIndex(c);
            var center = _grid.CellCenter(cell);
            for (var i = 0; i < 6; i++) corners[i] = _grid.CellCorner(cell, i, 0.97f);
            for (var i = 0; i < 6; i++) {
                lineVerts[c * 6 + i] = Local(corners[i]);
                lineCols[c * 6 + i] = lineColor;
                lineIdx[c * 12 + i * 2] = c * 6 + i;
                lineIdx[c * 12 + i * 2 + 1] = c * 6 + (i + 1) % 6;
            }
            fillVerts[c * 7] = Local(center);
            for (var i = 0; i < 6; i++) fillVerts[c * 7 + 1 + i] = Local(corners[i]);
            for (var i = 0; i < 6; i++) {
                fillIdx[c * 18 + i * 3] = c * 7;
                fillIdx[c * 18 + i * 3 + 1] = c * 7 + 1 + i;
                fillIdx[c * 18 + i * 3 + 2] = c * 7 + 1 + (i + 1) % 6;
            }
            for (var i = 0; i < 7; i++) _fillColors[c * 7 + i] = lowQsqColor;
        }

        _linesMesh.Clear();
        _linesMesh.vertices = lineVerts;
        _linesMesh.colors = lineCols;
        _linesMesh.SetIndices(lineIdx, MeshTopology.Lines, 0);
        _linesMesh.RecalculateBounds();

        _fillMesh.Clear();
        _fillMesh.vertices = fillVerts;
        _fillMesh.colors = _fillColors;
        _fillMesh.SetIndices(fillIdx, MeshTopology.Triangles, 0);
        _fillMesh.RecalculateBounds();
    }

    // Court meters → this object's local space (parent is the court root, so go via world).
    private Vector3 Local(Vector2 court) {
        return transform.InverseTransformPoint(_frame.CourtToWorld(court, FloorOffset));
    }

    private void OnDestroy() {
        if (_linesMesh) Destroy(_linesMesh);
        if (_fillMesh) Destroy(_fillMesh);
    }
}
