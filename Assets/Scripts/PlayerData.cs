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

    /// <summary>Reset all data (for debugging).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
