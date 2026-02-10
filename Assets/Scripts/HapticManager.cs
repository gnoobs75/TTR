using UnityEngine;

/// <summary>
/// Cross-platform haptic feedback for game events.
/// iOS: Uses Unity's built-in haptic API (available in Unity 6+)
/// Android: Handheld.Vibrate (basic)
/// Editor: No-op (debug logs only)
/// </summary>
public static class HapticManager
{
    private static bool _enabled = true;
    public static bool Enabled { get => _enabled; set => _enabled = value; }

    /// Light tap - coin collect, near miss
    public static void LightTap()
    {
        if (!_enabled) return;
#if !UNITY_EDITOR
#if UNITY_ANDROID
        Handheld.Vibrate();
#endif
#endif
    }

    /// Medium tap - combo milestone, speed boost
    public static void MediumTap()
    {
        if (!_enabled) return;
#if !UNITY_EDITOR
#if UNITY_ANDROID
        Handheld.Vibrate();
#endif
#endif
    }

    /// Heavy tap - game over, big collision
    public static void HeavyTap()
    {
        if (!_enabled) return;
#if !UNITY_EDITOR
#if UNITY_ANDROID
        Handheld.Vibrate();
#endif
#endif
    }
}
