using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Cross-platform haptic feedback for game events.
/// iOS: Uses UIImpactFeedbackGenerator via native plugin bridge.
/// Android: Handheld.Vibrate (basic).
/// Editor: No-op.
/// </summary>
public static class HapticManager
{
    private static bool _enabled = true;
    public static bool Enabled { get => _enabled; set => _enabled = value; }

    private static bool _initialized;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _HapticLight();
    [DllImport("__Internal")]
    private static extern void _HapticMedium();
    [DllImport("__Internal")]
    private static extern void _HapticHeavy();
    [DllImport("__Internal")]
    private static extern void _HapticSuccess();
    [DllImport("__Internal")]
    private static extern bool _HapticSupported();

    private static bool _iosSupported;
#endif

    static void Init()
    {
        if (_initialized) return;
        _initialized = true;
#if UNITY_IOS && !UNITY_EDITOR
        try { _iosSupported = _HapticSupported(); }
        catch { _iosSupported = false; }
#endif
    }

    /// Light tap - coin collect, near miss
    public static void LightTap()
    {
        if (!_enabled) return;
        Init();
#if UNITY_IOS && !UNITY_EDITOR
        if (_iosSupported) { try { _HapticLight(); } catch {} }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// Medium tap - combo milestone, speed boost
    public static void MediumTap()
    {
        if (!_enabled) return;
        Init();
#if UNITY_IOS && !UNITY_EDITOR
        if (_iosSupported) { try { _HapticMedium(); } catch {} }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// Heavy tap - game over, big collision
    public static void HeavyTap()
    {
        if (!_enabled) return;
        Init();
#if UNITY_IOS && !UNITY_EDITOR
        if (_iosSupported) { try { _HapticHeavy(); } catch {} }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// Success notification - challenge complete, high score
    public static void Success()
    {
        if (!_enabled) return;
        Init();
#if UNITY_IOS && !UNITY_EDITOR
        if (_iosSupported) { try { _HapticSuccess(); } catch {} }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
