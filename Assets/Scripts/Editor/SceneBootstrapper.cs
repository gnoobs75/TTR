using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// One-click scene setup for Turd Tunnel Rush.
/// Menu: TTR > Setup Game Scene
/// Loads real Blender models, creates UI, lighting, and saves the scene.
/// </summary>
public class SceneBootstrapper
{
    static Font _font;
    static Shader _urpLit;
    static Shader _toonLit;

    [MenuItem("TTR/Setup Game Scene")]
    public static void SetupGameScene()
    {
        // Cannot run during play mode
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("TTR: Cannot setup scene during Play Mode. Stop the game first.");
            return;
        }

        // Ensure asset database is up to date (picks up newly exported GLBs)
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Start with a completely fresh scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        _font = GetFont();
        // Toon shader is primary; URP Lit is fallback
        _toonLit = Shader.Find("Custom/ToonLit");
        _urpLit = _toonLit != null ? _toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (_urpLit == null)
            _urpLit = Shader.Find("Standard"); // final fallback
        Debug.Log($"TTR: Shader setup — ToonLit={((_toonLit != null) ? "FOUND" : "NULL")}, urpLit={((_urpLit != null) ? _urpLit.name : "NULL")}");
        _matCounter = 0;

        // Clean old generated materials
        if (AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.DeleteAsset("Assets/Materials");
        AssetDatabase.CreateFolder("Assets", "Materials");

        // Set up Polyhaven texture import settings (normal maps need special type)
        SetupTextureImports();
        SetupRoughnessImports();

        GameObject player = CreatePlayer();
        GameObject pipeGenObj = CreatePipeGenerator(player.transform);
        CreateCamera(player.transform);
        CreateObstacleSpawner(player.transform);
        GameUI gameUI = CreateUI();
        CreateGameManager(player.GetComponent<TurdController>(), gameUI);

        TurdController tc = player.GetComponent<TurdController>();
        if (tc != null && pipeGenObj != null)
            tc.pipeGen = pipeGenObj.GetComponent<PipeGenerator>();

        CreateLighting();
        CreatePostProcessing();
        SetupOutlineRendererFeature();
        CreatePrefabs();
        SaveMrCornyGalleryPrefab(player);
        CreateScenerySpawner(player.transform);
        CreatePowerUpSpawner(player.transform);
        CreateSmoothSnake(pipeGenObj.GetComponent<PipeGenerator>());
        CreateRaceSystem(player.GetComponent<TurdController>(), pipeGenObj.GetComponent<PipeGenerator>(), gameUI);
        CreateWaterCreatureSpawner(player.transform);
        CreateWaterAnimator(player.transform, pipeGenObj);
        CreateSewerWaterEffects(player.transform, pipeGenObj);
        CreateBrownStreakTrail(player.transform);
        CreatePooperSnooper(player.GetComponent<TurdController>());
        CreatePipeZoneSystem();
        CreateScorePopup();
        CreateCheerOverlay(gameUI.gameObject);
        CreateFlushSequence();
        CreateAssetGallery(gameUI);
        CreateSewerTour(player.GetComponent<TurdController>(), pipeGenObj.GetComponent<PipeGenerator>());
        SaveScene();

        Debug.Log("TTR: Scene setup complete! Press Play to test.");
        EditorUtility.DisplayDialog("Turd Tunnel Rush",
            "Scene created with real Blender models!\n\n" +
            "Controls:\n" +
            "  SPACE - Start / Restart\n" +
            "  LEFT/RIGHT or A/D - Steer\n\n" +
            "Press Play to test!", "FLUSH!");
    }

    static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;
        string[] tryFonts = { "Arial", "Segoe UI", "Helvetica", "Liberation Sans" };
        foreach (string name in tryFonts)
        {
            f = Font.CreateDynamicFontFromOSFont(name, 14);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>Configure a TextMesh for URP rendering: set font and ensure render queue above haze.</summary>
    static void ConfigureSignText(TextMesh tm)
    {
        if (_font != null) tm.font = _font;
        MeshRenderer mr = tm.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            // Default font shader often invisible in URP. Use Sprites/Default
            // which handles alpha-textured meshes in all render pipelines.
            Shader spriteShader = Shader.Find("Sprites/Default");
            Material fontSrc = (_font != null && _font.material != null) ? _font.material : mr.sharedMaterial;
            if (spriteShader != null && fontSrc != null)
            {
                Material fontMat = new Material(spriteShader);
                fontMat.mainTexture = fontSrc.mainTexture;
                fontMat.color = tm.color;
                fontMat.renderQueue = 3100;
                mr.sharedMaterial = fontMat;
            }
            else if (fontSrc != null)
            {
                mr.sharedMaterial = new Material(fontSrc);
                mr.sharedMaterial.renderQueue = 3100;
            }
        }
    }

    // ===== GALLERY PREFAB =====
    /// <summary>
    /// Saves the fully-dressed MrCorny model (with face features, yellow corn kernels,
    /// mouth, eyes, mustache) as a prefab so the Gallery can display it correctly.
    /// Must be called AFTER CreatePlayer() and CreatePrefabs() (which creates the Prefabs folder).
    /// </summary>
    static void SaveMrCornyGalleryPrefab(GameObject player)
    {
        Transform modelT = player.transform.Find("Model");
        if (modelT == null) return;

        // Clone the model so we don't disturb the live player
        GameObject clone = (GameObject)Object.Instantiate(modelT.gameObject);
        clone.name = "MrCorny_Gallery";
        clone.transform.SetParent(null);
        clone.transform.position = Vector3.zero;
        clone.transform.rotation = Quaternion.identity;

        // Remove MrCornyFaceAnimator (not needed for static gallery display)
        var anim = clone.GetComponent<MrCornyFaceAnimator>();
        if (anim != null) Object.DestroyImmediate(anim);

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        PrefabUtility.SaveAsPrefabAsset(clone, "Assets/Prefabs/MrCorny_Gallery.prefab");
        Object.DestroyImmediate(clone);
        Debug.Log("TTR: Saved MrCorny_Gallery.prefab (with face features + yellow corn kernels)");
    }

    /// <summary>
    /// Saves a racer's visual model as a gallery prefab (no AI/physics components).
    /// </summary>
    static void SaveRacerGalleryPrefab(GameObject racer, string presetName)
    {
        Transform modelT = racer.transform.Find("Model");
        GameObject source = modelT != null ? modelT.gameObject : racer;

        GameObject clone = (GameObject)Object.Instantiate(source);
        clone.name = $"Racer_{presetName}_Gallery";
        clone.transform.SetParent(null);
        clone.transform.position = Vector3.zero;
        clone.transform.rotation = Quaternion.identity;

        // Strip any non-visual components
        foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>())
            Object.DestroyImmediate(comp);

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string path = $"Assets/Prefabs/Racer_{presetName}_Gallery.prefab";
        PrefabUtility.SaveAsPrefabAsset(clone, path);
        Object.DestroyImmediate(clone);
        Debug.Log($"TTR: Saved {path} for gallery display");
    }

    // ===== TEXTURE SETUP =====
    static void SetupTextureImports()
    {
        string[] normalMaps = {
            "Assets/Textures/concrete_moss_normal.png",
            "Assets/Textures/brick_4_normal.png",
            "Assets/Textures/rust_coarse_01_normal.png",
            "Assets/Textures/interior_tiles_normal.png"
        };
        foreach (string path in normalMaps)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                Debug.Log($"TTR: Set {path} as normal map");
            }
        }
    }

    /// <summary>Configure roughness maps as linear (not sRGB) for correct PBR.</summary>
    static void SetupRoughnessImports()
    {
        string[] linearMaps = {
            "Assets/Textures/concrete_moss_roughness.png",
            "Assets/Textures/concrete_moss_ao.png",
            "Assets/Textures/brick_4_roughness.png",
            "Assets/Textures/brick_4_ao.png",
            "Assets/Textures/rust_coarse_01_roughness.png",
            "Assets/Textures/rust_coarse_01_ao.png",
            "Assets/Textures/interior_tiles_roughness.png",
            "Assets/Textures/interior_tiles_ao.png"
        };
        foreach (string path in linearMaps)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.sRGBTexture)
            {
                imp.sRGBTexture = false;
                imp.SaveAndReimport();
            }
        }
    }

    /// <summary>
    /// Creates a textured URP Lit material using Polyhaven textures.
    /// Falls back to flat color if textures are missing.
    /// </summary>
    static Material MakeTexturedMat(string name, string texPrefix, Color tint,
        float metallic, float smoothness, Vector2 tiling,
        float bumpScale = 1.2f, float occlusionStrength = 0.7f)
    {
        Material mat = MakeURPMat(name, tint, metallic, smoothness);

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texPrefix}_diffuse.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texPrefix}_normal.png");
        Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texPrefix}_ao.png");

        if (diffuse != null)
        {
            mat.SetTexture("_BaseMap", diffuse);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", tint); // tint multiplied with texture
        }
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetTextureScale("_BumpMap", tiling);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetTextureScale("_OcclusionMap", tiling);
            mat.SetFloat("_OcclusionStrength", occlusionStrength);
        }

        // Toon shader shadow color for textured materials
        if (_toonLit != null && mat.HasProperty("_ShadowColor"))
        {
            float h, s, v;
            Color.RGBToHSV(tint, out h, out s, out v);
            mat.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f));
        }

        EditorUtility.SetDirty(mat);

        if (diffuse != null)
            Debug.Log($"TTR: Created textured material '{name}' with {texPrefix} textures (toon)");
        else
            Debug.LogWarning($"TTR: Textures not found for '{name}', using flat color");

        return mat;
    }

    // ===== PLAYER =====
    static GameObject CreatePlayer()
    {
        // Empty root for gameplay mechanics (TurdController, collider, physics).
        // The visual model goes in a CHILD so TurdController's rotation doesn't
        // destroy the FBX import rotation that handles Blender→Unity axis conversion.
        // This was the root cause of "facing wrong way" and "rolling in place".
        GameObject player = new GameObject("MrCorny");
        player.transform.position = new Vector3(0, -3f, 0);
        player.tag = "Player";

        // Load visual model as child
        GameObject modelPrefab = LoadModel("Assets/Models/MrCorny_Rigged.fbx");

        if (modelPrefab != null)
        {
            GameObject model = (GameObject)Object.Instantiate(modelPrefab);
            model.name = "Model";
            // SetParent(parent, false) preserves the FBX import rotation!
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 0.51f;

            // Log model bounds for size debugging (skip face meshes for accurate body size)
            {
                Bounds mb = new Bounds(model.transform.position, Vector3.zero);
                bool mbHas = false;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    string gn = r.gameObject.name;
                    if (gn.Contains("Eye") || gn.Contains("Pupil") || gn.Contains("Mouth"))
                        continue;
                    if (!mbHas) { mb = r.bounds; mbHas = true; }
                    else mb.Encapsulate(r.bounds);
                }
                Debug.Log($"TTR: MrCorny body bounds at scale 0.17: size={mb.size} center={mb.center} (GLB={_lastModelWasGLB})");
            }

            // Remove imported colliders (we add our own on root)
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // Upgrade materials to URP
            UpgradeToURP(model);

            // Paint body brown, keep corn kernels yellow, strip textures.
            // GLB face meshes (LeftEye/RightEye/etc.) are kept if present; painting skips them.
            Color defaultBrown = new Color(0.45f, 0.28f, 0.1f);
            Color cornYellow = new Color(0.9f, 0.78f, 0.2f);
            float h, s, v;
            Color.RGBToHSV(defaultBrown, out h, out s, out v);
            Color brownShadow = Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.35f);
            Color.RGBToHSV(cornYellow, out h, out s, out v);
            Color cornShadow = Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.4f);

            // Create proper URP materials for face features (Blender materials don't survive import)
            Material eyeWhiteMat = MakeURPMat("FaceEyeWhite", Color.white, 0f, 0.85f);
            eyeWhiteMat.EnableKeyword("_EMISSION");
            eyeWhiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.95f));
            Material pupilBlackMat = MakeURPMat("FacePupilBlack", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
            Material mouthMat = MakeURPMat("FaceMouthDark", new Color(0.12f, 0.02f, 0.02f), 0f, 0.4f);

            int kernelCount = 0;
            int bodyCount = 0;
            int faceCount = 0;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                string goName = r.gameObject.name;

                // Face features get explicit URP materials (GLB materials import as grey)
                if (goName.Contains("Eye") && !goName.Contains("Pupil"))
                {
                    r.sharedMaterial = eyeWhiteMat;
                    faceCount++;
                    continue;
                }
                if (goName.Contains("Pupil"))
                {
                    r.sharedMaterial = pupilBlackMat;
                    faceCount++;
                    continue;
                }
                if (goName.Contains("Mouth"))
                {
                    r.sharedMaterial = mouthMat;
                    faceCount++;
                    continue;
                }

                // Detect corn kernels by GO name OR material name (GLB imports may differ)
                bool isCornKernel = goName.Contains("CornKernel") || goName.Contains("Corn_Kernel");
                if (!isCornKernel)
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m != null && m.name.Contains("CornKernel"))
                        { isCornKernel = true; break; }
                    }
                }
                Color baseCol = isCornKernel ? cornYellow : defaultBrown;
                Color shadowCol = isCornKernel ? cornShadow : brownShadow;
                if (isCornKernel) kernelCount++; else bodyCount++;

                // Safety: if material has broken/missing shader, replace with fresh URP material
                bool needsReplace = false;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name.Contains("Error") || !m.HasProperty("_BaseColor"))
                    { needsReplace = true; break; }
                }
                if (needsReplace)
                {
                    string matName = isCornKernel ? "CornKernel_Fix" : "Body_Fix_" + goName;
                    r.sharedMaterial = SaveMaterial(MakeURPMat(matName, baseCol, 0f, 0.3f));
                    Debug.LogWarning($"TTR: Replaced broken material on '{goName}' with fresh URP mat");
                }
                else
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.HasProperty("_BaseMap"))
                            m.SetTexture("_BaseMap", null);
                        if (m.HasProperty("_BaseColor"))
                            m.SetColor("_BaseColor", baseCol);
                        if (m.HasProperty("_ShadowColor"))
                            m.SetColor("_ShadowColor", shadowCol);
                    }
                }
            }
            Debug.Log($"TTR: MrCorny painting: {kernelCount} kernels, {bodyCount} body, {faceCount} face features");

            // If no corn kernels found in the FBX, add procedural ones as raised yellow nuggets
            if (kernelCount == 0)
            {
                AddProceduralCornKernels(model, cornYellow, cornShadow);
            }

            // Add comical face features based on selected skin
            AddFaceForSkin(model, PlayerData.SelectedSkin);

            Debug.Log($"TTR: Loaded MrCorny model as visual child (importRot={model.transform.localRotation.eulerAngles})");
        }
        else
        {
            // Fallback: brown capsule
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Model";
            Collider fc = capsule.GetComponent<Collider>();
            if (fc != null) Object.DestroyImmediate(fc);
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localScale = new Vector3(1.5f, 0.9f, 3f);
            capsule.GetComponent<Renderer>().material =
                MakeURPMat("MrCorny_Mat", new Color(0.55f, 0.35f, 0.18f), 0.05f, 0.4f);
            AddFaceForSkin(capsule, PlayerData.SelectedSkin);
            Debug.LogWarning("TTR: MrCorny_Rigged model not found, using placeholder capsule.");
        }

        // Gameplay collider on root
        CapsuleCollider col = player.AddComponent<CapsuleCollider>();
        col.radius = 0.3f;
        col.height = 1.0f;
        col.direction = 2; // Z axis (forward)

        TurdController controller = player.AddComponent<TurdController>();
        controller.useTiltControls = false;
        controller.pipeRadius = 3f;

        TurdSlither slither = player.AddComponent<TurdSlither>();
        controller.slither = slither;

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // No Light components on player - gizmo icons show in Game view.
        // Scene directional lights + ambient + emissive materials handle visibility.

        return player;
    }

    // ===== PIPE GENERATOR =====
    static GameObject CreatePipeGenerator(Transform player)
    {
        GameObject obj = new GameObject("PipeGenerator");
        PipeGenerator gen = obj.AddComponent<PipeGenerator>();
        gen.player = player;
        gen.pipeRadius = 3.5f;
        gen.visiblePipes = 8;

        // Sewer pipe - real concrete/moss Polyhaven texture with tiling
        // Tiling (3, 1.5) = 3 repeats around circumference, 1.5 along length (less repetitive)
        Material pipeMat = MakeTexturedMat("SewerPipe_Mat", "concrete_moss",
            new Color(0.85f, 0.83f, 0.78f), 0.05f, 0.35f, new Vector2(3f, 1.5f),
            1.2f, 0.8f);
        pipeMat.EnableKeyword("_EMISSION");
        pipeMat.SetColor("_EmissionColor", new Color(0.06f, 0.1f, 0.03f) * 0.5f);
        EditorUtility.SetDirty(pipeMat);
        gen.pipeMaterial = pipeMat;

        // Pipe ring bands - rusty metal rings at segment joints
        // Uses rust_coarse_01 texture for industrial metal look
        Material ringMat = MakeTexturedMat("PipeRing_Mat", "rust_coarse_01",
            new Color(0.4f, 0.35f, 0.28f), 0.65f, 0.35f, new Vector2(4f, 1f),
            1.5f, 0.6f);
        ringMat.EnableKeyword("_EMISSION");
        ringMat.SetColor("_EmissionColor", new Color(0.04f, 0.03f, 0.02f));
        EditorUtility.SetDirty(ringMat);
        gen.pipeRingMaterial = ringMat;

        return obj;
    }

    // ===== CAMERA =====
    static GameObject CreateCamera(Transform player)
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera mainCam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();

        PipeCamera pipeCam = camObj.AddComponent<PipeCamera>();
        pipeCam.target = player;
        pipeCam.followDistance = 2.75f;      // behind the turd on the pipe path
        pipeCam.lookAhead = 6f;              // look ahead of the turd
        pipeCam.pipeRadius = 3.5f;
        pipeCam.centerPull = 0.45f;          // 45% from player toward pipe center (above/behind)
        pipeCam.baseFOV = 68f;               // wide for speed feel
        pipeCam.speedFOVBoost = 8f;          // more at high speed

        mainCam.backgroundColor = new Color(0.02f, 0.03f, 0.02f);
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.fieldOfView = 65f;
        mainCam.nearClipPlane = 0.1f;
        mainCam.farClipPlane = 200f;

        return camObj;
    }

    // ===== OBSTACLE SPAWNER =====
    static GameObject CreateObstacleSpawner(Transform player)
    {
        GameObject obj = new GameObject("ObstacleSpawner");
        ObstacleSpawner spawner = obj.AddComponent<ObstacleSpawner>();
        spawner.player = player;
        spawner.pipeRadius = 3.5f;
        return obj;
    }

    // ===== GAME MANAGER =====
    static void CreateGameManager(TurdController playerController, GameUI gameUI)
    {
        GameObject obj = new GameObject("GameManager");
        GameManager gm = obj.AddComponent<GameManager>();
        gm.player = playerController;
        gm.gameUI = gameUI;
        gm.isPlaying = false;

        // TouchInput singleton for keyboard/touch/tilt abstraction
        obj.AddComponent<TouchInput>();

        // Combo system - wire combo text from GameUI
        ComboSystem combo = obj.AddComponent<ComboSystem>();
        if (gameUI != null)
            combo.comboText = gameUI.comboText;

        // Particle effects manager
        GameObject particleObj = new GameObject("ParticleManager");
        particleObj.AddComponent<ParticleManager>();

        // Procedural audio (SFX + BGM)
        obj.AddComponent<ProceduralAudio>();

        // Performance settings
        obj.AddComponent<PerformanceSettings>();

        // Skin manager
        obj.AddComponent<SkinManager>();

        // Ghost racer recorder
        obj.AddComponent<GhostRecorder>();

        // Daily challenge system
        ChallengeSystem challenge = obj.AddComponent<ChallengeSystem>();
        if (gameUI != null)
            challenge.challengeText = gameUI.challengeText;

        // iOS features
        obj.AddComponent<GameCenterManager>();
        obj.AddComponent<CloudSaveManager>();
        obj.AddComponent<AnalyticsManager>();
        obj.AddComponent<RateAppPrompt>();
        obj.AddComponent<TutorialOverlay>();

        // Pause menu (mobile-critical)
        obj.AddComponent<PauseMenu>();

        // Obstacle radar HUD (fades in at high speed)
        obj.AddComponent<ObstacleRadar>();
    }

    // ===== WATER CREATURE SPAWNER =====
    static void CreateWaterCreatureSpawner(Transform player)
    {
        // Create sewer squirt prefab
        GameObject prefab = CreateSewerSquirtPrefab();

        // Spawn them using a simple spawner script on a new object
        GameObject obj = new GameObject("WaterCreatureSpawner");
        WaterCreatureSpawner spawner = obj.AddComponent<WaterCreatureSpawner>();
        spawner.player = player;
        spawner.squirtPrefab = prefab;
        spawner.pipeRadius = 3.5f;

        Debug.Log("TTR: Created water creature spawner with sewer squirts!");
    }

    static GameObject CreateSewerSquirtPrefab()
    {
        string path = "Assets/Prefabs/SewerSquirt.prefab";
        GameObject root = new GameObject("SewerSquirt");

        // Translucent blue-green body (like real sea squirts)
        Material bodyMat = MakeURPMat("Squirt_Body", new Color(0.4f, 0.7f, 0.65f, 0.7f), 0.05f, 0.85f);
        bodyMat.EnableKeyword("_EMISSION");
        bodyMat.SetColor("_EmissionColor", new Color(0.1f, 0.2f, 0.15f));
        // Make semi-transparent
        bodyMat.SetFloat("_Surface", 1); // Transparent
        bodyMat.SetFloat("_Blend", 0); // Alpha
        bodyMat.SetOverrideTag("RenderType", "Transparent");
        bodyMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bodyMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        bodyMat.SetInt("_ZWrite", 0);
        bodyMat.renderQueue = 3000;
        EditorUtility.SetDirty(bodyMat);

        Material eyeWhite = MakeURPMat("Squirt_EyeWhite", Color.white, 0f, 0.9f);
        eyeWhite.EnableKeyword("_EMISSION");
        eyeWhite.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0.8f));
        EditorUtility.SetDirty(eyeWhite);

        Material pupilMat = MakeURPMat("Squirt_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material irisMat = MakeURPMat("Squirt_Iris", new Color(0.8f, 0.55f, 0.1f), 0f, 0.7f);
        irisMat.EnableKeyword("_EMISSION");
        irisMat.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.05f));
        EditorUtility.SetDirty(irisMat);

        Material stalkMat = MakeURPMat("Squirt_Stalk", new Color(0.35f, 0.6f, 0.55f, 0.8f), 0.05f, 0.8f);
        stalkMat.SetFloat("_Surface", 1);
        stalkMat.SetOverrideTag("RenderType", "Transparent");
        stalkMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        stalkMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        stalkMat.SetInt("_ZWrite", 0);
        stalkMat.renderQueue = 3000;
        EditorUtility.SetDirty(stalkMat);

        // Bulbous body (base)
        AddPrimChild(root, "Body", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.08f, 0.12f, 0.08f), bodyMat);

        // Left eye stalk
        GameObject leftStalk = AddPrimChild(root, "LeftStalk", PrimitiveType.Capsule,
            new Vector3(-0.04f, 0.1f, 0), Quaternion.identity,
            new Vector3(0.02f, 0.06f, 0.02f), stalkMat);
        // Left eye (big googly)
        GameObject leftEye = AddPrimChild(leftStalk, "LeftEye", PrimitiveType.Sphere,
            new Vector3(0, 0.06f, 0.01f), Quaternion.identity,
            Vector3.one * 0.045f, eyeWhite);
        // Left iris (orange like reference)
        AddPrimChild(leftEye, "LeftIris", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.35f), Quaternion.identity,
            Vector3.one * 0.6f, irisMat);
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.4f), Quaternion.identity,
            Vector3.one * 0.35f, pupilMat);

        // Right eye stalk
        GameObject rightStalk = AddPrimChild(root, "RightStalk", PrimitiveType.Capsule,
            new Vector3(0.04f, 0.1f, 0), Quaternion.identity,
            new Vector3(0.02f, 0.06f, 0.02f), stalkMat);
        // Right eye
        GameObject rightEye = AddPrimChild(rightStalk, "RightEye", PrimitiveType.Sphere,
            new Vector3(0, 0.06f, 0.01f), Quaternion.identity,
            Vector3.one * 0.04f, eyeWhite); // slightly different size
        AddPrimChild(rightEye, "RightIris", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.35f), Quaternion.identity,
            Vector3.one * 0.6f, irisMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.4f), Quaternion.identity,
            Vector3.one * 0.35f, pupilMat);

        // Add behavior
        root.AddComponent<SewerSquirt>();

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 3f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ===== WATER ANIMATOR =====
    static void CreateWaterAnimator(Transform player, GameObject pipeGenObj)
    {
        WaterAnimator wa = pipeGenObj.AddComponent<WaterAnimator>();
        wa.player = player;
        Debug.Log("TTR: Created water wave animator with splash effects!");

        // Poop buddy ski chain - attaches to player so buddies follow
        GameObject chainObj = new GameObject("PoopBuddyChain");
        chainObj.AddComponent<PoopBuddyChain>();
        Debug.Log("TTR: Created poop buddy chain system!");
    }

    // ===== SEWER WATER EFFECTS =====
    static void CreateSewerWaterEffects(Transform player, GameObject pipeGenObj)
    {
        GameObject obj = new GameObject("SewerWaterEffects");
        SewerWaterEffects fx = obj.AddComponent<SewerWaterEffects>();
        fx.player = player;
        fx.pipeGen = pipeGenObj.GetComponent<PipeGenerator>();
        Debug.Log("TTR: Created sewer water effects (drain pipes + ceiling waterfalls + debris)!");
    }

    // ===== BROWN STREAK TRAIL =====
    static void CreateBrownStreakTrail(Transform player)
    {
        GameObject obj = new GameObject("BrownStreakTrail");
        BrownStreakTrail trail = obj.AddComponent<BrownStreakTrail>();
        trail.player = player;
        Debug.Log("TTR: Created brown streak trail effect!");
    }

    // ===== POOPER SNOOPER =====
    static void CreatePooperSnooper(TurdController player)
    {
        // Find the canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject snooperObj = new GameObject("PooperSnooper");
        snooperObj.transform.SetParent(canvas.transform, false);

        PooperSnooper snooper = snooperObj.AddComponent<PooperSnooper>();
        snooper.player = player;

        // Find AI racer
        SmoothSnakeAI ai = Object.FindFirstObjectByType<SmoothSnakeAI>();
        snooper.aiRacer = ai;

        Debug.Log("TTR: Created Pooper Snooper progress tracker!");
    }

    // ===== LIGHTING (Toon-optimized: strong directional + minimal fill for hard shadow bands) =====
    static void CreateLighting()
    {
        // Main directional - the ONLY shadow-casting light for clean toon shadow bands
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.85f); // near-white warm light for clean toon color
        light.intensity = 2.0f; // strong - toon shading needs clear lit vs shadow
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f; // strong shadows for visible toon shadow band
        lightObj.transform.rotation = Quaternion.Euler(40, -15, 0); // overhead-ish for pipe interior

        // Fill light - very subtle, just prevents pitch-black areas inside pipe
        GameObject fillObj = new GameObject("Fill Light");
        Light fill = fillObj.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.35f, 0.4f, 0.3f); // subtle green sewer fill
        fill.intensity = 0.3f; // reduced - we want shadow bands to be visible
        fill.shadows = LightShadows.None;
        fillObj.transform.rotation = Quaternion.Euler(-25, 45, 0);

        // Back fill - minimal
        GameObject backObj = new GameObject("Back Fill Light");
        Light back = backObj.AddComponent<Light>();
        back.type = LightType.Directional;
        back.color = new Color(0.3f, 0.28f, 0.22f);
        back.intensity = 0.25f; // very subtle
        back.shadows = LightShadows.None;
        backObj.transform.rotation = Quaternion.Euler(15, 160, 0);

        // Ambient - moderate, toon shading handles dark areas via _ShadowColor
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.35f, 0.25f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.30f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.16f);

        // Fog - subtle, mainly for depth fade into darkness
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.03f);
        RenderSettings.fogDensity = 0.004f; // slightly denser for sewer atmosphere
    }

    // ===== POST-PROCESSING =====
    static void CreatePostProcessing()
    {
        // Try to set up URP Volume for bloom, vignette, color grading
        // This requires UnityEngine.Rendering.Universal which may or may not be available
        try
        {
            // Create a Volume Profile asset
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Try to add URP post-processing effects via reflection
            // (avoids hard dependency on URP assembly)
            System.Type bloomType = System.Type.GetType("UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime");
            System.Type vignetteType = System.Type.GetType("UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            System.Type colorAdjType = System.Type.GetType("UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");

            if (bloomType != null)
            {
                var bloom = (VolumeComponent)profile.Add(bloomType);
                SetVolumeParam(bloom, "threshold", 0.8f);   // only bright emissives bloom
                SetVolumeParam(bloom, "intensity", 1.5f);    // moderate - toon look is flat, not glowy
                SetVolumeParam(bloom, "scatter", 0.6f);
                Debug.Log("TTR: Bloom added (toon-optimized)");
            }

            if (vignetteType != null)
            {
                var vignette = (VolumeComponent)profile.Add(vignetteType);
                SetVolumeParam(vignette, "intensity", 0.35f);  // subtle panel-frame vignette
                SetVolumeParam(vignette, "smoothness", 0.4f);
                SetVolumeParamColor(vignette, "color", new Color(0.02f, 0.02f, 0.02f));
                Debug.Log("TTR: Vignette added (comic panel frame)");
            }

            if (colorAdjType != null)
            {
                var colorAdj = (VolumeComponent)profile.Add(colorAdjType);
                SetVolumeParam(colorAdj, "saturation", 45f);    // extra punchy for toon
                SetVolumeParam(colorAdj, "contrast", 35f);      // strong bands need contrast
                SetVolumeParam(colorAdj, "postExposure", 0.3f);
                Debug.Log("TTR: Color adjustments added (toon-optimized)");
            }

            // No film grain - conflicts with hatching lines

            // Save profile
            AssetDatabase.CreateAsset(profile, "Assets/Materials/PostProcessProfile.asset");

            // Create Volume in scene
            GameObject volumeObj = new GameObject("PostProcessVolume");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.profile = profile;
            volume.isGlobal = true;

            Debug.Log("TTR: Post-processing volume created");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"TTR: Could not set up post-processing (non-fatal): {e.Message}");
        }
    }

    static void SetVolumeParam(VolumeComponent comp, string fieldName, float value)
    {
        var field = comp.GetType().GetField(fieldName);
        if (field == null) return;
        var param = field.GetValue(comp);
        if (param == null) return;
        // Call overrideState setter and value setter via reflection
        var overrideProp = param.GetType().GetProperty("overrideState");
        if (overrideProp != null) overrideProp.SetValue(param, true);
        var valueProp = param.GetType().GetProperty("value");
        if (valueProp != null) valueProp.SetValue(param, value);
    }

    static void SetVolumeParamColor(VolumeComponent comp, string fieldName, Color value)
    {
        var field = comp.GetType().GetField(fieldName);
        if (field == null) return;
        var param = field.GetValue(comp);
        if (param == null) return;
        var overrideProp = param.GetType().GetProperty("overrideState");
        if (overrideProp != null) overrideProp.SetValue(param, true);
        var valueProp = param.GetType().GetProperty("value");
        if (valueProp != null) valueProp.SetValue(param, value);
    }

    // ===== PREFABS =====
    static void CreatePrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        List<GameObject> obstaclePrefabs = new List<GameObject>();

        // Try real Blender models first, fall back to awesome procedural builds
        GameObject ratModel = LoadModel("Assets/Models/SewerRat.fbx");
        if (ratModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("SewerRat", "Assets/Models/SewerRat.fbx",
                new Vector3(0.35f, 0.35f, 0.35f), PrimitiveType.Capsule, new Color(0.4f, 0.3f, 0.18f), true));
        else
            obstaclePrefabs.Add(CreateSewerRatPrefab());

        GameObject barrelModel = LoadModel("Assets/Models/ToxicBarrel.fbx");
        if (barrelModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("ToxicBarrel", "Assets/Models/ToxicBarrel.fbx",
                new Vector3(0.35f, 0.35f, 0.35f), PrimitiveType.Cylinder, new Color(0.15f, 0.5f, 0.1f), true));
        else
            obstaclePrefabs.Add(CreateToxicBarrelPrefab());

        GameObject blobModel = LoadModel("Assets/Models/PoopBlob.fbx");
        if (blobModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("PoopBlob", "Assets/Models/PoopBlob.fbx",
                new Vector3(0.4f, 0.3f, 0.4f), PrimitiveType.Sphere, new Color(0.4f, 0.25f, 0.1f), true));
        else
            obstaclePrefabs.Add(CreatePoopBlobPrefab());

        GameObject mineModel = LoadModel("Assets/Models/SewerMine.fbx");
        if (mineModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("SewerMine", "Assets/Models/SewerMine.fbx",
                new Vector3(0.3f, 0.3f, 0.3f), PrimitiveType.Sphere, new Color(0.25f, 0.25f, 0.25f), true));
        else
            obstaclePrefabs.Add(CreateSewerMinePrefab());

        GameObject roachModel = LoadModel("Assets/Models/Cockroach.fbx");
        if (roachModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("Cockroach", "Assets/Models/Cockroach.fbx",
                new Vector3(0.25f, 0.18f, 0.25f), PrimitiveType.Capsule, new Color(0.22f, 0.14f, 0.06f), true));
        else
            obstaclePrefabs.Add(CreateCockroachPrefab());

        // Hair Wads - comical hair blobs with googly eyes, multiple colors
        obstaclePrefabs.Add(CreateHairWadPrefab("HairWad_Black",
            new Color(0.12f, 0.08f, 0.06f), new Color(0.06f, 0.04f, 0.03f)));
        obstaclePrefabs.Add(CreateHairWadPrefab("HairWad_Blonde",
            new Color(0.85f, 0.7f, 0.35f), new Color(0.65f, 0.5f, 0.2f)));
        obstaclePrefabs.Add(CreateHairWadPrefab("HairWad_Red",
            new Color(0.6f, 0.15f, 0.08f), new Color(0.45f, 0.1f, 0.05f)));
        obstaclePrefabs.Add(CreateHairWadPrefab("HairWad_Brunette",
            new Color(0.35f, 0.2f, 0.1f), new Color(0.22f, 0.12f, 0.06f)));

        // Toxic Frog - squatty mutant frog that hops and lashes tongue
        obstaclePrefabs.Add(CreateToxicFrogPrefab());

        // Sewer Jellyfish - pulsing translucent blob with trailing tentacles
        obstaclePrefabs.Add(CreateSewerJellyfishPrefab());

        // Sewer Spider - creepy wall-hanger that drops down
        obstaclePrefabs.Add(CreateSewerSpiderPrefab());

        // Sewer Snake - slithering sine-wave across pipe
        obstaclePrefabs.Add(CreateSewerSnakePrefab());

        // TP Mummy - wrapped in toilet paper, unfurls when near
        obstaclePrefabs.Add(CreateTPMummyPrefab());

        // Grease Glob - slides along walls, drools, puffs up
        obstaclePrefabs.Add(CreateGreaseGlobPrefab());

        // Poop Fly Swarm - 8 orbiting flies that tighten toward player
        obstaclePrefabs.Add(CreatePoopFlySwarmPrefab());

        // Fartcoin - copper penny with $ emboss, sized like a real coin you'd find in a sewer
        GameObject coinPrefab = CreateFartcoinPrefab();

        // Apply creature-specific materials for better visual variety
        ApplyCreatureMaterials();

        // Special prefabs
        GameObject gratePrefab = CreateGratePrefab();
        GameObject bigAirRampPrefab = CreateBigAirRampPrefab();
        GameObject dropRingPrefab = CreateDropRingPrefab();
        GameObject dropZonePrefab = CreateDropZonePrefab(dropRingPrefab);

        // Wire to spawner
        ObstacleSpawner spawner = Object.FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null)
        {
            spawner.obstaclePrefabs = obstaclePrefabs.ToArray();
            spawner.coinPrefab = coinPrefab;
            spawner.gratePrefab = gratePrefab;
            spawner.bigAirRampPrefab = bigAirRampPrefab;
            spawner.dropZonePrefab = dropZonePrefab;
        }
    }

    /// <summary>
    /// Overrides generic UpgradeToURP materials on creature prefabs with unique surface properties.
    /// Each creature gets distinct metallic/smoothness/emission for visual identity.
    /// </summary>
    static void ApplyCreatureMaterials()
    {
        // creature name → (color, metallic, smoothness, emissionColor, emissionStrength)
        var creatures = new (string name, Color color, float metal, float smooth, Color emission, float emStr)[]
        {
            ("SewerRat",    new Color(0.32f, 0.2f, 0.1f),   0.05f, 0.25f, new Color(0.15f, 0.08f, 0.03f), 0.3f),  // rough fur
            ("ToxicBarrel", new Color(0.25f, 0.5f, 0.12f),  0.45f, 0.55f, new Color(0.1f, 0.6f, 0.05f),   2.5f),  // corroded metal + green glow
            ("PoopBlob",    new Color(0.35f, 0.2f, 0.08f),  0.05f, 0.78f, new Color(0.12f, 0.06f, 0.02f), 0.4f),  // wet glossy organic
            ("SewerMine",   new Color(0.08f, 0.08f, 0.08f), 0.3f,  0.5f,  new Color(0.05f, 0.03f, 0.01f), 0.2f),  // matte black bomb
            ("Cockroach",   new Color(0.3f, 0.15f, 0.06f),  0.15f, 0.8f,  new Color(0.05f, 0.02f, 0.01f), 0.2f),  // chitinous shell sheen
            ("ToxicFrog",   new Color(0.15f, 0.5f, 0.08f),  0.1f,  0.65f, new Color(0.05f, 0.3f, 0.02f),  1.5f),  // slimy green amphibian
            ("SewerJellyfish", new Color(0.2f, 0.6f, 0.5f), 0.0f,  0.9f,  new Color(0.08f, 0.5f, 0.35f),  3.0f),  // translucent bioluminescent
            ("SewerSpider",    new Color(0.12f, 0.1f, 0.08f), 0.05f, 0.4f,  new Color(0.03f, 0.01f, 0.01f), 0.15f), // dark matte chitin
        };

        foreach (var c in creatures)
        {
            string prefabPath = $"Assets/Prefabs/{c.name}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            GameObject instance = (GameObject)Object.Instantiate(prefab);

            Material creatureMat = MakeURPMat($"{c.name}_Creature", c.color, c.metal, c.smooth);
            creatureMat.EnableKeyword("_EMISSION");
            creatureMat.SetColor("_EmissionColor", c.emission * c.emStr);
            EditorUtility.SetDirty(creatureMat);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                // Skip face feature primitives (eyes, pupils, etc.)
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    string mn = mf.sharedMesh.name;
                    if (mn == "Sphere" || mn == "Capsule" || mn == "Cube" ||
                        mn == "Quad" || mn == "Cylinder")
                        continue;
                }

                // PRESERVE FBX TEXTURES: if the material already has a BaseMap texture,
                // tint it with the creature color instead of replacing with solid color.
                // This keeps the Blender-baked textures while adding our visual identity.
                Material[] mats = r.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    Material existing = mats[mi];
                    Texture existingTex = null;
                    Texture existingNormal = null;
                    if (existing != null)
                    {
                        // Check ALL common texture property names (FBX uses _MainTex, GLB/URP uses _BaseMap)
                        if (existing.HasProperty("_BaseMap"))
                            existingTex = existing.GetTexture("_BaseMap");
                        if (existingTex == null && existing.HasProperty("_MainTex"))
                            existingTex = existing.GetTexture("_MainTex");
                        if (existingTex == null && existing.HasProperty("_ColorMap"))
                            existingTex = existing.GetTexture("_ColorMap");
                        if (existingTex == null && existing.HasProperty("_Albedo"))
                            existingTex = existing.GetTexture("_Albedo");
                        // Universal fallback: scan ALL texture properties
                        if (existingTex == null)
                        {
                            try {
                                string[] texProps = existing.GetTexturePropertyNames();
                                foreach (string prop in texProps)
                                {
                                    Texture t = existing.GetTexture(prop);
                                    if (t != null && t is Texture2D)
                                    {
                                        string lp = prop.ToLower();
                                        if (lp.Contains("normal") || lp.Contains("bump")) { existingNormal = t; continue; }
                                        existingTex = t;
                                        Debug.Log($"TTR: ApplyCreatureMaterials found texture '{t.name}' via '{prop}' on {c.name}");
                                        break;
                                    }
                                }
                            } catch { }
                        }
                        if (existingNormal == null && existing.HasProperty("_BumpMap"))
                            existingNormal = existing.GetTexture("_BumpMap");
                        if (existingNormal == null && existing.HasProperty("_NormalMap"))
                            existingNormal = existing.GetTexture("_NormalMap");
                    }

                    if (existingTex != null)
                    {
                        // Has texture from FBX - create material that preserves it
                        Material texMat = new Material(creatureMat);
                        texMat.name = $"{c.name}_{existing.name}_Tex";
                        texMat.SetTexture("_BaseMap", existingTex);
                        if (existingNormal != null)
                        {
                            texMat.SetTexture("_BumpMap", existingNormal);
                            texMat.EnableKeyword("_NORMALMAP");
                        }
                        // Use a lighter tint so texture is visible through it
                        Color tint = Color.Lerp(Color.white, c.color, 0.3f);
                        texMat.SetColor("_BaseColor", tint);
                        newMats[mi] = SaveMaterial(texMat);
                    }
                    else
                    {
                        newMats[mi] = creatureMat;
                    }
                }
                r.sharedMaterials = newMats;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"TTR: Applied creature material to {c.name} (metal={c.metal}, smooth={c.smooth}, textures preserved)");
        }
    }

    // ===== SCENERY SPAWNER =====
    static void CreateScenerySpawner(Transform player)
    {
        GameObject obj = new GameObject("ScenerySpawner");
        ScenerySpawner spawner = obj.AddComponent<ScenerySpawner>();
        spawner.player = player;
        spawner.pipeRadius = 3.5f;

        // Regular scenery (valve wheels, grates, etc.)
        List<GameObject> sceneryPrefabs = new List<GameObject>();

        sceneryPrefabs.Add(EnsureSceneryPrefab("ValveWheel", "Assets/Models/ValveWheel.fbx",
            new Vector3(0.4f, 0.4f, 0.4f), new Color(0.4f, 0.35f, 0.25f)));
        sceneryPrefabs.Add(EnsureSceneryPrefab("SewerGrate", "Assets/Models/SewerGrate.fbx",
            new Vector3(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.3f, 0.28f)));
        sceneryPrefabs.Add(EnsureSceneryPrefab("Mushroom", "Assets/Models/Mushroom.fbx",
            new Vector3(0.35f, 0.35f, 0.35f), new Color(0.6f, 0.5f, 0.3f)));
        sceneryPrefabs.Add(EnsureSceneryPrefab("FishBone", "Assets/Models/FishBone.fbx",
            new Vector3(0.3f, 0.3f, 0.3f), new Color(0.85f, 0.82f, 0.7f)));
        sceneryPrefabs.Add(EnsureSceneryPrefab("ManholeCover", "Assets/Models/ManholeCover.fbx",
            new Vector3(0.45f, 0.45f, 0.45f), new Color(0.35f, 0.33f, 0.3f)));
        sceneryPrefabs.Add(EnsureSceneryPrefab("ToiletSeat", "Assets/Models/ToiletSeat.fbx",
            new Vector3(0.35f, 0.35f, 0.35f), new Color(0.9f, 0.9f, 0.85f)));

        spawner.sceneryPrefabs = sceneryPrefabs.ToArray();

        // Gross pipe decor - stains, drips, cracks (no signs - those go in signPrefabs)
        List<GameObject> grossPrefabs = new List<GameObject>();
        grossPrefabs.Add(CreateSlimeDripPrefab());
        grossPrefabs.Add(CreateGrimeStainPrefab());
        grossPrefabs.Add(CreateCrackDecalPrefab());
        grossPrefabs.Add(CreateRustDripPrefab());

        spawner.grossPrefabs = grossPrefabs.ToArray();

        // Signs, ads, graffiti, warnings - spawned separately on walls for readability
        // Build multiple variants so each is unique
        List<GameObject> signPrefabs = new List<GameObject>();
        for (int i = 0; i < 4; i++)
            signPrefabs.Add(CreateGraffitiSignPrefab());
        for (int i = 0; i < 4; i++)
            signPrefabs.Add(CreateSewerAdPrefab());
        for (int i = 0; i < 3; i++)
            signPrefabs.Add(CreateWarningSignPrefab());
        for (int i = 0; i < 3; i++)
            signPrefabs.Add(CreatePipeNumberPrefab());
        for (int i = 0; i < 3; i++)
            signPrefabs.Add(CreateArrowSignPrefab());
        for (int i = 0; i < 3; i++)
            signPrefabs.Add(CreateWantedPosterPrefab());

        spawner.signPrefabs = signPrefabs.ToArray();
        Debug.Log($"TTR: Created 4 gross decor types + {signPrefabs.Count} sign/graffiti variants (incl arrows + wanted posters).");
    }

    // ===== GROSS PIPE CHARACTER PREFABS =====

    /// <summary>Green slime drip hanging from pipe surface with gooey droplets.</summary>
    static GameObject CreateSlimeDripPrefab()
    {
        string path = "Assets/Prefabs/SlimeDrip_Gross.prefab";
        GameObject root = new GameObject("SlimeDrip");

        Material slimeMat = MakeURPMat("Gross_Slime", new Color(0.15f, 0.55f, 0.08f), 0.1f, 0.92f);
        slimeMat.EnableKeyword("_EMISSION");
        slimeMat.SetColor("_EmissionColor", new Color(0.08f, 0.35f, 0.04f) * 1.2f);
        EditorUtility.SetDirty(slimeMat);

        // Main drip blob on wall - BIGGER for visibility
        AddPrimChild(root, "Blob", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.4f, 0.25f, 0.35f), slimeMat);
        // Dripping strand hanging down - thicker
        AddPrimChild(root, "Strand1", PrimitiveType.Capsule, new Vector3(0, -0.3f, 0),
            Quaternion.identity, new Vector3(0.1f, 0.3f, 0.1f), slimeMat);
        AddPrimChild(root, "Strand2", PrimitiveType.Capsule, new Vector3(0.08f, -0.5f, 0.03f),
            Quaternion.identity, new Vector3(0.07f, 0.18f, 0.07f), slimeMat);
        // Droplet at tip
        AddPrimChild(root, "Drop", PrimitiveType.Sphere, new Vector3(0.05f, -0.7f, 0.01f),
            Quaternion.identity, Vector3.one * 0.12f, slimeMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.5f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Dark brownish grime stain splattered on pipe wall.</summary>
    static GameObject CreateGrimeStainPrefab()
    {
        string path = "Assets/Prefabs/GrimeStain_Gross.prefab";
        GameObject root = new GameObject("GrimeStain");

        Material grimeMat = MakeURPMat("Gross_Grime", new Color(0.18f, 0.12f, 0.06f), 0f, 0.15f);
        Material grimeWet = MakeURPMat("Gross_GrimeWet", new Color(0.12f, 0.08f, 0.04f), 0.1f, 0.65f);
        grimeWet.EnableKeyword("_EMISSION");
        grimeWet.SetColor("_EmissionColor", new Color(0.03f, 0.02f, 0.01f));
        EditorUtility.SetDirty(grimeWet);

        // Larger irregular splatter
        AddPrimChild(root, "Splat0", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.55f, 0.08f, 0.45f), grimeMat);
        AddPrimChild(root, "Splat1", PrimitiveType.Sphere, new Vector3(0.18f, 0.01f, 0.12f),
            Quaternion.Euler(0, 25, 0), new Vector3(0.35f, 0.06f, 0.4f), grimeWet);
        AddPrimChild(root, "Splat2", PrimitiveType.Sphere, new Vector3(-0.15f, 0.01f, -0.1f),
            Quaternion.Euler(0, -15, 0), new Vector3(0.3f, 0.06f, 0.35f), grimeMat);
        // Extra drip edge
        AddPrimChild(root, "Drip", PrimitiveType.Capsule, new Vector3(0, -0.15f, 0),
            Quaternion.identity, new Vector3(0.06f, 0.15f, 0.06f), grimeWet);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.5f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Cluster of wriggling worms poking out of pipe wall.</summary>
    static GameObject CreateWormClusterPrefab()
    {
        string path = "Assets/Prefabs/WormCluster_Gross.prefab";
        GameObject root = new GameObject("WormCluster");

        Material wormPink = MakeURPMat("Gross_WormPink", new Color(0.7f, 0.4f, 0.45f), 0f, 0.6f);
        Material wormBrown = MakeURPMat("Gross_WormBrown", new Color(0.45f, 0.25f, 0.15f), 0f, 0.5f);

        // 4-6 worms poking out at different angles
        int wormCount = 5;
        for (int i = 0; i < wormCount; i++)
        {
            float a = (i - 2) * 30f;
            float len = 0.15f + (i % 3) * 0.06f;
            Vector3 pos = new Vector3((i - 2) * 0.06f, 0, 0);
            Quaternion rot = Quaternion.Euler(a, (i * 40) % 360, (i * 25) % 90 - 45);
            Material mat = (i % 2 == 0) ? wormPink : wormBrown;
            AddPrimChild(root, $"Worm{i}", PrimitiveType.Capsule, pos,
                rot, new Vector3(0.03f, len, 0.03f), mat);
        }

        // Dirt hole they come out of
        Material dirtMat = MakeURPMat("Gross_Dirt", new Color(0.15f, 0.1f, 0.05f), 0f, 0.2f);
        AddPrimChild(root, "Hole", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.15f, 0.04f, 0.15f), dirtMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.8f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Fuzzy mold patch - yellowish-green growth on pipe wall.</summary>
    static GameObject CreateMoldPatchPrefab()
    {
        string path = "Assets/Prefabs/MoldPatch_Gross.prefab";
        GameObject root = new GameObject("MoldPatch");

        Material moldMat = MakeURPMat("Gross_Mold", new Color(0.35f, 0.45f, 0.15f), 0f, 0.1f);
        Material moldDark = MakeURPMat("Gross_MoldDark", new Color(0.2f, 0.3f, 0.08f), 0f, 0.08f);

        // Organic fuzzy growth from overlapping flat spheres
        AddPrimChild(root, "Core", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.3f, 0.06f, 0.28f), moldMat);
        AddPrimChild(root, "Edge1", PrimitiveType.Sphere, new Vector3(0.15f, 0, 0.08f),
            Quaternion.identity, new Vector3(0.2f, 0.05f, 0.18f), moldDark);
        AddPrimChild(root, "Edge2", PrimitiveType.Sphere, new Vector3(-0.08f, 0, -0.12f),
            Quaternion.identity, new Vector3(0.22f, 0.05f, 0.16f), moldMat);
        // Spore bumps
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 0.08f;
            float z = ((i * 37) % 20 - 10) * 0.01f;
            AddPrimChild(root, $"Spore{i}", PrimitiveType.Sphere,
                new Vector3(x, 0.03f, z), Quaternion.identity,
                Vector3.one * 0.04f, moldDark);
        }

        RemoveAllColliders(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Cluster of sewer gas bubbles on pipe surface - some popping.</summary>
    static GameObject CreateBubbleClusterPrefab()
    {
        string path = "Assets/Prefabs/BubbleCluster_Gross.prefab";
        GameObject root = new GameObject("BubbleCluster");

        Material bubbleMat = MakeURPMat("Gross_Bubble", new Color(0.3f, 0.4f, 0.2f), 0.05f, 0.95f);
        bubbleMat.EnableKeyword("_EMISSION");
        bubbleMat.SetColor("_EmissionColor", new Color(0.15f, 0.2f, 0.1f) * 0.5f);
        EditorUtility.SetDirty(bubbleMat);

        // Various size bubbles clustered together
        float[] sizes = { 0.08f, 0.12f, 0.06f, 0.1f, 0.05f, 0.09f };
        for (int i = 0; i < sizes.Length; i++)
        {
            float x = (i - 2.5f) * 0.06f;
            float z = ((i * 31) % 10 - 5) * 0.01f;
            float y = sizes[i] * 0.3f;
            AddPrimChild(root, $"Bubble{i}", PrimitiveType.Sphere,
                new Vector3(x, y, z), Quaternion.identity,
                Vector3.one * sizes[i], bubbleMat);
        }

        RemoveAllColliders(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Crude sewer graffiti / warning sign on pipe wall.</summary>
    /// <summary>Yellow warning sign with humorous hazard text.</summary>
    static readonly string[] WarningTexts = {
        "DANGER\nTOXIC\nGAS", "CAUTION\nWET\nFLOOR", "BEWARE\nOF\nRATS",
        "WARNING\nSLIPPERY\nWHEN GROSS", "DANGER\nDO NOT\nSWIM", "CAUTION\nFALLING\nTURDS",
        "WARNING\nHIGH\nSTINK ZONE", "DANGER\nLOW\nFLUSH AREA", "CAUTION\nGATOR\nCROSSING",
        "WARNING\nBROWN\nWATER ONLY", "DANGER\nUNSTABLE\nFLOATERS", "CAUTION\nEXPLOSIVE\nGAS BUILDUP",
        "WARNING\nMUTANT\nGOLDFISH", "DANGER\nPIPE\nNARROWS AHEAD", "CAUTION\nRATTLESNAKE\nNEST",
        "WARNING\nDO NOT\nLICK WALLS", "NOTICE\nNO DIVING\n(SERIOUSLY)", "DANGER\nEELS\nBELOW"
    };
    static int _warningIndex = 0;

    static GameObject CreateWarningSignPrefab()
    {
        string path = $"Assets/Prefabs/WarningSign_Gross_{_warningIndex}.prefab";
        GameObject root = new GameObject("WarningSign");

        // Spray paint stencil style — yellow text on pipe surface
        Color yellow = new Color(0.95f, 0.85f, 0.1f);

        // Overspray haze — flat cube BEHIND the text
        Material hazeMat = MakeURPMat("Gross_WarnHaze", new Color(yellow.r, yellow.g, yellow.b, 0.10f), 0f, 0.1f);
        hazeMat.EnableKeyword("_EMISSION");
        hazeMat.SetColor("_EmissionColor", yellow * 0.05f);
        if (hazeMat.HasProperty("_Surface"))
        {
            hazeMat.SetFloat("_Surface", 1);
            hazeMat.SetOverrideTag("RenderType", "Transparent");
            hazeMat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(hazeMat);
        // Positive z = toward pipe center = visible to player inside pipe
        AddPrimChild(root, "Haze", PrimitiveType.Cube, new Vector3(0, 0, 0.003f),
            Quaternion.identity, new Vector3(0.6f, 0.35f, 0.001f), hazeMat);

        string txt = WarningTexts[_warningIndex % WarningTexts.Length];
        _warningIndex++;

        // Spray-painted warning text — visible inside pipe
        GameObject textObj = new GameObject("SprayText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, 0.006f);
        textObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-6f, 6f));
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = txt;
        tm.fontSize = 56;
        tm.characterSize = 0.045f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = yellow;
        tm.fontStyle = FontStyle.Bold;
        ConfigureSignText(tm);

        // Stencil-style hazard stripes
        Material stripeMat = MakeURPMat("Gross_WarnStripe", new Color(0.1f, 0.08f, 0.05f), 0f, 0.15f);
        stripeMat.EnableKeyword("_EMISSION");
        stripeMat.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.01f));
        EditorUtility.SetDirty(stripeMat);
        for (int s = 0; s < 3; s++)
        {
            float x = -0.08f + s * 0.08f;
            AddPrimChild(root, $"Stripe{s}", PrimitiveType.Cube,
                new Vector3(x, 0.08f, 0.005f),
                Quaternion.Euler(0, 0, 45), new Vector3(0.12f, 0.008f, 0.002f), stripeMat);
        }

        // Paint drips
        Material dripMat = MakeURPMat("Gross_WarnDrip", yellow * 0.7f, 0f, 0.15f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", yellow * 0.1f);
        EditorUtility.SetDirty(dripMat);
        for (int d = 0; d < 3; d++)
        {
            float x = Random.Range(-0.08f, 0.08f);
            float len = Random.Range(0.02f, 0.07f);
            AddPrimChild(root, $"Drip{d}", PrimitiveType.Capsule,
                new Vector3(x, -0.06f - d * 0.02f, 0.005f),
                Quaternion.identity, new Vector3(0.005f, len, 0.003f), dripMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f; // bigger for readability at speed
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Pipe section number marker - "SECTOR 7G" style.</summary>
    static readonly string[] PipeNumbers = {
        "SECTOR 7G", "PIPE 42", "TUNNEL B-12", "DRAIN 69",
        "ZONE 404", "DUCT 13", "SHAFT 99", "MAIN 1A",
        "OVERFLOW 3", "JUNCTION 8", "SLUDGE LINE 5", "OUTFALL 17",
        "BYPASS C-4", "TRUNK 88", "LATERAL 2B", "INTERCEPTOR 6"
    };
    static int _pipeNumIndex = 0;

    static GameObject CreatePipeNumberPrefab()
    {
        string path = $"Assets/Prefabs/PipeNumber_Gross_{_pipeNumIndex}.prefab";
        GameObject root = new GameObject("PipeNumber");

        // Stenciled sector number — white spray paint on pipe
        Color stencilCol = new Color(0.85f, 0.82f, 0.7f);

        // Overspray haze — flat cube BEHIND the text
        Material hazeMat = MakeURPMat("Gross_NumHaze", new Color(stencilCol.r, stencilCol.g, stencilCol.b, 0.06f), 0f, 0.1f);
        hazeMat.EnableKeyword("_EMISSION");
        hazeMat.SetColor("_EmissionColor", stencilCol * 0.03f);
        if (hazeMat.HasProperty("_Surface"))
        {
            hazeMat.SetFloat("_Surface", 1);
            hazeMat.SetOverrideTag("RenderType", "Transparent");
            hazeMat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(hazeMat);
        // Positive z = toward player inside pipe
        AddPrimChild(root, "Haze", PrimitiveType.Cube, new Vector3(0, 0, 0.003f),
            Quaternion.identity, new Vector3(0.55f, 0.2f, 0.001f), hazeMat);

        string num = PipeNumbers[_pipeNumIndex % PipeNumbers.Length];
        _pipeNumIndex++;

        // Stenciled text — visible inside pipe
        GameObject textObj = new GameObject("SprayText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, 0.006f);
        textObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-3f, 3f));
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = num;
        tm.fontSize = 44;
        tm.characterSize = 0.03f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = stencilCol;
        tm.fontStyle = FontStyle.Bold;
        ConfigureSignText(tm);

        // Underline spray stroke
        Material lineMat = MakeURPMat("Gross_NumLine", stencilCol * 0.6f, 0f, 0.15f);
        lineMat.EnableKeyword("_EMISSION");
        lineMat.SetColor("_EmissionColor", stencilCol * 0.08f);
        EditorUtility.SetDirty(lineMat);
        AddPrimChild(root, "Underline", PrimitiveType.Cube,
            new Vector3(0, -0.04f, 0.005f), Quaternion.identity,
            new Vector3(0.25f, 0.005f, 0.002f), lineMat);

        // Paint drips
        Material dripMat = MakeURPMat("Gross_NumDrip", stencilCol * 0.5f, 0f, 0.15f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", stencilCol * 0.06f);
        EditorUtility.SetDirty(dripMat);
        for (int d = 0; d < 2; d++)
        {
            float x = Random.Range(-0.1f, 0.1f);
            float len = Random.Range(0.015f, 0.04f);
            AddPrimChild(root, $"Drip{d}", PrimitiveType.Capsule,
                new Vector3(x, -0.05f - d * 0.015f, 0.005f),
                Quaternion.identity, new Vector3(0.004f, len, 0.002f), dripMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f; // bigger for readability at speed
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Spray paint graffiti messages - sewer humor
    static readonly string[] GraffitiMessages = new string[]
    {
        "FLUSH\nTHE\nSYSTEM", "TURDS\nRULE", "POO WAS\nHERE",
        "EAT AT\nJOE'S\n(don't)", "NO\nFLOATING", "CORN\nPOWER",
        "SEWER\nLIFE", "PLUNGE\nOR DIE", "WHO\nFARTED?",
        "DUMP\nSTREET", "BROWN\nTOWN\nPOP: YOU", "WIPE\nBETTER",
        "THIS\nPIPE\nSUCKS", "FLUSH\nTWICE", "SEWER\nSURF\nCLUB",
        "RATS\nWERE\nHERE", "DRAIN\nGANG", "POO\nCREW",
        "STINK\nOR\nSWIM", "MR CORNY\nWAS HERE",
        "LIVE\nLAUGH\nFLUSH", "NOT ALL\nWHO WANDER\nARE FLUSHED",
        "CORNY\n4\nPRESIDENT", "SEWER\nPUNKS\nNOT DEAD",
        "EAT\nSLEEP\nFLUSH\nREPEAT", "PIPE\nDREAMS", "DRIP\nDROP\nPLOP",
        "RATED\nE FOR\nEWWW", "TURD\nBURGLAR\nWAS HERE", "FLOATERS\nUNITE",
        "WHAT\nGOES DOWN\nMUST COME UP", "LOG\nJAM\nAHEAD", "FREE\nCORN\n(USED)",
        "TOILET\nHUMOR\nIS VALID", "THE\nPIPES\nHAVE EARS", "BORN\nTO\nFLOAT",
        "KEEP\nCALM\nAND FLUSH", "DOOKIE\nWAS HERE\n10/10", "FLUSH\nMOB\n2024",
        "SEWAGE\nIS JUST\nLIQUID HISTORY", "ONE MAN'S\nTRASH IS\nOUR HOME",
        "PLUMBER\nTRUTHER", "GRAVITY\nIS A\nFLUSH MYTH", "I CAME\nI SAW\nI FLUSHED"
    };

    static int _graffitiIndex = 0;

    static GameObject CreateGraffitiSignPrefab()
    {
        string path = $"Assets/Prefabs/GraffitiSign_Gross_{_graffitiIndex}.prefab";
        GameObject root = new GameObject("GraffitiSign");

        // Spray paint colors
        Color[] sprayColors = new Color[]
        {
            new Color(0.9f, 0.15f, 0.1f),  // red
            new Color(0.1f, 0.85f, 0.2f),  // green
            new Color(0.2f, 0.5f, 0.95f),  // blue
            new Color(0.95f, 0.85f, 0.1f), // yellow
            new Color(0.9f, 0.3f, 0.8f),   // pink
            new Color(1f, 0.5f, 0.1f),     // orange
        };
        Color sprayColor = sprayColors[_graffitiIndex % sprayColors.Length];

        // Overspray haze — flat quad BEHIND the text (use Cube with tiny Z to avoid
        // sphere geometry poking through and occluding the text)
        Material hazeMat = MakeURPMat("Gross_GrafHaze", new Color(sprayColor.r, sprayColor.g, sprayColor.b, 0.08f), 0f, 0.1f);
        hazeMat.EnableKeyword("_EMISSION");
        hazeMat.SetColor("_EmissionColor", sprayColor * 0.04f);
        if (hazeMat.HasProperty("_Surface"))
        {
            hazeMat.SetFloat("_Surface", 1); // Transparent
            hazeMat.SetFloat("_Blend", 0);   // Alpha blend
            hazeMat.SetOverrideTag("RenderType", "Transparent");
            hazeMat.renderQueue = 3000; // render before text
        }
        EditorUtility.SetDirty(hazeMat);
        // Haze slightly behind text but still inside pipe (positive z = toward player)
        AddPrimChild(root, "Haze", PrimitiveType.Cube, new Vector3(0, 0, 0.003f),
            Quaternion.identity, new Vector3(0.65f, 0.35f, 0.001f), hazeMat);

        // Spray painted text — in front of haze, facing player inside the pipe
        // Sign faces INWARD so positive Z = toward pipe center = toward player = visible
        string msg = GraffitiMessages[_graffitiIndex % GraffitiMessages.Length];
        _graffitiIndex++;

        GameObject textObj = new GameObject("SprayText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, 0.006f);
        textObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));

        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = msg;
        tm.fontSize = 64;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = sprayColor;
        tm.fontStyle = FontStyle.Bold;
        ConfigureSignText(tm);

        // Paint drips (more for that authentic spray-paint feel)
        Material dripMat = MakeURPMat("Gross_Paint", sprayColor * 0.7f, 0f, 0.15f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", sprayColor * 0.15f);
        EditorUtility.SetDirty(dripMat);

        for (int i = 0; i < 4; i++)
        {
            float x = Random.Range(-0.1f, 0.1f);
            float len = Random.Range(0.02f, 0.08f);
            float thick = Random.Range(0.004f, 0.007f);
            AddPrimChild(root, $"Drip{i}", PrimitiveType.Capsule,
                new Vector3(x, -0.07f - i * 0.025f, 0.005f),
                Quaternion.identity, new Vector3(thick, len, 0.003f), dripMat);
        }

        // Splatter dots (overspray specks)
        Material splatMat = MakeURPMat("Gross_Splat", sprayColor * 0.8f, 0f, 0.2f);
        splatMat.EnableKeyword("_EMISSION");
        splatMat.SetColor("_EmissionColor", sprayColor * 0.1f);
        EditorUtility.SetDirty(splatMat);
        for (int s = 0; s < 3; s++)
        {
            Vector3 pos = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.1f), 0.005f);
            float sz = Random.Range(0.008f, 0.015f);
            AddPrimChild(root, $"Splat{s}", PrimitiveType.Sphere, pos,
                Quaternion.identity, new Vector3(sz, sz, 0.002f), splatMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f; // bigger for readability at speed
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Crack / damage in pipe wall with debris.</summary>
    static GameObject CreateCrackDecalPrefab()
    {
        string path = "Assets/Prefabs/CrackDecal_Gross.prefab";
        GameObject root = new GameObject("CrackDecal");

        Material crackMat = MakeURPMat("Gross_Crack", new Color(0.08f, 0.06f, 0.04f), 0f, 0.15f);
        Material debrisMat = MakeURPMat("Gross_Debris", new Color(0.35f, 0.3f, 0.22f), 0.1f, 0.2f);

        // Dark crack lines
        AddPrimChild(root, "CrackMain", PrimitiveType.Cube, Vector3.zero,
            Quaternion.Euler(0, 0, 25), new Vector3(0.35f, 0.02f, 0.015f), crackMat);
        AddPrimChild(root, "CrackBranch1", PrimitiveType.Cube, new Vector3(0.1f, 0.03f, 0),
            Quaternion.Euler(0, 0, -35), new Vector3(0.15f, 0.015f, 0.012f), crackMat);
        AddPrimChild(root, "CrackBranch2", PrimitiveType.Cube, new Vector3(-0.08f, -0.02f, 0),
            Quaternion.Euler(0, 0, 50), new Vector3(0.12f, 0.015f, 0.012f), crackMat);
        // Small debris chunks
        AddPrimChild(root, "Debris1", PrimitiveType.Cube, new Vector3(0.05f, -0.06f, 0.01f),
            Quaternion.Euler(15, 30, 45), Vector3.one * 0.03f, debrisMat);
        AddPrimChild(root, "Debris2", PrimitiveType.Cube, new Vector3(-0.04f, -0.05f, 0.008f),
            Quaternion.Euler(40, -20, 10), Vector3.one * 0.025f, debrisMat);

        RemoveAllColliders(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Rusty brown drip stain running down pipe wall.</summary>
    static GameObject CreateRustDripPrefab()
    {
        string path = "Assets/Prefabs/RustDrip_Gross.prefab";
        GameObject root = new GameObject("RustDrip");

        Material rustMat = MakeURPMat("Gross_Rust", new Color(0.5f, 0.28f, 0.1f), 0.15f, 0.25f);
        Material rustDark = MakeURPMat("Gross_RustDark", new Color(0.35f, 0.18f, 0.06f), 0.2f, 0.3f);

        // Source point (bolt or crack)
        AddPrimChild(root, "Source", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.08f, 0.06f, 0.04f), rustDark);
        // Main drip trail running down
        AddPrimChild(root, "Trail", PrimitiveType.Cube, new Vector3(0, -0.15f, 0),
            Quaternion.identity, new Vector3(0.04f, 0.25f, 0.015f), rustMat);
        // Wider stain at bottom
        AddPrimChild(root, "Spread", PrimitiveType.Sphere, new Vector3(0, -0.28f, 0),
            Quaternion.identity, new Vector3(0.12f, 0.06f, 0.02f), rustMat);
        // Branch drip
        AddPrimChild(root, "Branch", PrimitiveType.Cube, new Vector3(0.03f, -0.08f, 0),
            Quaternion.Euler(0, 0, -25), new Vector3(0.02f, 0.1f, 0.012f), rustDark);

        RemoveAllColliders(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Side pipe poking through the sewer wall with dripping water.</summary>
    static GameObject CreateSidePipePrefab()
    {
        string path = "Assets/Prefabs/SidePipe_Gross.prefab";
        GameObject root = new GameObject("SidePipe");

        Material pipeMetal = MakeURPMat("Gross_PipeMetal", new Color(0.35f, 0.35f, 0.32f), 0.5f, 0.4f);
        Material pipeRust = MakeURPMat("Gross_PipeRust", new Color(0.45f, 0.25f, 0.1f), 0.3f, 0.25f);
        Material waterDrip = MakeURPMat("Gross_WaterDrip", new Color(0.15f, 0.25f, 0.08f), 0.5f, 0.9f);
        waterDrip.EnableKeyword("_EMISSION");
        waterDrip.SetColor("_EmissionColor", new Color(0.04f, 0.08f, 0.02f));
        EditorUtility.SetDirty(waterDrip);

        // Main pipe cylinder poking out of wall
        AddPrimChild(root, "Pipe", PrimitiveType.Cylinder, new Vector3(0, 0, 0.15f),
            Quaternion.Euler(90, 0, 0), new Vector3(0.2f, 0.2f, 0.2f), pipeMetal);
        // Pipe flange/collar at wall
        AddPrimChild(root, "Flange", PrimitiveType.Cylinder, Vector3.zero,
            Quaternion.Euler(90, 0, 0), new Vector3(0.28f, 0.03f, 0.28f), pipeRust);
        // Rust stain below pipe
        AddPrimChild(root, "RustStain", PrimitiveType.Sphere, new Vector3(0, -0.15f, 0.02f),
            Quaternion.identity, new Vector3(0.15f, 0.2f, 0.02f), pipeRust);
        // Water stream dripping from pipe opening
        AddPrimChild(root, "WaterStream", PrimitiveType.Capsule, new Vector3(0, -0.12f, 0.25f),
            Quaternion.identity, new Vector3(0.04f, 0.1f, 0.04f), waterDrip);
        // Splash puddle at bottom
        AddPrimChild(root, "Splash", PrimitiveType.Sphere, new Vector3(0, -0.25f, 0.2f),
            Quaternion.identity, new Vector3(0.12f, 0.02f, 0.12f), waterDrip);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.5f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Vertical pipe from above with waterfall cascade effect.</summary>
    static GameObject CreateWaterfallPipePrefab()
    {
        string path = "Assets/Prefabs/WaterfallPipe_Gross.prefab";
        GameObject root = new GameObject("WaterfallPipe");

        Material pipeMetal = MakeURPMat("Gross_WfPipeMetal", new Color(0.3f, 0.3f, 0.28f), 0.45f, 0.35f);
        Material waterFall = MakeURPMat("Gross_Waterfall", new Color(0.12f, 0.22f, 0.06f), 0.4f, 0.85f);
        waterFall.EnableKeyword("_EMISSION");
        waterFall.SetColor("_EmissionColor", new Color(0.06f, 0.12f, 0.03f) * 1.5f);
        EditorUtility.SetDirty(waterFall);
        Material splashMat = MakeURPMat("Gross_WfSplash", new Color(0.18f, 0.28f, 0.1f), 0.3f, 0.8f);
        splashMat.EnableKeyword("_EMISSION");
        splashMat.SetColor("_EmissionColor", new Color(0.05f, 0.1f, 0.03f));
        EditorUtility.SetDirty(splashMat);

        // Vertical pipe from ceiling
        AddPrimChild(root, "VertPipe", PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0),
            Quaternion.identity, new Vector3(0.18f, 0.3f, 0.18f), pipeMetal);
        // Pipe opening rim
        AddPrimChild(root, "Rim", PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0),
            Quaternion.identity, new Vector3(0.22f, 0.02f, 0.22f), pipeMetal);
        // Cascading water column
        AddPrimChild(root, "WaterCol", PrimitiveType.Capsule, new Vector3(0, -0.15f, 0),
            Quaternion.identity, new Vector3(0.1f, 0.25f, 0.1f), waterFall);
        // Splash impact rings (multiple overlapping)
        for (int i = 0; i < 3; i++)
        {
            float s = 0.15f + i * 0.08f;
            float a = 0.8f - i * 0.2f;
            Material ringMat = new Material(splashMat.shader);
            ringMat.CopyPropertiesFromMaterial(splashMat);
            Color c = splashMat.GetColor("_BaseColor"); c.a = a;
            ringMat.SetColor("_BaseColor", c);
            AddPrimChild(root, $"SplashRing{i}", PrimitiveType.Cylinder,
                new Vector3(0, -0.38f, 0), Quaternion.identity,
                new Vector3(s, 0.01f, s), splashMat);
        }
        // Mist particles (small spheres around splash)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 0.12f, -0.3f, Mathf.Sin(angle) * 0.12f);
            AddPrimChild(root, $"Mist{i}", PrimitiveType.Sphere,
                pos, Quaternion.identity, Vector3.one * 0.04f, splashMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.8f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Large rust patch with peeling paint and corrosion detail.</summary>
    static GameObject CreateLargeRustPatchPrefab()
    {
        string path = "Assets/Prefabs/LargeRust_Gross.prefab";
        GameObject root = new GameObject("LargeRust");

        Material rustBase = MakeURPMat("Gross_LargeRust", new Color(0.5f, 0.3f, 0.12f), 0.25f, 0.2f);
        Material rustDark = MakeURPMat("Gross_LargeRustDark", new Color(0.3f, 0.15f, 0.05f), 0.3f, 0.15f);
        Material paintPeel = MakeURPMat("Gross_PaintPeel", new Color(0.2f, 0.25f, 0.18f), 0.05f, 0.3f);

        // Base rust stain (large flat area)
        AddPrimChild(root, "Base", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.4f, 0.3f, 0.015f), rustBase);
        // Darker center corrosion
        AddPrimChild(root, "Center", PrimitiveType.Sphere, new Vector3(0.02f, -0.03f, -0.005f),
            Quaternion.identity, new Vector3(0.2f, 0.15f, 0.01f), rustDark);
        // Peeling paint flakes
        for (int i = 0; i < 5; i++)
        {
            float ax = Random.Range(-0.15f, 0.15f);
            float ay = Random.Range(-0.1f, 0.1f);
            AddPrimChild(root, $"Peel{i}", PrimitiveType.Cube,
                new Vector3(ax, ay, 0.005f),
                Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-30f, 30f)),
                new Vector3(0.06f, 0.04f, 0.003f), paintPeel);
        }
        // Bolt holes
        Material boltMat = MakeURPMat("Gross_Bolt", new Color(0.25f, 0.22f, 0.18f), 0.5f, 0.4f);
        AddPrimChild(root, "Bolt1", PrimitiveType.Sphere, new Vector3(-0.12f, 0.08f, 0.01f),
            Quaternion.identity, Vector3.one * 0.025f, boltMat);
        AddPrimChild(root, "Bolt2", PrimitiveType.Sphere, new Vector3(0.1f, -0.06f, 0.01f),
            Quaternion.identity, Vector3.one * 0.025f, boltMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Funny poop-themed sewer advertisement billboard.</summary>
    // Funny sewer advertisement content
    static readonly string[][] SewerAds = new string[][]
    {
        new[] { "PIPE DREAMS", "Real Estate", "\"Live Where\nYou Flush!\"", "Luxury Sewage\nCondos from $9.99" },
        new[] { "BROWN TOWN", "Tourism Board", "\"Come for the smell\nStay because\nyou're stuck\"", "Visit Today!" },
        new[] { "UNCLE PLUNGER'S", "Plumbing Co.", "\"We'll unclog\nyour life!\"", "Call 1-800-PLU-NGER" },
        new[] { "TURD INSURANCE", "by PoopState", "\"Covered in case\nof a bad flush\"", "Premiums from\n2 corn coins/mo" },
        new[] { "DR. FLUSH", "Medical Center", "\"Feeling backed up?\nWe'll get you\nmoving again!\"", "No appointment\nneeded" },
        new[] { "SEWER EATS", "5-Star Dining", "\"Our special:\nMystery Float\"", "Rated 0.5 Stars\non Yelp" },
        new[] { "RAT KING'S", "Fight Club", "\"First rule:\nSqueak about it\"", "Tuesdays 9pm\nPipe Junction 4" },
        new[] { "THE DAILY FLUSH", "Newspaper", "\"All the news\nthat's fit to wipe\"", "Subscribe:\n1 corn coin/week" },
        new[] { "GATOR TOURS", "Premium Guided", "\"See the sewer\nbefore it sees you\"", "Survival rate: 73%" },
        new[] { "CORN COIN", "Crypto Exchange", "\"To the toilet\nand beyond!\"", "100% Organic\nCurrency" },
        new[] { "TURD TAXI", "Express Service", "\"We pick up\nwhat others\nwon't!\"", "Flat rate:\n3 Fartcoins" },
        new[] { "PLOP FITNESS", "Sewer Gym", "\"Get ripped\nbefore you\nget flushed\"", "First dump free!" },
        new[] { "CORNY'S DELI", "Fine Cuisine", "\"Our corn chowder\nis surprisingly\nfamiliar\"", "Open 24/7\n(We never close)" },
        new[] { "SEWER U", "Online Degrees", "\"Learn to float\nwith the best!\"", "Majors in\nPipe Science" },
        new[] { "POOP DECK", "Cruise Lines", "\"Luxury travel\nthrough the finest\npipes in town\"", "All-inclusive\n5 Fartcoins" },
    };
    static int _adIndex = 0;

    static GameObject CreateSewerAdPrefab()
    {
        string path = $"Assets/Prefabs/SewerAd_Gross_{_adIndex}.prefab";
        GameObject root = new GameObject("SewerAd");

        // Pick an ad
        string[] ad = SewerAds[_adIndex % SewerAds.Length];
        _adIndex++;

        // Spray paint ad — all text directly on pipe surface, no board/frame
        // Each line uses a different spray color for that hand-tagged look
        Color titleCol = new Color(0.9f, 0.15f, 0.1f);
        Color subCol = new Color(0.85f, 0.8f, 0.65f);
        Color bodyCol = new Color(0.2f, 0.6f, 0.95f);
        Color footCol = new Color(0.95f, 0.85f, 0.15f);

        // Big overspray haze — flat cube BEHIND the text
        Material hazeMat = MakeURPMat("Gross_AdHaze", new Color(0.5f, 0.5f, 0.5f, 0.05f), 0f, 0.1f);
        if (hazeMat.HasProperty("_Surface"))
        {
            hazeMat.SetFloat("_Surface", 1);
            hazeMat.SetOverrideTag("RenderType", "Transparent");
            hazeMat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(hazeMat);
        // Positive z = toward player inside pipe
        AddPrimChild(root, "Haze", PrimitiveType.Cube, new Vector3(0, -0.02f, 0.003f),
            Quaternion.identity, new Vector3(0.55f, 0.4f, 0.001f), hazeMat);

        float tilt = Random.Range(-5f, 5f);

        // Title — big spray paint (visible inside pipe)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(root.transform, false);
        titleObj.transform.localPosition = new Vector3(0, 0.1f, 0.006f);
        titleObj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
        TextMesh titleTm = titleObj.AddComponent<TextMesh>();
        titleTm.text = ad[0];
        titleTm.fontSize = 56;
        titleTm.characterSize = 0.035f;
        titleTm.anchor = TextAnchor.MiddleCenter;
        titleTm.alignment = TextAlignment.Center;
        titleTm.color = titleCol;
        titleTm.fontStyle = FontStyle.Bold;
        ConfigureSignText(titleTm);

        // Subtitle
        GameObject subObj = new GameObject("SubText");
        subObj.transform.SetParent(root.transform, false);
        subObj.transform.localPosition = new Vector3(0, 0.05f, 0.006f);
        subObj.transform.localRotation = Quaternion.Euler(0, 0, tilt + Random.Range(-3f, 3f));
        TextMesh subTm = subObj.AddComponent<TextMesh>();
        subTm.text = ad[1];
        subTm.fontSize = 38;
        subTm.characterSize = 0.025f;
        subTm.anchor = TextAnchor.MiddleCenter;
        subTm.alignment = TextAlignment.Center;
        subTm.color = subCol;
        ConfigureSignText(subTm);

        // Body — the funny part
        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(root.transform, false);
        bodyObj.transform.localPosition = new Vector3(0, -0.04f, 0.006f);
        bodyObj.transform.localRotation = Quaternion.Euler(0, 0, tilt + Random.Range(-4f, 4f));
        TextMesh bodyTm = bodyObj.AddComponent<TextMesh>();
        bodyTm.text = ad[2];
        bodyTm.fontSize = 34;
        bodyTm.characterSize = 0.025f;
        bodyTm.anchor = TextAnchor.MiddleCenter;
        bodyTm.alignment = TextAlignment.Center;
        bodyTm.color = bodyCol;
        bodyTm.fontStyle = FontStyle.Bold;
        ConfigureSignText(bodyTm);

        // Footer
        GameObject footObj = new GameObject("FooterText");
        footObj.transform.SetParent(root.transform, false);
        footObj.transform.localPosition = new Vector3(0, -0.12f, 0.006f);
        footObj.transform.localRotation = Quaternion.Euler(0, 0, tilt + Random.Range(-2f, 2f));
        TextMesh footTm = footObj.AddComponent<TextMesh>();
        footTm.text = ad[3];
        footTm.fontSize = 28;
        footTm.characterSize = 0.022f;
        footTm.anchor = TextAnchor.MiddleCenter;
        footTm.alignment = TextAlignment.Center;
        footTm.color = footCol;
        ConfigureSignText(footTm);

        // Paint drips from title (most paint = most drips)
        Material dripMat = MakeURPMat("Gross_AdDrip", titleCol * 0.7f, 0f, 0.15f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", titleCol * 0.12f);
        EditorUtility.SetDirty(dripMat);
        for (int d = 0; d < 4; d++)
        {
            float x = Random.Range(-0.15f, 0.15f);
            float len = Random.Range(0.03f, 0.1f);
            float thick = Random.Range(0.004f, 0.007f);
            AddPrimChild(root, $"Drip{d}", PrimitiveType.Capsule,
                new Vector3(x, -0.16f - d * 0.025f, 0.005f),
                Quaternion.identity, new Vector3(thick, len, 0.003f), dripMat);
        }

        // Splatter dots
        Material splatMat = MakeURPMat("Gross_AdSplat", bodyCol * 0.8f, 0f, 0.2f);
        splatMat.EnableKeyword("_EMISSION");
        splatMat.SetColor("_EmissionColor", bodyCol * 0.08f);
        EditorUtility.SetDirty(splatMat);
        for (int s = 0; s < 3; s++)
        {
            Vector3 pos = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.15f, 0.12f), 0.005f);
            float sz = Random.Range(0.008f, 0.018f);
            AddPrimChild(root, $"Splat{s}", PrimitiveType.Sphere, pos,
                Quaternion.identity, new Vector3(sz, sz, 0.002f), splatMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f; // bigger for readability at speed
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ===== ARROW DIRECTION SIGNS =====
    static readonly string[] ArrowTexts = {
        "THIS WAY >>>", "NO EXIT", "DANGER\nAHEAD", "TURN BACK\nNOW",
        ">>> FLUSH >>>", "KEEP LEFT\n(or don't)", "SPEED UP!\n(you'll need it)",
        "EXIT\n(just kidding)", "PIPE 7G >>>", "SHORTCUT\n(it's not)",
        "BROWN TOWN\n>>> 500m", "POINT OF\nNO RETURN", "SEWAGE PLANT\n>>>",
        "YOU ARE\nHERE\n(unfortunately)", "SCENIC\nROUTE\n(it's all gross)"
    };
    static int _arrowIndex = 0;

    static GameObject CreateArrowSignPrefab()
    {
        string path = $"Assets/Prefabs/ArrowSign_Gross_{_arrowIndex}.prefab";
        GameObject root = new GameObject("ArrowSign");

        Color arrowCol = new Color(1f, 0.6f, 0.1f); // orange
        Color bgCol = new Color(0.08f, 0.06f, 0.04f);

        // Dark background rectangle (positive z = toward player inside pipe)
        Material bgMat = MakeURPMat("Gross_ArrowBG", bgCol, 0f, 0.15f);
        AddPrimChild(root, "BG", PrimitiveType.Cube, new Vector3(0, 0, 0.003f),
            Quaternion.identity, new Vector3(0.4f, 0.15f, 0.003f), bgMat);

        // Arrow triangle (pointing right)
        Material arrowMat = MakeURPMat("Gross_Arrow", arrowCol, 0f, 0.3f);
        arrowMat.EnableKeyword("_EMISSION");
        arrowMat.SetColor("_EmissionColor", arrowCol * 0.3f);
        EditorUtility.SetDirty(arrowMat);

        // Big directional arrow using cubes
        float arrowX = 0.12f;
        AddPrimChild(root, "Shaft", PrimitiveType.Cube, new Vector3(arrowX - 0.08f, 0, 0.005f),
            Quaternion.identity, new Vector3(0.12f, 0.03f, 0.003f), arrowMat);
        AddPrimChild(root, "Head1", PrimitiveType.Cube, new Vector3(arrowX, 0.02f, 0.005f),
            Quaternion.Euler(0, 0, -45), new Vector3(0.06f, 0.02f, 0.003f), arrowMat);
        AddPrimChild(root, "Head2", PrimitiveType.Cube, new Vector3(arrowX, -0.02f, 0.005f),
            Quaternion.Euler(0, 0, 45), new Vector3(0.06f, 0.02f, 0.003f), arrowMat);

        // Text
        string txt = ArrowTexts[_arrowIndex % ArrowTexts.Length];
        _arrowIndex++;

        GameObject textObj = new GameObject("SprayText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(-0.04f, 0, 0.006f);
        textObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-3f, 3f));
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = txt;
        tm.fontSize = 38;
        tm.characterSize = 0.025f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = arrowCol;
        tm.fontStyle = FontStyle.Bold;
        ConfigureSignText(tm);

        // Paint drips
        Material dripMat = MakeURPMat("Gross_ArrowDrip", arrowCol * 0.6f, 0f, 0.15f);
        for (int d = 0; d < 2; d++)
        {
            float x = Random.Range(-0.12f, 0.08f);
            AddPrimChild(root, $"Drip{d}", PrimitiveType.Capsule,
                new Vector3(x, -0.08f - d * 0.02f, 0.005f),
                Quaternion.identity, new Vector3(0.004f, Random.Range(0.02f, 0.05f), 0.003f), dripMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ===== WANTED POSTERS =====
    static readonly string[][] WantedPosters = {
        new[] { "WANTED", "MR. CORNY", "Crimes: Being too\nslippery. Armed\nwith corn.", "REWARD:\n500 Fartcoins" },
        new[] { "WANTED", "SEWER RAT KING", "Crimes: Cheese theft,\ntunnel blocking,\ngeneral stinkiness", "REWARD:\n1000 Fartcoins" },
        new[] { "WANTED", "THE CLOGGER", "Crimes: Mass\npipe blockage,\ntoilet terrorism", "APPROACH WITH\nPLUNGER" },
        new[] { "WANTED", "FLUSH GORDON", "Crimes: Excessive\nflushing, water\nwaste, pipe surfing", "LAST SEEN:\nPipe Junction 4" },
        new[] { "WANTED", "EL TURDO", "Crimes: Illegal\nwrestling moves,\nmask violations", "REWARD:\n200 Fartcoins" },
        new[] { "MISSING", "MY DIGNITY", "Last seen: Before\nentering this pipe.\nPlease return.", "NO REWARD\n(obviously)" },
        new[] { "WANTED", "PRINCESS PLOP", "Crimes: Clogging\nthe royal pipes,\ntiara theft", "REWARD:\n750 Fartcoins" },
        new[] { "WANTED", "SKIDMARK STEVE", "Crimes: Leaving marks\neverywhere, excessive\nbraking", "REWARD:\n300 Fartcoins" },
        new[] { "WANTED", "THE LOG", "Crimes: Impersonating\na log, pipe surfing\nwithout a license", "CAUTION:\nVERY DENSE" },
        new[] { "MISSING", "CLEAN WATER", "Last seen: 1987.\nIf found, contact\nBrown Town PD.", "NOT EXPECTED\nTO RETURN" },
    };
    static int _wantedIndex = 0;

    static GameObject CreateWantedPosterPrefab()
    {
        string path = $"Assets/Prefabs/WantedPoster_Gross_{_wantedIndex}.prefab";
        GameObject root = new GameObject("WantedPoster");

        string[] poster = WantedPosters[_wantedIndex % WantedPosters.Length];
        _wantedIndex++;

        // Yellowed paper background (positive z = toward player inside pipe)
        Color paperCol = new Color(0.85f, 0.78f, 0.55f);
        Material paperMat = MakeURPMat("Gross_Paper", paperCol, 0f, 0.2f);
        paperMat.EnableKeyword("_EMISSION");
        paperMat.SetColor("_EmissionColor", paperCol * 0.1f);
        EditorUtility.SetDirty(paperMat);
        AddPrimChild(root, "Paper", PrimitiveType.Cube, new Vector3(0, 0, 0.003f),
            Quaternion.identity, new Vector3(0.22f, 0.3f, 0.002f), paperMat);

        // Torn/worn edges
        Material edgeMat = MakeURPMat("Gross_WornEdge", paperCol * 0.7f, 0f, 0.15f);
        AddPrimChild(root, "Tear1", PrimitiveType.Cube, new Vector3(0.1f, 0.13f, 0.002f),
            Quaternion.Euler(0, 0, 15), new Vector3(0.04f, 0.03f, 0.002f), edgeMat);
        AddPrimChild(root, "Tear2", PrimitiveType.Cube, new Vector3(-0.08f, -0.12f, 0.002f),
            Quaternion.Euler(0, 0, -20), new Vector3(0.05f, 0.025f, 0.002f), edgeMat);

        float tilt = Random.Range(-4f, 4f);

        // "WANTED" / "MISSING" header
        Color headerCol = poster[0] == "MISSING" ? new Color(0.15f, 0.4f, 0.8f) : new Color(0.75f, 0.1f, 0.05f);
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(root.transform, false);
        headerObj.transform.localPosition = new Vector3(0, 0.1f, 0.006f);
        headerObj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
        TextMesh headerTm = headerObj.AddComponent<TextMesh>();
        headerTm.text = poster[0];
        headerTm.fontSize = 52;
        headerTm.characterSize = 0.03f;
        headerTm.anchor = TextAnchor.MiddleCenter;
        headerTm.alignment = TextAlignment.Center;
        headerTm.color = headerCol;
        headerTm.fontStyle = FontStyle.Bold;
        ConfigureSignText(headerTm);

        // Name
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(root.transform, false);
        nameObj.transform.localPosition = new Vector3(0, 0.055f, 0.006f);
        nameObj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
        TextMesh nameTm = nameObj.AddComponent<TextMesh>();
        nameTm.text = poster[1];
        nameTm.fontSize = 40;
        nameTm.characterSize = 0.025f;
        nameTm.anchor = TextAnchor.MiddleCenter;
        nameTm.alignment = TextAlignment.Center;
        nameTm.color = new Color(0.15f, 0.12f, 0.08f);
        nameTm.fontStyle = FontStyle.Bold;
        ConfigureSignText(nameTm);

        // Description
        GameObject descObj = new GameObject("Desc");
        descObj.transform.SetParent(root.transform, false);
        descObj.transform.localPosition = new Vector3(0, -0.03f, 0.006f);
        descObj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
        TextMesh descTm = descObj.AddComponent<TextMesh>();
        descTm.text = poster[2];
        descTm.fontSize = 28;
        descTm.characterSize = 0.02f;
        descTm.anchor = TextAnchor.MiddleCenter;
        descTm.alignment = TextAlignment.Center;
        descTm.color = new Color(0.2f, 0.18f, 0.12f);
        ConfigureSignText(descTm);

        // Footer
        GameObject footObj = new GameObject("Footer");
        footObj.transform.SetParent(root.transform, false);
        footObj.transform.localPosition = new Vector3(0, -0.1f, 0.006f);
        footObj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
        TextMesh footTm = footObj.AddComponent<TextMesh>();
        footTm.text = poster[3];
        footTm.fontSize = 30;
        footTm.characterSize = 0.02f;
        footTm.anchor = TextAnchor.MiddleCenter;
        footTm.alignment = TextAlignment.Center;
        footTm.color = headerCol * 0.8f;
        footTm.fontStyle = FontStyle.Bold;
        ConfigureSignText(footTm);

        // Tape/pin at top (holding poster to wall)
        Material tapeMat = MakeURPMat("Gross_Tape", new Color(0.75f, 0.7f, 0.55f, 0.8f), 0f, 0.3f);
        AddPrimChild(root, "Tape", PrimitiveType.Cube, new Vector3(0, 0.14f, 0.005f),
            Quaternion.Euler(0, 0, 8), new Vector3(0.08f, 0.015f, 0.002f), tapeMat);

        // Coffee stain ring (authentic sewer posting)
        Material stainMat = MakeURPMat("Gross_CoffeeRing", new Color(0.45f, 0.3f, 0.15f, 0.3f), 0f, 0.1f);
        AddPrimChild(root, "Stain", PrimitiveType.Cylinder, new Vector3(0.05f, -0.06f, 0.004f),
            Quaternion.Euler(90, 0, 0), new Vector3(0.04f, 0.001f, 0.04f), stainMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Helper: removes all colliders from object and children.</summary>
    static void RemoveAllColliders(GameObject obj)
    {
        foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject EnsureSceneryPrefab(string name, string modelPath, Vector3 scale, Color fallbackColor)
    {
        string prefabPath = $"Assets/Prefabs/{name}_Scenery.prefab";

        GameObject modelAsset = LoadModel(modelPath);
        GameObject obj;

        if (modelAsset != null)
        {
            obj = (GameObject)Object.Instantiate(modelAsset);
            obj.name = name;
            obj.transform.localScale = scale;
            UpgradeToURP(obj);
            Debug.Log($"TTR: Loaded scenery {name} from Blender model (GLB={_lastModelWasGLB})");
        }
        else
        {
            // Simple fallback shape
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.localScale = scale * 0.5f;

            // Remove collider since scenery is non-interactive
            Collider col = obj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Renderer rend = obj.GetComponent<Renderer>();
            rend.material = MakeURPMat($"{name}_Mat", fallbackColor, 0.15f, 0.25f);
            Debug.LogWarning($"TTR: Scenery {name} not found at {modelPath}, using fallback");
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        Object.DestroyImmediate(obj);
        return prefab;
    }

    // ===== POWER-UP SPAWNER =====
    static void CreatePowerUpSpawner(Transform player)
    {
        GameObject obj = new GameObject("PowerUpSpawner");
        PowerUpSpawner spawner = obj.AddComponent<PowerUpSpawner>();
        spawner.player = player;
        spawner.pipeRadius = 3.5f;

        // Create speed boost prefab - glowing cyan chevron pad
        spawner.speedBoostPrefab = CreateSpeedBoostPrefab();

        // Create jump ramp prefab - angled orange ramp
        spawner.jumpRampPrefab = CreateJumpRampPrefab();

        // Create bonus Fartcoin prefab - special big coin only reachable after ramp jumps
        spawner.bonusCoinPrefab = CreateBonusCoinPrefab();

        // Create special power-up prefabs (Shield, Magnet, Slow-Mo)
        spawner.shieldPrefab = CreateShieldPickupPrefab();
        spawner.magnetPrefab = CreateMagnetPickupPrefab();
        spawner.slowMoPrefab = CreateSlowMoPickupPrefab();
    }

    static GameObject CreateSpeedBoostPrefab()
    {
        string prefabPath = "Assets/Prefabs/SpeedBoost.prefab";
        GameObject root = new GameObject("SpeedBoost");

        // Try loading the ToiletSeat FBX model
        GameObject toiletModel = LoadModel("Assets/Models/ToiletSeat.fbx");
        if (toiletModel == null)
            toiletModel = LoadModel("Assets/Models/ToiletSeat.glb");

        // Porcelain white material with cyan glow emission
        Material porcelainMat = MakeURPMat("SpeedBoost_Porcelain", new Color(0.95f, 0.95f, 0.92f), 0.15f, 0.85f);
        porcelainMat.EnableKeyword("_EMISSION");
        porcelainMat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1f) * 2f);
        EditorUtility.SetDirty(porcelainMat);

        if (toiletModel != null)
        {
            // Use real ToiletSeat model
            GameObject seat = (GameObject)Object.Instantiate(toiletModel);
            seat.name = "ToiletSeatModel";
            seat.transform.SetParent(root.transform);
            seat.transform.localPosition = new Vector3(0, 0.3f, 0); // float above surface
            seat.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            UpgradeToURP(seat);

            // Override all materials with glowing porcelain
            foreach (Renderer r in seat.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int mi = 0; mi < mats.Length; mi++)
                    mats[mi] = porcelainMat;
                r.sharedMaterials = mats;
            }

            // Remove imported colliders
            foreach (Collider c in seat.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            Debug.Log("TTR: Loaded ToiletSeat FBX for speed boost");
        }
        else
        {
            // Fallback: torus-like shape from cylinders to suggest toilet seat
            AddPrimChild(root, "Ring", PrimitiveType.Cylinder,
                new Vector3(0, 0.3f, 0), Quaternion.identity,
                new Vector3(1.8f, 0.1f, 1.8f), porcelainMat);
            Debug.LogWarning("TTR: ToiletSeat model not found, using fallback cylinder");
        }

        // Glowing ring underneath (ground effect)
        Material glowRingMat = MakeURPMat("SpeedBoost_GlowRing", new Color(0.1f, 0.9f, 1f), 0.3f, 0.9f);
        glowRingMat.EnableKeyword("_EMISSION");
        glowRingMat.SetColor("_EmissionColor", new Color(0.2f, 1f, 1.2f) * 4f);
        EditorUtility.SetDirty(glowRingMat);
        AddPrimChild(root, "GlowRing", PrimitiveType.Cylinder,
            new Vector3(0, 0.02f, 0), Quaternion.identity,
            new Vector3(2f, 0.02f, 2f), glowRingMat);

        // Forward-pointing speed arrows (>> chevrons)
        Material boostArrowMat = MakeURPMat("SpeedBoost_Arrow", new Color(0.2f, 1f, 1f), 0.1f, 0.7f);
        boostArrowMat.EnableKeyword("_EMISSION");
        boostArrowMat.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 1f) * 5f);
        EditorUtility.SetDirty(boostArrowMat);
        float[] boostZs = { -0.4f, 0f, 0.4f };
        for (int ba = 0; ba < 3; ba++)
        {
            float bz = boostZs[ba];
            AddPrimChild(root, $"BoostArrowL{ba}", PrimitiveType.Cube,
                new Vector3(-0.15f, 0.05f, bz), Quaternion.Euler(0, -35, 0),
                new Vector3(0.05f, 0.02f, 0.35f), boostArrowMat);
            AddPrimChild(root, $"BoostArrowR{ba}", PrimitiveType.Cube,
                new Vector3(0.15f, 0.05f, bz), Quaternion.Euler(0, 35, 0),
                new Vector3(0.05f, 0.02f, 0.35f), boostArrowMat);
        }

        // Trigger collider
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(2.5f, 1.5f, 2.5f);

        root.AddComponent<SpeedBoost>();
        root.transform.localScale = Vector3.one * 0.6f; // scaled down to not dominate pipe

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Spinning Toilet Seat speed boost prefab");
        return prefab;
    }

    // ===== BIG AIR RAMP PREFAB =====
    static GameObject CreateBigAirRampPrefab()
    {
        string prefabPath = "Assets/Prefabs/BigAirRamp.prefab";
        GameObject root = new GameObject("BigAirRamp");

        // Hot red-orange danger ramp - wider and taller than regular jump ramp
        Material rampMat = MakeURPMat("BigAirRamp_Mat", new Color(0.9f, 0.25f, 0.1f), 0.25f, 0.5f);
        rampMat.EnableKeyword("_EMISSION");
        rampMat.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 0.05f) * 2f);
        EditorUtility.SetDirty(rampMat);

        // Wide ramp surface (steeper than regular)
        AddPrimChild(root, "RampSurface", PrimitiveType.Cube,
            new Vector3(0, 0.35f, 0), Quaternion.Euler(-35, 0, 0),
            new Vector3(2.8f, 0.2f, 3f), rampMat);

        // Side rails - thick and industrial
        Material railMat = MakeURPMat("BigAirRamp_Rail", new Color(0.35f, 0.35f, 0.35f), 0.6f, 0.5f);
        AddPrimChild(root, "LeftRail", PrimitiveType.Cube,
            new Vector3(-1.3f, 0.55f, 0), Quaternion.Euler(-35, 0, 0),
            new Vector3(0.15f, 0.5f, 3f), railMat);
        AddPrimChild(root, "RightRail", PrimitiveType.Cube,
            new Vector3(1.3f, 0.55f, 0), Quaternion.Euler(-35, 0, 0),
            new Vector3(0.15f, 0.5f, 3f), railMat);

        // Danger stripes (red/yellow)
        Material stripeMat = MakeURPMat("BigAirRamp_Stripe", new Color(1f, 0.85f, 0.1f), 0.1f, 0.4f);
        stripeMat.EnableKeyword("_EMISSION");
        stripeMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0f) * 1.5f);
        EditorUtility.SetDirty(stripeMat);

        for (int s = 0; s < 4; s++)
        {
            float z = -0.9f + s * 0.55f;
            AddPrimChild(root, $"Stripe{s}", PrimitiveType.Cube,
                new Vector3(0, 0.46f, z), Quaternion.Euler(-35, 0, 0),
                new Vector3(2.4f, 0.02f, 0.12f), stripeMat);
        }

        // Arrow chevrons (bigger, more dramatic)
        Material arrowMat = MakeURPMat("BigAirRamp_Arrow", new Color(1f, 0.6f, 0.2f), 0.1f, 0.6f);
        arrowMat.EnableKeyword("_EMISSION");
        arrowMat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 3f);
        EditorUtility.SetDirty(arrowMat);

        float[] chevronZs = { -0.8f, -0.3f, 0.2f, 0.7f };
        for (int a = 0; a < 4; a++)
        {
            float az = chevronZs[a];
            float ay = 0.48f + (a * 0.03f);
            AddPrimChild(root, $"ChevronL{a}", PrimitiveType.Cube,
                new Vector3(-0.3f, ay, az), Quaternion.Euler(-35, 0, -35),
                new Vector3(0.08f, 0.02f, 0.55f), arrowMat);
            AddPrimChild(root, $"ChevronR{a}", PrimitiveType.Cube,
                new Vector3(0.3f, ay, az), Quaternion.Euler(-35, 0, 35),
                new Vector3(0.08f, 0.02f, 0.55f), arrowMat);
        }

        // "BIG AIR" sign (cube backdrop)
        Material signMat = MakeURPMat("BigAirRamp_Sign", new Color(0.15f, 0.12f, 0.08f), 0.3f, 0.4f);
        AddPrimChild(root, "SignBoard", PrimitiveType.Cube,
            new Vector3(0, 1.2f, -1.3f), Quaternion.identity,
            new Vector3(2f, 0.5f, 0.1f), signMat);

        // Trigger collider (wider than regular)
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.6f, 0);
        col.size = new Vector3(3.2f, 2f, 3.5f);

        BigAirRamp ramp = root.AddComponent<BigAirRamp>();
        ramp.launchHeight = 6f;
        ramp.arcDuration = 5.5f;

        root.transform.localScale = Vector3.one * 0.9f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Big Air Ramp prefab");
        return prefab;
    }

    // ===== GRATE PREFAB =====
    static GameObject CreateGratePrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerGrate.prefab";
        GameObject root = new GameObject("SewerGrate");

        // Industrial metal grate
        Material grateMat = MakeURPMat("Grate_Metal", new Color(0.35f, 0.33f, 0.3f), 0.65f, 0.35f);
        grateMat.EnableKeyword("_EMISSION");
        grateMat.SetColor("_EmissionColor", new Color(0.15f, 0.12f, 0.1f));
        EditorUtility.SetDirty(grateMat);

        Material rustMat = MakeURPMat("Grate_Rust", new Color(0.5f, 0.25f, 0.1f), 0.3f, 0.25f);

        // Create grid bars (horizontal and vertical)
        float grateWidth = 3.5f;  // half pipe diameter
        float grateHeight = 3.5f;
        int hBars = 6;
        int vBars = 4;

        // Horizontal bars
        for (int i = 0; i < hBars; i++)
        {
            float y = -grateHeight / 2f + (i + 0.5f) * (grateHeight / hBars);
            Material m = (i % 2 == 0) ? grateMat : rustMat;
            AddPrimChild(root, $"HBar{i}", PrimitiveType.Cube,
                new Vector3(0, y, 0), Quaternion.identity,
                new Vector3(grateWidth, 0.12f, 0.08f), m);
        }

        // Vertical bars
        for (int i = 0; i < vBars; i++)
        {
            float x = -grateWidth / 2f + (i + 0.5f) * (grateWidth / vBars);
            Material m = (i % 2 == 0) ? rustMat : grateMat;
            AddPrimChild(root, $"VBar{i}", PrimitiveType.Cube,
                new Vector3(x, 0, 0), Quaternion.identity,
                new Vector3(0.1f, grateHeight, 0.08f), m);
        }

        // Frame border
        Material frameMat = MakeURPMat("Grate_Frame", new Color(0.28f, 0.26f, 0.24f), 0.7f, 0.4f);
        AddPrimChild(root, "FrameTop", PrimitiveType.Cube,
            new Vector3(0, grateHeight / 2f, 0), Quaternion.identity,
            new Vector3(grateWidth + 0.2f, 0.2f, 0.12f), frameMat);
        AddPrimChild(root, "FrameBottom", PrimitiveType.Cube,
            new Vector3(0, -grateHeight / 2f, 0), Quaternion.identity,
            new Vector3(grateWidth + 0.2f, 0.2f, 0.12f), frameMat);
        AddPrimChild(root, "FrameLeft", PrimitiveType.Cube,
            new Vector3(-grateWidth / 2f, 0, 0), Quaternion.identity,
            new Vector3(0.2f, grateHeight + 0.2f, 0.12f), frameMat);
        AddPrimChild(root, "FrameRight", PrimitiveType.Cube,
            new Vector3(grateWidth / 2f, 0, 0), Quaternion.identity,
            new Vector3(0.2f, grateHeight + 0.2f, 0.12f), frameMat);

        // Solid collision
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.size = new Vector3(grateWidth, grateHeight, 0.15f);

        root.AddComponent<GrateBehavior>();
        root.tag = "Obstacle";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Grate prefab");
        return prefab;
    }

    // ===== DROP RING PREFAB =====
    static GameObject CreateDropRingPrefab()
    {
        string prefabPath = "Assets/Prefabs/DropRing.prefab";
        GameObject root = new GameObject("DropRing");

        // Glowing cyan torus-like ring (approximated with torus-shaped cubes)
        Material ringMat = MakeURPMat("DropRing_Mat", new Color(0.2f, 0.7f, 0.9f, 0.8f), 0.2f, 0.7f);
        ringMat.EnableKeyword("_EMISSION");
        ringMat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1f) * 0.5f);
        // Transparent
        ringMat.SetFloat("_Surface", 1);
        ringMat.SetFloat("_Blend", 0);
        ringMat.SetOverrideTag("RenderType", "Transparent");
        ringMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ringMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ringMat.SetInt("_ZWrite", 0);
        ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ringMat.renderQueue = 3000;
        EditorUtility.SetDirty(ringMat);

        // Build ring from 8 capsules arranged in a circle
        float ringRadius = 0.5f;
        float segRadius = 0.08f;
        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float nextAngle = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            float midAngle = (angle + nextAngle) / 2f;

            Vector3 pos = new Vector3(Mathf.Cos(midAngle) * ringRadius, Mathf.Sin(midAngle) * ringRadius, 0);
            float tangent = midAngle + Mathf.PI / 2f;

            AddPrimChild(root, $"Seg{i}", PrimitiveType.Capsule,
                pos, Quaternion.Euler(0, 0, midAngle * Mathf.Rad2Deg + 90),
                new Vector3(segRadius * 2f, ringRadius * Mathf.PI / segments, segRadius * 2f), ringMat);
        }

        // Center glow sphere
        Material glowMat = MakeURPMat("DropRing_Glow", new Color(0.3f, 0.9f, 1f, 0.3f), 0f, 0.9f);
        glowMat.EnableKeyword("_EMISSION");
        glowMat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.6f) * 0.3f);
        glowMat.SetFloat("_Surface", 1);
        glowMat.SetFloat("_Blend", 0);
        glowMat.SetOverrideTag("RenderType", "Transparent");
        glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glowMat.SetInt("_ZWrite", 0);
        glowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glowMat.renderQueue = 3000;
        EditorUtility.SetDirty(glowMat);

        AddPrimChild(root, "CenterGlow", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity,
            Vector3.one * 0.3f, glowMat);

        // Trigger collider
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.6f;

        root.AddComponent<DropRing>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Drop Ring prefab");
        return prefab;
    }

    // ===== DROP ZONE PREFAB =====
    static GameObject CreateDropZonePrefab(GameObject ringPrefab)
    {
        string prefabPath = "Assets/Prefabs/DropZone.prefab";
        GameObject root = new GameObject("UnderwaterZone");

        // Visual: rising water wall - murky blue-green
        Material waterWallMat = MakeURPMat("Underwater_Wall", new Color(0.08f, 0.25f, 0.18f, 0.7f), 0.3f, 0.85f);
        waterWallMat.EnableKeyword("_EMISSION");
        waterWallMat.SetColor("_EmissionColor", new Color(0.05f, 0.2f, 0.15f) * 1.2f);
        waterWallMat.SetFloat("_Surface", 1);
        waterWallMat.SetFloat("_Blend", 0);
        waterWallMat.SetOverrideTag("RenderType", "Transparent");
        waterWallMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        waterWallMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        waterWallMat.SetInt("_ZWrite", 0);
        waterWallMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        waterWallMat.renderQueue = 3000;
        EditorUtility.SetDirty(waterWallMat);

        // Rising water disc (fills the pipe cross-section)
        AddPrimChild(root, "WaterWall", PrimitiveType.Cylinder,
            new Vector3(0, 0, 1f), Quaternion.Euler(90f, 0, 0),
            new Vector3(3.5f, 0.3f, 3.5f), waterWallMat);

        // Bubble clusters on the water surface
        Material bubbleMat = MakeURPMat("Underwater_Bubble", new Color(0.4f, 0.7f, 0.6f, 0.4f), 0f, 0.95f);
        bubbleMat.EnableKeyword("_EMISSION");
        bubbleMat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 0.5f) * 0.6f);
        bubbleMat.SetFloat("_Surface", 1);
        bubbleMat.SetFloat("_Blend", 0);
        bubbleMat.SetOverrideTag("RenderType", "Transparent");
        bubbleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bubbleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        bubbleMat.SetInt("_ZWrite", 0);
        bubbleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bubbleMat.renderQueue = 3000;
        EditorUtility.SetDirty(bubbleMat);

        for (int i = 0; i < 8; i++)
        {
            float angle = (i / 8f) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float r = Random.Range(0.5f, 1.5f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0.5f);
            float bs = Random.Range(0.15f, 0.35f);
            AddPrimChild(root, $"Bubble{i}", PrimitiveType.Sphere,
                pos, Quaternion.identity,
                Vector3.one * bs, bubbleMat);
        }

        // "DIVE!" warning text sign (faces player)
        GameObject signObj = new GameObject("DiveSign");
        signObj.transform.SetParent(root.transform, false);
        signObj.transform.localPosition = new Vector3(0, 1.5f, -2f);
        TextMesh tm = signObj.AddComponent<TextMesh>();
        tm.text = "DIVE!";
        tm.fontSize = 48;
        tm.characterSize = 0.15f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.2f, 0.8f, 0.9f);
        tm.fontStyle = FontStyle.Bold;
        ConfigureSignText(tm);

        // Trigger collider - must span entire pipe cross-section so player hits it
        // Player rides the wall at ~3m from center, so radius must cover that
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.center = Vector3.zero;
        col.radius = 5f; // covers entire pipe diameter (player at 3m, pipe at 3.5m)

        VerticalDrop drop = root.AddComponent<VerticalDrop>();
        drop.ringPrefab = ringPrefab;
        drop.swimDuration = 14f;
        drop.swimSpeed = 8f;
        drop.moveRadius = 2.8f;
        drop.moveSpeed = 12f;
        drop.plungeSpeedBoost = 2.0f;
        drop.plungeBoostDuration = 4f;
        drop.obstacleCount = 25;
        drop.ringCount = 20;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Underwater Zone prefab (swim + plunge flush)");
        return prefab;
    }

    static GameObject CreateJumpRampPrefab()
    {
        string prefabPath = "Assets/Prefabs/JumpRamp.prefab";
        GameObject root = new GameObject("JumpRamp");

        // Orange angled ramp - bright and visible from distance
        Material rampMat = MakeURPMat("JumpRamp_Mat", new Color(0.95f, 0.55f, 0.12f), 0.2f, 0.5f);
        rampMat.EnableKeyword("_EMISSION");
        rampMat.SetColor("_EmissionColor", new Color(0.85f, 0.45f, 0.06f) * 2.5f);
        EditorUtility.SetDirty(rampMat);

        // Ramp surface (angled cube)
        AddPrimChild(root, "RampSurface", PrimitiveType.Cube,
            new Vector3(0, 0.25f, 0), Quaternion.Euler(-25, 0, 0),
            new Vector3(1.8f, 0.15f, 2f), rampMat);

        // Side rails
        Material railMat = MakeURPMat("JumpRamp_Rail", new Color(0.7f, 0.35f, 0.05f), 0.4f, 0.6f);
        AddPrimChild(root, "LeftRail", PrimitiveType.Cube,
            new Vector3(-0.85f, 0.4f, 0), Quaternion.Euler(-25, 0, 0),
            new Vector3(0.1f, 0.4f, 2f), railMat);
        AddPrimChild(root, "RightRail", PrimitiveType.Cube,
            new Vector3(0.85f, 0.4f, 0), Quaternion.Euler(-25, 0, 0),
            new Vector3(0.1f, 0.4f, 2f), railMat);

        // Warning stripes
        Material stripeMat = MakeURPMat("JumpRamp_Stripe", new Color(1f, 0.9f, 0.2f), 0.1f, 0.4f);
        stripeMat.EnableKeyword("_EMISSION");
        stripeMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0f));
        EditorUtility.SetDirty(stripeMat);

        AddPrimChild(root, "Stripe1", PrimitiveType.Cube,
            new Vector3(0, 0.35f, -0.6f), Quaternion.Euler(-25, 0, 0),
            new Vector3(1.6f, 0.02f, 0.15f), stripeMat);
        AddPrimChild(root, "Stripe2", PrimitiveType.Cube,
            new Vector3(0, 0.35f, -0.2f), Quaternion.Euler(-25, 0, 0),
            new Vector3(1.6f, 0.02f, 0.15f), stripeMat);

        // Light-up arrow chevrons pointing up the ramp
        Material arrowMat = MakeURPMat("JumpRamp_Arrow", new Color(1f, 1f, 0.4f), 0.1f, 0.6f);
        arrowMat.EnableKeyword("_EMISSION");
        arrowMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 4f);
        EditorUtility.SetDirty(arrowMat);

        // Build 3 arrow chevrons (^ shape) from thin cubes
        float[] arrowZs = { -0.65f, -0.25f, 0.15f };
        for (int a = 0; a < 3; a++)
        {
            float az = arrowZs[a];
            float ay = 0.36f + (a * 0.02f); // slightly higher on incline
            // Left arm of chevron
            AddPrimChild(root, $"ArrowL{a}", PrimitiveType.Cube,
                new Vector3(-0.2f, ay, az), Quaternion.Euler(-25, 0, -35),
                new Vector3(0.06f, 0.02f, 0.45f), arrowMat);
            // Right arm of chevron
            AddPrimChild(root, $"ArrowR{a}", PrimitiveType.Cube,
                new Vector3(0.2f, ay, az), Quaternion.Euler(-25, 0, 35),
                new Vector3(0.06f, 0.02f, 0.45f), arrowMat);
        }

        // Trigger collider
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(2.2f, 1.5f, 2.5f);

        root.AddComponent<JumpRamp>();
        root.transform.localScale = Vector3.one * 0.9f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Jump Ramp prefab");
        return prefab;
    }

    /// <summary>
    /// Creates the Fartcoin collectible - a copper penny with raised $ emboss,
    /// ridged edge, and patina. Smaller and more detailed than the old gold disc.
    /// </summary>
    static GameObject CreateFartcoinPrefab()
    {
        string prefabPath = "Assets/Prefabs/CornCoin.prefab";
        GameObject root = new GameObject("Fartcoin");

        // === MATERIALS (opaque bright gold coin) ===
        // Gold coin body - fully opaque, bright and visible
        Material copperMat = MakeURPMat("Fartcoin_Copper", new Color(0.95f, 0.75f, 0.15f), 0.7f, 0.8f);
        copperMat.EnableKeyword("_EMISSION");
        copperMat.SetColor("_EmissionColor", new Color(0.9f, 0.65f, 0.1f) * 0.5f);
        EditorUtility.SetDirty(copperMat);

        // Darker rim ring
        Material patinaMat = MakeURPMat("Fartcoin_Patina", new Color(0.75f, 0.55f, 0.1f), 0.6f, 0.7f);
        patinaMat.EnableKeyword("_EMISSION");
        patinaMat.SetColor("_EmissionColor", new Color(0.7f, 0.5f, 0.08f) * 0.3f);
        EditorUtility.SetDirty(patinaMat);

        // Bright gold for the $ symbol
        Material symbolMat = MakeURPMat("Fartcoin_Symbol", new Color(1f, 0.9f, 0.2f), 0.85f, 0.9f);
        symbolMat.EnableKeyword("_EMISSION");
        symbolMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.15f) * 0.7f);
        EditorUtility.SetDirty(symbolMat);

        // Edge ridges material
        Material ridgeMat = MakeURPMat("Fartcoin_Ridge", new Color(0.8f, 0.6f, 0.12f), 0.75f, 0.7f);
        ridgeMat.EnableKeyword("_EMISSION");
        ridgeMat.SetColor("_EmissionColor", new Color(0.7f, 0.5f, 0.1f) * 0.3f);
        EditorUtility.SetDirty(ridgeMat);

        // Halo glow - soft transparent beckoning ring
        Material haloMat = MakeURPMat("CoinHalo_Glow", new Color(1f, 0.85f, 0.2f, 0.3f), 0f, 0.1f);
        haloMat.EnableKeyword("_EMISSION");
        haloMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.15f) * 0.8f);
        haloMat.SetFloat("_Surface", 1f);
        haloMat.SetFloat("_Blend", 0f);
        haloMat.SetFloat("_SrcBlend", 5f);
        haloMat.SetFloat("_DstBlend", 10f);
        haloMat.SetFloat("_ZWrite", 0f);
        haloMat.SetOverrideTag("RenderType", "Transparent");
        haloMat.renderQueue = 3000;
        haloMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(haloMat);

        // === COIN BODY (visible, collectible-sized gold coin) ===
        float coinDiam = 0.3f;
        float coinThick = 0.08f;
        AddPrimChild(root, "CoinBody", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(coinDiam, coinThick, coinDiam), copperMat);

        // === RAISED RIM (slightly larger ring) ===
        AddPrimChild(root, "CoinRim", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(coinDiam * 1.05f, coinThick * 1.15f, coinDiam * 1.05f), patinaMat);

        // === RIDGED EDGE (small cubes around circumference for reeded edge) ===
        int ridgeCount = 16; // fewer ridges on smaller coin
        float ridgeW = coinDiam * 0.04f;
        float ridgeD = coinDiam * 0.08f;
        for (int i = 0; i < ridgeCount; i++)
        {
            float a = (i / (float)ridgeCount) * Mathf.PI * 2f;
            float rx = Mathf.Cos(a) * coinDiam * 0.52f;
            float rz = Mathf.Sin(a) * coinDiam * 0.52f;
            AddPrimChild(root, $"Ridge{i}", PrimitiveType.Cube,
                new Vector3(rx, 0, rz),
                Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0),
                new Vector3(ridgeW, coinThick * 1.1f, ridgeD), ridgeMat);
        }

        // === $ SYMBOL on both faces (bold, visible) ===
        float symbolHeight = coinDiam * 0.6f;
        float cubeSize = 0.04f;
        float barWidth = 0.025f;

        Vector3[] sPath = {
            new Vector3( 0.04f, 0, -symbolHeight * 0.45f),
            new Vector3( 0.06f, 0, -symbolHeight * 0.3f),
            new Vector3( 0.05f, 0, -symbolHeight * 0.15f),
            new Vector3( 0.02f, 0,  0f),
            new Vector3(-0.01f, 0,  0f),
            new Vector3(-0.05f, 0,  symbolHeight * 0.15f),
            new Vector3(-0.06f, 0,  symbolHeight * 0.3f),
            new Vector3(-0.04f, 0,  symbolHeight * 0.45f),
        };

        float embossY = coinThick * 0.55f;

        for (int face = 0; face < 2; face++)
        {
            float ySign = (face == 0) ? 1f : -1f;
            string facePrefix = face == 0 ? "Front" : "Back";

            for (int i = 0; i < sPath.Length; i++)
            {
                Vector3 p = sPath[i];
                AddPrimChild(root, $"{facePrefix}S{i}", PrimitiveType.Cube,
                    new Vector3(p.x, embossY * ySign, p.z),
                    Quaternion.identity,
                    new Vector3(cubeSize, cubeSize * 0.6f, cubeSize), symbolMat);
            }

            AddPrimChild(root, $"{facePrefix}Bar", PrimitiveType.Cube,
                new Vector3(0, embossY * ySign, 0),
                Quaternion.identity,
                new Vector3(barWidth, cubeSize * 0.5f, symbolHeight * 1.1f), symbolMat);
        }

        // === GLOW HALO ===
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "GlowHalo";
        halo.transform.SetParent(root.transform, false);
        halo.transform.localPosition = Vector3.down * 0.01f;
        halo.transform.localScale = new Vector3(coinDiam * 1.4f, 0.005f, coinDiam * 1.4f);
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.GetComponent<Renderer>().sharedMaterial = haloMat;

        // === COLLIDER ===
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.0f; // very generous trigger radius - easy to collect while sliding past

        root.AddComponent<Collectible>();
        root.tag = "Collectible";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Fartcoin penny with $ emboss and ridged edge");
        return prefab;
    }

    /// <summary>Creates the bonus Fartcoin - bigger, flashier gold penny worth 10x. Only after ramps.</summary>
    static GameObject CreateBonusCoinPrefab()
    {
        string prefabPath = "Assets/Prefabs/BonusCoin.prefab";
        GameObject root = new GameObject("BonusCoin");

        // Gold material - opaque bright gold with strong glow
        Material goldMat = MakeURPMat("BonusCoin_Gold", new Color(1f, 0.85f, 0.1f), 0.9f, 0.9f);
        goldMat.EnableKeyword("_EMISSION");
        goldMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 0.8f);
        EditorUtility.SetDirty(goldMat);

        Material symbolMat = MakeURPMat("BonusCoin_Symbol", new Color(1f, 0.95f, 0.3f), 0.95f, 0.95f);
        symbolMat.EnableKeyword("_EMISSION");
        symbolMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 0.9f);
        EditorUtility.SetDirty(symbolMat);

        // Bonus coin body (bigger than regular, flashy)
        float coinDiam = 0.45f;
        float coinThick = 0.1f;
        AddPrimChild(root, "CoinBody", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(coinDiam, coinThick, coinDiam), goldMat);
        AddPrimChild(root, "CoinRim", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(coinDiam * 1.05f, coinThick * 1.2f, coinDiam * 1.05f), goldMat);

        // Ridged edge
        int ridgeCount = 30;
        for (int i = 0; i < ridgeCount; i++)
        {
            float a = (i / (float)ridgeCount) * Mathf.PI * 2f;
            float rx = Mathf.Cos(a) * coinDiam * 0.52f;
            float rz = Mathf.Sin(a) * coinDiam * 0.52f;
            AddPrimChild(root, $"Ridge{i}", PrimitiveType.Cube,
                new Vector3(rx, 0, rz),
                Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0),
                new Vector3(0.04f, coinThick * 1.15f, 0.07f), goldMat);
        }

        // $ symbol on both faces (same as regular but bigger)
        float symbolHeight = coinDiam * 0.3f;
        float cubeSize = 0.08f;
        float embossY = coinThick * 0.6f;
        Vector3[] sPath = {
            new Vector3( 0.1f, 0, -symbolHeight * 0.45f),
            new Vector3( 0.15f, 0, -symbolHeight * 0.3f),
            new Vector3( 0.12f, 0, -symbolHeight * 0.15f),
            new Vector3( 0.05f, 0,  0f),
            new Vector3(-0.03f, 0,  0f),
            new Vector3(-0.12f, 0,  symbolHeight * 0.15f),
            new Vector3(-0.15f, 0,  symbolHeight * 0.3f),
            new Vector3(-0.1f, 0,  symbolHeight * 0.45f),
        };
        for (int face = 0; face < 2; face++)
        {
            float ySign = (face == 0) ? 1f : -1f;
            string fp = face == 0 ? "F" : "B";
            for (int i = 0; i < sPath.Length; i++)
            {
                Vector3 p = sPath[i];
                AddPrimChild(root, $"{fp}S{i}", PrimitiveType.Cube,
                    new Vector3(p.x, embossY * ySign, p.z), Quaternion.identity,
                    new Vector3(cubeSize, cubeSize * 0.5f, cubeSize), symbolMat);
            }
            AddPrimChild(root, $"{fp}Bar", PrimitiveType.Cube,
                new Vector3(0, embossY * ySign, 0), Quaternion.identity,
                new Vector3(0.05f, cubeSize * 0.5f, symbolHeight * 1.1f), symbolMat);
        }

        // Sparkle halo (soft glow ring)
        Material sparkleMat = MakeURPMat("BonusCoin_Sparkle", new Color(1f, 1f, 0.6f, 0.2f), 0f, 0.1f);
        sparkleMat.EnableKeyword("_EMISSION");
        sparkleMat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.4f) * 0.6f);
        sparkleMat.SetFloat("_Surface", 1f);
        sparkleMat.SetFloat("_Blend", 0f);
        sparkleMat.SetFloat("_SrcBlend", 5f);
        sparkleMat.SetFloat("_DstBlend", 10f);
        sparkleMat.SetFloat("_ZWrite", 0f);
        sparkleMat.SetOverrideTag("RenderType", "Transparent");
        sparkleMat.renderQueue = 3000;
        sparkleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(sparkleMat);

        GameObject sparkle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sparkle.name = "SparkleRing";
        sparkle.transform.SetParent(root.transform, false);
        sparkle.transform.localScale = new Vector3(coinDiam * 1.5f, 0.005f, coinDiam * 1.5f);
        Object.DestroyImmediate(sparkle.GetComponent<Collider>());
        sparkle.GetComponent<Renderer>().sharedMaterial = sparkleMat;

        // Collider and component
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.2f, 1.2f, 1.2f);
        root.AddComponent<BonusCoin>();
        root.tag = "Collectible";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Bonus Fartcoin (gold penny, 10x value, post-ramp reward)");
        return prefab;
    }

    // ===== SPECIAL POWER-UP PREFABS =====

    static GameObject CreateShieldPickupPrefab()
    {
        string prefabPath = "Assets/Prefabs/ShieldPickup.prefab";
        GameObject root = new GameObject("ShieldPickup");

        // Cyan translucent shield orb
        Material shieldMat = MakeURPMat("Shield_Orb", new Color(0.2f, 0.85f, 1f, 0.6f), 0.8f, 0.95f);
        shieldMat.EnableKeyword("_EMISSION");
        shieldMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 1f) * 3f);
        shieldMat.SetFloat("_Surface", 1f);
        shieldMat.SetFloat("_Blend", 0f);
        shieldMat.SetFloat("_SrcBlend", 5f);
        shieldMat.SetFloat("_DstBlend", 10f);
        shieldMat.SetFloat("_ZWrite", 0f);
        shieldMat.SetOverrideTag("RenderType", "Transparent");
        shieldMat.renderQueue = 3000;
        shieldMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        SaveMaterial(shieldMat);

        Material coreMat = MakeURPMat("Shield_Core", new Color(0.4f, 0.9f, 1f), 0.9f, 0.98f);
        coreMat.EnableKeyword("_EMISSION");
        coreMat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 4f);
        SaveMaterial(coreMat);

        // Outer sphere (translucent shield)
        AddPrimChild(root, "ShieldOrb", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity, Vector3.one * 0.5f, shieldMat);

        // Inner core (bright solid)
        AddPrimChild(root, "Core", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity, Vector3.one * 0.2f, coreMat);

        // Orbiting ring
        AddPrimChild(root, "Ring", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.Euler(90f, 0, 0),
            new Vector3(0.55f, 0.02f, 0.55f), coreMat);

        // Cross bars (shield emblem)
        AddPrimChild(root, "CrossH", PrimitiveType.Cube,
            Vector3.zero, Quaternion.identity,
            new Vector3(0.35f, 0.04f, 0.04f), coreMat);
        AddPrimChild(root, "CrossV", PrimitiveType.Cube,
            Vector3.zero, Quaternion.identity,
            new Vector3(0.04f, 0.35f, 0.04f), coreMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.8f;
        root.AddComponent<ShieldPickup>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Shield Pickup (cyan orb, 5s invincibility)");
        return prefab;
    }

    static GameObject CreateMagnetPickupPrefab()
    {
        string prefabPath = "Assets/Prefabs/MagnetPickup.prefab";
        GameObject root = new GameObject("MagnetPickup");

        Material magnetMat = MakeURPMat("Magnet_Body", new Color(1f, 0.15f, 0.1f), 0.7f, 0.6f);
        magnetMat.EnableKeyword("_EMISSION");
        magnetMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.1f) * 1.5f);
        SaveMaterial(magnetMat);

        Material tipMat = MakeURPMat("Magnet_Tip", new Color(0.8f, 0.8f, 0.85f), 0.9f, 0.8f);
        tipMat.EnableKeyword("_EMISSION");
        tipMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 2f);
        SaveMaterial(tipMat);

        Material goldMat = MakeURPMat("Magnet_Gold", new Color(1f, 0.85f, 0.1f), 0.8f, 0.85f);
        goldMat.EnableKeyword("_EMISSION");
        goldMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 3f);
        SaveMaterial(goldMat);

        // Horseshoe magnet shape from primitives
        // Left arm
        AddPrimChild(root, "LeftArm", PrimitiveType.Cube,
            new Vector3(-0.15f, 0, 0), Quaternion.identity,
            new Vector3(0.08f, 0.4f, 0.08f), magnetMat);
        // Right arm
        AddPrimChild(root, "RightArm", PrimitiveType.Cube,
            new Vector3(0.15f, 0, 0), Quaternion.identity,
            new Vector3(0.08f, 0.4f, 0.08f), magnetMat);
        // Top bridge
        AddPrimChild(root, "Bridge", PrimitiveType.Cube,
            new Vector3(0, 0.2f, 0), Quaternion.identity,
            new Vector3(0.38f, 0.08f, 0.08f), magnetMat);
        // Tips (silver/white)
        AddPrimChild(root, "TipL", PrimitiveType.Cube,
            new Vector3(-0.15f, -0.22f, 0), Quaternion.identity,
            new Vector3(0.1f, 0.06f, 0.1f), tipMat);
        AddPrimChild(root, "TipR", PrimitiveType.Cube,
            new Vector3(0.15f, -0.22f, 0), Quaternion.identity,
            new Vector3(0.1f, 0.06f, 0.1f), tipMat);
        // Golden aura sphere
        AddPrimChild(root, "Aura", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity, Vector3.one * 0.45f, goldMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.8f;
        root.AddComponent<MagnetPickup>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Magnet Pickup (horseshoe, 8s coin attraction)");
        return prefab;
    }

    static GameObject CreateSlowMoPickupPrefab()
    {
        string prefabPath = "Assets/Prefabs/SlowMoPickup.prefab";
        GameObject root = new GameObject("SlowMoPickup");

        Material clockMat = MakeURPMat("SlowMo_Clock", new Color(0.6f, 0.2f, 1f, 0.7f), 0.6f, 0.85f);
        clockMat.EnableKeyword("_EMISSION");
        clockMat.SetColor("_EmissionColor", new Color(0.5f, 0.15f, 1f) * 3f);
        clockMat.SetFloat("_Surface", 1f);
        clockMat.SetFloat("_Blend", 0f);
        clockMat.SetFloat("_SrcBlend", 5f);
        clockMat.SetFloat("_DstBlend", 10f);
        clockMat.SetFloat("_ZWrite", 0f);
        clockMat.SetOverrideTag("RenderType", "Transparent");
        clockMat.renderQueue = 3000;
        clockMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        SaveMaterial(clockMat);

        Material handMat = MakeURPMat("SlowMo_Hand", new Color(1f, 1f, 1f), 0.9f, 0.95f);
        handMat.EnableKeyword("_EMISSION");
        handMat.SetColor("_EmissionColor", new Color(0.8f, 0.5f, 1f) * 4f);
        SaveMaterial(handMat);

        // Clock face (flat cylinder)
        AddPrimChild(root, "ClockFace", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(0.4f, 0.03f, 0.4f), clockMat);

        // Clock frame ring
        AddPrimChild(root, "Frame", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(0.45f, 0.04f, 0.45f), handMat);

        // Hour hand
        AddPrimChild(root, "HourHand", PrimitiveType.Cube,
            new Vector3(0, 0.04f, 0.06f), Quaternion.identity,
            new Vector3(0.025f, 0.02f, 0.12f), handMat);

        // Minute hand (longer)
        AddPrimChild(root, "MinuteHand", PrimitiveType.Cube,
            new Vector3(0.05f, 0.04f, 0), Quaternion.identity,
            new Vector3(0.16f, 0.02f, 0.02f), handMat);

        // Center dot
        AddPrimChild(root, "CenterDot", PrimitiveType.Sphere,
            new Vector3(0, 0.04f, 0), Quaternion.identity,
            Vector3.one * 0.04f, handMat);

        // Hour markers (12 small dots)
        for (int i = 0; i < 12; i++)
        {
            float a = (i / 12f) * Mathf.PI * 2f;
            float mx = Mathf.Sin(a) * 0.16f;
            float mz = Mathf.Cos(a) * 0.16f;
            AddPrimChild(root, $"Mark{i}", PrimitiveType.Sphere,
                new Vector3(mx, 0.04f, mz), Quaternion.identity,
                Vector3.one * 0.02f, handMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.8f;
        root.AddComponent<SlowMoPickup>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Slow-Mo Pickup (purple clock, 3s time slow)");
        return prefab;
    }

    // ===== SMOOTH SNAKE AI RACER =====
    static void CreateSmoothSnake(PipeGenerator pipeGen)
    {
        // Empty root for AI mechanics (same pattern as player - prevents rotation bugs)
        GameObject snake = new GameObject("SmoothSnake");
        snake.transform.position = new Vector3(1f, -3f, -5f);

        // Load visual model as child (uses LoadModel for GLB preference)
        GameObject modelAsset = LoadModel("Assets/Models/MrCorny_Rigged.fbx");

        if (modelAsset != null)
        {
            GameObject model = (GameObject)Object.Instantiate(modelAsset);
            model.name = "Model";
            model.transform.SetParent(snake.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 0.16f;

            // Remove imported colliders
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // Re-skin ALL renderers to a darker, glossier brown
            Material darkPoop = MakeURPMat("SmoothSnake_Body", new Color(0.25f, 0.15f, 0.08f), 0.15f, 0.7f);
            darkPoop.EnableKeyword("_EMISSION");
            darkPoop.SetColor("_EmissionColor", new Color(0.1f, 0.05f, 0.02f));
            EditorUtility.SetDirty(darkPoop);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = darkPoop;
                r.sharedMaterials = mats;
            }

            // Add smug half-lidded eyes
            AddSmugFace(model);
        }
        else
        {
            // Fallback: primitive-based darker poop
            Material smoothMat = MakeURPMat("SmoothSnake_Body", new Color(0.25f, 0.15f, 0.08f), 0.15f, 0.7f);
            AddPrimChild(snake, "Body", PrimitiveType.Capsule, Vector3.zero,
                Quaternion.Euler(90, 0, 0), new Vector3(0.6f, 1.2f, 0.5f), smoothMat);
            AddPrimChild(snake, "Front", PrimitiveType.Sphere,
                new Vector3(0, 0.05f, 0.9f), Quaternion.identity,
                new Vector3(0.55f, 0.48f, 0.5f), smoothMat);
        }

        // Collider on root
        CapsuleCollider col = snake.AddComponent<CapsuleCollider>();
        col.isTrigger = false;
        col.radius = 0.3f;
        col.height = 1.5f;
        col.direction = 2;

        // Slither animation
        snake.AddComponent<TurdSlither>();

        // AI Controller
        SmoothSnakeAI ai = snake.AddComponent<SmoothSnakeAI>();
        ai.pipeGen = pipeGen;
        ai.pipeRadius = 3f;
        ai.baseSpeed = 6.5f;
        ai.maxSpeed = 13f;

        // AI glow via emissive material on body (no Light component = no gizmo icons)
        // The snake's model material already has color; boost emissive for visibility
        foreach (Renderer r in snake.GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial != null)
            {
                r.sharedMaterial.EnableKeyword("_EMISSION");
                r.sharedMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.4f, 0.15f) * 0.4f);
                EditorUtility.SetDirty(r.sharedMaterial);
            }
        }

        // Save gallery prefab for Smooth Snake
        SaveRacerGalleryPrefab(snake, "SmoothSnake");

        Debug.Log("TTR: Created Smooth Snake AI racer (dark MrCorny model, root/child)!");
    }

    static void AddSmugFace(GameObject snake)
    {
        // Use shared transform-based face positioning (handles FBX import rotation correctly)
        FaceBounds fb = ComputeFaceBounds(snake);
        Vector3 fwd = fb.fwd, upV = fb.upV, sideV = fb.sideV;
        float fwdExt = fb.fwdExt, upExt = fb.upExt, sideExt = fb.sideExt;

        // Warm yellowish tint to differentiate from player's bright white eyes
        Material whiteMat = MakeURPMat("SmoothSnake_EyeWhite", new Color(1f, 0.95f, 0.82f), 0f, 0.9f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.45f, 0.4f, 0.3f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("SmoothSnake_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material lidMat = MakeURPMat("SmoothSnake_Lid", new Color(0.15f, 0.08f, 0.03f), 0f, 0.6f);

        Vector3 frontPos = fb.frontPos;
        float eyeGap = Mathf.Max(sideExt * 0.24f, 0.08f);
        // Smaller eyes than player (0.3x instead of 0.4x)
        float eyeSize = Mathf.Max(sideExt, upExt) * 0.3f;
        eyeSize = Mathf.Clamp(eyeSize, 0.12f, 0.8f);
        Vector3 eyeBase = frontPos + upV * (upExt * 0.3f);

        // Smug narrowed eyes (smaller than player)
        GameObject leftEye = AddPrimChild(snake, "LeftEye", PrimitiveType.Sphere,
            eyeBase - sideV * eyeGap, Quaternion.identity,
            new Vector3(eyeSize, eyeSize * 0.6f, eyeSize * 0.7f), whiteMat);
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.5f, pupilMat);
        // Heavy eyelid
        AddPrimChild(snake, "LeftLid", PrimitiveType.Sphere,
            eyeBase - sideV * eyeGap + upV * (eyeSize * 0.22f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.15f, eyeSize * 0.4f, eyeSize * 0.85f), lidMat);

        GameObject rightEye = AddPrimChild(snake, "RightEye", PrimitiveType.Sphere,
            eyeBase + sideV * eyeGap, Quaternion.identity,
            new Vector3(eyeSize, eyeSize * 0.6f, eyeSize * 0.7f), whiteMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.5f, pupilMat);
        AddPrimChild(snake, "RightLid", PrimitiveType.Sphere,
            eyeBase + sideV * eyeGap + upV * (eyeSize * 0.22f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.15f, eyeSize * 0.4f, eyeSize * 0.85f), lidMat);

        // Smirk mouth
        Material mouthMat = MakeURPMat("SmoothSnake_Mouth", new Color(0.5f, 0.1f, 0.1f), 0f, 0.5f);
        Vector3 mouthPos = frontPos - upV * (upExt * 0.1f) + sideV * (eyeGap * 0.2f);
        AddPrimChild(snake, "Mouth", PrimitiveType.Capsule,
            mouthPos, Quaternion.Euler(0, 0, 15),
            new Vector3(eyeSize * 0.15f, eyeSize * 0.5f, eyeSize * 0.15f), mouthMat);
    }

    // ===== RACE SYSTEM (Brown Town Grand Prix) =====
    static void CreateRaceSystem(TurdController playerController, PipeGenerator pipeGen, GameUI gameUI)
    {
        // ---- 4 AI Racer GameObjects ----
        string[] presets = { "SkidmarkSteve", "PrincessPlop", "TheLog", "LilSquirt" };
        Color[] racerColors = {
            new Color(0.7f, 0.35f, 0.1f),   // Steve - burnt orange
            new Color(0.85f, 0.5f, 0.75f),  // Plop - pink
            new Color(0.4f, 0.25f, 0.1f),   // Log - dark brown
            new Color(0.9f, 0.8f, 0.3f),    // Squirt - yellow-brown
        };

        RacerAI[] aiRacers = new RacerAI[4];
        GameObject modelAsset = LoadModel("Assets/Models/MrCorny_Rigged.fbx");

        for (int i = 0; i < 4; i++)
        {
            // Same root/child pattern as player and SmoothSnake
            GameObject racer = new GameObject($"Racer_{presets[i]}");
            racer.transform.position = new Vector3(0, -3f, -(5f + i * 3f));

            if (modelAsset != null)
            {
                GameObject model = (GameObject)Object.Instantiate(modelAsset);
                model.name = "Model";
                model.transform.SetParent(racer.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one * 0.41f; // ~80% of player (0.51) for visible racing

                // Remove imported colliders
                foreach (Collider c in model.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c);

                // Unique body color per racer
                Material bodyMat = MakeURPMat($"Racer_{presets[i]}_Body", racerColors[i], 0.1f, 0.65f);
                bodyMat.EnableKeyword("_EMISSION");
                bodyMat.SetColor("_EmissionColor", racerColors[i] * 0.15f);
                EditorUtility.SetDirty(bodyMat);

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = bodyMat;
                    r.sharedMaterials = mats;
                }

                // Add face based on preset personality
                AddRacerFace(model, presets[i], racerColors[i]);

                // Save a gallery prefab for this racer (visual only, no AI/physics)
                SaveRacerGalleryPrefab(racer, presets[i]);
            }
            else
            {
                // Fallback: primitive body
                Material mat = MakeURPMat($"Racer_{presets[i]}_Body", racerColors[i], 0.1f, 0.65f);
                AddPrimChild(racer, "Body", PrimitiveType.Capsule, Vector3.zero,
                    Quaternion.Euler(90, 0, 0), new Vector3(0.5f, 1f, 0.4f), mat);
                SaveRacerGalleryPrefab(racer, presets[i]);
            }

            // Collider
            CapsuleCollider col = racer.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.24f;
            col.height = 0.8f;
            col.direction = 2; // Z axis

            // Slither animation
            racer.AddComponent<TurdSlither>();

            // AI controller with personality preset
            RacerAI ai = racer.AddComponent<RacerAI>();
            ai.pipeGen = pipeGen;
            ai.pipeRadius = 3f;
            ai.racerIndex = i;
            RacerAI.ApplyPreset(ai, presets[i]);

            aiRacers[i] = ai;
        }

        // ---- Race Manager ----
        GameObject rmObj = new GameObject("RaceManager");
        RaceManager rm = rmObj.AddComponent<RaceManager>();
        rm.playerController = playerController;
        rm.aiRacers = aiRacers;
        rm.pipeGen = pipeGen;
        rm.raceDistance = 2000f;
        // Race music loaded at runtime from Resources/music/ (all .ogg files)

        // ---- Race Leaderboard (on the game canvas) ----
        Canvas canvas = null;
        if (gameUI != null)
            canvas = gameUI.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            // Leaderboard panel (left side of screen)
            GameObject lbPanel = new GameObject("RaceLeaderboardPanel");
            RectTransform lbRect = lbPanel.AddComponent<RectTransform>();
            lbRect.SetParent(canvas.transform, false);
            lbRect.anchorMin = new Vector2(0.02f, 0.42f);
            lbRect.anchorMax = new Vector2(0.20f, 0.76f);
            lbRect.offsetMin = Vector2.zero;
            lbRect.offsetMax = Vector2.zero;

            RaceLeaderboard lb = lbPanel.AddComponent<RaceLeaderboard>();
            lb.panelRoot = lbRect;
            rm.leaderboard = lb;

            // Race Finish UI
            GameObject finishObj = new GameObject("RaceFinish");
            finishObj.transform.SetParent(rmObj.transform, false);
            RaceFinish rf = finishObj.AddComponent<RaceFinish>();
            rf.Initialize(canvas);
            rm.finishLine = rf;
        }

        Debug.Log("TTR: Created Brown Town Grand Prix race system! 4 AI racers + leaderboard + finish line.");
    }

    /// <summary>Add unique face features per AI racer personality (eyes, brows, mouth, nostrils).</summary>
    static void AddRacerFace(GameObject model, string preset, Color bodyColor)
    {
        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        Vector3 fwd = fb.fwd, upV = fb.upV, sideV = fb.sideV;
        float upExt = fb.upExt, sideExt = fb.sideExt;

        float eyeSize = Mathf.Max(sideExt, upExt) * 0.32f;
        eyeSize = Mathf.Clamp(eyeSize, 0.1f, 0.7f);
        float eyeGap = Mathf.Max(sideExt * 0.22f, 0.07f);
        Vector3 eyeBase = fb.frontPos + upV * (upExt * 0.3f);
        Vector3 mouthBase = fb.frontPos - upV * (upExt * 0.12f);

        // Shared materials
        Material whiteMat = MakeURPMat($"Racer_{preset}_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat($"Racer_{preset}_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material mouthMat = MakeURPMat($"Racer_{preset}_Mouth", new Color(0.15f, 0.02f, 0.02f), 0f, 0.4f);
        Material toothMat = MakeURPMat($"Racer_{preset}_Tooth", new Color(0.95f, 0.92f, 0.85f), 0.1f, 0.9f);
        Material nostrilMat = MakeURPMat($"Racer_{preset}_Nostril", new Color(0.12f, 0.06f, 0.03f), 0f, 0.15f);

        // Nostrils (shared by all racers)
        float nostrilGap = eyeGap * 0.35f;
        Vector3 nostrilBase = fb.frontPos + upV * (upExt * 0.05f);
        AddPrimChild(model, "LeftNostril", PrimitiveType.Sphere,
            nostrilBase - sideV * nostrilGap + fwd * (eyeSize * 0.2f),
            Quaternion.identity, Vector3.one * eyeSize * 0.18f, nostrilMat);
        AddPrimChild(model, "RightNostril", PrimitiveType.Sphere,
            nostrilBase + sideV * nostrilGap + fwd * (eyeSize * 0.2f),
            Quaternion.identity, Vector3.one * eyeSize * 0.18f, nostrilMat);

        switch (preset)
        {
            case "SkidmarkSteve":
            {
                // Angry brow, big wide eyes, snarling grimace with teeth
                AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap, Quaternion.identity,
                    new Vector3(eyeSize * 1.1f, eyeSize * 0.9f, eyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "LeftPupil", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + fwd * (eyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * eyeSize * 0.45f, pupilMat);
                AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap, Quaternion.identity,
                    new Vector3(eyeSize * 1.1f, eyeSize * 0.9f, eyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "RightPupil", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + fwd * (eyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * eyeSize * 0.45f, pupilMat);
                // Angry V-shaped brows
                Material browMat = MakeURPMat($"Racer_Steve_Brow", new Color(0.3f, 0.15f, 0.05f), 0f, 0.5f);
                AddPrimChild(model, "LeftBrow", PrimitiveType.Cube,
                    eyeBase - sideV * eyeGap + upV * (eyeSize * 0.55f),
                    Quaternion.Euler(0, 0, -25),
                    new Vector3(eyeSize * 0.85f, eyeSize * 0.18f, eyeSize * 0.3f), browMat);
                AddPrimChild(model, "RightBrow", PrimitiveType.Cube,
                    eyeBase + sideV * eyeGap + upV * (eyeSize * 0.55f),
                    Quaternion.Euler(0, 0, 25),
                    new Vector3(eyeSize * 0.85f, eyeSize * 0.18f, eyeSize * 0.3f), browMat);
                // Snarling mouth — wide, showing teeth
                AddPrimChild(model, "Mouth", PrimitiveType.Cube,
                    mouthBase + fwd * (eyeSize * 0.15f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 1.2f, eyeSize * 0.25f, eyeSize * 0.4f), mouthMat);
                // Gritted teeth
                for (int t = 0; t < 4; t++)
                {
                    float tx = -eyeSize * 0.35f + t * eyeSize * 0.24f;
                    AddPrimChild(model, $"Tooth{t}", PrimitiveType.Cube,
                        mouthBase + sideV * tx + fwd * (eyeSize * 0.25f) + upV * (eyeSize * 0.05f),
                        Quaternion.identity,
                        new Vector3(eyeSize * 0.15f, eyeSize * 0.15f, eyeSize * 0.12f), toothMat);
                }
                break;
            }

            case "PrincessPlop":
            {
                // Big sparkly eyes, eyelashes, cute kissy lips
                float pEyeSize = eyeSize * 1.2f;
                Material irisMat = MakeURPMat("Racer_Plop_Iris", new Color(0.3f, 0.1f, 0.4f), 0f, 0.9f);
                AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap, Quaternion.identity,
                    new Vector3(pEyeSize, pEyeSize * 1.1f, pEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "LeftPupil", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + fwd * (pEyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.5f, irisMat);
                // Sparkle in left eye
                Material sparkleMat = MakeURPMat("Racer_Plop_Sparkle", Color.white, 0f, 0.95f);
                AddPrimChild(model, "LeftSparkle", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + fwd * (pEyeSize * 0.35f) + upV * (pEyeSize * 0.15f) + sideV * (pEyeSize * 0.1f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.15f, sparkleMat);
                AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap, Quaternion.identity,
                    new Vector3(pEyeSize, pEyeSize * 1.1f, pEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "RightPupil", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + fwd * (pEyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.5f, irisMat);
                AddPrimChild(model, "RightSparkle", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + fwd * (pEyeSize * 0.35f) + upV * (pEyeSize * 0.15f) - sideV * (pEyeSize * 0.1f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.15f, sparkleMat);
                // Long eyelashes
                Material lashMat = MakeURPMat("Racer_Plop_Lash", new Color(0.1f, 0.05f, 0.05f), 0f, 0.5f);
                AddPrimChild(model, "LeftLash", PrimitiveType.Cube,
                    eyeBase - sideV * eyeGap + upV * (pEyeSize * 0.5f),
                    Quaternion.Euler(0, 0, -10),
                    new Vector3(pEyeSize * 0.9f, pEyeSize * 0.08f, pEyeSize * 0.3f), lashMat);
                AddPrimChild(model, "RightLash", PrimitiveType.Cube,
                    eyeBase + sideV * eyeGap + upV * (pEyeSize * 0.5f),
                    Quaternion.Euler(0, 0, 10),
                    new Vector3(pEyeSize * 0.9f, pEyeSize * 0.08f, pEyeSize * 0.3f), lashMat);
                // Cute kissy lips — small puckered mouth
                Material lipMat = MakeURPMat("Racer_Plop_Lip", new Color(0.9f, 0.4f, 0.5f), 0f, 0.5f);
                AddPrimChild(model, "UpperLip", PrimitiveType.Sphere,
                    mouthBase + fwd * (eyeSize * 0.2f) + upV * (eyeSize * 0.03f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.4f, eyeSize * 0.2f, eyeSize * 0.25f), lipMat);
                AddPrimChild(model, "LowerLip", PrimitiveType.Sphere,
                    mouthBase + fwd * (eyeSize * 0.2f) - upV * (eyeSize * 0.06f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.35f, eyeSize * 0.18f, eyeSize * 0.22f), lipMat);
                // Blush spots on cheeks
                Material blushMat = MakeURPMat("Racer_Plop_Blush", new Color(0.95f, 0.55f, 0.6f, 0.6f), 0f, 0.3f);
                AddPrimChild(model, "LeftBlush", PrimitiveType.Sphere,
                    eyeBase - sideV * (eyeGap * 1.6f) - upV * (eyeSize * 0.2f),
                    Quaternion.identity, new Vector3(eyeSize * 0.4f, eyeSize * 0.25f, eyeSize * 0.15f), blushMat);
                AddPrimChild(model, "RightBlush", PrimitiveType.Sphere,
                    eyeBase + sideV * (eyeGap * 1.6f) - upV * (eyeSize * 0.2f),
                    Quaternion.identity, new Vector3(eyeSize * 0.4f, eyeSize * 0.25f, eyeSize * 0.15f), blushMat);
                break;
            }

            case "TheLog":
            {
                // Small, squinty, unfazed eyes with flat unimpressed mouth
                float lEyeSize = eyeSize * 0.8f;
                Material lidMat = MakeURPMat("Racer_Log_Lid", new Color(0.25f, 0.15f, 0.06f), 0f, 0.5f);
                AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap, Quaternion.identity,
                    new Vector3(lEyeSize, lEyeSize * 0.5f, lEyeSize * 0.6f), whiteMat);
                AddPrimChild(model, "LeftPupil", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + fwd * (lEyeSize * 0.25f),
                    Quaternion.identity, Vector3.one * lEyeSize * 0.4f, pupilMat);
                AddPrimChild(model, "LeftLid", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + upV * (lEyeSize * 0.15f),
                    Quaternion.identity,
                    new Vector3(lEyeSize * 1.1f, lEyeSize * 0.35f, lEyeSize * 0.7f), lidMat);
                AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap, Quaternion.identity,
                    new Vector3(lEyeSize, lEyeSize * 0.5f, lEyeSize * 0.6f), whiteMat);
                AddPrimChild(model, "RightPupil", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + fwd * (lEyeSize * 0.25f),
                    Quaternion.identity, Vector3.one * lEyeSize * 0.4f, pupilMat);
                AddPrimChild(model, "RightLid", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + upV * (lEyeSize * 0.15f),
                    Quaternion.identity,
                    new Vector3(lEyeSize * 1.1f, lEyeSize * 0.35f, lEyeSize * 0.7f), lidMat);
                // Flat unimpressed line mouth — "meh"
                AddPrimChild(model, "Mouth", PrimitiveType.Cube,
                    mouthBase + fwd * (eyeSize * 0.15f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.8f, eyeSize * 0.08f, eyeSize * 0.2f), mouthMat);
                break;
            }

            case "LilSquirt":
            {
                // Huge wild googly eyes, cross-eyed, big goofy grin
                float sEyeSize = eyeSize * 1.3f;
                AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
                    eyeBase - sideV * (eyeGap * 1.2f), Quaternion.identity,
                    new Vector3(sEyeSize, sEyeSize, sEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "LeftPupil", PrimitiveType.Sphere,
                    eyeBase - sideV * (eyeGap * 1.2f) + fwd * (sEyeSize * 0.3f) + sideV * (sEyeSize * 0.15f),
                    Quaternion.identity, Vector3.one * sEyeSize * 0.5f, pupilMat);
                AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
                    eyeBase + sideV * (eyeGap * 1.2f), Quaternion.identity,
                    new Vector3(sEyeSize, sEyeSize, sEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "RightPupil", PrimitiveType.Sphere,
                    eyeBase + sideV * (eyeGap * 1.2f) + fwd * (sEyeSize * 0.3f) - sideV * (sEyeSize * 0.15f),
                    Quaternion.identity, Vector3.one * sEyeSize * 0.5f, pupilMat);
                // Big goofy grin — wide curved mouth with buck teeth
                AddPrimChild(model, "Mouth", PrimitiveType.Sphere,
                    mouthBase + fwd * (eyeSize * 0.1f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 1.3f, eyeSize * 0.4f, eyeSize * 0.35f), mouthMat);
                // Two big buck teeth
                AddPrimChild(model, "LeftTooth", PrimitiveType.Cube,
                    mouthBase + fwd * (eyeSize * 0.2f) - sideV * (eyeSize * 0.1f) + upV * (eyeSize * 0.08f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.2f, eyeSize * 0.22f, eyeSize * 0.15f), toothMat);
                AddPrimChild(model, "RightTooth", PrimitiveType.Cube,
                    mouthBase + fwd * (eyeSize * 0.2f) + sideV * (eyeSize * 0.1f) + upV * (eyeSize * 0.08f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.2f, eyeSize * 0.22f, eyeSize * 0.15f), toothMat);
                // Tongue sticking out
                Material tongueMat = MakeURPMat("Racer_Squirt_Tongue", new Color(0.85f, 0.35f, 0.35f), 0f, 0.4f);
                AddPrimChild(model, "Tongue", PrimitiveType.Sphere,
                    mouthBase + fwd * (eyeSize * 0.25f) - upV * (eyeSize * 0.15f),
                    Quaternion.identity,
                    new Vector3(eyeSize * 0.35f, eyeSize * 0.15f, eyeSize * 0.25f), tongueMat);
                break;
            }
        }
    }

    // ===== FACE DISPATCHER =====
    /// <summary>
    /// Applies the correct face features based on selected skin/character.
    /// Each character has unique comical face features.
    /// </summary>
    static void AddFaceForSkin(GameObject model, string skinId)
    {
        // Check if model has built-in face features from GLB (properly positioned in Blender)
        bool hasBuiltInFace = false;
        foreach (Transform child in model.GetComponentsInChildren<Transform>())
        {
            if (child.gameObject.name == "LeftEye" || child.gameObject.name == "RightEye")
            {
                hasBuiltInFace = true;
                break;
            }
        }

        if (hasBuiltInFace)
        {
            Debug.Log("TTR: Model has built-in face features from GLB, skipping procedural face");
            return;
        }

        // DESTROY FBX face meshes (eyes/pupils/mustache from Hunyuan generation).
        // They are positioned by armature bones and end up far from the body in Unity.
        var toDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in model.GetComponentsInChildren<Transform>())
        {
            if (child == model.transform) continue;
            string n = child.gameObject.name;
            if (n.Contains("Eye") || n.Contains("Pupil") || n.Contains("Mustache"))
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);

        switch (skinId)
        {
            case "DoodleDoo":   AddDoodleDooFace(model); break;
            case "ProfPlop":    AddProfPlopFace(model); break;
            case "BabyStool":   AddBabyStoolFace(model); break;
            case "ElTurdo":     AddElTurdoFace(model); break;
            default:            AddMrCornyFace(model); break; // all recolors use Mr Corny's face
        }
    }

    // ===== PROCEDURAL CORN KERNELS =====
    /// <summary>
    /// Adds raised yellow corn kernel nuggets on the poop body surface.
    /// Called when the FBX model doesn't have recognizable corn kernel sub-objects.
    /// Kernels are irregularly shaped spheres placed at semi-random positions on the body.
    /// </summary>
    static void AddProceduralCornKernels(GameObject model, Color cornYellow, Color cornShadow)
    {
        // Compute body bounds (skip face features)
        Bounds bodyBounds = new Bounds(model.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            string gn = r.gameObject.name;
            if (gn.Contains("Eye") || gn.Contains("Pupil") || gn.Contains("Mouth") ||
                gn.Contains("Brow") || gn.Contains("Lid") || gn.Contains("Stache") ||
                gn.Contains("Arm") || gn.Contains("Hand") || gn.Contains("Nostril") ||
                gn.Contains("Tooth") || gn.Contains("Tongue") || gn.Contains("Lip") ||
                gn.Contains("Cheek") || gn.Contains("Sweat") || gn.Contains("Spiral") ||
                gn.Contains("Sparkle") || gn.Contains("Lash"))
                continue;
            if (!hasBounds) { bodyBounds = r.bounds; hasBounds = true; }
            else bodyBounds.Encapsulate(r.bounds);
        }
        if (!hasBounds) return;

        // Corn kernel material - bright yellow with strong emission for visibility against brown body
        Material kernelMat = MakeURPMat("CornKernel_Proc", cornYellow, 0.05f, 0.45f);
        kernelMat.EnableKeyword("_EMISSION");
        kernelMat.SetColor("_EmissionColor", cornYellow * 0.35f);
        EditorUtility.SetDirty(kernelMat);

        // Darker underside material for depth
        Material kernelDarkMat = MakeURPMat("CornKernel_ProcShadow", cornShadow, 0.02f, 0.35f);

        // Place kernels at semi-random positions around the body surface
        // Use a fixed seed for reproducibility across rebuilds
        Random.State oldState = Random.state;
        Random.InitState(42);

        Vector3 center = model.transform.InverseTransformPoint(bodyBounds.center);
        Vector3 extents = bodyBounds.size;
        // Convert extents to local space scale
        float scaleInv = 1f / model.transform.lossyScale.x;
        extents *= scaleInv;

        int kernelCount = 14; // enough to look corny but not overwhelming
        for (int i = 0; i < kernelCount; i++)
        {
            // Distribute kernels around the body using spherical coordinates
            float theta = Random.Range(0f, Mathf.PI * 2f);
            float phi = Random.Range(0.25f, 0.75f); // avoid top/bottom poles
            float heightT = Random.Range(-0.3f, 0.3f); // along body length

            // Place kernels poking OUT of the body — 0.38x so they're clearly visible
            float radiusX = extents.x * 0.38f;
            float radiusZ = extents.z * 0.38f;

            Vector3 localPos = center + new Vector3(
                Mathf.Cos(theta) * radiusX * Mathf.Sin(phi * Mathf.PI),
                heightT * extents.y,
                Mathf.Sin(theta) * radiusZ * Mathf.Sin(phi * Mathf.PI)
            );

            // Kernel size — each corn nugget pokes out clearly above the brown body
            float baseSize = Random.Range(0.20f, 0.35f);
            Vector3 kernelScale = new Vector3(
                baseSize * Random.Range(0.8f, 1.2f),
                baseSize * Random.Range(0.7f, 1.1f),
                baseSize * Random.Range(0.8f, 1.2f)
            );

            // Orient each kernel to point outward from the body center
            Vector3 outward = (localPos - center).normalized;
            Quaternion rot = Quaternion.LookRotation(outward) *
                Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-20f, 20f), 0);

            GameObject kernel = AddPrimChild(model, $"CornKernel_{i}", PrimitiveType.Sphere,
                localPos, rot, kernelScale, kernelMat);

            // Add a tiny darker ring at the base of each kernel for depth
            AddPrimChild(kernel, "Shadow", PrimitiveType.Sphere,
                new Vector3(0, 0, -0.2f), Quaternion.identity,
                new Vector3(1.1f, 1.1f, 0.3f), kernelDarkMat);
        }

        Random.state = oldState;
        Debug.Log($"TTR: Added {kernelCount} procedural corn kernels to MrCorny body");
    }

    // ===== MR. CORNY FACE FEATURES =====
    /// <summary>
    /// Creates googly eyes, pupils, and corn-stache using primitive spheres positioned
    /// via ComputeFaceBounds(). FBX eye meshes are disabled (armature positions are wrong).
    /// Same proven approach as DoodleDoo/ProfPlop/BabyStool/ElTurdo face builders.
    /// </summary>
    static void AddMrCornyFace(GameObject model)
    {
        Material whiteMat = MakeURPMat("MrCorny_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.95f, 0.95f, 0.95f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("MrCorny_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material stacheMat = MakeURPMat("MrCorny_Stache", new Color(0.25f, 0.14f, 0.06f), 0f, 0.3f);

        // rear=true puts face on the BACK of the turd, facing the camera behind the player
        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        float eyeSize = fb.eyeSize;

        // === BIG GOOGLY EYES (on rear, facing camera) ===
        Vector3 eyeBase = fb.frontPos + fb.upV * (fb.upExt * 0.35f);

        GameObject leftEye = AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap, Quaternion.identity,
            Vector3.one * eyeSize, whiteMat);
        GameObject leftPupilObj = AddPrimChild(leftEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f, Quaternion.identity,
            Vector3.one * 0.45f, pupilMat);

        GameObject rightEye = AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
            eyeBase + fb.sideV * fb.eyeGap, Quaternion.identity,
            Vector3.one * eyeSize, whiteMat);
        GameObject rightPupilObj = AddPrimChild(rightEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f, Quaternion.identity,
            Vector3.one * 0.45f, pupilMat);

        // === IRISES (colored ring between white eye and black pupil) ===
        Material irisMat = MakeURPMat("MrCorny_Iris", new Color(0.05f, 0.05f, 0.05f), 0f, 0.6f);
        irisMat.EnableKeyword("_EMISSION");
        irisMat.SetColor("_EmissionColor", new Color(0.05f, 0.05f, 0.05f) * 0.3f);
        EditorUtility.SetDirty(irisMat);

        GameObject leftIrisObj = AddPrimChild(leftEye, "LeftIris", PrimitiveType.Sphere,
            fb.fwd * 0.32f, Quaternion.identity,
            Vector3.one * 0.55f, irisMat);
        GameObject rightIrisObj = AddPrimChild(rightEye, "RightIris", PrimitiveType.Sphere,
            fb.fwd * 0.32f, Quaternion.identity,
            Vector3.one * 0.55f, irisMat);

        // === EYE SPARKLE HIGHLIGHTS (tiny bright spheres) ===
        Material sparkleMat = MakeURPMat("MrCorny_Sparkle", Color.white, 0f, 0.95f);
        sparkleMat.EnableKeyword("_EMISSION");
        sparkleMat.SetColor("_EmissionColor", Color.white * 2f);
        EditorUtility.SetDirty(sparkleMat);

        AddPrimChild(leftEye, "LeftSparkle", PrimitiveType.Sphere,
            fb.fwd * 0.4f + fb.upV * 0.12f + fb.sideV * 0.08f, Quaternion.identity,
            Vector3.one * 0.1f, sparkleMat);
        AddPrimChild(rightEye, "RightSparkle", PrimitiveType.Sphere,
            fb.fwd * 0.4f + fb.upV * 0.12f - fb.sideV * 0.08f, Quaternion.identity,
            Vector3.one * 0.1f, sparkleMat);

        // === EYELIDS (half-dome over each eye) ===
        Material eyelidMat = MakeURPMat("MrCorny_Eyelid", new Color(0.48f, 0.3f, 0.12f), 0f, 0.3f);
        GameObject leftEyelid = AddPrimChild(model, "LeftEyelid", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.35f) + fb.fwd * 0.1f,
            Quaternion.identity,
            new Vector3(eyeSize * 1.05f, eyeSize * 0.35f, eyeSize * 0.6f), eyelidMat);
        GameObject rightEyelid = AddPrimChild(model, "RightEyelid", PrimitiveType.Sphere,
            eyeBase + fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.35f) + fb.fwd * 0.1f,
            Quaternion.identity,
            new Vector3(eyeSize * 1.05f, eyeSize * 0.35f, eyeSize * 0.6f), eyelidMat);

        // === EYEBROWS (expressive capsules above eyelids) ===
        Material browMat = MakeURPMat("MrCorny_Brow", new Color(0.05f, 0.05f, 0.05f), 0f, 0.3f);
        GameObject leftBrow = AddPrimChild(model, "LeftBrow", PrimitiveType.Capsule,
            eyeBase - fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.55f),
            Quaternion.Euler(0, 0, 8),
            new Vector3(eyeSize * 0.9f, eyeSize * 0.12f, eyeSize * 0.15f), browMat);
        GameObject rightBrow = AddPrimChild(model, "RightBrow", PrimitiveType.Capsule,
            eyeBase + fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.55f),
            Quaternion.Euler(0, 0, -8),
            new Vector3(eyeSize * 0.9f, eyeSize * 0.12f, eyeSize * 0.15f), browMat);

        // === CORN-STACHE (two curved corn-kernel bars + bridge) ===
        Vector3 stacheCenter = fb.frontPos - fb.upV * (fb.upExt * 0.05f);
        GameObject stacheBridge = AddPrimChild(model, "StacheBridge", PrimitiveType.Cube,
            stacheCenter, Quaternion.identity,
            new Vector3(eyeSize * 0.5f, eyeSize * 0.08f, eyeSize * 0.15f), stacheMat);
        GameObject stacheL = AddPrimChild(model, "StacheLeft", PrimitiveType.Capsule,
            stacheCenter - fb.sideV * (eyeSize * 0.35f) - fb.upV * (eyeSize * 0.08f),
            Quaternion.Euler(0, 0, 25),
            new Vector3(eyeSize * 0.15f, eyeSize * 0.3f, eyeSize * 0.15f), stacheMat);
        GameObject stacheR = AddPrimChild(model, "StacheRight", PrimitiveType.Capsule,
            stacheCenter + fb.sideV * (eyeSize * 0.35f) - fb.upV * (eyeSize * 0.08f),
            Quaternion.Euler(0, 0, -25),
            new Vector3(eyeSize * 0.15f, eyeSize * 0.3f, eyeSize * 0.15f), stacheMat);

        // === STACHE CURL TIPS (refined ends) ===
        GameObject stacheTipL = AddPrimChild(model, "StacheTipL", PrimitiveType.Sphere,
            stacheCenter - fb.sideV * (eyeSize * 0.58f) + fb.upV * (eyeSize * 0.02f),
            Quaternion.identity,
            Vector3.one * (eyeSize * 0.1f), stacheMat);
        GameObject stacheTipR = AddPrimChild(model, "StacheTipR", PrimitiveType.Sphere,
            stacheCenter + fb.sideV * (eyeSize * 0.58f) + fb.upV * (eyeSize * 0.02f),
            Quaternion.identity,
            Vector3.one * (eyeSize * 0.1f), stacheMat);

        // === NOSTRILS (two small dark indentations) ===
        Material nostrilMat = MakeURPMat("MrCorny_Nostril", new Color(0.12f, 0.06f, 0.03f), 0f, 0.15f);
        Vector3 nostrilCenter = fb.frontPos + fb.upV * (fb.upExt * 0.12f);
        GameObject nostrilL = AddPrimChild(model, "LeftNostril", PrimitiveType.Sphere,
            nostrilCenter - fb.sideV * (eyeSize * 0.12f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.1f, eyeSize * 0.08f, eyeSize * 0.08f), nostrilMat);
        GameObject nostrilR = AddPrimChild(model, "RightNostril", PrimitiveType.Sphere,
            nostrilCenter + fb.sideV * (eyeSize * 0.12f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.1f, eyeSize * 0.08f, eyeSize * 0.08f), nostrilMat);

        // === EXPRESSIVE MOUTH WITH JAW GROUPS ===
        Material mouthMat = MakeURPMat("MrCorny_Mouth", new Color(0.15f, 0.02f, 0.02f), 0f, 0.4f);
        Material toothMat = MakeURPMat("MrCorny_Tooth", new Color(0.95f, 0.95f, 0.88f), 0.1f, 0.9f);
        toothMat.EnableKeyword("_EMISSION");
        toothMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.85f) * 0.3f);
        EditorUtility.SetDirty(toothMat);
        // White lips so the mouth is clearly visible on the brown poop
        Material lipMat = MakeURPMat("MrCorny_Lip", new Color(0.95f, 0.92f, 0.88f), 0f, 0.5f);
        lipMat.EnableKeyword("_EMISSION");
        lipMat.SetColor("_EmissionColor", new Color(0.9f, 0.88f, 0.85f) * 0.2f);
        EditorUtility.SetDirty(lipMat);
        Material tongueMat = MakeURPMat("MrCorny_Tongue", new Color(0.85f, 0.35f, 0.35f), 0f, 0.4f);

        Vector3 mouthPos = fb.frontPos - fb.upV * (fb.upExt * 0.35f);

        // Mouth group parent (for overall position/scale animation)
        GameObject mouthGroup = new GameObject("MouthGroup");
        mouthGroup.transform.SetParent(model.transform);
        mouthGroup.transform.localPosition = mouthPos;
        mouthGroup.transform.localRotation = Quaternion.identity;
        mouthGroup.transform.localScale = Vector3.one;

        // Dark mouth interior (flattened sphere)
        GameObject mouthInterior = AddPrimChild(mouthGroup, "MouthInterior", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity,
            new Vector3(eyeSize * 0.8f, eyeSize * 0.28f, eyeSize * 0.25f), mouthMat);

        // Tongue (pink sphere inside mouth, slightly below center)
        GameObject tongueObj = AddPrimChild(mouthGroup, "Tongue", PrimitiveType.Sphere,
            -fb.upV * (eyeSize * 0.06f), Quaternion.identity,
            new Vector3(eyeSize * 0.35f, eyeSize * 0.1f, eyeSize * 0.15f), tongueMat);

        // Upper jaw pivot (rotates open/closed from top edge)
        GameObject upperJaw = new GameObject("UpperJaw");
        upperJaw.transform.SetParent(mouthGroup.transform);
        upperJaw.transform.localPosition = fb.upV * (eyeSize * 0.12f);
        upperJaw.transform.localRotation = Quaternion.identity;
        upperJaw.transform.localScale = Vector3.one;

        // Upper lip (smooth curved capsule)
        AddPrimChild(upperJaw, "UpperLip", PrimitiveType.Capsule,
            fb.upV * (eyeSize * 0.04f),
            Quaternion.Euler(0, 0, 90),
            new Vector3(eyeSize * 0.1f, eyeSize * 0.42f, eyeSize * 0.1f), lipMat);

        // Upper teeth row (single smooth capsule instead of individual cubes)
        AddPrimChild(upperJaw, "UpperTeethRow", PrimitiveType.Capsule,
            fb.fwd * (eyeSize * 0.06f) - fb.upV * (eyeSize * 0.02f),
            Quaternion.Euler(0, 0, 90),
            new Vector3(eyeSize * 0.08f, eyeSize * 0.32f, eyeSize * 0.08f), toothMat);

        // Lower jaw pivot (rotates open/closed from bottom edge)
        GameObject lowerJaw = new GameObject("LowerJaw");
        lowerJaw.transform.SetParent(mouthGroup.transform);
        lowerJaw.transform.localPosition = -fb.upV * (eyeSize * 0.12f);
        lowerJaw.transform.localRotation = Quaternion.identity;
        lowerJaw.transform.localScale = Vector3.one;

        // Lower lip
        AddPrimChild(lowerJaw, "LowerLip", PrimitiveType.Capsule,
            -fb.upV * (eyeSize * 0.04f),
            Quaternion.Euler(0, 0, 90),
            new Vector3(eyeSize * 0.09f, eyeSize * 0.38f, eyeSize * 0.09f), lipMat);

        // Lower teeth row (single smooth capsule)
        AddPrimChild(lowerJaw, "LowerTeethRow", PrimitiveType.Capsule,
            fb.fwd * (eyeSize * 0.06f) + fb.upV * (eyeSize * 0.02f),
            Quaternion.Euler(0, 0, 90),
            new Vector3(eyeSize * 0.07f, eyeSize * 0.28f, eyeSize * 0.07f), toothMat);

        // Cheek bumps (small spheres at mouth corners for smile expression)
        Material cheekMat = MakeURPMat("MrCorny_Cheek", new Color(0.5f, 0.28f, 0.15f), 0f, 0.3f);
        GameObject cheekL = AddPrimChild(mouthGroup, "CheekL", PrimitiveType.Sphere,
            -fb.sideV * (eyeSize * 0.42f) + fb.upV * (eyeSize * 0.04f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.15f, eyeSize * 0.12f, eyeSize * 0.1f), cheekMat);
        GameObject cheekR = AddPrimChild(mouthGroup, "CheekR", PrimitiveType.Sphere,
            fb.sideV * (eyeSize * 0.42f) + fb.upV * (eyeSize * 0.04f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.15f, eyeSize * 0.12f, eyeSize * 0.1f), cheekMat);

        // === STUBBY ARMS (comical little arms on sides near face) ===
        Material armMat = MakeURPMat("MrCorny_Arm", new Color(0.48f, 0.3f, 0.12f), 0f, 0.3f);
        Material handMat = MakeURPMat("MrCorny_Hand", new Color(0.58f, 0.38f, 0.18f), 0f, 0.35f);

        float armRad = eyeSize * 0.13f;
        float armHalfLen = eyeSize * 0.22f;
        Vector3 shoulderL = fb.frontPos - fb.sideV * (fb.sideExt * 0.55f) - fb.upV * (fb.upExt * 0.05f);
        Vector3 shoulderR = fb.frontPos + fb.sideV * (fb.sideExt * 0.55f) - fb.upV * (fb.upExt * 0.05f);

        // Left arm
        GameObject leftUpperArm = AddPrimChild(model, "LeftUpperArm", PrimitiveType.Capsule,
            shoulderL, Quaternion.Euler(0, 0, 70),
            new Vector3(armRad, armHalfLen, armRad), armMat);
        GameObject leftForearm = AddPrimChild(model, "LeftForearm", PrimitiveType.Capsule,
            shoulderL - fb.sideV * (eyeSize * 0.35f) - fb.upV * (eyeSize * 0.15f),
            Quaternion.Euler(0, 0, 55),
            new Vector3(armRad * 0.85f, armHalfLen * 0.8f, armRad * 0.85f), armMat);
        GameObject leftHandObj = AddPrimChild(model, "LeftHand", PrimitiveType.Sphere,
            shoulderL - fb.sideV * (eyeSize * 0.6f) - fb.upV * (eyeSize * 0.35f),
            Quaternion.identity,
            Vector3.one * (eyeSize * 0.14f), handMat);

        // Right arm (mirrored)
        GameObject rightUpperArm = AddPrimChild(model, "RightUpperArm", PrimitiveType.Capsule,
            shoulderR, Quaternion.Euler(0, 0, -70),
            new Vector3(armRad, armHalfLen, armRad), armMat);
        GameObject rightForearm = AddPrimChild(model, "RightForearm", PrimitiveType.Capsule,
            shoulderR + fb.sideV * (eyeSize * 0.35f) - fb.upV * (eyeSize * 0.15f),
            Quaternion.Euler(0, 0, -55),
            new Vector3(armRad * 0.85f, armHalfLen * 0.8f, armRad * 0.85f), armMat);
        GameObject rightHandObj = AddPrimChild(model, "RightHand", PrimitiveType.Sphere,
            shoulderR + fb.sideV * (eyeSize * 0.6f) - fb.upV * (eyeSize * 0.35f),
            Quaternion.identity,
            Vector3.one * (eyeSize * 0.14f), handMat);

        // === SWEAT DROPS (visible at high speed, start at full scale - animator hides them) ===
        Material sweatMat = MakeURPMat("MrCorny_Sweat", new Color(0.7f, 0.85f, 1f), 0f, 0.9f);
        sweatMat.EnableKeyword("_EMISSION");
        sweatMat.SetColor("_EmissionColor", new Color(0.6f, 0.8f, 1f) * 0.5f);
        EditorUtility.SetDirty(sweatMat);

        Vector3 sweatBaseL = eyeBase - fb.sideV * (fb.eyeGap + eyeSize * 0.3f) + fb.upV * (eyeSize * 0.5f);
        Vector3 sweatBaseR = eyeBase + fb.sideV * (fb.eyeGap + eyeSize * 0.3f) + fb.upV * (eyeSize * 0.5f);
        GameObject sweatObjL = AddPrimChild(model, "SweatL", PrimitiveType.Capsule,
            sweatBaseL, Quaternion.identity,
            new Vector3(eyeSize * 0.06f, eyeSize * 0.12f, eyeSize * 0.06f), sweatMat);
        GameObject sweatObjR = AddPrimChild(model, "SweatR", PrimitiveType.Capsule,
            sweatBaseR, Quaternion.identity,
            new Vector3(eyeSize * 0.06f, eyeSize * 0.12f, eyeSize * 0.06f), sweatMat);

        // === HYPNOTIC SPIRAL DISCS (disabled by default, enabled at 5x speed) ===
        Material spiralWhite = MakeURPMat("MrCorny_SpiralW", Color.white, 0f, 0.9f);
        spiralWhite.EnableKeyword("_EMISSION");
        spiralWhite.SetColor("_EmissionColor", Color.white * 0.8f);
        EditorUtility.SetDirty(spiralWhite);
        Material spiralBlack = MakeURPMat("MrCorny_SpiralB", new Color(0.05f, 0.05f, 0.05f), 0f, 0.9f);

        // Create spiral disc for each eye (pinwheel of alternating black/white spokes)
        GameObject hypnoL = CreateHypnoDisc(leftEye, "HypnoDiscL", fb.fwd, eyeSize,
            spiralWhite, spiralBlack);
        hypnoL.SetActive(false);
        GameObject hypnoR = CreateHypnoDisc(rightEye, "HypnoDiscR", fb.fwd, eyeSize,
            spiralWhite, spiralBlack);
        hypnoR.SetActive(false);

        // === REPARENT FACE TO HEAD BONE (so face follows slither deformation) ===
        // TurdSlither deforms spine bones in LateUpdate, which moves the mesh surface.
        // Face parts must be children of the LAST spine bone (head) so they track properly.
        Transform headBone = null;
        for (int i = 19; i >= 0; i--)
        {
            Transform bone = FindDeepChild(model.transform, $"Spine_{i:D2}");
            if (bone != null) { headBone = bone; break; }
        }
        if (headBone != null)
        {
            // Reparent top-level face parts from model root → head bone, preserving world pos
            GameObject[] faceParts = {
                leftEye, rightEye, mouthGroup, stacheBridge, stacheL, stacheR,
                leftEyelid, rightEyelid, leftBrow, rightBrow,
                nostrilL, nostrilR, stacheTipL, stacheTipR,
                leftUpperArm, leftForearm, leftHandObj, rightUpperArm, rightForearm, rightHandObj,
                sweatObjL, sweatObjR
            };
            foreach (var part in faceParts)
            {
                if (part != null)
                    part.transform.SetParent(headBone, worldPositionStays: true);
            }
            Debug.Log($"TTR: Reparented {faceParts.Length} face parts to head bone '{headBone.name}'");
        }

        // Wire up face animator with all new references
        MrCornyFaceAnimator anim = model.AddComponent<MrCornyFaceAnimator>();
        anim.leftEye = leftEye.transform;
        anim.rightEye = rightEye.transform;
        anim.leftPupil = leftPupilObj.transform;
        anim.rightPupil = rightPupilObj.transform;
        anim.stacheBridge = stacheBridge.transform;
        anim.stacheLeft = stacheL.transform;
        anim.stacheRight = stacheR.transform;
        anim.mouthGroup = mouthGroup.transform;
        anim.upperJaw = upperJaw.transform;
        anim.lowerJaw = lowerJaw.transform;
        anim.tongue = tongueObj.transform;
        anim.cheekL = cheekL.transform;
        anim.cheekR = cheekR.transform;
        anim.hypnoDiscL = hypnoL.transform;
        anim.hypnoDiscR = hypnoR.transform;
        anim.leftIris = leftIrisObj.transform;
        anim.rightIris = rightIrisObj.transform;
        anim.leftEyelid = leftEyelid.transform;
        anim.rightEyelid = rightEyelid.transform;
        anim.leftBrow = leftBrow.transform;
        anim.rightBrow = rightBrow.transform;
        anim.sweatL = sweatObjL.transform;
        anim.sweatR = sweatObjR.transform;

        Debug.Log($"TTR: Added MrCorny face with enhanced details (irises, eyelids, brows, nostrils, arms, sweat) eyeSize={eyeSize:F2}, " +
            $"frontPos={fb.frontPos}, fwdExt={fb.fwdExt:F2}");
    }

    /// <summary>
    /// Creates a pinwheel hypnotic disc on an eye. 8 alternating black/white spokes
    /// arranged in a flat disc that covers the eye. Spins rapidly when enabled.
    /// </summary>
    static GameObject CreateHypnoDisc(GameObject eye, string name, Vector3 fwd, float eyeSize,
        Material whiteMat, Material blackMat)
    {
        GameObject disc = new GameObject(name);
        disc.transform.SetParent(eye.transform);
        disc.transform.localPosition = fwd * 0.38f; // just in front of pupil
        disc.transform.localRotation = Quaternion.identity;
        disc.transform.localScale = Vector3.one;

        int spokeCount = 8;
        float spokeLen = 0.42f;
        float spokeWidth = 0.12f;
        float spokeThick = 0.02f;
        for (int i = 0; i < spokeCount; i++)
        {
            float angle = (360f / spokeCount) * i;
            Material mat = (i % 2 == 0) ? whiteMat : blackMat;

            GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoke.name = $"Spoke{i}";
            spoke.transform.SetParent(disc.transform);
            spoke.transform.localScale = new Vector3(spokeWidth, spokeLen, spokeThick);
            spoke.transform.localPosition = Vector3.zero;
            // Rotate spoke around local Z (forward axis of disc)
            spoke.transform.localRotation = Quaternion.Euler(0, 0, angle);
            // Offset spoke so it extends outward from center
            spoke.transform.localPosition = spoke.transform.localRotation * (Vector3.up * spokeLen * 0.5f);
            spoke.GetComponent<Renderer>().material = mat;
            Collider c = spoke.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        // Center dot (always visible, covers the seam)
        GameObject centerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centerDot.name = "CenterDot";
        centerDot.transform.SetParent(disc.transform);
        centerDot.transform.localPosition = Vector3.zero;
        centerDot.transform.localScale = Vector3.one * 0.15f;
        centerDot.GetComponent<Renderer>().material = blackMat;
        Collider cc = centerDot.GetComponent<Collider>();
        if (cc != null) Object.DestroyImmediate(cc);

        return disc;
    }

    // ===== DOODLE DOO FACE (artistic turd with beret, goatee, paint splats) =====
    static void AddDoodleDooFace(GameObject model)
    {
        Material whiteMat = MakeURPMat("Doodle_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Doodle_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material beretMat = MakeURPMat("Doodle_Beret", new Color(0.7f, 0.12f, 0.15f), 0f, 0.35f);
        Material goateeMat = MakeURPMat("Doodle_Goatee", new Color(0.15f, 0.08f, 0.03f), 0f, 0.2f);
        // Paint splat materials
        Material paintBlue = MakeURPMat("Doodle_PaintBlue", new Color(0.2f, 0.4f, 0.9f), 0f, 0.6f);
        paintBlue.EnableKeyword("_EMISSION");
        paintBlue.SetColor("_EmissionColor", new Color(0.1f, 0.2f, 0.5f) * 0.5f);
        EditorUtility.SetDirty(paintBlue);
        Material paintYellow = MakeURPMat("Doodle_PaintYellow", new Color(1f, 0.85f, 0.1f), 0f, 0.55f);
        paintYellow.EnableKeyword("_EMISSION");
        paintYellow.SetColor("_EmissionColor", new Color(0.5f, 0.4f, 0.05f) * 0.4f);
        EditorUtility.SetDirty(paintYellow);
        Material paintPink = MakeURPMat("Doodle_PaintPink", new Color(0.9f, 0.3f, 0.6f), 0f, 0.5f);

        // Compute face bounds on REAR so face is visible to camera behind player
        FaceBounds fb = ComputeFaceBounds(model, rear: true);

        // === BIG DREAMY EYES (slightly half-closed, artistic look) ===
        float eyeSize = fb.eyeSize;
        Vector3 eyeBase = fb.frontPos + fb.upV * (fb.upExt * 0.35f);

        GameObject leftEye = AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap, Quaternion.identity,
            new Vector3(eyeSize, eyeSize * 0.85f, eyeSize * 0.9f), whiteMat);
        AddPrimChild(leftEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f - fb.upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.4f, pupilMat);

        GameObject rightEye = AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
            eyeBase + fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.1f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.92f, eyeSize * 0.8f, eyeSize * 0.85f), whiteMat);
        AddPrimChild(rightEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.33f + fb.sideV * 0.08f, Quaternion.identity,
            Vector3.one * 0.42f, pupilMat);

        // === BERET (flat tilted disc on top) ===
        Vector3 beretPos = fb.frontPos + fb.upV * (fb.upExt * 0.75f) + fb.sideV * (fb.eyeGap * 0.3f);
        AddPrimChild(model, "BeretBase", PrimitiveType.Cylinder,
            beretPos, Quaternion.Euler(0, 0, 15),
            new Vector3(eyeSize * 1.8f, eyeSize * 0.12f, eyeSize * 1.8f), beretMat);
        // Beret nub on top
        AddPrimChild(model, "BeretNub", PrimitiveType.Sphere,
            beretPos + fb.upV * (eyeSize * 0.15f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.2f), beretMat);

        // === POINTY GOATEE ===
        Vector3 chinPos = fb.frontPos - fb.upV * (fb.upExt * 0.25f);
        AddPrimChild(model, "GoateeBase", PrimitiveType.Capsule,
            chinPos, Quaternion.identity,
            new Vector3(eyeSize * 0.2f, eyeSize * 0.45f, eyeSize * 0.2f), goateeMat);
        AddPrimChild(model, "GoateeTip", PrimitiveType.Sphere,
            chinPos - fb.upV * (eyeSize * 0.35f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.12f), goateeMat);

        // === PAINT SPLATS (decorative blobs on body) ===
        AddPrimChild(model, "PaintSplat1", PrimitiveType.Sphere,
            fb.frontPos + fb.sideV * (fb.sideExt * 0.5f) - fb.upV * (fb.upExt * 0.1f),
            Quaternion.identity, new Vector3(eyeSize * 0.4f, eyeSize * 0.15f, eyeSize * 0.35f), paintBlue);
        AddPrimChild(model, "PaintSplat2", PrimitiveType.Sphere,
            fb.frontPos - fb.sideV * (fb.sideExt * 0.4f) + fb.upV * (fb.upExt * 0.15f),
            Quaternion.identity, new Vector3(eyeSize * 0.3f, eyeSize * 0.12f, eyeSize * 0.28f), paintYellow);
        AddPrimChild(model, "PaintSplat3", PrimitiveType.Sphere,
            fb.frontPos - fb.fwd * (fb.fwdExt * 0.3f) + fb.sideV * (fb.sideExt * 0.2f),
            Quaternion.identity, new Vector3(eyeSize * 0.35f, eyeSize * 0.13f, eyeSize * 0.3f), paintPink);

        Debug.Log("TTR: Added Doodle Doo face (beret, goatee, paint splats)!");
    }

    // ===== PROFESSOR PLOP FACE (monocle, top hat, tiny mustache) =====
    static void AddProfPlopFace(GameObject model)
    {
        Material whiteMat = MakeURPMat("Prof_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Prof_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material hatMat = MakeURPMat("Prof_Hat", new Color(0.08f, 0.06f, 0.05f), 0.1f, 0.4f);
        Material hatBand = MakeURPMat("Prof_HatBand", new Color(0.55f, 0.12f, 0.12f), 0f, 0.3f);
        Material goldMat = MakeURPMat("Prof_Gold", new Color(0.85f, 0.7f, 0.2f), 0.8f, 0.85f);
        goldMat.EnableKeyword("_EMISSION");
        goldMat.SetColor("_EmissionColor", new Color(0.4f, 0.3f, 0.05f) * 0.5f);
        EditorUtility.SetDirty(goldMat);
        Material stacheMat = MakeURPMat("Prof_Stache", new Color(0.2f, 0.12f, 0.05f), 0f, 0.25f);
        Material monocleMat = MakeURPMat("Prof_Monocle", new Color(0.7f, 0.8f, 0.9f), 0.2f, 0.95f);
        monocleMat.EnableKeyword("_EMISSION");
        monocleMat.SetColor("_EmissionColor", new Color(0.3f, 0.35f, 0.4f) * 0.3f);
        EditorUtility.SetDirty(monocleMat);

        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        float eyeSize = fb.eyeSize;

        // === DISTINGUISHED EYES (one has monocle) ===
        Vector3 eyeBase = fb.frontPos + fb.upV * (fb.upExt * 0.35f);

        // Left eye (normal, but with a refined look)
        GameObject leftEye = AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap, Quaternion.identity,
            Vector3.one * eyeSize, whiteMat);
        AddPrimChild(leftEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f, Quaternion.identity,
            Vector3.one * 0.38f, pupilMat);

        // Right eye (slightly raised eyebrow look)
        Vector3 rightEyePos = eyeBase + fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.12f);
        GameObject rightEye = AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
            rightEyePos, Quaternion.identity,
            Vector3.one * (eyeSize * 0.95f), whiteMat);
        AddPrimChild(rightEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f, Quaternion.identity,
            Vector3.one * 0.36f, pupilMat);

        // === MONOCLE on right eye ===
        // Ring around right eye
        AddPrimChild(model, "MonocleRing", PrimitiveType.Cylinder,
            rightEyePos + fb.fwd * (eyeSize * 0.4f),
            Quaternion.LookRotation(fb.fwd, fb.upV),
            new Vector3(eyeSize * 1.25f, eyeSize * 0.04f, eyeSize * 1.25f), goldMat);
        // Glass lens
        AddPrimChild(model, "MonocleLens", PrimitiveType.Sphere,
            rightEyePos + fb.fwd * (eyeSize * 0.45f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.9f, eyeSize * 0.9f, eyeSize * 0.15f), monocleMat);
        // Chain hanging down
        AddPrimChild(model, "MonocleChain", PrimitiveType.Capsule,
            rightEyePos + fb.fwd * (eyeSize * 0.35f) - fb.upV * (eyeSize * 0.6f) + fb.sideV * (eyeSize * 0.4f),
            Quaternion.Euler(0, 0, -20),
            new Vector3(eyeSize * 0.04f, eyeSize * 0.5f, eyeSize * 0.04f), goldMat);

        // === TOP HAT ===
        Vector3 hatBase = fb.frontPos + fb.upV * (fb.upExt * 0.65f);
        // Hat brim
        AddPrimChild(model, "HatBrim", PrimitiveType.Cylinder,
            hatBase, Quaternion.identity,
            new Vector3(eyeSize * 2.2f, eyeSize * 0.06f, eyeSize * 2.2f), hatMat);
        // Hat cylinder
        AddPrimChild(model, "HatTop", PrimitiveType.Cylinder,
            hatBase + fb.upV * (eyeSize * 0.55f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.3f, eyeSize * 0.5f, eyeSize * 1.3f), hatMat);
        // Hat band
        AddPrimChild(model, "HatBandStripe", PrimitiveType.Cylinder,
            hatBase + fb.upV * (eyeSize * 0.15f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.35f, eyeSize * 0.06f, eyeSize * 1.35f), hatBand);

        // === TINY DISTINGUISHED MUSTACHE ===
        Vector3 stachePos = fb.frontPos - fb.upV * (fb.upExt * 0.05f);
        AddPrimChild(model, "StacheLeft", PrimitiveType.Capsule,
            stachePos - fb.sideV * (fb.eyeGap * 0.3f),
            Quaternion.Euler(0, 0, -35),
            new Vector3(eyeSize * 0.12f, eyeSize * 0.3f, eyeSize * 0.12f), stacheMat);
        AddPrimChild(model, "StacheRight", PrimitiveType.Capsule,
            stachePos + fb.sideV * (fb.eyeGap * 0.3f),
            Quaternion.Euler(0, 0, 35),
            new Vector3(eyeSize * 0.12f, eyeSize * 0.3f, eyeSize * 0.12f), stacheMat);
        // Curled tips
        AddPrimChild(model, "StacheTipL", PrimitiveType.Sphere,
            stachePos - fb.sideV * (fb.eyeGap * 0.55f) + fb.upV * (eyeSize * 0.1f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.1f), stacheMat);
        AddPrimChild(model, "StacheTipR", PrimitiveType.Sphere,
            stachePos + fb.sideV * (fb.eyeGap * 0.55f) + fb.upV * (eyeSize * 0.1f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.1f), stacheMat);

        Debug.Log("TTR: Added Prof. Plop face (monocle, top hat, tiny mustache)!");
    }

    // ===== BABY STOOL FACE (pacifier, bonnet, huge innocent eyes) =====
    static void AddBabyStoolFace(GameObject model)
    {
        Material whiteMat = MakeURPMat("Baby_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.95f, 0.95f, 0.95f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Baby_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material irisMat = MakeURPMat("Baby_Iris", new Color(0.3f, 0.6f, 0.9f), 0f, 0.7f); // big blue baby eyes
        irisMat.EnableKeyword("_EMISSION");
        irisMat.SetColor("_EmissionColor", new Color(0.15f, 0.3f, 0.5f) * 0.3f);
        EditorUtility.SetDirty(irisMat);
        Material bonnetMat = MakeURPMat("Baby_Bonnet", new Color(0.9f, 0.75f, 0.85f), 0f, 0.3f); // soft pink
        Material pacifierMat = MakeURPMat("Baby_Pacifier", new Color(0.4f, 0.85f, 0.9f), 0.1f, 0.75f); // baby blue
        Material pacRingMat = MakeURPMat("Baby_PacRing", new Color(0.9f, 0.9f, 0.5f), 0.3f, 0.7f); // yellow ring
        Material blushMat = MakeURPMat("Baby_Blush", new Color(0.9f, 0.55f, 0.55f), 0f, 0.15f);

        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        float eyeSize = fb.eyeSize * 1.3f; // HUGE baby eyes

        // === ENORMOUS INNOCENT EYES ===
        Vector3 eyeBase = fb.frontPos + fb.upV * (fb.upExt * 0.3f);

        GameObject leftEye = AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap, Quaternion.identity,
            Vector3.one * eyeSize, whiteMat);
        // Big blue iris
        AddPrimChild(leftEye, "LeftIris", PrimitiveType.Sphere,
            fb.fwd * 0.3f - fb.upV * 0.02f, Quaternion.identity,
            Vector3.one * 0.6f, irisMat);
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            fb.fwd * 0.38f - fb.upV * 0.02f, Quaternion.identity,
            Vector3.one * 0.3f, pupilMat);
        // Sparkle highlight
        Material sparkleMat = MakeURPMat("Baby_Sparkle", Color.white, 0f, 0.95f);
        sparkleMat.EnableKeyword("_EMISSION");
        sparkleMat.SetColor("_EmissionColor", Color.white * 2f);
        EditorUtility.SetDirty(sparkleMat);
        AddPrimChild(leftEye, "LeftSparkle", PrimitiveType.Sphere,
            fb.fwd * 0.4f + fb.upV * 0.15f + fb.sideV * 0.1f,
            Quaternion.identity, Vector3.one * 0.12f, sparkleMat);

        GameObject rightEye = AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
            eyeBase + fb.sideV * fb.eyeGap, Quaternion.identity,
            Vector3.one * (eyeSize * 1.05f), whiteMat); // slightly different for cuteness
        AddPrimChild(rightEye, "RightIris", PrimitiveType.Sphere,
            fb.fwd * 0.3f + fb.upV * 0.01f, Quaternion.identity,
            Vector3.one * 0.58f, irisMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            fb.fwd * 0.38f + fb.upV * 0.01f, Quaternion.identity,
            Vector3.one * 0.28f, pupilMat);
        AddPrimChild(rightEye, "RightSparkle", PrimitiveType.Sphere,
            fb.fwd * 0.4f + fb.upV * 0.15f - fb.sideV * 0.1f,
            Quaternion.identity, Vector3.one * 0.1f, sparkleMat);

        // === BLUSH CHEEKS ===
        AddPrimChild(model, "BlushLeft", PrimitiveType.Sphere,
            eyeBase - fb.sideV * (fb.eyeGap * 1.3f) - fb.upV * (eyeSize * 0.3f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.4f, eyeSize * 0.25f, eyeSize * 0.15f), blushMat);
        AddPrimChild(model, "BlushRight", PrimitiveType.Sphere,
            eyeBase + fb.sideV * (fb.eyeGap * 1.3f) - fb.upV * (eyeSize * 0.3f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.4f, eyeSize * 0.25f, eyeSize * 0.15f), blushMat);

        // === BONNET ===
        Vector3 bonnetPos = fb.frontPos + fb.upV * (fb.upExt * 0.6f);
        // Main bonnet shell
        AddPrimChild(model, "BonnetShell", PrimitiveType.Sphere,
            bonnetPos - fb.fwd * (fb.fwdExt * 0.1f),
            Quaternion.identity,
            new Vector3(eyeSize * 2.2f, eyeSize * 1.5f, eyeSize * 1.8f), bonnetMat);
        // Bonnet brim/ruffle
        AddPrimChild(model, "BonnetRuffle", PrimitiveType.Cylinder,
            bonnetPos + fb.fwd * (fb.fwdExt * 0.1f) - fb.upV * (eyeSize * 0.1f),
            Quaternion.identity,
            new Vector3(eyeSize * 2.4f, eyeSize * 0.08f, eyeSize * 2.4f), bonnetMat);
        // Ribbon ties
        AddPrimChild(model, "RibbonL", PrimitiveType.Capsule,
            bonnetPos - fb.sideV * (eyeSize * 0.8f) - fb.upV * (eyeSize * 0.6f),
            Quaternion.Euler(0, 0, 20),
            new Vector3(eyeSize * 0.12f, eyeSize * 0.35f, eyeSize * 0.12f), bonnetMat);
        AddPrimChild(model, "RibbonR", PrimitiveType.Capsule,
            bonnetPos + fb.sideV * (eyeSize * 0.8f) - fb.upV * (eyeSize * 0.6f),
            Quaternion.Euler(0, 0, -20),
            new Vector3(eyeSize * 0.12f, eyeSize * 0.35f, eyeSize * 0.12f), bonnetMat);

        // === PACIFIER ===
        Vector3 pacPos = fb.frontPos - fb.upV * (fb.upExt * 0.15f) + fb.fwd * (fb.fwdExt * 0.1f);
        // Shield/guard
        AddPrimChild(model, "PacShield", PrimitiveType.Sphere,
            pacPos, Quaternion.identity,
            new Vector3(eyeSize * 0.55f, eyeSize * 0.45f, eyeSize * 0.15f), pacifierMat);
        // Nipple
        AddPrimChild(model, "PacNipple", PrimitiveType.Sphere,
            pacPos + fb.fwd * (eyeSize * 0.15f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.2f), pacifierMat);
        // Ring handle
        AddPrimChild(model, "PacRing", PrimitiveType.Cylinder,
            pacPos - fb.fwd * (eyeSize * 0.1f),
            Quaternion.LookRotation(fb.fwd, fb.upV),
            new Vector3(eyeSize * 0.3f, eyeSize * 0.04f, eyeSize * 0.3f), pacRingMat);

        Debug.Log("TTR: Added Baby Stool face (huge baby eyes, bonnet, pacifier, blush cheeks)!");
    }

    // ===== EL TURDO FACE (wrestling mask, flexing arms, fierce scowl) =====
    static void AddElTurdoFace(GameObject model)
    {
        Material whiteMat = MakeURPMat("Turdo_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Turdo_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material maskMat = MakeURPMat("Turdo_Mask", new Color(0.8f, 0.12f, 0.08f), 0.1f, 0.5f); // red luchador
        maskMat.EnableKeyword("_EMISSION");
        maskMat.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.02f) * 0.4f);
        EditorUtility.SetDirty(maskMat);
        Material goldTrim = MakeURPMat("Turdo_GoldTrim", new Color(0.9f, 0.75f, 0.15f), 0.7f, 0.8f);
        goldTrim.EnableKeyword("_EMISSION");
        goldTrim.SetColor("_EmissionColor", new Color(0.45f, 0.35f, 0.05f) * 0.5f);
        EditorUtility.SetDirty(goldTrim);
        Material skinMat = MakeURPMat("Turdo_Skin", new Color(0.5f, 0.35f, 0.2f), 0f, 0.3f);
        Material mouthMat = MakeURPMat("Turdo_Mouth", new Color(0.12f, 0.02f, 0.02f), 0f, 0.4f);

        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        float eyeSize = fb.eyeSize;

        // === MASK covers most of head ===
        // Mask shell wrapping the front
        AddPrimChild(model, "MaskShell", PrimitiveType.Sphere,
            fb.frontPos + fb.fwd * (fb.fwdExt * 0.05f) + fb.upV * (fb.upExt * 0.15f),
            Quaternion.identity,
            new Vector3(fb.sideExt * 1.8f, fb.upExt * 1.6f, fb.fwdExt * 0.3f), maskMat);

        // Gold trim stripe down center
        AddPrimChild(model, "MaskStripe", PrimitiveType.Cube,
            fb.frontPos + fb.fwd * (fb.fwdExt * 0.15f) + fb.upV * (fb.upExt * 0.3f),
            Quaternion.identity,
            new Vector3(eyeSize * 0.15f, fb.upExt * 1.2f, eyeSize * 0.02f), goldTrim);

        // Gold trim X across forehead
        Vector3 xCenter = fb.frontPos + fb.upV * (fb.upExt * 0.55f) + fb.fwd * (fb.fwdExt * 0.12f);
        AddPrimChild(model, "MaskX1", PrimitiveType.Cube,
            xCenter, Quaternion.Euler(0, 0, 35),
            new Vector3(eyeSize * 1.2f, eyeSize * 0.08f, eyeSize * 0.02f), goldTrim);
        AddPrimChild(model, "MaskX2", PrimitiveType.Cube,
            xCenter, Quaternion.Euler(0, 0, -35),
            new Vector3(eyeSize * 1.2f, eyeSize * 0.08f, eyeSize * 0.02f), goldTrim);

        // === ANGRY EYES (triangular eye holes, angled eyebrows built in) ===
        Vector3 eyeBase = fb.frontPos + fb.upV * (fb.upExt * 0.3f);

        // Eye holes through mask - trapezoidal (wider at bottom = angry)
        GameObject leftEye = AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
            eyeBase - fb.sideV * fb.eyeGap + fb.fwd * (fb.fwdExt * 0.1f),
            Quaternion.Euler(0, 0, 15), // tilted for angry
            new Vector3(eyeSize * 0.9f, eyeSize * 0.65f, eyeSize * 0.8f), whiteMat);
        AddPrimChild(leftEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f - fb.upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.45f, pupilMat);

        // Angry eyebrow ridge (gold trim)
        AddPrimChild(model, "BrowL", PrimitiveType.Cube,
            eyeBase - fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.45f) + fb.fwd * (fb.fwdExt * 0.12f),
            Quaternion.Euler(0, 0, 15),
            new Vector3(eyeSize * 1.1f, eyeSize * 0.1f, eyeSize * 0.08f), goldTrim);

        GameObject rightEye = AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
            eyeBase + fb.sideV * fb.eyeGap + fb.fwd * (fb.fwdExt * 0.1f),
            Quaternion.Euler(0, 0, -15), // tilted angry
            new Vector3(eyeSize * 0.9f, eyeSize * 0.65f, eyeSize * 0.8f), whiteMat);
        AddPrimChild(rightEye, "Pupil", PrimitiveType.Sphere,
            fb.fwd * 0.35f - fb.upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.45f, pupilMat);

        AddPrimChild(model, "BrowR", PrimitiveType.Cube,
            eyeBase + fb.sideV * fb.eyeGap + fb.upV * (eyeSize * 0.45f) + fb.fwd * (fb.fwdExt * 0.12f),
            Quaternion.Euler(0, 0, -15),
            new Vector3(eyeSize * 1.1f, eyeSize * 0.1f, eyeSize * 0.08f), goldTrim);

        // === SNARLING MOUTH (exposed below mask) ===
        Vector3 mouthPos = fb.frontPos - fb.upV * (fb.upExt * 0.2f) + fb.fwd * (fb.fwdExt * 0.05f);
        AddPrimChild(model, "MouthInside", PrimitiveType.Sphere,
            mouthPos, Quaternion.identity,
            new Vector3(eyeSize * 0.7f, eyeSize * 0.3f, eyeSize * 0.25f), mouthMat);
        // Gritted teeth
        Material toothMat = MakeURPMat("Turdo_Teeth", new Color(0.95f, 0.92f, 0.82f), 0f, 0.7f);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2f) * eyeSize * 0.12f;
            AddPrimChild(model, $"Tooth{i}", PrimitiveType.Cube,
                mouthPos + fb.sideV * x + fb.fwd * (eyeSize * 0.12f),
                Quaternion.identity,
                new Vector3(eyeSize * 0.08f, eyeSize * 0.1f, eyeSize * 0.06f), toothMat);
        }

        // === FLEXING ARMS (small stubby but muscular) ===
        Material armMat = MakeURPMat("Turdo_Arm", new Color(0.45f, 0.3f, 0.15f), 0f, 0.35f);
        // Left arm flexing up
        Vector3 armBaseL = fb.frontPos - fb.sideV * (fb.sideExt * 0.75f) - fb.upV * (fb.upExt * 0.05f);
        AddPrimChild(model, "ArmL_Upper", PrimitiveType.Capsule,
            armBaseL - fb.sideV * (eyeSize * 0.15f),
            Quaternion.Euler(0, 0, 45),
            new Vector3(eyeSize * 0.22f, eyeSize * 0.4f, eyeSize * 0.22f), armMat);
        AddPrimChild(model, "ArmL_Forearm", PrimitiveType.Capsule,
            armBaseL - fb.sideV * (eyeSize * 0.25f) + fb.upV * (eyeSize * 0.45f),
            Quaternion.Euler(0, 0, -30),
            new Vector3(eyeSize * 0.2f, eyeSize * 0.3f, eyeSize * 0.2f), armMat);
        // Bicep bulge
        AddPrimChild(model, "BicepL", PrimitiveType.Sphere,
            armBaseL - fb.sideV * (eyeSize * 0.2f) + fb.upV * (eyeSize * 0.2f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.2f), armMat);
        // Fist
        AddPrimChild(model, "FistL", PrimitiveType.Sphere,
            armBaseL - fb.sideV * (eyeSize * 0.15f) + fb.upV * (eyeSize * 0.65f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.18f), armMat);

        // Right arm flexing up
        Vector3 armBaseR = fb.frontPos + fb.sideV * (fb.sideExt * 0.75f) - fb.upV * (fb.upExt * 0.05f);
        AddPrimChild(model, "ArmR_Upper", PrimitiveType.Capsule,
            armBaseR + fb.sideV * (eyeSize * 0.15f),
            Quaternion.Euler(0, 0, -45),
            new Vector3(eyeSize * 0.22f, eyeSize * 0.4f, eyeSize * 0.22f), armMat);
        AddPrimChild(model, "ArmR_Forearm", PrimitiveType.Capsule,
            armBaseR + fb.sideV * (eyeSize * 0.25f) + fb.upV * (eyeSize * 0.45f),
            Quaternion.Euler(0, 0, 30),
            new Vector3(eyeSize * 0.2f, eyeSize * 0.3f, eyeSize * 0.2f), armMat);
        AddPrimChild(model, "BicepR", PrimitiveType.Sphere,
            armBaseR + fb.sideV * (eyeSize * 0.2f) + fb.upV * (eyeSize * 0.2f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.2f), armMat);
        AddPrimChild(model, "FistR", PrimitiveType.Sphere,
            armBaseR + fb.sideV * (eyeSize * 0.15f) + fb.upV * (eyeSize * 0.65f),
            Quaternion.identity, Vector3.one * (eyeSize * 0.18f), armMat);

        Debug.Log("TTR: Added El Turdo face (wrestling mask, angry eyes, flexing arms)!");
    }

    // === Face bounds helper struct ===
    struct FaceBounds
    {
        public Vector3 frontPos, fwd, upV, sideV;
        public float fwdExt, upExt, sideExt, eyeGap, eyeSize;
    }

    /// <summary>
    /// Computes face positioning data by detecting the model's actual orientation from its
    /// bounding box, NOT assuming alignment with root.forward. The LONGEST axis of the AABB
    /// is the model's body length. This handles any FBX import rotation correctly.
    /// All computation is done in world space first, then converted to model-local via
    /// InverseTransformPoint (not InverseTransformDirection) to avoid rotation conversion issues.
    /// </summary>
    static FaceBounds ComputeFaceBounds(GameObject model, bool rear = false)
    {
        // Step 1: Get world-space bounds of the BODY mesh only.
        // Skip Unity primitives (procedural face features) and GLB face meshes (Eye/Pupil/Mouth).
        Bounds worldBounds = new Bounds(model.transform.position, Vector3.one * 0.01f);
        bool hasBounds = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            // Skip Unity primitives (procedural face features from AddMrCornyFace etc.)
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string mn = mf.sharedMesh.name;
                if (mn == "Sphere" || mn == "Capsule" || mn == "Cube" ||
                    mn == "Quad" || mn == "Cylinder")
                    continue;
            }
            // Skip GLB face meshes - they can shift body bounds and misposition procedural face
            // Skip GLB face meshes - they shift body bounds and misposition procedural face
            string goName = r.gameObject.name;
            if (goName.Contains("Eye") || goName.Contains("Pupil") || goName.Contains("Mouth")
                || goName.Contains("Mustache") || goName.Contains("Stache"))
                continue;

            Debug.Log($"TTR: FaceBounds including renderer '{goName}' type={r.GetType().Name} bounds={r.bounds.center} size={r.bounds.size}");
            if (!hasBounds) { worldBounds = r.bounds; hasBounds = true; }
            else worldBounds.Encapsulate(r.bounds);
        }
        if (!hasBounds)
        {
            Debug.LogWarning("TTR: ComputeFaceBounds - NO valid body renderers found! Using model position fallback.");
            worldBounds = new Bounds(model.transform.position, Vector3.one * 0.1f);
        }

        // Step 2: Detect model's actual orientation from bounding box geometry.
        // The LONGEST AABB axis = body length direction (forward).
        // Don't assume root.forward matches model geometry - Hunyuan FBX models
        // can be oriented along any axis depending on generation.
        Transform root = model.transform.parent != null ? model.transform.parent : model.transform;
        Vector3 we = worldBounds.extents;

        Vector3 worldFwd, worldUp, worldRight;
        float wFwdExt, wUpExt, wSideExt;

        if (we.x >= we.y && we.x >= we.z)
        {
            // X is longest = model lies along X axis
            worldFwd = Vector3.right;
            wFwdExt = we.x;
            worldUp = Vector3.up;
            wUpExt = we.y;
            worldRight = Vector3.forward;
            wSideExt = we.z;
        }
        else if (we.y >= we.x && we.y >= we.z)
        {
            // Y is longest = model stands vertically
            worldFwd = Vector3.up;
            wFwdExt = we.y;
            worldUp = Vector3.forward;
            wUpExt = we.z;
            worldRight = Vector3.right;
            wSideExt = we.x;
        }
        else
        {
            // Z is longest = standard orientation
            worldFwd = Vector3.forward;
            wFwdExt = we.z;
            worldUp = Vector3.up;
            wUpExt = we.y;
            worldRight = Vector3.right;
            wSideExt = we.x;
        }

        // Align forward with root's forward (travel direction)
        if (Vector3.Dot(worldFwd, root.forward) < 0)
            worldFwd = -worldFwd;

        // Ensure up points generally upward in world space
        if (Vector3.Dot(worldUp, Vector3.up) < 0)
            worldUp = -worldUp;

        // Recompute right to form proper right-handed basis
        worldRight = Vector3.Cross(worldFwd, worldUp).normalized;
        worldUp = Vector3.Cross(worldRight, worldFwd).normalized;

        // Step 3: Compute face position in world space
        Vector3 worldFrontPos;
        Vector3 faceFwd;
        if (rear)
        {
            worldFrontPos = worldBounds.center - worldFwd * wFwdExt * 0.85f;
            faceFwd = -worldFwd;
        }
        else
        {
            worldFrontPos = worldBounds.center + worldFwd * wFwdExt * 0.9f;
            faceFwd = worldFwd;
        }

        // Step 4: Convert to model-local space using InverseTransformPoint
        // (not InverseTransformDirection which can give wrong results with some FBX rotations)
        float uniformScale = model.transform.lossyScale.x;
        if (uniformScale < 0.001f) uniformScale = 1f;

        Vector3 localCenter = model.transform.InverseTransformPoint(worldBounds.center);
        Vector3 localFront = model.transform.InverseTransformPoint(worldFrontPos);

        // Derive local directions from transformed points
        Vector3 localFwd = (localFront - localCenter);
        if (localFwd.sqrMagnitude > 0.0001f) localFwd = localFwd.normalized;
        else localFwd = Vector3.forward;

        Vector3 localUpRef = model.transform.InverseTransformPoint(worldBounds.center + worldUp * 0.01f);
        Vector3 localRightRef = model.transform.InverseTransformPoint(worldBounds.center + worldRight * 0.01f);
        Vector3 localUp = (localUpRef - localCenter).normalized;
        Vector3 localRight = (localRightRef - localCenter).normalized;

        FaceBounds fb = new FaceBounds();
        fb.fwd = localFwd;
        fb.upV = localUp;
        fb.sideV = localRight;
        fb.fwdExt = wFwdExt / uniformScale;
        fb.upExt = wUpExt / uniformScale;
        fb.sideExt = wSideExt / uniformScale;
        fb.frontPos = localFront;
        // Use the SMALLER cross-section dimension so eyes fit within the model body
        // (prevents giant eyes on flat models like MrCorny which is wide but short)
        float crossMin = Mathf.Min(fb.sideExt, fb.upExt);
        fb.eyeGap = Mathf.Max(crossMin * 0.55f, 0.15f);
        fb.eyeSize = crossMin * 0.75f;
        fb.eyeSize = Mathf.Clamp(fb.eyeSize, 0.2f, 1.5f);

        Debug.Log($"TTR: FaceBounds rear={rear} worldCenter={worldBounds.center} worldFront={worldFrontPos} " +
            $"modelFwd={worldFwd} localFront={fb.frontPos} localFwd={fb.fwd} scale={uniformScale:F3} " +
            $"fwdExt={fb.fwdExt:F2} upExt={fb.upExt:F2} sideExt={fb.sideExt:F2} " +
            $"worldExtents=({we.x:F3}, {we.y:F3}, {we.z:F3})");

        return fb;
    }

    // ===== HAIR WAD OBSTACLE =====
    /// <summary>
    /// Builds a comical hair wad obstacle from primitives - tangled matted hair
    /// with googly eyes, open screaming mouth, and teeth. Big enough to partially block pipe.
    /// </summary>
    static GameObject CreateHairWadPrefab(string name, Color hairColor, Color darkColor)
    {
        string prefabPath = $"Assets/Prefabs/{name}.prefab";
        GameObject root = new GameObject(name);

        // Materials - strands get emission so they pop against body under toon shading
        Material hairMat = MakeURPMat($"{name}_Hair", hairColor, 0f, 0.15f);
        hairMat.EnableKeyword("_EMISSION");
        hairMat.SetColor("_EmissionColor", hairColor * 0.4f);
        EditorUtility.SetDirty(hairMat);
        Material darkMat = MakeURPMat($"{name}_Dark", darkColor * 0.6f, 0f, 0.1f);
        darkMat.EnableKeyword("_EMISSION");
        darkMat.SetColor("_EmissionColor", darkColor * 0.2f);
        EditorUtility.SetDirty(darkMat);

        // Bright strand material - contrasting highlights so individual hairs are visible
        Color brightHair = Color.Lerp(hairColor, Color.white, 0.35f);
        Material brightMat = MakeURPMat($"{name}_BrightHair", brightHair, 0f, 0.2f);
        brightMat.EnableKeyword("_EMISSION");
        brightMat.SetColor("_EmissionColor", brightHair * 0.5f);
        EditorUtility.SetDirty(brightMat);
        Material whiteMat = MakeURPMat($"{name}_EyeW", Color.white, 0f, 0.85f);
        Material pupilMat = MakeURPMat($"{name}_EyeP", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material mouthMat = MakeURPMat($"{name}_Mouth", new Color(0.15f, 0.02f, 0.02f), 0f, 0.5f);
        Material lipMat = MakeURPMat($"{name}_Lips", new Color(0.6f, 0.15f, 0.18f), 0f, 0.35f);
        Material toothMat = MakeURPMat($"{name}_Teeth", new Color(0.95f, 0.92f, 0.8f), 0f, 0.7f);

        // Core body - large flattened sphere of matted hair
        AddPrimChild(root, "HairCore", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(2.2f, 1.6f, 2f), hairMat);

        // Lumps for gross irregular shape
        AddPrimChild(root, "Lump0", PrimitiveType.Sphere, new Vector3(0.5f, 0.4f, 0.2f),
            Quaternion.identity, Vector3.one * 0.9f, darkMat);
        AddPrimChild(root, "Lump1", PrimitiveType.Sphere, new Vector3(-0.6f, 0.2f, -0.3f),
            Quaternion.identity, Vector3.one * 0.85f, hairMat);
        AddPrimChild(root, "Lump2", PrimitiveType.Sphere, new Vector3(0.2f, -0.3f, 0.4f),
            Quaternion.identity, Vector3.one * 0.8f, darkMat);
        AddPrimChild(root, "Lump3", PrimitiveType.Sphere, new Vector3(-0.3f, 0.5f, -0.1f),
            Quaternion.identity, Vector3.one * 0.75f, hairMat);

        // Hair strands sticking out in all directions - more strands, thinner, longer
        Material[] strandMats = { hairMat, darkMat, brightMat };
        float[] strandAngles = { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 };
        for (int i = 0; i < strandAngles.Length; i++)
        {
            float a = strandAngles[i] * Mathf.Deg2Rad;
            float y = (i % 3 - 1) * 0.35f;
            Vector3 pos = new Vector3(Mathf.Cos(a) * 0.85f, y, Mathf.Sin(a) * 0.85f);
            // Deterministic "random" angles using index math
            float rx = ((i * 37) % 140) - 70;
            float ry = (i * 63) % 360;
            float rz = ((i * 51) % 140) - 70;
            Quaternion rot = Quaternion.Euler(rx, ry, rz);
            float len = 0.5f + (i % 4) * 0.15f; // longer strands
            float thickness = 0.05f + (i % 3) * 0.02f; // varying thickness
            AddPrimChild(root, $"Strand{i}", PrimitiveType.Capsule, pos,
                rot, new Vector3(thickness, len, thickness), strandMats[i % 3]);
        }

        // Extra wispy outer strands - really thin and long for that tangled look
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45 + 15) * Mathf.Deg2Rad;
            float y = ((i * 31) % 5 - 2) * 0.2f;
            Vector3 pos = new Vector3(Mathf.Cos(a) * 1.1f, y, Mathf.Sin(a) * 1.1f);
            float rx = ((i * 53) % 160) - 80;
            float ry = (i * 77) % 360;
            Quaternion rot = Quaternion.Euler(rx, ry, 0);
            float len = 0.6f + (i % 3) * 0.2f;
            AddPrimChild(root, $"Wisp{i}", PrimitiveType.Capsule, pos,
                rot, new Vector3(0.03f, len, 0.03f), strandMats[(i + 1) % 3]);
        }

        // === GOOGLY EYES (asymmetric for comedy) ===
        // Left eye - bigger
        GameObject leftEye = AddPrimChild(root, "LeftEye", PrimitiveType.Sphere,
            new Vector3(-0.4f, 0.35f, 0.85f), Quaternion.identity, Vector3.one * 0.38f, whiteMat);
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            new Vector3(0.05f, -0.1f, 0.38f), Quaternion.identity, Vector3.one * 0.5f, pupilMat);

        // Right eye - smaller, slightly offset
        GameObject rightEye = AddPrimChild(root, "RightEye", PrimitiveType.Sphere,
            new Vector3(0.42f, 0.45f, 0.78f), Quaternion.identity, Vector3.one * 0.32f, whiteMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            new Vector3(-0.08f, -0.12f, 0.38f), Quaternion.identity, Vector3.one * 0.55f, pupilMat);

        // === SCREAMING MOUTH ===
        // Dark interior
        AddPrimChild(root, "MouthInside", PrimitiveType.Sphere,
            new Vector3(0, -0.1f, 0.88f), Quaternion.identity, new Vector3(0.6f, 0.4f, 0.3f), mouthMat);

        // Lips (horizontal capsules)
        AddPrimChild(root, "UpperLip", PrimitiveType.Capsule,
            new Vector3(0, 0.08f, 0.92f), Quaternion.Euler(0, 0, 90),
            new Vector3(0.12f, 0.32f, 0.12f), lipMat);
        AddPrimChild(root, "LowerLip", PrimitiveType.Capsule,
            new Vector3(0, -0.28f, 0.9f), Quaternion.Euler(0, 0, 90),
            new Vector3(0.13f, 0.34f, 0.13f), lipMat);

        // Teeth - janky little cubes for maximum comedy
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 0.13f;
            AddPrimChild(root, $"TopTooth{i}", PrimitiveType.Cube,
                new Vector3(x, 0.02f, 0.98f), Quaternion.identity,
                new Vector3(0.08f, 0.1f, 0.06f), toothMat);
            AddPrimChild(root, $"BotTooth{i}", PrimitiveType.Cube,
                new Vector3(x + 0.06f, -0.22f, 0.96f), Quaternion.identity,
                new Vector3(0.08f, 0.1f, 0.06f), toothMat);
        }

        // Collider and obstacle tag
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.center = Vector3.zero;
        col.radius = 1.2f;

        root.AddComponent<Obstacle>();
        root.AddComponent<HairWadBehavior>();
        root.tag = "Obstacle";

        // Scale up to partially block the sewer pipe
        root.transform.localScale = Vector3.one * 1.2f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"TTR: Created {name} obstacle with googly eyes, screaming mouth, and teeth!");
        return prefab;
    }

    // ===== SEWER RAT OBSTACLE =====
    /// <summary>
    /// Fat sewer rat with beady googly eyes, buck teeth, round ears, whiskers, and a long tail.
    /// </summary>
    static GameObject CreateSewerRatPrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerRat.prefab";
        GameObject root = new GameObject("SewerRat");

        Material furMat = MakeURPMat("Rat_Fur", new Color(0.4f, 0.3f, 0.18f), 0f, 0.2f);
        Material bellyMat = MakeURPMat("Rat_Belly", new Color(0.55f, 0.45f, 0.35f), 0f, 0.25f);
        Material noseMat = MakeURPMat("Rat_Nose", new Color(0.7f, 0.3f, 0.35f), 0f, 0.6f);
        Material earMat = MakeURPMat("Rat_Ear", new Color(0.65f, 0.35f, 0.4f), 0f, 0.4f);
        Material whiteMat = MakeURPMat("Rat_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Rat_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material toothMat = MakeURPMat("Rat_Teeth", new Color(0.9f, 0.85f, 0.6f), 0f, 0.5f);
        Material tailMat = MakeURPMat("Rat_Tail", new Color(0.6f, 0.4f, 0.45f), 0f, 0.5f);
        Material whiskerMat = MakeURPMat("Rat_Whisker", new Color(0.7f, 0.7f, 0.65f), 0f, 0.1f);

        // Fat body
        AddPrimChild(root, "Body", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(1.6f, 1.2f, 2f), furMat);
        // Lighter belly
        AddPrimChild(root, "Belly", PrimitiveType.Sphere, new Vector3(0, -0.2f, 0.1f),
            Quaternion.identity, new Vector3(1.3f, 0.9f, 1.6f), bellyMat);
        // Butt
        AddPrimChild(root, "Butt", PrimitiveType.Sphere, new Vector3(0, -0.05f, -0.7f),
            Quaternion.identity, new Vector3(1.2f, 1f, 1f), furMat);

        // Pointy snout
        AddPrimChild(root, "Snout", PrimitiveType.Sphere, new Vector3(0, -0.05f, 0.9f),
            Quaternion.identity, new Vector3(0.55f, 0.45f, 0.7f), furMat);
        // Big pink nose
        AddPrimChild(root, "Nose", PrimitiveType.Sphere, new Vector3(0, 0.02f, 1.25f),
            Quaternion.identity, Vector3.one * 0.22f, noseMat);

        // Big round ears
        AddPrimChild(root, "LeftEar", PrimitiveType.Sphere, new Vector3(-0.5f, 0.6f, 0.3f),
            Quaternion.identity, new Vector3(0.45f, 0.5f, 0.12f), earMat);
        AddPrimChild(root, "RightEar", PrimitiveType.Sphere, new Vector3(0.5f, 0.6f, 0.3f),
            Quaternion.identity, new Vector3(0.45f, 0.5f, 0.12f), earMat);

        // Googly eyes - beady and shifty
        GameObject le = AddPrimChild(root, "LeftEye", PrimitiveType.Sphere,
            new Vector3(-0.28f, 0.25f, 0.75f), Quaternion.identity, Vector3.one * 0.28f, whiteMat);
        AddPrimChild(le, "Pupil", PrimitiveType.Sphere,
            new Vector3(0.15f, -0.1f, 0.35f), Quaternion.identity, Vector3.one * 0.55f, pupilMat);
        GameObject re = AddPrimChild(root, "RightEye", PrimitiveType.Sphere,
            new Vector3(0.28f, 0.3f, 0.75f), Quaternion.identity, Vector3.one * 0.25f, whiteMat);
        AddPrimChild(re, "Pupil", PrimitiveType.Sphere,
            new Vector3(-0.15f, -0.12f, 0.35f), Quaternion.identity, Vector3.one * 0.6f, pupilMat);

        // Buck teeth
        AddPrimChild(root, "LeftTooth", PrimitiveType.Cube,
            new Vector3(-0.06f, -0.18f, 1.1f), Quaternion.identity,
            new Vector3(0.1f, 0.18f, 0.08f), toothMat);
        AddPrimChild(root, "RightTooth", PrimitiveType.Cube,
            new Vector3(0.06f, -0.18f, 1.1f), Quaternion.identity,
            new Vector3(0.1f, 0.18f, 0.08f), toothMat);

        // Whiskers (6 thin capsules)
        float[] whiskerAngles = { -25, 0, 25 };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = whiskerAngles[i];
                AddPrimChild(root, $"Whisker_{(side < 0 ? "L" : "R")}{i}", PrimitiveType.Capsule,
                    new Vector3(side * 0.35f, -0.02f + i * 0.06f, 1f),
                    Quaternion.Euler(a, 0, side * 75),
                    new Vector3(0.025f, 0.35f, 0.025f), whiskerMat);
            }
        }

        // Long tail
        AddPrimChild(root, "Tail1", PrimitiveType.Capsule,
            new Vector3(0, 0.05f, -1.1f), Quaternion.Euler(70, 0, 0),
            new Vector3(0.1f, 0.6f, 0.1f), tailMat);
        AddPrimChild(root, "Tail2", PrimitiveType.Capsule,
            new Vector3(0, 0.45f, -1.4f), Quaternion.Euler(30, 15, 0),
            new Vector3(0.07f, 0.45f, 0.07f), tailMat);

        // Tiny paws
        for (int sx = -1; sx <= 1; sx += 2)
        {
            AddPrimChild(root, $"FrontPaw{(sx < 0 ? "L" : "R")}", PrimitiveType.Sphere,
                new Vector3(sx * 0.45f, -0.55f, 0.5f), Quaternion.identity,
                new Vector3(0.18f, 0.1f, 0.22f), noseMat);
            AddPrimChild(root, $"BackPaw{(sx < 0 ? "L" : "R")}", PrimitiveType.Sphere,
                new Vector3(sx * 0.5f, -0.55f, -0.4f), Quaternion.identity,
                new Vector3(0.2f, 0.1f, 0.25f), noseMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.1f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerRatBehavior>();
        root.transform.localScale = Vector3.one * 0.9f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Rat with googly eyes, buck teeth, and whiskers!");
        return prefab;
    }

    // ===== TOXIC BARREL OBSTACLE =====
    /// <summary>
    /// Leaking toxic barrel with hazard stripes, skull face, oozing green slime.
    /// </summary>
    static GameObject CreateToxicBarrelPrefab()
    {
        string prefabPath = "Assets/Prefabs/ToxicBarrel.prefab";
        GameObject root = new GameObject("ToxicBarrel");

        // Dark/black barrel body - stands out against ANY pipe color
        Material barrelMat = MakeURPMat("Barrel_Body", new Color(0.08f, 0.08f, 0.06f), 0.4f, 0.5f);
        // Rusty metal bands
        Material bandMat = MakeURPMat("Barrel_Band", new Color(0.45f, 0.35f, 0.2f), 0.8f, 0.6f);
        bandMat.EnableKeyword("_EMISSION");
        bandMat.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.1f) * 0.2f);
        EditorUtility.SetDirty(bandMat);
        // Bright hazard yellow with strong glow
        Material hazardYellow = MakeURPMat("Barrel_Hazard", new Color(1f, 0.9f, 0.05f), 0.1f, 0.3f);
        hazardYellow.EnableKeyword("_EMISSION");
        hazardYellow.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.05f) * 1.5f);
        EditorUtility.SetDirty(hazardYellow);
        // Neon green toxic slime - unmistakable
        Material slimeMat = MakeURPMat("Barrel_Slime", new Color(0.2f, 1f, 0.1f), 0.2f, 0.9f);
        slimeMat.EnableKeyword("_EMISSION");
        slimeMat.SetColor("_EmissionColor", new Color(0.15f, 0.9f, 0.05f) * 4f);
        EditorUtility.SetDirty(slimeMat);
        // Blazing red skull eyes
        Material eyeMat = MakeURPMat("Barrel_Eye", new Color(1f, 0.15f, 0.05f), 0f, 0.85f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.02f) * 5f);
        EditorUtility.SetDirty(eyeMat);
        // Bone-white skull markings
        Material skullMat = MakeURPMat("Barrel_Skull", new Color(0.95f, 0.92f, 0.85f), 0f, 0.5f);
        skullMat.EnableKeyword("_EMISSION");
        skullMat.SetColor("_EmissionColor", new Color(0.8f, 0.75f, 0.65f) * 0.4f);
        EditorUtility.SetDirty(skullMat);
        Material darkMat = MakeURPMat("Barrel_Dark", new Color(0.03f, 0.03f, 0.02f), 0f, 0.3f);
        // Smoke wisp material - semi-transparent gray-green
        Material smokeMat = MakeURPMat("Barrel_Smoke", new Color(0.4f, 0.5f, 0.3f, 0.25f), 0f, 0.1f);
        smokeMat.EnableKeyword("_EMISSION");
        smokeMat.SetColor("_EmissionColor", new Color(0.3f, 0.45f, 0.15f) * 0.6f);
        smokeMat.SetFloat("_Surface", 1f);
        smokeMat.SetFloat("_Blend", 0f);
        smokeMat.SetFloat("_SrcBlend", 5f);
        smokeMat.SetFloat("_DstBlend", 10f);
        smokeMat.SetFloat("_ZWrite", 0f);
        smokeMat.SetOverrideTag("RenderType", "Transparent");
        smokeMat.renderQueue = 3000;
        smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(smokeMat);

        // Main barrel body - DARK so it pops in any zone
        AddPrimChild(root, "Barrel", PrimitiveType.Cylinder, Vector3.zero,
            Quaternion.identity, new Vector3(1.4f, 1.2f, 1.4f), barrelMat);
        // Dented top
        AddPrimChild(root, "Dent", PrimitiveType.Sphere, new Vector3(0.2f, 0.7f, 0.15f),
            Quaternion.identity, new Vector3(0.6f, 0.3f, 0.6f), barrelMat);

        // Metal bands
        AddPrimChild(root, "TopBand", PrimitiveType.Cylinder, new Vector3(0, 0.9f, 0),
            Quaternion.identity, new Vector3(1.55f, 0.07f, 1.55f), bandMat);
        AddPrimChild(root, "MidBand", PrimitiveType.Cylinder, new Vector3(0, 0, 0),
            Quaternion.identity, new Vector3(1.55f, 0.07f, 1.55f), bandMat);
        AddPrimChild(root, "BotBand", PrimitiveType.Cylinder, new Vector3(0, -0.9f, 0),
            Quaternion.identity, new Vector3(1.55f, 0.07f, 1.55f), bandMat);

        // Hazard warning stripes on front AND back (visible from both sides)
        for (int face = 0; face < 2; face++)
        {
            float zOff = (face == 0) ? 0.68f : -0.68f;
            for (int i = 0; i < 5; i++)
            {
                float y = -0.5f + i * 0.25f;
                AddPrimChild(root, $"Stripe{face}_{i}", PrimitiveType.Cube,
                    new Vector3(0, y, zOff), Quaternion.Euler(0, 0, 45),
                    new Vector3(0.55f, 0.14f, 0.04f), (i % 2 == 0) ? hazardYellow : darkMat);
            }
        }

        // SKULL AND CROSSBONES on front (larger, more visible)
        // Skull outline (white circle)
        AddPrimChild(root, "SkullHead", PrimitiveType.Sphere,
            new Vector3(0, 0.2f, 0.7f), Quaternion.identity,
            new Vector3(0.55f, 0.6f, 0.08f), skullMat);
        // Eye sockets (big dark holes)
        AddPrimChild(root, "LeftSocket", PrimitiveType.Sphere,
            new Vector3(-0.14f, 0.3f, 0.72f), Quaternion.identity,
            new Vector3(0.2f, 0.22f, 0.1f), darkMat);
        AddPrimChild(root, "RightSocket", PrimitiveType.Sphere,
            new Vector3(0.14f, 0.3f, 0.72f), Quaternion.identity,
            new Vector3(0.2f, 0.22f, 0.1f), darkMat);
        // Blazing red eyes inside sockets
        AddPrimChild(root, "LeftGlow", PrimitiveType.Sphere,
            new Vector3(-0.14f, 0.3f, 0.74f), Quaternion.identity,
            new Vector3(0.12f, 0.14f, 0.06f), eyeMat);
        AddPrimChild(root, "RightGlow", PrimitiveType.Sphere,
            new Vector3(0.14f, 0.3f, 0.74f), Quaternion.identity,
            new Vector3(0.12f, 0.14f, 0.06f), eyeMat);
        // Nose hole
        AddPrimChild(root, "NoseHole", PrimitiveType.Sphere,
            new Vector3(0, 0.1f, 0.73f), Quaternion.identity,
            new Vector3(0.08f, 0.1f, 0.05f), darkMat);
        // Jagged teeth
        for (int i = 0; i < 6; i++)
        {
            float x = (i - 2.5f) * 0.08f;
            float h = (i % 2 == 0) ? 0.1f : 0.07f;
            AddPrimChild(root, $"SkullTooth{i}", PrimitiveType.Cube,
                new Vector3(x, -0.08f, 0.72f), Quaternion.identity,
                new Vector3(0.06f, h, 0.04f), skullMat);
        }
        // CROSSBONES behind skull
        AddPrimChild(root, "Bone1", PrimitiveType.Capsule,
            new Vector3(0, 0.15f, 0.69f), Quaternion.Euler(0, 0, 45),
            new Vector3(0.08f, 0.4f, 0.08f), skullMat);
        AddPrimChild(root, "Bone2", PrimitiveType.Capsule,
            new Vector3(0, 0.15f, 0.69f), Quaternion.Euler(0, 0, -45),
            new Vector3(0.08f, 0.4f, 0.08f), skullMat);
        // Bone knobs at ends
        for (int b = 0; b < 4; b++)
        {
            float bx = (b < 2 ? -1f : 1f) * 0.28f;
            float by = (b % 2 == 0 ? -1f : 1f) * 0.28f + 0.15f;
            AddPrimChild(root, $"BoneKnob{b}", PrimitiveType.Sphere,
                new Vector3(bx, by, 0.69f), Quaternion.identity,
                Vector3.one * 0.07f, skullMat);
        }

        // Oozing slime over the top
        AddPrimChild(root, "SlimeTop", PrimitiveType.Sphere,
            new Vector3(-0.1f, 1.1f, 0.1f), Quaternion.identity,
            new Vector3(1.0f, 0.45f, 1.0f), slimeMat);
        // Dripping slime - thicker, more visible
        AddPrimChild(root, "Drip1", PrimitiveType.Capsule,
            new Vector3(0.35f, 0.5f, 0.55f), Quaternion.identity,
            new Vector3(0.15f, 0.45f, 0.15f), slimeMat);
        AddPrimChild(root, "Drip2", PrimitiveType.Capsule,
            new Vector3(-0.4f, 0.6f, -0.3f), Quaternion.identity,
            new Vector3(0.13f, 0.4f, 0.13f), slimeMat);
        AddPrimChild(root, "Drip3", PrimitiveType.Capsule,
            new Vector3(0.1f, 0.3f, -0.6f), Quaternion.identity,
            new Vector3(0.12f, 0.35f, 0.12f), slimeMat);
        AddPrimChild(root, "Drip4", PrimitiveType.Capsule,
            new Vector3(-0.15f, 0.4f, 0.6f), Quaternion.identity,
            new Vector3(0.11f, 0.3f, 0.11f), slimeMat);

        // SMOKE WISPS rising from top (semi-transparent puffs)
        AddPrimChild(root, "Smoke1", PrimitiveType.Sphere,
            new Vector3(0.1f, 1.5f, 0f), Quaternion.identity,
            new Vector3(0.5f, 0.35f, 0.5f), smokeMat);
        AddPrimChild(root, "Smoke2", PrimitiveType.Sphere,
            new Vector3(-0.15f, 1.85f, 0.1f), Quaternion.identity,
            new Vector3(0.4f, 0.3f, 0.4f), smokeMat);
        AddPrimChild(root, "Smoke3", PrimitiveType.Sphere,
            new Vector3(0.05f, 2.15f, -0.05f), Quaternion.identity,
            new Vector3(0.3f, 0.25f, 0.3f), smokeMat);

        // Puddle at base (brighter)
        AddPrimChild(root, "Puddle", PrimitiveType.Sphere,
            new Vector3(0.2f, -1.15f, 0.3f), Quaternion.identity,
            new Vector3(1.2f, 0.15f, 1.2f), slimeMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.0f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<ToxicBarrelBehavior>();
        root.transform.localScale = Vector3.one * 1.2f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Toxic Barrel with skull face and oozing slime!");
        return prefab;
    }

    // ===== POOP BLOB OBSTACLE =====
    /// <summary>
    /// Disgusting poop blob with sad googly eyes, frowning mouth, flies, and stink lines.
    /// </summary>
    static GameObject CreatePoopBlobPrefab()
    {
        string prefabPath = "Assets/Prefabs/PoopBlob.prefab";
        GameObject root = new GameObject("PoopBlob");

        Material poopMat = MakeURPMat("Poop_Main", new Color(0.4f, 0.25f, 0.1f), 0f, 0.45f);
        Material darkPoop = MakeURPMat("Poop_Dark", new Color(0.28f, 0.16f, 0.06f), 0f, 0.5f);
        Material lightPoop = MakeURPMat("Poop_Light", new Color(0.5f, 0.32f, 0.14f), 0f, 0.35f);
        Material whiteMat = MakeURPMat("Poop_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Poop_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material mouthMat = MakeURPMat("Poop_Mouth", new Color(0.15f, 0.05f, 0.03f), 0f, 0.5f);
        Material flyMat = MakeURPMat("Poop_Fly", new Color(0.08f, 0.08f, 0.06f), 0.2f, 0.4f);
        Material stinkMat = MakeURPMat("Poop_Stink", new Color(0.4f, 0.6f, 0.15f), 0f, 0.1f);
        stinkMat.EnableKeyword("_EMISSION");
        stinkMat.SetColor("_EmissionColor", new Color(0.3f, 0.45f, 0.1f));
        EditorUtility.SetDirty(stinkMat);
        Material tearMat = MakeURPMat("Poop_Tear", new Color(0.3f, 0.5f, 0.8f), 0f, 0.9f);

        // Main blobby body - irregular lumps
        AddPrimChild(root, "MainBlob", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(2f, 1.4f, 1.8f), poopMat);
        AddPrimChild(root, "Lump1", PrimitiveType.Sphere, new Vector3(0.5f, 0.35f, 0.2f),
            Quaternion.identity, new Vector3(1f, 0.7f, 0.9f), darkPoop);
        AddPrimChild(root, "Lump2", PrimitiveType.Sphere, new Vector3(-0.55f, 0.2f, -0.3f),
            Quaternion.identity, new Vector3(0.9f, 0.65f, 0.85f), lightPoop);
        AddPrimChild(root, "Lump3", PrimitiveType.Sphere, new Vector3(0.15f, -0.3f, 0.5f),
            Quaternion.identity, new Vector3(0.85f, 0.6f, 0.8f), poopMat);
        AddPrimChild(root, "Lump4", PrimitiveType.Sphere, new Vector3(-0.2f, 0.55f, 0.1f),
            Quaternion.identity, new Vector3(0.7f, 0.5f, 0.65f), darkPoop);
        // Slimy trail behind
        AddPrimChild(root, "Trail", PrimitiveType.Sphere, new Vector3(0, -0.65f, -0.5f),
            Quaternion.identity, new Vector3(1.5f, 0.15f, 1.8f), darkPoop);

        // Sad googly eyes - looking upward like "why me"
        GameObject le = AddPrimChild(root, "LeftEye", PrimitiveType.Sphere,
            new Vector3(-0.35f, 0.45f, 0.75f), Quaternion.identity, Vector3.one * 0.38f, whiteMat);
        AddPrimChild(le, "Pupil", PrimitiveType.Sphere,
            new Vector3(0.05f, 0.2f, 0.35f), Quaternion.identity, Vector3.one * 0.5f, pupilMat);
        // Sad eyebrow (tilted down on outside)
        AddPrimChild(root, "LeftBrow", PrimitiveType.Capsule,
            new Vector3(-0.35f, 0.7f, 0.78f), Quaternion.Euler(0, 0, 20),
            new Vector3(0.06f, 0.2f, 0.06f), darkPoop);

        GameObject re = AddPrimChild(root, "RightEye", PrimitiveType.Sphere,
            new Vector3(0.35f, 0.5f, 0.72f), Quaternion.identity, Vector3.one * 0.35f, whiteMat);
        AddPrimChild(re, "Pupil", PrimitiveType.Sphere,
            new Vector3(-0.05f, 0.18f, 0.35f), Quaternion.identity, Vector3.one * 0.5f, pupilMat);
        AddPrimChild(root, "RightBrow", PrimitiveType.Capsule,
            new Vector3(0.35f, 0.75f, 0.75f), Quaternion.Euler(0, 0, -20),
            new Vector3(0.06f, 0.2f, 0.06f), darkPoop);

        // Tear drops running down
        AddPrimChild(root, "LeftTear", PrimitiveType.Capsule,
            new Vector3(-0.38f, 0.1f, 0.82f), Quaternion.identity,
            new Vector3(0.06f, 0.18f, 0.06f), tearMat);
        AddPrimChild(root, "RightTear", PrimitiveType.Capsule,
            new Vector3(0.4f, 0.15f, 0.8f), Quaternion.identity,
            new Vector3(0.05f, 0.15f, 0.05f), tearMat);

        // Wide frowning mouth
        AddPrimChild(root, "MouthInside", PrimitiveType.Sphere,
            new Vector3(0, -0.05f, 0.82f), Quaternion.identity,
            new Vector3(0.55f, 0.3f, 0.2f), mouthMat);
        // Droopy lip
        AddPrimChild(root, "LowerLip", PrimitiveType.Capsule,
            new Vector3(0, -0.2f, 0.84f), Quaternion.Euler(0, 0, 90),
            new Vector3(0.08f, 0.3f, 0.08f), darkPoop);

        // Buzzing flies (small dark spheres around the blob)
        float[] flyAngles = { 30, 110, 200, 290, 160 };
        for (int i = 0; i < flyAngles.Length; i++)
        {
            float a = flyAngles[i] * Mathf.Deg2Rad;
            float h = 0.5f + (i % 3) * 0.35f;
            Vector3 fpos = new Vector3(Mathf.Cos(a) * 1.3f, h, Mathf.Sin(a) * 1.1f);
            GameObject fly = AddPrimChild(root, $"Fly{i}", PrimitiveType.Sphere, fpos,
                Quaternion.identity, Vector3.one * 0.08f, flyMat);
            // Tiny wings
            AddPrimChild(fly, "Wing", PrimitiveType.Sphere,
                new Vector3(0, 0.4f, 0), Quaternion.identity,
                new Vector3(2.5f, 0.3f, 1.5f), whiteMat);
        }

        // Stink wavy lines rising up
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.5f;
            AddPrimChild(root, $"Stink{i}", PrimitiveType.Capsule,
                new Vector3(x, 1f + i * 0.2f, 0), Quaternion.Euler(0, 0, (i - 1) * 15),
                new Vector3(0.04f, 0.4f, 0.04f), stinkMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.1f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<PoopBlobBehavior>();
        root.transform.localScale = Vector3.one * 1.0f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Poop Blob with sad eyes, flies, and stink lines!");
        return prefab;
    }

    // ===== SEWER MINE OBSTACLE =====
    /// <summary>
    /// Classic cartoon bomb mine that floats in the sewer water.
    /// Round black bomb body with a lit fuse on top. The fuse spark is the warning.
    /// Mostly submerged - only the top and fuse poke above the water surface.
    /// </summary>
    static GameObject CreateSewerMinePrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerMine.prefab";
        GameObject root = new GameObject("SewerMine");

        Material bombMat = MakeURPMat("Mine_Bomb", new Color(0.18f, 0.16f, 0.14f), 0.35f, 0.5f);
        Material fuseTubeMat = MakeURPMat("Mine_FuseTube", new Color(0.45f, 0.35f, 0.2f), 0f, 0.3f);
        Material fuseSparkMat = MakeURPMat("Mine_Spark", new Color(1f, 0.7f, 0.1f), 0f, 0.9f);
        fuseSparkMat.EnableKeyword("_EMISSION");
        fuseSparkMat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.05f) * 6f);
        EditorUtility.SetDirty(fuseSparkMat);
        Material metalRingMat = MakeURPMat("Mine_Ring", new Color(0.45f, 0.38f, 0.3f), 0.8f, 0.6f);
        metalRingMat.EnableKeyword("_EMISSION");
        metalRingMat.SetColor("_EmissionColor", new Color(0.3f, 0.25f, 0.2f) * 0.2f);
        EditorUtility.SetDirty(metalRingMat);
        // Pulsing red warning light material
        Material warnRedMat = MakeURPMat("Mine_WarnRed", new Color(1f, 0.1f, 0.05f), 0f, 0.9f);
        warnRedMat.EnableKeyword("_EMISSION");
        warnRedMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.05f) * 5f);
        EditorUtility.SetDirty(warnRedMat);

        // Main bomb body - big round black sphere
        AddPrimChild(root, "BombBody", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, Vector3.one * 1.5f, bombMat);

        // Metal ring around the fuse hole
        AddPrimChild(root, "FuseRing", PrimitiveType.Cylinder,
            new Vector3(0, 0.72f, 0), Quaternion.identity,
            new Vector3(0.25f, 0.04f, 0.25f), metalRingMat);

        // Fuse tube - sticks up and curves slightly
        AddPrimChild(root, "FuseTube", PrimitiveType.Cylinder,
            new Vector3(0, 0.85f, 0.05f), Quaternion.Euler(0, 0, 5f),
            new Vector3(0.06f, 0.15f, 0.06f), fuseTubeMat);

        // Fuse tip - curly bit at the top
        AddPrimChild(root, "FuseTip", PrimitiveType.Capsule,
            new Vector3(0.03f, 1.0f, 0.08f), Quaternion.Euler(0, 0, 30f),
            new Vector3(0.04f, 0.08f, 0.04f), fuseTubeMat);

        // Fuse spark - LARGE glowing ball at the lit end (primary warning)
        AddPrimChild(root, "WarnLight", PrimitiveType.Sphere,
            new Vector3(0.06f, 1.08f, 0.1f), Quaternion.identity,
            Vector3.one * 0.22f, fuseSparkMat);

        // Pulsing red warning light on front of bomb (secondary warning)
        AddPrimChild(root, "RedWarnLight", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.72f), Quaternion.identity,
            Vector3.one * 0.15f, warnRedMat);

        // Equator band (metallic sheen ring around the middle)
        AddPrimChild(root, "EquatorBand", PrimitiveType.Cylinder,
            new Vector3(0, 0, 0), Quaternion.identity,
            new Vector3(0.80f, 0.04f, 0.80f), metalRingMat);

        // Rivets around equator
        for (int i = 0; i < 6; i++)
        {
            float angle = i / 6f * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * 0.74f;
            float z = Mathf.Sin(angle) * 0.74f;
            AddPrimChild(root, $"Rivet{i}", PrimitiveType.Sphere,
                new Vector3(x, 0, z), Quaternion.identity,
                Vector3.one * 0.06f, metalRingMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.85f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerMineBehavior>();
        root.transform.localScale = Vector3.one * 0.95f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Mine - classic bomb with lit fuse!");
        return prefab;
    }

    // ===== COCKROACH OBSTACLE =====
    /// <summary>
    /// Giant cockroach with panicked oversized googly eyes, antennae, 6 legs, and wings.
    /// </summary>
    static GameObject CreateCockroachPrefab()
    {
        string prefabPath = "Assets/Prefabs/Cockroach.prefab";
        GameObject root = new GameObject("Cockroach");

        // Shiny chitin with visible sheen
        Material chitinMat = MakeURPMat("Roach_Chitin", new Color(0.3f, 0.2f, 0.1f), 0.2f, 0.85f);
        chitinMat.EnableKeyword("_EMISSION");
        chitinMat.SetColor("_EmissionColor", new Color(0.15f, 0.1f, 0.04f) * 0.3f);
        EditorUtility.SetDirty(chitinMat);
        Material darkMat = MakeURPMat("Roach_Dark", new Color(0.12f, 0.08f, 0.04f), 0.1f, 0.65f);
        Material bellyMat = MakeURPMat("Roach_Belly", new Color(0.35f, 0.22f, 0.1f), 0.05f, 0.5f);
        Material whiteMat = MakeURPMat("Roach_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(1f, 1f, 0.9f) * 1.5f);
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Roach_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material legMat = MakeURPMat("Roach_Leg", new Color(0.18f, 0.12f, 0.06f), 0.1f, 0.5f);
        Material antMat = MakeURPMat("Roach_Antenna", new Color(0.15f, 0.1f, 0.05f), 0f, 0.3f);
        Material wingMat = MakeURPMat("Roach_Wing", new Color(0.28f, 0.18f, 0.08f), 0.05f, 0.55f);
        Material mandMat = MakeURPMat("Roach_Mandible", new Color(0.3f, 0.15f, 0.05f), 0.2f, 0.5f);

        // Flat oval body - thorax
        AddPrimChild(root, "Thorax", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(1.6f, 0.6f, 1.4f), chitinMat);
        // Abdomen (bigger back section)
        AddPrimChild(root, "Abdomen", PrimitiveType.Sphere, new Vector3(0, -0.05f, -0.7f),
            Quaternion.identity, new Vector3(1.4f, 0.55f, 1.6f), darkMat);
        // Segmented abdomen lines
        for (int i = 0; i < 3; i++)
        {
            AddPrimChild(root, $"AbdSeg{i}", PrimitiveType.Cylinder,
                new Vector3(0, 0.2f, -0.4f - i * 0.3f), Quaternion.Euler(0, 0, 90),
                new Vector3(0.53f, 0.65f, 0.02f), chitinMat);
        }
        // Belly (underneath)
        AddPrimChild(root, "Belly", PrimitiveType.Sphere, new Vector3(0, -0.2f, -0.3f),
            Quaternion.identity, new Vector3(1.2f, 0.4f, 1.8f), bellyMat);

        // Head
        AddPrimChild(root, "Head", PrimitiveType.Sphere, new Vector3(0, 0.05f, 0.8f),
            Quaternion.identity, new Vector3(0.8f, 0.5f, 0.6f), chitinMat);

        // HUGE panicked googly eyes (comically oversized)
        GameObject le = AddPrimChild(root, "LeftEye", PrimitiveType.Sphere,
            new Vector3(-0.35f, 0.35f, 0.9f), Quaternion.identity, Vector3.one * 0.42f, whiteMat);
        // Tiny panicked pupil (looking different directions = panic)
        AddPrimChild(le, "Pupil", PrimitiveType.Sphere,
            new Vector3(-0.1f, 0.2f, 0.3f), Quaternion.identity, Vector3.one * 0.4f, pupilMat);
        GameObject re = AddPrimChild(root, "RightEye", PrimitiveType.Sphere,
            new Vector3(0.35f, 0.4f, 0.88f), Quaternion.identity, Vector3.one * 0.4f, whiteMat);
        AddPrimChild(re, "Pupil", PrimitiveType.Sphere,
            new Vector3(0.15f, 0.15f, 0.3f), Quaternion.identity, Vector3.one * 0.4f, pupilMat);

        // Antennae - long curved
        AddPrimChild(root, "LeftAntenna1", PrimitiveType.Capsule,
            new Vector3(-0.2f, 0.3f, 1.05f), Quaternion.Euler(-40, -20, 0),
            new Vector3(0.04f, 0.5f, 0.04f), antMat);
        AddPrimChild(root, "LeftAntenna2", PrimitiveType.Capsule,
            new Vector3(-0.35f, 0.7f, 1.3f), Quaternion.Euler(-65, -25, 0),
            new Vector3(0.03f, 0.35f, 0.03f), antMat);
        AddPrimChild(root, "RightAntenna1", PrimitiveType.Capsule,
            new Vector3(0.2f, 0.3f, 1.05f), Quaternion.Euler(-40, 20, 0),
            new Vector3(0.04f, 0.5f, 0.04f), antMat);
        AddPrimChild(root, "RightAntenna2", PrimitiveType.Capsule,
            new Vector3(0.35f, 0.7f, 1.3f), Quaternion.Euler(-65, 25, 0),
            new Vector3(0.03f, 0.35f, 0.03f), antMat);

        // Mandibles
        AddPrimChild(root, "LeftMandible", PrimitiveType.Cube,
            new Vector3(-0.12f, -0.08f, 1.08f), Quaternion.Euler(0, -15, -10),
            new Vector3(0.06f, 0.04f, 0.15f), mandMat);
        AddPrimChild(root, "RightMandible", PrimitiveType.Cube,
            new Vector3(0.12f, -0.08f, 1.08f), Quaternion.Euler(0, 15, 10),
            new Vector3(0.06f, 0.04f, 0.15f), mandMat);

        // 6 legs (3 per side) - jointed look
        float[] legZ = { 0.3f, 0f, -0.35f };
        float[] legSpread = { 20f, 40f, 55f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float spread = legSpread[i] * side;
                // Upper leg segment
                AddPrimChild(root, $"Leg{(side < 0 ? "L" : "R")}{i}_Upper", PrimitiveType.Capsule,
                    new Vector3(side * 0.7f, -0.1f, legZ[i]),
                    Quaternion.Euler(0, spread, side * 60),
                    new Vector3(0.06f, 0.35f, 0.06f), legMat);
                // Lower leg segment
                AddPrimChild(root, $"Leg{(side < 0 ? "L" : "R")}{i}_Lower", PrimitiveType.Capsule,
                    new Vector3(side * 1.15f, -0.4f, legZ[i] + i * 0.05f * side),
                    Quaternion.Euler(0, spread * 0.5f, side * 15),
                    new Vector3(0.04f, 0.3f, 0.04f), legMat);
            }
        }

        // Wings (folded on back)
        AddPrimChild(root, "LeftWing", PrimitiveType.Cube,
            new Vector3(-0.3f, 0.28f, -0.2f), Quaternion.Euler(0, -5, -5),
            new Vector3(0.55f, 0.04f, 1.2f), wingMat);
        AddPrimChild(root, "RightWing", PrimitiveType.Cube,
            new Vector3(0.3f, 0.28f, -0.2f), Quaternion.Euler(0, 5, 5),
            new Vector3(0.55f, 0.04f, 1.2f), wingMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.0f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<CockroachBehavior>();
        root.transform.localScale = Vector3.one * 1.1f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Cockroach with panicked googly eyes, antennae, and 6 legs!");
        return prefab;
    }

    // ===== SEWER SPIDER OBSTACLE =====
    /// <summary>
    /// Giant sewer spider - dark body, 8 spindly legs, red eyes, web strand.
    /// Hangs from ceiling and drops down when player approaches.
    /// </summary>
    static GameObject CreateSewerSpiderPrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerSpider.prefab";
        GameObject root = new GameObject("SewerSpider");

        // Materials
        Material bodyMat = MakeURPMat("Spider_Body", new Color(0.12f, 0.1f, 0.08f), 0.05f, 0.4f);
        Material legMat = MakeURPMat("Spider_Leg", new Color(0.15f, 0.12f, 0.08f), 0.05f, 0.35f);
        Material abdomenMat = MakeURPMat("Spider_Abdomen", new Color(0.1f, 0.08f, 0.06f), 0.05f, 0.5f);
        abdomenMat.EnableKeyword("_EMISSION");
        abdomenMat.SetColor("_EmissionColor", new Color(0.04f, 0.01f, 0.01f));
        EditorUtility.SetDirty(abdomenMat);
        Material eyeMat = MakeURPMat("Spider_EyeRed", new Color(0.9f, 0.1f, 0.05f), 0f, 0.9f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor("_EmissionColor", new Color(0.8f, 0.05f, 0.02f) * 2f);
        EditorUtility.SetDirty(eyeMat);
        Material whiteMat = MakeURPMat("Spider_EyeW", new Color(0.85f, 0.82f, 0.75f), 0f, 0.8f);
        Material webMat = MakeURPMat("Spider_Web", new Color(0.8f, 0.8f, 0.75f, 0.6f), 0f, 0.3f);
        Material fangMat = MakeURPMat("Spider_Fang", new Color(0.2f, 0.18f, 0.12f), 0.1f, 0.6f);

        // Cephalothorax (front body)
        AddPrimChild(root, "Body", PrimitiveType.Sphere, new Vector3(0, 0, 0.2f),
            Quaternion.identity, new Vector3(0.8f, 0.5f, 0.7f), bodyMat);

        // Abdomen (rear, larger and rounder)
        AddPrimChild(root, "Abdomen", PrimitiveType.Sphere, new Vector3(0, 0.05f, -0.5f),
            Quaternion.identity, new Vector3(1.1f, 0.9f, 1.3f), abdomenMat);

        // Skull marking on abdomen (lighter shape)
        Material markMat = MakeURPMat("Spider_Mark", new Color(0.25f, 0.15f, 0.08f), 0f, 0.3f);
        AddPrimChild(root, "Mark", PrimitiveType.Sphere, new Vector3(0, 0.35f, -0.45f),
            Quaternion.identity, new Vector3(0.3f, 0.12f, 0.3f), markMat);

        // 8 eyes (4 pairs, front-facing, spiders have lots of eyes!)
        float[] eyeX = { -0.12f, 0.12f, -0.2f, 0.2f, -0.08f, 0.08f, -0.15f, 0.15f };
        float[] eyeY = { 0.18f, 0.18f, 0.12f, 0.12f, 0.25f, 0.25f, 0.08f, 0.08f };
        float[] eyeSize = { 0.1f, 0.1f, 0.08f, 0.08f, 0.06f, 0.06f, 0.07f, 0.07f };
        for (int e = 0; e < 8; e++)
        {
            AddPrimChild(root, $"Eye{e}", PrimitiveType.Sphere,
                new Vector3(eyeX[e], eyeY[e], 0.5f), Quaternion.identity,
                Vector3.one * eyeSize[e], e < 4 ? whiteMat : eyeMat);
            if (e < 4)
            {
                // Main eyes get red pupils
                AddPrimChild(root, $"Pupil{e}", PrimitiveType.Sphere,
                    new Vector3(eyeX[e], eyeY[e], 0.54f), Quaternion.identity,
                    Vector3.one * (eyeSize[e] * 0.5f), eyeMat);
            }
        }

        // Fangs (chelicerae)
        AddPrimChild(root, "FangL", PrimitiveType.Capsule, new Vector3(-0.08f, -0.1f, 0.55f),
            Quaternion.Euler(15, 0, 10), new Vector3(0.06f, 0.12f, 0.05f), fangMat);
        AddPrimChild(root, "FangR", PrimitiveType.Capsule, new Vector3(0.08f, -0.1f, 0.55f),
            Quaternion.Euler(15, 0, -10), new Vector3(0.06f, 0.12f, 0.05f), fangMat);

        // 8 spindly legs (4 per side)
        float[] legAngles = { 30f, 55f, 80f, 105f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = legAngles[i] * Mathf.Deg2Rad;
                float lx = side * 0.3f;
                float lz = 0.1f - i * 0.18f;
                string sideStr = side < 0 ? "L" : "R";

                // Upper segment
                AddPrimChild(root, $"Leg{sideStr}{i}", PrimitiveType.Capsule,
                    new Vector3(lx, 0.1f, lz),
                    Quaternion.Euler(0, 0, side * (40 + i * 5)),
                    new Vector3(0.04f, 0.35f, 0.04f), legMat);

                // Lower segment (bent downward)
                AddPrimChild(root, $"Leg{sideStr}{i}_Lower", PrimitiveType.Capsule,
                    new Vector3(lx + side * 0.4f, -0.2f, lz),
                    Quaternion.Euler(0, 0, side * (15 + i * 3)),
                    new Vector3(0.03f, 0.3f, 0.03f), legMat);
            }
        }

        // Web strand above (connects to ceiling)
        AddPrimChild(root, "WebStrand", PrimitiveType.Cylinder, new Vector3(0, 1.2f, -0.2f),
            Quaternion.identity, new Vector3(0.02f, 1f, 0.02f), webMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.9f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerSpiderBehavior>();
        root.transform.localScale = Vector3.one * 0.55f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Spider with 8 eyes, 8 legs, fangs, and web strand!");
        return prefab;
    }

    // ===== SEWER SNAKE OBSTACLE =====
    static GameObject CreateSewerSnakePrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerSnake.prefab";
        GameObject root = new GameObject("SewerSnake");

        Material skinMat = MakeURPMat("Snake_Skin", new Color(0.25f, 0.35f, 0.12f), 0.05f, 0.7f);
        skinMat.EnableKeyword("_EMISSION");
        skinMat.SetColor("_EmissionColor", new Color(0.03f, 0.06f, 0.01f));
        EditorUtility.SetDirty(skinMat);
        Material bellyMat = MakeURPMat("Snake_Belly", new Color(0.6f, 0.55f, 0.3f), 0f, 0.6f);
        Material whiteMat = MakeURPMat("Snake_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Snake_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material tongueMat = MakeURPMat("Snake_Tongue", new Color(0.85f, 0.2f, 0.25f), 0f, 0.6f);

        // Head (slightly flattened)
        AddPrimChild(root, "Head", PrimitiveType.Sphere, new Vector3(0, 0, 0.8f),
            Quaternion.identity, new Vector3(0.5f, 0.35f, 0.55f), skinMat);

        // 6-8 body segments (elongated spheres in a line)
        for (int i = 0; i < 7; i++)
        {
            float z = 0.5f - i * 0.28f;
            float segScale = Mathf.Lerp(0.4f, 0.18f, (float)i / 6f); // taper toward tail
            // Alternating slight color pattern
            Material segMat = (i % 2 == 0) ? skinMat : bellyMat;
            AddPrimChild(root, $"Seg{i}", PrimitiveType.Sphere, new Vector3(0, 0, z),
                Quaternion.identity, new Vector3(segScale, segScale * 0.8f, 0.3f), segMat);
        }

        // Googly eyes on head
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.15f;
            AddPrimChild(root, side < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                new Vector3(x, 0.2f, 0.9f), Quaternion.identity,
                new Vector3(0.18f, 0.18f, 0.18f), whiteMat);
            AddPrimChild(root, side < 0 ? "PupilL" : "PupilR", PrimitiveType.Sphere,
                new Vector3(x, 0.2f, 1.0f), Quaternion.identity,
                new Vector3(0.09f, 0.11f, 0.06f), pupilMat);
        }

        // Forked tongue
        AddPrimChild(root, "Tongue", PrimitiveType.Capsule, new Vector3(0, 0, 1.1f),
            Quaternion.Euler(90, 0, 0), new Vector3(0.04f, 0.12f, 0.03f), tongueMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.9f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerSnakeBehavior>();
        root.transform.localScale = Vector3.one * 0.6f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Snake with 7 body segments and forked tongue!");
        return prefab;
    }

    // ===== TP MUMMY OBSTACLE =====
    static GameObject CreateTPMummyPrefab()
    {
        string prefabPath = "Assets/Prefabs/TPMummy.prefab";
        GameObject root = new GameObject("TPMummy");

        Material wrapMat = MakeURPMat("Mummy_Wrap", new Color(0.9f, 0.88f, 0.8f), 0f, 0.3f);
        Material dirtMat = MakeURPMat("Mummy_Dirty", new Color(0.7f, 0.65f, 0.5f), 0f, 0.25f);
        Material whiteMat = MakeURPMat("Mummy_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Mummy_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);

        // Cylinder core body
        AddPrimChild(root, "Body", PrimitiveType.Cylinder, Vector3.zero,
            Quaternion.identity, new Vector3(0.6f, 0.8f, 0.6f), wrapMat);

        // Head (sphere on top)
        AddPrimChild(root, "Head", PrimitiveType.Sphere, new Vector3(0, 0.75f, 0),
            Quaternion.identity, new Vector3(0.55f, 0.5f, 0.5f), wrapMat);

        // Paper strips hanging off (flattened cubes)
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * 0.35f;
            float z = Mathf.Sin(angle) * 0.35f;
            float y = Random.Range(-0.3f, 0.5f);
            Material stripMat = (i % 3 == 0) ? dirtMat : wrapMat;
            AddPrimChild(root, $"Strip{i}", PrimitiveType.Cube,
                new Vector3(x, y, z),
                Quaternion.Euler(Random.Range(-10f, 10f), i * 45f, Random.Range(-15f, 15f)),
                new Vector3(0.08f, 0.35f, 0.25f), stripMat);
        }

        // Arms (hidden under wraps, extend on react)
        GameObject armGroup = new GameObject("Arm");
        armGroup.transform.SetParent(root.transform);
        armGroup.transform.localPosition = Vector3.zero;
        AddPrimChild(armGroup, "ArmL", PrimitiveType.Capsule, new Vector3(-0.4f, 0.2f, 0),
            Quaternion.Euler(0, 0, 30), new Vector3(0.12f, 0.3f, 0.12f), wrapMat);
        AddPrimChild(armGroup, "ArmR", PrimitiveType.Capsule, new Vector3(0.4f, 0.2f, 0),
            Quaternion.Euler(0, 0, -30), new Vector3(0.12f, 0.3f, 0.12f), wrapMat);

        // Googly eyes peeking through wraps
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.12f;
            AddPrimChild(root, side < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                new Vector3(x, 0.85f, 0.2f), Quaternion.identity,
                new Vector3(0.2f, 0.2f, 0.2f), whiteMat);
            AddPrimChild(root, side < 0 ? "PupilL" : "PupilR", PrimitiveType.Sphere,
                new Vector3(x, 0.85f, 0.3f), Quaternion.identity,
                new Vector3(0.1f, 0.12f, 0.06f), pupilMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.7f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<ToiletPaperMummyBehavior>();
        root.transform.localScale = Vector3.one * 0.6f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created TP Mummy with paper strips and peeking eyes!");
        return prefab;
    }

    // ===== GREASE GLOB OBSTACLE =====
    static GameObject CreateGreaseGlobPrefab()
    {
        string prefabPath = "Assets/Prefabs/GreaseGlob.prefab";
        GameObject root = new GameObject("GreaseGlob");

        Material globMat = MakeURPMat("Glob_Body", new Color(0.55f, 0.45f, 0.15f), 0.1f, 0.85f);
        globMat.EnableKeyword("_EMISSION");
        globMat.SetColor("_EmissionColor", new Color(0.15f, 0.12f, 0.03f));
        EditorUtility.SetDirty(globMat);
        Material dripMat = MakeURPMat("Glob_Drip", new Color(0.5f, 0.4f, 0.1f), 0.05f, 0.9f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", new Color(0.1f, 0.08f, 0.02f));
        EditorUtility.SetDirty(dripMat);
        Material whiteMat = MakeURPMat("Glob_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Glob_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);

        // Main body sphere (irregular by using slightly non-uniform scale)
        AddPrimChild(root, "Body", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(1.2f, 1.0f, 1.1f), globMat);

        // Drip spheres hanging off bottom
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * 0.3f;
            float z = Mathf.Sin(angle) * 0.3f;
            float y = -0.4f - Random.Range(0f, 0.2f);
            float scale = Random.Range(0.12f, 0.22f);
            AddPrimChild(root, $"Drip{i}", PrimitiveType.Sphere,
                new Vector3(x, y, z), Quaternion.identity,
                new Vector3(scale, scale * 1.5f, scale), dripMat);
        }

        // Smaller blob lumps on surface
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad + 0.5f;
            float x = Mathf.Cos(angle) * 0.45f;
            float z = Mathf.Sin(angle) * 0.4f;
            float y = Random.Range(0.1f, 0.35f);
            AddPrimChild(root, $"Lump{i}", PrimitiveType.Sphere,
                new Vector3(x, y, z), Quaternion.identity,
                new Vector3(0.25f, 0.2f, 0.25f), globMat);
        }

        // Googly eyes
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.22f;
            AddPrimChild(root, side < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                new Vector3(x, 0.35f, 0.4f), Quaternion.identity,
                new Vector3(0.22f, 0.22f, 0.22f), whiteMat);
            AddPrimChild(root, side < 0 ? "PupilL" : "PupilR", PrimitiveType.Sphere,
                new Vector3(x, 0.35f, 0.5f), Quaternion.identity,
                new Vector3(0.11f, 0.13f, 0.07f), pupilMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.7f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<GreaseGlobBehavior>();
        root.transform.localScale = Vector3.one * 0.55f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Grease Glob with drip spheres and iridescent sheen!");
        return prefab;
    }

    // ===== POOP FLY SWARM OBSTACLE =====
    static GameObject CreatePoopFlySwarmPrefab()
    {
        string prefabPath = "Assets/Prefabs/PoopFlySwarm.prefab";
        GameObject root = new GameObject("PoopFlySwarm");

        Material flyMat = MakeURPMat("Fly_Body", new Color(0.08f, 0.08f, 0.06f), 0.1f, 0.5f);
        Material wingMat = MakeURPMat("Fly_Wing", new Color(0.5f, 0.5f, 0.55f, 0.6f), 0f, 0.7f);
        wingMat.EnableKeyword("_EMISSION");
        wingMat.SetColor("_EmissionColor", new Color(0.15f, 0.15f, 0.2f));
        EditorUtility.SetDirty(wingMat);
        Material whiteMat = MakeURPMat("Fly_EyeW", new Color(0.9f, 0.1f, 0.05f), 0f, 0.85f); // red compound eyes
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.02f));
        EditorUtility.SetDirty(whiteMat);

        // 8 flies orbiting around center
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * 0.4f;
            float z = Mathf.Sin(angle) * 0.4f;
            float y = Random.Range(-0.2f, 0.2f);

            // Elongated body
            AddPrimChild(root, $"Fly{i}", PrimitiveType.Sphere,
                new Vector3(x, y, z), Quaternion.identity,
                new Vector3(0.08f, 0.06f, 0.14f), flyMat);

            // Wings (tiny cubes)
            AddPrimChild(root, $"Fly{i}_WingL", PrimitiveType.Cube,
                new Vector3(x - 0.04f, y + 0.03f, z),
                Quaternion.Euler(0, 0, -20f),
                new Vector3(0.07f, 0.01f, 0.04f), wingMat);
            AddPrimChild(root, $"Fly{i}_WingR", PrimitiveType.Cube,
                new Vector3(x + 0.04f, y + 0.03f, z),
                Quaternion.Euler(0, 0, 20f),
                new Vector3(0.07f, 0.01f, 0.04f), wingMat);

            // Compound eyes (tiny red spheres)
            AddPrimChild(root, $"Fly{i}_Eye", PrimitiveType.Sphere,
                new Vector3(x, y + 0.02f, z + 0.06f), Quaternion.identity,
                new Vector3(0.04f, 0.04f, 0.04f), whiteMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.8f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<PoopFlySwarmBehavior>();
        root.transform.localScale = Vector3.one * 1.2f; // swarm is bigger overall

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Poop Fly Swarm with 8 buzzing flies!");
        return prefab;
    }

    // ===== TOXIC FROG OBSTACLE =====
    /// <summary>
    /// Fat toxic frog - squat body, big googly eyes, throat pouch, 4 legs, hidden tongue.
    /// Hops periodically and lashes tongue on player hit.
    /// </summary>
    static GameObject CreateToxicFrogPrefab()
    {
        string prefabPath = "Assets/Prefabs/ToxicFrog.prefab";
        GameObject root = new GameObject("ToxicFrog");

        // Materials
        Material skinMat = MakeURPMat("Frog_Skin", new Color(0.15f, 0.5f, 0.08f), 0.05f, 0.65f);
        skinMat.EnableKeyword("_EMISSION");
        skinMat.SetColor("_EmissionColor", new Color(0.03f, 0.15f, 0.02f));
        EditorUtility.SetDirty(skinMat);
        Material bellyMat = MakeURPMat("Frog_Belly", new Color(0.55f, 0.7f, 0.25f), 0f, 0.5f);
        Material throatMat = MakeURPMat("Frog_Throat", new Color(0.7f, 0.4f, 0.15f), 0f, 0.6f);
        throatMat.EnableKeyword("_EMISSION");
        throatMat.SetColor("_EmissionColor", new Color(0.2f, 0.1f, 0.02f));
        EditorUtility.SetDirty(throatMat);
        Material tongueMat = MakeURPMat("Frog_Tongue", new Color(0.85f, 0.3f, 0.35f), 0f, 0.7f);
        Material wartMat = MakeURPMat("Frog_Wart", new Color(0.2f, 0.55f, 0.12f), 0.1f, 0.5f);
        Material whiteMat = MakeURPMat("Frog_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Frog_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material legMat = MakeURPMat("Frog_Leg", new Color(0.12f, 0.42f, 0.06f), 0.05f, 0.55f);

        // Squat wide body
        AddPrimChild(root, "Body", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(1.8f, 1.0f, 1.6f), skinMat);

        // Lighter belly
        AddPrimChild(root, "Belly", PrimitiveType.Sphere, new Vector3(0, -0.15f, 0.1f),
            Quaternion.identity, new Vector3(1.5f, 0.7f, 1.3f), bellyMat);

        // Throat pouch (inflates during idle)
        AddPrimChild(root, "Throat", PrimitiveType.Sphere, new Vector3(0, -0.2f, 0.65f),
            Quaternion.identity, new Vector3(0.7f, 0.5f, 0.5f), throatMat);

        // Tongue (hidden by default, extends on hit)
        AddPrimChild(root, "Tongue", PrimitiveType.Capsule, new Vector3(0, -0.1f, 0.8f),
            Quaternion.Euler(90, 0, 0), new Vector3(0.12f, 0.4f, 0.08f), tongueMat);

        // Big googly eyes on top (frog style, bulging up)
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.35f;
            AddPrimChild(root, side < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                new Vector3(x, 0.55f, 0.35f), Quaternion.identity,
                new Vector3(0.45f, 0.45f, 0.45f), whiteMat);
            AddPrimChild(root, side < 0 ? "PupilL" : "PupilR", PrimitiveType.Sphere,
                new Vector3(x, 0.55f, 0.55f), Quaternion.identity,
                new Vector3(0.22f, 0.26f, 0.15f), pupilMat);
        }

        // 4 legs (front pair shorter, rear pair longer for hopping)
        float[] legX = { -0.7f, 0.7f, -0.75f, 0.75f };
        float[] legZ = { 0.3f, 0.3f, -0.4f, -0.4f };
        float[] legLen = { 0.25f, 0.25f, 0.35f, 0.35f };
        string[] legNames = { "LegFL", "LegFR", "LegRL", "LegRR" };
        for (int i = 0; i < 4; i++)
        {
            // Upper leg
            AddPrimChild(root, legNames[i], PrimitiveType.Capsule,
                new Vector3(legX[i], -0.15f, legZ[i]),
                Quaternion.Euler(0, 0, legX[i] > 0 ? 35 : -35),
                new Vector3(0.18f, legLen[i], 0.14f), legMat);
            // Foot (webbed paddle)
            AddPrimChild(root, legNames[i] + "_Foot", PrimitiveType.Sphere,
                new Vector3(legX[i] * 1.3f, -0.4f, legZ[i] + 0.05f),
                Quaternion.identity, new Vector3(0.2f, 0.06f, 0.25f), legMat);
        }

        // Warts (bumpy texture dots)
        for (int w = 0; w < 8; w++)
        {
            float angle = w * 45f * Mathf.Deg2Rad;
            float wx = Mathf.Cos(angle) * 0.6f;
            float wz = Mathf.Sin(angle) * 0.5f;
            float wy = Random.Range(0.1f, 0.4f);
            AddPrimChild(root, $"Wart{w}", PrimitiveType.Sphere,
                new Vector3(wx, wy, wz), Quaternion.identity,
                new Vector3(0.08f, 0.06f, 0.08f), wartMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.85f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<ToxicFrogBehavior>();
        root.transform.localScale = Vector3.one * 0.6f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Toxic Frog with throat pouch, legs, tongue, and warts!");
        return prefab;
    }

    // ===== SEWER JELLYFISH OBSTACLE =====
    /// <summary>
    /// Translucent sewer jellyfish - pulsing bell dome, trailing tentacles, bioluminescent glow.
    /// Drifts gently and stings on contact.
    /// </summary>
    static GameObject CreateSewerJellyfishPrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerJellyfish.prefab";
        GameObject root = new GameObject("SewerJellyfish");

        // Materials - translucent / glowy
        Material bellMat = MakeURPMat("Jelly_Bell", new Color(0.2f, 0.6f, 0.5f, 0.7f), 0f, 0.9f);
        bellMat.EnableKeyword("_EMISSION");
        bellMat.SetColor("_EmissionColor", new Color(0.08f, 0.5f, 0.35f) * 2f);
        EditorUtility.SetDirty(bellMat);
        Material innerMat = MakeURPMat("Jelly_Inner", new Color(0.3f, 0.8f, 0.6f), 0f, 0.95f);
        innerMat.EnableKeyword("_EMISSION");
        innerMat.SetColor("_EmissionColor", new Color(0.15f, 0.7f, 0.5f) * 3f);
        EditorUtility.SetDirty(innerMat);
        Material tentMat = MakeURPMat("Jelly_Tentacle", new Color(0.25f, 0.55f, 0.45f, 0.6f), 0f, 0.8f);
        tentMat.EnableKeyword("_EMISSION");
        tentMat.SetColor("_EmissionColor", new Color(0.05f, 0.3f, 0.2f) * 1.5f);
        EditorUtility.SetDirty(tentMat);
        Material whiteMat = MakeURPMat("Jelly_EyeW", Color.white, 0f, 0.9f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("Jelly_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material spotMat = MakeURPMat("Jelly_Spot", new Color(0.35f, 0.75f, 0.6f), 0f, 0.85f);
        spotMat.EnableKeyword("_EMISSION");
        spotMat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.3f) * 2f);
        EditorUtility.SetDirty(spotMat);

        // Bell dome (wider than tall, mushroom cap shape)
        AddPrimChild(root, "Bell", PrimitiveType.Sphere, new Vector3(0, 0.3f, 0),
            Quaternion.identity, new Vector3(1.8f, 1.2f, 1.8f), bellMat);

        // Inner glow core
        AddPrimChild(root, "InnerCore", PrimitiveType.Sphere, new Vector3(0, 0.2f, 0),
            Quaternion.identity, new Vector3(1.0f, 0.7f, 1.0f), innerMat);

        // Glowing spots on bell surface
        for (int s = 0; s < 6; s++)
        {
            float angle = s * 60f * Mathf.Deg2Rad;
            float sx = Mathf.Cos(angle) * 0.6f;
            float sz = Mathf.Sin(angle) * 0.6f;
            AddPrimChild(root, $"Spot{s}", PrimitiveType.Sphere,
                new Vector3(sx, 0.45f + Random.Range(-0.1f, 0.1f), sz),
                Quaternion.identity, new Vector3(0.12f, 0.08f, 0.12f), spotMat);
        }

        // Tiny googly eyes underneath the bell
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.25f;
            AddPrimChild(root, side < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                new Vector3(x, -0.05f, 0.3f), Quaternion.identity,
                new Vector3(0.2f, 0.2f, 0.2f), whiteMat);
            AddPrimChild(root, side < 0 ? "PupilL" : "PupilR", PrimitiveType.Sphere,
                new Vector3(x, -0.05f, 0.4f), Quaternion.identity,
                new Vector3(0.1f, 0.12f, 0.06f), pupilMat);
        }

        // 8 trailing tentacles
        for (int t = 0; t < 8; t++)
        {
            float angle = t * 45f * Mathf.Deg2Rad;
            float tx = Mathf.Cos(angle) * 0.45f;
            float tz = Mathf.Sin(angle) * 0.45f;
            float tentLen = Random.Range(0.5f, 0.8f);
            AddPrimChild(root, $"Tentacle{t}", PrimitiveType.Capsule,
                new Vector3(tx, -0.5f - t * 0.03f, tz),
                Quaternion.Euler(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)),
                new Vector3(0.06f, tentLen, 0.06f), tentMat);
        }

        // Frilly rim around bell edge
        for (int f = 0; f < 12; f++)
        {
            float angle = f * 30f * Mathf.Deg2Rad;
            float fx = Mathf.Cos(angle) * 0.8f;
            float fz = Mathf.Sin(angle) * 0.8f;
            AddPrimChild(root, $"Frill{f}", PrimitiveType.Sphere,
                new Vector3(fx, -0.1f, fz), Quaternion.identity,
                new Vector3(0.15f, 0.08f, 0.1f), tentMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.9f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerJellyfishBehavior>();
        root.transform.localScale = Vector3.one * 0.7f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Jellyfish with pulsing bell, tentacles, and bioluminescent glow!");
        return prefab;
    }

    /// <summary>
    /// Helper: creates a primitive child, removes its collider, applies material.
    /// </summary>
    static GameObject AddPrimChild(GameObject parent, string name, PrimitiveType type,
        Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        obj.transform.localScale = localScale;
        obj.GetComponent<Renderer>().material = mat;
        Collider c = obj.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        return obj;
    }

    static bool _lastModelWasGLB = false;

    static GameObject LoadModel(string path)
    {
        _lastModelWasGLB = false;
        // Prefer GLB format over FBX - GLB embeds textures, FBX often loses them
        string glbPath = path.Replace(".fbx", ".glb").Replace(".FBX", ".glb");
        if (glbPath != path)
        {
            GameObject glbGo = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (glbGo != null)
            {
                _lastModelWasGLB = true;
                Debug.Log($"TTR: Using GLB model (embedded textures): {glbPath}");
                return glbGo;
            }
        }

        // Try direct load
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go != null) return go;

        // Try loading all assets at path and finding the GameObject
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
        {
            foreach (Object asset in allAssets)
            {
                if (asset is GameObject g)
                    return g;
            }
        }

        // Try main asset
        Object main = AssetDatabase.LoadMainAssetAtPath(path);
        if (main is GameObject mg) return mg;

        return null;
    }

    static GameObject EnsureModelPrefab(string name, string modelPath, Vector3 scale,
        PrimitiveType fallbackShape, Color fallbackColor, bool isObstacle)
    {
        string prefabPath = $"Assets/Prefabs/{name}.prefab";

        GameObject modelAsset = LoadModel(modelPath);
        GameObject obj;

        if (modelAsset != null)
        {
            obj = (GameObject)Object.Instantiate(modelAsset);
            obj.name = name;
            obj.transform.localScale = scale;

            // Upgrade all materials to URP Lit
            UpgradeToURP(obj);

            // Add collider that wraps the model
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // Auto-size from renderers
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            box.center = obj.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size / Mathf.Max(scale.x, scale.y, scale.z);

            Debug.Log($"TTR: Loaded {name} from Blender model ({modelPath})");
        }
        else
        {
            // Log what assets ARE available at that path for debugging
            string[] allPaths = AssetDatabase.GetAllAssetPaths();
            string matches = "";
            foreach (string p in allPaths)
                if (p.Contains("Models/") && p.Contains(name))
                    matches += p + "; ";

            obj = GameObject.CreatePrimitive(fallbackShape);
            obj.name = name;
            obj.transform.localScale = scale;

            Collider col = obj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Renderer rend = obj.GetComponent<Renderer>();
            Material mat = MakeURPMat($"{name}_Mat", fallbackColor, 0.1f, 0.3f);
            // Slight emission so obstacles are visible in dark pipe
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", fallbackColor * 0.3f);
            EditorUtility.SetDirty(mat);
            rend.material = mat;

            Debug.LogWarning($"TTR: {modelPath} not found for {name}. Found paths: [{matches}]");
        }

        if (isObstacle)
        {
            obj.AddComponent<Obstacle>();
            obj.tag = "Obstacle";
        }
        else
        {
            obj.AddComponent<Collectible>();
            obj.tag = "Collectible";
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        Object.DestroyImmediate(obj);
        return prefab;
    }

    // ===== MATERIALS =====
    static int _matCounter = 0;

    static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    /// <summary>
    /// Saves a material as an asset on disk so it survives prefab instantiation.
    /// </summary>
    static Material SaveMaterial(Material m)
    {
        // If the material is already a saved asset, just return it as-is
        if (AssetDatabase.Contains(m))
            return m;

        EnsureMaterialsFolder();
        string safeName = m.name.Replace(" ", "_").Replace("/", "_");
        string path = $"Assets/Materials/{safeName}_{_matCounter++}.mat";

        // If asset already exists at this path, delete it first to avoid CreateAsset failure
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    /// <summary>
    /// Replaces ALL materials with saved-to-disk URP Lit materials.
    /// Materials are saved as assets so they persist when prefabs are instantiated at runtime.
    /// Preserves textures (critical for corn kernels on Mr. Corny's body).
    /// </summary>
    static void UpgradeToURP(GameObject obj)
    {
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            Material[] newMats = new Material[mats.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                Material old = mats[i];
                Color baseColor = new Color(0.5f, 0.5f, 0.5f);
                float metallic = 0f;
                float smoothness = 0.3f;
                Texture mainTex = null;
                Texture normalMap = null;
                Texture metallicMap = null;
                Texture occlusionMap = null;
                string matName = "Default_URP";

                if (old != null)
                {
                    matName = old.name + "_URP";
                    try
                    {
                        if (old.HasProperty("_BaseColor")) baseColor = old.GetColor("_BaseColor");
                        else if (old.HasProperty("_Color")) baseColor = old.GetColor("_Color");
                    } catch { }
                    try
                    {
                        if (old.HasProperty("_Metallic")) metallic = old.GetFloat("_Metallic");
                        if (old.HasProperty("_Glossiness")) smoothness = old.GetFloat("_Glossiness");
                        else if (old.HasProperty("_Smoothness")) smoothness = old.GetFloat("_Smoothness");
                    } catch { }
                    // Try ALL common texture property names to preserve textures
                    try
                    {
                        if (old.HasProperty("_BaseMap")) mainTex = old.GetTexture("_BaseMap");
                        if (mainTex == null && old.HasProperty("_MainTex")) mainTex = old.GetTexture("_MainTex");
                        if (mainTex == null && old.HasProperty("_ColorMap")) mainTex = old.GetTexture("_ColorMap");
                        if (mainTex == null && old.HasProperty("_Albedo")) mainTex = old.GetTexture("_Albedo");
                        // Universal fallback: scan ALL texture properties (catches GLB/glTF imports)
                        if (mainTex == null)
                        {
                            string[] texProps = old.GetTexturePropertyNames();
                            foreach (string prop in texProps)
                            {
                                Texture t = old.GetTexture(prop);
                                if (t != null && t is Texture2D)
                                {
                                    // Skip normal/bump maps - we want the albedo/diffuse
                                    string lp = prop.ToLower();
                                    if (lp.Contains("normal") || lp.Contains("bump")) continue;
                                    mainTex = t;
                                    Debug.Log($"TTR: Found texture '{t.name}' via fallback property '{prop}' on '{matName}'");
                                    break;
                                }
                            }
                        }
                    } catch { }
                    try
                    {
                        if (old.HasProperty("_BumpMap")) normalMap = old.GetTexture("_BumpMap");
                        if (normalMap == null && old.HasProperty("_NormalMap")) normalMap = old.GetTexture("_NormalMap");
                    } catch { }
                    try
                    {
                        if (old.HasProperty("_MetallicGlossMap")) metallicMap = old.GetTexture("_MetallicGlossMap");
                    } catch { }
                    try
                    {
                        if (old.HasProperty("_OcclusionMap")) occlusionMap = old.GetTexture("_OcclusionMap");
                    } catch { }
                }

                // Brighten dark colors more aggressively for cartoonish look
                float brightness = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
                if (brightness < 0.2f)
                    baseColor = Color.Lerp(baseColor, baseColor * 3.5f, 0.5f);
                // Boost saturation slightly for cartoon feel
                float h, s, v;
                Color.RGBToHSV(baseColor, out h, out s, out v);
                s = Mathf.Min(s * 1.2f, 1f);
                v = Mathf.Max(v, 0.25f); // minimum brightness
                baseColor = Color.HSVToRGB(h, s, v);
                baseColor.a = 1f;

                Material m = new Material(_urpLit);
                m.name = matName;
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                if (mainTex != null) m.SetTexture("_BaseMap", mainTex);
                if (normalMap != null) { m.SetTexture("_BumpMap", normalMap); m.EnableKeyword("_NORMALMAP"); }
                if (metallicMap != null) m.SetTexture("_MetallicGlossMap", metallicMap);
                if (occlusionMap != null) m.SetTexture("_OcclusionMap", occlusionMap);

                // Toon shader shadow color
                if (_toonLit != null && m.HasProperty("_ShadowColor"))
                {
                    float ch, cs, cv;
                    Color.RGBToHSV(baseColor, out ch, out cs, out cv);
                    m.SetColor("_ShadowColor", Color.HSVToRGB(ch, Mathf.Min(cs * 1.2f, 1f), cv * 0.35f));
                }

                if (mainTex != null)
                    Debug.Log($"TTR: Preserved texture '{mainTex.name}' on material '{matName}'");

                // SAVE TO DISK so it survives prefab instantiation
                newMats[i] = SaveMaterial(m);
            }

            r.sharedMaterials = newMats;
        }
    }

    static Material MakeURPMat(string name, Color color, float metallic, float smoothness)
    {
        Material mat = new Material(_urpLit);
        mat.name = name;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        // Toon shader shadow color: auto-computed darker desaturated version
        if (_toonLit != null && mat.HasProperty("_ShadowColor"))
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            // Shadow = darker, slightly more saturated, hue-shifted toward cool
            Color shadowCol = Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.35f);
            mat.SetColor("_ShadowColor", shadowCol);
        }

        return SaveMaterial(mat);
    }

    // ===== OUTLINE RENDERER FEATURE =====
    static void SetupOutlineRendererFeature()
    {
        try
        {
            // Load the outline shader and create a material for it
            Shader outlineShader = Shader.Find("Hidden/OutlineEdgeDetect");
            if (outlineShader == null)
            {
                Debug.LogWarning("TTR: OutlineEdgeDetect shader not found - outlines disabled");
                return;
            }

            Material outlineMat = new Material(outlineShader);
            outlineMat.name = "OutlineEdgeDetect";
            outlineMat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 1f)); // pure black ink
            outlineMat.SetFloat("_OutlineThickness", 2.5f); // thick comic lines
            outlineMat.SetFloat("_DepthThreshold", 1.5f);
            outlineMat.SetFloat("_NormalThreshold", 0.4f);
            EnsureMaterialsFolder();
            if (!AssetDatabase.Contains(outlineMat))
            {
                string outlinePath = $"Assets/Materials/OutlineEdgeDetect_{_matCounter++}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(outlinePath) != null)
                    AssetDatabase.DeleteAsset(outlinePath);
                AssetDatabase.CreateAsset(outlineMat, outlinePath);
                outlineMat = AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
            }

            // Find the URP renderer data asset and add the outline feature
            string[] rendererGuids = AssetDatabase.FindAssets("PC_Renderer t:ScriptableObject");
            if (rendererGuids.Length == 0)
            {
                // Try loading directly
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PC_Renderer.asset");
                if (rendererData != null)
                    AddOutlineFeatureToRenderer(rendererData, outlineMat);
                else
                    Debug.LogWarning("TTR: Could not find PC_Renderer asset for outline feature");
                return;
            }

            foreach (string guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("PC_Renderer"))
                {
                    var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (rendererData != null)
                        AddOutlineFeatureToRenderer(rendererData, outlineMat);
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"TTR: Outline feature setup failed (non-fatal): {e.Message}");
        }
    }

    static void AddOutlineFeatureToRenderer(ScriptableObject rendererData, Material outlineMat)
    {
        // Use reflection to add the renderer feature to the URP renderer
        // This avoids hard dependency on URP assembly internals
        var rdType = rendererData.GetType();
        var featuresField = rdType.GetField("m_RendererFeatures",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (featuresField == null)
        {
            Debug.LogWarning("TTR: Cannot access renderer features field via reflection");
            return;
        }

        var featuresList = featuresField.GetValue(rendererData) as System.Collections.IList;
        if (featuresList == null) return;

        // Check if outline feature already exists
        foreach (var f in featuresList)
        {
            if (f != null && f.GetType().Name == "OutlineRendererFeature")
            {
                Debug.Log("TTR: Outline renderer feature already exists, updating material");
                // Update the material reference
                var settingsField = f.GetType().GetField("settings");
                if (settingsField != null)
                {
                    var settings = settingsField.GetValue(f);
                    var matField = settings.GetType().GetField("outlineMaterial");
                    if (matField != null) matField.SetValue(settings, outlineMat);
                }
                EditorUtility.SetDirty(rendererData);
                return;
            }
        }

        // Create new OutlineRendererFeature instance
        var feature = ScriptableObject.CreateInstance<OutlineRendererFeature>();
        feature.name = "OutlineRendererFeature";
        feature.settings.outlineMaterial = outlineMat;
        feature.settings.thickness = 2.5f;
        feature.settings.outlineColor = Color.black;
        feature.settings.depthThreshold = 1.5f;
        feature.settings.normalThreshold = 0.4f;

        // Add as sub-asset of the renderer data
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        featuresList.Add(feature);
        featuresField.SetValue(rendererData, featuresList);

        // Also update the feature map
        var mapField = rdType.GetField("m_RendererFeatureMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (mapField != null)
        {
            // The map is a long, just set any value - Unity will recalculate
            // Actually we need to trigger Create() on the renderer data
        }

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        Debug.Log("TTR: Added OutlineRendererFeature to URP renderer!");
    }

    // ===== PIPE ZONE SYSTEM =====
    static void CreatePipeZoneSystem()
    {
        GameObject obj = new GameObject("PipeZoneSystem");
        obj.AddComponent<PipeZoneSystem>();
        // PipeZoneSystem finds TurdController and Light automatically in Start()
        Debug.Log("TTR: Created Pipe Zone System (5 zones: Porcelain → Hellsewer)!");
    }

    // ===== SCORE POPUP SYSTEM =====
    static void CreateScorePopup()
    {
        // ScorePopup creates its own overlay canvas internally
        GameObject obj = new GameObject("ScorePopup");
        obj.AddComponent<ScorePopup>();

        Debug.Log("TTR: Created Score Popup system (floating text for coins, tricks, stomps, milestones)!");
    }

    // ===== FLUSH SEQUENCE =====
    static void CreateFlushSequence()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Main FlushSequence component on a new root
        GameObject obj = new GameObject("FlushSequence");
        FlushSequence flush = obj.AddComponent<FlushSequence>();

        // Countdown text - big center of screen
        Text countdownText = MakeText(canvas.transform, "FlushCountdown", "",
            120, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 200), true);
        countdownText.gameObject.SetActive(false);
        flush.countdownText = countdownText;

        // Swirling water overlay (fills screen, starts invisible)
        GameObject whirlObj = new GameObject("WhirlOverlay");
        whirlObj.transform.SetParent(canvas.transform, false);
        RectTransform whirlRt = whirlObj.AddComponent<RectTransform>();
        whirlRt.anchorMin = Vector2.zero;
        whirlRt.anchorMax = Vector2.one;
        whirlRt.offsetMin = Vector2.zero;
        whirlRt.offsetMax = Vector2.zero;
        Image whirlImg = whirlObj.AddComponent<Image>();
        whirlImg.color = new Color(0.15f, 0.25f, 0.1f, 0f); // transparent green-brown
        whirlObj.SetActive(false);
        flush.whirlOverlay = whirlImg;

        // Vignette overlay for dramatic effect
        GameObject vigObj = new GameObject("FlushVignette");
        vigObj.transform.SetParent(canvas.transform, false);
        RectTransform vigRt = vigObj.AddComponent<RectTransform>();
        vigRt.anchorMin = Vector2.zero;
        vigRt.anchorMax = Vector2.one;
        vigRt.offsetMin = Vector2.zero;
        vigRt.offsetMax = Vector2.zero;
        Image vigImg = vigObj.AddComponent<Image>();
        vigImg.color = new Color(0, 0, 0, 0f);
        vigObj.SetActive(false);
        flush.vignetteOverlay = vigImg;

        Debug.Log("TTR: Created Flush Sequence (3-2-1-FLUSH! intro)!");
    }

    // ===== ASSET GALLERY =====
    /// <summary>Creates a short sewer pipe segment as optional gallery background.</summary>
    static GameObject CreateGallerySewerBackground(Vector3 center)
    {
        GameObject bg = new GameObject("GallerySewerBG");
        bg.transform.position = center;

        Material pipeMat = MakeURPMat("GalleryPipe", new Color(0.35f, 0.35f, 0.3f), 0.1f, 0.3f);
        Material waterMat = MakeURPMat("GalleryWater", new Color(0.2f, 0.35f, 0.15f, 0.7f), 0f, 0.8f);
        waterMat.SetFloat("_Surface", 1f);
        waterMat.SetFloat("_Blend", 0f);
        waterMat.SetFloat("_SrcBlend", 5f);
        waterMat.SetFloat("_DstBlend", 10f);
        waterMat.SetFloat("_ZWrite", 0f);
        waterMat.SetOverrideTag("RenderType", "Transparent");
        waterMat.renderQueue = 3000;

        // Half-pipe curved floor
        float radius = 5f;
        int segments = 24;
        float length = 12f;
        Mesh pipeMesh = new Mesh();
        Vector3[] verts = new Vector3[(segments + 1) * 2];
        int[] tris = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.PI * 0.2f + t * Mathf.PI * 1.6f; // 288 degree arc
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            verts[i * 2] = new Vector3(x, y, -length / 2);
            verts[i * 2 + 1] = new Vector3(x, y, length / 2);
        }
        for (int i = 0; i < segments; i++)
        {
            int bi = i * 6;
            int vi = i * 2;
            tris[bi] = vi; tris[bi + 1] = vi + 2; tris[bi + 2] = vi + 1;
            tris[bi + 3] = vi + 1; tris[bi + 4] = vi + 2; tris[bi + 5] = vi + 3;
        }
        pipeMesh.vertices = verts;
        pipeMesh.triangles = tris;
        pipeMesh.RecalculateNormals();

        GameObject pipe = new GameObject("Pipe");
        pipe.transform.SetParent(bg.transform);
        pipe.transform.localPosition = Vector3.zero;
        MeshFilter mfPipe = pipe.AddComponent<MeshFilter>();
        mfPipe.mesh = pipeMesh;
        MeshRenderer mrPipe = pipe.AddComponent<MeshRenderer>();
        mrPipe.material = pipeMat;

        // Water plane at bottom
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = "Water";
        water.transform.SetParent(bg.transform);
        water.transform.localPosition = new Vector3(0, -radius * 0.8f, 0);
        water.transform.localRotation = Quaternion.Euler(90, 0, 0);
        water.transform.localScale = new Vector3(radius * 1.5f, length, 1);
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.GetComponent<Renderer>().material = waterMat;

        // A couple of grime elements for atmosphere
        Material grimeMat = MakeURPMat("GalleryGrime", new Color(0.2f, 0.22f, 0.15f), 0f, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float angle = Random.Range(0.3f, 2.8f) * Mathf.PI;
            float z = Random.Range(-4f, 4f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * (radius - 0.1f),
                                      Mathf.Sin(angle) * (radius - 0.1f), z);
            GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stain.name = "Stain";
            stain.transform.SetParent(bg.transform);
            stain.transform.localPosition = pos;
            stain.transform.localRotation = Quaternion.LookRotation((-pos).normalized);
            stain.transform.localScale = Vector3.one * Random.Range(0.5f, 1.5f);
            Object.DestroyImmediate(stain.GetComponent<Collider>());
            stain.GetComponent<Renderer>().material = grimeMat;
        }

        return bg;
    }

    static void CreateAssetGallery(GameUI gameUI)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Stage position far from game world
        Vector3 stagePos = new Vector3(0, 100, 0);

        // Gallery root
        GameObject galleryRoot = new GameObject("AssetGallery");
        AssetGallery gallery = galleryRoot.AddComponent<AssetGallery>();

        // Stage center (where assets are spawned)
        GameObject stageObj = new GameObject("GalleryStage");
        stageObj.transform.position = stagePos;
        stageObj.transform.SetParent(galleryRoot.transform);
        gallery.stageCenter = stageObj.transform;

        // Gallery camera (looks at stage)
        GameObject camObj = new GameObject("GalleryCamera");
        camObj.transform.SetParent(galleryRoot.transform);
        camObj.transform.position = stagePos + new Vector3(0, 0.5f, -3.5f);
        camObj.transform.LookAt(stagePos + Vector3.up * 0.3f);
        Camera galleryCam = camObj.AddComponent<Camera>();
        galleryCam.clearFlags = CameraClearFlags.SolidColor;
        galleryCam.backgroundColor = new Color(0.06f, 0.08f, 0.05f);
        galleryCam.fieldOfView = 40f;
        galleryCam.nearClipPlane = 0.1f;
        galleryCam.farClipPlane = 50f;
        galleryCam.depth = 10; // above main camera
        galleryCam.enabled = false;
        gallery.galleryCamera = galleryCam;

        // Gallery lighting - 3-point setup for good model viewing
        // Key light (main, warm)
        GameObject lightObj = new GameObject("GalleryLight");
        lightObj.transform.SetParent(galleryRoot.transform);
        lightObj.transform.position = stagePos + new Vector3(-2, 4, -3);
        lightObj.transform.LookAt(stagePos);
        Light galleryLight = lightObj.AddComponent<Light>();
        galleryLight.type = LightType.Directional;
        galleryLight.intensity = 3.5f;
        galleryLight.color = new Color(1f, 0.97f, 0.92f);
        galleryLight.enabled = false;
        gallery.galleryLight = galleryLight;

        // Fill light (softer, cool, from the other side)
        GameObject fillObj = new GameObject("GalleryFillLight");
        fillObj.transform.SetParent(lightObj.transform); // child of key light so enable/disable propagates
        fillObj.transform.position = stagePos + new Vector3(3, 2, -2);
        fillObj.transform.LookAt(stagePos);
        Light fillLight = fillObj.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 1.5f;
        fillLight.color = new Color(0.8f, 0.85f, 1f);

        // Rim light (from behind, adds edge definition)
        GameObject rimObj = new GameObject("GalleryRimLight");
        rimObj.transform.SetParent(lightObj.transform);
        rimObj.transform.position = stagePos + new Vector3(0, 3, 4);
        rimObj.transform.LookAt(stagePos);
        Light rimLight = rimObj.AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.intensity = 2f;
        rimLight.color = new Color(1f, 0.95f, 0.85f);

        // Gallery UI Panel
        GameObject galleryPanel = MakePanel(canvas.transform, "GalleryPanel",
            new Color(0.04f, 0.06f, 0.03f, 0.85f),
            new Vector2(0f, 0f), new Vector2(1f, 1f));
        galleryPanel.SetActive(false);
        gallery.galleryPanel = galleryPanel;

        // Title bar
        MakeStretchText(galleryPanel.transform, "GalleryTitle", "ASSET GALLERY",
            48, TextAnchor.MiddleCenter, new Color(0.3f, 0.85f, 0.5f),
            new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.98f), true);

        // Asset name (big, center-top)
        Text nameText = MakeStretchText(galleryPanel.transform, "AssetName", "",
            38, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.9f), true);
        gallery.assetNameText = nameText;

        // Description (below name)
        Text descText = MakeStretchText(galleryPanel.transform, "AssetDesc", "",
            22, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.65f),
            new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.82f), false);
        gallery.assetDescText = descText;

        // Category label
        Text catText = MakeStretchText(galleryPanel.transform, "CategoryLabel", "All",
            26, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.1f), false);
        gallery.categoryText = catText;

        // Counter (1/N)
        Text counterText = MakeStretchText(galleryPanel.transform, "Counter", "",
            22, TextAnchor.MiddleCenter, new Color(0.5f, 0.6f, 0.45f),
            new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.16f), false);
        gallery.counterText = counterText;

        // Navigation buttons (wired at runtime in AssetGallery.Start - lambdas don't serialize)
        Button prevBtn = MakeButton(galleryPanel.transform, "PrevBtn", "<",
            42, new Color(0.3f, 0.3f, 0.25f), Color.white,
            new Vector2(0.02f, 0.4f), new Vector2(0.1f, 0.6f));
        gallery.prevButton = prevBtn;

        Button nextBtn = MakeButton(galleryPanel.transform, "NextBtn", ">",
            42, new Color(0.3f, 0.3f, 0.25f), Color.white,
            new Vector2(0.9f, 0.4f), new Vector2(0.98f, 0.6f));
        gallery.nextButton = nextBtn;

        Button closeBtn = MakeButton(galleryPanel.transform, "GalleryCloseBtn", "BACK",
            30, new Color(0.5f, 0.12f, 0.08f), Color.white,
            new Vector2(0.02f, 0.01f), new Vector2(0.2f, 0.07f));
        gallery.closeButton = closeBtn;

        // Background toggle button
        Button bgBtn = MakeButton(galleryPanel.transform, "BgToggleBtn", "BG: SEWER",
            18, new Color(0.25f, 0.35f, 0.25f), Color.white,
            new Vector2(0.78f, 0.01f), new Vector2(0.98f, 0.07f));
        gallery.bgToggleButton = bgBtn;

        // Create a sewer pipe segment as optional background
        GameObject sewerBg = CreateGallerySewerBackground(stageObj.transform.position);
        sewerBg.SetActive(false);
        gallery.sewerBackground = sewerBg;

        // Category filter buttons (wired at runtime via categoryNames array)
        string[] categories = { "All", "Characters", "Obstacles", "Power-Ups", "Scenery", "Collectibles", "Signs" };
        Color[] catColors = {
            new Color(0.3f, 0.3f, 0.3f), new Color(0.5f, 0.3f, 0.15f),
            new Color(0.5f, 0.15f, 0.1f), new Color(0.1f, 0.4f, 0.55f),
            new Color(0.25f, 0.45f, 0.2f), new Color(0.55f, 0.45f, 0.1f),
            new Color(0.45f, 0.35f, 0.15f)
        };
        List<Button> catBtns = new List<Button>();
        float catWidth = 0.9f / categories.Length;
        for (int i = 0; i < categories.Length; i++)
        {
            float x0 = 0.05f + i * catWidth;
            float x1 = x0 + catWidth - 0.005f;
            Button cb = MakeButton(galleryPanel.transform, "Cat_" + categories[i], categories[i],
                16, catColors[i], Color.white,
                new Vector2(x0, 0.94f), new Vector2(x1, 0.99f));
            catBtns.Add(cb);
        }
        gallery.categoryButtons = catBtns.ToArray();
        gallery.categoryNames = categories;

        // Register all game assets
        RegisterGalleryAssets(gallery);

        Debug.Log($"TTR: Created Asset Gallery with {gallery.galleryPanel != null} panel!");
    }

    static void RegisterGalleryAssets(AssetGallery gallery)
    {
        // === CHARACTERS ===
        // MrCorny gallery prefab (with face features, yellow corn, mouth, eyes)
        string mrCornyGallery = "Assets/Prefabs/MrCorny_Gallery.prefab";
        gallery.RegisterAsset("Mr. Corny", "The classic corn-studded turd. Hero of the sewers.",
            "Characters", mrCornyGallery, 0.17f);

        // Skin color variants (same model, different body color)
        var skins = new (string name, string desc, Color col)[] {
            ("Golden Corny", "Gilded glory - a turd of pure gold!", new Color(0.85f, 0.7f, 0.15f)),
            ("Toxic Corny", "Radioactive sludge given form.", new Color(0.2f, 0.85f, 0.15f)),
            ("Frozen Corny", "A glacial log from the arctic pipes.", new Color(0.6f, 0.85f, 0.95f)),
            ("Royal Corny", "Regal waste from the king's throne.", new Color(0.55f, 0.2f, 0.7f)),
            ("Lava Corny", "Forged in the bowels of a volcano.", new Color(0.15f, 0.02f, 0.02f)),
            ("Ghost Corny", "The phantom of the pipes.", new Color(0.9f, 0.92f, 0.95f)),
        };
        foreach (var s in skins)
            gallery.RegisterAsset(s.name, s.desc, "Characters", mrCornyGallery, 0.17f, s.col);

        // Smooth Snake (standalone AI racer)
        gallery.RegisterAsset("Smooth Snake", "The smuggest turd in the sewer. Half-lidded and unimpressed.",
            "Characters", "Assets/Prefabs/Racer_SmoothSnake_Gallery.prefab", 0.16f, new Color(0.25f, 0.15f, 0.08f));

        // AI Racers (with unique faces and expressions)
        var racers = new (string preset, string name, string desc, Color col)[] {
            ("SkidmarkSteve", "Skidmark Steve", "Angry racer with a snarl. Leaves marks everywhere.", new Color(0.7f, 0.35f, 0.1f)),
            ("PrincessPlop", "Princess Plop", "Glamorous turd with sparkly eyes and kissy lips.", new Color(0.85f, 0.5f, 0.75f)),
            ("TheLog", "The Log", "Stoic and unfazed. Nothing impresses this turd.", new Color(0.4f, 0.25f, 0.1f)),
            ("LilSquirt", "Lil' Squirt", "Wild googly eyes and a goofy grin. Unpredictable!", new Color(0.9f, 0.8f, 0.3f)),
        };
        foreach (var r in racers)
        {
            string racerPrefab = $"Assets/Prefabs/Racer_{r.preset}_Gallery.prefab";
            gallery.RegisterAsset(r.name, r.desc, "Characters", racerPrefab, 0.15f, r.col);
        }

        // === OBSTACLES ===
        var obstacles = new (string name, string desc, string path)[] {
            ("Sewer Rat", "Lurking rodent. Will pounce and stun you!", "Assets/Prefabs/SewerRat.prefab"),
            ("Toxic Barrel", "Corroded waste barrel. Glows menacingly.", "Assets/Prefabs/ToxicBarrel.prefab"),
            ("Poop Blob", "A slithering wet blob. Splatters on impact.", "Assets/Prefabs/PoopBlob.prefab"),
            ("Sewer Mine", "Explosive metal sphere. Red warning glow.", "Assets/Prefabs/SewerMine.prefab"),
            ("Cockroach", "Chitinous nightmare. Panics when hit.", "Assets/Prefabs/Cockroach.prefab"),
            ("Hair Wad (Black)", "Tangled hair blob with googly eyes.", "Assets/Prefabs/HairWad_Black.prefab"),
            ("Hair Wad (Blonde)", "Golden locks of clogged horror.", "Assets/Prefabs/HairWad_Blonde.prefab"),
            ("Hair Wad (Red)", "Fiery red hair monster.", "Assets/Prefabs/HairWad_Red.prefab"),
            ("Hair Wad (Brunette)", "Dark tangled mass blocking your path.", "Assets/Prefabs/HairWad_Brunette.prefab"),
        };
        foreach (var o in obstacles)
            gallery.RegisterAsset(o.name, o.desc, "Obstacles", o.path, 1f);

        // === COLLECTIBLES ===
        gallery.RegisterAsset("Fartcoin", "Brown Town's official currency. Spin big and shiny like Sonic rings!",
            "Collectibles", "Assets/Prefabs/CornCoin.prefab", 1f);

        // === POWER-UPS ===
        gallery.RegisterAsset("Speed Boost", "Cyan toilet seat that rockets you forward at 1.5x speed!",
            "Power-Ups", "Assets/Prefabs/SpeedBoost.prefab", 0.35f);
        gallery.RegisterAsset("Jump Ramp", "Orange launch ramp. Hit it to fly and do tricks!",
            "Power-Ups", "Assets/Prefabs/JumpRamp.prefab", 1f);

        // === SCENERY ===
        var scenery = new (string name, string desc, string path)[] {
            ("Valve Wheel", "Rusty pipe valve mounted on walls.", "Assets/Models/ValveWheel.glb"),
            ("Sewer Grate", "Metal drainage grate.", "Assets/Models/SewerGrate.glb"),
            ("Mushroom", "Toxic fungus. Bouncy!", "Assets/Models/Mushroom.glb"),
            ("Fish Bone", "Skeletal remains of a sewer fish.", "Assets/Models/FishBone.glb"),
            ("Manhole Cover", "Heavy iron cover from above.", "Assets/Models/ManholeCover.glb"),
            ("Toilet Seat", "Porcelain relic of the surface world.", "Assets/Models/ToiletSeat.glb"),
        };
        foreach (var s in scenery)
            gallery.RegisterAsset(s.name, s.desc, "Scenery", s.path, 0.4f);

        // === SIGNS & GRAFFITI ===
        // These are procedurally generated with _Gross_ prefix - register saved prefab variants
        var signDefs = new (string prefix, string label, string desc, int count)[] {
            ("GraffitiSign_Gross_", "Graffiti", "Sewer wall graffiti with crude messages.", 4),
            ("SewerAd_Gross_", "Sewer Ad", "Underground advertisement for dubious products.", 4),
            ("WarningSign_Gross_", "Warning Sign", "Warning sign about hazards ahead.", 3),
            ("PipeNumber_Gross_", "Pipe Number", "Pipe section number marker.", 3),
        };
        foreach (var sd in signDefs)
        {
            for (int i = 0; i < sd.count; i++)
            {
                string path = $"Assets/Prefabs/{sd.prefix}{i}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    gallery.RegisterAsset($"{sd.label} #{i+1}", sd.desc,
                        "Signs", path, 1f);
                }
            }
        }

        // Also add gross decor prefabs
        string[] grossNames = { "SlimeDrip_Gross", "GrimeStain_Gross", "CrackDecal_Gross", "RustDrip_Gross" };
        string[] grossLabels = { "Slime Drip", "Grime Stain", "Crack Decal", "Rust Drip" };
        string[] grossDescs = { "Dripping slime stalactite.", "Grimy wall stain.", "Cracked pipe wall.", "Rusty drip streak." };
        for (int i = 0; i < grossNames.Length; i++)
        {
            string path = $"Assets/Prefabs/{grossNames[i]}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                gallery.RegisterAsset(grossLabels[i], grossDescs[i], "Scenery", path, 1f);
        }

        // Bonus Fartcoin
        gallery.RegisterAsset("Bonus Fartcoin", "Special golden coin only reachable after ramp jumps. Worth 10 Fartcoins!",
            "Collectibles", "Assets/Prefabs/BonusCoin.prefab", 1f);

        Debug.Log($"TTR: Registered gallery assets (check Gallery button on start screen)");
    }

    // ===== SAVE =====
    static void SaveScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string scenePath = "Assets/Scenes/SewerRun.unity";
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        foreach (var s in scenes)
            if (s.path == scenePath) { found = true; break; }
        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    // ===== UI =====
    static GameUI CreateUI()
    {
        GameObject canvasObj = new GameObject("GameCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // EventSystem with New Input System
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // === SAFE AREA CONTAINER (protects HUD from notch/dynamic island) ===
        GameObject safeAreaObj = new GameObject("SafeArea");
        safeAreaObj.transform.SetParent(canvasObj.transform, false);
        RectTransform safeRt = safeAreaObj.AddComponent<RectTransform>();
        safeRt.anchorMin = Vector2.zero;
        safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = Vector2.zero;
        safeRt.offsetMax = Vector2.zero;
        safeAreaObj.AddComponent<SafeAreaFitter>();

        // === RIGHT-SIDE PUFFY COMIC SCORE HUD (percentage anchors for any resolution) ===
        // Pooper Snooper occupies top strip (0.895-0.995), HUD starts below at 0.84
        // Max X anchor 0.88 to keep outlines well within screen edge

        // HUD elements parent to SafeArea to avoid notch/dynamic island overlap
        Transform hudParent = safeAreaObj.transform;

        // "SCORE" label — right column
        Text scoreLabelText = MakeStretchText(hudParent, "ScoreLabel", "SCORE",
            26, TextAnchor.LowerRight, new Color(1f, 0.85f, 0.4f, 0.8f),
            new Vector2(0.80f, 0.88f), new Vector2(0.98f, 0.93f), true);

        // Score number - big puffy golden text — right column
        Text scoreText = MakeStretchText(hudParent, "ScoreText", "0",
            52, TextAnchor.UpperRight, new Color(1f, 0.92f, 0.2f),
            new Vector2(0.72f, 0.81f), new Vector2(0.98f, 0.885f), true);
        {
            Outline o2 = scoreText.gameObject.AddComponent<Outline>();
            o2.effectColor = new Color(0.55f, 0.25f, 0f, 0.95f);
            o2.effectDistance = new Vector2(2, -2);
            Outline o3 = scoreText.gameObject.AddComponent<Outline>();
            o3.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o3.effectDistance = new Vector2(3, -3);
            Shadow sh = scoreText.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(3, -4);
        }

        // Distance — right column below score
        Text distanceText = MakeStretchText(hudParent, "DistanceText", "0m",
            32, TextAnchor.UpperRight, new Color(0.6f, 1f, 0.5f),
            new Vector2(0.80f, 0.76f), new Vector2(0.98f, 0.81f), true);
        {
            Outline dOut = distanceText.gameObject.AddComponent<Outline>();
            dOut.effectColor = new Color(0f, 0.2f, 0f, 0.8f);
            dOut.effectDistance = new Vector2(2, -2);
        }

        // Combo counter (compact — hype labels go to CheerOverlay now)
        Text comboText = MakeText(hudParent, "ComboText", "",
            42, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.2f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(200, 60), true);
        Outline comboOutline2 = comboText.gameObject.AddComponent<Outline>();
        comboOutline2.effectColor = new Color(0, 0, 0, 0.6f);
        comboOutline2.effectDistance = new Vector2(-2, 2);
        comboText.gameObject.SetActive(false);

        // Start Panel - Full screen with bathroom stall splash image
        GameObject startPanel = new GameObject("StartPanel");
        startPanel.transform.SetParent(canvasObj.transform, false);
        {
            RectTransform srt = startPanel.AddComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
        }

        // Load splash image as background
        Image startBg = startPanel.AddComponent<Image>();
        string splashPath = "Assets/Images/TTR_Splash.jpg";
        TextureImporter splashImp = AssetImporter.GetAtPath(splashPath) as TextureImporter;
        if (splashImp != null && splashImp.textureType != TextureImporterType.Sprite)
        {
            splashImp.textureType = TextureImporterType.Sprite;
            splashImp.spriteImportMode = SpriteImportMode.Single;
            splashImp.maxTextureSize = 2048;
            splashImp.SaveAndReimport();
        }
        Sprite splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(splashPath);
        if (splashSprite != null)
        {
            startBg.sprite = splashSprite;
            startBg.color = Color.white;
            startBg.type = Image.Type.Simple;
            startBg.preserveAspect = false;
        }
        else
        {
            startBg.color = new Color(0.04f, 0.07f, 0.03f, 0.93f);
        }

        // === Invisible hotspot buttons over door artwork (glow on hover) ===
        // The splash image already has "Poop Alone" and "Poop With Friends"
        // painted on the door - we just need clickable areas that light up.

        // "Poop Alone" hotspot (left side of door artwork)
        GameObject startBtnObj = new GameObject("StartButton");
        startBtnObj.transform.SetParent(startPanel.transform, false);
        {
            RectTransform rt = startBtnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.24f, 0.34f);
            rt.anchorMax = new Vector2(0.49f, 0.52f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        Image startBtnImg = startBtnObj.AddComponent<Image>();
        startBtnImg.color = new Color(1f, 1f, 1f, 0f); // fully transparent
        Button startButton = startBtnObj.AddComponent<Button>();
        {
            ColorBlock cb = startButton.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);       // invisible
            cb.highlightedColor = new Color(1f, 0.95f, 0.7f, 0.25f); // warm glow
            cb.pressedColor = new Color(1f, 0.90f, 0.5f, 0.45f);     // brighter press
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);     // back to invisible
            cb.fadeDuration = 0.15f;
            startButton.colors = cb;
        }
        startBtnObj.AddComponent<ButtonPressEffect>();

        // "Poop With Friends" hotspot (right side of door artwork)
        GameObject tourBtnObj = new GameObject("TourButton");
        tourBtnObj.transform.SetParent(startPanel.transform, false);
        {
            RectTransform rt = tourBtnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.51f, 0.34f);
            rt.anchorMax = new Vector2(0.76f, 0.52f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        Image tourBtnImg = tourBtnObj.AddComponent<Image>();
        tourBtnImg.color = new Color(1f, 1f, 1f, 0f); // fully transparent
        Button tourButton = tourBtnObj.AddComponent<Button>();
        {
            ColorBlock cb = tourButton.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);       // invisible
            cb.highlightedColor = new Color(1f, 0.95f, 0.7f, 0.25f); // warm glow
            cb.pressedColor = new Color(1f, 0.90f, 0.5f, 0.45f);     // brighter press
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);     // back to invisible
            cb.fadeDuration = 0.15f;
            tourButton.colors = cb;
        }
        tourBtnObj.AddComponent<ButtonPressEffect>();

        // === SHOP & GALLERY on the stall columns ===

        // SHOP on left column
        Button shopButton = MakeButton(startPanel.transform, "ShopButton", "SHOP",
            24, new Color(0.65f, 0.50f, 0.12f, 0.85f), Color.white,
            new Vector2(0.06f, 0.30f), new Vector2(0.18f, 0.48f));
        shopButton.transform.localRotation = Quaternion.Euler(0, 0, -3f);
        {
            Outline shopOutline = shopButton.GetComponent<Image>().gameObject.AddComponent<Outline>();
            shopOutline.effectColor = new Color(0.3f, 0.22f, 0.05f);
            shopOutline.effectDistance = new Vector2(3, -3);
        }

        // GALLERY on right column
        Button galleryButton = MakeButton(startPanel.transform, "GalleryButton", "GALLERY",
            20, new Color(0.15f, 0.42f, 0.58f, 0.85f), Color.white,
            new Vector2(0.82f, 0.30f), new Vector2(0.94f, 0.48f));
        galleryButton.transform.localRotation = Quaternion.Euler(0, 0, 3f);
        {
            Outline galOutline = galleryButton.GetComponent<Image>().gameObject.AddComponent<Outline>();
            galOutline.effectColor = new Color(0.08f, 0.2f, 0.32f);
            galOutline.effectDistance = new Vector2(3, -3);
        }

        // Wallet/Fartcoin count (above the door hotspots)
        Text startWalletText = MakeStretchText(startPanel.transform, "StartWallet", "0 Fartcoins",
            20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.30f, 0.53f), new Vector2(0.70f, 0.57f), true);

        // Daily challenge text (below the door, subtle on dark area)
        Text challengeText = MakeStretchText(startPanel.transform, "ChallengeText", "DAILY: ...",
            16, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f, 0.7f),
            new Vector2(0.15f, 0.27f), new Vector2(0.85f, 0.32f), true);

        // Race tagline above the wallet
        MakeStretchText(startPanel.transform, "RaceTagline", "RACE TO BROWN TOWN!",
            24, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 0.15f, 0.85f),
            new Vector2(0.10f, 0.57f), new Vector2(0.90f, 0.62f), true);

        // "or press SPACE" hint at very bottom
        MakeStretchText(startPanel.transform, "HintText", "or press SPACE",
            18, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.65f, 0.5f),
            new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.06f), false);

        // === VOLUME SLIDERS (bottom of splash screen) ===
        Slider musicSlider = MakeVolumeSlider(startPanel.transform, "MusicSlider", "MUSIC",
            new Vector2(0.08f, 0.10f), new Vector2(0.48f, 0.24f), 0.4f);
        Slider sfxSlider = MakeVolumeSlider(startPanel.transform, "SFXSlider", "SOUND",
            new Vector2(0.52f, 0.10f), new Vector2(0.92f, 0.24f), 1.0f);

        // Shop Panel (hidden by default)
        GameObject shopPanel = MakePanel(canvasObj.transform, "ShopPanel",
            new Color(0.04f, 0.06f, 0.03f, 0.95f),
            new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));
        shopPanel.SetActive(false);

        MakeStretchText(shopPanel.transform, "ShopTitle", "SKIN SHOP",
            52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.1f),
            new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.98f), true);

        Button shopCloseButton = MakeButton(shopPanel.transform, "ShopCloseBtn", "BACK",
            30, new Color(0.5f, 0.12f, 0.08f), Color.white,
            new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.08f));

        // Shop scroll area
        GameObject shopScrollObj = new GameObject("ShopScroll");
        shopScrollObj.transform.SetParent(shopPanel.transform, false);
        RectTransform scrollRt = shopScrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.05f, 0.1f);
        scrollRt.anchorMax = new Vector2(0.95f, 0.87f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        ScrollRect scrollRect = shopScrollObj.AddComponent<ScrollRect>();
        shopScrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // needed for scroll
        shopScrollObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content container with vertical layout
        GameObject shopContentObj = new GameObject("ShopContent");
        shopContentObj.transform.SetParent(shopScrollObj.transform, false);
        RectTransform contentRt = shopContentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0, 600); // will be resized by layout

        VerticalLayoutGroup vlg = shopContentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        ContentSizeFitter csf = shopContentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Multiplier text — right column below speed
        Text multiplierText = MakeStretchText(hudParent, "MultiplierText", "x1.0",
            38, TextAnchor.MiddleRight, new Color(1f, 1f, 0.7f),
            new Vector2(0.80f, 0.54f), new Vector2(0.98f, 0.60f), true);
        multiplierText.gameObject.SetActive(false);

        // "FARTCOINS" label — right column below distance
        Text coinLabel = MakeStretchText(hudParent, "CoinLabel", "FARTCOINS",
            18, TextAnchor.LowerRight, new Color(0.85f, 0.6f, 0.15f, 0.85f),
            new Vector2(0.80f, 0.72f), new Vector2(0.98f, 0.76f), true);

        // HUD coin counter — right column below fartcoin label
        Text coinCountText = MakeStretchText(hudParent, "CoinCountText", "0",
            40, TextAnchor.UpperRight, new Color(1f, 0.75f, 0.25f),
            new Vector2(0.80f, 0.66f), new Vector2(0.98f, 0.72f), true);
        {
            Outline cOut2 = coinCountText.gameObject.AddComponent<Outline>();
            cOut2.effectColor = new Color(0.45f, 0.2f, 0f, 0.9f);
            cOut2.effectDistance = new Vector2(2, -2);
            Outline cOut3 = coinCountText.gameObject.AddComponent<Outline>();
            cOut3.effectColor = new Color(0f, 0f, 0f, 0.85f);
            cOut3.effectDistance = new Vector2(3, -3);
            Shadow cSh = coinCountText.gameObject.AddComponent<Shadow>();
            cSh.effectColor = new Color(0f, 0f, 0f, 0.5f);
            cSh.effectDistance = new Vector2(2, -3);
        }

        // HUD speed indicator — right column below coins
        Text speedText = MakeStretchText(hudParent, "SpeedText", "0 SMPH",
            22, TextAnchor.MiddleRight, new Color(0.3f, 1f, 0.4f, 0.85f),
            new Vector2(0.80f, 0.60f), new Vector2(0.98f, 0.66f), true);

        // HUD wallet text — right column below multiplier
        Text walletText = MakeStretchText(hudParent, "WalletText", "0",
            26, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.80f, 0.48f), new Vector2(0.98f, 0.54f), true);

        // Game Over Panel
        GameObject gameOverPanel = MakePanel(canvasObj.transform, "GameOverPanel",
            new Color(0.12f, 0.02f, 0.02f, 0.93f),
            new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));
        gameOverPanel.SetActive(false);

        Text goTitle = MakeStretchText(gameOverPanel.transform, "GOTitle", "CLOGGED!",
            82, TextAnchor.MiddleCenter, new Color(1f, 0.25f, 0.2f),
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f), true);
        Outline goOutline2 = goTitle.gameObject.AddComponent<Outline>();
        goOutline2.effectColor = new Color(0.3f, 0f, 0f, 0.9f);
        goOutline2.effectDistance = new Vector2(4, -4);

        Text finalScoreText = MakeStretchText(gameOverPanel.transform, "FinalScoreText",
            "Score: 0", 46, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.7f), true);

        Text highScoreText = MakeStretchText(gameOverPanel.transform, "HighScoreText",
            "Best: 0", 32, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.1f),
            new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.52f), false);

        Text runStatsText = MakeStretchText(gameOverPanel.transform, "RunStatsText",
            "0m  |  0 coins", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.65f),
            new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.42f), false);

        Button restartButton = MakeButton(gameOverPanel.transform, "RestartButton",
            "FLUSH AGAIN", 42, new Color(0.55f, 0.12f, 0.08f), Color.white,
            new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.3f));

        MakeStretchText(gameOverPanel.transform, "HintText2", "or press SPACE",
            22, TextAnchor.MiddleCenter, new Color(0.45f, 0.45f, 0.4f),
            new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.11f), false);

        GameUI ui = canvasObj.AddComponent<GameUI>();
        ui.scoreText = scoreText;
        ui.distanceText = distanceText;
        ui.comboText = comboText;
        ui.walletText = walletText;
        ui.startPanel = startPanel;
        ui.startButton = startButton;
        ui.challengeText = challengeText;
        ui.startWalletText = startWalletText;
        ui.shopButton = shopButton;
        ui.galleryButton = galleryButton;
        ui.tourButton = tourButton;
        ui.shopPanel = shopPanel;
        ui.shopContent = shopContentObj.transform;
        ui.shopCloseButton = shopCloseButton;
        ui.gameOverPanel = gameOverPanel;
        ui.finalScoreText = finalScoreText;
        ui.highScoreText = highScoreText;
        ui.runStatsText = runStatsText;
        ui.restartButton = restartButton;
        ui.multiplierText = multiplierText;
        ui.coinCountText = coinCountText;
        ui.speedText = speedText;
        ui.musicVolumeSlider = musicSlider;
        ui.sfxVolumeSlider = sfxSlider;

        // Screen Effects overlay (hit flash, speed vignette)
        GameObject sfxOverlay = new GameObject("ScreenEffects");
        sfxOverlay.transform.SetParent(canvasObj.transform, false);
        RectTransform sfxRt = sfxOverlay.AddComponent<RectTransform>();
        sfxRt.anchorMin = Vector2.zero;
        sfxRt.anchorMax = Vector2.one;
        sfxRt.offsetMin = Vector2.zero;
        sfxRt.offsetMax = Vector2.zero;
        sfxOverlay.AddComponent<ScreenEffects>();

        return ui;
    }

    // ===== CHEER OVERLAY (Poop Crew Live) =====
    static void CreateCheerOverlay(GameObject canvas)
    {
        // Fullscreen passthrough container — poops self-build in Start()
        GameObject container = new GameObject("CheerOverlay");
        container.transform.SetParent(canvas.transform, false);
        RectTransform rt = container.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        container.AddComponent<CheerOverlay>();
    }

    // ===== SEWER TOUR =====
    static void CreateSewerTour(TurdController player, PipeGenerator pipeGen)
    {
        GameObject obj = new GameObject("SewerTour");
        SewerTour tour = obj.AddComponent<SewerTour>();
        tour.player = player;
        tour.pipeGen = pipeGen;

        // Grab prefab references from existing spawners
        ObstacleSpawner obs = Object.FindFirstObjectByType<ObstacleSpawner>();
        if (obs != null)
        {
            tour.obstaclePrefabs = obs.obstaclePrefabs;
            tour.coinPrefab = obs.coinPrefab;
            tour.gratePrefab = obs.gratePrefab;
            tour.bigAirRampPrefab = obs.bigAirRampPrefab;
            tour.dropZonePrefab = obs.dropZonePrefab;
        }

        PowerUpSpawner pus = Object.FindFirstObjectByType<PowerUpSpawner>();
        if (pus != null)
        {
            tour.speedBoostPrefab = pus.speedBoostPrefab;
            tour.jumpRampPrefab = pus.jumpRampPrefab;
            tour.bonusCoinPrefab = pus.bonusCoinPrefab;
            tour.shieldPrefab = pus.shieldPrefab;
            tour.magnetPrefab = pus.magnetPrefab;
            tour.slowMoPrefab = pus.slowMoPrefab;
        }

        ScenerySpawner ss = Object.FindFirstObjectByType<ScenerySpawner>();
        if (ss != null)
        {
            tour.sceneryPrefabs = ss.sceneryPrefabs;
            tour.grossPrefabs = ss.grossPrefabs;
            tour.signPrefabs = ss.signPrefabs;
        }

        WaterCreatureSpawner wcs = Object.FindFirstObjectByType<WaterCreatureSpawner>();
        if (wcs != null)
            tour.squirtPrefab = wcs.squirtPrefab;

        Debug.Log("TTR: Created Sewer Tour with all prefab references wired.");
    }

    // ===== UI HELPERS =====
    static Text MakeText(Transform parent, string name, string content, int fontSize,
        TextAnchor align, Color color, Vector2 anchor, Vector2 pos, Vector2 size, bool outline)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = _font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        if (outline)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(2, -2);
        }

        return t;
    }

    static Text MakeStretchText(Transform parent, string name, string content, int fontSize,
        TextAnchor align, Color color, Vector2 anchorMin, Vector2 anchorMax, bool outline)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = _font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        if (outline)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(2, -2);
        }

        return t;
    }

    static GameObject MakePanel(Transform parent, string name, Color bg,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bg;

        return go;
    }

    static Button MakeButton(Transform parent, string name, string label, int fontSize,
        Color bg, Color textColor, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bg;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(
            Mathf.Min(bg.r * 1.3f, 1f),
            Mathf.Min(bg.g * 1.3f, 1f),
            Mathf.Min(bg.b * 1.3f, 1f), 1f);
        cb.pressedColor = bg * 0.7f;
        btn.colors = cb;

        MakeStretchText(go.transform, "Label", label, fontSize,
            TextAnchor.MiddleCenter, textColor,
            Vector2.zero, Vector2.one, true);

        go.AddComponent<ButtonPressEffect>();

        return btn;
    }

    static Slider MakeVolumeSlider(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, float defaultValue)
    {
        // Container
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent, false);
        RectTransform crt = container.AddComponent<RectTransform>();
        crt.anchorMin = anchorMin;
        crt.anchorMax = anchorMax;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        // Label (top 40%)
        MakeStretchText(container.transform, "Label", label, 14,
            TextAnchor.MiddleCenter, new Color(0.8f, 0.75f, 0.65f, 0.8f),
            new Vector2(0f, 0.55f), new Vector2(1f, 1f), true);

        // Slider (bottom 50%)
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(container.transform, false);
        RectTransform srt = sliderObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.08f, 0.05f);
        srt.anchorMax = new Vector2(0.92f, 0.50f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        // Background track
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.13f, 0.10f, 0.7f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.65f, 0.50f, 0.12f, 0.9f);

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        // Handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRt = handle.AddComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 0f);
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.9f, 0.85f, 0.7f);

        // Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = PlayerPrefs.GetFloat(
            name.Contains("Music") ? "MusicVolume" : "SFXVolume", defaultValue);

        return slider;
    }
}
