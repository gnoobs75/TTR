using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// First-run tutorial hints that appear during gameplay.
/// Shows contextual tips (steer, coins, near-miss, ramps) then never again.
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
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
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
        _tutorialDone = true;
        PlayerPrefs.SetInt(PREFS_KEY, 1);
        PlayerPrefs.Save();
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

    IEnumerator ShowHintCoroutine()
    {
        _showing = true;

        // Fade in
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSeconds(2.5f);

        // Fade out
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - (t / 0.5f);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        _showing = false;
        _hintIndex++;

        // After showing all 4 hint types, mark done
        if (_shownSteer && _shownCoin && _shownNearMiss && _shownRamp)
            CompleteTutorial();
    }
}
