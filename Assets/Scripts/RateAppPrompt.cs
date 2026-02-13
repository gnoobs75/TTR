using UnityEngine;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

/// <summary>
/// Prompts the player to rate the app at the right moment.
/// Uses Apple's SKStoreReviewController (shows at most 3 times per year).
/// Triggers after a good run (new high score or 5+ runs).
/// </summary>
public class RateAppPrompt : MonoBehaviour
{
    public static RateAppPrompt Instance { get; private set; }

    private const string PREFS_KEY_PROMPTED = "RateApp_Prompted";
    private const string PREFS_KEY_RUNS_SINCE = "RateApp_RunsSince";
    private const int MIN_RUNS_BEFORE_PROMPT = 5;

    private bool _alreadyPrompted;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _alreadyPrompted = PlayerPrefs.GetInt(PREFS_KEY_PROMPTED, 0) == 1;
    }

    /// Call after each run ends. Decides whether to show the rate prompt.
    public void OnRunEnd(int score, float distance)
    {
        if (_alreadyPrompted) return;

        int runsSince = PlayerPrefs.GetInt(PREFS_KEY_RUNS_SINCE, 0) + 1;
        PlayerPrefs.SetInt(PREFS_KEY_RUNS_SINCE, runsSince);

        bool isNewHighScore = score >= PlayerData.HighScore && score > 0;
        bool enoughRuns = runsSince >= MIN_RUNS_BEFORE_PROMPT;

        // Prompt on a new high score after enough runs, or after many runs
        if ((isNewHighScore && enoughRuns) || runsSince >= MIN_RUNS_BEFORE_PROMPT * 2)
        {
            RequestReview();
        }
    }

    void RequestReview()
    {
        _alreadyPrompted = true;
        PlayerPrefs.SetInt(PREFS_KEY_PROMPTED, 1);
        PlayerPrefs.Save();

#if UNITY_IOS && !UNITY_EDITOR
        Device.RequestStoreReview();
        Debug.Log("TTR: Requested App Store review");
#else
        Debug.Log("TTR: Rate app prompt (iOS only)");
#endif
    }
}
