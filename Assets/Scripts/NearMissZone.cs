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

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayNearMiss(other.transform.position);

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowNearMiss(other.transform.position,
                    ComboSystem.Instance != null ? Mathf.RoundToInt(25 * ComboSystem.Instance.Multiplier) : 25);

            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.15f);
                PipeCamera.Instance.PunchFOV(2f);
            }

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayNearMiss();

            HapticManager.LightTap();
        }
    }
}
