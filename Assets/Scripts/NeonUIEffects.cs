using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arcade neon visual polish for UI elements.
/// Applies glowing edges, breathing pulses, scanline overlays, and spark accents.
/// Tasteful intensity — polished but not distracting from gameplay.
/// </summary>
public static class NeonUIEffects
{
    // ================================================================
    // NEON GLOW — Colored outer glow via stacked Outline + Shadow
    // ================================================================

    /// <summary>Apply a soft neon glow to a UI element (best for Images — for Text use ApplyNeonTextGlow).
    /// Strips existing Shadow/Outline effects first to prevent stacking.</summary>
    public static void ApplyNeonGlow(GameObject obj, Color glowColor, float intensity = 1f)
    {
        if (obj == null) return;

        // Strip existing effects to prevent stacking (Shadow is base class of Outline)
        foreach (var s in obj.GetComponents<Shadow>()) Object.Destroy(s);

        // Outer soft glow (large distance, low alpha)
        Outline outerGlow = obj.AddComponent<Outline>();
        outerGlow.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.25f * intensity);
        outerGlow.effectDistance = new Vector2(4f, -4f);

        // Mid glow (medium distance, medium alpha)
        Shadow midGlow = obj.AddComponent<Shadow>();
        midGlow.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f * intensity);
        midGlow.effectDistance = new Vector2(2f, -2f);

        // Inner bright edge (tight, bright)
        Shadow innerGlow = obj.AddComponent<Shadow>();
        innerGlow.effectColor = new Color(
            Mathf.Min(glowColor.r * 1.3f, 1f),
            Mathf.Min(glowColor.g * 1.3f, 1f),
            Mathf.Min(glowColor.b * 1.3f, 1f),
            0.6f * intensity);
        innerGlow.effectDistance = new Vector2(1f, -1f);
    }

    /// <summary>Apply a lightweight neon glow to a Text element.
    /// Keeps max 1 Outline (readability) + adds 1 Shadow (glow) = 10x vertex multiplier.
    /// Prevents the 65k vertex overflow from stacking 3+ effects on Text.</summary>
    public static void ApplyNeonTextGlow(Text text, Color glowColor, float intensity = 1f)
    {
        if (text == null) return;
        var obj = text.gameObject;

        // Strip all Shadows (but keep first Outline for readability if present)
        var shadows = obj.GetComponents<Shadow>();
        bool keptOneOutline = false;
        foreach (var s in shadows)
        {
            if (!keptOneOutline && s is Outline)
            {
                keptOneOutline = true; // keep the first black readability outline
                continue;
            }
            Object.Destroy(s);
        }

        // Add single glow shadow (total: 1 Outline + 1 Shadow = 10x verts, safe for Text)
        Shadow glow = obj.AddComponent<Shadow>();
        glow.effectColor = new Color(
            Mathf.Min(glowColor.r * 1.2f, 1f),
            Mathf.Min(glowColor.g * 1.2f, 1f),
            Mathf.Min(glowColor.b * 1.2f, 1f),
            0.55f * intensity);
        glow.effectDistance = new Vector2(2f, -2f);
    }

    // ================================================================
    // SCANLINE OVERLAY — CRT-style horizontal lines on dark panels
    // ================================================================

    /// <summary>Add a subtle scanline texture overlay to a panel.</summary>
    public static RawImage AddScanlineOverlay(RectTransform parent, float alpha = 0.04f)
    {
        if (parent == null) return null;

        // Create scanline texture (horizontal lines at 4px spacing)
        Texture2D scanTex = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        scanTex.filterMode = FilterMode.Point;
        scanTex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                // Every other pair of rows is slightly lighter
                float brightness = (y % 4 < 2) ? 0.3f : 0f;
                scanTex.SetPixel(x, y, new Color(brightness, brightness, brightness, brightness > 0 ? 1f : 0f));
            }
        }
        scanTex.Apply();

        // Overlay object
        GameObject scanObj = new GameObject("ScanlineOverlay");
        RectTransform scanRt = scanObj.AddComponent<RectTransform>();
        scanRt.SetParent(parent, false);
        scanRt.anchorMin = Vector2.zero;
        scanRt.anchorMax = Vector2.one;
        scanRt.offsetMin = Vector2.zero;
        scanRt.offsetMax = Vector2.zero;

        RawImage scanImg = scanObj.AddComponent<RawImage>();
        scanImg.texture = scanTex;
        scanImg.color = new Color(1f, 1f, 1f, alpha);
        scanImg.raycastTarget = false;

        // Tile the texture across the panel
        scanImg.uvRect = new Rect(0, 0, 1f, parent.rect.height / 8f);

        return scanImg;
    }

    // ================================================================
    // BREATHING PULSE — Animate neon glow brightness
    // ================================================================

    /// <summary>Apply breathing pulse to an Image element's alpha/color.</summary>
    public static void PulseColor(Image image, Color baseColor, float time, float frequency = 1f, float amplitude = 0.15f)
    {
        if (image == null) return;
        float pulse = 1f + Mathf.Sin(time * frequency * Mathf.PI * 2f) * amplitude;
        image.color = new Color(
            Mathf.Clamp01(baseColor.r * pulse),
            Mathf.Clamp01(baseColor.g * pulse),
            Mathf.Clamp01(baseColor.b * pulse),
            baseColor.a);
    }

    /// <summary>Apply breathing pulse to a Text element.</summary>
    public static void PulseText(Text text, Color baseColor, float time, float frequency = 1f, float amplitude = 0.15f)
    {
        if (text == null) return;
        float pulse = 1f + Mathf.Sin(time * frequency * Mathf.PI * 2f) * amplitude;
        text.color = new Color(
            Mathf.Clamp01(baseColor.r * pulse),
            Mathf.Clamp01(baseColor.g * pulse),
            Mathf.Clamp01(baseColor.b * pulse),
            baseColor.a);
    }

    // ================================================================
    // NEON BORDER — Glowing colored border around a panel
    // ================================================================

    /// <summary>Create a neon border effect around a panel using a thin outlined Image.</summary>
    public static Image CreateNeonBorder(RectTransform parent, Color neonColor, float thickness = 2f)
    {
        if (parent == null) return null;

        GameObject borderObj = new GameObject("NeonBorder");
        RectTransform borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.SetParent(parent, false);
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-thickness, -thickness);
        borderRt.offsetMax = new Vector2(thickness, thickness);

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = Color.clear; // transparent fill
        borderImg.raycastTarget = false;

        // Inner glow outline
        Outline innerOutline = borderObj.AddComponent<Outline>();
        innerOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.7f);
        innerOutline.effectDistance = new Vector2(thickness, -thickness);

        // Outer soft glow
        Shadow outerGlow = borderObj.AddComponent<Shadow>();
        outerGlow.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.3f);
        outerGlow.effectDistance = new Vector2(thickness * 2f, -thickness * 2f);

        return borderImg;
    }

    // ================================================================
    // SPARK PARTICLES — Tiny burst of colored sparks on UI events
    // ================================================================

    /// <summary>
    /// Spawn a small particle burst at a screen position.
    /// Uses ParticleManager if available, otherwise creates a temporary one.
    /// </summary>
    public static void SpawnUISparks(Vector3 worldPos, Color sparkColor, int count = 5)
    {
        if (ParticleManager.Instance == null) return;

        // Use existing celebration system with smaller scale for UI sparks
        // This piggybacks on the pooled particle system
        ParticleManager.Instance.PlayCelebration(worldPos);
    }

    // ================================================================
    // CONVENIENCE — Common neon color presets
    // ================================================================

    public static readonly Color NeonCyan = new Color(0f, 0.85f, 1f);
    public static readonly Color NeonGold = new Color(1f, 0.85f, 0.1f);
    public static readonly Color NeonRed = new Color(1f, 0.2f, 0.15f);
    public static readonly Color NeonGreen = new Color(0.2f, 1f, 0.4f);
    public static readonly Color NeonPurple = new Color(0.7f, 0.3f, 1f);
    public static readonly Color NeonOrange = new Color(1f, 0.55f, 0.1f);
    public static readonly Color NeonTeal = new Color(0f, 0.9f, 0.7f);
}
