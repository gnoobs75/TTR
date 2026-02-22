using UnityEngine;

/// <summary>
/// Sewer grate that blocks half the pipe cross-section.
/// Forces the player to steer to the open side to pass through.
/// Can block left, right, top, or bottom half.
/// Rattles and sparks when the player approaches.
/// </summary>
public class GrateBehavior : MonoBehaviour
{
    public enum BlockSide { Left, Right, Top, Bottom }

    [Header("Settings")]
    public BlockSide blockSide = BlockSide.Left;
    public float damageSpeedMult = 0.4f;

    private Transform _player;
    private bool _playerHit;
    private float _rattleTimer;
    private Vector3 _originalPos;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _lastRattleSoundTime = -10f;
    private bool _rattleSoundPlayed;

    void Start()
    {
        _originalPos = transform.localPosition;
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false; // solid collision
            gameObject.tag = "Obstacle";
        }

        if (GameManager.Instance != null && GameManager.Instance.player != null)
            _player = GameManager.Instance.player.transform;
    }

    void Update()
    {
        if (_player == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.player != null)
                _player = GameManager.Instance.player.transform;
            return;
        }

        float distSqr = (_player.position - transform.position).sqrMagnitude;

        // Rattle when player is close
        if (distSqr < 20f * 20f)
        {
            float intensity = 1f - Mathf.Sqrt(distSqr) / 20f;
            _rattleTimer += Time.deltaTime * (8f + intensity * 12f);
            float rattle = Mathf.Sin(_rattleTimer) * 0.03f * intensity;
            transform.localPosition = _originalPos + transform.right * rattle;

            // Emission pulse - warning glow
            float glow = 0.3f + intensity * 0.5f;
            Color warnColor = Color.Lerp(
                new Color(0.3f, 0.25f, 0.2f),
                new Color(1f, 0.4f, 0.1f),
                intensity) * glow;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", warnColor);
                r.SetPropertyBlock(_mpb);
            }

            // Metallic rattle sound when player gets close (one-shot per approach)
            if (intensity > 0.5f && !_rattleSoundPlayed && Time.time - _lastRattleSoundTime > 4f)
            {
                _rattleSoundPlayed = true;
                _lastRattleSoundTime = Time.time;
                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayBarrelBeep(); // metallic clang warning
                HapticManager.LightTap();
            }
        }
        else
        {
            _rattleSoundPlayed = false; // reset when player moves away
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_playerHit) return;
        if (!collision.collider.CompareTag("Player")) return;

        _playerHit = true;
#if UNITY_EDITOR
        Debug.Log($"[GRATE] Player hit grate at {transform.position}");
#endif

        TurdController tc = collision.collider.GetComponent<TurdController>();
        if (tc != null)
        {
            tc.TakeHit(null);

            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.5f);

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayObstacleHit();

            HapticManager.HeavyTap();
        }

        // Reset after a bit so it can hit again on revisit
        Invoke(nameof(ResetHit), 2f);
    }

    void ResetHit()
    {
        _playerHit = false;
    }
}
