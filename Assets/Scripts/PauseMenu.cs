using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mobile pause menu with resume, restart, control scheme picker.
/// Pause button lives top-left during gameplay. Overlay fills screen when paused.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    private bool _isPaused;
    private float _savedTimeScale = 1f;

    // UI references
    private GameObject _pauseButton;
    private GameObject _pausePanel;
    private Text _controlSchemeLabel;
    private Image _pauseButtonImage;
    private float _pauseButtonPulsePhase;
    private CanvasGroup _pausePanelGroup;
    private float _pauseFadeTimer;
    private bool _pauseFadingIn;
    private bool _pauseFadingOut;

    // Control scheme display
    private static readonly string[] SCHEME_NAMES = { "Touch Zones", "Swipe", "Tilt", "Keyboard" };

    public bool IsPaused => _isPaused;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        BuildPauseUI();
    }

    void Update()
    {
        // Pulse pause button gently during gameplay
        if (_pauseButton != null && _pauseButton.activeSelf && !_isPaused)
        {
            _pauseButtonPulsePhase += Time.deltaTime;
            float alpha = 0.5f + Mathf.Sin(_pauseButtonPulsePhase * 1.5f) * 0.1f;
            if (_pauseButtonImage != null)
            {
                Color c = _pauseButtonImage.color;
                c.a = alpha;
                _pauseButtonImage.color = c;
            }
        }

        // Pause panel fade-in animation (uses unscaledDeltaTime since TimeScale=0)
        if (_pauseFadingIn && _pausePanelGroup != null)
        {
            _pauseFadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_pauseFadeTimer / 0.3f);
            _pausePanelGroup.alpha = t;
            // Elastic scale: 0.85 → overshoot 1.05 → settle 1.0
            float scale;
            if (t < 0.6f)
                scale = Mathf.Lerp(0.85f, 1.05f, t / 0.6f);
            else
                scale = Mathf.Lerp(1.05f, 1f, (t - 0.6f) / 0.4f);
            _pausePanel.transform.localScale = Vector3.one * scale;
            if (t >= 1f)
            {
                _pauseFadingIn = false;
                _pausePanel.transform.localScale = Vector3.one;
            }
        }

        // Pause panel fade-out animation
        if (_pauseFadingOut && _pausePanelGroup != null)
        {
            _pauseFadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_pauseFadeTimer / 0.2f);
            _pausePanelGroup.alpha = 1f - t;
            _pausePanel.transform.localScale = Vector3.one * (1f - t * 0.15f);
            if (t >= 1f)
            {
                _pauseFadingOut = false;
                _pausePanel.SetActive(false);
                _pausePanelGroup.alpha = 1f;
                _pausePanel.transform.localScale = Vector3.one;
            }
        }

        // Keyboard escape to toggle pause
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused) Resume();
            else if (GameManager.Instance != null && GameManager.Instance.isPlaying) Pause();
        }
    }

    void BuildPauseUI()
    {
        // Find the main game canvas
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            { canvas = c; break; }
        }
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // === PAUSE BUTTON (top-left, small tap target) ===
        _pauseButton = new GameObject("PauseButton");
        _pauseButton.transform.SetParent(canvas.transform, false);
        RectTransform pbRt = _pauseButton.AddComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0.02f, 0.90f);
        pbRt.anchorMax = new Vector2(0.12f, 0.96f);
        pbRt.offsetMin = Vector2.zero;
        pbRt.offsetMax = Vector2.zero;

        _pauseButtonImage = _pauseButton.AddComponent<Image>();
        _pauseButtonImage.color = new Color(0.15f, 0.12f, 0.08f, 0.5f);

        Button pauseBtn = _pauseButton.AddComponent<Button>();
        pauseBtn.onClick.AddListener(Pause);

        // Pause icon (two vertical bars)
        GameObject pauseIcon = new GameObject("PauseIcon");
        pauseIcon.transform.SetParent(_pauseButton.transform, false);
        RectTransform iconRt = pauseIcon.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        Text iconText = pauseIcon.AddComponent<Text>();
        iconText.font = font;
        iconText.fontSize = 28;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = new Color(0.9f, 0.85f, 0.7f, 0.8f);
        iconText.text = "| |";
        iconText.fontStyle = FontStyle.Bold;

        _pauseButton.SetActive(false); // hidden until game starts

        // === PAUSE OVERLAY PANEL ===
        _pausePanel = new GameObject("PausePanel");
        _pausePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = _pausePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image panelBg = _pausePanel.AddComponent<Image>();
        panelBg.color = new Color(0.03f, 0.05f, 0.02f, 0.92f);
        _pausePanelGroup = _pausePanel.AddComponent<CanvasGroup>();

        // Title: "PIPE BLOCKED!"
        GameObject titleObj = new GameObject("PauseTitle");
        titleObj.transform.SetParent(_pausePanel.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.72f);
        titleRt.anchorMax = new Vector2(0.9f, 0.88f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 72;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.1f);
        titleText.text = "PIPE BLOCKED!";
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0, 0, 0, 1f);
        titleOutline.effectDistance = new Vector2(4, -4);
        Outline titleOutline2 = titleObj.AddComponent<Outline>();
        titleOutline2.effectColor = new Color(0.4f, 0.2f, 0f, 0.8f);
        titleOutline2.effectDistance = new Vector2(-2, 2);

        // Subtitle
        GameObject subObj = new GameObject("PauseSub");
        subObj.transform.SetParent(_pausePanel.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.2f, 0.65f);
        subRt.anchorMax = new Vector2(0.8f, 0.72f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;

        Text subText = subObj.AddComponent<Text>();
        subText.font = font;
        subText.fontSize = 22;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.6f, 0.55f, 0.45f);
        subText.text = "Game Paused";

        // === RESUME BUTTON ===
        CreateMenuButton(_pausePanel.transform, "ResumeBtn", "UNCLOG!", font,
            new Color(0.15f, 0.55f, 0.12f), new Color(1f, 1f, 0.9f),
            new Vector2(0.2f, 0.48f), new Vector2(0.8f, 0.60f),
            Resume);

        // === RESTART BUTTON ===
        CreateMenuButton(_pausePanel.transform, "RestartBtn", "FLUSH AGAIN", font,
            new Color(0.55f, 0.12f, 0.08f), new Color(1f, 1f, 0.9f),
            new Vector2(0.2f, 0.34f), new Vector2(0.8f, 0.46f),
            Restart);

        // === CONTROL SCHEME SELECTOR ===
        GameObject controlRow = new GameObject("ControlRow");
        controlRow.transform.SetParent(_pausePanel.transform, false);
        RectTransform crRt = controlRow.AddComponent<RectTransform>();
        crRt.anchorMin = new Vector2(0.1f, 0.20f);
        crRt.anchorMax = new Vector2(0.9f, 0.30f);
        crRt.offsetMin = Vector2.zero;
        crRt.offsetMax = Vector2.zero;

        // Label
        GameObject clObj = new GameObject("ControlLabel");
        clObj.transform.SetParent(controlRow.transform, false);
        RectTransform clRt = clObj.AddComponent<RectTransform>();
        clRt.anchorMin = new Vector2(0, 0);
        clRt.anchorMax = new Vector2(0.4f, 1f);
        clRt.offsetMin = Vector2.zero;
        clRt.offsetMax = Vector2.zero;

        Text clText = clObj.AddComponent<Text>();
        clText.font = font;
        clText.fontSize = 22;
        clText.alignment = TextAnchor.MiddleRight;
        clText.color = new Color(0.7f, 0.7f, 0.65f);
        clText.text = "Controls:";

        // Scheme button (cycles through schemes)
        GameObject schemeBtn = new GameObject("SchemeBtn");
        schemeBtn.transform.SetParent(controlRow.transform, false);
        RectTransform sbRt = schemeBtn.AddComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.45f, 0.1f);
        sbRt.anchorMax = new Vector2(0.95f, 0.9f);
        sbRt.offsetMin = Vector2.zero;
        sbRt.offsetMax = Vector2.zero;

        Image sbBg = schemeBtn.AddComponent<Image>();
        sbBg.color = new Color(0.2f, 0.18f, 0.15f, 0.8f);
        Button sbButton = schemeBtn.AddComponent<Button>();
        sbButton.onClick.AddListener(CycleControlScheme);

        GameObject slObj = new GameObject("SchemeText");
        slObj.transform.SetParent(schemeBtn.transform, false);
        RectTransform slRt = slObj.AddComponent<RectTransform>();
        slRt.anchorMin = Vector2.zero;
        slRt.anchorMax = Vector2.one;
        slRt.offsetMin = Vector2.zero;
        slRt.offsetMax = Vector2.zero;

        _controlSchemeLabel = slObj.AddComponent<Text>();
        _controlSchemeLabel.font = font;
        _controlSchemeLabel.fontSize = 24;
        _controlSchemeLabel.fontStyle = FontStyle.Bold;
        _controlSchemeLabel.alignment = TextAnchor.MiddleCenter;
        _controlSchemeLabel.color = new Color(1f, 0.85f, 0.3f);
        UpdateSchemeLabel();

        // Version / credit at bottom
        GameObject verObj = new GameObject("PauseVersion");
        verObj.transform.SetParent(_pausePanel.transform, false);
        RectTransform verRt = verObj.AddComponent<RectTransform>();
        verRt.anchorMin = new Vector2(0.1f, 0.02f);
        verRt.anchorMax = new Vector2(0.9f, 0.08f);
        verRt.offsetMin = Vector2.zero;
        verRt.offsetMax = Vector2.zero;

        Text verText = verObj.AddComponent<Text>();
        verText.font = font;
        verText.fontSize = 16;
        verText.alignment = TextAnchor.MiddleCenter;
        verText.color = new Color(0.4f, 0.38f, 0.32f);
        verText.text = "Turd Tunnel Rush v1.0 - Race to Brown Town!";

        _pausePanel.SetActive(false);
    }

    void CreateMenuButton(Transform parent, string name, string label, Font font,
        Color bgColor, Color textColor, Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = anchorMin;
        btnRt.anchorMax = anchorMax;
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.text = label;

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);
    }

    // === PUBLIC API ===

    /// <summary>Show the pause button during gameplay.</summary>
    public void ShowPauseButton()
    {
        if (_pauseButton != null)
            _pauseButton.SetActive(true);
    }

    /// <summary>Hide the pause button (menu, game over).</summary>
    public void HidePauseButton()
    {
        if (_pauseButton != null)
            _pauseButton.SetActive(false);
    }

    public void Pause()
    {
        if (_isPaused) return;
        if (GameManager.Instance == null || !GameManager.Instance.isPlaying) return;

        _isPaused = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (_pausePanel != null)
        {
            _pausePanel.SetActive(true);
            if (_pausePanelGroup != null) _pausePanelGroup.alpha = 0f;
            _pausePanel.transform.localScale = Vector3.one * 0.85f;
            _pauseFadingIn = true;
            _pauseFadingOut = false;
            _pauseFadeTimer = 0f;
        }
        if (_pauseButton != null) _pauseButton.SetActive(false);

        UpdateSchemeLabel();

        // Mute audio during pause
        AudioListener.pause = true;

        HapticManager.LightTap();
    }

    public void Resume()
    {
        if (!_isPaused) return;

        _isPaused = false;
        Time.timeScale = _savedTimeScale;

        // Fade out pause panel instead of instant hide
        _pauseFadingOut = true;
        _pauseFadingIn = false;
        _pauseFadeTimer = 0f;
        if (_pauseButton != null) _pauseButton.SetActive(true);

        AudioListener.pause = false;

        // Welcome back!
        if (CheerOverlay.Instance != null)
            CheerOverlay.Instance.ShowCheer("LET'S GO!", new Color(0.3f, 1f, 0.5f), false);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCoinCollect();
        HapticManager.LightTap();
    }

    void Restart()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (_pausePanel != null) _pausePanel.SetActive(false);
        _pauseFadingIn = false;
        _pauseFadingOut = false;

        // Fresh start energy!
        if (CheerOverlay.Instance != null)
            CheerOverlay.Instance.ShowCheer("AGAIN!", new Color(1f, 0.6f, 0.2f), true);
        HapticManager.MediumTap();

        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }

    void CycleControlScheme()
    {
        if (TouchInput.Instance == null) return;

        int current = (int)TouchInput.Instance.controlScheme;
        int next = (current + 1) % SCHEME_NAMES.Length;

        // Skip keyboard on mobile, skip tilt on desktop
#if UNITY_IOS || UNITY_ANDROID
        if (next == (int)TouchInput.ControlScheme.Keyboard) next = 0;
#else
        if (next == (int)TouchInput.ControlScheme.Tilt) next++;
        if (next >= SCHEME_NAMES.Length) next = 0;
#endif

        TouchInput.Instance.SetControlScheme((TouchInput.ControlScheme)next);
        UpdateSchemeLabel();
        HapticManager.LightTap();
    }

    void UpdateSchemeLabel()
    {
        if (_controlSchemeLabel == null) return;
        int idx = TouchInput.Instance != null ? (int)TouchInput.Instance.controlScheme : 0;
        if (idx >= 0 && idx < SCHEME_NAMES.Length)
            _controlSchemeLabel.text = SCHEME_NAMES[idx];
    }
}
