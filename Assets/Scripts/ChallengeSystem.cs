using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daily challenge system. Generates a daily challenge based on the date.
/// Tracks progress during runs and awards bonus coins on completion.
/// </summary>
public class ChallengeSystem : MonoBehaviour
{
    public static ChallengeSystem Instance { get; private set; }

    public enum ChallengeType
    {
        TravelDistance,
        CollectCoins,
        NearMisses,
        ReachCombo,
        ReachScore
    }

    [System.Serializable]
    public struct Challenge
    {
        public ChallengeType type;
        public int target;
        public int reward;
        public string description;
    }

    [Header("UI")]
    public Text challengeText;

    public Challenge TodaysChallenge { get; private set; }
    public int Progress { get; private set; }
    public bool Completed { get; private set; }

    private string _todayKey;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        GenerateDailyChallenge();
    }

    void GenerateDailyChallenge()
    {
        // Deterministic daily challenge based on date
        System.DateTime today = System.DateTime.Now.Date;
        _todayKey = "Challenge_" + today.ToString("yyyyMMdd");
        int seed = today.Year * 10000 + today.Month * 100 + today.Day;

        // Check if already completed today
        Completed = PlayerPrefs.GetInt(_todayKey + "_Done", 0) == 1;
        Progress = PlayerPrefs.GetInt(_todayKey + "_Progress", 0);

        // Generate challenge from seed
        System.Random rng = new System.Random(seed);
        int typeIdx = rng.Next(5);
        ChallengeType type = (ChallengeType)typeIdx;

        Challenge c = new Challenge();
        c.type = type;

        switch (type)
        {
            case ChallengeType.TravelDistance:
                c.target = (rng.Next(3) + 1) * 100; // 100, 200, or 300m
                c.reward = c.target / 2;
                c.description = $"Travel {c.target}m in a single run";
                break;
            case ChallengeType.CollectCoins:
                c.target = (rng.Next(3) + 1) * 10; // 10, 20, or 30 coins
                c.reward = c.target * 3;
                c.description = $"Collect {c.target} coins in a single run";
                break;
            case ChallengeType.NearMisses:
                c.target = rng.Next(3) + 3; // 3, 4, or 5 near misses
                c.reward = c.target * 20;
                c.description = $"Get {c.target} near misses in a single run";
                break;
            case ChallengeType.ReachCombo:
                c.target = rng.Next(3) + 3; // 3, 4, or 5x combo
                c.reward = c.target * 25;
                c.description = $"Reach a {c.target}x combo";
                break;
            case ChallengeType.ReachScore:
                c.target = (rng.Next(4) + 1) * 500; // 500-2000 score
                c.reward = c.target / 5;
                c.description = $"Score {c.target} points in a single run";
                break;
        }

        TodaysChallenge = c;
        UpdateUI();
    }

    /// <summary>Call at end of each run to check challenge progress.</summary>
    public void CheckRun(int coins, float distance, int nearMisses, int bestCombo, int score)
    {
        if (Completed) return;

        int runProgress = 0;
        switch (TodaysChallenge.type)
        {
            case ChallengeType.TravelDistance:
                runProgress = Mathf.FloorToInt(distance);
                break;
            case ChallengeType.CollectCoins:
                runProgress = coins;
                break;
            case ChallengeType.NearMisses:
                runProgress = nearMisses;
                break;
            case ChallengeType.ReachCombo:
                runProgress = bestCombo;
                break;
            case ChallengeType.ReachScore:
                runProgress = score;
                break;
        }

        if (runProgress > Progress)
        {
            Progress = runProgress;
            PlayerPrefs.SetInt(_todayKey + "_Progress", Progress);
        }

        if (Progress >= TodaysChallenge.target && !Completed)
        {
            Completed = true;
            PlayerPrefs.SetInt(_todayKey + "_Done", 1);
            PlayerPrefs.Save();

            // Award bonus coins
            PlayerData.AddCoins(TodaysChallenge.reward);

            Debug.Log($"TTR: Daily challenge completed! +{TodaysChallenge.reward} coins");
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (challengeText == null) return;

        if (Completed)
        {
            challengeText.text = "DAILY: COMPLETE!";
            challengeText.color = new Color(0.3f, 1f, 0.3f);
        }
        else
        {
            int pct = TodaysChallenge.target > 0
                ? Mathf.FloorToInt((float)Progress / TodaysChallenge.target * 100f)
                : 0;
            challengeText.text = $"DAILY: {TodaysChallenge.description} ({pct}%)  +{TodaysChallenge.reward}";
            challengeText.color = new Color(1f, 0.85f, 0.3f);
        }
    }
}
