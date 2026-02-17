using UnityEngine;

/// <summary>
/// Syncs PlayerData to iCloud Key-Value Store for cross-device progress.
/// Falls back to PlayerPrefs-only if iCloud unavailable.
/// Conflict resolution: takes the higher value (max coins, best scores).
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    private const string KEY_WALLET = "cloud_wallet";
    private const string KEY_HIGH_SCORE = "cloud_highscore";
    private const string KEY_BEST_DISTANCE = "cloud_bestdistance";
    private const string KEY_BEST_COMBO = "cloud_bestcombo";
    private const string KEY_TOTAL_RUNS = "cloud_totalruns";
    private const string KEY_TOTAL_DISTANCE = "cloud_totaldistance";
    private const string KEY_TOTAL_COINS = "cloud_totalcoins";
    private const string KEY_UNLOCKED_SKINS = "cloud_skins";
    private const string KEY_SELECTED_SKIN = "cloud_selectedskin";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SyncFromCloud();
    }

    /// Pull cloud data and merge with local (take best values)
    public void SyncFromCloud()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // iCloud KVS is accessed through NSUbiquitousKeyValueStore
        // Unity doesn't have built-in iCloud KVS - would need native plugin
        // For now, use PlayerPrefs as the backing store (works for single-device)
        Debug.Log("TTR: Cloud save sync (PlayerPrefs fallback - iCloud plugin needed for full sync)");
#else
        Debug.Log("TTR: Cloud save not available on this platform");
#endif
    }

    /// Push local data to cloud after each run
    public void SyncToCloud()
    {
        // Save current PlayerData state
        // In production, this would write to iCloud KVS via native plugin
        Debug.Log("TTR: Saving progress locally");

        // PlayerData already saves to PlayerPrefs in its own methods
        // This method exists as a hook point for when iCloud plugin is added
        PlayerPrefs.Save();
    }

    /// Merge cloud values with local, taking the best of each
    void MergeCloudData(int cloudWallet, int cloudHighScore, float cloudBestDist,
                         int cloudBestCombo, int cloudTotalRuns, float cloudTotalDist,
                         int cloudTotalCoins, string cloudSkins)
    {
        // Wallet: take max (player shouldn't lose coins)
        if (cloudWallet > PlayerData.Wallet)
            PlayerData.AddCoins(cloudWallet - PlayerData.Wallet);

        // Scores: take best
        if (cloudHighScore > PlayerData.HighScore)
            PlayerPrefs.SetInt("HighScore", cloudHighScore);
        if (cloudBestDist > PlayerData.BestDistance)
            PlayerPrefs.SetFloat("BestDistance", cloudBestDist);
        if (cloudBestCombo > PlayerData.BestCombo)
            PlayerPrefs.SetInt("BestCombo", cloudBestCombo);

        // Lifetime stats: take highest (they only go up)
        if (cloudTotalRuns > PlayerData.TotalRuns)
            PlayerPrefs.SetInt("TotalRuns", cloudTotalRuns);
        if (cloudTotalDist > PlayerData.TotalDistance)
            PlayerPrefs.SetFloat("TotalDistance", cloudTotalDist);
        if (cloudTotalCoins > PlayerData.TotalCoinsEver)
            PlayerPrefs.SetInt("TotalCoinsEver", cloudTotalCoins);

        // Skins: union of unlocked (never re-lock)
        if (!string.IsNullOrEmpty(cloudSkins))
        {
            string[] skinIds = cloudSkins.Split(',');
            foreach (string id in skinIds)
            {
                string trimmed = id.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !PlayerData.IsSkinUnlocked(trimmed))
                    PlayerData.UnlockSkin(trimmed);
            }
        }

        PlayerPrefs.Save();
    }
}
