using UnityEngine;

/// <summary>
/// Big Air ramp at pipe breaks. Launches the turd into a long 5-6 second
/// arc over a gap in the sewer pipe. Plenty of time for tricks and flips.
/// Spawned every 300-500m, with dramatic pipe break visuals.
/// </summary>
public class BigAirRamp : MonoBehaviour
{
    [Header("Launch")]
    public float launchHeight = 6f;
    public float arcDuration = 5.5f;

    [Header("Visuals")]
    public float warningDistance = 30f;

    private Renderer[] _arrowRenderers;
    private float _pulseTimer;
    private bool _launched;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Find arrow chevron renderers for animation
        var arrows = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("Arrow") || r.gameObject.name.StartsWith("Chevron"))
                arrows.Add(r);
        }
        _arrowRenderers = arrows.ToArray();
    }

    void Update()
    {
        if (_arrowRenderers == null || _arrowRenderers.Length == 0) return;

        _pulseTimer += Time.deltaTime;

        // Faster, more dramatic chase pattern than regular ramps
        for (int i = 0; i < _arrowRenderers.Length; i++)
        {
            int pairIndex = i / 2;
            float phase = (_pulseTimer * 4f - pairIndex * 0.3f) % 1f;
            float brightness = Mathf.Max(0.2f, Mathf.Pow(Mathf.Max(0, 1f - phase * 2f), 2f));
            // Hot orange-red for danger ramp
            Color emitColor = new Color(1f, 0.5f, 0.1f) * (3f * brightness);

            if (_arrowRenderers[i] != null && _arrowRenderers[i].material != null)
                _arrowRenderers[i].material.SetColor("_EmissionColor", emitColor);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_launched) return;
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc != null)
        {
            _launched = true;
            tc.LaunchJump(launchHeight, arcDuration);

            // Extra juice for big air
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.4f);
                PipeCamera.Instance.PunchFOV(8f);
            }

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(tc.transform.position + Vector3.up * 2f, "BIG AIR!");

            HapticManager.HeavyTap();
        }
    }
}
