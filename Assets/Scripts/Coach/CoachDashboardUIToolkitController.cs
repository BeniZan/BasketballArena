using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using DecisionEngine.Model.Rating;

public class CoachDashboardUIToolkitController : MonoBehaviour
{
    [Header("Fonts")]
    [SerializeField] private Font barlow600;
    [SerializeField] private Font barlow700;
    [SerializeField] private Font barlow900;
    [SerializeField] private Font inter400;
    [SerializeField] private Font inter700;

    [Header("Sprites")]
    [SerializeField] private Sprite logoSprite;
    [SerializeField] private Sprite streamEyeSprite;
    [SerializeField] private Sprite iconRealistic;
    [SerializeField] private Sprite iconHologram;
    [SerializeField] private Sprite iconStart;
    [SerializeField] private Sprite iconNext;
    [SerializeField] private Sprite iconPause;
    [SerializeField] private Sprite iconStop;
    [SerializeField] private Sprite iconForce;
    [SerializeField] private Sprite iconPickAndRoll;
    [SerializeField] private Sprite iconShooting;
    [SerializeField] private Sprite iconPostPlays;
    [SerializeField] private Sprite iconMargin;
    [SerializeField] private Sprite iconRandomDrill;
    [SerializeField] private Sprite iconAnalytics;

    [Header("Exercise Foldouts")]
    [SerializeField] private string[] pickAndRollDrills = { "High Screen", "Side P&R", "Horns", "Spain P&R", "Step-Up", "Drag Screen" };
    [SerializeField] private string[] shootingDrills = { "Catch & Shoot", "Off The Dribble", "Spot-Up Corner", "Pull-Up Mid", "Transition 3", "Free Throws" };
    [SerializeField] private string[] postPlaysDrills = { "Drop Step", "Up & Under", "Seal & Feed", "Face Up" };

    private readonly Dictionary<string, System.Action> _foldoutToggleHandlers = new Dictionary<string, System.Action>();

    private UIDocument _uiDocument;
    private VisualElement _root;

    // UI elements
    private Label _timerText;
    private Label _repText;
    private Label _statusText;
    private Button _startBtn;
    private Button _pauseBtn;
    private Button _stopBtn;
    private Button _nextBtn;
    private Button _forceBtn;
    private Button _realisticBtn;
    private Button _hologramBtn;

    private Button _randomDrillBtn;

    // Header XR headset indicator (green pill next to the stream-mode toggles)
    private VisualElement _xrStatus;
    private Label _xrStatusText; 

    private Label _drillsCountText;
    private VisualElement _buildSessionPlaceholder;
    private ListView _drillListView;

    private VisualElement _castingView;

    // Decision engine (HoopEval) report overlay + hex-grid toggle for the player's headset
    private DecisionCardView _decisionCard;
    private Button _hexGridBtn;
    private bool _hexGridOn;
    private VisualElement _castingPlaceholder;
    private UnityEngine.UIElements.Image _castingImage;
    private VisualElement _liveTag;
    private VisualElement _streamPlaceholder;

    // WebRTCVideoReceiver exposes no public API (no singleton, event, or property), so the
    // casting view polls its private "_recievedVideo" field via cached reflection each frame.
    [SerializeField] WebRTCVideoReceiver videoReceiver;

    // State variables
    private readonly TrainingSession _session = new TrainingSession();

    // True from START until STOP. PAUSE is a sub-state that keeps the session active
    // (so the coach can resume / advance / stop). Drives control interactability and the LIVE tag.
    private bool _isSessionActive = false;

    private void Awake()
    {
        //_uiDocument = GetComponent<UIDocument>();
        //if (_uiDocument == null && Application.isPlaying)
        //{
        //    Debug.LogError("UIDocument component is required on the same GameObject!");
        //    return;
        //}
    }

    private void OnEnable()
    {
        // Early attempt. The UIDocument may not have built its rootVisualElement yet
        // (its OnEnable order is not guaranteed relative to ours), so stay silent if
        // it isn't ready — Start() runs later and will retry.
        InitializeUI(false);
    }

    private void Start()
    {
        // Last-resort attempt: by now the UIDocument has definitely built its tree, so
        // if the root is still null it's a genuine misconfiguration worth reporting.
        InitializeUI(true);
    }
     
    private void OnDisable()
    {
        NetSpawnedXRData.ScoringReportReceived -= OnScoringReportReceived;
        _session.OnDrillsChanged -= HandleDrillsChanged;
        _session.OnActiveDrillChanged -= HandleActiveDrillChanged;
        if (_drillListView != null) _drillListView.itemIndexChanged -= OnDrillReordered;
    }

    private void InitializeUI(bool logIfNotReady = false)
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
        if (_root == null)
        {
            // Only treat a null root as an error from the last-resort Start() path in play
            // mode; early/edit-mode attempts are expected to no-op until the tree is built.
            if (Application.isPlaying && logIfNotReady)
            {
                Debug.LogError("CoachDashboardUIToolkitController: rootVisualElement is null. Is a PanelSettings and Source Asset assigned to the UIDocument?");
            }
            return;
        }

        // Query elements
        _timerText = _root.Q<Label>("timerText");
        _repText = _root.Q<Label>("repText");
        _statusText = _root.Q<Label>("statusText");

        _startBtn = _root.Q<Button>("startBtn");
        _pauseBtn = _root.Q<Button>("pauseBtn");
        _stopBtn = _root.Q<Button>("stopBtn");
        _nextBtn = _root.Q<Button>("nextBtn");
        _forceBtn = _root.Q<Button>("forceBtn");

        _realisticBtn = _root.Q<Button>("realisticBtn");
        _hologramBtn = _root.Q<Button>("hologramBtn");

        _randomDrillBtn = _root.Q<Button>("randomDrillBtn");

        _xrStatus = _root.Q<VisualElement>("xrStatus");
        _xrStatusText = _root.Q<Label>("xrStatusText"); 

        _drillsCountText = _root.Q<Label>("drillsCountText");
        _buildSessionPlaceholder = _root.Q<VisualElement>("buildSessionPlaceholder");

        _liveTag = _root.Q<VisualElement>("liveTag");
        _streamPlaceholder = _root.Q<VisualElement>(className: "viewport-placeholder");
        SetupCastingView();
        SetupDecisionReport();
        NetBoot.Instance.NetMnger.OnConnectionEvent += NetMnger_OnConnectionEvent;

        // Robustly acquire the Training Flow ListView. The runtime UI Toolkit importer in
        // this project does not reliably instantiate a <ui:ListView> from UXML (it can fall
        // back to a ScrollView), which would leave _drillListView null and nothing rendered.
        // So if the named element is missing or is not a real ListView, we create the
        // ListView in C# (guaranteed correct type) and insert it where the UXML element was.
        _drillListView = _root.Q<ListView>("drillListContainer");
        if (_drillListView == null)
        {
            var existing = _root.Q<VisualElement>("drillListContainer");
            VisualElement parent = existing != null ? existing.parent : _root.Q<VisualElement>(className: "training-flow-content");
            int insertIndex = (existing != null && parent != null) ? parent.IndexOf(existing) : -1;
            if (existing != null) existing.RemoveFromHierarchy();

            _drillListView = new ListView { name = "drillListContainer" };
            _drillListView.AddToClassList("drill-list-view");
            _drillListView.style.display = DisplayStyle.None;

            if (parent != null)
            {
                if (insertIndex >= 0 && insertIndex <= parent.childCount) parent.Insert(insertIndex, _drillListView);
                else parent.Add(_drillListView);
            }
        }

        // Apply visual Sprites and Fonts
        ApplySprites();
        ApplyTypography();

        pickAndRollDrills = NetDrillsActivator.Instance.AllTeamManeuvers.Select(t => t.name).ToArray();
        shootingDrills = new string[0];
        postPlaysDrills = new string[0];

        // Build collapsible exercise category foldouts (works in EditMode preview and PlayMode)
        SetupExerciseFoldout("pickAndRoll", pickAndRollDrills);
        SetupExerciseFoldout("shooting", shootingDrills);
        SetupExerciseFoldout("postPlays", postPlaysDrills);

        // Bind the data-driven Training Flow ListView and observe the session model.
        SetupDrillListView();

        // Unsubscribe-first guard so EditMode <-> PlayMode reloads don't double-subscribe.
        _session.OnDrillsChanged -= HandleDrillsChanged;
        _session.OnDrillsChanged += HandleDrillsChanged;
        _session.OnActiveDrillChanged -= HandleActiveDrillChanged;
        _session.OnActiveDrillChanged += HandleActiveDrillChanged;

        // Initial setup/visual values that can be shown in editor too
        UpdateTimerDisplay();
        UpdateRepDisplay();
        UpdateDrillsDisplay();

        // Session starts inactive: hide the LIVE tag and evaluate initial button interactability
        // (START disabled until a drill exists; NEXT/PAUSE/STOP/FORCE disabled until a session is active).
        if (_liveTag != null) _liveTag.style.display = DisplayStyle.None;
        UpdateControlInteractability();

        // Wire event handlers and run play-mode specific visual initializations
        if (Application.isPlaying)
        {
            // Unsubscribe first to avoid double registration in EditMode -> PlayMode transitions
            if (_startBtn != null) _startBtn.clicked -= StartTimer;
            if (_pauseBtn != null) _pauseBtn.clicked -= PauseTimer;
            if (_stopBtn != null) _stopBtn.clicked -= StopTimer;
            if (_nextBtn != null) _nextBtn.clicked -= NextRep;
            if (_forceBtn != null) _forceBtn.clicked -= ForceSession;

            if (_startBtn != null) _startBtn.clicked += StartTimer;
            if (_pauseBtn != null) _pauseBtn.clicked += PauseTimer;
            if (_stopBtn != null) _stopBtn.clicked += StopTimer;
            if (_nextBtn != null) _nextBtn.clicked += NextRep;
            if (_forceBtn != null) _forceBtn.clicked += ForceSession;

            if (_realisticBtn != null) _realisticBtn.clicked += () => SetStreamMode(true);
            if (_hologramBtn != null) _hologramBtn.clicked += () => SetStreamMode(false);

            if (_randomDrillBtn != null) _randomDrillBtn.clicked += AddRandomDrill;

            SetStreamMode(true);
        }
        else
        {
            // In editor mode, let's also visually apply some sensible defaults to the layout (like active tabs) so it looks right in scene view
            SetStreamMode(true);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(_uiDocument.visualTreeAsset);
#endif

    }

    private void NetMnger_OnConnectionEvent(NetworkManager arg1, ConnectionEventData arg2) {
        UpdateConnectedStatus();
    }

    private void Update()
    {
        UpdateTimerDisplay();  
    }  

    // ---- XR headset indicator --------------------------------------------------

    private void UpdateConnectedStatus()
    {
        if (_xrStatus == null) return;

        bool connected = HasConnectedClients(); 
         
        _xrStatus.EnableInClassList("connected", connected);
        if (_xrStatusText != null) _xrStatusText.text = connected ? "HEADSET ACTIVE" : "NO HEADSET";
    }

    /// <summary>
    /// True while a player client is connected to the coach host.
    /// </summary>
    private static bool HasConnectedClients()
    {
        if (!Application.isPlaying) return false;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return false;
        if (!nm.IsServer) return nm.IsConnectedClient;

        return nm.ConnectedClients.Count > 0;
    }

    // ---- Decision engine report ---------------------------------------------------

    private void SetupDecisionReport()
    {
        var host = _root.Q<VisualElement>("decisionReport");
        if (host == null) return;
        if (_decisionCard == null)
        {
            _decisionCard = new DecisionCardView(barlow900, barlow700, inter400);
            host.Add(_decisionCard);
            _hexGridBtn = _decisionCard.AddToggle("HEX GRID", ToggleHexGrid);
        }

        NetSpawnedXRData.ScoringReportReceived -= OnScoringReportReceived;
        NetSpawnedXRData.ScoringReportReceived += OnScoringReportReceived;
        RefreshDecisionToggles();
    }

    private void OnScoringReportReceived(NetSpawnedXRData player, DecisionEngine.Model.Rating.ScoringReport report)
    {
        _decisionCard?.SetReport(report);
    }

    private static void SetHexGridOnAllPlayers(bool on)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        foreach (var client in nm.ConnectedClientsList)
        {
            var data = client.PlayerObject != null ? client.PlayerObject.GetComponent<NetSpawnedXRData>() : null;
            if (data != null) data.ShowHexGrid.Value = on;
        }
    }

    // Flip the hex-grid overlay on every connected headset (server-authoritative).
    private void ToggleHexGrid()
    {
        _hexGridOn = !_hexGridOn;
        SetHexGridOnAllPlayers(_hexGridOn);
        RefreshDecisionToggles();
    }

    private void RefreshDecisionToggles()
    {
        DecisionCardView.SetToggle(_hexGridBtn, _hexGridOn, "HEX GRID", "HEX GRID ON");
    }

    // ---- Casting view (player stream) -------------------------------------------

    private void SetupCastingView()
    {
        _castingView = _root.Q<VisualElement>("castingView");
        if (_castingView == null) return;

        _castingPlaceholder = _castingView.Q<VisualElement>(className: "viewport-placeholder");

        if (_castingImage == null)
        {
            _castingImage = new UnityEngine.UIElements.Image { name = "castingStreamImage" };
            _castingImage.scaleMode = ScaleMode.ScaleAndCrop;
            _castingImage.style.position = Position.Absolute;
            _castingImage.style.left = 0;
            _castingImage.style.right = 0;
            _castingImage.style.top = 0;
            _castingImage.style.bottom = 0;
        }
        if (_castingImage.parent != _castingView)
            _castingView.Insert(0, _castingImage);

        // Locate the receiver now (Update keeps retrying if it appears later) and reflect the
        // current stream state (covers re-initialization while already streaming).
        if (Application.isPlaying && videoReceiver == null)
            videoReceiver = FindObjectOfType<WebRTCVideoReceiver>(); 

        videoReceiver.OnVideoTextureChanged -= VideoReceiver_OnVideoTextureChanged; 
        videoReceiver.OnVideoTextureChanged += VideoReceiver_OnVideoTextureChanged;
    }

    private void VideoReceiver_OnVideoTextureChanged(Texture texture) {
        _castingImage.image = texture;
        _castingImage.style.display = texture ? DisplayStyle.Flex : DisplayStyle.None;
    } 

    private void SetStreamMode(bool realistic)
    {
        if (realistic)
        {
            _realisticBtn.AddToClassList("active");
            _hologramBtn.RemoveFromClassList("active");
        }
        else
        {
            _realisticBtn.RemoveFromClassList("active");
            _hologramBtn.AddToClassList("active");
        }

        NetCoachDashboardState.Instance?.Server_SetStreamMode(realistic);
    }

    /// <summary>
    /// Central rule for button interactability:
    /// - START is usable only when NO session is active AND the Training Flow has at least one drill.
    /// - NEXT / PAUSE / STOP / FORCE are usable only WHILE a session is active.
    /// </summary>
    private void UpdateControlInteractability()
    {
        bool hasDrills = _session.Drills.Count > 0;
        if (_startBtn != null) _startBtn.SetEnabled(!_isSessionActive && hasDrills);
        if (_nextBtn != null) _nextBtn.SetEnabled(_isSessionActive);
        if (_pauseBtn != null) _pauseBtn.SetEnabled(_isSessionActive);
        if (_stopBtn != null) _stopBtn.SetEnabled(_isSessionActive);
        if (_forceBtn != null) _forceBtn.SetEnabled(_isSessionActive);
    }

    private void StartTimer()
    {
        // Guard: START must only work when a drill exists (mirrors the disabled-button rule).
        if (_session.Drills.Count == 0) return;

        _statusText.text = "RUNNING";
        _statusText.style.color = new StyleColor(new Color(16f/255f, 185f/255f, 129f/255f, 1f)); // Green

        // Enter the active session state: trigger the live stream visual and show the LIVE SESSION tag.
        _isSessionActive = true;
        if (_liveTag != null) _liveTag.style.display = DisplayStyle.Flex;
        if (_streamPlaceholder != null) _streamPlaceholder.style.display = DisplayStyle.None;

        // Activate the first drill if the session hasn't started yet.
        // HandleActiveDrillChanged propagates the drill to NetDrillsActivator by name.
        if (_session.ActiveIndex < 0)
            _session.Start();

        DrillPlayer.Instance.IsPlaying = true;

        UpdateControlInteractability();
    }

    private void PauseTimer()
    {
        _statusText.text = "PAUSED";
        _statusText.style.color = new StyleColor(new Color(255f/255f, 107f/255f, 0f, 1f)); // Orange

        DrillPlayer.Instance.IsPlaying = false;
        //NetCoachDashboardState.Instance?.Server_SetTimerRunning(false);
    }

    private void StopTimer()
    { 
        _statusText.text = "READY";
        _statusText.style.color = new StyleColor(new Color(100f/255f, 116f/255f, 139f/255f, 1f)); // Gray
        UpdateTimerDisplay();
        _session.Reset(); // clears active drill -> HandleActiveDrillChanged updates repText + highlight

        DrillPlayer.Instance.ResetTimeAndPlay(); // resets the animation/timer to 0 and pauses
        //NetCoachDashboardState.Instance?.Server_SetTimerRunning(false); 

        // Exit the active session state: hide the LIVE SESSION tag, restore the placeholder,
        // and re-lock NEXT/PAUSE/STOP/FORCE (START re-enables if drills remain).
        _isSessionActive = false;
        if (_liveTag != null) _liveTag.style.display = DisplayStyle.None;
        if (_streamPlaceholder != null) _streamPlaceholder.style.display = DisplayStyle.Flex;
        UpdateControlInteractability();
    }

    private void NextRep()
    {
        if (DrillPlayer.Instance.IsPlaying)
        {
            _session.Next(); // advances active drill -> HandleActiveDrillChanged updates repText + highlight
        }
    }

    private void ForceSession()
    {
        _session.ForceToEnd();
    }

    private void UpdateTimerDisplay()
    {
        var elapsedTime = DrillPlayer.Instance.AnimationTime;
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateRepDisplay()
    {
        if (_repText == null) return;
        var active = _session.ActiveDrill;
        _repText.text = active == null
            ? "REP 0/0"
            : string.Format("{0} · REP {1}/{2}", active, _session.ActiveIndex + 1, _session.Drills.Count);
    }

    private void SetupExerciseFoldout(string baseName, string[] drills)
    {
        var foldout = _root.Q<VisualElement>(baseName + "Foldout");
        var header = _root.Q<Button>(baseName + "Header");
        var content = _root.Q<VisualElement>(baseName + "Content");
        var badge = _root.Q<Label>(baseName + "Badge");
        if (foldout == null || header == null || content == null) return;

        drills = drills ?? new string[0];

        // Auto-derive the count badge from the number of sub-drills
        if (badge != null) badge.text = drills.Length.ToString();

        // Chevron caret + rotation are drawn entirely via USS (no sprite needed).

        // Rebuild sub-drill rows(Clear prevents duplicates across OnEnable/OnValidate)
        content.Clear();
        foreach (var drillName in drills)
        {
            var row = new Button { name = baseName + "_" + drillName };
            row.AddToClassList("exercise-sub-item");

            var icon = new UnityEngine.UIElements.Image();
            icon.AddToClassList("exercise-sub-icon");

            var lbl = new Label(drillName);
            lbl.AddToClassList("exercise-sub-text");
            if (barlow700 != null) lbl.style.unityFontDefinition = new StyleFontDefinition(barlow700);

            row.Add(icon);
            row.Add(lbl);

            if (Application.isPlaying)
            {
                string captured = drillName;
                row.clicked += () => AddDrill(captured);
            }

            content.Add(row);
        }

        // Header toggles expand/collapse. Unsubscribe any previous handler first to
        // avoid double-toggling when InitializeUI runs multiple times.
        if (_foldoutToggleHandlers.TryGetValue(baseName, out var prev))
        {
            header.clicked -= prev;
        }
        System.Action toggle = () => foldout.ToggleInClassList("expanded");
        header.clicked += toggle;
        _foldoutToggleHandlers[baseName] = toggle;
    }

    // ---- Training Flow ListView ------------------------------------------------

    private void SetupDrillListView()
    {
        if (_drillListView == null) return;

        _drillListView.itemsSource = _session.Drills;
        _drillListView.fixedItemHeight = 36;
        // Selection disabled: removes the built-in cyan click-highlight (and the "stuck" selected
        // state). Drag-reorder still works. The orange .drill-item-active highlight is driven
        // separately by the session's ActiveIndex, not by ListView selection.
        _drillListView.selectionType = SelectionType.None;
        _drillListView.reorderable = true;
        _drillListView.reorderMode = ListViewReorderMode.Animated;
        _drillListView.makeItem = MakeDrillItem;
        _drillListView.bindItem = BindDrillItem;

        // Unsubscribe-first guard against EditMode <-> PlayMode reloads.
        _drillListView.itemIndexChanged -= OnDrillReordered;
        _drillListView.itemIndexChanged += OnDrillReordered;

        _drillListView.RefreshItems();
    }

    private VisualElement MakeDrillItem()
    {
        var item = new VisualElement();
        item.AddToClassList("drill-item-uss");

        var label = new Label();
        label.AddToClassList("drill-item-text-uss");
        if (barlow700 != null)
        {
            label.style.unityFontDefinition = new StyleFontDefinition(barlow700);
        }

        var close = new Button { text = "\u2715" }; // ✕
        close.AddToClassList("drill-item-close");
        close.clicked += () =>
        {
            if (item.userData is int idx) _session.RemoveAt(idx);
        };

        item.Add(label);
        item.Add(close);
        return item;
    }

    private void BindDrillItem(VisualElement element, int index)
    {
        if (index < 0 || index >= _session.Drills.Count) return;

        element.userData = index; // read by the Close button click handler
        var label = element.Q<Label>(className: "drill-item-text-uss");
        if (label != null) label.text = _session.Drills[index];
        element.EnableInClassList("drill-item-active", index == _session.ActiveIndex);
    }

    private void OnDrillReordered(int oldIndex, int newIndex)
    {
        // ListView already reordered itemsSource (== _session.Drills); just fix the active index.
        _session.OnReordered(oldIndex, newIndex);
    }

    // ---- Session event handlers ------------------------------------------------

    private void HandleDrillsChanged()
    {
        if (_drillListView != null) _drillListView.RefreshItems();
        UpdateDrillsDisplay();

        // Adding/removing a drill may enable/disable START (needs >= 1 drill in the flow).
        UpdateControlInteractability();

        NetCoachDashboardState.Instance?.Server_SetDrills(_session.Drills);
    }

    private void HandleActiveDrillChanged()
    {
        UpdateRepDisplay();
        if (_drillListView != null)
        {
            _drillListView.RefreshItems(); // re-evaluate the .drill-item-active highlight
            if (_session.ActiveIndex >= 0) _drillListView.ScrollToItem(_session.ActiveIndex);
        }

        NetCoachDashboardState.Instance?.Server_SetActiveIndex(_session.ActiveIndex);

        // Resolve by name: the session list order differs from NetDrillsActivator's asset list,
        // so indices are not interchangeable. Unknown names resolve to null and clear the drill.
        if (Application.isPlaying && NetDrillsActivator.Instance != null)
        {
            var drill = NetDrillsActivator.Instance.GetDrill(_session.ActiveDrill ?? "");
            NetDrillsActivator.Instance.Server_SetActiveDrill(drill);
        }
    }

    private void AddDrill(string drillName)
    {
        _session.AddDrill(drillName); // -> HandleDrillsChanged refreshes the list + count
    }

    private void AddRandomDrill()
    {
        string[] randomDrills = { "ISO DRIVE", "FAST BREAK", "CORNER 3", "ZONE DEFENSE", "SCREEN & ROLL" };
        string drill = randomDrills[Random.Range(0, randomDrills.Length)];
        AddDrill(drill);
    }

    private void UpdateDrillsDisplay()
    {
        int count = _session.Drills.Count;
        if (_drillsCountText != null) _drillsCountText.text = string.Format("{0} drills", count);

        if (count > 0)
        {
            if (_buildSessionPlaceholder != null) _buildSessionPlaceholder.style.display = DisplayStyle.None;
            if (_drillListView != null) _drillListView.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (_buildSessionPlaceholder != null) _buildSessionPlaceholder.style.display = DisplayStyle.Flex;
            if (_drillListView != null) _drillListView.style.display = DisplayStyle.None;
        }
    }

    private void ApplySprites()
    {
        SetImageSprite("logoImage", logoSprite);
        SetImageSprite("streamEyeIcon", streamEyeSprite);
        SetImageSprite("realisticIcon", iconRealistic);
        SetImageSprite("hologramIcon", iconHologram);
        SetImageSprite("startIcon", iconStart);
        SetImageSprite("nextIcon", iconNext);
        SetImageSprite("pauseIcon", iconPause);
        SetImageSprite("stopIcon", iconStop);
        SetImageSprite("forceIcon", iconForce);
        SetImageSprite("pickAndRollIcon", iconPickAndRoll);
        SetImageSprite("shootingIcon", iconShooting);
        SetImageSprite("postPlaysIcon", iconPostPlays);
        SetImageSprite("marginIcon", iconMargin);
        SetImageSprite("randomDrillIcon", iconRandomDrill);
        SetImageSprite("analyticsIcon", iconAnalytics);

        // The ARena logo is much wider than it is tall; Image defaults to ScaleAndCrop, which would
        // crop it inside the header slot (USS -unity-background-scale-mode doesn't affect Image.sprite).
        var logo = _root.Q<UnityEngine.UIElements.Image>("logoImage");
        if (logo != null) logo.scaleMode = ScaleMode.ScaleToFit;
    }

    private void SetImageSprite(string name, Sprite sprite)
    {
        if (sprite == null) return;
        var img = _root.Q<UnityEngine.UIElements.Image>(name);
        if (img != null)
        {
            img.sprite = sprite;
        }
    }

    private void ApplyTypography()
    {
        SetFontToLabel(_timerText, inter700);
        SetFontToLabel(_repText, inter400);
        SetFontToLabel(_statusText, barlow700);
        SetFontToLabel(_drillsCountText, inter400);

        _root.Query<Label>().ForEach(lbl =>
        {
            if (lbl.ClassListContains("stream-toggle-text") ||
                lbl.ClassListContains("status-lbl") || lbl.ClassListContains("playback-text") ||
                lbl.ClassListContains("section-title") || lbl.ClassListContains("exercise-text") ||
                lbl.ClassListContains("random-drill-text") || lbl.ClassListContains("analytics-text") ||
                lbl.ClassListContains("pro-badge-text"))
            {
                SetFontToLabel(lbl, barlow700);
            }
            else if (lbl.ClassListContains("viewport-header") || lbl.ClassListContains("build-session-heading"))
            {
                SetFontToLabel(lbl, barlow600);
            }
            else if (lbl.ClassListContains("xr-status-text") || lbl.ClassListContains("status-timer") ||
                     lbl.ClassListContains("exercise-badge-text") || lbl.ClassListContains("upgrade-btn-text"))
            {
                SetFontToLabel(lbl, inter700);
            }
            else
            {
                SetFontToLabel(lbl, inter400);
            }
        });
    }

    private void SetFontToLabel(Label lbl, Font font)
    {
        if (lbl != null && font != null)
        {
            lbl.style.unityFontDefinition = new StyleFontDefinition(font);
        }
    }

    private void OnDestroy() {
        if(NetBoot.Instance && NetBoot.Instance.NetMnger)
            NetBoot.Instance.NetMnger.OnConnectionEvent -= NetMnger_OnConnectionEvent;
        if (videoReceiver)
            videoReceiver.OnVideoTextureChanged -= VideoReceiver_OnVideoTextureChanged;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        barlow600 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_600.ttf");
        barlow700 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_700.ttf");
        barlow900 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_900.ttf");
        inter400 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Inter_400.ttf");
        inter700 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Inter_700.ttf");

        string spriteDir = "Assets/UI/FigmaImport/CoachDashboard";
        logoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Container.png");
        streamEyeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Container_1_87.png");
        iconRealistic = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon.png");
        iconHologram = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_80.png");
        iconStart = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_112.png");
        iconNext = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_117.png");
        iconPause = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_123.png");
        iconStop = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_129.png");
        iconForce = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_134.png");
        iconPickAndRoll = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_148.png");
        iconShooting = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_156.png");
        iconPostPlays = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_164.png");
        iconMargin = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_margin.png");
        iconRandomDrill = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_188.png");
        iconAnalytics = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_200.png");
    }
#endif
}
