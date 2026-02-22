using UnityEngine;

/// <summary>
/// Fartcoin collectible. Stands upright and spins like Sonic rings.
/// Big, bright, and unmissable Brown Town currency.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    public float rotateSpeed = 360f;  // Full rotation per second - fast spin like Sonic rings
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2f;
    public float pulseFrequency = 3f;
    public float floatHeight = 0.08f;  // How high above surface to float

    private Vector3 _startLocalPos;
    private float _bobOffset;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Transform _halo;
    private Quaternion _baseRotation;
    private bool _started;

    // Coin magnetism: pull toward player when close
    private Transform _player;
    private const float MAGNET_RANGE = 3.5f;
    private const float MAGNET_SPEED = 12f;
    private bool _magnetActive;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";

        // Float the coin up off the surface
        transform.position += transform.up * floatHeight;

        _startLocalPos = transform.localPosition;
        _bobOffset = Random.value * Mathf.PI * 2f;
        _renderers = GetComponentsInChildren<Renderer>();
        Transform h = transform.Find("GlowHalo");
        if (h != null) _halo = h;

        // Save the spawn rotation so we can add standing-up tilt + spin on top
        _baseRotation = transform.rotation;

        // Cache player reference for magnetism
        var tc = Object.FindFirstObjectByType<TurdController>();
        if (tc != null) _player = tc.transform;

        _started = true;
    }

    void Update()
    {
        if (!_started) return;
        // Stand coin upright (90° tilt) then spin like a Sonic ring
        // The tilt makes the flat disc face perpendicular to the pipe surface
        // The spin rotates it around the forward axis so the player sees the face flash by
        float spin = Time.time * rotateSpeed;
        transform.rotation = _baseRotation * Quaternion.Euler(90f, spin, 0f);

        // Pronounced bob up and down
        float bob = Mathf.Sin((Time.time + _bobOffset) * bobFrequency * Mathf.PI) * bobAmplitude;
        if (transform.parent != null)
            transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(Vector3.up) * bob;

        // Coin magnetism: pull toward player when in range
        float proximity = 0f;
        if (_player != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist < MAGNET_RANGE)
            {
                proximity = 1f - (dist / MAGNET_RANGE); // 0 at edge, 1 when touching
                // Accelerating pull: slow at edge, fast when close
                float pullStrength = proximity * proximity * MAGNET_SPEED;
                Vector3 toPlayer = (_player.position - transform.position).normalized;
                transform.position += toPlayer * pullStrength * Time.deltaTime;
                _magnetActive = true;
                // Override bob when magnetized - coin flies straight at player
                if (proximity > 0.5f)
                    _startLocalPos = transform.localPosition;
            }
        }

        // Glow pulse + proximity intensification
        float pulse = 0.6f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI) * 0.3f;
        // Coins glow brighter as player gets closer (beckoning effect)
        float proximityBoost = 1f + proximity * 1.5f;
        Color glow = new Color(1f, 0.85f, 0.1f) * pulse * proximityBoost;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            r.SetPropertyBlock(_mpb);
        }

        // Halo breathe effect (bigger when player is close)
        if (_halo != null)
        {
            float haloBase = 1.0f + proximity * 0.3f;
            float haloScale = haloBase + Mathf.Sin(Time.time * 2.5f + _bobOffset) * 0.15f;
            _halo.localScale = new Vector3(haloScale, 0.01f, haloScale);
        }

        // Spin faster when being magnetized (excited coin!)
        if (_magnetActive)
        {
            float extraSpin = Time.time * rotateSpeed * (1f + proximity * 2f);
            transform.rotation = _baseRotation * Quaternion.Euler(90f, extraSpin, 0f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.CollectCoin();

            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowCoin(transform.position, GameManager.Instance != null ? GameManager.Instance.scorePerCoin : 10);

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.CoinCollect);

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayCoinCollect(transform.position);

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCoinCollect();

            if (TutorialOverlay.Instance != null)
                TutorialOverlay.Instance.OnFirstCoin();

            HapticManager.LightTap();

            Destroy(gameObject);
        }
    }
}
