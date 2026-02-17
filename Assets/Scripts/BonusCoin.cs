using UnityEngine;

/// <summary>
/// Special bonus Fartcoin that spawns after jump ramps. Only reachable mid-air.
/// Worth 10 regular Fartcoins. Extra big, extra shiny, extra satisfying.
/// Stands upright and spins like a giant Sonic ring.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BonusCoin : MonoBehaviour
{
    public int coinValue = 10;
    public float rotateSpeed = 540f; // Spins even faster than normal coins
    public float bobAmplitude = 0.35f;
    public float bobFrequency = 1.5f;
    public float pulseFrequency = 4f;

    private Vector3 _startLocalPos;
    private float _bobOffset;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Quaternion _baseRotation;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";
        _startLocalPos = transform.localPosition;
        _bobOffset = Random.value * Mathf.PI * 2f;
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();

        // Save spawn rotation for standing-up tilt + spin
        _baseRotation = transform.rotation;
    }

    void Update()
    {
        // Stand upright and spin like a giant golden Sonic ring
        float spin = Time.time * rotateSpeed;
        transform.rotation = _baseRotation * Quaternion.Euler(90f, spin, 0f);

        // Big dramatic bob
        float bob = Mathf.Sin((Time.time + _bobOffset) * bobFrequency * Mathf.PI) * bobAmplitude;
        if (transform.parent != null)
            transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(Vector3.up) * bob;

        // Subtle rainbow shimmer glow
        float pulse = 0.8f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI) * 0.4f;
        float hue = (Time.time * 0.3f) % 1f;
        Color baseGlow = Color.HSVToRGB(hue, 0.3f, 1f);
        Color glow = baseGlow * pulse;

        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            r.SetPropertyBlock(_mpb);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Worth 10 regular coins!
            if (GameManager.Instance != null)
            {
                for (int i = 0; i < coinValue; i++)
                    GameManager.Instance.CollectCoin();
            }

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(transform.position, $"BONUS! +{coinValue} FARTCOINS!");

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.CoinCollect);

            if (ParticleManager.Instance != null)
            {
                ParticleManager.Instance.PlayCoinCollect(transform.position);
                ParticleManager.Instance.PlayCelebration(transform.position);
            }

            if (ProceduralAudio.Instance != null)
            {
                ProceduralAudio.Instance.PlayCoinCollect();
                ProceduralAudio.Instance.PlayCelebration();
            }

            if (PipeCamera.Instance != null)
                PipeCamera.Instance.PunchFOV(5f);

            HapticManager.HeavyTap();

            Destroy(gameObject);
        }
    }
}
