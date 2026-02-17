using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Temporarily turns Mr. Corny solid black (charred) after mine explosion.
/// Eyes and mouth are preserved - everything else goes black.
/// Fades back to normal over the duration.
/// </summary>
public class CharredEffect : MonoBehaviour
{
    private bool _isCharred;
    private float _charredTimer;
    private float _charredDuration;

    // Saved original colors to restore
    private struct SavedMaterial
    {
        public Renderer renderer;
        public Color originalColor;
        public Color originalEmission;
        public bool hadEmission;
    }
    private List<SavedMaterial> _savedMaterials = new List<SavedMaterial>();

    // Names of parts to NOT blacken (eyes, mouth, pupils)
    private static readonly string[] _protectedNames = {
        "eye", "pupil", "mouth", "tongue", "tooth", "teeth", "lip",
        "hypno", "cheek"
    };

    public void ApplyCharred(float duration)
    {
        if (_isCharred) return;
        _isCharred = true;
        _charredDuration = duration;
        _charredTimer = duration;

        _savedMaterials.Clear();

        // Gather all renderers and blacken non-protected ones
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (IsProtected(r.gameObject.name)) continue;

            // Save original color
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            Color origColor = Color.white;
            Color origEmission = Color.black;
            bool hadEmission = false;

            if (r.sharedMaterial != null)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                    origColor = r.sharedMaterial.GetColor("_BaseColor");
                else if (r.sharedMaterial.HasProperty("_Color"))
                    origColor = r.sharedMaterial.GetColor("_Color");

                if (r.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    origEmission = r.sharedMaterial.GetColor("_EmissionColor");
                    hadEmission = origEmission.maxColorComponent > 0.01f;
                }
            }

            _savedMaterials.Add(new SavedMaterial
            {
                renderer = r,
                originalColor = origColor,
                originalEmission = origEmission,
                hadEmission = hadEmission
            });

            // Apply black with property block (doesn't destroy the material)
            mpb.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.03f));
            mpb.SetColor("_Color", new Color(0.03f, 0.03f, 0.03f));
            mpb.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(mpb);
        }

        StartCoroutine(CharredCoroutine());
    }

    bool IsProtected(string name)
    {
        string lower = name.ToLower();
        foreach (var prot in _protectedNames)
        {
            if (lower.Contains(prot)) return true;
        }
        return false;
    }

    IEnumerator CharredCoroutine()
    {
        // Stay solid black for 2/3 of the duration
        float blackTime = _charredDuration * 0.65f;
        yield return new WaitForSeconds(blackTime);

        // Fade back to original over remaining time
        float fadeTime = _charredDuration - blackTime;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);

            // Ease out - fast at start of recovery, slow at end
            float eased = 1f - (1f - t) * (1f - t);

            foreach (var saved in _savedMaterials)
            {
                if (saved.renderer == null) continue;

                Color current = Color.Lerp(new Color(0.03f, 0.03f, 0.03f), saved.originalColor, eased);
                Color currentEmission = Color.Lerp(Color.black, saved.originalEmission, eased);

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                saved.renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", current);
                mpb.SetColor("_Color", current);
                if (saved.hadEmission)
                    mpb.SetColor("_EmissionColor", currentEmission);
                saved.renderer.SetPropertyBlock(mpb);
            }

            yield return null;
        }

        // Fully restore - clear all property blocks
        foreach (var saved in _savedMaterials)
        {
            if (saved.renderer == null) continue;
            saved.renderer.SetPropertyBlock(new MaterialPropertyBlock());
        }

        _savedMaterials.Clear();
        _isCharred = false;
    }
}
