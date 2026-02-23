using UnityEngine;

/// <summary>
/// Purple clock power-up. Slows time for a few seconds, making dodging easier.
/// Spins with purple energy, clock-face visual (built from primitives).
/// </summary>
public class SlowMoPickup : MonoBehaviour
{
    public float duration = 3f;
    public float timeScale = 0.4f;
    public float spinSpeed = 90f;
    public float bobAmplitude = 0.3f;
    public float bobSpeed = 1f;
    public float floatHeight = 0.5f;

    private Renderer[] _renderers;
    private bool _used;
    private Vector3 _startLocalPos;
    private float _bobPhase;
    private Quaternion _baseRotation;

    void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in _renderers)
        {
            if (r != null && r.material != null)
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", new Color(0.6f, 0.2f, 1f) * 2f);
            }
        }

        transform.position += transform.up * floatHeight;
        _startLocalPos = transform.localPosition;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        _baseRotation = transform.rotation;
    }

    void Update()
    {
        if (_used) return;

        // Slow spin (like a clock)
        float spin = Time.time * spinSpeed;
        transform.rotation = _baseRotation * Quaternion.Euler(90f, spin, 0f);

        float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + _bobPhase) * bobAmplitude;
        if (transform.parent != null)
            transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(Vector3.up) * bob;

        // Dreamy slow pulse
        float pulse = (Mathf.Sin(Time.time * 1.5f) + 1f) * 0.5f;
        foreach (var r in _renderers)
        {
            if (r != null && r.material != null)
                r.material.SetColor("_EmissionColor", new Color(0.6f, 0.2f, 1f) * (1.5f + pulse * 3f));
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc != null)
        {
#if UNITY_EDITOR
            Debug.Log($"[BOOST] SlowMoPickup collected at dist={tc.DistanceTraveled:F0}");
#endif
            if (TutorialOverlay.Instance != null)
                TutorialOverlay.Instance.OnFirstPowerUp();
            tc.ActivateSlowMo(duration, timeScale);
            _used = true;

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(transform.position + Vector3.up * 1.5f, "SLOW-MO!");

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayCoinCollect(transform.position);

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayZoneTransition();

            foreach (var r in _renderers)
            {
                if (r != null)
                    r.material.SetColor("_EmissionColor", Color.white * 8f);
            }

            Destroy(gameObject, 0.3f);
        }
    }
}
