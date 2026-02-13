using UnityEngine;

/// <summary>
/// Lightweight analytics tracker for game events.
/// Logs locally and can be extended with a backend (Firebase, Unity Analytics, etc.).
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    /// Log the start of a gameplay run
    public void LogRunStart()
    {
        Log("run_start", $"run={PlayerData.TotalRuns + 1}");
    }

    /// Log end-of-run stats
    public void LogRunEnd(int score, float distance, int coins, int nearMisses, int bestCombo)
    {
        Log("run_end",
            $"score={score} dist={distance:F0} coins={coins} " +
            $"near_misses={nearMisses} combo={bestCombo} " +
            $"total_runs={PlayerData.TotalRuns}");
    }

    /// Log zone reached during a run
    public void LogZoneReached(string zoneName, float distance)
    {
        Log("zone_reached", $"zone={zoneName} dist={distance:F0}");
    }

    /// Log skin unlock / purchase
    public void LogSkinUnlock(string skinId)
    {
        Log("skin_unlock", $"skin={skinId}");
    }

    /// Log achievement earned
    public void LogAchievement(string achievementId)
    {
        Log("achievement", $"id={achievementId}");
    }

    /// Log tutorial completion
    public void LogTutorialComplete()
    {
        Log("tutorial_complete", $"run={PlayerData.TotalRuns}");
    }

    /// Log settings change
    public void LogSettingsChange(string setting, string value)
    {
        Log("settings", $"{setting}={value}");
    }

    void Log(string eventName, string data)
    {
        Debug.Log($"TTR Analytics: [{eventName}] {data}");

        // Hook point for real analytics backend:
        // Firebase: FirebaseAnalytics.LogEvent(eventName, ...);
        // Unity Analytics: AnalyticsService.Instance.CustomData(eventName, ...);
    }
}
