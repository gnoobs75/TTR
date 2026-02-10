using UnityEngine;

/// <summary>
/// Ramp that launches Mr. Corny into a brief aerial arc inside the pipe.
/// Player detaches from the pipe surface, arcs through the air, then re-attaches.
/// </summary>
public class JumpRamp : MonoBehaviour
{
    public float launchForce = 8f;
    public float arcDuration = 0.8f;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc != null)
        {
            tc.LaunchJump(launchForce, arcDuration);
        }
    }
}
