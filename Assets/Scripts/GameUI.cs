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
        if (shopPanel != null)
            shopPanel.SetActive(true);
        if (startPanel != null)
            startPanel.SetActive(false);
        UpdateWallet();
        RefreshShopItems();
    }

    void OnShopCloseClicked()
    {
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
    }

    public void UpdateWallet()
    {
        string coinStr = PlayerData.Wallet.ToString("N0");
        if (walletText != null)
            walletText.text = coinStr;
        if (startWalletText != null)
            startWalletText.text = coinStr + " Fartcoins";
    }

    static readonly string[] DeathQuips = {
        "You got FLUSHED!", "Down the drain!", "CLOGGED!", "Totally wiped out!",
        "Splashdown!", "What a dump!", "Sewer surfing fail!", "Brown out!",
        "That stinks!", "You hit rock bottom!", "Pipe dream over!",
        "Went down the toilet!", "PLOP!", "That was crappy!", "Washed up!"
    };

    public void ShowGameOver(int finalScore, int highScore, int coins, float distance, int nearMisses, int bestCombo)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (finalScoreText != null)
            finalScoreText.text = finalScore.ToString("N0");

        if (highScoreText != null)
        {
            if (finalScore >= highScore && finalScore > 0)
                highScoreText.text = "NEW HIGH SCORE!";
            else
                highScoreText.text = "Best: " + highScore.ToString("N0");
        }

        if (runStatsText != null)
        {
            string stats = $"{Mathf.FloorToInt(distance)}m  |  {coins} Fartcoins";
            if (nearMisses > 0) stats += $"  |  {nearMisses} close calls";
            if (bestCombo > 1) stats += $"  |  {bestCombo}x combo";
            runStatsText.text = stats;
        }

        // Set random death quip as title
        Transform goTitle = gameOverPanel.transform.Find("GOTitle");
        if (goTitle != null)
        {
            Text titleText = goTitle.GetComponent<Text>();
            if (titleText != null)
                titleText.text = DeathQuips[Random.Range(0, DeathQuips.Length)];
        }

        UpdateWallet();
    }

    void OnRestartClicked()
    {
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
        rt.sizeDelta = new Vector2(0, 70);

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
            nameText.text += $"\n<size=18>{skin.cost} Fartcoins</size>";
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (nameText.font == null) nameText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        nameText.fontSize = 24;
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
        btnText.fontSize = 20;
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
}
