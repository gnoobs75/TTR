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
                    SetCountdownText("3");
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayCountdownTick();
                }
                break;

            case FlushState.Countdown:
                // Pulse countdown text
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 15f) * 0.1f;
                if (countdownText != null)
                    countdownText.transform.localScale = Vector3.one * pulse;

                if (_timer <= 0f)
                {
                    _countdownValue--;
                    if (_countdownValue > 0)
                    {
                        _timer = countdownInterval;
                        SetCountdownText(_countdownValue.ToString());
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayCountdownTick();
                    }
                    else
                    {
                        State = FlushState.Flushing;
                        _timer = flushDuration;
                        SetCountdownText("FLUSH!");
                        if (countdownText != null)
                        {
                            countdownText.fontSize = 72;
                            countdownText.color = new Color(0.3f, 0.9f, 1f);
                        }
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayFlush();
                        HapticManager.HeavyTap();
                    }
                }
                break;

            case FlushState.Flushing:
                float flushT = 1f - (_timer / flushDuration);

                // Whirling water overlay
                _whirlAngle += Time.unscaledDeltaTime * (200f + flushT * 800f);
                SetWhirlAlpha(Mathf.Lerp(0f, 0.8f, flushT));
                if (whirlOverlay != null)
                    whirlOverlay.transform.localRotation = Quaternion.Euler(0, 0, _whirlAngle);

                // Camera dives down into pipe
                if (_cam != null)
                {
                    _cam.transform.position = Vector3.Lerp(
                        new Vector3(0, 3f, 0f),
                        new Vector3(0, -2f, 3f), flushT);
                    _cam.transform.rotation = Quaternion.Slerp(
                        Quaternion.Euler(70f, 0, 0),
                        Quaternion.Euler(0, 0, 0), flushT);
                    _cam.fieldOfView = Mathf.Lerp(68f, 90f, flushT);
                }

                // Vignette closes in
                SetVignetteAlpha(Mathf.Lerp(0.6f, 1f, flushT));

                // Fade out text
                if (countdownText != null)
                {
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
