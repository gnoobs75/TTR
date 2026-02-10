using UnityEngine;

/// <summary>
/// Sets performance targets for mobile.
/// Attach to GameManager or any persistent object.
/// </summary>
public class PerformanceSettings : MonoBehaviour
{
    [Header("Frame Rate")]
    public int targetFrameRate = 60;

    [Header("Quality")]
    public bool reduceShadowsOnMobile = true;
    public int mobileShadowResolution = 1024;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0; // Use targetFrameRate instead

#if UNITY_IOS || UNITY_ANDROID
        if (reduceShadowsOnMobile)
        {
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 30f;
        }

        // Reduce particle budget on mobile
        QualitySettings.particleRaycastBudget = 64;

        // LOD bias - slightly aggressive on mobile
        QualitySettings.lodBias = 0.8f;
#endif

        // Enable GPU instancing hint
        QualitySettings.skinWeights = SkinWeights.TwoBones;
    }
}
