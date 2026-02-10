using UnityEngine;

/// <summary>
/// Static utility class for creature animation helpers.
/// Sine-wave based animations that are cheap to compute.
/// </summary>
public static class CreatureAnimUtils
{
    /// <summary>
    /// Lerps a pupil's local position toward a world-space target, clamped within radius.
    /// </summary>
    public static void TrackEyeTarget(Transform pupil, Transform eye, Vector3 targetWorldPos, float trackRadius, float speed = 5f)
    {
        if (pupil == null || eye == null) return;

        // Convert target to eye's local space
        Vector3 localTarget = eye.InverseTransformPoint(targetWorldPos);
        Vector3 dir = localTarget.normalized;
        Vector3 desired = dir * Mathf.Min(localTarget.magnitude * 0.1f, trackRadius);

        pupil.localPosition = Vector3.Lerp(pupil.localPosition, desired, Time.deltaTime * speed);
    }

    /// <summary>
    /// Returns a sine wave offset for breathing animation.
    /// </summary>
    public static float BreathingOffset(float time, float freq = 1.2f, float amplitude = 0.03f)
    {
        return Mathf.Sin(time * freq * 2f * Mathf.PI) * amplitude;
    }

    /// <summary>
    /// Returns a sine wave value for idle fidget rotation.
    /// </summary>
    public static float IdleFidget(float time, float freq = 0.8f, float amplitude = 3f)
    {
        return Mathf.Sin(time * freq * 2f * Mathf.PI) * amplitude;
    }

    /// <summary>
    /// Returns a compound sine value for more organic-looking movement.
    /// Combines two sine waves at different frequencies.
    /// </summary>
    public static float OrganicWobble(float time, float freq1 = 1.3f, float freq2 = 2.1f, float amp1 = 1f, float amp2 = 0.4f)
    {
        return Mathf.Sin(time * freq1 * 2f * Mathf.PI) * amp1 +
               Mathf.Sin(time * freq2 * 2f * Mathf.PI) * amp2;
    }

    /// <summary>
    /// Pulsing scale factor for breathing/squishing.
    /// Returns a value centered on 1.0 (e.g., 0.95 to 1.05).
    /// </summary>
    public static float BreathingScale(float time, float freq = 1f, float amplitude = 0.05f)
    {
        return 1f + Mathf.Sin(time * freq * 2f * Mathf.PI) * amplitude;
    }

    /// <summary>
    /// Quick flinch: spikes up then decays. Returns 0-1.
    /// </summary>
    public static float FlinchDecay(float timeSinceTrigger, float duration = 0.5f)
    {
        if (timeSinceTrigger < 0 || timeSinceTrigger > duration) return 0f;
        float t = timeSinceTrigger / duration;
        return (1f - t) * (1f - t); // Quadratic decay
    }

    /// <summary>
    /// Blink pattern: returns 0 (open) or 1 (closed). Blinks briefly at intervals.
    /// </summary>
    public static float BlinkPattern(float time, float blinkInterval = 3f, float blinkDuration = 0.15f)
    {
        float cycle = time % blinkInterval;
        return cycle < blinkDuration ? 1f : 0f;
    }

    /// <summary>
    /// Rapid blink for alarmed state.
    /// </summary>
    public static float RapidBlink(float time, float freq = 8f)
    {
        return Mathf.Sin(time * freq * 2f * Mathf.PI) > 0.5f ? 1f : 0f;
    }

    /// <summary>
    /// Find a child transform recursively by partial name match (case-insensitive).
    /// </summary>
    public static Transform FindChildRecursive(Transform parent, string nameContains)
    {
        if (parent == null) return null;
        nameContains = nameContains.ToLowerInvariant();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.ToLowerInvariant().Contains(nameContains))
                return child;

            Transform found = FindChildRecursive(child, nameContains);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Find all children matching a partial name.
    /// </summary>
    public static void FindChildrenRecursive(Transform parent, string nameContains, System.Collections.Generic.List<Transform> results)
    {
        if (parent == null) return;
        nameContains = nameContains.ToLowerInvariant();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.ToLowerInvariant().Contains(nameContains))
                results.Add(child);
            FindChildrenRecursive(child, nameContains, results);
        }
    }
}
