using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// First-run tutorial hints that appear during gameplay.
/// Shows contextual tips (steer, coins, near-miss, ramps) then never again.
/// Self-builds UI at runtime - finds the overlay canvas and creates hint panel.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    public static TutorialOverlay Instance { get; private set; }

    [Header("UI")]
    public Text hintText;
    public CanvasGroup canvasGroup;

    private bool _tutorialDone;
    private bool _showing;
    private int _hintIndex;
    private float _gameTimer;
    private Text _progressText; // shows "1/4", "2/4" etc.
    private float _uiScale = 1f;

    // Track which hints have been shown this session
    private bool _shownSteer;
    private bool _shownCoin;
    private bool _shownNearMiss;
    private bool _shownRamp;

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

    /// Mark tutorial as complete (called after all hints shown or game over)
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
            // Keep coin/nearmiss/ramp as shown - only steer hint repeats per run
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
        return n;
    }

    IEnumerator ShowHintCoroutine()
    {
        _showing = true;

        // Update progress indicator
        int shown = CountHintsShown();
        if (_progressText != null)
            _progressText.text = $"{shown}/4";

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

        _showing = false;
        _hintIndex++;

        // After showing all 4 hint types, mark done
        if (_shownSteer && _shownCoin && _shownNearMiss && _shownRamp)
            CompleteTutorial();
    }
}
