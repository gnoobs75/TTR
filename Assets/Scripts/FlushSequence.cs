using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-game flush countdown sequence.
/// Shows character faces in a toilet bowl view, counts "3, 2, 1, FLUSH!"
/// with swirling water animation and camera dive into the pipe.
/// </summary>
public class FlushSequence : MonoBehaviour
{
    public static FlushSequence Instance { get; private set; }

    public enum FlushState { Idle, ShowingFaces, Countdown, Flushing, Done }
    public FlushState State { get; private set; } = FlushState.Idle;

    [Header("UI")]
    public Text countdownText;
    public Image whirlOverlay;
    public Image vignetteOverlay;

    [Header("Timing")]
    public float faceShowDuration = 2f;
    public float countdownInterval = 0.8f;
    public float flushDuration = 1.5f;

    private float _timer;
    private int _countdownValue;
    private Camera _cam;
    private PipeCamera _pipeCam;
    private Vector3 _originalCamPos;
    private Quaternion _originalCamRot;
    private float _whirlAngle;
    private float _countdownPunchTime; // for punch-scale on each number
    private float _flushPunchTime;     // for FLUSH! text burst

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _cam = Camera.main;
        _pipeCam = Object.FindFirstObjectByType<PipeCamera>();
    }

    /// <summary>
    /// Called from GameManager when player presses Start.
    /// Plays the flush sequence before gameplay begins.
    /// </summary>
    public void StartFlushSequence(System.Action onComplete)
    {
        _onComplete = onComplete;
        State = FlushState.ShowingFaces;
        _timer = faceShowDuration;

        // Play the real toilet flush sound when the sequence begins
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayToiletFlush();

        if (_cam != null)
        {
            _originalCamPos = _cam.transform.position;
            _originalCamRot = _cam.transform.rotation;
        }

        // Position camera looking down into toilet
        if (_pipeCam != null) _pipeCam.enabled = false;
        if (_cam != null)
        {
            _cam.transform.position = new Vector3(0, 5f, -2f);
            _cam.transform.rotation = Quaternion.Euler(70f, 0, 0);
        }

        SetCountdownText("");
        SetWhirlAlpha(0f);
        SetVignetteAlpha(0.6f);
    }

    private System.Action _onComplete;

    void Update()
    {
        if (State == FlushState.Idle || State == FlushState.Done) return;

        _timer -= Time.unscaledDeltaTime;

        switch (State)
        {
            case FlushState.ShowingFaces:
                // Camera slowly zooms toward the turds
                if (_cam != null)
                {
                    float t = 1f - (_timer / faceShowDuration);
                    _cam.transform.position = Vector3.Lerp(
                        new Vector3(0, 5f, -2f),
                        new Vector3(0, 3f, 0f), t * 0.5f);
                }

                if (_timer <= 0f)
                {
                    State = FlushState.Countdown;
                    _countdownValue = 3;
                    _timer = countdownInterval;
                    _countdownPunchTime = Time.unscaledTime;
                    SetCountdownText("3");
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayCountdownTick();
                    HapticManager.LightTap();
                }
                break;

            case FlushState.Countdown:
                // Punch animation: scale bursts big then settles back
                float punchElapsed = Time.unscaledTime - _countdownPunchTime;
                float punchScale;
                if (punchElapsed < 0.12f)
                    punchScale = 1f + (1f - punchElapsed / 0.12f) * 0.5f; // burst to 1.5x
                else if (punchElapsed < 0.35f)
                    punchScale = 1f + Mathf.Sin((punchElapsed - 0.12f) / 0.23f * Mathf.PI) * 0.08f; // settle wobble
                else
                    punchScale = 1f;
                if (countdownText != null)
                    countdownText.transform.localScale = Vector3.one * punchScale;

                if (_timer <= 0f)
                {
                    _countdownValue--;
                    if (_countdownValue > 0)
                    {
                        _timer = countdownInterval;
                        _countdownPunchTime = Time.unscaledTime;
                        SetCountdownText(_countdownValue.ToString());
                        // Color shifts warmer as countdown approaches 1
                        if (countdownText != null)
                            countdownText.color = Color.Lerp(Color.white,
                                new Color(1f, 0.7f, 0.2f), (3 - _countdownValue) / 2f);
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayCountdownTick();
                        HapticManager.LightTap();
                    }
                    else
                    {
                        State = FlushState.Flushing;
                        _timer = flushDuration;
                        _flushPunchTime = Time.unscaledTime;
                        SetCountdownText("FLUSH!");
                        if (countdownText != null)
                        {
                            countdownText.fontSize = 80;
                            countdownText.color = new Color(0.3f, 0.95f, 1f);
                            countdownText.transform.localScale = Vector3.one * 1.6f; // big burst
                        }
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayFlush();
                        HapticManager.HeavyTap();
                    }
                }
                break;

            case FlushState.Flushing:
                float flushT = 1f - (_timer / flushDuration);
                // Ease-out curve: starts slow, accelerates (feels like gravity pulling)
                float easedT = 1f - (1f - flushT) * (1f - flushT);

                // Whirling water overlay (full alpha for dramatic effect)
                _whirlAngle += Time.unscaledDeltaTime * (200f + flushT * 1000f);
                SetWhirlAlpha(Mathf.Lerp(0f, 1f, flushT));
                if (whirlOverlay != null)
                    whirlOverlay.transform.localRotation = Quaternion.Euler(0, 0, _whirlAngle);

                // Camera dives down into pipe (ease-out for momentum feel)
                if (_cam != null)
                {
                    _cam.transform.position = Vector3.Lerp(
                        new Vector3(0, 3f, 0f),
                        new Vector3(0, -2f, 3f), easedT);
                    _cam.transform.rotation = Quaternion.Slerp(
                        Quaternion.Euler(70f, 0, 0),
                        Quaternion.Euler(0, 0, 0), easedT);
                    _cam.fieldOfView = Mathf.Lerp(68f, 95f, easedT);
                }

                // Screen shake ramps up during flush dive
                if (PipeCamera.Instance != null)
                    PipeCamera.Instance.Shake(0.02f + flushT * 0.08f);

                // Vignette closes in
                SetVignetteAlpha(Mathf.Lerp(0.6f, 1f, flushT));

                // FLUSH! text: punch-scale decay + fade
                float flushTextElapsed = Time.unscaledTime - _flushPunchTime;
                if (countdownText != null)
                {
                    // Scale settles from 1.6 down to 1.0 over first 0.3s
                    float textScale = flushTextElapsed < 0.3f
                        ? Mathf.Lerp(1.6f, 1f, flushTextElapsed / 0.3f)
                        : 1f;
                    countdownText.transform.localScale = Vector3.one * textScale;

                    Color c = countdownText.color;
                    c.a = 1f - flushT;
                    countdownText.color = c;
                }

                if (_timer <= 0f)
                {
                    State = FlushState.Done;
                    SetCountdownText("");
                    SetWhirlAlpha(0f);
                    SetVignetteAlpha(0f);

                    // Restore camera
                    if (_cam != null)
                        _cam.fieldOfView = 68f;
                    if (_pipeCam != null)
                        _pipeCam.enabled = true;

                    _onComplete?.Invoke();
                }
                break;
        }
    }

    void SetCountdownText(string text)
    {
        if (countdownText == null) return;
        countdownText.text = text;
        countdownText.fontSize = 64;
        countdownText.color = Color.white;
        countdownText.transform.localScale = Vector3.one;
        countdownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    void SetWhirlAlpha(float a)
    {
        if (whirlOverlay == null) return;
        Color c = whirlOverlay.color;
        c.a = a;
        whirlOverlay.color = c;
        whirlOverlay.gameObject.SetActive(a > 0.01f);
    }

    void SetVignetteAlpha(float a)
    {
        if (vignetteOverlay == null) return;
        Color c = vignetteOverlay.color;
        c.a = a;
        vignetteOverlay.color = c;
        vignetteOverlay.gameObject.SetActive(a > 0.01f);
    }
}
