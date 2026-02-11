using UnityEngine;

/// <summary>
/// Corn Coin collectible. Spins, bobs, and glows to stand out.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 2.5f;
    public float pulseFrequency = 3f;

    private Vector3 _startLocalPos;
    private float _bobOffset;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";
        _startLocalPos = transform.localPosition;
        _bobOffset = Random.value * Mathf.PI * 2f; // stagger so coins don't bob in sync
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        // Spin faster for more eye-catching rotation
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        // Bob up and down in local space (safe in curved pipes)
        float bob = Mathf.Sin((Time.time + _bobOffset) * bobFrequency * Mathf.PI) * bobAmplitude;
        transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(transform.up) * bob;

        // Pulsing glow intensity
        float pulse = 2f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI) * 1f;
        Color glow = new Color(1f, 0.8f, 0.1f) * pulse;
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

            HapticManager.LightTap();

            Destroy(gameObject);
        }
    }
}
