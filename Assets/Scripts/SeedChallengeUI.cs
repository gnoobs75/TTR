using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI for the seed-based challenge system.
/// Players type any word as a seed (e.g. "MondoDook") and share it with friends.
/// Everyone who types the same word gets the same track layout.
/// </summary>
public class SeedChallengeUI : MonoBehaviour
{
    public static SeedChallengeUI Instance { get; private set; }

    private Canvas _canvas;
    private Font _font;
    private float _uiScale = 1f;

    // Panel references
    private GameObject _seedDisplayPanel;   // Post-race: shows seed word + stats
    private GameObject _seedEntryPanel;     // Start screen: type a seed word
    private GameObject _resultsPanel;       // Comparison: YOU vs THEM

    // Seed display (post-race)
    private Text _displaySeedWord;
    private Text _displayCopyFeedback;
    private Text[] _displayStatTexts;

    // Seed entry
    private InputField _entryInput;
    private Text _entryError;

    // Results comparison
    private Text[] _resultYouTexts;
    private Text[] _resultThemTexts;
    private Text[] _resultIndicators;
    private Text _resultVerdict;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Auto-initialize at runtime — find canvas (we're on it) and a font
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        }
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (_canvas != null && _seedDisplayPanel == null)
            Initialize(_canvas, _font);
    }

    public void Initialize(Canvas canvas, Font font)
    {
        _canvas = canvas;
        _font = font;
        _uiScale = Mathf.Max(1f, Screen.height / 1080f);

        CreateSeedDisplayPanel();
        CreateSeedEntryPanel();
        CreateResultsPanel();

        HideAll();
    }

    public void HideAll()
    {
        if (_seedDisplayPanel != null) _seedDisplayPanel.SetActive(false);
        if (_seedEntryPanel != null) _seedEntryPanel.SetActive(false);
        if (_resultsPanel != null) _resultsPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    // PANEL 1: Post-Race Seed Display
    // Shows seed word + your stats after a seeded race
    // ═══════════════════════════════════════════════════

    void CreateSeedDisplayPanel()
    {
        _seedDisplayPanel = CreatePanelBase("SeedDisplay");

        // Header
        CreatePanelText(_seedDisplayPanel.transform, "Header", "RACE COMPLETE!",
            new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.96f),
            new Color(1f, 0.85f, 0.2f), Mathf.RoundToInt(28 * _uiScale), FontStyle.Bold);

        // Seed word (big and prominent)
        _displaySeedWord = CreatePanelText(_seedDisplayPanel.transform, "SeedWord", "SEED: ???",
            new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.86f),
            new Color(0.3f, 0.9f, 1f), Mathf.RoundToInt(24 * _uiScale), FontStyle.Bold);

        // Subtext
        CreatePanelText(_seedDisplayPanel.transform, "ShareHint", "Share this seed with friends!",
            new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.75f),
            new Color(0.6f, 0.6f, 0.6f), Mathf.RoundToInt(13 * _uiScale), FontStyle.Italic);

        // Stats grid
        string[] labels = { "Place", "Time", "Score", "Max Speed", "Coins", "Near Misses", "Combo", "Stomps" };
        _displayStatTexts = new Text[labels.Length];
        float rowH = 0.055f;
        float startY = 0.67f;
        for (int i = 0; i < labels.Length; i++)
        {
            float y = startY - i * rowH;
            CreatePanelText(_seedDisplayPanel.transform, $"SLabel{i}", labels[i],
                new Vector2(0.08f, y - rowH), new Vector2(0.48f, y),
                new Color(0.7f, 0.7f, 0.7f), Mathf.RoundToInt(14 * _uiScale), FontStyle.Normal);
            _displayStatTexts[i] = CreatePanelText(_seedDisplayPanel.transform, $"SValue{i}", "-",
                new Vector2(0.52f, y - rowH), new Vector2(0.92f, y),
                Color.white, Mathf.RoundToInt(14 * _uiScale), FontStyle.Bold);
        }

        // Copy feedback
        _displayCopyFeedback = CreatePanelText(_seedDisplayPanel.transform, "CopyFeedback", "",
            new Vector2(0.2f, 0.19f), new Vector2(0.8f, 0.24f),
            new Color(0.4f, 1f, 0.4f), Mathf.RoundToInt(14 * _uiScale), FontStyle.Italic);

        // Buttons
        CreatePanelButton(_seedDisplayPanel.transform, "CopySeed", "COPY SEED",
            new Vector2(0.05f, 0.06f), new Vector2(0.35f, 0.18f),
            new Color(0.2f, 0.5f, 0.7f), OnCopySeed);

        CreatePanelButton(_seedDisplayPanel.transform, "Share", "SHARE",
            new Vector2(0.37f, 0.06f), new Vector2(0.63f, 0.18f),
            new Color(0.5f, 0.3f, 0.7f), OnShare);

        CreatePanelButton(_seedDisplayPanel.transform, "Close", "CLOSE",
            new Vector2(0.65f, 0.06f), new Vector2(0.95f, 0.18f),
            new Color(0.4f, 0.15f, 0.15f), () => _seedDisplayPanel.SetActive(false));
    }

    public void ShowSeedDisplay(SeedChallenge.ChallengeData data)
    {
        HideAll();

        string seedWord = !string.IsNullOrEmpty(SeedChallenge.SeedWord)
            ? SeedChallenge.SeedWord
            : data.seed.ToString();
        _displaySeedWord.text = $"SEED: {seedWord.ToUpperInvariant()}";

        // Fill stats
        _displayStatTexts[0].text = data.isRace ? $"#{data.place + 1}" : "---";
        _displayStatTexts[1].text = $"{data.time:F1}s";
        _displayStatTexts[2].text = data.score.ToString("N0");
        _displayStatTexts[3].text = $"{data.maxSpeed:F1} SMPH";
        _displayStatTexts[4].text = data.coins.ToString();
        _displayStatTexts[5].text = data.nearMisses.ToString();
        _displayStatTexts[6].text = data.bestCombo.ToString();
        _displayStatTexts[7].text = data.stomps.ToString();

        // Color highlights
        if (data.isRace && data.place == 0)
            _displayStatTexts[0].color = new Color(1f, 0.85f, 0.2f); // gold for 1st
        else if (data.isRace)
            _displayStatTexts[0].color = Color.white;

        _displayCopyFeedback.text = "";
        _seedDisplayPanel.SetActive(true);
    }

    // Backwards-compatible wrapper for existing callers
    public void ShowChallengeCode(SeedChallenge.ChallengeData data)
    {
        ShowSeedDisplay(data);
    }

    void OnCopySeed()
    {
        string seedWord = !string.IsNullOrEmpty(SeedChallenge.SeedWord)
            ? SeedChallenge.SeedWord
            : (SeedManager.Instance != null ? SeedManager.Instance.CurrentSeed.ToString() : "???");
        GUIUtility.systemCopyBuffer = seedWord;
        _displayCopyFeedback.text = "SEED COPIED!";
        StartCoroutine(ClearFeedbackAfter(2f));
    }

    void OnShare()
    {
        if (SeedChallenge.PlayerResult.HasValue)
        {
            string seedWord = !string.IsNullOrEmpty(SeedChallenge.SeedWord)
                ? SeedChallenge.SeedWord
                : (SeedManager.Instance != null ? SeedManager.Instance.CurrentSeed.ToString() : "???");
            string shareText = SeedChallenge.BuildShareText(SeedChallenge.PlayerResult.Value, seedWord);
            GUIUtility.systemCopyBuffer = shareText;
            _displayCopyFeedback.text = "SHARE TEXT COPIED!";
            StartCoroutine(ClearFeedbackAfter(2f));
        }
    }

    IEnumerator ClearFeedbackAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (_displayCopyFeedback != null)
            _displayCopyFeedback.text = "";
    }

    // ═══════════════════════════════════════════════════
    // PANEL 2: Seed Entry (from start screen)
    // Type any word → same track for everyone
    // ═══════════════════════════════════════════════════

    void CreateSeedEntryPanel()
    {
        _seedEntryPanel = CreatePanelBase("SeedEntry");

        // Header
        CreatePanelText(_seedEntryPanel.transform, "Header", "SEED RACE",
            new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.95f),
            new Color(0.3f, 0.9f, 1f), Mathf.RoundToInt(30 * _uiScale), FontStyle.Bold);

        // Explanation
        CreatePanelText(_seedEntryPanel.transform, "Explain", "Type any word. Friends who type\nthe same word get the same track!",
            new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.80f),
            new Color(0.7f, 0.7f, 0.7f), Mathf.RoundToInt(15 * _uiScale), FontStyle.Normal);

        // Input field
        GameObject inputGO = new GameObject("InputField");
        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.SetParent(_seedEntryPanel.transform, false);
        inputRect.anchorMin = new Vector2(0.08f, 0.48f);
        inputRect.anchorMax = new Vector2(0.92f, 0.64f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;

        Image inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.15f);

        // Outline on input
        Outline inputOutline = inputGO.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0.3f, 0.9f, 1f, 0.4f);
        inputOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Text child for InputField
        GameObject textGO = new GameObject("Text");
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.SetParent(inputRect, false);
        textRect.anchorMin = new Vector2(0.03f, 0f);
        textRect.anchorMax = new Vector2(0.97f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text inputText = textGO.AddComponent<Text>();
        inputText.font = _font;
        inputText.fontSize = Mathf.RoundToInt(22 * _uiScale);
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleCenter;
        inputText.supportRichText = false;

        // Placeholder child
        GameObject placeholderGO = new GameObject("Placeholder");
        RectTransform phRect = placeholderGO.AddComponent<RectTransform>();
        phRect.SetParent(inputRect, false);
        phRect.anchorMin = new Vector2(0.03f, 0f);
        phRect.anchorMax = new Vector2(0.97f, 1f);
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        Text placeholder = placeholderGO.AddComponent<Text>();
        placeholder.font = _font;
        placeholder.fontSize = Mathf.RoundToInt(18 * _uiScale);
        placeholder.color = new Color(0.4f, 0.4f, 0.45f);
        placeholder.alignment = TextAnchor.MiddleCenter;
        placeholder.text = "MondoDook";
        placeholder.fontStyle = FontStyle.Italic;

        _entryInput = inputGO.AddComponent<InputField>();
        _entryInput.textComponent = inputText;
        _entryInput.placeholder = placeholder;
        _entryInput.characterLimit = 30;

        // Error text
        _entryError = CreatePanelText(_seedEntryPanel.transform, "Error", "",
            new Vector2(0.1f, 0.40f), new Vector2(0.9f, 0.48f),
            new Color(1f, 0.3f, 0.3f), Mathf.RoundToInt(14 * _uiScale), FontStyle.Normal);

        // Buttons
        CreatePanelButton(_seedEntryPanel.transform, "Go", "RACE IT!",
            new Vector2(0.15f, 0.18f), new Vector2(0.55f, 0.35f),
            new Color(0.15f, 0.5f, 0.15f), OnGoSeed);

        CreatePanelButton(_seedEntryPanel.transform, "Cancel", "CANCEL",
            new Vector2(0.58f, 0.18f), new Vector2(0.85f, 0.35f),
            new Color(0.35f, 0.12f, 0.12f), () => _seedEntryPanel.SetActive(false));
    }

    public void ShowSeedEntry()
    {
        HideAll();
        if (_entryInput != null) _entryInput.text = "";
        if (_entryError != null) _entryError.text = "";
        _seedEntryPanel.SetActive(true);
    }

    // Backwards-compatible wrapper
    public void ShowCodeEntry()
    {
        ShowSeedEntry();
    }

    void OnGoSeed()
    {
        string word = _entryInput.text.Trim();
        if (string.IsNullOrEmpty(word))
        {
            _entryError.text = "Type a seed word first!";
            return;
        }

        _entryError.text = "";

        // Set seed word and compute seed
        SeedChallenge.SeedWord = word;
        SeedChallenge.ActiveChallenge = null; // Word seed, not a stat-challenge
        SeedChallenge.PlayerResult = null;

        HideAll();
        Time.timeScale = 1f;

        // Start a race with this seed
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    // ═══════════════════════════════════════════════════
    // PANEL 3: Challenge Results Comparison (optional)
    // For when stat codes are shared in the future
    // ═══════════════════════════════════════════════════

    void CreateResultsPanel()
    {
        _resultsPanel = CreatePanelBase("ChallengeResults");

        // Header
        CreatePanelText(_resultsPanel.transform, "Header", "CHALLENGE RESULTS",
            new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.97f),
            new Color(1f, 0.85f, 0.2f), Mathf.RoundToInt(26 * _uiScale), FontStyle.Bold);

        // Column headers
        CreatePanelText(_resultsPanel.transform, "YouHeader", "YOU",
            new Vector2(0.35f, 0.80f), new Vector2(0.55f, 0.88f),
            new Color(0.4f, 0.8f, 1f), Mathf.RoundToInt(18 * _uiScale), FontStyle.Bold);
        CreatePanelText(_resultsPanel.transform, "ThemHeader", "THEM",
            new Vector2(0.60f, 0.80f), new Vector2(0.80f, 0.88f),
            new Color(1f, 0.6f, 0.3f), Mathf.RoundToInt(18 * _uiScale), FontStyle.Bold);

        // Stats rows
        string[] labels = { "Score", "Time", "Max Speed", "Coins", "Near Misses", "Combo", "Stomps", "Hits", "Boosts" };
        int count = labels.Length;
        _resultYouTexts = new Text[count];
        _resultThemTexts = new Text[count];
        _resultIndicators = new Text[count];

        float rowH = 0.055f;
        float startY = 0.79f;
        for (int i = 0; i < count; i++)
        {
            float y = startY - i * rowH;
            CreatePanelText(_resultsPanel.transform, $"RLabel{i}", labels[i],
                new Vector2(0.05f, y - rowH), new Vector2(0.33f, y),
                new Color(0.7f, 0.7f, 0.7f), Mathf.RoundToInt(13 * _uiScale), FontStyle.Normal);
            _resultYouTexts[i] = CreatePanelText(_resultsPanel.transform, $"RYou{i}", "-",
                new Vector2(0.35f, y - rowH), new Vector2(0.55f, y),
                Color.white, Mathf.RoundToInt(13 * _uiScale), FontStyle.Bold);
            _resultThemTexts[i] = CreatePanelText(_resultsPanel.transform, $"RThem{i}", "-",
                new Vector2(0.60f, y - rowH), new Vector2(0.80f, y),
                Color.white, Mathf.RoundToInt(13 * _uiScale), FontStyle.Bold);
            _resultIndicators[i] = CreatePanelText(_resultsPanel.transform, $"RInd{i}", "",
                new Vector2(0.82f, y - rowH), new Vector2(0.95f, y),
                Color.white, Mathf.RoundToInt(14 * _uiScale), FontStyle.Bold);
        }

        // Verdict
        _resultVerdict = CreatePanelText(_resultsPanel.transform, "Verdict", "",
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.30f),
            Color.white, Mathf.RoundToInt(22 * _uiScale), FontStyle.Bold);

        // Buttons
        CreatePanelButton(_resultsPanel.transform, "ChallengeBack", "CHALLENGE BACK",
            new Vector2(0.03f, 0.05f), new Vector2(0.33f, 0.19f),
            new Color(0.6f, 0.5f, 0.1f), OnChallengeBack);

        CreatePanelButton(_resultsPanel.transform, "PlayAgain", "PLAY AGAIN",
            new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.19f),
            new Color(0.15f, 0.45f, 0.15f), OnPlayAgain);

        CreatePanelButton(_resultsPanel.transform, "MainMenu", "MAIN MENU",
            new Vector2(0.67f, 0.05f), new Vector2(0.97f, 0.19f),
            new Color(0.4f, 0.15f, 0.15f), OnMainMenu);
    }

    public void ShowResults(SeedChallenge.ChallengeData player, SeedChallenge.ChallengeData challenger)
    {
        HideAll();

        var comp = SeedChallenge.Compare(player, challenger);

        // Fill "YOU" column
        _resultYouTexts[0].text = player.score.ToString("N0");
        _resultYouTexts[1].text = $"{player.time:F1}s";
        _resultYouTexts[2].text = $"{player.maxSpeed:F1}";
        _resultYouTexts[3].text = player.coins.ToString();
        _resultYouTexts[4].text = player.nearMisses.ToString();
        _resultYouTexts[5].text = player.bestCombo.ToString();
        _resultYouTexts[6].text = player.stomps.ToString();
        _resultYouTexts[7].text = player.hits.ToString();
        _resultYouTexts[8].text = player.boosts.ToString();

        // Fill "THEM" column
        _resultThemTexts[0].text = challenger.score.ToString("N0");
        _resultThemTexts[1].text = $"{challenger.time:F1}s";
        _resultThemTexts[2].text = $"{challenger.maxSpeed:F1}";
        _resultThemTexts[3].text = challenger.coins.ToString();
        _resultThemTexts[4].text = challenger.nearMisses.ToString();
        _resultThemTexts[5].text = challenger.bestCombo.ToString();
        _resultThemTexts[6].text = challenger.stomps.ToString();
        _resultThemTexts[7].text = challenger.hits.ToString();
        _resultThemTexts[8].text = challenger.boosts.ToString();

        // Indicators
        SeedChallenge.StatResult[] results = {
            comp.score, comp.time, comp.maxSpeed, comp.coins,
            comp.nearMisses, comp.bestCombo, comp.stomps, comp.hits, comp.boosts
        };
        for (int i = 0; i < results.Length; i++)
        {
            switch (results[i])
            {
                case SeedChallenge.StatResult.Win:
                    _resultIndicators[i].text = "W";
                    _resultIndicators[i].color = new Color(0.3f, 1f, 0.3f);
                    _resultYouTexts[i].color = new Color(0.3f, 1f, 0.3f);
                    _resultThemTexts[i].color = new Color(0.6f, 0.6f, 0.6f);
                    break;
                case SeedChallenge.StatResult.Lose:
                    _resultIndicators[i].text = "L";
                    _resultIndicators[i].color = new Color(1f, 0.3f, 0.3f);
                    _resultYouTexts[i].color = new Color(0.6f, 0.6f, 0.6f);
                    _resultThemTexts[i].color = new Color(1f, 0.3f, 0.3f);
                    break;
                default:
                    _resultIndicators[i].text = "=";
                    _resultIndicators[i].color = new Color(0.7f, 0.7f, 0.7f);
                    _resultYouTexts[i].color = Color.white;
                    _resultThemTexts[i].color = Color.white;
                    break;
            }
        }

        // Verdict
        if (comp.playerWins > comp.challengerWins)
        {
            _resultVerdict.text = $"YOU WIN! {comp.playerWins}/{comp.playerWins + comp.challengerWins}";
            _resultVerdict.color = new Color(0.3f, 1f, 0.3f);
        }
        else if (comp.challengerWins > comp.playerWins)
        {
            _resultVerdict.text = $"THEY WIN! {comp.challengerWins}/{comp.playerWins + comp.challengerWins}";
            _resultVerdict.color = new Color(1f, 0.3f, 0.3f);
        }
        else
        {
            _resultVerdict.text = "IT'S A TIE!";
            _resultVerdict.color = new Color(1f, 0.85f, 0.2f);
        }

        _resultsPanel.SetActive(true);
    }

    void OnChallengeBack()
    {
        if (SeedChallenge.PlayerResult.HasValue)
            ShowSeedDisplay(SeedChallenge.PlayerResult.Value);
    }

    void OnPlayAgain()
    {
        HideAll();
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }

    void OnMainMenu()
    {
        HideAll();
        SeedChallenge.ActiveChallenge = null;
        SeedChallenge.PlayerResult = null;
        SeedChallenge.SeedWord = null;
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }

    // ═══════════════════════════════════════════════════
    // UI Helpers
    // ═══════════════════════════════════════════════════

    GameObject CreatePanelBase(string name)
    {
        GameObject panel = new GameObject(name);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.SetParent(_canvas.transform, false);
        rect.anchorMin = new Vector2(0.1f, 0.1f);
        rect.anchorMax = new Vector2(0.9f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.9f, 1f, 0.6f);
        outline.effectDistance = new Vector2(2, -2);

        panel.AddComponent<CanvasGroup>();
        return panel;
    }

    Text CreatePanelText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize, FontStyle style)
    {
        GameObject go = new GameObject(name);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text t = go.AddComponent<Text>();
        t.font = _font;
        t.text = text;
        t.color = color;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    void CreatePanelButton(Transform parent, string name, string label,
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
        btn.targetGraphic = btnBg;

        GameObject labelObj = new GameObject("Label");
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.SetParent(btnRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObj.AddComponent<Text>();
        labelText.font = _font;
        labelText.text = label;
        labelText.color = Color.white;
        labelText.fontSize = Mathf.RoundToInt(16 * _uiScale);
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;

        btn.onClick.AddListener(onClick);
    }
}
