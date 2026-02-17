using UnityEngine;
using UnityEngine.SocialPlatforms;
#if UNITY_IOS
using UnityEngine.SocialPlatforms.GameCenter;
#endif

/// <summary>
/// Game Center integration for leaderboards and achievements.
/// Authenticates on launch, submits scores after each run.
/// </summary>
public class GameCenterManager : MonoBehaviour
{
    public static GameCenterManager Instance { get; private set; }

    public bool IsAuthenticated => Social.localUser.authenticated;

    // Leaderboard IDs - must match App Store Connect configuration
    public const string LB_HIGH_SCORE = "com.ttrgames.turdtunnelrush.highscore";
    public const string LB_BEST_DISTANCE = "com.ttrgames.turdtunnelrush.bestdistance";

    // Achievement IDs
    public const string ACH_FIRST_FLUSH = "com.ttrgames.turdtunnelrush.firstflush";
    public const string ACH_COMBO_KING = "com.ttrgames.turdtunnelrush.comboking";
    public const string ACH_ZONE_GRIMY = "com.ttrgames.turdtunnelrush.zone.grimy";
    public const string ACH_ZONE_TOXIC = "com.ttrgames.turdtunnelrush.zone.toxic";
    public const string ACH_ZONE_RUSTY = "com.ttrgames.turdtunnelrush.zone.rusty";
    public const string ACH_ZONE_HELL = "com.ttrgames.turdtunnelrush.zone.hellsewer";
    public const string ACH_FASHIONISTA = "com.ttrgames.turdtunnelrush.fashionista";
    public const string ACH_COLLECTOR = "com.ttrgames.turdtunnelrush.collector";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Authenticate();
    }

    public void Authenticate()
    {
#if UNITY_IOS || UNITY_STANDALONE_OSX
        Social.localUser.Authenticate(success =>
        {
            if (success)
                Debug.Log("TTR: Game Center authenticated: " + Social.localUser.userName);
            else
                Debug.Log("TTR: Game Center auth failed (offline or not signed in)");
        });
#endif
    }

    /// Submit score and distance after each run
    public void ReportScore(int score, float distance)
    {
        if (!IsAuthenticated) return;

        Social.ReportScore(score, LB_HIGH_SCORE, success =>
        {
            if (success) Debug.Log($"TTR: Reported score {score} to leaderboard");
        });

        Social.ReportScore((long)(distance * 100), LB_BEST_DISTANCE, success =>
        {
            if (success) Debug.Log($"TTR: Reported distance {distance:F0}m to leaderboard");
        });
    }

    /// Report achievement progress (0-100)
    public void ReportAchievement(string achievementId, float percentComplete = 100f)
    {
        if (!IsAuthenticated) return;

        Social.ReportProgress(achievementId, percentComplete, success =>
        {
            if (success) Debug.Log($"TTR: Achievement {achievementId} reported ({percentComplete}%)");
        });
    }

    /// Check and report zone achievements based on distance
    public void CheckZoneAchievements(float distance)
    {
        if (distance >= 80f) ReportAchievement(ACH_ZONE_GRIMY);
        if (distance >= 250f) ReportAchievement(ACH_ZONE_TOXIC);
        if (distance >= 500f) ReportAchievement(ACH_ZONE_RUSTY);
        if (distance >= 800f) ReportAchievement(ACH_ZONE_HELL);
    }

    /// Show native leaderboard UI
    public void ShowLeaderboard()
    {
        if (!IsAuthenticated)
        {
            Authenticate();
            return;
        }
        Social.ShowLeaderboardUI();
    }

    /// Show native achievements UI
    public void ShowAchievements()
    {
        if (!IsAuthenticated)
        {
            Authenticate();
            return;
        }
        Social.ShowAchievementsUI();
    }
}
