using UnityEngine;

/// <summary>
/// Persistent player data using PlayerPrefs.
/// Tracks wallet, stats, and high scores across sessions.
/// </summary>
public static class PlayerData
{
    // Keys
    const string KEY_WALLET = "CoinWallet";
    const string KEY_HIGH_SCORE = "HighScore";
    const string KEY_BEST_DISTANCE = "BestDistance";
    const string KEY_TOTAL_DISTANCE = "TotalDistance";
    const string KEY_TOTAL_RUNS = "TotalRuns";
    const string KEY_TOTAL_COINS = "TotalCoinsEver";
    const string KEY_TOTAL_NEAR_MISSES = "TotalNearMisses";
    const string KEY_BEST_COMBO = "BestCombo";
    const string KEY_SELECTED_SKIN = "SelectedSkin";

    // Mode-specific keys
    const string KEY_ENDLESS_HIGH_SCORE = "EndlessHighScore";
    const string KEY_ENDLESS_BEST_DISTANCE = "EndlessBestDistance";
    const string KEY_RACE_HIGH_SCORE = "RaceHighScore";
    const string KEY_RACE_BEST_TIME = "RaceBestTime";
    const string KEY_RACE_BEST_PLACE = "RaceBestPlace";
    const string KEY_RACE_WINS = "RaceWins";

    // === WALLET ===
    public static int Wallet
    {
        get => PlayerPrefs.GetInt(KEY_WALLET, 0);
        set { PlayerPrefs.SetInt(KEY_WALLET, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static void AddCoins(int amount)
    {
        Wallet += amount;
        TotalCoinsEver += amount;
    }

    public static bool SpendCoins(int amount)
    {
        if (Wallet < amount) return false;
        Wallet -= amount;
        return true;
    }

    // === HIGH SCORES ===
    public static int HighScore
    {
        get => PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0);
        set { PlayerPrefs.SetInt(KEY_HIGH_SCORE, Mathf.Max(PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0), value)); PlayerPrefs.Save(); }
    }

    public static float BestDistance
    {
        get => PlayerPrefs.GetFloat(KEY_BEST_DISTANCE, 0f);
        set { PlayerPrefs.SetFloat(KEY_BEST_DISTANCE, Mathf.Max(PlayerPrefs.GetFloat(KEY_BEST_DISTANCE, 0f), value)); PlayerPrefs.Save(); }
    }

    public static int BestCombo
    {
        get => PlayerPrefs.GetInt(KEY_BEST_COMBO, 0);
        set { PlayerPrefs.SetInt(KEY_BEST_COMBO, Mathf.Max(PlayerPrefs.GetInt(KEY_BEST_COMBO, 0), value)); PlayerPrefs.Save(); }
    }

    // === LIFETIME STATS ===
    public static int TotalRuns
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_RUNS, 0);
        set { PlayerPrefs.SetInt(KEY_TOTAL_RUNS, value); PlayerPrefs.Save(); }
    }

    public static float TotalDistance
    {
        get => PlayerPrefs.GetFloat(KEY_TOTAL_DISTANCE, 0f);
        set { PlayerPrefs.SetFloat(KEY_TOTAL_DISTANCE, value); PlayerPrefs.Save(); }
    }

    public static int TotalCoinsEver
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_COINS, 0);
        set { PlayerPrefs.SetInt(KEY_TOTAL_COINS, value); PlayerPrefs.Save(); }
    }

    public static int TotalNearMisses
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_NEAR_MISSES, 0);
        set { PlayerPrefs.SetInt(KEY_TOTAL_NEAR_MISSES, value); PlayerPrefs.Save(); }
    }

    // === COSMETICS ===
    public static string SelectedSkin
    {
        get => PlayerPrefs.GetString(KEY_SELECTED_SKIN, "MrCorny");
        set { PlayerPrefs.SetString(KEY_SELECTED_SKIN, value); PlayerPrefs.Save(); }
    }

    public static bool IsSkinUnlocked(string skinId)
    {
        if (skinId == "MrCorny") return true; // Default always unlocked
        return PlayerPrefs.GetInt("Skin_" + skinId, 0) == 1;
    }

    public static void UnlockSkin(string skinId)
    {
        PlayerPrefs.SetInt("Skin_" + skinId, 1);
        PlayerPrefs.Save();
    }

    // === MODE-SPECIFIC STATS ===
    public static int EndlessHighScore
    {
        get => PlayerPrefs.GetInt(KEY_ENDLESS_HIGH_SCORE, 0);
        set { PlayerPrefs.SetInt(KEY_ENDLESS_HIGH_SCORE, Mathf.Max(PlayerPrefs.GetInt(KEY_ENDLESS_HIGH_SCORE, 0), value)); PlayerPrefs.Save(); }
    }

    public static float EndlessBestDistance
    {
        get => PlayerPrefs.GetFloat(KEY_ENDLESS_BEST_DISTANCE, 0f);
        set { PlayerPrefs.SetFloat(KEY_ENDLESS_BEST_DISTANCE, Mathf.Max(PlayerPrefs.GetFloat(KEY_ENDLESS_BEST_DISTANCE, 0f), value)); PlayerPrefs.Save(); }
    }

    public static int RaceHighScore
    {
        get => PlayerPrefs.GetInt(KEY_RACE_HIGH_SCORE, 0);
        set { PlayerPrefs.SetInt(KEY_RACE_HIGH_SCORE, Mathf.Max(PlayerPrefs.GetInt(KEY_RACE_HIGH_SCORE, 0), value)); PlayerPrefs.Save(); }
    }

    /// <summary>Best race time (lower is better). 0 = no recorded time.</summary>
    public static float RaceBestTime
    {
        get => PlayerPrefs.GetFloat(KEY_RACE_BEST_TIME, 0f);
        set
        {
            float current = PlayerPrefs.GetFloat(KEY_RACE_BEST_TIME, 0f);
            if (current <= 0f || value < current)
            {
                PlayerPrefs.SetFloat(KEY_RACE_BEST_TIME, value);
                PlayerPrefs.Save();
            }
        }
    }

    public static int RaceBestPlace
    {
        get => PlayerPrefs.GetInt(KEY_RACE_BEST_PLACE, 0);
        set
        {
            int current = PlayerPrefs.GetInt(KEY_RACE_BEST_PLACE, 0);
            if (current <= 0 || value < current)
            {
                PlayerPrefs.SetInt(KEY_RACE_BEST_PLACE, value);
                PlayerPrefs.Save();
            }
        }
    }

    public static int RaceWins
    {
        get => PlayerPrefs.GetInt(KEY_RACE_WINS, 0);
        set { PlayerPrefs.SetInt(KEY_RACE_WINS, value); PlayerPrefs.Save(); }
    }

    // === RUN TRACKING ===
    /// <summary>Call at end of each run to update all lifetime stats.</summary>
    public static void RecordRun(int coinsCollected, float distance, int score, int nearMisses, int bestCombo)
    {
        TotalRuns++;
        TotalDistance += distance;
        AddCoins(coinsCollected);
        HighScore = score;
        BestDistance = distance;
        BestCombo = bestCombo;
        TotalNearMisses += nearMisses;
    }

    /// <summary>Record an endless mode run (updates mode-specific stats).</summary>
    public static void RecordEndlessRun(int coinsCollected, float distance, int score, int nearMisses, int bestCombo)
    {
        RecordRun(coinsCollected, distance, score, nearMisses, bestCombo);
        EndlessHighScore = score;
        EndlessBestDistance = distance;
    }

    /// <summary>Record a race mode run (updates mode-specific stats).</summary>
    public static void RecordRaceRun(int coinsCollected, float distance, int score, int nearMisses, int bestCombo,
        float raceTime, int finishPlace)
    {
        RecordRun(coinsCollected, distance, score, nearMisses, bestCombo);
        RaceHighScore = score;
        if (raceTime > 0f) RaceBestTime = raceTime;
        if (finishPlace > 0) RaceBestPlace = finishPlace;
        if (finishPlace == 1) RaceWins++;
    }

    /// <summary>Reset all data (for debugging).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
