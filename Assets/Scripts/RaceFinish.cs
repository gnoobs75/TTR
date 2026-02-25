using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Race finish UI — single unified panel with 3 auto-cycling tabs:
/// RESULTS (racer placements), STATS (player race stats), PODIUM (joke podium).
/// Replaces the old multi-panel + 3D podium system.
/// </summary>
public class RaceFinish : MonoBehaviour
{
    [Header("UI References")]
    public Canvas finishCanvas;
    public RectTransform bannerRoot;   // kept for compat, unused
    public RectTransform podiumRoot;   // kept for compat, unused

    // Internal state
    private bool _initialized;
    private bool _podiumShown;

    // Unified panel
    private RectTransform _panelRoot;
    private CanvasGroup _panelGroup;
    private Text _headerPlaceText;
    private Text _headerTimeText;

    // Tab bar
    private Image[] _tabButtonImages = new Image[3];
    private Text[] _tabButtonTexts = new Text[3];
    private Button[] _tabButtons = new Button[3];

    // Tab content pages
    private RectTransform[] _tabPages = new RectTransform[3];
    private CanvasGroup[] _tabPageGroups = new CanvasGroup[3];

    // Tab 0: Results
    private ResultRow[] _resultRows;
    private int _nextResultRow;
    private float _firstFinishTime;

    // Tab 1: Stats
    private Text[] _statValueTexts;
    private float[] _statTargetValues;
    private float[] _statCurrentValues;
    private bool _statsAnimating;
    private float _statsAnimTimer;

    // Tab 2: Podium
    private Text _podiumJokeTitle;
    private Text _podiumAnnouncementText;
    private Text[] _podiumNameTexts = new Text[3];
    private Image[] _podiumSwatches = new Image[3];

    // Tab cycling
    private int _activeTab = -1;
    private float _autoTabTimer;
    private const float AUTO_TAB_INTERVAL = 5f;
    private int _autoTabCount;
    private bool _tabSwitching;

    // Animation state
    private float _headerPulsePhase;
    private Color _placeBaseColor;

    struct ResultRow
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image bg;
        public Image colorSwatch;
        public Text placeText;
        public Text nameText;
        public Text timeText;
    }

    // Colors
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color SilverColor = new Color(0.75f, 0.75f, 0.82f);
    static readonly Color BronzeColor = new Color(0.72f, 0.45f, 0.2f);
    static readonly Color PanelBg = new Color(0.04f, 0.03f, 0.02f, 0.93f);
    static readonly Color GoldBorder = new Color(0.7f, 0.55f, 0.1f, 0.8f);
    static readonly Color TabActive = new Color(0.7f, 0.55f, 0.1f, 0.9f);
    static readonly Color TabInactive = new Color(0.15f, 0.12f, 0.08f, 0.8f);
    static readonly Color TabTextActive = new Color(1f, 0.92f, 0.3f);
    static readonly Color TabTextInactive = new Color(0.5f, 0.48f, 0.4f);

    void Awake()
    {
        if (finishCanvas == null)
            finishCanvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        if (!_initialized && finishCanvas != null)
        {
            // Clean up stale UI from editor-time Setup
            string[] staleNames = { "FinishBanner", "WinnersPoodium", "RaceResults",
                "RaceStats", "MenuButtons", "RaceFinishPanel" };
            foreach (string name in staleNames)
            {
                Transform stale = finishCanvas.transform.Find(name);
                if (stale != null) Destroy(stale.gameObject);
            }
            Initialize(finishCanvas);
        }
    }

    public void Initialize(Canvas canvas)
    {
        if (_initialized) return;
        _initialized = true;
        finishCanvas = canvas;

        CreateUnifiedPanel();

        // Hide panel initially
        _panelGroup.alpha = 0f;
        _panelRoot.gameObject.SetActive(false);
    }

    // ================================================================
    // PANEL CONSTRUCTION
    // ================================================================

    void CreateUnifiedPanel()
    {
        Font font = GetFont();

        // Main panel — large center with clean margins
        GameObject panelObj = new GameObject("RaceFinishPanel");
        _panelRoot = panelObj.AddComponent<RectTransform>();
        _panelRoot.SetParent(finishCanvas.transform, false);
        _panelRoot.anchorMin = new Vector2(0.12f, 0.08f);
        _panelRoot.anchorMax = new Vector2(0.88f, 0.92f);
        _panelRoot.offsetMin = Vector2.zero;
        _panelRoot.offsetMax = Vector2.zero;

        _panelGroup = panelObj.AddComponent<CanvasGroup>();

        // Dark background
        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = PanelBg;

        // Thin gold border via inner outline rect
        GameObject borderObj = new GameObject("GoldBorder");
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.SetParent(_panelRoot, false);
        borderRect.anchorMin = new Vector2(0.003f, 0.003f);
        borderRect.anchorMax = new Vector2(0.997f, 0.997f);
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 0); // transparent fill
        borderImg.raycastTarget = false;
        Outline borderOutline = borderObj.AddComponent<Outline>();
        borderOutline.effectColor = GoldBorder;
        borderOutline.effectDistance = new Vector2(2, -2);

        CreateHeader(font);
        CreateTabBar(font);
        CreateTabPages(font);
        CreateMenuButtons(font);
    }

    void CreateHeader(Font font)
    {
        GameObject headerObj = new GameObject("Header");
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.SetParent(_panelRoot, false);
        headerRect.anchorMin = new Vector2(0.03f, 0.88f);
        headerRect.anchorMax = new Vector2(0.97f, 0.97f);
        headerRect.offsetMin = Vector2.zero;
        headerRect.offsetMax = Vector2.zero;

        Image headerBg = headerObj.AddComponent<Image>();
        headerBg.color = new Color(0.08f, 0.06f, 0.03f, 0.6f);

        // Place text (left side)
        _headerPlaceText = CreateText(headerRect, "PlaceText",
            new Vector2(0.02f, 0f), new Vector2(0.68f, 1f),
            font, 26, FontStyle.Bold, TextAnchor.MiddleLeft, GoldColor, true);
        _headerPlaceText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Time text (right side)
        _headerTimeText = CreateText(headerRect, "TimeText",
            new Vector2(0.68f, 0f), new Vector2(0.98f, 1f),
            font, 20, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.8f, 0.8f, 0.75f), true);
    }

    void CreateTabBar(Font font)
    {
        GameObject tabBarObj = new GameObject("TabBar");
        RectTransform tabBarRect = tabBarObj.AddComponent<RectTransform>();
        tabBarRect.SetParent(_panelRoot, false);
        tabBarRect.anchorMin = new Vector2(0.03f, 0.82f);
        tabBarRect.anchorMax = new Vector2(0.97f, 0.87f);
        tabBarRect.offsetMin = Vector2.zero;
        tabBarRect.offsetMax = Vector2.zero;

        string[] tabNames = { "RESULTS", "STATS", "PODIUM" };
        for (int i = 0; i < 3; i++)
        {
            float xMin = i / 3f;
            float xMax = (i + 1) / 3f;

            GameObject tabObj = new GameObject($"Tab_{tabNames[i]}");
            RectTransform tabRect = tabObj.AddComponent<RectTransform>();
            tabRect.SetParent(tabBarRect, false);
            tabRect.anchorMin = new Vector2(xMin + 0.005f, 0f);
            tabRect.anchorMax = new Vector2(xMax - 0.005f, 1f);
            tabRect.offsetMin = Vector2.zero;
            tabRect.offsetMax = Vector2.zero;

            _tabButtonImages[i] = tabObj.AddComponent<Image>();
            _tabButtonImages[i].color = TabInactive;

            _tabButtons[i] = tabObj.AddComponent<Button>();
            int tabIndex = i;
            _tabButtons[i].onClick.AddListener(() => OnTabClicked(tabIndex));
            // Remove default button color transition (we handle it manually)
            var cb = _tabButtons[i].colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            _tabButtons[i].colors = cb;

            _tabButtonTexts[i] = CreateText(tabRect, "Label",
                Vector2.zero, Vector2.one,
                font, 14, FontStyle.Bold, TextAnchor.MiddleCenter, TabTextInactive, false);
            _tabButtonTexts[i].text = tabNames[i];
            _tabButtonTexts[i].raycastTarget = false;
        }
    }

    void CreateTabPages(Font font)
    {
        string[] pageNames = { "ResultsPage", "StatsPage", "PodiumPage" };
        for (int i = 0; i < 3; i++)
        {
            GameObject pageObj = new GameObject(pageNames[i]);
            _tabPages[i] = pageObj.AddComponent<RectTransform>();
            _tabPages[i].SetParent(_panelRoot, false);
            _tabPages[i].anchorMin = new Vector2(0.03f, 0.14f);
            _tabPages[i].anchorMax = new Vector2(0.97f, 0.81f);
            _tabPages[i].offsetMin = Vector2.zero;
            _tabPages[i].offsetMax = Vector2.zero;

            _tabPageGroups[i] = pageObj.AddComponent<CanvasGroup>();
            _tabPageGroups[i].alpha = 0f;
            pageObj.SetActive(false);
        }

        CreateResultsTab(font);
        CreateStatsTab(font);
        CreatePodiumTab(font);
    }

    // ---- Tab 0: RESULTS ----

    void CreateResultsTab(Font font)
    {
        RectTransform page = _tabPages[0];
        CreatePageTitle(page, font, "RACE RESULTS");

        _resultRows = new ResultRow[5];
        _nextResultRow = 0;
        for (int i = 0; i < 5; i++)
            _resultRows[i] = CreateResultRow(page, font, i);
    }

    ResultRow CreateResultRow(RectTransform parent, Font font, int index)
    {
        ResultRow row = new ResultRow();
        float rowHeight = 0.15f;
        float yTop = 0.85f - index * (rowHeight + 0.02f);

        GameObject rowObj = new GameObject($"ResultRow_{index}");
        row.root = rowObj.AddComponent<RectTransform>();
        row.root.SetParent(parent, false);
        row.root.anchorMin = new Vector2(0.02f, yTop - rowHeight);
        row.root.anchorMax = new Vector2(0.98f, yTop);
        row.root.offsetMin = Vector2.zero;
        row.root.offsetMax = Vector2.zero;

        row.group = rowObj.AddComponent<CanvasGroup>();
        row.group.alpha = 0f;

        row.bg = rowObj.AddComponent<Image>();
        row.bg.color = new Color(0.12f, 0.10f, 0.07f, 0.6f);

        // Color swatch (left edge)
        GameObject swatchObj = new GameObject("Swatch");
        RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
        swatchRect.SetParent(row.root, false);
        swatchRect.anchorMin = new Vector2(0f, 0.1f);
        swatchRect.anchorMax = new Vector2(0.025f, 0.9f);
        swatchRect.offsetMin = Vector2.zero;
        swatchRect.offsetMax = Vector2.zero;
        row.colorSwatch = swatchObj.AddComponent<Image>();
        row.colorSwatch.color = Color.white;

        // Place number
        row.placeText = CreateText(row.root, "Place",
            new Vector2(0.04f, 0f), new Vector2(0.15f, 1f),
            font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, false);

        // Racer name
        row.nameText = CreateText(row.root, "Name",
            new Vector2(0.17f, 0f), new Vector2(0.70f, 1f),
            font, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, false);
        row.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Time
        row.timeText = CreateText(row.root, "Time",
            new Vector2(0.72f, 0f), new Vector2(0.98f, 1f),
            font, 14, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.75f, 0.75f, 0.7f), false);

        return row;
    }

    // ---- Tab 1: STATS ----

    void CreateStatsTab(Font font)
    {
        RectTransform page = _tabPages[1];
        CreatePageTitle(page, font, "YOUR RACE STATS");

        string[] statLabels = {
            "Coins Collected", "Max Speed", "Hits Taken", "Boosts Used",
            "Near Misses", "Best Combo", "Stomps", "Score"
        };

        _statValueTexts = new Text[statLabels.Length];
        _statTargetValues = new float[statLabels.Length];
        _statCurrentValues = new float[statLabels.Length];
        float rowH = 0.09f;
        float startY = 0.84f;

        for (int i = 0; i < statLabels.Length; i++)
        {
            float yTop = startY - i * (rowH + 0.015f);

            // Alternating row bg
            GameObject rowBgObj = new GameObject($"StatRowBg_{i}");
            RectTransform rowBgRect = rowBgObj.AddComponent<RectTransform>();
            rowBgRect.SetParent(page, false);
            rowBgRect.anchorMin = new Vector2(0.02f, yTop - rowH);
            rowBgRect.anchorMax = new Vector2(0.98f, yTop);
            rowBgRect.offsetMin = Vector2.zero;
            rowBgRect.offsetMax = Vector2.zero;
            Image rowBgImg = rowBgObj.AddComponent<Image>();
            rowBgImg.color = i % 2 == 0
                ? new Color(0.08f, 0.06f, 0.04f, 0.3f)
                : new Color(0.06f, 0.05f, 0.03f, 0.15f);
            rowBgImg.raycastTarget = false;

            // Label (left)
            Text labelText = CreateText(page, $"StatLabel_{i}",
                new Vector2(0.05f, yTop - rowH), new Vector2(0.55f, yTop),
                font, 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Color(0.7f, 0.65f, 0.55f), false);
            labelText.text = statLabels[i];

            // Value (right)
            _statValueTexts[i] = CreateText(page, $"StatValue_{i}",
                new Vector2(0.55f, yTop - rowH), new Vector2(0.95f, yTop),
                font, 16, FontStyle.Bold, TextAnchor.MiddleRight, Color.white, false);
            _statValueTexts[i].text = "-";
        }
    }

    // ---- Tab 2: PODIUM ----

    void CreatePodiumTab(Font font)
    {
        RectTransform page = _tabPages[2];

        // Joke title
        _podiumJokeTitle = CreateText(page, "PodiumJokeTitle",
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.97f),
            font, 24, FontStyle.Bold, TextAnchor.MiddleCenter, GoldColor, true);
        _podiumJokeTitle.text = "YOU'RE NUMBER TWO!";
        _podiumJokeTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Announcement sub-text
        _podiumAnnouncementText = CreateText(page, "Announcement",
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.82f),
            font, 14, FontStyle.Italic, TextAnchor.MiddleCenter,
            new Color(0.75f, 0.7f, 0.6f), false);
        _podiumAnnouncementText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 2D Podium blocks: visual layout [2ND] [1ST] [3RD]
        // 2nd is TALLEST (poop joke)
        float[] blockHeights = { 0.55f, 0.38f, 0.22f }; // 2nd, 1st, 3rd
        float[] blockXMin = { 0.08f, 0.37f, 0.67f };
        float[] blockXMax = { 0.33f, 0.63f, 0.92f };
        Color[] blockColors = {
            new Color(SilverColor.r * 0.4f, SilverColor.g * 0.4f, SilverColor.b * 0.4f, 0.9f),
            new Color(GoldColor.r * 0.4f, GoldColor.g * 0.4f, GoldColor.b * 0.4f, 0.9f),
            new Color(BronzeColor.r * 0.4f, BronzeColor.g * 0.4f, BronzeColor.b * 0.4f, 0.9f)
        };
        Color[] labelColors = { SilverColor, GoldColor, BronzeColor };
        string[] placeLabels = { "2ND", "1ST", "3RD" };
        int[] dataMap = { 1, 0, 2 }; // visual→data: vis0=2nd(slot 1), vis1=1st(slot 0), vis2=3rd(slot 2)

        for (int vis = 0; vis < 3; vis++)
        {
            int slot = dataMap[vis];
            float baseY = 0.05f;

            // Pedestal block
            GameObject blockObj = new GameObject($"PodiumBlock_{placeLabels[vis]}");
            RectTransform blockRect = blockObj.AddComponent<RectTransform>();
            blockRect.SetParent(page, false);
            blockRect.anchorMin = new Vector2(blockXMin[vis], baseY);
            blockRect.anchorMax = new Vector2(blockXMax[vis], baseY + blockHeights[vis]);
            blockRect.offsetMin = Vector2.zero;
            blockRect.offsetMax = Vector2.zero;
            Image blockImg = blockObj.AddComponent<Image>();
            blockImg.color = blockColors[vis];

            // Place label on block
            Text plLabel = CreateText(blockRect, "PlaceLabel",
                new Vector2(0f, 0f), new Vector2(1f, 0.35f),
                font, 20, FontStyle.Bold, TextAnchor.MiddleCenter, labelColors[vis], true);
            plLabel.text = placeLabels[vis];

            // Color swatch
            GameObject swObj = new GameObject("Swatch");
            RectTransform swRect = swObj.AddComponent<RectTransform>();
            swRect.SetParent(blockRect, false);
            swRect.anchorMin = new Vector2(0.3f, 0.6f);
            swRect.anchorMax = new Vector2(0.7f, 0.75f);
            swRect.offsetMin = Vector2.zero;
            swRect.offsetMax = Vector2.zero;
            _podiumSwatches[slot] = swObj.AddComponent<Image>();
            _podiumSwatches[slot].color = Color.clear;

            // Racer name above block
            _podiumNameTexts[slot] = CreateText(blockRect, "RacerName",
                new Vector2(-0.1f, 1f), new Vector2(1.1f, 1.3f),
                font, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
            _podiumNameTexts[slot].text = "";
            _podiumNameTexts[slot].horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    // ---- Menu Buttons (inside panel) ----

    void CreateMenuButtons(Font font)
    {
        GameObject container = new GameObject("MenuButtons");
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.SetParent(_panelRoot, false);
        containerRect.anchorMin = new Vector2(0.05f, 0.02f);
        containerRect.anchorMax = new Vector2(0.95f, 0.12f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // "RACE AGAIN" (left)
        CreateFinishButton(containerRect, font, "RaceAgain", "RACE AGAIN",
            new Vector2(0.02f, 0.05f), new Vector2(0.48f, 0.95f),
            new Color(0.15f, 0.45f, 0.15f), () =>
            {
                Time.timeScale = 1f;
                if (GameManager.Instance != null)
                    GameManager.Instance.RestartGame();
            });

        // "MAIN MENU" (right)
        CreateFinishButton(containerRect, font, "MainMenu", "MAIN MENU",
            new Vector2(0.52f, 0.05f), new Vector2(0.98f, 0.95f),
            new Color(0.45f, 0.15f, 0.15f), () =>
            {
                Time.timeScale = 1f;
                if (GameManager.Instance != null)
                    GameManager.Instance.RestartGame();
            });
    }

    void CreateFinishButton(RectTransform parent, Font font, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f);
        colors.pressedColor = new Color(bgColor.r * 0.7f, bgColor.g * 0.7f, bgColor.b * 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        Text labelText = CreateText(btnRect, "Label",
            Vector2.zero, Vector2.one,
            font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
        labelText.text = label;
        labelText.raycastTarget = false;
    }

    // ================================================================
    // PUBLIC API
    // ================================================================

    /// <summary>Called when any racer finishes. Populates results rows (hidden until animated).</summary>
    public void OnRacerFinished(string racerName, Color racerColor, int place, float time, bool isPlayer)
    {
        if (!_initialized && finishCanvas != null)
            Initialize(finishCanvas);
        if (!_initialized) return;
        if (_nextResultRow >= _resultRows.Length) return;

        var row = _resultRows[_nextResultRow];

        Color placeColor = place == 1 ? GoldColor :
                           place == 2 ? SilverColor :
                           place == 3 ? BronzeColor : new Color(0.6f, 0.55f, 0.5f);

        row.placeText.text = $"{place}{GetOrdinal(place)}";
        row.placeText.color = placeColor;
        row.colorSwatch.color = racerColor;
        row.nameText.text = racerName;
        row.nameText.color = isPlayer ? GoldColor : Color.white;
        row.bg.color = isPlayer
            ? new Color(0.3f, 0.28f, 0.05f, 0.5f)
            : new Color(0.12f, 0.10f, 0.07f, 0.6f);

        if (place == 1)
        {
            row.timeText.text = $"{time:F1}s";
            _firstFinishTime = time;
        }
        else
        {
            row.timeText.text = $"+{time - _firstFinishTime:F1}s";
        }
        row.timeText.color = new Color(0.75f, 0.75f, 0.7f);

        // Row stays hidden (alpha=0) until animated in PodiumRevealSequence
        _nextResultRow++;
    }

    /// <summary>Called when the player crosses the finish line.</summary>
    public void OnPlayerFinished(int place, float time)
    {
        if (!_initialized && finishCanvas != null)
            Initialize(finishCanvas);
        if (!_initialized) return;

        string ordinal = GetOrdinal(place);
        Color placeColor = place == 1 ? GoldColor :
                           place == 2 ? SilverColor :
                           place == 3 ? BronzeColor : Color.white;
        _placeBaseColor = placeColor;

        string[] placeQuips = {
            "KING OF THE SEWER!",
            "THE ULTIMATE #2!",
            "BRONZE IS JUST FANCY RUST!",
            "YOU TRIED YOUR WORST!",
            "LAST PLACE, BEST SMELL!"
        };
        string quip = placeQuips[Mathf.Clamp(place - 1, 0, placeQuips.Length - 1)];
        _headerPlaceText.text = $"YOU FINISHED {place}{ordinal}! {quip}";
        _headerPlaceText.color = placeColor;
        _headerTimeText.text = $"{time:F1}s";

        // Celebration effects (same as before, minus CheerOverlay — panel is the celebration)
        Vector3 playerPos = RaceManager.Instance != null && RaceManager.Instance.PlayerController != null
            ? RaceManager.Instance.PlayerController.transform.position
            : transform.position;

        if (place == 1)
        {
            if (ProceduralAudio.Instance != null)
            {
                ProceduralAudio.Instance.PlayVictoryFanfare();
                ProceduralAudio.Instance.PlayCelebration();
            }
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.4f);
                PipeCamera.Instance.PunchFOV(10f);
            }
            if (ParticleManager.Instance != null)
            {
                ParticleManager.Instance.PlayCelebration(playerPos);
                ParticleManager.Instance.PlayFinishConfetti(playerPos);
            }
            HapticManager.HeavyTap();
        }
        else if (place <= 3)
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerPowerUpFlash();
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.2f);
            if (ParticleManager.Instance != null)
            {
                ParticleManager.Instance.PlayCelebration(playerPos);
                ParticleManager.Instance.PlayFinishConfetti(playerPos);
            }
            HapticManager.MediumTap();
        }
        else
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlaySadTrombone();
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerHitFlash(new Color(0.3f, 0.3f, 0.4f));
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.1f);
            HapticManager.MediumTap();
        }
    }

    /// <summary>Show the unified finish panel with all tabs. Called once all racers are done.</summary>
    public void ShowPodium(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized && finishCanvas != null)
            Initialize(finishCanvas);
        if (!_initialized || _podiumShown) return;
        _podiumShown = true;

        PopulateStats();
        PopulatePodiumTab(entries);
        StartCoroutine(PodiumRevealSequence());
    }

    // ================================================================
    // REVEAL SEQUENCE
    // ================================================================

    IEnumerator PodiumRevealSequence()
    {
        // Brief pause
        yield return new WaitForSecondsRealtime(0.5f);

        // Show + slide panel up from below
        _panelRoot.gameObject.SetActive(true);
        _panelGroup.alpha = 1f;

        Vector2 targetPos = _panelRoot.anchoredPosition;
        // Estimate panel height from screen fraction
        float screenH = Screen.height;
        float slideOffset = screenH * 0.8f;
        _panelRoot.anchoredPosition = targetPos + Vector2.down * slideOffset;

        float slideDuration = 0.4f;
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / slideDuration;
            float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            _panelRoot.anchoredPosition = Vector2.Lerp(
                targetPos + Vector2.down * slideOffset, targetPos, ease);
            yield return null;
        }
        _panelRoot.anchoredPosition = targetPos;

        // Show Results tab with staggered row animation
        SwitchTab(0, false);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return StartCoroutine(AnimateResultRows());

        // Celebration sound on results reveal
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCelebration();

        // Start auto-tab timer
        _autoTabTimer = AUTO_TAB_INTERVAL;
        _autoTabCount = 0;
    }

    IEnumerator AnimateResultRows()
    {
        for (int i = 0; i < _nextResultRow && i < _resultRows.Length; i++)
        {
            var row = _resultRows[i];
            float duration = 0.3f;
            float elapsed = 0f;

            Vector2 origPos = row.root.anchoredPosition;
            Vector2 startPos = origPos + Vector2.right * 50f;
            row.root.anchoredPosition = startPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                row.group.alpha = ease;
                row.root.anchoredPosition = Vector2.Lerp(startPos, origPos, ease);
                yield return null;
            }
            row.group.alpha = 1f;
            row.root.anchoredPosition = origPos;

            HapticManager.LightTap();
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }

    // ================================================================
    // TAB SWITCHING
    // ================================================================

    void OnTabClicked(int tabIndex)
    {
        if (_tabSwitching) return;
        _autoTabCount = 99; // stop auto-cycling on manual tap
        SwitchTab(tabIndex, true);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayUIClick();
        HapticManager.LightTap();
    }

    void SwitchTab(int tabIndex, bool animate)
    {
        if (tabIndex == _activeTab) return;

        if (animate && _activeTab >= 0)
            StartCoroutine(CrossfadeTabs(_activeTab, tabIndex));
        else
        {
            // Instant switch
            for (int i = 0; i < 3; i++)
            {
                _tabPages[i].gameObject.SetActive(i == tabIndex);
                _tabPageGroups[i].alpha = i == tabIndex ? 1f : 0f;
            }
        }

        _activeTab = tabIndex;
        UpdateTabBarVisuals();

        if (tabIndex == 1)
            StartStatCountUp();
    }

    IEnumerator CrossfadeTabs(int fromTab, int toTab)
    {
        _tabSwitching = true;
        float duration = 0.3f;
        float elapsed = 0f;

        _tabPages[toTab].gameObject.SetActive(true);
        _tabPageGroups[toTab].alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            _tabPageGroups[fromTab].alpha = 1f - t;
            _tabPageGroups[toTab].alpha = t;
            yield return null;
        }

        _tabPageGroups[fromTab].alpha = 0f;
        _tabPageGroups[toTab].alpha = 1f;
        _tabPages[fromTab].gameObject.SetActive(false);

        _tabSwitching = false;
    }

    void UpdateTabBarVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            bool active = (i == _activeTab);
            _tabButtonImages[i].color = active ? TabActive : TabInactive;
            _tabButtonTexts[i].color = active ? TabTextActive : TabTextInactive;
        }
    }

    // ================================================================
    // STAT COUNT-UP ANIMATION
    // ================================================================

    void StartStatCountUp()
    {
        _statsAnimating = true;
        _statsAnimTimer = 0f;
        for (int i = 0; i < _statCurrentValues.Length; i++)
            _statCurrentValues[i] = 0f;
    }

    void UpdateStatCountUp()
    {
        if (!_statsAnimating) return;
        _statsAnimTimer += Time.unscaledDeltaTime;

        float countUpDuration = 1.2f;
        float t = Mathf.Clamp01(_statsAnimTimer / countUpDuration);
        float ease = 1f - Mathf.Pow(1f - t, 3f);

        for (int i = 0; i < _statTargetValues.Length && i < _statValueTexts.Length; i++)
        {
            _statCurrentValues[i] = _statTargetValues[i] * ease;
            FormatStatDisplay(i);
        }

        if (t >= 1f)
            _statsAnimating = false;
    }

    void FormatStatDisplay(int index)
    {
        if (_statValueTexts == null || index >= _statValueTexts.Length) return;
        float val = _statCurrentValues[index];
        switch (index)
        {
            case 1: // Max Speed
                _statValueTexts[index].text = $"{val:F1} SMPH";
                break;
            case 7: // Score
                _statValueTexts[index].text = Mathf.RoundToInt(val).ToString("N0");
                break;
            default:
                _statValueTexts[index].text = Mathf.RoundToInt(val).ToString();
                break;
        }
    }

    // ================================================================
    // DATA POPULATION
    // ================================================================

    void PopulateStats()
    {
        if (_statValueTexts == null || _statValueTexts.Length < 8) return;
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        _statTargetValues[0] = gm.RunCoins;
        _statTargetValues[1] = gm.RunMaxSpeed;
        _statTargetValues[2] = gm.RunHitsTaken;
        _statTargetValues[3] = gm.RunBoostsUsed;
        _statTargetValues[4] = gm.RunNearMisses;
        _statTargetValues[5] = gm.RunBestCombo;
        _statTargetValues[6] = gm.RunStomps;
        _statTargetValues[7] = gm.score;

        // Color-coding
        _statValueTexts[0].color = GoldColor;

        float maxSpd = gm.RunMaxSpeed;
        _statValueTexts[1].color = maxSpd > 15f ? new Color(1f, 0.3f, 0.2f) :
                                   maxSpd > 10f ? new Color(1f, 0.7f, 0.2f) :
                                   new Color(0.3f, 1f, 0.4f);

        _statValueTexts[2].color = gm.RunHitsTaken == 0 ? new Color(0.3f, 1f, 0.4f) :
                                   gm.RunHitsTaken > 5 ? new Color(1f, 0.3f, 0.2f) : Color.white;

        _statValueTexts[3].color = new Color(0.2f, 0.9f, 1f);
        _statValueTexts[4].color = gm.RunNearMisses > 10 ? new Color(0.7f, 0.3f, 1f) : Color.white;
        _statValueTexts[5].color = gm.RunBestCombo >= 10 ? GoldColor : Color.white;
        _statValueTexts[6].color = gm.RunStomps > 0 ? new Color(0.3f, 1f, 0.4f) : Color.white;
        _statValueTexts[7].color = GoldColor;

        for (int i = 0; i < _statValueTexts.Length; i++)
            _statValueTexts[i].text = "-";
    }

    void PopulatePodiumTab(List<RaceManager.RacerEntry> entries)
    {
        var sorted = new List<RaceManager.RacerEntry>(entries);
        sorted.Sort((a, b) => a.finishPlace.CompareTo(b.finishPlace));

        int playerPlace = 0;
        foreach (var e in sorted)
            if (e.isPlayer) { playerPlace = e.finishPlace; break; }

        // Comedy title
        _podiumJokeTitle.text = playerPlace == 2 ? "YOU'RE NUMBER TWO!"
            : playerPlace == 1 ? "YOU'RE NUMBER ONE!\n...BUT TWO IS TALLER!"
            : $"YOU'RE NUMBER {playerPlace}!";

        string[] announcements = {
            "Winner winner chicken dinner! But look at that #2 pedestal...",
            "The Ultimate #2! The pedestal of champions... of poop.",
            "Bronze is just fancy rust. But hey, you made it!",
            "At least you showed up. That counts for something. Maybe.",
            "Dead last but hey... everyone poops."
        };
        _podiumAnnouncementText.text = announcements[Mathf.Clamp(playerPlace - 1, 0, announcements.Length - 1)];

        // Populate podium names + swatches (top 3)
        for (int i = 0; i < 3 && i < sorted.Count; i++)
        {
            var entry = sorted[i];
            _podiumNameTexts[i].text = entry.name;
            _podiumNameTexts[i].color = entry.isPlayer ? GoldColor : Color.white;
            _podiumSwatches[i].color = entry.color;
        }
    }

    // ================================================================
    // UPDATE
    // ================================================================

    void Update()
    {
        if (!_podiumShown || _panelGroup == null) return;

        // Auto-tab cycling (stops after reaching PODIUM)
        if (_autoTabCount < 2 && _activeTab >= 0 && !_tabSwitching)
        {
            _autoTabTimer -= Time.unscaledDeltaTime;
            if (_autoTabTimer <= 0f)
            {
                _autoTabTimer = AUTO_TAB_INTERVAL;
                int nextTab = _activeTab + 1;
                if (nextTab < 3)
                {
                    SwitchTab(nextTab, true);
                    _autoTabCount++;
                }
            }
        }

        // Stat count-up
        UpdateStatCountUp();

        // Header gold pulse
        if (_headerPlaceText != null)
        {
            _headerPulsePhase += Time.unscaledDeltaTime;
            float shimmer = Mathf.Sin(_headerPulsePhase * 3f) * 0.15f;
            _headerPlaceText.color = new Color(
                Mathf.Clamp01(_placeBaseColor.r + shimmer),
                Mathf.Clamp01(_placeBaseColor.g + shimmer * 0.5f),
                Mathf.Clamp01(_placeBaseColor.b),
                1f);
        }

        // Podium joke title pulse (only when on podium tab)
        if (_podiumJokeTitle != null && _activeTab == 2)
        {
            float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 2.5f) * 0.2f;
            _podiumJokeTitle.color = new Color(GoldColor.r * pulse, GoldColor.g * pulse, GoldColor.b * 0.1f);
        }
    }

    // ================================================================
    // RESET
    // ================================================================

    public void Reset()
    {
        if (!_initialized) return;
        _podiumShown = false;
        _activeTab = -1;
        _autoTabCount = 0;
        _statsAnimating = false;
        _nextResultRow = 0;
        _firstFinishTime = 0f;

        if (_panelRoot != null)
        {
            _panelGroup.alpha = 0f;
            _panelRoot.gameObject.SetActive(false);
        }

        if (_resultRows != null)
        {
            for (int i = 0; i < _resultRows.Length; i++)
                if (_resultRows[i].group != null)
                    _resultRows[i].group.alpha = 0f;
        }

        for (int i = 0; i < 3; i++)
        {
            if (_tabPages[i] != null)
            {
                _tabPageGroups[i].alpha = 0f;
                _tabPages[i].gameObject.SetActive(false);
            }
        }

        if (_statValueTexts != null)
            for (int i = 0; i < _statValueTexts.Length; i++)
                if (_statValueTexts[i] != null)
                    _statValueTexts[i].text = "-";

        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StopFinishConfetti();
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>Create a Text element with standard setup.</summary>
    Text CreateText(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Font font, int fontSize, FontStyle style, TextAnchor alignment,
        Color color, bool outline)
    {
        GameObject obj = new GameObject(name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;

        if (outline)
        {
            Outline ol = obj.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.9f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return text;
    }

    void CreatePageTitle(RectTransform page, Font font, string title)
    {
        Text t = CreateText(page, "PageTitle",
            new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.99f),
            font, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.6f, 0.55f, 0.45f), false);
        t.text = title;
    }

    static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    static string GetOrdinal(int n)
    {
        if (n == 1) return "ST";
        if (n == 2) return "ND";
        if (n == 3) return "RD";
        return "TH";
    }
}
