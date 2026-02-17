using UnityEngine;

/// <summary>
/// Applies the selected skin to Mr. Corny at runtime.
/// Finds the player's renderers and swaps material properties.
/// </summary>
public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    private Renderer[] _playerRenderers;
    private string _currentSkinId;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        // Find player renderers - ONLY body mesh, not face features
        TurdController tc = Object.FindFirstObjectByType<TurdController>();
        if (tc != null)
        {
            var allRenderers = tc.GetComponentsInChildren<Renderer>();
            var bodyRenderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in allRenderers)
            {
                // Skip face features AND corn kernels (kernels keep their yellow color)
                string goName = r.gameObject.name;
                if (goName.Contains("Eye") || goName.Contains("Pupil") || goName.Contains("Mustache")
                    || goName.Contains("Mouth") || goName.Contains("CornKernel"))
                    continue;

                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    string meshName = mf.sharedMesh.name;
                    if (meshName == "Sphere" || meshName == "Capsule" ||
                        meshName == "Cube" || meshName == "Quad" || meshName == "Cylinder")
                        continue;
                }
                bodyRenderers.Add(r);
            }
            _playerRenderers = bodyRenderers.ToArray();
        }

        ApplySkin(PlayerData.SelectedSkin);
    }

    void Update()
    {
        // Rainbow skin: cycle colors over time
        if (_currentSkinId == "RainbowCorny" && _playerRenderers != null)
        {
            float hue = (Time.time * 0.3f) % 1f;
            Color rainbow = Color.HSVToRGB(hue, 0.8f, 1f);
            Color rainbowShadow = Color.HSVToRGB(hue, 0.9f, 0.35f);
            foreach (var r in _playerRenderers)
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    m.SetColor("_BaseColor", rainbow);
                    m.SetColor("_EmissionColor", rainbow * 0.3f);
                    if (m.HasProperty("_ShadowColor"))
                        m.SetColor("_ShadowColor", rainbowShadow);
                }
            }
        }
    }

    public void ApplySkin(string skinId)
    {
        _currentSkinId = skinId;
        var skin = SkinData.GetSkin(skinId);

        if (_playerRenderers == null) return;

        foreach (var r in _playerRenderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials)
            {
                m.SetColor("_BaseColor", skin.baseColor);

                // Toon shadow color
                if (m.HasProperty("_ShadowColor"))
                {
                    float h, s, v;
                    Color.RGBToHSV(skin.baseColor, out h, out s, out v);
                    m.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.35f));
                }

                if (skin.emissionColor != Color.black)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", skin.emissionColor);
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                }

                m.SetFloat("_Smoothness", skin.smoothness);
                m.SetFloat("_Metallic", skin.metallic);

                // Ghost skin: enable transparency
                if (skinId == "GhostCorny")
                {
                    m.SetFloat("_Surface", 1f); // Transparent
                    m.SetFloat("_Blend", 0f);   // Alpha
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.renderQueue = 3000;
                }
            }
        }

        PlayerData.SelectedSkin = skinId;
    }

    public bool TryPurchaseSkin(string skinId)
    {
        var skin = SkinData.GetSkin(skinId);
        if (PlayerData.IsSkinUnlocked(skinId)) return true; // already owned

        if (PlayerData.SpendCoins(skin.cost))
        {
            PlayerData.UnlockSkin(skinId);
            return true;
        }
        return false;
    }
}
