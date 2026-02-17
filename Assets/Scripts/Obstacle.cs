using UnityEngine;

/// <summary>
/// Obstacle that slows/stuns the player on contact (Mario Kart style).
/// Auto-creates a near-miss detection zone around itself.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    public float rotateSpeed = 30f;
    public bool doesRotate = false;

    [Header("Near Miss")]
    public float nearMissMultiplier = 1.8f;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Obstacle";
        CreateNearMissZone();
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
        if (doesRotate && GetComponent<ObstacleBehavior>() == null)
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
            ObstacleBehavior behavior = GetComponent<ObstacleBehavior>();
            if (behavior != null)
                behavior.OnStomped(other.transform);

            tc.StompBounce();

            // Squash VFX
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayHitExplosion(transform.position);

            // Disable collider so it can't hit player again
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Destroy after squash animation plays
            Destroy(gameObject, 1.5f);
            return;
        }

        // Don't hit if already stunned or invincible
        if (tc.IsInvincible) return;

        // Apply stun to player
        ObstacleBehavior behavior2 = GetComponent<ObstacleBehavior>();
        tc.TakeHit(behavior2);

        // Tell the obstacle to play its unique hit animation
        if (behavior2 != null)
            behavior2.OnPlayerHit(other.transform);

        // Generic feedback (particles, camera already handled in TakeHit)
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayHitExplosion(other.transform.position);
    }
}
