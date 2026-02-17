using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Asset Gallery - shows all game models/prefabs on a lit stage so the user can
/// inspect what each item looks like. Accessible from the start screen.
/// Assets are spawned one at a time at a far-off gallery position.
/// Buttons are wired at runtime (Unity doesn't serialize AddListener lambdas).
/// </summary>
public class AssetGallery : MonoBehaviour
{
    public static AssetGallery Instance { get; private set; }

    [Header("UI")]
    public GameObject galleryPanel;
    public Text assetNameText;
    public Text assetDescText;
    public Text categoryText;
    public Text counterText;
    public Button prevButton;
    public Button nextButton;
    public Button closeButton;
    public Button[] categoryButtons;

    [Header("Category Names (matches categoryButtons order)")]
    public string[] categoryNames;

    [Header("Stage")]
    public Transform stageCenter;
    public Camera galleryCamera;
    public Light galleryLight;
    public GameObject sewerBackground;
    public Button bgToggleButton;

    [System.Serializable]
    public struct AssetEntry
    {
        public string name;
        public string description;
        public string category;
        public string prefabPath;
        public float displayScale;
        public Color overrideColor;
    }

    [SerializeField]
    private List<AssetEntry> _allAssets = new List<AssetEntry>();
    private List<AssetEntry> _filteredAssets = new List<AssetEntry>();
    private string _currentCategory = "All";
    private int _currentIndex = 0;
    private GameObject _currentInstance;
    private Camera _mainCamera;
    private float _orbitAngle = 0f;
    private float _orbitSpeed = 30f;
    private bool _showBackground = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Wire buttons at runtime (AddListener lambdas don't survive scene save)
        if (prevButton != null) prevButton.onClick.AddListener(ShowPrevious);
        if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (categoryButtons != null && categoryNames != null)
        {
            for (int i = 0; i < categoryButtons.Length && i < categoryNames.Length; i++)
            {
                string cat = categoryNames[i];
                categoryButtons[i].onClick.AddListener(() => FilterCategory(cat));
            }
        }

        if (bgToggleButton != null)
            bgToggleButton.onClick.AddListener(ToggleBackground);

        Debug.Log($"TTR Gallery: Start() wired buttons, {_allAssets.Count} assets registered");
    }

    void Update()
    {
        if (galleryPanel == null || !galleryPanel.activeSelf) return;

        _orbitAngle += _orbitSpeed * Time.deltaTime;
        if (_currentInstance != null)
        {
            _currentInstance.transform.rotation = Quaternion.Euler(0, _orbitAngle, 0);
        }

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame)
                ShowPrevious();
            if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
                ShowNext();
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }
    }

    public void RegisterAsset(string name, string description, string category,
        string prefabPath, float displayScale, Color overrideColor = default)
    {
        _allAssets.Add(new AssetEntry
        {
            name = name,
            description = description,
            category = category,
            prefabPath = prefabPath,
            displayScale = displayScale,
            overrideColor = overrideColor
        });
    }

    public void Open()
    {
        if (galleryPanel == null) return;
        galleryPanel.SetActive(true);

        if (Camera.main != null && Camera.main != galleryCamera)
        {
            _mainCamera = Camera.main;
            _mainCamera.enabled = false;
        }
        if (galleryCamera != null)
            galleryCamera.enabled = true;
        if (galleryLight != null)
            galleryLight.enabled = true;

        FilterCategory("All");
    }

    public void ToggleBackground()
    {
        _showBackground = !_showBackground;
        if (sewerBackground != null)
            sewerBackground.SetActive(_showBackground);
        if (galleryCamera != null)
        {
            galleryCamera.clearFlags = _showBackground
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            if (!_showBackground)
                galleryCamera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        }
    }

    public void Close()
    {
        if (galleryPanel != null)
            galleryPanel.SetActive(false);

        ClearInstance();

        if (galleryCamera != null)
            galleryCamera.enabled = false;
        if (galleryLight != null)
            galleryLight.enabled = false;
        if (_mainCamera != null)
            _mainCamera.enabled = true;

        if (GameManager.Instance != null && GameManager.Instance.gameUI != null)
        {
            var startPanel = GameManager.Instance.gameUI.startPanel;
            if (startPanel != null) startPanel.SetActive(true);
        }
    }

    public void FilterCategory(string category)
    {
        _currentCategory = category;
        _filteredAssets.Clear();

        foreach (var a in _allAssets)
        {
            if (category == "All" || a.category == category)
                _filteredAssets.Add(a);
        }

        _currentIndex = 0;
        if (categoryText != null)
            categoryText.text = category;

        ShowCurrentAsset();
    }

    public void ShowNext()
    {
        if (_filteredAssets.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _filteredAssets.Count;
        ShowCurrentAsset();
    }

    public void ShowPrevious()
    {
        if (_filteredAssets.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _filteredAssets.Count) % _filteredAssets.Count;
        ShowCurrentAsset();
    }

    void ShowCurrentAsset()
    {
        ClearInstance();

        if (_filteredAssets.Count == 0)
        {
            if (assetNameText != null) assetNameText.text = "No assets";
            if (assetDescText != null) assetDescText.text = "";
            if (counterText != null) counterText.text = "";
            return;
        }

        AssetEntry entry = _filteredAssets[_currentIndex];

        if (assetNameText != null) assetNameText.text = entry.name;
        if (assetDescText != null) assetDescText.text = entry.description;
        if (counterText != null)
            counterText.text = $"{_currentIndex + 1} / {_filteredAssets.Count}";

        GameObject source = null;
        if (!string.IsNullOrEmpty(entry.prefabPath))
        {
#if UNITY_EDITOR
            source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            // Try GLB variant if FBX/original path failed (GLB has embedded textures)
            if (source == null && (entry.prefabPath.EndsWith(".fbx") || entry.prefabPath.EndsWith(".FBX")))
            {
                string glbPath = entry.prefabPath.Replace(".fbx", ".glb").Replace(".FBX", ".glb");
                source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            }
            // Try FBX fallback if GLB path was given but failed
            if (source == null && entry.prefabPath.EndsWith(".glb"))
            {
                string fbxPath = entry.prefabPath.Replace(".glb", ".fbx");
                source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            }
#endif
        }

        if (source != null && stageCenter != null)
        {
            _currentInstance = Instantiate(source, stageCenter.position, Quaternion.identity);
            _currentInstance.SetActive(false); // Prevent Start()/Update() from firing
            _currentInstance.name = "GalleryPreview_" + entry.name;

            // IMPORTANT: Destroy MonoBehaviours FIRST because some have [RequireComponent(typeof(Collider))]
            // which prevents collider removal while the script exists (e.g. Obstacle.cs)
            // Two passes: first non-MonoBehaviour components that might block removal, then scripts
            foreach (var mb in _currentInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && !(mb is AssetGallery))
                    DestroyImmediate(mb);
            }
            // Now safe to remove colliders, rigidbodies, particles (no RequireComponent blockers)
            foreach (var col in _currentInstance.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(col);
            foreach (var rb in _currentInstance.GetComponentsInChildren<Rigidbody>(true))
                DestroyImmediate(rb);
            foreach (var ps in _currentInstance.GetComponentsInChildren<ParticleSystem>(true))
                DestroyImmediate(ps);
            _currentInstance.SetActive(true); // Safe to activate now - all behaviors stripped

            // Scale
            float scale = entry.displayScale;
            if (scale > 0f)
                _currentInstance.transform.localScale = Vector3.one * scale;

            // Auto-scale to fit display area
            Bounds bounds = GetCompositeBounds(_currentInstance);
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim > 0.01f)
            {
                float autoScale = 2f / maxDim;
                _currentInstance.transform.localScale *= autoScale;
            }

            // Re-parent into a pivot wrapper so rotation orbits around the visual center
            // (without this, models with offset geometry orbit around their transform origin)
            bounds = GetCompositeBounds(_currentInstance);
            Vector3 boundsCenter = bounds.center;

            GameObject pivot = new GameObject("GalleryPivot");
            pivot.transform.position = boundsCenter;
            _currentInstance.transform.SetParent(pivot.transform, true);

            // Now move pivot to stage center
            pivot.transform.position = stageCenter.position;

            // Store pivot as the instance we rotate and destroy
            _currentInstance = pivot;

            // Override color if specified (skip corn kernels - they stay yellow)
            if (entry.overrideColor.a > 0.01f)
            {
                foreach (var r in _currentInstance.GetComponentsInChildren<Renderer>())
                {
                    string rName = r.gameObject.name;
                    if (rName.Contains("CornKernel") || rName.Contains("Eye") || rName.Contains("Pupil") || rName.Contains("Mouth"))
                        continue;
                    foreach (var m in r.materials)
                    {
                        if (m.HasProperty("_BaseColor"))
                            m.SetColor("_BaseColor", entry.overrideColor);
                    }
                }
            }

            _orbitAngle = 180f; // Start facing the camera
        }
        else
        {
            if (assetDescText != null)
                assetDescText.text = entry.description + "\n(asset not found at: " + entry.prefabPath + ")";
        }
    }

    void ClearInstance()
    {
        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
        }
    }

    Bounds GetCompositeBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one * 0.1f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
