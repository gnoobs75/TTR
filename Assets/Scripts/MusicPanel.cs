using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game music panel: bottom-right music icon that opens a panel to
/// adjust volumes and cycle through music tracks. Pauses game while open.
/// </summary>
public class MusicPanel : MonoBehaviour
{
    public static MusicPanel Instance { get; private set; }

    private bool _isOpen;
    private float _savedTimeScale = 1f;

    // UI references
    private GameObject _musicButton;
    private GameObject _musicPanel;
    private Image _musicButtonImage;
    private CanvasGroup _panelGroup;
    private float _fadeTimer;
    private bool _fadingIn;
    private bool _fadingOut;

    // Track controls
    private AudioClip[] _allTracks;
    private int _currentTrackIndex;
    private Text _trackNameText;

    // Volume sliders
    private Slider _musicSlider;
    private Slider _sfxSlider;
    private Text _musicValueText;
    private Text _sfxValueText;

    public bool IsOpen => _isOpen;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Load all music tracks
        _allTracks = Resources.LoadAll<AudioClip>("music");
        if (_allTracks == null || _allTracks.Length == 0)
            _allTracks = new AudioClip[0];

        // Restore saved track index
        _currentTrackIndex = PlayerPrefs.GetInt("MusicTrack", 0);
        if (_currentTrackIndex >= _allTracks.Length) _currentTrackIndex = 0;

        BuildMusicUI();
    }

    void Update()
    {
        // Pulse music button gently
        if (_musicButton != null && _musicButton.activeSelf && !_isOpen && _musicButtonImage != null)
        {
            float alpha = 0.5f + Mathf.Sin(Time.unscaledTime * 1.5f) * 0.1f;
            Color c = _musicButtonImage.color;
            c.a = alpha;
            _musicButtonImage.color = c;
        }

        // Fade-in animation
        if (_fadingIn && _panelGroup != null)
        {
            _fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeTimer / 0.3f);
            _panelGroup.alpha = t;
            float scale;
            if (t < 0.6f)
                scale = Mathf.Lerp(0.85f, 1.05f, t / 0.6f);
            else
                scale = Mathf.Lerp(1.05f, 1f, (t - 0.6f) / 0.4f);
            _musicPanel.transform.localScale = Vector3.one * scale;
            if (t >= 1f)
            {
                _fadingIn = false;
                _musicPanel.transform.localScale = Vector3.one;
            }
        }

        // Fade-out animation
        if (_fadingOut && _panelGroup != null)
        {
            _fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeTimer / 0.2f);
            _panelGroup.alpha = 1f - t;
            _musicPanel.transform.localScale = Vector3.one * (1f - t * 0.15f);
            if (t >= 1f)
            {
                _fadingOut = false;
                _musicPanel.SetActive(false);
                _panelGroup.alpha = 1f;
                _musicPanel.transform.localScale = Vector3.one;
            }
        }
    }

    void BuildMusicUI()
    {
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            { canvas = c; break; }
        }
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // === MUSIC BUTTON (bottom-right) ===
        _musicButton = new GameObject("MusicButton");
        _musicButton.transform.SetParent(canvas.transform, false);
        RectTransform mbRt = _musicButton.AddComponent<RectTransform>();
        mbRt.anchorMin = new Vector2(0.88f, 0.02f);
        mbRt.anchorMax = new Vector2(0.98f, 0.08f);
        mbRt.offsetMin = Vector2.zero;
        mbRt.offsetMax = Vector2.zero;

        _musicButtonImage = _musicButton.AddComponent<Image>();
        _musicButtonImage.color = new Color(0.15f, 0.12f, 0.08f, 0.5f);

        Button musicBtn = _musicButton.AddComponent<Button>();
        musicBtn.onClick.AddListener(OpenMusic);
        _musicButton.AddComponent<ButtonPressEffect>();

        GameObject iconObj = new GameObject("MusicIcon");
        iconObj.transform.SetParent(_musicButton.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        Text iconText = iconObj.AddComponent<Text>();
        iconText.font = font;
        iconText.fontSize = 28;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = new Color(0.9f, 0.85f, 0.7f, 0.8f);
        iconText.text = "\u266A";
        iconText.fontStyle = FontStyle.Bold;

        _musicButton.SetActive(false); // hidden until game starts

        // === MUSIC OVERLAY PANEL ===
        _musicPanel = new GameObject("MusicPanel");
        _musicPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = _musicPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image panelBg = _musicPanel.AddComponent<Image>();
        panelBg.color = new Color(0.03f, 0.05f, 0.02f, 0.92f);
        _panelGroup = _musicPanel.AddComponent<CanvasGroup>();

        // Title: "MUSIC"
        CreateLabel(_musicPanel.transform, "MusicTitle", font,
            new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.90f),
            "MUSIC", 72, FontStyle.Bold, new Color(1f, 0.85f, 0.1f), true);

        // Now Playing label
        CreateLabel(_musicPanel.transform, "NowPlayingLabel", font,
            new Vector2(0.2f, 0.67f), new Vector2(0.8f, 0.74f),
            "Now Playing:", 22, FontStyle.Normal, new Color(0.6f, 0.55f, 0.45f), false);

        // Track name display
        GameObject trackObj = CreateLabel(_musicPanel.transform, "TrackName", font,
            new Vector2(0.15f, 0.59f), new Vector2(0.85f, 0.67f),
            GetCurrentTrackName(), 32, FontStyle.Bold, new Color(0.9f, 0.85f, 0.7f), false);
        _trackNameText = trackObj.GetComponent<Text>();

        // PREV / NEXT buttons
        CreateTrackButton(_musicPanel.transform, "PrevBtn", "\u25C0 PREV", font,
            new Vector2(0.15f, 0.50f), new Vector2(0.45f, 0.58f), PrevTrack);
        CreateTrackButton(_musicPanel.transform, "NextBtn", "NEXT \u25B6", font,
            new Vector2(0.55f, 0.50f), new Vector2(0.85f, 0.58f), NextTrack);

        // Volume sliders
        _musicSlider = CreateVolumeSlider(_musicPanel.transform, "MusicSlider", "MUSIC", font,
            new Vector2(0.08f, 0.34f), new Vector2(0.48f, 0.47f),
            PlayerPrefs.GetFloat("MusicVolume", 0.4f), OnMusicVolume);
        _sfxSlider = CreateVolumeSlider(_musicPanel.transform, "SFXSlider", "SOUND", font,
            new Vector2(0.52f, 0.34f), new Vector2(0.92f, 0.47f),
            PlayerPrefs.GetFloat("SFXVolume", 1f), OnSFXVolume);

        // RESUME button
        CreateMenuButton(_musicPanel.transform, "ResumeBtn", "RESUME", font,
            new Color(0.15f, 0.55f, 0.12f), new Color(1f, 1f, 0.9f),
            new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.30f),
            CloseMusic);

        _musicPanel.SetActive(false);
    }

    // === PUBLIC API ===

    public void ShowMusicButton()
    {
        if (_musicButton != null)
            _musicButton.SetActive(true);
    }

    public void HideMusicButton()
    {
        if (_musicButton != null)
            _musicButton.SetActive(false);
    }

    public void OpenMusic()
    {
        if (_isOpen) return;
        if (GameManager.Instance == null || !GameManager.Instance.isPlaying) return;

        _isOpen = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (_musicPanel != null)
        {
            _musicPanel.SetActive(true);
            if (_panelGroup != null) _panelGroup.alpha = 0f;
            _musicPanel.transform.localScale = Vector3.one * 0.85f;
            _fadingIn = true;
            _fadingOut = false;
            _fadeTimer = 0f;
        }
        if (_musicButton != null) _musicButton.SetActive(false);

        // Hide pause button while music panel is open
        if (PauseMenu.Instance != null)
            PauseMenu.Instance.HidePauseButton();

        SyncVolumeSliders();
        UpdateTrackDisplay();

        HapticManager.LightTap();
    }

    public void CloseMusic()
    {
        if (!_isOpen) return;

        _isOpen = false;
        Time.timeScale = _savedTimeScale;

        _fadingOut = true;
        _fadingIn = false;
        _fadeTimer = 0f;
        if (_musicButton != null) _musicButton.SetActive(true);

        // Restore pause button
        if (PauseMenu.Instance != null)
            PauseMenu.Instance.ShowPauseButton();

        HapticManager.LightTap();
    }

    // === TRACK CONTROLS ===

    void PrevTrack()
    {
        if (_allTracks.Length == 0) return;
        _currentTrackIndex--;
        if (_currentTrackIndex < 0) _currentTrackIndex = _allTracks.Length - 1;
        ApplyTrack();
    }

    void NextTrack()
    {
        if (_allTracks.Length == 0) return;
        _currentTrackIndex = (_currentTrackIndex + 1) % _allTracks.Length;
        ApplyTrack();
    }

    void ApplyTrack()
    {
        PlayerPrefs.SetInt("MusicTrack", _currentTrackIndex);
        UpdateTrackDisplay();

        if (_allTracks.Length == 0) return;
        AudioClip newClip = _allTracks[_currentTrackIndex];

        // Stop ALL music-length AudioSources first, then swap the first one found
        float vol = PlayerPrefs.GetFloat("MusicVolume", 0.4f);
        AudioSource target = null;
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (src != null && src.clip != null && src.clip.length > 10f)
            {
                if (target == null)
                    target = src;
                else
                    src.Stop(); // kill duplicates
            }
        }

        if (target != null)
        {
            target.clip = newClip;
            target.volume = vol;
            target.Play();
        }

        HapticManager.LightTap();
    }

    void UpdateTrackDisplay()
    {
        if (_trackNameText != null)
            _trackNameText.text = GetCurrentTrackName();
    }

    string GetCurrentTrackName()
    {
        if (_allTracks == null || _allTracks.Length == 0) return "No tracks";
        if (_currentTrackIndex < 0 || _currentTrackIndex >= _allTracks.Length) return "???";
        string name = _allTracks[_currentTrackIndex].name;
        return $"{name}  ({_currentTrackIndex + 1}/{_allTracks.Length})";
    }

    // === VOLUME ===

    void SyncVolumeSliders()
    {
        if (_musicSlider != null)
            _musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.4f);
        if (_sfxSlider != null)
            _sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    void OnMusicVolume(float val)
    {
        PlayerPrefs.SetFloat("MusicVolume", val);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.musicVolume = val;
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (src != null && src.isPlaying && src.clip != null && src.clip.length > 10f)
                src.volume = val;
        }
        if (_musicValueText != null)
            _musicValueText.text = $"MUSIC {Mathf.RoundToInt(val * 100)}%";
    }

    void OnSFXVolume(float val)
    {
        PlayerPrefs.SetFloat("SFXVolume", val);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.sfxVolume = val;
        if (_sfxValueText != null)
            _sfxValueText.text = $"SOUND {Mathf.RoundToInt(val * 100)}%";
    }

    // === UI BUILDERS (same patterns as PauseMenu) ===

    GameObject CreateLabel(Transform parent, string name, Font font,
        Vector2 anchorMin, Vector2 anchorMax, string text,
        int fontSize, FontStyle style, Color color, bool outline)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;

        if (outline)
        {
            Outline o1 = obj.AddComponent<Outline>();
            o1.effectColor = new Color(0, 0, 0, 1f);
            o1.effectDistance = new Vector2(4, -4);
            Outline o2 = obj.AddComponent<Outline>();
            o2.effectColor = new Color(0.4f, 0.2f, 0f, 0.8f);
            o2.effectDistance = new Vector2(-2, 2);
        }

        return obj;
    }

    void CreateTrackButton(Transform parent, string name, string label, Font font,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = anchorMin;
        btnRt.anchorMax = anchorMax;
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.25f, 0.20f, 0.15f, 0.85f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        btnObj.AddComponent<ButtonPressEffect>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.9f, 0.85f, 0.7f);
        text.text = label;

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);
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
        btnObj.AddComponent<ButtonPressEffect>();

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

    Slider CreateVolumeSlider(Transform parent, string name, string label, Font font,
        Vector2 anchorMin, Vector2 anchorMax, float defaultValue,
        UnityEngine.Events.UnityAction<float> onChange)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent, false);
        RectTransform crt = container.AddComponent<RectTransform>();
        crt.anchorMin = anchorMin;
        crt.anchorMax = anchorMax;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        // Label with percentage (top 40%)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(container.transform, false);
        RectTransform lrt = labelObj.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.55f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        Text labelText = labelObj.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 16;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.8f, 0.75f, 0.65f, 0.9f);
        labelText.text = $"{label} {Mathf.RoundToInt(defaultValue * 100)}%";

        if (label == "MUSIC") _musicValueText = labelText;
        else _sfxValueText = labelText;

        // Slider (bottom 50%)
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(container.transform, false);
        RectTransform srt = sliderObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.08f, 0.05f);
        srt.anchorMax = new Vector2(0.92f, 0.50f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        // Background track
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.13f, 0.10f, 0.7f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.65f, 0.50f, 0.12f, 0.9f);

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRt = handle.AddComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 0f);
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.9f, 0.85f, 0.7f);

        // Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;
        slider.onValueChanged.AddListener(onChange);

        return slider;
    }
}
