using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game UI - HUD, start screen, game over screen, and shop.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("HUD")]
    public Text scoreText;
    public Text distanceText;
    public Text comboText;
    public Text walletText;
    public Text multiplierText;
    public Text coinCountText;
    public Text speedText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public Text finalScoreText;
    public Text highScoreText;
    public Text runStatsText;
    public Button restartButton;

    [Header("Start Screen")]
    public GameObject startPanel;
    public Button startButton;
    public Text challengeText;
    public Text startWalletText;
    public Button shopButton;

    [Header("Gallery")]
    public Button galleryButton;

    [Header("Sewer Tour")]
    public Button tourButton;

    [Header("Shop Panel")]
    public GameObject shopPanel;
    public Transform shopContent;
    public Button shopCloseButton;

    // Mobile-responsive UI scale factor
    private float _uiScale = 1f;

    void Awake()
    {
        // DPI-based scale for mobile devices (reference: 160 DPI desktop)
        float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
        _uiScale = Mathf.Clamp(dpi / 160f, 1f, 2.5f);
#if UNITY_IOS || UNITY_ANDROID
        _uiScale = Mathf.Max(_uiScale, 1.4f); // minimum 1.4x on mobile for readability
#endif
    }

    void Start()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
            _gameOverGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (_gameOverGroup == null) _gameOverGroup = gameOverPanel.AddComponent<CanvasGroup>();
        }

        if (startPanel != null)
            startPanel.SetActive(true);

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
            _shopCanvasGroup = shopPanel.GetComponent<CanvasGroup>();
            if (_shopCanvasGroup == null) _shopCanvasGroup = shopPanel.AddComponent<CanvasGroup>();
        }

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (shopButton != null)
            shopButton.onClick.AddListener(OnShopClicked);

        if (shopCloseButton != null)
            shopCloseButton.onClick.AddListener(OnShopCloseClicked);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OnGalleryClicked);

        if (tourButton != null)
            tourButton.onClick.AddListener(OnTourClicked);

        UpdateWallet();
        BuildShopItems();

        // Setup start screen fade-out group
        if (startPanel != null)
        {
            _startCanvasGroup = startPanel.GetComponent<CanvasGroup>();
            if (_startCanvasGroup == null)
                _startCanvasGroup = startPanel.AddComponent<CanvasGroup>();
            _quipIndex = Random.Range(0, StartQuips.Length);
            _nextQuipTime = Time.time + 3.5f;
        }
    }

    void OnStartClicked()
    {
        if (_startScreenFading) return; // prevent double-tap
        HapticManager.MediumTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        // Start smooth fade-out then launch game
        _startScreenFading = true;
        _startFadeTimer = 0f;
    }

    void OnGalleryClicked()
    {
        if (startPanel != null)
            startPanel.SetActive(false);
        if (AssetGallery.Instance != null)
            AssetGallery.Instance.Open();
    }

    void OnTourClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartSewerTour();
    }

    void OnShopClicked()
    {
        HapticManager.LightTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            if (_shopCanvasGroup != null) _shopCanvasGroup.alpha = 0f;
            shopPanel.transform.localScale = Vector3.one * 0.92f;
            _shopFadingIn = true;
            _shopFadingOut = false;
            _shopFadeTimer = 0f;
        }
        if (startPanel != null)
            startPanel.SetActive(false);
        UpdateWallet();
        RefreshShopItems();
    }

    void OnShopCloseClicked()
    {
        HapticManager.LightTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        _shopFadingOut = true;
        _shopFadingIn = false;
        _shopFadeTimer = 0f;
        UpdateWallet();
    }

    public void ShowHUD()
    {
        if (startPanel != null)
            startPanel.SetActive(false);
        if (shopPanel != null)
            shopPanel.SetActive(false);
        UpdateWallet();
    }

    private int _displayedScore;
    private float _scorePunchTime;
    private float _distPunchTime;
    private int _displayedCoins;
    private float _coinPunchTime;

    // Game over animation
    private float _gameOverShowTime;
    private bool _gameOverAnimating;
    private bool _isNewHighScore;
    private float _newHighScorePhase;
    private CanvasGroup _gameOverGroup;

    // Shop panel animation
    private CanvasGroup _shopCanvasGroup;
    private float _shopFadeTimer;
    private bool _shopFadingIn;
    private bool _shopFadingOut;

    // Start screen animations
    private float _startPulsePhase;
    private float _startScreenTimer; // time on start screen (for staggered anims)
    private bool _startScreenFading; // smooth exit transition
    private float _startFadeTimer;
    private CanvasGroup _startCanvasGroup;
    private int _quipIndex;
    private float _nextQuipTime;

    // Boost timer bar
    private RectTransform _boostBarBg;
    private RectTransform _boostBarFill;
    private Image _boostFillImage;
    private Text _boostLabel;
    private bool _boostBarCreated;

    // Stun recovery indicator
    private RectTransform _stunIndicatorBg;
    private Image _stunFillImage;
    private Text _stunLabel;
    private bool _stunIndicatorCreated;

    // Coin magnet indicator
    private Text _magnetIndicator;
    private bool _magnetIndicatorCreated;

    // Race position indicator
    private Text _racePositionText;
    private bool _racePositionCreated;
    private int _lastRacePosition;
    private float _racePosChangeTime;

    // Fork preview arrows
    private RectTransform _forkArrowRoot;
    private CanvasGroup _forkArrowGroup;
    private Text _forkLeftLabel;
    private Text _forkRightLabel;
    private RectTransform _forkLeftArrow;
    private RectTransform _forkRightArrow;
    private bool _forkArrowsCreated;
    private bool _forkArrowsShown; // one-shot: fires effects when arrows first appear

    private static readonly string[] StartQuips = {
        "\"Abandon hope, all ye who flush\"",
        "\"It's a dirty job, but someone's gotta race it\"",
        "\"May the flush be with you\"",
        "\"Sewer speed record: still unclaimed\"",
        "\"Warning: Contains actual gameplay\"",
        "\"Rated P for Poop\"",
        "\"No turds were harmed in the making\"",
        "\"Your toilet called. It wants its turd back.\"",
        "\"Built different. Flushed the same.\"",
        "\"The #2 racing game of all time\"",
    };

    public void UpdateCoinCount(int coins)
    {
        if (coinCountText == null) return;
        coinCountText.text = coins.ToString();
        if (coins > _displayedCoins && _displayedCoins >= 0)
            _coinPunchTime = Time.time;
        _displayedCoins = coins;
    }

    public void UpdateScore(int score)
    {
        if (scoreText == null) return;
        scoreText.text = score.ToString("N0");

        // Punch-scale when score increases
        if (score > _displayedScore && _displayedScore > 0)
        {
            _scorePunchTime = Time.time;
        }
        _displayedScore = score;
    }

    public void UpdateDistance(float meters)
    {
        if (distanceText == null) return;
        int m = Mathf.FloorToInt(meters);
        string prev = distanceText.text;
        distanceText.text = m + "m";

        // Pulse every 50m
        if (m > 0 && m % 50 == 0 && prev != distanceText.text)
            _distPunchTime = Time.time;
    }

    public void UpdateSpeed(float speed)
    {
        if (speedText == null) return;
        int mph = Mathf.RoundToInt(speed * 3.6f); // arbitrary "sewer mph"
        speedText.text = mph + " SMPH";
        // Color: green at low, yellow mid, red high
        float t = Mathf.Clamp01((speed - 5f) / 15f);
        speedText.color = Color.Lerp(
            new Color(0.3f, 1f, 0.4f),
            new Color(1f, 0.3f, 0.15f), t);

        // Notify screen effects
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.UpdateSpeed(speed);

        // Update boost timer bar
        UpdateBoostBar();

        // Update stun recovery indicator
        UpdateStunIndicator();

        // Update coin magnet indicator
        UpdateMagnetIndicator();

        // Update fork preview arrows
        UpdateForkArrows();

        // Update race position
        UpdateRacePosition();
    }

    void CreateBoostBar()
    {
        if (_boostBarCreated) return;
        _boostBarCreated = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null && ScreenEffects.Instance != null)
            canvas = ScreenEffects.Instance.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Boost bar background (bottom-center, above controls)
        GameObject bgObj = new GameObject("BoostBarBg");
        _boostBarBg = bgObj.AddComponent<RectTransform>();
        _boostBarBg.SetParent(canvas.transform, false);
        _boostBarBg.anchorMin = new Vector2(0.25f, 0.06f);
        _boostBarBg.anchorMax = new Vector2(0.75f, 0.085f);
        _boostBarBg.offsetMin = Vector2.zero;
        _boostBarBg.offsetMax = Vector2.zero;

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        // Fill bar
        GameObject fillObj = new GameObject("BoostFill");
        _boostBarFill = fillObj.AddComponent<RectTransform>();
        _boostBarFill.SetParent(_boostBarBg, false);
        _boostBarFill.anchorMin = Vector2.zero;
        _boostBarFill.anchorMax = Vector2.one;
        _boostBarFill.offsetMin = Vector2.zero;
        _boostBarFill.offsetMax = Vector2.zero;

        _boostFillImage = fillObj.AddComponent<Image>();
        _boostFillImage.color = new Color(0f, 0.9f, 1f, 0.8f);

        // Label
        GameObject labelObj = new GameObject("BoostLabel");
        RectTransform lrt = labelObj.AddComponent<RectTransform>();
        lrt.SetParent(_boostBarBg, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        _boostLabel = labelObj.AddComponent<Text>();
        _boostLabel.font = font;
        _boostLabel.fontSize = 12;
        _boostLabel.alignment = TextAnchor.MiddleCenter;
        _boostLabel.color = Color.white;
        _boostLabel.text = "BOOST";
        _boostLabel.fontStyle = FontStyle.Bold;

        Outline lo = labelObj.AddComponent<Outline>();
        lo.effectColor = new Color(0, 0, 0, 0.8f);
        lo.effectDistance = new Vector2(1, -1);

        bgObj.SetActive(false);
    }

    void UpdateBoostBar()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;
        TurdController tc = GameManager.Instance.player;
        if (!tc.IsBoosting)
        {
            if (_boostBarBg != null && _boostBarBg.gameObject.activeSelf)
                _boostBarBg.gameObject.SetActive(false);
            return;
        }

        if (!_boostBarCreated) CreateBoostBar();
        if (_boostBarBg == null) return;

        _boostBarBg.gameObject.SetActive(true);
        float pct = tc.BoostTimeRemaining / Mathf.Max(0.01f, tc.BoostDuration);
        _boostBarFill.anchorMax = new Vector2(pct, 1f);

        // Color: cyan at full -> orange at low -> red flashing at very low
        Color barColor;
        if (pct > 0.5f)
            barColor = Color.Lerp(new Color(1f, 0.6f, 0f), new Color(0f, 0.9f, 1f), (pct - 0.5f) * 2f);
        else if (pct > 0.2f)
            barColor = Color.Lerp(new Color(1f, 0.3f, 0.1f), new Color(1f, 0.6f, 0f), (pct - 0.2f) / 0.3f);
        else
        {
            barColor = new Color(1f, 0.2f, 0.1f);
            // Flash warning when almost out
            float flash = Mathf.Abs(Mathf.Sin(Time.time * 8f));
            barColor = Color.Lerp(barColor, Color.white, flash * 0.3f);
        }
        _boostFillImage.color = barColor;
    }

    void CreateStunIndicator()
    {
        if (_stunIndicatorCreated) return;
        _stunIndicatorCreated = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Small pill-shaped bar above boost bar position
        GameObject bgObj = new GameObject("StunIndicatorBg");
        _stunIndicatorBg = bgObj.AddComponent<RectTransform>();
        _stunIndicatorBg.SetParent(canvas.transform, false);
        _stunIndicatorBg.anchorMin = new Vector2(0.30f, 0.09f);
        _stunIndicatorBg.anchorMax = new Vector2(0.70f, 0.105f);
        _stunIndicatorBg.offsetMin = Vector2.zero;
        _stunIndicatorBg.offsetMax = Vector2.zero;

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.05f, 0.05f, 0.7f);

        // Fill
        GameObject fillObj = new GameObject("StunFill");
        RectTransform fillRT = fillObj.AddComponent<RectTransform>();
        fillRT.SetParent(_stunIndicatorBg, false);
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        _stunFillImage = fillObj.AddComponent<Image>();
        _stunFillImage.color = new Color(1f, 0.2f, 0.1f);

        // Label
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        GameObject labelObj = new GameObject("StunLabel");
        RectTransform lrt = labelObj.AddComponent<RectTransform>();
        lrt.SetParent(_stunIndicatorBg, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        _stunLabel = labelObj.AddComponent<Text>();
        _stunLabel.font = font;
        _stunLabel.fontSize = 10;
        _stunLabel.alignment = TextAnchor.MiddleCenter;
        _stunLabel.color = Color.white;
        _stunLabel.fontStyle = FontStyle.Bold;

        Outline lo = labelObj.AddComponent<Outline>();
        lo.effectColor = new Color(0, 0, 0, 0.8f);
        lo.effectDistance = new Vector2(1, -1);

        bgObj.SetActive(false);
    }

    void UpdateStunIndicator()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;
        TurdController tc = GameManager.Instance.player;
        var state = tc.CurrentHitState;

        if (state == TurdController.HitState.Normal)
        {
            if (_stunIndicatorBg != null && _stunIndicatorBg.gameObject.activeSelf)
                _stunIndicatorBg.gameObject.SetActive(false);
            return;
        }

        if (!_stunIndicatorCreated) CreateStunIndicator();
        if (_stunIndicatorBg == null) return;

        _stunIndicatorBg.gameObject.SetActive(true);
        float progress = tc.HitPhaseProgress;

        // Color and label based on phase
        Color fillColor;
        string label;
        if (state == TurdController.HitState.Stunned)
        {
            fillColor = Color.Lerp(new Color(1f, 0.15f, 0.05f), new Color(1f, 0.4f, 0.1f), progress);
            label = "STUNNED";
            // Pulse for urgency
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * 6f));
            fillColor = Color.Lerp(fillColor, Color.white, pulse * 0.15f);
        }
        else if (state == TurdController.HitState.Recovering)
        {
            fillColor = Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.9f, 0.2f), progress);
            label = "RECOVERING";
        }
        else // Invincible
        {
            fillColor = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(0.3f, 1f, 0.4f), progress);
            label = "PROTECTED";
        }

        _stunFillImage.color = fillColor;
        // Fill bar shows time remaining (drains left to right)
        RectTransform fillRT = _stunFillImage.GetComponent<RectTransform>();
        fillRT.anchorMax = new Vector2(1f - progress, 1f);

        _stunLabel.text = label;
    }

    void CreateMagnetIndicator()
    {
        if (_magnetIndicatorCreated) return;
        _magnetIndicatorCreated = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Small golden label above the coin count area
        GameObject obj = new GameObject("MagnetIndicator");
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.78f, 0.90f);
        rt.anchorMax = new Vector2(0.98f, 0.95f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _magnetIndicator = obj.AddComponent<Text>();
        _magnetIndicator.font = font;
        _magnetIndicator.fontSize = Mathf.RoundToInt(14 * _uiScale);
        _magnetIndicator.alignment = TextAnchor.MiddleCenter;
        _magnetIndicator.fontStyle = FontStyle.Bold;
        _magnetIndicator.color = new Color(1f, 0.85f, 0.2f);
        _magnetIndicator.text = "MAGNET";

        Outline ol = obj.AddComponent<Outline>();
        ol.effectColor = new Color(0.4f, 0.2f, 0f, 0.9f);
        ol.effectDistance = new Vector2(1, -1);

        obj.SetActive(false);
    }

    void UpdateMagnetIndicator()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;
        TurdController tc = GameManager.Instance.player;

        if (!tc.IsMagnetActive)
        {
            if (_magnetIndicator != null && _magnetIndicator.gameObject.activeSelf)
                _magnetIndicator.gameObject.SetActive(false);
            return;
        }

        if (!_magnetIndicatorCreated) CreateMagnetIndicator();
        if (_magnetIndicator == null) return;

        _magnetIndicator.gameObject.SetActive(true);

        // Pulsing golden glow
        float pulse = 0.7f + Mathf.Sin(Time.time * 5f) * 0.3f;
        _magnetIndicator.color = new Color(1f, 0.85f, 0.2f, pulse);

        // Gentle scale breathing
        float scale = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
        _magnetIndicator.transform.localScale = Vector3.one * scale;
    }

    void CreateForkArrows()
    {
        if (_forkArrowsCreated) return;
        _forkArrowsCreated = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Root container (center of screen, lower third)
        GameObject rootObj = new GameObject("ForkArrows");
        _forkArrowRoot = rootObj.AddComponent<RectTransform>();
        _forkArrowRoot.SetParent(canvas.transform, false);
        _forkArrowRoot.anchorMin = new Vector2(0.1f, 0.35f);
        _forkArrowRoot.anchorMax = new Vector2(0.9f, 0.55f);
        _forkArrowRoot.offsetMin = Vector2.zero;
        _forkArrowRoot.offsetMax = Vector2.zero;

        _forkArrowGroup = rootObj.AddComponent<CanvasGroup>();
        _forkArrowGroup.alpha = 0f;

        // Left arrow (SAFE - green)
        GameObject leftObj = new GameObject("LeftArrow");
        _forkLeftArrow = leftObj.AddComponent<RectTransform>();
        _forkLeftArrow.SetParent(_forkArrowRoot, false);
        _forkLeftArrow.anchorMin = new Vector2(0f, 0f);
        _forkLeftArrow.anchorMax = new Vector2(0.4f, 1f);
        _forkLeftArrow.offsetMin = Vector2.zero;
        _forkLeftArrow.offsetMax = Vector2.zero;

        // Arrow shape background
        Image leftBg = leftObj.AddComponent<Image>();
        leftBg.color = new Color(0.15f, 0.5f, 0.2f, 0.6f);

        _forkLeftLabel = CreateArrowLabel(leftObj.transform, font,
            "\u25C0 SAFE", new Color(0.4f, 1f, 0.5f));

        // Right arrow (RISKY - red)
        GameObject rightObj = new GameObject("RightArrow");
        _forkRightArrow = rightObj.AddComponent<RectTransform>();
        _forkRightArrow.SetParent(_forkArrowRoot, false);
        _forkRightArrow.anchorMin = new Vector2(0.6f, 0f);
        _forkRightArrow.anchorMax = new Vector2(1f, 1f);
        _forkRightArrow.offsetMin = Vector2.zero;
        _forkRightArrow.offsetMax = Vector2.zero;

        Image rightBg = rightObj.AddComponent<Image>();
        rightBg.color = new Color(0.5f, 0.15f, 0.1f, 0.6f);

        _forkRightLabel = CreateArrowLabel(rightObj.transform, font,
            "RISKY \u25B6", new Color(1f, 0.4f, 0.3f));

        // Center "FORK!" label
        GameObject centerObj = new GameObject("ForkCenter");
        RectTransform crt = centerObj.AddComponent<RectTransform>();
        crt.SetParent(_forkArrowRoot, false);
        crt.anchorMin = new Vector2(0.35f, 0.1f);
        crt.anchorMax = new Vector2(0.65f, 0.9f);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        Text centerText = centerObj.AddComponent<Text>();
        centerText.font = font;
        centerText.fontSize = Mathf.RoundToInt(18 * _uiScale);
        centerText.alignment = TextAnchor.MiddleCenter;
        centerText.fontStyle = FontStyle.Bold;
        centerText.color = new Color(1f, 0.9f, 0.3f);
        centerText.text = "FORK!";

        Outline co = centerObj.AddComponent<Outline>();
        co.effectColor = new Color(0, 0, 0, 0.9f);
        co.effectDistance = new Vector2(1, -1);

        rootObj.SetActive(false);
    }

    Text CreateArrowLabel(Transform parent, Font font, string text, Color color)
    {
        GameObject obj = new GameObject("Label");
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = Mathf.RoundToInt(16 * _uiScale);
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.text = text;

        Outline ol = obj.AddComponent<Outline>();
        ol.effectColor = new Color(0, 0, 0, 0.9f);
        ol.effectDistance = new Vector2(1, -1);

        return t;
    }

    void UpdateForkArrows()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null)
        {
            HideForkArrows();
            return;
        }

        float dist = GameManager.Instance.distanceTraveled;
        PipeFork nearestFork = null;
        float nearestDist = float.MaxValue;

        foreach (var fork in PipeFork.ActiveForks)
        {
            if (fork == null) continue;
            float ahead = fork.forkDistance - dist;
            // Show arrows when 5-45m ahead of fork
            if (ahead > 5f && ahead < 45f && ahead < nearestDist)
            {
                nearestDist = ahead;
                nearestFork = fork;
            }
        }

        if (nearestFork == null)
        {
            HideForkArrows();
            _forkArrowsShown = false;
            return;
        }

        if (!_forkArrowsCreated) CreateForkArrows();
        if (_forkArrowRoot == null) return;

        _forkArrowRoot.gameObject.SetActive(true);

        // One-shot dramatic entrance when arrows first appear
        if (!_forkArrowsShown)
        {
            _forkArrowsShown = true;
            // Camera rumble: choice is coming!
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.15f);
            // Haptic nudge to draw attention
            HapticManager.MediumTap();
            // Poop crew reacts
            if (CheerOverlay.Instance != null)
                CheerOverlay.Instance.ShowCheer("FORK", Color.yellow, false);
        }

        // Fade in as player approaches (full at 15m, faint at 45m)
        float fadeT = Mathf.Clamp01((45f - nearestDist) / 30f);
        _forkArrowGroup.alpha = fadeT * (0.7f + Mathf.Sin(Time.time * 3f) * 0.3f);

        // Arrows bob with urgency that increases as fork approaches
        float urgency = Mathf.Lerp(4f, 7f, fadeT);
        float amp = Mathf.Lerp(8f, 14f, fadeT);
        float leftBob = Mathf.Sin(Time.time * urgency) * amp;
        float rightBob = Mathf.Sin(Time.time * urgency + Mathf.PI) * amp;
        _forkLeftArrow.localPosition = new Vector3(leftBob, 0f, 0f);
        _forkRightArrow.localPosition = new Vector3(rightBob, 0f, 0f);
    }

    void HideForkArrows()
    {
        if (_forkArrowRoot != null && _forkArrowRoot.gameObject.activeSelf)
            _forkArrowRoot.gameObject.SetActive(false);
    }

    void CreateRacePosition()
    {
        if (_racePositionCreated) return;
        _racePositionCreated = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Large position indicator top-left
        GameObject obj = new GameObject("RacePosition");
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.02f, 0.85f);
        rt.anchorMax = new Vector2(0.18f, 0.95f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _racePositionText = obj.AddComponent<Text>();
        _racePositionText.font = font;
        _racePositionText.fontSize = Mathf.RoundToInt(28 * _uiScale);
        _racePositionText.alignment = TextAnchor.MiddleLeft;
        _racePositionText.fontStyle = FontStyle.Bold;
        _racePositionText.color = Color.white;

        Outline ol = obj.AddComponent<Outline>();
        ol.effectColor = new Color(0, 0, 0, 0.9f);
        ol.effectDistance = new Vector2(2, -2);

        obj.SetActive(false);
    }

    void UpdateRacePosition()
    {
        if (RaceManager.Instance == null ||
            (RaceManager.Instance.RaceState != RaceManager.State.Racing &&
             RaceManager.Instance.RaceState != RaceManager.State.PlayerFinished))
        {
            if (_racePositionText != null && _racePositionText.gameObject.activeSelf)
                _racePositionText.gameObject.SetActive(false);
            return;
        }

        if (!_racePositionCreated) CreateRacePosition();
        if (_racePositionText == null) return;

        _racePositionText.gameObject.SetActive(true);

        int pos = RaceManager.Instance.GetPlayerPosition();
        string ordinal = pos == 1 ? "ST" : pos == 2 ? "ND" : pos == 3 ? "RD" : "TH";
        _racePositionText.text = $"{pos}{ordinal} / 5";

        // Color by position
        Color posColor;
        if (pos == 1) posColor = new Color(1f, 0.85f, 0.2f);      // gold
        else if (pos == 2) posColor = new Color(0.8f, 0.8f, 0.9f); // silver
        else if (pos == 3) posColor = new Color(0.8f, 0.55f, 0.3f);// bronze
        else posColor = new Color(0.7f, 0.3f, 0.3f);               // red-ish

        // Punch animation on position change
        if (pos != _lastRacePosition && _lastRacePosition > 0)
        {
            _racePosChangeTime = Time.time;
            if (pos < _lastRacePosition && ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayComboUp(); // moving up!
            HapticManager.LightTap();
        }
        _lastRacePosition = pos;

        float changeSince = Time.time - _racePosChangeTime;
        float scale = 1f;
        if (changeSince < 0.3f)
            scale = 1f + (1f - changeSince / 0.3f) * 0.4f;

        _racePositionText.color = posColor;
        _racePositionText.transform.localScale = Vector3.one * scale;
    }

    public void UpdateMultiplier(float mult)
    {
        if (multiplierText == null) return;
        if (mult > 1.1f)
        {
            multiplierText.gameObject.SetActive(true);
            multiplierText.text = $"x{mult:F1}";
            float t = Mathf.Clamp01((mult - 1f) / 4f);
            multiplierText.color = Color.Lerp(
                new Color(1f, 1f, 0.7f),
                new Color(1f, 0.3f, 0.1f), t);
            float pulse = 1f + Mathf.Sin(Time.time * (5f + t * 10f)) * t * 0.15f;
            multiplierText.transform.localScale = Vector3.one * pulse;
        }
        else
        {
            multiplierText.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        // Score punch animation
        if (scoreText != null)
        {
            float scoreSince = Time.time - _scorePunchTime;
            if (scoreSince < 0.2f)
            {
                float punch = 1f + (1f - scoreSince / 0.2f) * 0.25f;
                scoreText.transform.localScale = Vector3.one * punch;
            }
            else
            {
                scoreText.transform.localScale = Vector3.one;
            }
        }

        // Coin count punch animation
        if (coinCountText != null)
        {
            float coinSince = Time.time - _coinPunchTime;
            if (coinSince < 0.25f)
            {
                float punch = 1f + (1f - coinSince / 0.25f) * 0.4f;
                coinCountText.transform.localScale = Vector3.one * punch;
            }
            else
            {
                coinCountText.transform.localScale = Vector3.one;
            }
        }

        // Distance milestone pulse
        if (distanceText != null)
        {
            float distSince = Time.time - _distPunchTime;
            if (distSince < 0.3f)
            {
                float punch = 1f + Mathf.Sin(distSince * 20f) * 0.15f * (1f - distSince / 0.3f);
                distanceText.transform.localScale = Vector3.one * punch;
            }
            else
            {
                distanceText.transform.localScale = Vector3.one;
            }
        }

        // Game over panel entrance animation (scale up with elastic bounce)
        if (_gameOverAnimating && gameOverPanel != null)
        {
            float elapsed = Time.unscaledTime - _gameOverShowTime;
            float dur = 0.6f;
            if (elapsed < dur)
            {
                float t = elapsed / dur;
                // Elastic ease-out: overshoot then settle
                float scale;
                if (t < 0.5f)
                    scale = Mathf.Lerp(0.01f, 1.15f, t / 0.5f);
                else if (t < 0.7f)
                    scale = Mathf.Lerp(1.15f, 0.95f, (t - 0.5f) / 0.2f);
                else
                    scale = Mathf.Lerp(0.95f, 1f, (t - 0.7f) / 0.3f);
                gameOverPanel.transform.localScale = Vector3.one * scale;
                // Fade in alpha faster than scale (fully visible by 40% through animation)
                if (_gameOverGroup != null)
                    _gameOverGroup.alpha = Mathf.Clamp01(t / 0.4f);
            }
            else
            {
                gameOverPanel.transform.localScale = Vector3.one;
                if (_gameOverGroup != null) _gameOverGroup.alpha = 1f;
                _gameOverAnimating = false;
            }
        }

        // New high score text: rainbow color cycle + gentle pulse
        if (_isNewHighScore && highScoreText != null && highScoreText.gameObject.activeInHierarchy)
        {
            _newHighScorePhase += Time.unscaledDeltaTime;
            // Rainbow cycle through gold -> orange -> pink -> gold
            float hue = Mathf.PingPong(_newHighScorePhase * 0.4f, 0.15f) + 0.08f; // gold-orange range
            Color rainbow = Color.HSVToRGB(hue, 0.85f, 1f);
            highScoreText.color = rainbow;

            // Gentle breathing scale
            float breathe = 1f + Mathf.Sin(_newHighScorePhase * 3f) * 0.06f;
            highScoreText.transform.localScale = Vector3.one * breathe;
        }

        // Shop panel fade animation
        if (_shopFadingIn && _shopCanvasGroup != null)
        {
            _shopFadeTimer += Time.deltaTime;
            float st = Mathf.Clamp01(_shopFadeTimer / 0.3f);
            _shopCanvasGroup.alpha = st;
            shopPanel.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, st);
            if (st >= 1f) _shopFadingIn = false;
        }
        if (_shopFadingOut && _shopCanvasGroup != null)
        {
            _shopFadeTimer += Time.deltaTime;
            float st = Mathf.Clamp01(_shopFadeTimer / 0.2f);
            _shopCanvasGroup.alpha = 1f - st;
            shopPanel.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, st);
            if (st >= 1f)
            {
                _shopFadingOut = false;
                shopPanel.SetActive(false);
                _shopCanvasGroup.alpha = 1f;
                shopPanel.transform.localScale = Vector3.one;
                if (startPanel != null) startPanel.SetActive(true);
            }
        }

        // === START SCREEN ANIMATIONS ===
        if (startPanel != null && startPanel.activeSelf)
        {
            _startPulsePhase += Time.deltaTime;
            _startScreenTimer += Time.deltaTime;

            // Start button breathing pulse
            if (startButton != null)
            {
                float pulse = 1f + Mathf.Sin(_startPulsePhase * 3f) * 0.06f;
                startButton.transform.localScale = Vector3.one * pulse;
            }

            // Metal plate gentle float (subtle bobbing)
            Transform metalPlate = startPanel.transform.Find("MetalPlate");
            if (metalPlate != null)
            {
                float bob = Mathf.Sin(_startPulsePhase * 1.2f) * 3f;
                float sway = Mathf.Sin(_startPulsePhase * 0.8f + 1.5f) * 1f;
                metalPlate.localPosition = new Vector3(sway, bob, 0f);
            }

            // Race tagline wobble + color pulse
            Transform tagline = startPanel.transform.Find("RaceTagline");
            if (tagline != null)
            {
                float wobble = Mathf.Sin(_startPulsePhase * 2.5f) * 1.5f;
                tagline.localRotation = Quaternion.Euler(0f, 0f, wobble);
                Text tagText = tagline.GetComponent<Text>();
                if (tagText != null)
                {
                    // Warm color pulse between orange and gold
                    float hue = 0.08f + Mathf.Sin(_startPulsePhase * 1.8f) * 0.03f;
                    tagText.color = Color.HSVToRGB(hue, 0.85f, 1f);
                }
            }

            // Sticker buttons gentle wobble (SHOP / GALLERY)
            Transform shopBtn = startPanel.transform.Find("ShopButton");
            Transform galleryBtn = startPanel.transform.Find("GalleryButton");
            if (shopBtn != null)
            {
                float wobShop = -3f + Mathf.Sin(_startPulsePhase * 1.1f) * 2f;
                shopBtn.localRotation = Quaternion.Euler(0f, 0f, wobShop);
            }
            if (galleryBtn != null)
            {
                float wobGal = 2f + Mathf.Sin(_startPulsePhase * 0.9f + 2f) * 2f;
                galleryBtn.localRotation = Quaternion.Euler(0f, 0f, wobGal);
            }

            // Rotating quip in the challenge text slot
            if (challengeText != null && Time.time >= _nextQuipTime)
            {
                _quipIndex = (_quipIndex + 1) % StartQuips.Length;
                challengeText.text = StartQuips[_quipIndex];
                _nextQuipTime = Time.time + 4f;
            }
            // Quip text fade in/out cycle
            if (challengeText != null)
            {
                float quipAge = _nextQuipTime - Time.time;
                float fadeIn = Mathf.Clamp01((4f - quipAge) / 0.5f);  // fade in over 0.5s
                float fadeOut = Mathf.Clamp01(quipAge / 0.5f);         // fade out last 0.5s
                float alpha = Mathf.Min(fadeIn, fadeOut) * 0.7f;
                challengeText.color = new Color(1f, 0.85f, 0.3f, alpha);
            }

            // Smooth fade-out transition when starting game
            if (_startScreenFading)
            {
                _startFadeTimer += Time.deltaTime;
                float fadeDur = 0.4f;
                float t = Mathf.Clamp01(_startFadeTimer / fadeDur);

                if (_startCanvasGroup != null)
                    _startCanvasGroup.alpha = 1f - t;

                // Slight zoom-in as it fades
                startPanel.transform.localScale = Vector3.one * (1f + t * 0.15f);

                if (t >= 1f)
                {
                    _startScreenFading = false;
                    startPanel.transform.localScale = Vector3.one;
                    if (_startCanvasGroup != null) _startCanvasGroup.alpha = 1f;
                    if (GameManager.Instance != null)
                        GameManager.Instance.StartGame();
                }
            }

            // Entrance animation: staggered elements slide in from off-screen
            if (_startScreenTimer < 1.5f)
            {
                float et = _startScreenTimer;

                // Metal plate slides up from below (0.2s delay, 0.5s duration)
                if (metalPlate != null)
                {
                    float plateT = Mathf.Clamp01((et - 0.2f) / 0.5f);
                    // Elastic ease
                    float elastic = plateT >= 1f ? 1f :
                        Mathf.Pow(2f, -10f * plateT) * Mathf.Sin((plateT - 0.075f) * Mathf.PI * 2f / 0.3f) + 1f;
                    float slideOffset = (1f - Mathf.Clamp01(elastic)) * -200f;
                    metalPlate.localPosition += new Vector3(0f, slideOffset, 0f);
                }

                // Tagline fades in (0.6s delay)
                if (tagline != null)
                {
                    float tagAlpha = Mathf.Clamp01((et - 0.6f) / 0.4f);
                    Text tagText = tagline.GetComponent<Text>();
                    if (tagText != null)
                    {
                        Color c = tagText.color;
                        c.a *= tagAlpha;
                        tagText.color = c;
                    }
                }

                // Stickers pop in (0.8s delay)
                float stickerScale = Mathf.Clamp01((et - 0.8f) / 0.3f);
                float stickerElastic = stickerScale >= 1f ? 1f :
                    Mathf.Pow(2f, -8f * stickerScale) * Mathf.Sin((stickerScale - 0.1f) * Mathf.PI * 2f / 0.35f) + 1f;
                if (shopBtn != null)
                    shopBtn.localScale = Vector3.one * Mathf.Clamp01(stickerElastic);
                if (galleryBtn != null)
                    galleryBtn.localScale = Vector3.one * Mathf.Clamp01(stickerElastic);
            }
        }
    }

    public void UpdateWallet()
    {
        string coinStr = PlayerData.Wallet.ToString("N0");
        if (walletText != null)
            walletText.text = coinStr;
        if (startWalletText != null)
            startWalletText.text = coinStr + " Fartcoins";
    }

    static readonly string[] RaceWinQuips = {
        "NUMBER ONE! Literally!", "WINNER WINNER SEWER DINNER!",
        "GOLD MEDAL TURD!", "KING OF THE PIPES!", "UNSTOPPABLE!",
        "TOP OF THE BOWL!", "CHAMPION FLOATER!", "FLUSH ROYALE!"
    };

    static readonly string[] RacePodiumQuips = {
        "Not bad for a turd!", "Podium finish!", "Almost the best!",
        "Close but no cigar!", "The silver lining!", "Bronze and proud!",
        "Respectable flush!", "Solid performance!"
    };

    static readonly string[] RaceLoseQuips = {
        "Back of the pack!", "Clogged up!", "Log jammed again!",
        "You stink at racing!", "Dead last... fitting!", "Brown town shutdown!",
        "Did not finish!", "Try again, floater!", "Sewer surfing fail!"
    };

    static readonly string[] DeathQuips = {
        "You got FLUSHED!", "Down the drain!", "CLOGGED!", "Totally wiped out!",
        "Splashdown!", "What a dump!", "Sewer surfing fail!", "Brown out!",
        "That stinks!", "You hit rock bottom!", "Pipe dream over!",
        "Went down the toilet!", "PLOP!", "That was crappy!", "Washed up!",
        "Skid mark!", "Floater down!", "Log jammed!", "Number TWO bad!",
        "Royal flush... OUT!", "Brown town shutdown!", "Corn-ered!",
        "Sewer wipeout!", "Turd burglar got you!", "Septic shock!",
        "Full stoppage!", "Drain-o'd!", "That's the pits!",
        "Back to the bowl!", "Un-BOWL-ievable!", "Total dump fire!"
    };

    public void ShowGameOver(int finalScore, int highScore, int coins, float distance, int nearMisses, int bestCombo)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            // Start animated entrance (scale + fade)
            _gameOverShowTime = Time.unscaledTime;
            _gameOverAnimating = true;
            gameOverPanel.transform.localScale = Vector3.one * 0.01f;
            if (_gameOverGroup != null) _gameOverGroup.alpha = 0f;
        }

        if (finalScoreText != null)
            finalScoreText.text = finalScore.ToString("N0");

        _isNewHighScore = finalScore >= highScore && finalScore > 0;
        _newHighScorePhase = 0f;
        if (highScoreText != null)
        {
            if (_isNewHighScore)
            {
                highScoreText.text = "NEW HIGH SCORE!";
                highScoreText.color = new Color(1f, 0.85f, 0.1f); // gold
                highScoreText.fontSize = Mathf.RoundToInt(28 * _uiScale);
            }
            else
            {
                highScoreText.text = "Best: " + highScore.ToString("N0");
                highScoreText.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // Celebrate new high score with extra effects
        if (_isNewHighScore)
        {
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(8f);
                PipeCamera.Instance.Shake(0.3f);
            }
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();
            if (ParticleManager.Instance != null)
            {
                var player = GameManager.Instance?.player;
                if (player != null)
                    ParticleManager.Instance.PlayCelebration(player.transform.position);
            }

            // Poop Crew goes wild for new high score
            if (CheerOverlay.Instance != null)
                CheerOverlay.Instance.ShowCheer("NEW RECORD!", new Color(1f, 0.85f, 0.1f), true);

            HapticManager.HeavyTap();
        }

        // Stat-based celebration effects (even without high score)
        if (!_isNewHighScore)
        {
            Vector3 celebPos = GameManager.Instance?.player != null
                ? GameManager.Instance.player.transform.position : Vector3.zero;

            // Distance milestone celebrations
            if (distance >= 800f)
            {
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer("DEEP DIVE!", new Color(0.8f, 0.2f, 0.1f), true);
                if (ParticleManager.Instance != null)
                    ParticleManager.Instance.PlayCelebration(celebPos);
                HapticManager.MediumTap();
            }
            else if (distance >= 500f)
            {
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer("SEWER SURVIVOR!", new Color(0.9f, 0.5f, 0.2f), false);
                HapticManager.LightTap();
            }

            // Near-miss master
            if (nearMisses >= 15)
            {
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer("DODGE MASTER!", new Color(0.3f, 0.9f, 1f), nearMisses >= 30);
                HapticManager.LightTap();
            }

            // Combo king
            if (bestCombo >= 10)
            {
                if (ScreenEffects.Instance != null)
                    ScreenEffects.Instance.TriggerMilestoneFlash();
                HapticManager.LightTap();
            }

            // Race podium (1st-3rd)
            if (RaceManager.Instance != null)
            {
                int place = RaceManager.Instance.GetPlayerFinishPlace();
                if (place == 1)
                {
                    if (ParticleManager.Instance != null)
                        ParticleManager.Instance.PlayCelebration(celebPos);
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayCelebration();
                    if (PipeCamera.Instance != null)
                        PipeCamera.Instance.PunchFOV(6f);
                    HapticManager.HeavyTap();
                }
                else if (place <= 3 && place > 0)
                {
                    if (ParticleManager.Instance != null)
                        ParticleManager.Instance.PlayCelebration(celebPos);
                    HapticManager.MediumTap();
                }
            }
        }

        if (runStatsText != null)
        {
            // Formatted stats with line breaks for mobile readability
            string stats = $"{Mathf.FloorToInt(distance)}m traveled";
            stats += $"\n{coins} Fartcoins collected";
            if (nearMisses > 0) stats += $"\n{nearMisses} close calls";
            if (bestCombo > 1) stats += $"\n{bestCombo}x best combo";

            // Add race position if in a race
            if (RaceManager.Instance != null)
            {
                int place = RaceManager.Instance.GetPlayerFinishPlace();
                if (place > 0)
                    stats += $"\n{place}{GetOrdinalUpper(place)} Place Finish";
                else
                {
                    int pos = RaceManager.Instance.GetPlayerPosition();
                    stats += $"\n{pos}{GetOrdinalUpper(pos)} (DNF)";
                }
            }

            // Zone context: where you died
            if (PipeZoneSystem.Instance != null)
                stats += $"\nZone: {PipeZoneSystem.Instance.CurrentZoneName}";

            // Stat-based flavor line
            string flavor = GetStatFlavor(coins, nearMisses, bestCombo, distance);
            if (!string.IsNullOrEmpty(flavor))
                stats += $"\n<size=18><i>{flavor}</i></size>";

            runStatsText.text = stats;
        }

        // Set title: race-themed quip if in race, else random death quip
        Transform goTitle = gameOverPanel.transform.Find("GOTitle");
        if (goTitle != null)
        {
            Text titleText = goTitle.GetComponent<Text>();
            if (titleText != null)
            {
                if (RaceManager.Instance != null)
                {
                    int place = RaceManager.Instance.GetPlayerFinishPlace();
                    if (place == 1)
                        titleText.text = RaceWinQuips[Random.Range(0, RaceWinQuips.Length)];
                    else if (place > 0 && place <= 3)
                        titleText.text = RacePodiumQuips[Random.Range(0, RacePodiumQuips.Length)];
                    else
                        titleText.text = RaceLoseQuips[Random.Range(0, RaceLoseQuips.Length)];
                }
                else
                {
                    titleText.text = DeathQuips[Random.Range(0, DeathQuips.Length)];
                }
            }
        }

        UpdateWallet();
    }

    void OnRestartClicked()
    {
        HapticManager.MediumTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }

    // === SHOP ===

    void BuildShopItems()
    {
        if (shopContent == null) return;

        foreach (var skin in SkinData.AllSkins)
        {
            CreateShopItem(skin);
        }
    }

    void CreateShopItem(SkinData.Skin skin)
    {
        if (shopContent == null) return;

        GameObject item = new GameObject("ShopItem_" + skin.id);
        item.transform.SetParent(shopContent, false);

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, Mathf.RoundToInt(70 * _uiScale));

        // Background
        Image bg = item.AddComponent<Image>();
        bool owned = PlayerData.IsSkinUnlocked(skin.id);
        bool selected = PlayerData.SelectedSkin == skin.id;
        bg.color = selected ? new Color(0.2f, 0.4f, 0.15f, 0.8f)
                  : owned ? new Color(0.15f, 0.2f, 0.12f, 0.6f)
                  : new Color(0.1f, 0.1f, 0.1f, 0.6f);

        // Color swatch
        GameObject swatch = new GameObject("Swatch");
        swatch.transform.SetParent(item.transform, false);
        RectTransform swatchRt = swatch.AddComponent<RectTransform>();
        swatchRt.anchorMin = new Vector2(0, 0.1f);
        swatchRt.anchorMax = new Vector2(0, 0.9f);
        swatchRt.offsetMin = new Vector2(10, 0);
        swatchRt.offsetMax = new Vector2(60, 0);
        Image swatchImg = swatch.AddComponent<Image>();
        swatchImg.color = skin.baseColor;

        // Name + cost text
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(item.transform, false);
        RectTransform nameRt = nameObj.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0);
        nameRt.anchorMax = new Vector2(0.6f, 1);
        nameRt.offsetMin = new Vector2(70, 5);
        nameRt.offsetMax = new Vector2(0, -5);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = skin.name;
        if (!owned && skin.cost > 0)
            nameText.text += $"\n<size={Mathf.RoundToInt(18 * _uiScale)}>{skin.cost} Fartcoins</size>";
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (nameText.font == null) nameText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        nameText.fontSize = Mathf.RoundToInt(24 * _uiScale);
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.supportRichText = true;

        // Action button
        GameObject btnObj = new GameObject("ActionBtn");
        btnObj.transform.SetParent(item.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.65f, 0.15f);
        btnRt.anchorMax = new Vector2(0.95f, 0.85f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        Image btnBg = btnObj.AddComponent<Image>();
        Button btn = btnObj.AddComponent<Button>();

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTextRt = btnTextObj.AddComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = nameText.font;
        btnText.fontSize = Mathf.RoundToInt(20 * _uiScale);
        btnText.alignment = TextAnchor.MiddleCenter;

        if (selected)
        {
            btnBg.color = new Color(0.3f, 0.7f, 0.2f);
            btnText.text = "EQUIPPED";
            btnText.color = Color.white;
            btn.interactable = false;
        }
        else if (owned)
        {
            btnBg.color = new Color(0.2f, 0.5f, 0.7f);
            btnText.text = "SELECT";
            btnText.color = Color.white;
            string skinId = skin.id;
            btn.onClick.AddListener(() => OnSelectSkin(skinId));
        }
        else
        {
            bool canAfford = PlayerData.Wallet >= skin.cost;
            btnBg.color = canAfford ? new Color(0.7f, 0.5f, 0.1f) : new Color(0.3f, 0.3f, 0.3f);
            btnText.text = "BUY";
            btnText.color = canAfford ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            btn.interactable = canAfford;
            string skinId = skin.id;
            btn.onClick.AddListener(() => OnBuySkin(skinId));
        }
    }

    void OnSelectSkin(string skinId)
    {
        if (SkinManager.Instance != null)
            SkinManager.Instance.ApplySkin(skinId);
        RefreshShopItems();

        // Confirm skin equipped
        if (CheerOverlay.Instance != null)
            CheerOverlay.Instance.ShowCheer("DRIP!", new Color(0.4f, 0.9f, 1f), false);
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.TriggerPowerUpFlash();
        HapticManager.LightTap();
    }

    void OnBuySkin(string skinId)
    {
        if (SkinManager.Instance != null && SkinManager.Instance.TryPurchaseSkin(skinId))
        {
            SkinManager.Instance.ApplySkin(skinId);
            UpdateWallet();
            RefreshShopItems();

            // New skin celebration!
            if (CheerOverlay.Instance != null)
                CheerOverlay.Instance.ShowCheer("NEW LOOK!", new Color(1f, 0.7f, 0.9f), true);
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();
            HapticManager.HeavyTap();
        }
    }

    void RefreshShopItems()
    {
        if (shopContent == null) return;
        // Destroy existing items
        for (int i = shopContent.childCount - 1; i >= 0; i--)
            Destroy(shopContent.GetChild(i).gameObject);
        BuildShopItems();
    }

    static string GetOrdinalUpper(int n)
    {
        if (n == 1) return "ST";
        if (n == 2) return "ND";
        if (n == 3) return "RD";
        return "TH";
    }

    /// <summary>Pick a stat-based flavor quip for the game over screen.</summary>
    static string GetStatFlavor(int coins, int nearMisses, int bestCombo, float distance)
    {
        // Priority: highlight their best achievement
        if (bestCombo >= 15) return "Combo legend!";
        if (coins >= 50) return "Fartcoin millionaire in the making!";
        if (nearMisses >= 10) return "Living on the edge!";
        if (distance >= 800f) return "So close to Brown Town!";
        if (distance >= 500f) return "Deep in the pipes!";
        if (bestCombo >= 8) return "Nice combo skills!";
        if (coins >= 20) return "Coin magnet!";
        if (nearMisses >= 5) return "Daredevil poop!";
        if (distance >= 200f) return "Getting the hang of it!";
        if (distance < 30f) return "That was quick...";
        if (coins == 0) return "Not a single coin? Ouch.";
        return "";
    }
}
