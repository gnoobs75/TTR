using UnityEngine;

/// <summary>
/// Adjusts a RectTransform to fit within Screen.safeArea.
/// Attach to a child of a ScreenSpaceOverlay Canvas to protect UI from notch/dynamic island.
/// Updates on orientation change.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    void Start()
    {
        ApplySafeArea();
    }

    void Update()
    {
        // Reapply if screen size or safe area changed (rotation, etc.)
        if (Screen.safeArea != _lastSafeArea ||
            Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0) return;

        // Convert safe area to anchor values (0-1)
        Vector2 anchorMin = new Vector2(
            safeArea.x / Screen.width,
            safeArea.y / Screen.height);
        Vector2 anchorMax = new Vector2(
            (safeArea.x + safeArea.width) / Screen.width,
            (safeArea.y + safeArea.height) / Screen.height);

        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
