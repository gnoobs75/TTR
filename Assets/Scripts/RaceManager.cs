using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central controller for the Brown Town Grand Prix.
/// Manages race state, tracks all racers, calculates positions and time gaps.
/// Spawns 3D finish line gate and handles camera orbit on finish.
/// Race to the Brown Town Sewage Treatment Plant!
/// </summary>
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }

    public enum State { PreRace, Countdown, Racing, PlayerFinished, Finished }

    [Header("Race Settings")]
    public float raceDistance = 1000f; // meters to the Sewage Treatment Plant
    public float countdownDuration = 3f;

    [Header("References")]
    public TurdController playerController;
    public RacerAI[] aiRacers;
    public RaceLeaderboard leaderboard;
    public RaceFinish finishLine;
    public PipeGenerator pipeGen;

    // State
    private State _state = State.PreRace;
    private float _raceStartTime;
    private float _countdownTimer;
    private int _nextFinishPlace = 1;

    // Racer tracking
    private List<RacerEntry> _entries = new List<RacerEntry>();
    private float _leaderDistance;

    // Finish line 3D gate
    private GameObject _finishGate;
    private bool _gateSpawned;

    // Camera orbit on finish
    private bool _orbitingCamera;
    private float _orbitTimer;
    private float _orbitDuration = 4f;
    private Vector3 _orbitCenter;
    private float _orbitRadius = 5f;

    // Auto-finish timeout (don't wait forever for AI)
    private float _autoFinishTimer;
    private const float AUTO_FINISH_TIMEOUT = 15f;

    // Position change tracking
    private int _lastPlayerPosition = 0;

    // === COUNTDOWN UI ===
    private Text _countdownText;
    private Outline _countdownOutline;
    private int _lastCountdownNumber = -1;
    private float _countdownPunchTime;

    // === RACE POSITION HUD ===
    private Text _positionHudText;
    private Outline _positionHudOutline;
    private float _positionHudPunchTime;
    private int _lastHudPosition = -1;

    // === RACE TIMER HUD ===
    private Text _raceTimerText;

    // === RACE PROGRESS BAR ===
    private RectTransform _progressBarBg;
    private RectTransform _progressBarFill;
    private Image _progressFillImage;
    private RectTransform[] _racerDots;
    private Image[] _racerDotImages;
    private Text _progressLabel;

    // === FINAL STRETCH ===
    private bool _finalStretchTriggered;
    private float _finalStretchStart;
    private const float FINAL_STRETCH_DISTANCE = 100f; // last 100m

    // === DISTANCE MILESTONES ===
    private int _lastMilestoneIndex = -1;

    // === POSITION CHANGE ARROW ===
    private Text _posChangeArrow;
    private float _posChangeShowTime;
    private bool _posChangeUp; // true = gained, false = lost
    private static readonly string[] GAIN_QUIPS = {
        "EAT MY CORN!", "OUTTA THE WAY!", "PLOP PLOP PASS!",
        "SEWER SPEED!", "BROWN LIGHTNING!", "FLUSH 'EM!"
    };
    private static readonly string[] LOSE_QUIPS = {
        "THEY SLIPPED PAST!", "TURBO TURD!", "WATCH YOUR SIX!",
        "INCOMING!", "GOT SPLASHED!", "OVERTAKEN!"
    };

    public State RaceState => _state;
    public float LeaderDistance => _leaderDistance;
    public float RaceTime => _state >= State.Racing ? Time.time - _raceStartTime : 0f;
    public float RaceDistance => raceDistance;
    public TurdController PlayerController => playerController;
    public List<RacerEntry> Entries => _entries;
    public bool IsOrbiting => _orbitingCamera;

    public struct RacerEntry
    {
        public string name;
        public Color color;
        public float distance;
        public float speed;
        public int position;       // 1-based
        public float gapToLeader;  // seconds behind leader
        public bool isPlayer;
        public bool isFinished;
        public int finishPlace;    // 0 = not finished yet
        public float finishTime;
        public RacerAI ai;         // null for player
        public Transform transform; // for podium positioning
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildEntries();
        CreateCountdownUI();
        CreatePositionHUD();
        CreatePositionChangeArrow();
        CreateRaceTimerHUD();
        CreateProgressBar();
    }

    void BuildEntries()
    {
        _entries.Clear();

        // Player entry
        _entries.Add(new RacerEntry
        {
            name = "Mr. Corny",
            color = new Color(0.45f, 0.28f, 0.1f),
            isPlayer = true,
            ai = null,
            transform = playerController != null ? playerController.transform : null
        });

        // AI entries
        if (aiRacers != null)
        {
            foreach (var ai in aiRacers)
            {
                if (ai == null) continue;
                _entries.Add(new RacerEntry
                {
                    name = ai.racerName,
                    color = ai.racerColor,
                    isPlayer = false,
                    ai = ai,
                    transform = ai.transform
                });
            }
        }
    }

    void CreateCountdownUI()
    {
        // Find or create a canvas for the countdown overlay
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Big center-screen countdown number
        GameObject countObj = new GameObject("RaceCountdown");
        RectTransform rt = countObj.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.3f, 0.35f);
        rt.anchorMax = new Vector2(0.7f, 0.7f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _countdownText = countObj.AddComponent<Text>();
        _countdownText.font = font;
        _countdownText.fontSize = 120;
        _countdownText.fontStyle = FontStyle.Bold;
        _countdownText.alignment = TextAnchor.MiddleCenter;
        _countdownText.color = new Color(1f, 0.92f, 0.15f);
        _countdownText.text = "";
        _countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _countdownText.verticalOverflow = VerticalWrapMode.Overflow;

        _countdownOutline = countObj.AddComponent<Outline>();
        _countdownOutline.effectColor = new Color(0, 0, 0, 1f);
        _countdownOutline.effectDistance = new Vector2(4, -4);

        // Second outline for extra punch
        Outline outline2 = countObj.AddComponent<Outline>();
        outline2.effectColor = new Color(0.3f, 0.15f, 0f, 0.8f);
        outline2.effectDistance = new Vector2(-2, 2);

        countObj.SetActive(false);
    }

    void CreatePositionHUD()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Big position indicator top-right
        GameObject posObj = new GameObject("RacePositionHUD");
        RectTransform rt = posObj.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.78f, 0.82f);
        rt.anchorMax = new Vector2(0.98f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Dark background for readability
        Image bg = posObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.04f, 0.02f, 0.65f);

        // Position text
        GameObject textObj = new GameObject("PosText");
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        _positionHudText = textObj.AddComponent<Text>();
        _positionHudText.font = font;
        _positionHudText.fontSize = 52;
        _positionHudText.fontStyle = FontStyle.Bold;
        _positionHudText.alignment = TextAnchor.MiddleCenter;
        _positionHudText.color = new Color(1f, 0.85f, 0.1f);
        _positionHudText.text = "";
        _positionHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _positionHudText.verticalOverflow = VerticalWrapMode.Overflow;

        _positionHudOutline = textObj.AddComponent<Outline>();
        _positionHudOutline.effectColor = new Color(0, 0, 0, 1f);
        _positionHudOutline.effectDistance = new Vector2(2, -2);

        posObj.SetActive(false);
    }

    void CreatePositionChangeArrow()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Arrow indicator to the LEFT of the position HUD
        GameObject arrowObj = new GameObject("PosChangeArrow");
        RectTransform art = arrowObj.AddComponent<RectTransform>();
        art.SetParent(canvas.transform, false);
        art.anchorMin = new Vector2(0.68f, 0.85f);
        art.anchorMax = new Vector2(0.78f, 0.95f);
        art.offsetMin = Vector2.zero;
        art.offsetMax = Vector2.zero;

        _posChangeArrow = arrowObj.AddComponent<Text>();
        _posChangeArrow.font = font;
        _posChangeArrow.fontSize = 48;
        _posChangeArrow.fontStyle = FontStyle.Bold;
        _posChangeArrow.alignment = TextAnchor.MiddleCenter;
        _posChangeArrow.color = Color.clear; // hidden initially
        _posChangeArrow.text = "";
        _posChangeArrow.horizontalOverflow = HorizontalWrapMode.Overflow;

        Outline arrowOutline = arrowObj.AddComponent<Outline>();
        arrowOutline.effectColor = new Color(0, 0, 0, 0.9f);
        arrowOutline.effectDistance = new Vector2(2, -2);
    }

    void CreateRaceTimerHUD()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Race timer below position HUD
        GameObject timerObj = new GameObject("RaceTimer");
        RectTransform rt = timerObj.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.78f, 0.76f);
        rt.anchorMax = new Vector2(0.98f, 0.83f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _raceTimerText = timerObj.AddComponent<Text>();
        _raceTimerText.font = font;
        _raceTimerText.fontSize = 22;
        _raceTimerText.alignment = TextAnchor.MiddleCenter;
        _raceTimerText.color = new Color(0.8f, 0.8f, 0.75f);
        _raceTimerText.text = "";

        Outline timerOutline = timerObj.AddComponent<Outline>();
        timerOutline.effectColor = new Color(0, 0, 0, 0.9f);
        timerOutline.effectDistance = new Vector2(1, -1);

        timerObj.SetActive(false);
    }

    void UpdateProgressBar()
    {
        if (_progressBarBg == null || !_progressBarBg.gameObject.activeSelf) return;

        // Update fill based on player distance
        float playerDist = playerController != null ? playerController.DistanceTraveled : 0f;
        float progress = Mathf.Clamp01(playerDist / raceDistance);
        _progressBarFill.anchorMax = new Vector2(progress, 1f);

        // Color shifts from green to gold as you approach the finish
        Color fillColor = Color.Lerp(
            new Color(0.3f, 0.8f, 0.2f, 0.8f),
            new Color(1f, 0.85f, 0.1f, 0.9f),
            progress);
        _progressFillImage.color = fillColor;

        // Update racer dots
        for (int i = 0; i < _entries.Count && i < _racerDots.Length; i++)
        {
            var entry = _entries[i];
            float racerProgress = Mathf.Clamp01(entry.distance / raceDistance);

            _racerDots[i].gameObject.SetActive(true);
            _racerDots[i].anchorMin = new Vector2(racerProgress, 0.5f);
            _racerDots[i].anchorMax = new Vector2(racerProgress, 0.5f);
            _racerDots[i].anchoredPosition = Vector2.zero;

            // Player dot is bright gold, AI uses their racer color
            _racerDotImages[i].color = entry.isPlayer
                ? new Color(1f, 0.92f, 0.15f)
                : entry.color;

            _racerDots[i].sizeDelta = entry.isPlayer
                ? new Vector2(14, 14) : new Vector2(8, 8);
        }

        // Distance label
        if (_progressLabel != null)
        {
            int remaining = Mathf.Max(0, Mathf.CeilToInt(raceDistance - playerDist));
            _progressLabel.text = remaining > 0
                ? $"{remaining}m to Brown Town"
                : "BROWN TOWN!";
        }
    }

    void CreateProgressBar()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Background bar at top of screen (slim, under safe area)
        GameObject bgObj = new GameObject("RaceProgressBg");
        _progressBarBg = bgObj.AddComponent<RectTransform>();
        _progressBarBg.SetParent(canvas.transform, false);
        _progressBarBg.anchorMin = new Vector2(0.08f, 0.94f);
        _progressBarBg.anchorMax = new Vector2(0.72f, 0.965f);
        _progressBarBg.offsetMin = Vector2.zero;
        _progressBarBg.offsetMax = Vector2.zero;

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.06f, 0.04f, 0.7f);

        // Fill bar (shows player progress)
        GameObject fillObj = new GameObject("ProgressFill");
        _progressBarFill = fillObj.AddComponent<RectTransform>();
        _progressBarFill.SetParent(_progressBarBg, false);
        _progressBarFill.anchorMin = Vector2.zero;
        _progressBarFill.anchorMax = new Vector2(0f, 1f); // width = 0 initially
        _progressBarFill.offsetMin = Vector2.zero;
        _progressBarFill.offsetMax = Vector2.zero;

        _progressFillImage = fillObj.AddComponent<Image>();
        _progressFillImage.color = new Color(0.3f, 0.8f, 0.2f, 0.6f);

        // Distance label (e.g. "420m to Brown Town")
        GameObject labelObj = new GameObject("ProgressLabel");
        RectTransform labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.SetParent(_progressBarBg, false);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4, 0);
        labelRt.offsetMax = new Vector2(-4, 0);

        _progressLabel = labelObj.AddComponent<Text>();
        _progressLabel.font = font;
        _progressLabel.fontSize = 14;
        _progressLabel.alignment = TextAnchor.MiddleCenter;
        _progressLabel.color = new Color(0.9f, 0.9f, 0.85f, 0.9f);
        _progressLabel.text = "";
        _progressLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        Outline labelOutline = labelObj.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0, 0, 0, 0.9f);
        labelOutline.effectDistance = new Vector2(1, -1);

        // Racer dots (one per entry)
        int count = _entries.Count;
        _racerDots = new RectTransform[count];
        _racerDotImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject dotObj = new GameObject("RacerDot_" + i);
            _racerDots[i] = dotObj.AddComponent<RectTransform>();
            _racerDots[i].SetParent(_progressBarBg, false);
            _racerDots[i].sizeDelta = new Vector2(10, 10);
            _racerDots[i].anchorMin = new Vector2(0, 0.5f);
            _racerDots[i].anchorMax = new Vector2(0, 0.5f);
            _racerDots[i].anchoredPosition = Vector2.zero;

            _racerDotImages[i] = dotObj.AddComponent<Image>();
            _racerDotImages[i].color = _entries[i].color;

            // Player dot is bigger and has outline
            if (_entries[i].isPlayer)
            {
                _racerDots[i].sizeDelta = new Vector2(14, 14);
                Outline dotOutline = dotObj.AddComponent<Outline>();
                dotOutline.effectColor = new Color(1f, 0.85f, 0.1f, 0.9f);
                dotOutline.effectDistance = new Vector2(1, -1);
            }
        }

        // Finish flag icon at the right end
        GameObject flagObj = new GameObject("FinishFlag");
        RectTransform flagRt = flagObj.AddComponent<RectTransform>();
        flagRt.SetParent(_progressBarBg, false);
        flagRt.anchorMin = new Vector2(1f, 0);
        flagRt.anchorMax = new Vector2(1f, 1f);
        flagRt.anchoredPosition = new Vector2(8, 0);
        flagRt.sizeDelta = new Vector2(16, 0);

        Text flagText = flagObj.AddComponent<Text>();
        flagText.font = font;
        flagText.fontSize = 14;
        flagText.alignment = TextAnchor.MiddleCenter;
        flagText.color = Color.white;
        flagText.text = "F"; // simple finish marker
        flagText.fontStyle = FontStyle.Bold;

        bgObj.SetActive(false);
    }

    Canvas FindOverlayCanvas()
    {
        // Find existing UI canvas (ScreenEffects sits on one)
        if (ScreenEffects.Instance != null)
        {
            Canvas c = ScreenEffects.Instance.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        // Try finding any screen-space canvas
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        return null;
    }

    void Update()
    {
        switch (_state)
        {
            case State.PreRace:
                if (GameManager.Instance != null && GameManager.Instance.isPlaying)
                    StartCountdown();
                break;

            case State.Countdown:
                UpdateCountdown();
                break;

            case State.Racing:
                UpdateRace();
                SpawnFinishGateAhead();
                break;

            case State.PlayerFinished:
                UpdateRace(); // keep tracking AI
                UpdateOrbit();
                UpdateAutoFinish();
                break;

            case State.Finished:
                UpdateOrbit();
                break;
        }
    }

    void StartCountdown()
    {
        _state = State.Countdown;
        _countdownTimer = countdownDuration;

        if (aiRacers != null)
        {
            for (int i = 0; i < aiRacers.Length; i++)
            {
                if (aiRacers[i] != null)
                    aiRacers[i].SetStartOffset(-3f - i * 2f);
            }
        }
    }

    void UpdateCountdown()
    {
        _countdownTimer -= Time.deltaTime;

        // Show countdown numbers (3, 2, 1, GO!)
        int displayNum = Mathf.CeilToInt(_countdownTimer);
        if (displayNum != _lastCountdownNumber && displayNum >= 0)
        {
            _lastCountdownNumber = displayNum;
            _countdownPunchTime = Time.time;

            if (_countdownText != null)
            {
                _countdownText.gameObject.SetActive(true);

                if (displayNum > 0)
                {
                    _countdownText.text = displayNum.ToString();
                    _countdownText.color = displayNum == 1
                        ? new Color(1f, 0.3f, 0.15f) // red for 1
                        : displayNum == 2
                            ? new Color(1f, 0.85f, 0.1f) // yellow for 2
                            : new Color(0.3f, 1f, 0.4f); // green for 3
                    _countdownText.fontSize = 140;
                }
                else
                {
                    _countdownText.text = "GO!";
                    _countdownText.color = new Color(0.1f, 1f, 0.3f);
                    _countdownText.fontSize = 160;
                }
            }

            // Audio tick
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCountdownTick();

            // Camera shake escalates
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.1f + (3 - displayNum) * 0.05f);

            HapticManager.MediumTap();
        }

        // Animate countdown text - punch scale then decay
        if (_countdownText != null && _countdownText.gameObject.activeSelf)
        {
            float since = Time.time - _countdownPunchTime;
            float scale;
            if (since < 0.1f)
                scale = Mathf.Lerp(2.5f, 0.9f, since / 0.1f);
            else if (since < 0.2f)
                scale = Mathf.Lerp(0.9f, 1.05f, (since - 0.1f) / 0.1f);
            else
                scale = Mathf.Lerp(1.05f, 1f, (since - 0.2f) / 0.1f);
            _countdownText.transform.localScale = Vector3.one * Mathf.Max(scale, 1f);
        }

        if (_countdownTimer <= 0f)
        {
            _state = State.Racing;
            _raceStartTime = Time.time;

            // Show "GO!" briefly then hide
            StartCoroutine(HideCountdownAfterDelay(0.6f));

            // Show position HUD, race timer, and progress bar
            if (_positionHudText != null)
                _positionHudText.transform.parent.gameObject.SetActive(true);
            if (_raceTimerText != null)
                _raceTimerText.gameObject.SetActive(true);
            if (_progressBarBg != null)
                _progressBarBg.gameObject.SetActive(true);

            // Play game start sound for emphasis
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayGameStart();

            // Big dramatic camera punch
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(8f);
                PipeCamera.Instance.Shake(0.15f);
            }

            // Speed streaks flash for launch feel
            if (ScreenEffects.Instance != null)
            {
                ScreenEffects.Instance.FlashSpeedStreaks(1.2f);
                ScreenEffects.Instance.TriggerPowerUpFlash();
            }

            // "RACE TO BROWN TOWN!" motivational popup
            StartCoroutine(ShowRaceStartPopup());

            HapticManager.HeavyTap();
        }
    }

    IEnumerator HideCountdownAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_countdownText != null)
            _countdownText.gameObject.SetActive(false);
    }

    IEnumerator ShowRaceStartPopup()
    {
        yield return new WaitForSeconds(0.8f);
        string[] raceMottos = {
            "RACE TO BROWN TOWN!",
            "EAT MY FLUSH!",
            "FULL SPEED TO THE PLANT!",
            "RACE FOR THE THRONE!",
            "GO GO GO!"
        };
        string motto = raceMottos[Random.Range(0, raceMottos.Length)];
        if (ScorePopup.Instance != null && playerController != null)
            ScorePopup.Instance.ShowMilestone(
                playerController.transform.position + Vector3.up * 3f, motto);
    }

    void UpdateRace()
    {
        float raceTime = Time.time - _raceStartTime;

        _leaderDistance = 0f;
        float leaderSpeed = 1f;

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];

            if (e.isPlayer && playerController != null)
            {
                e.distance = playerController.DistanceTraveled;
                e.speed = playerController.CurrentSpeed;
            }
            else if (e.ai != null)
            {
                e.distance = e.ai.DistanceTraveled;
                e.speed = e.ai.CurrentSpeed;
            }

            if (e.distance > _leaderDistance)
            {
                _leaderDistance = e.distance;
                leaderSpeed = Mathf.Max(e.speed, 1f);
            }

            _entries[i] = e;
        }

        _entries.Sort((a, b) => b.distance.CompareTo(a.distance));

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.position = i + 1;

            if (i == 0)
                e.gapToLeader = 0f;
            else
                e.gapToLeader = (_leaderDistance - e.distance) / leaderSpeed;

            // Check finish line crossing
            if (!e.isFinished && e.distance >= raceDistance)
            {
                e.isFinished = true;
                e.finishPlace = _nextFinishPlace++;
                e.finishTime = raceTime;

                if (e.ai != null)
                    e.ai.OnFinish(raceTime);

                if (e.isPlayer)
                    OnPlayerFinished(e.finishPlace, raceTime);
            }

            _entries[i] = e;
        }

        // Position change announcements (with animated arrows + CheerOverlay)
        foreach (var e in _entries)
        {
            if (e.isPlayer && !e.isFinished)
            {
                if (_lastPlayerPosition > 0 && e.position != _lastPlayerPosition)
                {
                    bool improved = e.position < _lastPlayerPosition;
                    string posStr = e.position + GetOrdinal(e.position);
                    if (improved)
                    {
                        // Player moved UP in position
                        string quip = GAIN_QUIPS[Random.Range(0, GAIN_QUIPS.Length)];
                        if (ScorePopup.Instance != null && playerController != null)
                            ScorePopup.Instance.ShowMilestone(
                                playerController.transform.position + Vector3.up * 2f, posStr + " PLACE!");
                        if (PipeCamera.Instance != null)
                            PipeCamera.Instance.PunchFOV(3f);
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayCelebration();

                        // Show animated UP arrow
                        ShowPositionChangeArrow(true);

                        // CheerOverlay reacts to overtake
                        if (CheerOverlay.Instance != null)
                            CheerOverlay.Instance.ShowCheer(quip,
                                new Color(0.2f, 1f, 0.3f), e.position == 1);

                        // Green flash for gaining position
                        if (ScreenEffects.Instance != null)
                            ScreenEffects.Instance.TriggerMilestoneFlash();

                        HapticManager.MediumTap();

                        // Taking 1st place is a BIG deal
                        if (e.position == 1)
                        {
                            if (PipeCamera.Instance != null)
                                PipeCamera.Instance.Shake(0.2f);
                            if (ParticleManager.Instance != null && playerController != null)
                                ParticleManager.Instance.PlayCelebration(playerController.transform.position);
                            HapticManager.HeavyTap();
                        }
                    }
                    else
                    {
                        // Player dropped position - show who passed them
                        string passerName = GetRacerNameAtPosition(e.position - 1);
                        string quip = LOSE_QUIPS[Random.Range(0, LOSE_QUIPS.Length)];
                        if (ScorePopup.Instance != null && playerController != null && passerName.Length > 0)
                            ScorePopup.Instance.ShowMilestone(
                                playerController.transform.position + Vector3.up * 2f,
                                passerName + " PASSES YOU!");
                        if (PipeCamera.Instance != null)
                            PipeCamera.Instance.Shake(0.15f);
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayAITaunt();

                        // Show animated DOWN arrow
                        ShowPositionChangeArrow(false);

                        // CheerOverlay reacts to being passed
                        if (CheerOverlay.Instance != null)
                            CheerOverlay.Instance.ShowCheer(quip,
                                new Color(1f, 0.4f, 0.2f), false);

                        HapticManager.LightTap();
                    }
                }
                _lastPlayerPosition = e.position;
                break;
            }
        }

        if (leaderboard != null)
            leaderboard.UpdatePositions(_entries);

        // === UPDATE POSITION HUD ===
        UpdatePositionHUD();

        // === UPDATE PROGRESS BAR ===
        UpdateProgressBar();

        // === UPDATE RACE TIMER ===
        if (_raceTimerText != null && _raceTimerText.gameObject.activeSelf)
        {
            float t = raceTime;
            int mins = (int)(t / 60f);
            float secs = t % 60f;
            _raceTimerText.text = mins > 0
                ? $"{mins}:{secs:00.0}"
                : $"{secs:F1}s";
        }

        // === DISTANCE MILESTONES ===
        CheckDistanceMilestones();

        // === FINAL STRETCH CHECK ===
        CheckFinalStretch();

        // Check if all racers finished
        bool allFinished = true;
        foreach (var e in _entries)
        {
            if (!e.isFinished) { allFinished = false; break; }
        }
        if (allFinished)
            OnRaceComplete();
    }

    void OnPlayerFinished(int place, float time)
    {
        _state = State.PlayerFinished;
        _autoFinishTimer = 0f;
        Debug.Log($"TTR Race: Player finished in {place}{GetOrdinal(place)} place! Time: {time:F1}s");

        // Stop player movement
        if (playerController != null)
            playerController.enabled = false;

        // Hide race HUD (position + timer + progress + arrow) since the finish banner takes over
        if (_positionHudText != null)
            _positionHudText.transform.parent.gameObject.SetActive(false);
        if (_raceTimerText != null)
            _raceTimerText.gameObject.SetActive(false);
        if (_progressBarBg != null)
            _progressBarBg.gameObject.SetActive(false);
        if (_posChangeArrow != null)
            _posChangeArrow.gameObject.SetActive(false);

        // Start camera orbit around player
        StartCameraOrbit();

        if (finishLine != null)
            finishLine.OnPlayerFinished(place, time);

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCelebration();

        HapticManager.HeavyTap();
    }

    void OnRaceComplete()
    {
        _state = State.Finished;
        Debug.Log("TTR Race: All racers finished! Race complete.");

        if (finishLine != null)
            finishLine.ShowPodium(_entries);
    }

    // === 3D FINISH LINE GATE ===

    void SpawnFinishGateAhead()
    {
        if (_gateSpawned || pipeGen == null) return;

        float playerDist = playerController != null ? playerController.DistanceTraveled : 0f;

        // Spawn gate when player is within 100m of finish
        if (playerDist + 100f >= raceDistance)
        {
            _gateSpawned = true;
            CreateFinishGate();
        }
    }

    void CreateFinishGate()
    {
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(raceDistance, out center, out forward, out right, out up);

        _finishGate = new GameObject("FinishGate");
        _finishGate.transform.position = center;
        _finishGate.transform.rotation = Quaternion.LookRotation(forward, up);

        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader shader = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        float radius = pipeGen.pipeRadius * 0.92f;

        // Checkered arch ring at finish
        int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 360f;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
            Vector3 pos = center + dir * radius;

            // Alternating black/white checkered pattern
            bool isBlack = (i % 2 == 0);
            Color col = isBlack ? new Color(0.05f, 0.05f, 0.05f) : Color.white;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Checker";
            block.transform.SetParent(_finishGate.transform);
            block.transform.position = pos;
            block.transform.rotation = Quaternion.LookRotation(forward, -dir);
            block.transform.localScale = new Vector3(0.8f, 0.4f, 0.3f);

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", col);
            if (!isBlack)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 0.3f);
            }
            block.GetComponent<Renderer>().material = mat;
            Collider c = block.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        // "FINISH" banner across the top
        GameObject bannerObj = new GameObject("FinishBanner3D");
        bannerObj.transform.SetParent(_finishGate.transform);
        bannerObj.transform.position = center + up * (radius * 0.5f);
        bannerObj.transform.rotation = Quaternion.LookRotation(-forward, up);

        // Load font for TextMesh visibility
        Font bannerFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bannerFont == null) bannerFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        TextMesh tm = bannerObj.AddComponent<TextMesh>();
        tm.text = "FINISH";
        tm.fontSize = 80;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.1f);
        tm.fontStyle = FontStyle.Bold;
        if (bannerFont != null) tm.font = bannerFont;
        // Ensure material is set for URP rendering
        MeshRenderer tmr = bannerObj.GetComponent<MeshRenderer>();
        if (tmr != null && bannerFont != null && bannerFont.material != null)
        {
            tmr.sharedMaterial = new Material(bannerFont.material);
            tmr.sharedMaterial.renderQueue = 3100;
        }

        // Second banner facing opposite direction
        GameObject banner2 = new GameObject("FinishBanner3D_Back");
        banner2.transform.SetParent(_finishGate.transform);
        banner2.transform.position = center + up * (radius * 0.5f);
        banner2.transform.rotation = Quaternion.LookRotation(forward, up);
        TextMesh tm2 = banner2.AddComponent<TextMesh>();
        tm2.text = "FINISH";
        tm2.fontSize = 80;
        tm2.characterSize = 0.08f;
        tm2.alignment = TextAlignment.Center;
        tm2.anchor = TextAnchor.MiddleCenter;
        tm2.color = new Color(1f, 0.85f, 0.1f);
        tm2.fontStyle = FontStyle.Bold;
        if (bannerFont != null) tm2.font = bannerFont;
        MeshRenderer tmr2 = banner2.GetComponent<MeshRenderer>();
        if (tmr2 != null && bannerFont != null && bannerFont.material != null)
        {
            tmr2.sharedMaterial = new Material(bannerFont.material);
            tmr2.sharedMaterial.renderQueue = 3100;
        }

        Debug.Log($"TTR: Spawned checkered finish gate at {raceDistance:F0}m");
    }

    // === CAMERA ORBIT ===

    void StartCameraOrbit()
    {
        if (playerController == null) return;
        _orbitingCamera = true;
        _orbitTimer = 0f;
        _orbitCenter = playerController.transform.position;
    }

    void UpdateOrbit()
    {
        if (!_orbitingCamera) return;

        _orbitTimer += Time.deltaTime;
        if (_orbitTimer > _orbitDuration + 2f) // extra 2s pause before podium
        {
            _orbitingCamera = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        // Orbit around the player's finish position
        float angle = (_orbitTimer / _orbitDuration) * 360f * Mathf.Deg2Rad;
        float height = 1.5f + Mathf.Sin(_orbitTimer * 0.5f) * 0.5f;
        Vector3 orbitPos = _orbitCenter + new Vector3(
            Mathf.Cos(angle) * _orbitRadius,
            height,
            Mathf.Sin(angle) * _orbitRadius
        );

        cam.transform.position = Vector3.Lerp(cam.transform.position, orbitPos, Time.deltaTime * 3f);
        cam.transform.LookAt(_orbitCenter + Vector3.up * 0.5f);

        // Disable PipeCamera during orbit
        PipeCamera pipeCam = cam.GetComponent<PipeCamera>();
        if (pipeCam != null && pipeCam.enabled)
            pipeCam.enabled = false;
    }

    void UpdateAutoFinish()
    {
        _autoFinishTimer += Time.deltaTime;
        if (_autoFinishTimer >= AUTO_FINISH_TIMEOUT)
        {
            // Force finish all remaining racers
            StartCoroutine(FinishRemainingAI());
            _state = State.Finished; // prevent re-entry
        }
    }

    /// <summary>Called when player crashes. They get last place among unfinished.</summary>
    public void OnPlayerCrashed()
    {
        if (_state != State.Racing) return;

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.isPlayer && !e.isFinished)
            {
                e.isFinished = true;
                e.finishPlace = 5; // last place (DNF)
                e.finishTime = Time.time - _raceStartTime;
                _entries[i] = e;
                break;
            }
        }

        StartCoroutine(FinishRemainingAI());
    }

    IEnumerator FinishRemainingAI()
    {
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (!e.isFinished)
            {
                e.isFinished = true;
                e.finishPlace = _nextFinishPlace++;
                e.finishTime = Time.time - _raceStartTime;
                if (e.ai != null)
                    e.ai.OnFinish(e.finishTime);
                _entries[i] = e;
                yield return new WaitForSeconds(0.3f);
            }
        }

        OnRaceComplete();
    }

    void CheckDistanceMilestones()
    {
        if (playerController == null) return;
        float dist = playerController.DistanceTraveled;

        // Milestones at 25%, 50%, 75% of race distance
        float[] thresholds = { 0.25f, 0.50f, 0.75f };
        string[] messages = { "QUARTER WAY!", "HALFWAY THERE!", "THREE QUARTERS!" };
        string[] cheerWords = { "NICE", "HYPE", "YEAH" };

        for (int i = 0; i < thresholds.Length; i++)
        {
            if (i <= _lastMilestoneIndex) continue;
            if (dist >= raceDistance * thresholds[i])
            {
                _lastMilestoneIndex = i;

                if (ScorePopup.Instance != null)
                    ScorePopup.Instance.Show(
                        messages[i],
                        playerController.transform.position + Vector3.up * 2f,
                        ScorePopup.PopupType.Milestone, 1.3f);

                if (PipeCamera.Instance != null)
                    PipeCamera.Instance.PunchFOV(2f);

                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer(messages[i], new Color(1f, 0.85f, 0.1f));

                HapticManager.LightTap();
                break; // only one per frame
            }
        }
    }

    void CheckFinalStretch()
    {
        if (_finalStretchTriggered || playerController == null) return;

        float playerDist = playerController.DistanceTraveled;
        float remaining = raceDistance - playerDist;

        if (remaining <= FINAL_STRETCH_DISTANCE && remaining > 0f)
        {
            _finalStretchTriggered = true;
            _finalStretchStart = Time.time;

            // "FINAL STRETCH!" announcement
            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(
                    playerController.transform.position + Vector3.up * 2.5f,
                    "FINAL STRETCH!");

            // Big camera shake + FOV punch
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.25f);
                PipeCamera.Instance.PunchFOV(5f);
            }

            // Golden flash
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();

            // Celebration audio
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();

            HapticManager.HeavyTap();
        }

        // Continuous final stretch intensity (heartbeat camera pulses)
        if (_finalStretchTriggered && remaining > 0f)
        {
            float elapsed = Time.time - _finalStretchStart;
            float urgency = 1f - Mathf.Clamp01(remaining / FINAL_STRETCH_DISTANCE);

            // Heartbeat camera pulse - gets faster as you approach
            float heartRate = Mathf.Lerp(2f, 5f, urgency);
            float pulse = Mathf.Sin(elapsed * heartRate * Mathf.PI * 2f);
            if (pulse > 0.9f && PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.03f + urgency * 0.05f);

            // Increasing vignette intensity
            if (ScreenEffects.Instance != null)
            {
                Color stretchColor = Color.Lerp(
                    new Color(1f, 0.85f, 0.1f), // gold
                    new Color(1f, 0.3f, 0.1f),   // urgent red-gold
                    urgency);
                ScreenEffects.Instance.UpdateZoneVignette(stretchColor, 0.3f + urgency * 0.5f);
            }
        }
    }

    void ShowPositionChangeArrow(bool gained)
    {
        _posChangeShowTime = Time.time;
        _posChangeUp = gained;
        if (_posChangeArrow != null)
        {
            _posChangeArrow.text = gained ? "\u25B2" : "\u25BC"; // ▲ or ▼
            _posChangeArrow.color = gained
                ? new Color(0.1f, 1f, 0.3f, 1f)   // bright green
                : new Color(1f, 0.3f, 0.15f, 1f);  // bright red
        }
    }

    void UpdatePositionHUD()
    {
        if (_positionHudText == null) return;

        int playerPos = GetPlayerPosition();
        string ordinal = GetOrdinal(playerPos);
        string posStr = playerPos + ordinal;

        // Show gap to racer ahead (motivating) or behind (pressure)
        float gapAhead = 0f, gapBehind = 0f;
        float playerDist = playerController != null ? playerController.DistanceTraveled : 0f;
        float playerSpeed = playerController != null ? Mathf.Max(playerController.CurrentSpeed, 1f) : 1f;
        foreach (var e in _entries)
        {
            if (e.isPlayer) continue;
            float gap = (e.distance - playerDist) / playerSpeed;
            if (gap > 0f && (gapAhead == 0f || gap < gapAhead)) gapAhead = gap;
            if (gap < 0f && (-gap < gapBehind || gapBehind == 0f)) gapBehind = -gap;
        }

        // Show relevant gap info
        if (playerPos == 1 && gapBehind > 0f && gapBehind < 5f)
            posStr += $"\n+{gapBehind:F1}s";
        else if (playerPos > 1 && gapAhead > 0f && gapAhead < 10f)
            posStr += $"\n-{gapAhead:F1}s";

        _positionHudText.text = posStr;

        // Color by position
        Color posColor;
        switch (playerPos)
        {
            case 1: posColor = new Color(1f, 0.85f, 0.1f); break;     // gold
            case 2: posColor = new Color(0.75f, 0.75f, 0.82f); break; // silver
            case 3: posColor = new Color(0.72f, 0.45f, 0.2f); break;  // bronze
            default: posColor = new Color(0.5f, 0.5f, 0.45f); break;  // gray
        }
        _positionHudText.color = posColor;

        // Punch animation on position change
        if (playerPos != _lastHudPosition && _lastHudPosition >= 0)
        {
            _positionHudPunchTime = Time.time;
            _lastHudPosition = playerPos;
        }
        if (_lastHudPosition < 0) _lastHudPosition = playerPos;

        // Elastic bounce on position change (0.6s, overshoot and settle)
        float since = Time.time - _positionHudPunchTime;
        if (since < 0.6f)
        {
            float t = since / 0.6f;
            float elastic = Mathf.Pow(2f, -8f * t) * Mathf.Sin((t - 0.1f) * Mathf.PI * 2f / 0.35f);
            float scale = 1f + elastic * 0.5f;
            _positionHudText.transform.localScale = Vector3.one * scale;
        }
        else
        {
            _positionHudText.transform.localScale = Vector3.one;
        }

        // 1st place golden shimmer
        if (playerPos == 1)
        {
            float shimmer = 0.85f + Mathf.Sin(Time.time * 4f) * 0.15f;
            _positionHudText.color = new Color(1f, shimmer, 0.1f);
        }

        // Pressure pulse: flash text when racer behind is closing in fast
        if (gapBehind > 0f && gapBehind < 1.5f && playerPos <= 3)
        {
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * 6f));
            Color flash = Color.Lerp(posColor, Color.white, pulse * 0.4f);
            _positionHudText.color = flash;
        }

        // Animate position change arrow (elastic bounce + slide + fade over 1.5s)
        if (_posChangeArrow != null)
        {
            float arrowElapsed = Time.time - _posChangeShowTime;
            if (arrowElapsed < 1.5f)
            {
                float t = arrowElapsed / 1.5f;

                // Elastic entry then smooth slide
                float slideDir = _posChangeUp ? -1f : 1f;
                float elasticPhase = Mathf.Min(t * 3f, 1f); // first 1/3 is elastic
                float elasticBounce = Mathf.Pow(2f, -6f * elasticPhase) * Mathf.Sin(elasticPhase * Mathf.PI * 3f);
                float slide = slideDir * (t * 25f + elasticBounce * 8f);
                _posChangeArrow.transform.localPosition = new Vector3(0f, slide, 0f);

                // Elastic scale pop at start
                float arrowScale;
                if (t < 0.2f)
                {
                    float st = t / 0.2f;
                    float se = Mathf.Pow(2f, -8f * st) * Mathf.Sin((st - 0.075f) * Mathf.PI * 2f / 0.3f);
                    arrowScale = 1f + se * 0.8f;
                }
                else arrowScale = 1f;
                _posChangeArrow.transform.localScale = Vector3.one * arrowScale;

                // Fade out over last 35%
                float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                Color c = _posChangeArrow.color;
                c.a = alpha;
                _posChangeArrow.color = c;
            }
            else
            {
                Color c = _posChangeArrow.color;
                c.a = 0f;
                _posChangeArrow.color = c;
            }
        }
    }

    /// <summary>Get player's current race position (1-based).</summary>
    public int GetPlayerPosition()
    {
        foreach (var e in _entries)
            if (e.isPlayer) return e.position;
        return 5;
    }

    /// <summary>Get player's finish place (0 if not finished).</summary>
    public int GetPlayerFinishPlace()
    {
        foreach (var e in _entries)
            if (e.isPlayer) return e.finishPlace;
        return 0;
    }

    /// <summary>Get the name of the racer who just passed the player.</summary>
    public string GetRacerNameAtPosition(int pos)
    {
        foreach (var e in _entries)
            if (e.position == pos) return e.name;
        return "";
    }

    static string GetOrdinal(int n)
    {
        if (n == 1) return "st";
        if (n == 2) return "nd";
        if (n == 3) return "rd";
        return "th";
    }
}
