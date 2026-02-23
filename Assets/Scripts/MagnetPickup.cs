using UnityEngine;

/// <summary>
/// Golden magnet power-up. Activates coin attraction field for a duration.
/// Spins with golden energy, horseshoe-magnet shaped (built from primitives).
/// </summary>
public class MagnetPickup : MonoBehaviour
{
    public float duration = 8f;
    public float spinSpeed = 150f;
    public float bobAmplitude = 0.2f;
    public float bobSpeed = 2f;
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
                r.material.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 2f);
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

        float spin = Time.time * spinSpeed;
        transform.rotation = _baseRotation * Quaternion.Euler(90f, spin, 0f);

        float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + _bobPhase) * bobAmplitude;
        if (transform.parent != null)
            transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(Vector3.up) * bob;

        float pulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
        foreach (var r in _renderers)
        {
            if (r != null && r.material != null)
                r.material.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * (1.5f + pulse * 2.5f));
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
            Debug.Log($"[BOOST] MagnetPickup collected at dist={tc.DistanceTraveled:F0}");
#endif
            if (TutorialOverlay.Instance != null)
                TutorialOverlay.Instance.OnFirstPowerUp();
            tc.ActivateCoinMagnet(duration);
            _used = true;

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(transform.position + Vector3.up * 1.5f, "MAGNET!");

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayCoinCollect(transform.position);

            foreach (var r in _renderers)
            {
                if (r != null)
                    r.material.SetColor("_EmissionColor", Color.white * 8f);
            }

            Destroy(gameObject, 0.3f);
        }
    }
}
