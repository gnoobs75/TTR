using UnityEngine;

/// <summary>
/// Ramp that launches Mr. Corny into a brief aerial arc inside the pipe.
/// Player detaches from the pipe surface, arcs through the air, then re-attaches.
/// Arrow chevrons pulse with sequential animation for visual flair.
/// </summary>
public class JumpRamp : MonoBehaviour
{
    public float launchHeight = 3.5f;
    public float arcDuration = 1.2f;

    private Renderer[] _arrowRenderers;
    private float _pulseTimer;

    void Start()
    {
        // Find arrow renderers (named ArrowL0/R0, ArrowL1/R1, ArrowL2/R2)
        var arrows = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("Arrow"))
                arrows.Add(r);
        }
        _arrowRenderers = arrows.ToArray();
    }

    void Update()
    {
        if (_arrowRenderers == null || _arrowRenderers.Length == 0) return;

        _pulseTimer += Time.deltaTime;

        // Sequential chase pattern: each arrow pair lights up in sequence
        for (int i = 0; i < _arrowRenderers.Length; i++)
        {
            // Arrows are paired (L0/R0 = pair 0, L1/R1 = pair 1, etc.)
            int pairIndex = i / 2;
            float phase = (_pulseTimer * 3f - pairIndex * 0.4f) % 1f;
            float brightness = Mathf.Max(0.3f, Mathf.Pow(Mathf.Max(0, 1f - phase * 2f), 2f));
            Color emitColor = new Color(1f, 0.85f, 0.1f) * (4f * brightness);

            if (_arrowRenderers[i] != null && _arrowRenderers[i].material != null)
                _arrowRenderers[i].material.SetColor("_EmissionColor", emitColor);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc != null)
        {
            tc.LaunchJump(launchHeight, arcDuration);

            // Launch juice
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayJumpLaunch();

            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(5f);
                PipeCamera.Instance.Shake(0.12f);
            }

            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.FlashSpeedStreaks(0.8f);

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayLandingDust(transform.position);

            HapticManager.MediumTap();

            // Flash arrows bright on launch
            foreach (var r in _arrowRenderers)
            {
                if (r != null && r.material != null)
                    r.material.SetColor("_EmissionColor", Color.white * 8f);
            }
        }
    }
}
