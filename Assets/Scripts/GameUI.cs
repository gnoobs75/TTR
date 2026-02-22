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
            gameOverPanel.SetActive(false);

        if (startPanel != null)
            startPanel.SetActive(true);

        if (shopPanel != null)
            shopPanel.SetActive(false);

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
    }

    void OnStartClicked()
    {
        HapticManager.MediumTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
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
            shopPanel.SetActive(true);
        if (startPanel != null)
            startPanel.SetActive(false);
        UpdateWallet();
        RefreshShopItems();
    }

    void OnShopCloseClicked()
    {
        HapticManager.LightTap();
        if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.PlayUIClick();
        if (shopPanel != null)
            shopPanel.SetActive(false);
        if (startPanel != null)
            startPanel.SetActive(true);
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

    // Start screen pulse
    private float _startPulsePhase;

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
            }
            else
            {
                gameOverPanel.transform.localScale = Vector3.one;
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

        // Start button pulsing glow
        if (startButton != null && startPanel != null && startPanel.activeSelf)
        {
            _startPulsePhase += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(_startPulsePhase * 3f) * 0.06f;
            startButton.transform.localScale = Vector3.one * pulse;
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
            // Start animated entrance
            _gameOverShowTime = Time.unscaledTime;
            _gameOverAnimating = true;
            gameOverPanel.transform.localScale = Vector3.one * 0.01f;
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
    }

    void OnBuySkin(string skinId)
    {
        if (SkinManager.Instance != null && SkinManager.Instance.TryPurchaseSkin(skinId))
        {
            SkinManager.Instance.ApplySkin(skinId);
            UpdateWallet();
            RefreshShopItems();
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
