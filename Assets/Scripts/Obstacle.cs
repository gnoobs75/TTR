using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Obstacle that slows/stuns the player on contact (Mario Kart style).
/// Auto-creates a near-miss detection zone around itself.
/// Maintains a static registry for O(1) obstacle lookup (no FindObjectsByType).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    public float rotateSpeed = 30f;
    public bool doesRotate = false;

    [Header("Near Miss")]
    public float nearMissMultiplier = 1.8f;

    // Static registry — obstacles register/unregister themselves
    private static readonly HashSet<Obstacle> _allObstacles = new HashSet<Obstacle>();
    public static IReadOnlyCollection<Obstacle> AllObstacles => _allObstacles;

    private ObstacleBehavior _cachedBehavior;
    private bool _hasBehavior;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Obstacle";
        _cachedBehavior = GetComponent<ObstacleBehavior>();
        _hasBehavior = _cachedBehavior != null;
        CreateNearMissZone();
    }

    void OnEnable()
    {
        _allObstacles.Add(this);
    }

    void OnDisable()
    {
        _allObstacles.Remove(this);
    }

    void CreateNearMissZone()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var r in GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);

        float radius = bounds.extents.magnitude * nearMissMultiplier;
        if (radius < 0.5f) radius = 1.5f;

        GameObject zone = new GameObject("NearMissZone");
        zone.transform.SetParent(transform);
        zone.transform.localPosition = Vector3.zero;
        zone.layer = gameObject.layer;

        SphereCollider sc = zone.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = radius;

        zone.AddComponent<NearMissZone>();
    }

    void Update()
    {
        if (doesRotate && !_hasBehavior)
            transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc == null) return;

        // === STOMP CHECK: If player is jumping, stomp the obstacle! ===
        if (tc.IsJumping)
        {
            // STOMP! Squash this obstacle and bounce
            if (_cachedBehavior != null)
                _cachedBehavior.OnStomped(other.transform);

            tc.StompBounce();

            // Squash VFX (obstacle-colored burst for variety)
            if (ParticleManager.Instance != null)
            {
                Color stompColor = _cachedBehavior != null ? _cachedBehavior.HitFlashColor : Color.white;
                ParticleManager.Instance.PlayHitExplosion(transform.position, stompColor);
                ParticleManager.Instance.PlayStompSquash(transform.position, stompColor);
            }

            // Disable collider so it can't hit player again
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

#if UNITY_EDITOR
            Debug.Log($"[STOMP] {gameObject.name} stomped at pos={transform.position:F1} dist={tc.DistanceTraveled:F0}m");
            ObstacleSpawner spawner = Object.FindFirstObjectByType<ObstacleSpawner>();
            if (spawner != null) spawner.RecordCollision(gameObject.name, true);
#endif

            // Destroy after squash animation plays
            Destroy(gameObject, 1.5f);
            return;
        }

        // Don't hit if already stunned or invincible
        if (tc.IsInvincible) return;

        // Apply stun to player
        tc.TakeHit(_cachedBehavior);

        // Tell the obstacle to play its unique hit animation
        if (_cachedBehavior != null)
            _cachedBehavior.OnPlayerHit(other.transform);

#if UNITY_EDITOR
        Debug.Log($"[HIT] {gameObject.name} hit player at pos={transform.position:F1} dist={tc.DistanceTraveled:F0}m speed={tc.CurrentSpeed:F1} behavior={_hasBehavior}");
        ObstacleSpawner spawner2 = Object.FindFirstObjectByType<ObstacleSpawner>();
        if (spawner2 != null) spawner2.RecordCollision(gameObject.name, false);
#endif

        // Generic feedback (particles, camera already handled in TakeHit)
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayHitExplosion(other.transform.position);
    }
}
