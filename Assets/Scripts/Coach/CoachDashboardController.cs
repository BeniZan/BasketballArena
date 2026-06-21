using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CoachDashboardController : MonoBehaviour
{
    [Header("Timer & Status")]
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private TextMeshProUGUI _repText;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Controls")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _stopButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _forceButton;

    [Header("Stream Modes")]
    [SerializeField] private Button _realisticButton;
    [SerializeField] private Button _hologramButton;
    [SerializeField] private Image _realisticBg;
    [SerializeField] private Image _hologramBg;
    [SerializeField] private TextMeshProUGUI _realisticText;
    [SerializeField] private TextMeshProUGUI _hologramText;

    [Header("H1 / H2 Team Cards")]
    [SerializeField] private Button _h1OffenseBtn;
    [SerializeField] private Button _h1DefenseBtn;
    [SerializeField] private Button _h2OffenseBtn;
    [SerializeField] private Button _h2DefenseBtn;

    [Header("Exercise Library")]
    [SerializeField] private Button _pickAndRollBtn;
    [SerializeField] private Button _shootingBtn;
    [SerializeField] private Button _postPlaysBtn;
    [SerializeField] private Button _randomDrillBtn;

    [Header("Training Flow")]
    [SerializeField] private TextMeshProUGUI _drillsCountText;
    [SerializeField] private GameObject _buildSessionPlaceholder;
    [SerializeField] private Transform _drillListContainer;
    [SerializeField] private GameObject _drillItemPrefab; // Optional prefab or dynamically generated

    private float _elapsedTime = 0f;
    private bool _isTimerRunning = false;
    private int _repCount = 0;
    private int _totalReps = 0;
    private List<string> _addedDrills = new List<string>();

    private void Awake()
    {
        // Wire Stream Modes
        _realisticButton.onClick.AddListener(() => SetStreamMode(true));
        _hologramButton.onClick.AddListener(() => SetStreamMode(false));

        // Wire Controls
        _startButton.onClick.AddListener(StartTimer);
        _pauseButton.onClick.AddListener(PauseTimer);
        _stopButton.onClick.AddListener(StopTimer);
        _nextButton.onClick.AddListener(NextRep);
        _forceButton.onClick.AddListener(ForceSession);

        // Wire H1 / H2 Buttons
        _h1OffenseBtn.onClick.AddListener(() => ToggleOffDef(true, true));
        _h1DefenseBtn.onClick.AddListener(() => ToggleOffDef(true, false));
        _h2OffenseBtn.onClick.AddListener(() => ToggleOffDef(false, true));
        _h2DefenseBtn.onClick.AddListener(() => ToggleOffDef(false, false));

        // Wire Exercise Library
        _pickAndRollBtn.onClick.AddListener(() => AddDrill("PICK & ROLL"));
        _shootingBtn.onClick.AddListener(() => AddDrill("SHOOTING"));
        _postPlaysBtn.onClick.AddListener(() => AddDrill("POST PLAYS"));
        _randomDrillBtn.onClick.AddListener(AddRandomDrill);

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
        // Realistic Active Colors (White Bg, Black Text)
        // Hologram Active Colors (Gray/Transparent Bg, White/Gray Text)
        if (realistic)
        {
            _realisticBg.color = Color.white;
            _realisticText.color = Color.black;

            _hologramBg.color = new Color(1f, 1f, 1f, 0.05f);
            _hologramText.color = new Color(100f/255f, 116f/255f, 139f/255f, 1f);
        }
        else
        {
            _realisticBg.color = new Color(1f, 1f, 1f, 0.05f);
            _realisticText.color = new Color(100f/255f, 116f/255f, 139f/255f, 1f);

            _hologramBg.color = Color.white;
            _hologramText.color = Color.black;
        }
    }

    private void StartTimer()
    {
        _isTimerRunning = true;
        _statusText.text = "RUNNING";
        _statusText.color = new Color(16f/255f, 185f/255f, 129f/255f, 1f); // Green
    }

    private void PauseTimer()
    {
        _isTimerRunning = false;
        _statusText.text = "PAUSED";
        _statusText.color = new Color(255f/255f, 107f/255f, 0f, 1f); // Orange
    }

    private void StopTimer()
    {
        _isTimerRunning = false;
        _elapsedTime = 0f;
        _repCount = 0;
        _totalReps = 0;
        _statusText.text = "READY";
        _statusText.color = new Color(100f/255f, 116f/255f, 139f/255f, 1f); // Gray
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
        Color activeColor = new Color(255f/255f, 255f/255f, 255f/255f, 0.15f);
        Color inactiveColor = new Color(255f/255f, 255f/255f, 255f/255f, 0.05f);
        Color activeTextColor = Color.white;
        Color inactiveTextColor = new Color(100f/255f, 116f/255f, 139f/255f, 1f);

        if (isH1)
        {
            _h1OffenseBtn.GetComponent<Image>().color = isOffense ? activeColor : inactiveColor;
            _h1OffenseBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = isOffense ? activeTextColor : inactiveTextColor;

            _h1DefenseBtn.GetComponent<Image>().color = !isOffense ? activeColor : inactiveColor;
            _h1DefenseBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = !isOffense ? activeTextColor : inactiveTextColor;
        }
        else
        {
            _h2OffenseBtn.GetComponent<Image>().color = isOffense ? activeColor : inactiveColor;
            _h2OffenseBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = isOffense ? activeTextColor : inactiveTextColor;

            _h2DefenseBtn.GetComponent<Image>().color = !isOffense ? activeColor : inactiveColor;
            _h2DefenseBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = !isOffense ? activeTextColor : inactiveTextColor;
        }
    }

    private void AddDrill(string drillName)
    {
        _addedDrills.Add(drillName);
        UpdateDrillsDisplay();

        // Instantiate visual element for drill
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
        // Dynamically create a small nice-looking list item
        GameObject item = new GameObject("DrillItem_" + drillName, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(_drillListContainer, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(391, 32);

        Image img = item.GetComponent<Image>();
        img.color = new Color(255f/255f, 107f/255f, 0f, 0.08f); // Soft orange theme

        // Add padding helper or spacing components if needed
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(item.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-24, 0); // Padding left/right

        TextMeshProUGUI tmpText = textObj.GetComponent<TextMeshProUGUI>();
        tmpText.text = drillName;
        tmpText.font = _timerText.font; // Reuse SDF font
        tmpText.fontSize = 11;
        tmpText.alignment = TextAlignmentOptions.Left;
        tmpText.color = new Color(255f/255f, 255f/255f, 255f/255f, 0.85f);
        tmpText.verticalAlignment = VerticalAlignmentOptions.Middle;
    }

    private void UpdateDrillsDisplay()
    {
        _drillsCountText.text = string.Format("{0} drills", _addedDrills.Count);
        if (_addedDrills.Count > 0)
        {
            _buildSessionPlaceholder.SetActive(false);
        }
        else
        {
            _buildSessionPlaceholder.SetActive(true);
        }
    }
}
