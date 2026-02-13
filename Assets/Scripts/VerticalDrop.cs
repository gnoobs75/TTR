using UnityEngine;

/// <summary>
/// Trigger zone that sends the player into a vertical freefall drop.
/// The turd detaches from the pipe surface and free-falls with 2D movement controls.
/// Collect DropRings during the fall, speed boost at the bottom.
/// Spawned after 200m, every 400-600m.
/// </summary>
public class VerticalDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    public float dropDuration = 12f;       // 10-15 seconds of freefall
    public float dropSpeed = 18f;          // how fast the player advances along path during drop
    public float moveRadius = 2.5f;        // how far player can move from pipe center
    public float moveSpeed = 8f;           // 2D movement responsiveness
    public float exitSpeedBoost = 1.4f;    // speed multiplier on exit
    public float exitBoostDuration = 3f;

    [Header("Ring Spawning")]
    public int ringCount = 20;
    public float ringSpacing = 8f;
    public float ringRadius = 2f;
    public GameObject ringPrefab;

    private bool _triggered;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc == null) return;

        _triggered = true;

        // Spawn rings ahead along the path
        SpawnDropRings(tc.DistanceTraveled + 10f);

        // Tell TurdController to enter drop state
        tc.EnterDrop(dropDuration, dropSpeed, moveRadius, moveSpeed, exitSpeedBoost, exitBoostDuration);

        // Dramatic entry
        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.3f);
            PipeCamera.Instance.PunchFOV(10f);
        }

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySpeedBoost();

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowMilestone(tc.transform.position + Vector3.up * 2f, "FREEFALL!");

        HapticManager.HeavyTap();
    }

    void SpawnDropRings(float startDist)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null || ringPrefab == null) return;

        for (int i = 0; i < ringCount; i++)
        {
            float dist = startDist + i * ringSpacing;
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            // Place rings in patterns within the pipe cross-section
            float angle = (i * 137.5f) * Mathf.Deg2Rad; // golden angle spiral
            float r = ringRadius * (0.3f + 0.7f * ((i % 3) / 2f));
            Vector3 offset = right * Mathf.Cos(angle) * r + up * Mathf.Sin(angle) * r;

            Vector3 pos = center + offset;
            Quaternion rot = Quaternion.LookRotation(forward, up);
            Instantiate(ringPrefab, pos, rot, transform);
        }
    }
}
