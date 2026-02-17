using UnityEngine;

/// <summary>
/// Attached to floating poop buddies in the water. When the player touches one,
/// it gets added to the PoopBuddyChain as a skiing companion.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PoopBuddyPickup : MonoBehaviour
{
    void Start()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Untagged"; // don't interfere with coin collection
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (PoopBuddyChain.Instance == null) return;

        if (PoopBuddyChain.Instance.AddBuddy(gameObject))
        {
            // AddBuddy destroys this object and creates a skiing version
            // No need to do anything else
        }
    }
}
