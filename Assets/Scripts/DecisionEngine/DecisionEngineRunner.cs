using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Field.Hexagons;
using DecisionEngine.Model.OptimalPath;
using DecisionEngine.Model.Environment;
using DecisionEngine.Model.Rating;
using DecisionEngine.Model.QsqModel;

// Glue between the ARena runtime and the sport-agnostic DecisionEngine core.
// Lives on the local XR device. Per drill activation it:
// 1. builds a CalibratedCourtFrame from the calibrated court,
// 2. sizes a HexagonGrid to it (adaptive radius),
// 3. captures the scripted characters' trajectories (LiveTrajectoryCapture),
// 4. builds the qSQ table and, when the drill clock starts, solves the optimal path,
// 5. feeds the headset position to a ScoringSession at every 0.2 s of drill time,
// 6. publishes the ScoringReport to the log and the coach.
[DefaultExecutionOrder(100)] // after XRDrillActivator has placed characters
public class DecisionEngineRunner : MonoBehaviour {
    [Title("Engine")]
    [SerializeField] private float dt = 0.2f;
    [SerializeField] private int hexesAcross = HexagonGrid.DefaultHexagonsCount;
    [SerializeField] private float shotBaseline = PrecomputationEngine.DefaultShotBaseline;
    [SerializeField] private QsqParams qsqParams = new QsqParams();
    [SerializeField] private MovementModel movement = new MovementModel();

    [Title("Visualisation")]
    [SerializeField, ToggleLeft, Tooltip("Draw the hex grid on the court inside the headset.")] private bool showHexagons;
    [SerializeField, ToggleLeft, Tooltip("Tint every hex by its current shot quality.")] private bool colorByQsq;

    [Title("Debug")]
    [SerializeField, ToggleLeft, Tooltip("Compare captured trajectories against the live characters each frame.")] private bool driftCheck = true;
    [SerializeField] private float driftWarnMeters = 0.3f;

    private enum Phase { Idle, WaitingForCharacters, Ready, Running, Finished }
    [ShowInInspector, ReadOnly] private Phase _phase;
    [ShowInInspector, ReadOnly] private int _currentFrame = -1;
    [ShowInInspector, ReadOnly] private float _liveScore {
        get { return _session?.TotalScore ?? 0f; }
    }
    [ShowInInspector, ReadOnly] private float _maxPossible {
        get { return _plan?.MaxPossibleScore ?? 0f; }
    }


    private DrillData _drill;
    private CalibratedCourtFrame _frame;
    private HexagonGrid _grid;
    private AgentTrajectorySet _tracks;
    private ValueTable _qsq;
    private BlockedTable _blocked;
    private DecisionPlan _plan;
    private ScoringSession _session;
    private HexagonGridVisualizer _visualizer;
    private IShotDetector[] _detectors = Array.Empty<IShotDetector>();
    private int _settleFrames;
    private float _lastDriftWarn = -10f;
    private CustomLogger _logger;

    private bool _coachWantsHexagons;
    // Local Inspector flag OR the coach's networked toggle.
    public bool ShowHexagons {
        get { return showHexagons || _coachWantsHexagons; }
        set { showHexagons = value; RefreshVisualizer(); }
    }

    private void Awake() {
        _logger = new CustomLogger(this, Color.cyan);
        _detectors = GetComponentsInChildren<IShotDetector>(true);
        if (_detectors.Length == 0) _logger.LogWarning("No IShotDetector found under the runner; only ForceShot() will register shots.");
    }

    private void OnEnable() { foreach (var d in _detectors) d.Shot += OnShotDetected; }
    private void OnDisable() { foreach (var d in _detectors) d.Shot -= OnShotDetected; }

    private void Update() {
        var calib = Calibration.Instance;
        var activator = XRDrillActivator.Instance;
        var player = DrillPlayer.Instance;
        if (!calib || !calib.IsDoneCalibration || !activator || !player) return;

        var net = NetSpawnedXRData.Local;
        var coachWants = net != null && net.ShowHexGrid.Value;
        if (coachWants != _coachWantsHexagons) { _coachWantsHexagons = coachWants; RefreshVisualizer(); }

        var drill = activator.CurrentDrill;
        if (drill != _drill) BeginDrill(drill);
        if (_drill == null) return;

        switch (_phase) {
            case Phase.WaitingForCharacters:
                if (activator.PlacedChars.Count >= _drill.CharsData.Count && --_settleFrames <= 0)
                    Precompute(activator, player);
                break;

            case Phase.Ready:
                if (player.IsPlaying || player.AnimationTime > 0f) StartSession(player);
                break;

            case Phase.Running:
                Step(player, activator);
                break;

            case Phase.Finished:
                // Coach restarted the same drill from the top.
                if (player.AnimationTime < 0.5f * dt && player.IsPlaying && _session != null && _session.LastFrame > 1)
                    StartSession(player);
                break;
        }
    }

    // ---- lifecycle ---------------------------------------------------------------

    private void BeginDrill(DrillData drill) {
        _drill = drill;
        _session = null;
        _plan = null;
        _currentFrame = -1;
        if (drill == null) { _phase = Phase.Idle; return; }
        _settleFrames = 2; // let CharComponent.Start() randomise skins and build its graph
        _phase = Phase.WaitingForCharacters;
        _logger.Log($"Drill '{drill.name}' activated; waiting for characters");
    }

    private void Precompute(XRDrillActivator activator, DrillPlayer player) {
        var surface = Calibration.Instance.CourtHalfSurface;
        _frame = new CalibratedCourtFrame(surface);
        _grid = new HexagonGrid(_frame.Bounds, hexesAcross);

        var maxTime = 0.01f;
        foreach (var d in _drill.CharsData) if (d.Animation) maxTime = Mathf.Max(maxTime, d.Animation.length);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _tracks = LiveTrajectoryCapture.Capture(activator.PlacedChars, maxTime, dt, _frame, player.AnimationTime);
        var model = new QsqModel(qsqParams);
        _qsq = PrecomputationEngine.BuildQsqTable(_grid, _tracks, _frame.Rules, model);
        _blocked = PrecomputationEngine.BuildBlockedTable(_grid, _tracks, movement.blockedRadius);
        sw.Stop();

        _logger.Log($"Precomputed '{_drill.name}': court {_frame.Width:0.0}x{_frame.Length:0.0} m, R={_grid.Radius:0.00} m, " +
                    $"{_grid.CellCount} cells, {_tracks.frameCount} frames, {_tracks.OpponentCount} opponents, {sw.ElapsedMilliseconds} ms; " +
                    $"basket at court {_frame.Rules.Target}, player at court {PlayerCourtPosition()}");
        var problem = _frame.DescribeConventionProblem(activator.DrillOrigin, activator.PlacedChars);
        if (problem != null) _logger.LogError("Court frame check failed: " + problem);
        _phase = Phase.Ready;
        RefreshVisualizer();
    }

    private void StartSession(DrillPlayer player) {
        var frame = Mathf.Clamp(FrameIndexAt(player.AnimationTime), 0, _qsq.FrameCount - 1);
        var startCell = _grid.CellContainingPoint(PlayerCourtPosition());
        _plan = PrecomputationEngine.SolveOptimalPath(_grid, _qsq, startCell, shotBaseline, frame, movement, _blocked, dt);
        _session = new ScoringSession(_plan, _drill.name);
        _session.Finished += OnSessionFinished;
        _session.StartPossession();
        _currentFrame = frame;
        _phase = Phase.Running;
        _logger.Log($"Session started at frame {frame}, cell {startCell}; optimal shot at {_plan.ShotFrame * dt:0.0}s, max score {_plan.MaxPossibleScore:0.00}");
    }

    private void Step(DrillPlayer player, XRDrillActivator activator) {
        var frame = FrameIndexAt(player.AnimationTime);
        if (frame < _currentFrame - 1) { StartSession(player); return; } // clock was reset
        if (frame <= _currentFrame) return;

        var cell = _grid.CellContainingPoint(PlayerCourtPosition());
        _session.RecordPlayerCell(frame, cell);
        _currentFrame = frame;

        if (_visualizer != null && _session.IsActive)
            _visualizer.SetHeat(_qsq, Mathf.Min(frame, _qsq.FrameCount - 1), cell, _plan.BestCellAt(frame));
        if (driftCheck) CheckDrift(frame, activator);

        if (_session.IsActive && player.ReachedMaxAnimationTime()) _session.EndWithoutShot();
    }

    private void OnShotDetected() {
        if (_phase != Phase.Running || _session == null || !_session.IsActive) return;
        var player = DrillPlayer.Instance;
        var frame = Mathf.Clamp(FrameIndexAt(player ? player.AnimationTime : 0f), _session.LastFrame, _qsq.FrameCount - 1);
        var cell = _grid.CellContainingPoint(PlayerCourtPosition());
        _logger.Log($"Shot detected at frame {frame}, cell {cell}");
        _session.RecordShot(frame, cell);
    }

    [Button("Force Shot"), HideInEditorMode]
    private void ForceShot() {
        OnShotDetected();
    }

    private void OnSessionFinished(ScoringReport report) {
        _phase = Phase.Finished;
        _logger.Log($"REPORT {report}");
        var net = NetSpawnedXRData.Local;
        if (net != null) net.SubmitScoringReport(report);
    }

    // ---- helpers ------------------------------------------------------------------

    private int FrameIndexAt(float animationTime) {
        return Mathf.FloorToInt(animationTime / dt + 1e-4f);
    }

    private Vector2 PlayerCourtPosition() {
        var device = XRDeviceInstance.Instance;
        var head = device && device.HeadCam ? device.HeadCam.transform.position : transform.position;
        return _frame.WorldToCourt(head);
    }

    private void CheckDrift(int frame, XRDrillActivator activator) {
        if (_tracks == null || Time.time - _lastDriftWarn < 2f) return;
        var f = Mathf.Min(frame, _tracks.frameCount - 1);
        var worst = 0f; string worstName = "";
        var chars = activator.PlacedChars;
        for (var i = 0; i < chars.Count && i < _tracks.agents.Length; i++) {
            var live = _frame.WorldToCourt(LiveTrajectoryCapture.BoneUsedForPosition(chars[i]).position);
            var d = Vector2.Distance(live, _tracks.agents[i].positions[f]);
            if (d > worst) { worst = d; worstName = chars[i].name; }
        }
        if (worst > driftWarnMeters) {
            _lastDriftWarn = Time.time;
            _logger.LogWarning($"Trajectory drift {worst:0.00} m on '{worstName}' at frame {f}: captured positions disagree with live animation.");
        }
    }

    private void RefreshVisualizer() {
        if (ShowHexagons && _grid != null && _frame != null) {
            if (_visualizer == null) _visualizer = new GameObject("Hex Grid Overlay").AddComponent<HexagonGridVisualizer>();
            _visualizer.ColorByQsq = colorByQsq;
            _visualizer.Show(_grid, _frame);
        } else if (_visualizer != null) {
            _visualizer.Hide();
        }
    }

    private void OnValidate() { if (Application.isPlaying) RefreshVisualizer(); }

    private void OnDestroy() { if (_visualizer != null) Destroy(_visualizer.gameObject); }

    // ---- read-only access for gizmos / UI -------------------------------------------
    public HexagonGrid Grid {
        get { return _grid; }
    }
    public CalibratedCourtFrame Frame {
        get { return _frame; }
    }
    public DecisionPlan Plan {
        get { return _plan; }
    }
    public ScoringSession Session {
        get { return _session; }
    }
    public AgentTrajectorySet Tracks {
        get { return _tracks; }
    }
    public int CurrentFrame {
        get { return _currentFrame; }
    }
}
