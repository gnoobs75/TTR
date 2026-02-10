using UnityEngine;

/// <summary>
/// Procedural slither animation for Mr. Corny.
/// Drives spine bones with sine waves for a snake-like surfing motion.
/// Attach to the root GameObject that contains the SkinnedMeshRenderer.
/// </summary>
public class TurdSlither : MonoBehaviour
{
    [Header("Spine Bones")]
    [Tooltip("Assign spine bones from tail (index 0) to head (last index)")]
    public Transform[] spineBones;

    [Header("Slither Settings")]
    [Range(0f, 45f)]
    public float slitherAmplitude = 3f;     // subtle wobble, not a snake
    [Range(0.1f, 5f)]
    public float slitherFrequency = 0.4f;   // slow, lazy oscillation
    [Range(0f, 2f)]
    public float waveOffset = 0.3f;         // gentle S-wave along body

    [Header("Speed Response")]
    [Tooltip("Current movement speed - set by TurdController")]
    public float currentSpeed = 1f;
    [Range(0f, 2f)]
    public float speedAmplitudeScale = 0.15f;  // minimal speed wobble increase
    [Range(0f, 2f)]
    public float speedFrequencyScale = 0.1f;   // barely speeds up oscillation

    [Header("Turn Response")]
    [Tooltip("Current turn input (-1 to 1) - set by TurdController")]
    public float turnInput = 0f;
    [Range(0f, 30f)]
    public float turnBias = 4f;  // gentle lean into turns

    [Header("Head Look")]
    public bool headLooksForward = true;
    [Range(0f, 1f)]
    public float headStability = 0.85f;  // head stays mostly forward

    private float _time;
    private Quaternion[] _restRotations;

    void Start()
    {
        // Auto-find spine bones if not assigned
        if (spineBones == null || spineBones.Length == 0)
        {
            AutoFindSpineBones();
        }

        // Store rest pose rotations
        _restRotations = new Quaternion[spineBones.Length];
        for (int i = 0; i < spineBones.Length; i++)
        {
            if (spineBones[i] != null)
                _restRotations[i] = spineBones[i].localRotation;
        }
    }

    void LateUpdate()
    {
        if (spineBones == null || spineBones.Length == 0) return;

        _time += Time.deltaTime;

        // Calculate effective slither parameters based on speed
        float effectiveAmplitude = slitherAmplitude * (1f + currentSpeed * speedAmplitudeScale);
        float effectiveFrequency = slitherFrequency * (1f + currentSpeed * speedFrequencyScale);

        for (int i = 0; i < spineBones.Length; i++)
        {
            if (spineBones[i] == null) continue;

            // Normalized position along spine (0 = tail, 1 = head)
            float t = (float)i / (spineBones.Length - 1);

            // Base slither: sine wave with phase offset per bone
            float phase = _time * effectiveFrequency * Mathf.PI * 2f - i * waveOffset;
            float slitherAngle = Mathf.Sin(phase) * effectiveAmplitude;

            // Head stability: reduce slither near the head
            if (headLooksForward)
            {
                float headDamping = Mathf.Lerp(1f, 1f - headStability, t);
                slitherAngle *= headDamping;
            }

            // Turn bias: bend toward turn direction
            float turnAngle = turnInput * turnBias * (1f - t * 0.5f);

            // Tail whip: exaggerate tail movement
            float tailBoost = Mathf.Lerp(1.3f, 1f, t);
            slitherAngle *= tailBoost;

            // Apply rotation around the up axis (local Z for side-to-side slither)
            float totalAngle = slitherAngle + turnAngle;
            Quaternion slitherRot = Quaternion.Euler(0f, 0f, totalAngle);

            // Combine with rest rotation
            spineBones[i].localRotation = _restRotations[i] * slitherRot;
        }
    }

    void AutoFindSpineBones()
    {
        // Try to find bones named "Spine_00" through "Spine_XX"
        var bones = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < 20; i++)
        {
            string boneName = $"Spine_{i:D2}";
            Transform bone = FindDeepChild(transform, boneName);
            if (bone != null)
                bones.Add(bone);
            else
                break;
        }

        if (bones.Count > 0)
        {
            spineBones = bones.ToArray();
            Debug.Log($"TurdSlither: Auto-found {spineBones.Length} spine bones");
        }
        else
        {
            Debug.LogWarning("TurdSlither: No spine bones found! Assign them manually.");
        }
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
