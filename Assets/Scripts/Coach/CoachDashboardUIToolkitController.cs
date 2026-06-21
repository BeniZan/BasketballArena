using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class CoachDashboardUIToolkitController : MonoBehaviour
{
    [Header("Fonts")]
    [SerializeField] private Font _barlow600;
    [SerializeField] private Font _barlow700;
    [SerializeField] private Font _barlow900;
    [SerializeField] private Font _inter400;
    [SerializeField] private Font _inter700;

    [Header("Sprites")]
    [SerializeField] private Sprite _logoSprite;
    [SerializeField] private Sprite _coachViewEyeSprite;
    [SerializeField] private Sprite _streamEyeSprite;
    [SerializeField] private Sprite _iconRealistic;
    [SerializeField] private Sprite _iconHologram;
    [SerializeField] private Sprite _iconStart;
    [SerializeField] private Sprite _iconNext;
    [SerializeField] private Sprite _iconPause;
    [SerializeField] private Sprite _iconStop;
    [SerializeField] private Sprite _iconForce;
    [SerializeField] private Sprite _iconPickAndRoll;
    [SerializeField] private Sprite _iconShooting;
    [SerializeField] private Sprite _iconPostPlays;
    [SerializeField] private Sprite _iconMargin;
    [SerializeField] private Sprite _iconRandomDrill;
    [SerializeField] private Sprite _iconAnalytics;

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

    private Button _h1OffenseBtn;
    private Button _h1DefenseBtn;
    private Button _h2OffenseBtn;
    private Button _h2DefenseBtn;

    private Button _pickAndRollBtn;
    private Button _shootingBtn;
    private Button _postPlaysBtn;
    private Button _randomDrillBtn;

    private Label _drillsCountText;
    private VisualElement _buildSessionPlaceholder;
    private ScrollView _drillListContainer;

    // State variables
    private float _elapsedTime = 0f;
    private bool _isTimerRunning = false;
    private int _repCount = 0;
    private int _totalReps = 0;
    private List<string> _addedDrills = new List<string>();

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            Debug.LogError("UIDocument component is required on the same GameObject!");
            return;
        }
    }

    // Setup runs in Start() so the UIDocument has already built its visual tree
    // in its own OnEnable() before we query elements (avoids ordering NRE).
    private void Start()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
        if (_root == null)
        {
            Debug.LogError("CoachDashboardUIToolkitController: rootVisualElement is null. Is a PanelSettings and Source Asset assigned to the UIDocument?");
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

        _h1OffenseBtn = _root.Q<Button>("h1OffenseBtn");
        _h1DefenseBtn = _root.Q<Button>("h1DefenseBtn");
        _h2OffenseBtn = _root.Q<Button>("h2OffenseBtn");
        _h2DefenseBtn = _root.Q<Button>("h2DefenseBtn");

        _pickAndRollBtn = _root.Q<Button>("pickAndRollBtn");
        _shootingBtn = _root.Q<Button>("shootingBtn");
        _postPlaysBtn = _root.Q<Button>("postPlaysBtn");
        _randomDrillBtn = _root.Q<Button>("randomDrillBtn");

        _drillsCountText = _root.Q<Label>("drillsCountText");
        _buildSessionPlaceholder = _root.Q<VisualElement>("buildSessionPlaceholder");
        _drillListContainer = _root.Q<ScrollView>("drillListContainer");

        // Wire event handlers (null-guarded)
        if (_startBtn != null) _startBtn.clicked += StartTimer;
        if (_pauseBtn != null) _pauseBtn.clicked += PauseTimer;
        if (_stopBtn != null) _stopBtn.clicked += StopTimer;
        if (_nextBtn != null) _nextBtn.clicked += NextRep;
        if (_forceBtn != null) _forceBtn.clicked += ForceSession;

        if (_realisticBtn != null) _realisticBtn.clicked += () => SetStreamMode(true);
        if (_hologramBtn != null) _hologramBtn.clicked += () => SetStreamMode(false);

        if (_h1OffenseBtn != null) _h1OffenseBtn.clicked += () => ToggleOffDef(true, true);
        if (_h1DefenseBtn != null) _h1DefenseBtn.clicked += () => ToggleOffDef(true, false);
        if (_h2OffenseBtn != null) _h2OffenseBtn.clicked += () => ToggleOffDef(false, true);
        if (_h2DefenseBtn != null) _h2DefenseBtn.clicked += () => ToggleOffDef(false, false);

        if (_pickAndRollBtn != null) _pickAndRollBtn.clicked += () => AddDrill("PICK & ROLL");
        if (_shootingBtn != null) _shootingBtn.clicked += () => AddDrill("SHOOTING");
        if (_postPlaysBtn != null) _postPlaysBtn.clicked += () => AddDrill("POST PLAYS");
        if (_randomDrillBtn != null) _randomDrillBtn.clicked += AddRandomDrill;

        // Apply visual Sprites and Fonts
        ApplySprites();
        ApplyTypography();

        // Initial setup
        SetStreamMode(true);
        ToggleOffDef(true, true);
        ToggleOffDef(false, true);
        UpdateTimerDisplay();
        UpdateRepDisplay();
        UpdateDrillsDisplay();
    }

    private void Update()
    {
        if (_isTimerRunning)
        {
            _elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }
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
    }

    private void StartTimer()
    {
        _isTimerRunning = true;
        _statusText.text = "RUNNING";
        _statusText.style.color = new StyleColor(new Color(16f/255f, 185f/255f, 129f/255f, 1f)); // Green
    }

    private void PauseTimer()
    {
        _isTimerRunning = false;
        _statusText.text = "PAUSED";
        _statusText.style.color = new StyleColor(new Color(255f/255f, 107f/255f, 0f, 1f)); // Orange
    }

    private void StopTimer()
    {
        _isTimerRunning = false;
        _elapsedTime = 0f;
        _repCount = 0;
        _totalReps = 0;
        _statusText.text = "READY";
        _statusText.style.color = new StyleColor(new Color(100f/255f, 116f/255f, 139f/255f, 1f)); // Gray
        UpdateTimerDisplay();
        UpdateRepDisplay();
    }

    private void NextRep()
    {
        if (_isTimerRunning)
        {
            _repCount++;
            _totalReps = Mathf.Max(_totalReps, _repCount);
            UpdateRepDisplay();
        }
    }

    private void ForceSession()
    {
        _repCount += 5;
        _totalReps = Mathf.Max(_totalReps, _repCount);
        UpdateRepDisplay();
    }

    private void UpdateTimerDisplay()
    {
        int minutes = Mathf.FloorToInt(_elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(_elapsedTime % 60f);
        _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateRepDisplay()
    {
        _repText.text = string.Format("REP {0}/{1}", _repCount, _totalReps);
    }

    private void ToggleOffDef(bool isH1, bool isOffense)
    {
        if (isH1)
        {
            if (isOffense)
            {
                _h1OffenseBtn.AddToClassList("active");
                _h1DefenseBtn.RemoveFromClassList("active");
            }
            else
            {
                _h1OffenseBtn.RemoveFromClassList("active");
                _h1DefenseBtn.AddToClassList("active");
            }
        }
        else
        {
            if (isOffense)
            {
                _h2OffenseBtn.AddToClassList("active");
                _h2DefenseBtn.RemoveFromClassList("active");
            }
            else
            {
                _h2OffenseBtn.RemoveFromClassList("active");
                _h2DefenseBtn.AddToClassList("active");
            }
        }
    }

    private void AddDrill(string drillName)
    {
        _addedDrills.Add(drillName);
        UpdateDrillsDisplay();

        // Create visual drill element
        CreateVisualDrillItem(drillName);
    }

    private void AddRandomDrill()
    {
        string[] randomDrills = { "ISO DRIVE", "FAST BREAK", "CORNER 3", "ZONE DEFENSE", "SCREEN & ROLL" };
        string drill = randomDrills[Random.Range(0, randomDrills.Length)];
        AddDrill(drill);
    }

    private void CreateVisualDrillItem(string drillName)
    {
        var item = new VisualElement();
        item.AddToClassList("drill-item-uss");

        var label = new Label(drillName);
        label.AddToClassList("drill-item-text-uss");
        if (_barlow700 != null)
        {
            label.style.unityFontDefinition = new StyleFontDefinition(_barlow700);
        }

        item.Add(label);
        _drillListContainer.Add(item);
    }

    private void UpdateDrillsDisplay()
    {
        _drillsCountText.text = string.Format("{0} drills", _addedDrills.Count);
        if (_addedDrills.Count > 0)
        {
            _buildSessionPlaceholder.style.display = DisplayStyle.None;
            _drillListContainer.style.display = DisplayStyle.Flex;
        }
        else
        {
            _buildSessionPlaceholder.style.display = DisplayStyle.Flex;
            _drillListContainer.style.display = DisplayStyle.None;
        }
    }

    private void ApplySprites()
    {
        SetImageSprite("logoImage", _logoSprite);
        SetImageSprite("coachViewEyeIcon", _coachViewEyeSprite);
        SetImageSprite("streamEyeIcon", _streamEyeSprite);
        SetImageSprite("realisticIcon", _iconRealistic);
        SetImageSprite("hologramIcon", _iconHologram);
        SetImageSprite("startIcon", _iconStart);
        SetImageSprite("nextIcon", _iconNext);
        SetImageSprite("pauseIcon", _iconPause);
        SetImageSprite("stopIcon", _iconStop);
        SetImageSprite("forceIcon", _iconForce);
        SetImageSprite("pickAndRollIcon", _iconPickAndRoll);
        SetImageSprite("shootingIcon", _iconShooting);
        SetImageSprite("postPlaysIcon", _iconPostPlays);
        SetImageSprite("marginIcon", _iconMargin);
        SetImageSprite("randomDrillIcon", _iconRandomDrill);
        SetImageSprite("analyticsIcon", _iconAnalytics);
    }

    private void SetImageSprite(string name, Sprite sprite)
    {
        if (sprite == null) return;
        var img = _root.Q<Image>(name);
        if (img != null)
        {
            img.sprite = sprite;
        }
    }

    private void ApplyTypography()
    {
        SetFontToLabel(_timerText, _inter700);
        SetFontToLabel(_repText, _inter400);
        SetFontToLabel(_statusText, _barlow700);
        SetFontToLabel(_drillsCountText, _inter400);

        _root.Query<Label>().ForEach(lbl =>
        {
            if (lbl.ClassListContains("label-badge-text"))
            {
                SetFontToLabel(lbl, _barlow900);
            }
            else if (lbl.ClassListContains("off-def-btn-text") || lbl.ClassListContains("coach-view-text") || 
                     lbl.ClassListContains("stream-title") || lbl.ClassListContains("stream-toggle-text") || 
                     lbl.ClassListContains("status-lbl") || lbl.ClassListContains("playback-text") || 
                     lbl.ClassListContains("section-title") || lbl.ClassListContains("exercise-text") || 
                     lbl.ClassListContains("random-drill-text") || lbl.ClassListContains("analytics-text") || 
                     lbl.ClassListContains("pro-badge-text"))
            {
                SetFontToLabel(lbl, _barlow700);
            }
            else if (lbl.ClassListContains("viewport-header") || lbl.ClassListContains("build-session-heading"))
            {
                SetFontToLabel(lbl, _barlow600);
            }
            else if (lbl.ClassListContains("bottom-info-text") || lbl.ClassListContains("status-timer") || 
                     lbl.ClassListContains("exercise-badge-text") || lbl.ClassListContains("upgrade-btn-text"))
            {
                SetFontToLabel(lbl, _inter700);
            }
            else
            {
                SetFontToLabel(lbl, _inter400);
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

#if UNITY_EDITOR
    private void Reset()
    {
        _barlow600 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_600.ttf");
        _barlow700 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_700.ttf");
        _barlow900 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Barlow Condensed_900.ttf");
        _inter400 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Inter_400.ttf");
        _inter700 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Figma/Fonts/Inter_700.ttf");

        string spriteDir = "Assets/UI/FigmaImport/CoachDashboard";
        _logoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Container.png");
        _coachViewEyeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Container_1_53.png");
        _streamEyeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Container_1_87.png");
        _iconRealistic = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon.png");
        _iconHologram = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_80.png");
        _iconStart = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_112.png");
        _iconNext = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_117.png");
        _iconPause = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_123.png");
        _iconStop = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_129.png");
        _iconForce = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_134.png");
        _iconPickAndRoll = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_148.png");
        _iconShooting = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_156.png");
        _iconPostPlays = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_164.png");
        _iconMargin = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_margin.png");
        _iconRandomDrill = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_188.png");
        _iconAnalytics = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_1_200.png");
    }
#endif
}
