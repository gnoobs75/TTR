using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// First-run tutorial hints that appear during gameplay.
/// Shows contextual tips (9 hint types) then never again.
/// Self-builds UI at runtime - finds the overlay canvas and creates hint panel.
/// Completion: 6 of 9 hints = tutorial done (not all events occur every run).
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    public static TutorialOverlay Instance { get; private set; }

    [Header("UI")]
    public Text hintText;
    public CanvasGroup canvasGroup;

    private const int TOTAL_HINTS = 9;
    private const int COMPLETION_THRESHOLD = 6; // 6 of 9 = done

    private bool _tutorialDone;
    private bool _showing;
    private int _hintIndex;
    private float _gameTimer;
    private Text _progressText;
    private float _uiScale = 1f;

    // Animated arrow pointer
    private GameObject _arrowPointer;
    private RectTransform _arrowRt;

    // Track which hints have been shown this session
    private bool _shownSteer;
    private bool _shownCoin;
    private bool _shownNearMiss;
    private bool _shownRamp;
    private bool _shownStomp;
    private bool _shownTrick;
    private bool _shownPowerUp;
    private bool _shownFork;
    private bool _shownSpeedBoost;

    private static readonly string PREFS_KEY = "Tutorial_Done";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _tutorialDone = PlayerPrefs.GetInt(PREFS_KEY, 0) == 1;

        // DPI scaling for mobile
        float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
        _uiScale = Mathf.Clamp(dpi / 160f, 1f, 2.5f);

        BuildTutorialUI();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void BuildTutorialUI()
    {
        // Find the overlay canvas
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            { canvas = c; break; }
        }
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Hint container (centered, middle-lower area of screen)
        GameObject hintObj = new GameObject("TutorialHint");
        hintObj.transform.SetParent(canvas.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.1f, 0.22f);
        hintRt.anchorMax = new Vector2(0.9f, 0.32f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;

        // Translucent background for readability
        Image bg = hintObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.03f, 0.85f);

        // CanvasGroup for fading
        canvasGroup = hintObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Hint text
        GameObject textObj = new GameObject("HintText");
        textObj.transform.SetParent(hintObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.05f, 0f);
        textRt.anchorMax = new Vector2(0.95f, 1f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        hintText = textObj.AddComponent<Text>();
        hintText.font = font;
        hintText.fontSize = Mathf.RoundToInt(26 * _uiScale);
        hintText.fontStyle = FontStyle.Bold;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(1f, 0.95f, 0.7f);
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        // Progress indicator (small text at bottom-right of hint box)
        GameObject progObj = new GameObject("HintProgress");
        progObj.transform.SetParent(hintObj.transform, false);
        RectTransform progRt = progObj.AddComponent<RectTransform>();
        progRt.anchorMin = new Vector2(0.7f, 0f);
        progRt.anchorMax = new Vector2(0.98f, 0.35f);
        progRt.offsetMin = Vector2.zero;
        progRt.offsetMax = Vector2.zero;

        _progressText = progObj.AddComponent<Text>();
        _progressText.font = font;
        _progressText.fontSize = Mathf.RoundToInt(14 * _uiScale);
        _progressText.alignment = TextAnchor.LowerRight;
        _progressText.color = new Color(0.5f, 0.5f, 0.4f, 0.7f);

        // Animated arrow pointer (golden bouncing triangle)
        _arrowPointer = new GameObject("ArrowPointer");
        _arrowPointer.transform.SetParent(canvas.transform, false);
        _arrowRt = _arrowPointer.AddComponent<RectTransform>();
        _arrowRt.sizeDelta = new Vector2(40 * _uiScale, 40 * _uiScale);
        _arrowRt.anchorMin = new Vector2(0.5f, 0.35f);
        _arrowRt.anchorMax = new Vector2(0.5f, 0.35f);

        Text arrowText = _arrowPointer.AddComponent<Text>();
        arrowText.font = font;
        arrowText.text = "\u25BC"; // down-pointing triangle
        arrowText.fontSize = Mathf.RoundToInt(32 * _uiScale);
        arrowText.fontStyle = FontStyle.Bold;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.color = new Color(1f, 0.85f, 0.2f);

        Outline arrowOutline = _arrowPointer.AddComponent<Outline>();
        arrowOutline.effectColor = new Color(0.3f, 0.2f, 0f, 0.9f);
        arrowOutline.effectDistance = new Vector2(2, -2);

        _arrowPointer.SetActive(false);
    }

    void Update()
    {
        if (_tutorialDone) return;

        var gm = GameManager.Instance;
        if (gm == null || !gm.isPlaying) return;

        _gameTimer += Time.deltaTime;

        // Show steer hint after 1 second
        if (!_shownSteer && _gameTimer > 1f)
        {
            _shownSteer = true;
            string controlHint = "Tilt to steer!";
            var input = TouchInput.Instance;
            if (input != null)
            {
                switch (input.controlScheme)
                {
                    case TouchInput.ControlScheme.TouchZones:
                        controlHint = "Tap left/right to steer!";
                        break;
                    case TouchInput.ControlScheme.Swipe:
                        controlHint = "Swipe left/right to steer!";
                        break;
                    case TouchInput.ControlScheme.Tilt:
                        controlHint = "Tilt to steer!";
                        break;
                    case TouchInput.ControlScheme.Keyboard:
                        controlHint = "Arrow keys to steer!";
                        break;
                }
            }
            ShowHint(controlHint);
        }

        // Bounce the arrow pointer
        if (_arrowPointer != null && _arrowPointer.activeSelf && _arrowRt != null)
        {
            float bounce = Mathf.Sin(Time.time * 4f) * 8f * _uiScale;
            _arrowRt.anchoredPosition = new Vector2(0f, bounce);
        }
    }

    /// Called by Collectible when first coin is collected
    public void OnFirstCoin()
    {
        if (_tutorialDone || _shownCoin) return;
        _shownCoin = true;
        ShowHint("Collect Fartcoins!");
    }

    /// Called by NearMissZone when first near miss happens
    public void OnFirstNearMiss()
    {
        if (_tutorialDone || _shownNearMiss) return;
        _shownNearMiss = true;
        ShowHint("Near miss = bonus points!");
    }

    /// Called by JumpRamp when first ramp is nearby
    public void OnFirstRamp()
    {
        if (_tutorialDone || _shownRamp) return;
        _shownRamp = true;
        ShowHint("Hit ramps for tricks!");
    }

    /// Called by TurdController.StompBounce() on first stomp
    public void OnFirstStomp()
    {
        if (_tutorialDone || _shownStomp) return;
        _shownStomp = true;
        ShowHint("Jump on enemies to stomp!");
    }

    /// Called by TurdController on first completed trick (360 flip)
    public void OnFirstTrick()
    {
        if (_tutorialDone || _shownTrick) return;
        _shownTrick = true;
        ShowHint("Up/Down in air = tricks!");
    }

    /// Called by pickup scripts (Shield/Magnet/SlowMo) on first power-up collection
    public void OnFirstPowerUp()
    {
        if (_tutorialDone || _shownPowerUp) return;
        _shownPowerUp = true;
        ShowHint("Grab power-ups for buffs!");
    }

    /// Called by GameManager fork warning on first fork approach
    public void OnFirstFork()
    {
        if (_tutorialDone || _shownFork) return;
        _shownFork = true;
        ShowHint("Fork ahead! Left=Safe, Right=Risky!");
    }

    /// Called by SpeedBoost on first speed boost pickup
    public void OnFirstSpeedBoost()
    {
        if (_tutorialDone || _shownSpeedBoost) return;
        _shownSpeedBoost = true;
        ShowHint("Speed boosts = ZOOM!");
    }

    /// Mark tutorial as complete (called after enough hints shown or game over)
    public void CompleteTutorial()
    {
        if (_tutorialDone) return; // Don't celebrate twice
        _tutorialDone = true;
        PlayerPrefs.SetInt(PREFS_KEY, 1);
        PlayerPrefs.Save();

        // You did it, little turd!
        if (CheerOverlay.Instance != null)
            CheerOverlay.Instance.ShowCheer("YOU GOT THIS!", new Color(0.3f, 1f, 0.6f), true);
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.TriggerPowerUpFlash();
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCelebration();
        HapticManager.LightTap();
    }

    public void ResetForNewRun()
    {
        _gameTimer = 0f;
        if (!_tutorialDone)
        {
            _shownSteer = false;
            // Keep other hints as shown - only steer hint repeats per run
        }
    }

    void ShowHint(string text)
    {
        if (_showing || hintText == null || canvasGroup == null) return;
        hintText.text = text;
        StartCoroutine(ShowHintCoroutine());
    }

    int CountHintsShown()
    {
        int n = 0;
        if (_shownSteer) n++;
        if (_shownCoin) n++;
        if (_shownNearMiss) n++;
        if (_shownRamp) n++;
        if (_shownStomp) n++;
        if (_shownTrick) n++;
        if (_shownPowerUp) n++;
        if (_shownFork) n++;
        if (_shownSpeedBoost) n++;
        return n;
    }

    IEnumerator ShowHintCoroutine()
    {
        _showing = true;

        // Show arrow pointer during hint
        if (_arrowPointer != null) _arrowPointer.SetActive(true);

        // Update progress indicator
        int shown = CountHintsShown();
        if (_progressText != null)
            _progressText.text = $"{shown}/{TOTAL_HINTS}";

        // Fade in
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // Gentle haptic to draw attention
        HapticManager.LightTap();

        // Hold (longer for mobile readability)
        yield return new WaitForSeconds(3.5f);

        // Fade out
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1f - (t / 0.5f);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Hide arrow
        if (_arrowPointer != null) _arrowPointer.SetActive(false);

        _showing = false;
        _hintIndex++;

        // After showing enough hints, mark done
        if (CountHintsShown() >= COMPLETION_THRESHOLD)
            CompleteTutorial();
    }
}
