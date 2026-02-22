using UnityEngine;

/// <summary>
/// Attach to a child of an obstacle with a larger trigger collider.
/// When the player passes through the outer zone without hitting
/// the obstacle's inner collider, that's a near miss.
/// Auto-created by Obstacle.Start().
/// </summary>
public class NearMissZone : MonoBehaviour
{
    private bool _playerInside = false;
    private bool _scored = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !_playerInside || _scored) return;

        _playerInside = false;

        // If game is still playing, player dodged the obstacle = near miss
        if (GameManager.Instance != null && GameManager.Instance.isPlaying)
        {
            _scored = true;
            GameManager.Instance.RecordNearMiss();

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.NearMiss);

            int streak = GameManager.Instance.NearMissStreak;
            float mult = ComboSystem.Instance != null ? ComboSystem.Instance.Multiplier : 1f;

            // Escalating near-miss streak rewards
            int baseBonus = 25;
            float streakMult = 1f + Mathf.Min(streak, 15) * 0.15f; // up to 3.25x at 15 streak
            int totalBonus = Mathf.RoundToInt(baseBonus * mult * streakMult);

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayNearMiss(other.transform.position);

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowNearMiss(other.transform.position, totalBonus);

            // Escalating camera juice based on streak
            float shakeStr = 0.15f + Mathf.Min(streak, 10) * 0.02f;
            float fovPunch = 2f + Mathf.Min(streak, 10) * 0.3f;
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(shakeStr);
                PipeCamera.Instance.PunchFOV(fovPunch);
            }

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayNearMiss();

            // Streak milestones with dramatic announcements
            if (streak == 3 || streak == 5 || streak == 7 || streak == 10 || streak == 15)
            {
                string title = streak switch
                {
                    3 => "CLOSE SHAVE!",
                    5 => "DEATH WISH!",
                    7 => "UNTOUCHABLE!",
                    10 => "DODGE GOD!",
                    _ => "IMMORTAL!"
                };
                Color col = streak switch
                {
                    3 => new Color(0.5f, 1f, 0.5f),
                    5 => new Color(1f, 1f, 0.3f),
                    7 => new Color(1f, 0.6f, 0.2f),
                    10 => new Color(1f, 0.3f, 0.8f),
                    _ => new Color(1f, 0.2f, 0.2f)
                };

                if (ScorePopup.Instance != null)
                    ScorePopup.Instance.ShowMilestone(
                        other.transform.position + Vector3.up * 2f,
                        $"{title}\n{streak}x DODGE STREAK!");

                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer(title, col, streak >= 7);

                if (ScreenEffects.Instance != null)
                    ScreenEffects.Instance.TriggerMilestoneFlash();

                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayCelebration();

                // Bonus score for hitting streak milestones
                int streakBonus = streak * 50;
                GameManager.Instance.AddScore(streakBonus);

                HapticManager.MediumTap();
            }
            else
            {
                HapticManager.LightTap();
            }
        }
    }
}
