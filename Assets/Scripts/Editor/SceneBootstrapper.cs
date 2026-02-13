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
        CreateFlushSequence();
        CreateAssetGallery(gameUI);
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
        GameObject modelPrefab = LoadModel("Assets/Models/MrCorny.fbx");

        if (modelPrefab != null)
        {
            GameObject model = (GameObject)Object.Instantiate(modelPrefab);
            model.name = "Model";
            // SetParent(parent, false) preserves the FBX import rotation!
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 0.17f;

            // Log model bounds for size debugging
            {
                Bounds mb = new Bounds(model.transform.position, Vector3.zero);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                    mb.Encapsulate(r.bounds);
                Debug.Log($"TTR: MrCorny model bounds at scale 0.17: size={mb.size} (GLB={_lastModelWasGLB})");
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
            Debug.Log($"TTR: MrCorny painting: {kernelCount} kernels, {bodyCount} body, {faceCount} face features");

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
            capsule.transform.localScale = new Vector3(0.5f, 0.3f, 1f);
            capsule.GetComponent<Renderer>().material =
                MakeURPMat("MrCorny_Mat", new Color(0.55f, 0.35f, 0.18f), 0.05f, 0.4f);
            AddFaceForSkin(capsule, PlayerData.SelectedSkin);
            Debug.LogWarning("TTR: MrCorny.fbx not found, using placeholder capsule.");
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
        pipeCam.followDistance = 4f;         // behind the turd on the pipe path
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

        // Sh!tcoin - copper penny with $ emboss, sized like a real coin you'd find in a sewer
        GameObject coinPrefab = CreateShitcoinPrefab();

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
            ("SewerMine",   new Color(0.22f, 0.22f, 0.24f), 0.7f,  0.6f,  new Color(0.6f, 0.08f, 0.05f),  1.5f),  // heavy metal + red accent
            ("Cockroach",   new Color(0.3f, 0.15f, 0.06f),  0.15f, 0.8f,  new Color(0.05f, 0.02f, 0.01f), 0.2f),  // chitinous shell sheen
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

        spawner.signPrefabs = signPrefabs.ToArray();
        Debug.Log($"TTR: Created 4 gross decor types + {signPrefabs.Count} sign/graffiti variants.");
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
        "WARNING\nBROWN\nWATER ONLY"
    };
    static int _warningIndex = 0;

    static GameObject CreateWarningSignPrefab()
    {
        string path = $"Assets/Prefabs/WarningSign_Gross_{_warningIndex}.prefab";
        GameObject root = new GameObject("WarningSign");

        Material yellowMat = MakeURPMat("Gross_WarningBg", new Color(0.95f, 0.85f, 0.1f), 0f, 0.3f);
        yellowMat.EnableKeyword("_EMISSION");
        yellowMat.SetColor("_EmissionColor", new Color(0.3f, 0.25f, 0.02f));
        EditorUtility.SetDirty(yellowMat);
        Material borderMat = MakeURPMat("Gross_WarningBorder", new Color(0.15f, 0.12f, 0.1f), 0.3f, 0.3f);

        // Diamond-rotated yellow plate
        AddPrimChild(root, "Plate", PrimitiveType.Cube, Vector3.zero,
            Quaternion.Euler(0, 0, 45), new Vector3(0.22f, 0.22f, 0.015f), yellowMat);
        // Border
        AddPrimChild(root, "Border", PrimitiveType.Cube, Vector3.zero,
            Quaternion.Euler(0, 0, 45), new Vector3(0.25f, 0.25f, 0.012f), borderMat);

        // Warning text
        string txt = WarningTexts[_warningIndex % WarningTexts.Length];
        _warningIndex++;

        GameObject textObj = new GameObject("WarningText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.01f);
        textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = txt;
        tm.fontSize = 36;
        tm.characterSize = 0.008f; // small enough to fit inside diamond
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.1f, 0.08f, 0.05f);
        tm.fontStyle = FontStyle.Bold;

        // Mounting post
        AddPrimChild(root, "Post", PrimitiveType.Cube, new Vector3(0, -0.2f, 0.005f),
            Quaternion.identity, new Vector3(0.02f, 0.15f, 0.02f), borderMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 0.9f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Pipe section number marker - "SECTOR 7G" style.</summary>
    static readonly string[] PipeNumbers = {
        "SECTOR 7G", "PIPE 42", "TUNNEL B-12", "DRAIN 69",
        "ZONE 404", "DUCT 13", "SHAFT 99", "MAIN 1A",
        "OVERFLOW 3", "JUNCTION 8"
    };
    static int _pipeNumIndex = 0;

    static GameObject CreatePipeNumberPrefab()
    {
        string path = $"Assets/Prefabs/PipeNumber_Gross_{_pipeNumIndex}.prefab";
        GameObject root = new GameObject("PipeNumber");

        Material plateMat = MakeURPMat("Gross_PipeNumPlate", new Color(0.35f, 0.4f, 0.35f), 0.3f, 0.3f);

        // Metal number plate
        AddPrimChild(root, "Plate", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(0.3f, 0.12f, 0.01f), plateMat);

        // Number text
        string num = PipeNumbers[_pipeNumIndex % PipeNumbers.Length];
        _pipeNumIndex++;

        GameObject textObj = new GameObject("NumberText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.007f);
        textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = num;
        tm.fontSize = 40;
        tm.characterSize = 0.008f; // fits within plate
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.85f, 0.82f, 0.7f);
        tm.fontStyle = FontStyle.Bold;

        // Mounting bolts
        Material boltMat = MakeURPMat("Gross_PipeNumBolt", new Color(0.3f, 0.28f, 0.25f), 0.5f, 0.5f);
        AddPrimChild(root, "Bolt1", PrimitiveType.Sphere, new Vector3(-0.12f, 0.04f, -0.006f),
            Quaternion.identity, Vector3.one * 0.015f, boltMat);
        AddPrimChild(root, "Bolt2", PrimitiveType.Sphere, new Vector3(0.12f, 0.04f, -0.006f),
            Quaternion.identity, Vector3.one * 0.015f, boltMat);
        AddPrimChild(root, "Bolt3", PrimitiveType.Sphere, new Vector3(-0.12f, -0.04f, -0.006f),
            Quaternion.identity, Vector3.one * 0.015f, boltMat);
        AddPrimChild(root, "Bolt4", PrimitiveType.Sphere, new Vector3(0.12f, -0.04f, -0.006f),
            Quaternion.identity, Vector3.one * 0.015f, boltMat);

        // Rust stain below
        Material rustMat = MakeURPMat("Gross_PipeNumRust", new Color(0.45f, 0.25f, 0.1f), 0f, 0.2f);
        AddPrimChild(root, "RustStain", PrimitiveType.Sphere, new Vector3(0.02f, -0.1f, 0),
            Quaternion.identity, new Vector3(0.08f, 0.06f, 0.003f), rustMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 0.8f;
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
        "STINK\nOR\nSWIM", "MR CORNY\nWAS HERE"
    };

    static int _graffitiIndex = 0;

    static GameObject CreateGraffitiSignPrefab()
    {
        string path = $"Assets/Prefabs/GraffitiSign_Gross_{_graffitiIndex}.prefab";
        GameObject root = new GameObject("GraffitiSign");

        // NO wall patch - spray paint goes directly on the pipe surface (decal style)

        // Spray paint colors - cycle through for variety
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

        // 3D TextMesh - spray painted directly on pipe surface
        string msg = GraffitiMessages[_graffitiIndex % GraffitiMessages.Length];
        _graffitiIndex++;

        GameObject textObj = new GameObject("SprayText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.005f); // barely off surface
        textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);

        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = msg;
        tm.fontSize = 48;
        tm.characterSize = 0.018f; // smaller to fit naturally on pipe wall
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = sprayColor;
        tm.fontStyle = FontStyle.Bold;

        // Hand-sprayed tilt
        textObj.transform.localRotation *= Quaternion.Euler(0, 0, Random.Range(-12f, 12f));

        // Thin paint drips (subtle, just below text)
        Material dripMat = MakeURPMat("Gross_Paint", sprayColor * 0.7f, 0f, 0.15f);
        dripMat.EnableKeyword("_EMISSION");
        dripMat.SetColor("_EmissionColor", sprayColor * 0.15f);
        EditorUtility.SetDirty(dripMat);

        for (int i = 0; i < 2; i++)
        {
            float x = Random.Range(-0.08f, 0.08f);
            float len = Random.Range(0.02f, 0.06f);
            AddPrimChild(root, $"Drip{i}", PrimitiveType.Capsule,
                new Vector3(x, -0.08f - i * 0.02f, -0.003f),
                Quaternion.identity, new Vector3(0.006f, len, 0.003f), dripMat);
        }

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 1.0f;
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
    };
    static int _adIndex = 0;

    static GameObject CreateSewerAdPrefab()
    {
        string path = $"Assets/Prefabs/SewerAd_Gross_{_adIndex}.prefab";
        GameObject root = new GameObject("SewerAd");

        // Pick an ad
        string[] ad = SewerAds[_adIndex % SewerAds.Length];
        _adIndex++;

        // Billboard frame
        Material frameMat = MakeURPMat("Gross_AdFrame", new Color(0.3f, 0.28f, 0.25f), 0.4f, 0.3f);
        Material boardMat = MakeURPMat("Gross_AdBoard", new Color(0.92f, 0.88f, 0.75f), 0.02f, 0.15f);
        boardMat.EnableKeyword("_EMISSION");
        boardMat.SetColor("_EmissionColor", new Color(0.2f, 0.18f, 0.12f));
        EditorUtility.SetDirty(boardMat);

        // Background board
        AddPrimChild(root, "Board", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(0.6f, 0.45f, 0.012f), boardMat);
        // Thick frame border
        Material thickFrame = MakeURPMat("Gross_AdFrameThick", new Color(0.2f, 0.18f, 0.15f), 0.5f, 0.3f);
        float bw = 0.025f;
        AddPrimChild(root, "FrameTop", PrimitiveType.Cube, new Vector3(0, 0.235f, 0),
            Quaternion.identity, new Vector3(0.65f, bw, 0.025f), thickFrame);
        AddPrimChild(root, "FrameBot", PrimitiveType.Cube, new Vector3(0, -0.235f, 0),
            Quaternion.identity, new Vector3(0.65f, bw, 0.025f), thickFrame);
        AddPrimChild(root, "FrameL", PrimitiveType.Cube, new Vector3(-0.313f, 0, 0),
            Quaternion.identity, new Vector3(bw, 0.5f, 0.025f), thickFrame);
        AddPrimChild(root, "FrameR", PrimitiveType.Cube, new Vector3(0.313f, 0, 0),
            Quaternion.identity, new Vector3(bw, 0.5f, 0.025f), thickFrame);

        // Title text (3D TextMesh)
        Color titleCol = new Color(0.7f, 0.15f, 0.1f);
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(root.transform, false);
        titleObj.transform.localPosition = new Vector3(0, 0.14f, -0.008f);
        titleObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh titleTm = titleObj.AddComponent<TextMesh>();
        titleTm.text = ad[0]; // e.g. "PIPE DREAMS"
        titleTm.fontSize = 60;
        titleTm.characterSize = 0.012f;
        titleTm.anchor = TextAnchor.MiddleCenter;
        titleTm.alignment = TextAlignment.Center;
        titleTm.color = titleCol;
        titleTm.fontStyle = FontStyle.Bold;

        // Subtitle / company name
        GameObject subObj = new GameObject("SubText");
        subObj.transform.SetParent(root.transform, false);
        subObj.transform.localPosition = new Vector3(0, 0.08f, -0.008f);
        subObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh subTm = subObj.AddComponent<TextMesh>();
        subTm.text = ad[1]; // e.g. "Real Estate"
        subTm.fontSize = 40;
        subTm.characterSize = 0.01f;
        subTm.anchor = TextAnchor.MiddleCenter;
        subTm.alignment = TextAlignment.Center;
        subTm.color = new Color(0.35f, 0.3f, 0.25f);

        // Main ad copy (the funny part)
        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(root.transform, false);
        bodyObj.transform.localPosition = new Vector3(0, -0.04f, -0.008f);
        bodyObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh bodyTm = bodyObj.AddComponent<TextMesh>();
        bodyTm.text = ad[2]; // e.g. "Live Where You Flush!"
        bodyTm.fontSize = 36;
        bodyTm.characterSize = 0.009f;
        bodyTm.anchor = TextAnchor.MiddleCenter;
        bodyTm.alignment = TextAlignment.Center;
        bodyTm.color = new Color(0.15f, 0.12f, 0.1f);
        bodyTm.fontStyle = FontStyle.Italic;

        // Footer / CTA
        GameObject footObj = new GameObject("FooterText");
        footObj.transform.SetParent(root.transform, false);
        footObj.transform.localPosition = new Vector3(0, -0.16f, -0.008f);
        footObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        TextMesh footTm = footObj.AddComponent<TextMesh>();
        footTm.text = ad[3]; // e.g. "Luxury Sewage Condos from $9.99"
        footTm.fontSize = 30;
        footTm.characterSize = 0.008f;
        footTm.anchor = TextAnchor.MiddleCenter;
        footTm.alignment = TextAlignment.Center;
        footTm.color = new Color(0.5f, 0.35f, 0.15f);

        // Poop emoji accent (brown sphere)
        Material poopIconMat = MakeURPMat("Gross_PoopIcon", new Color(0.4f, 0.25f, 0.1f), 0f, 0.4f);
        AddPrimChild(root, "PoopIcon", PrimitiveType.Sphere, new Vector3(0.2f, 0.14f, -0.01f),
            Quaternion.identity, new Vector3(0.04f, 0.05f, 0.01f), poopIconMat);

        // Grime stain on corner
        Material grimeMat = MakeURPMat("Gross_AdGrime", new Color(0.15f, 0.12f, 0.08f, 0.6f), 0f, 0.5f);
        AddPrimChild(root, "Grime", PrimitiveType.Sphere, new Vector3(0.18f, -0.12f, -0.007f),
            Quaternion.identity, new Vector3(0.1f, 0.08f, 0.003f), grimeMat);
        // Water damage stain
        AddPrimChild(root, "WaterStain", PrimitiveType.Sphere, new Vector3(-0.12f, -0.18f, -0.007f),
            Quaternion.identity, new Vector3(0.14f, 0.06f, 0.003f), grimeMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 0.8f; // smaller - flush with pipe wall
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

        // Create bonus Sh!tcoin prefab - special big coin only reachable after ramp jumps
        spawner.bonusCoinPrefab = CreateBonusCoinPrefab();
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
        GameObject root = new GameObject("DropZone");

        // Visual: drain grate opening in the pipe floor
        Material drainMat = MakeURPMat("DropZone_Drain", new Color(0.2f, 0.18f, 0.15f), 0.5f, 0.4f);
        drainMat.EnableKeyword("_EMISSION");
        drainMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.6f) * 1.5f);
        EditorUtility.SetDirty(drainMat);

        // Drain ring (large disc on pipe floor)
        AddPrimChild(root, "DrainRing", PrimitiveType.Cylinder,
            Vector3.zero, Quaternion.identity,
            new Vector3(2.5f, 0.1f, 2.5f), drainMat);

        // Inner glow - cyan whirlpool effect
        Material whirlMat = MakeURPMat("DropZone_Whirl", new Color(0.1f, 0.5f, 0.8f, 0.6f), 0f, 0.9f);
        whirlMat.EnableKeyword("_EMISSION");
        whirlMat.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1f) * 0.8f);
        whirlMat.SetFloat("_Surface", 1);
        whirlMat.SetFloat("_Blend", 0);
        whirlMat.SetOverrideTag("RenderType", "Transparent");
        whirlMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        whirlMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        whirlMat.SetInt("_ZWrite", 0);
        whirlMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        whirlMat.renderQueue = 3000;
        EditorUtility.SetDirty(whirlMat);

        AddPrimChild(root, "WhirlPool", PrimitiveType.Cylinder,
            new Vector3(0, 0.05f, 0), Quaternion.identity,
            new Vector3(2f, 0.05f, 2f), whirlMat);

        // Warning bars around drain
        Material warnMat = MakeURPMat("DropZone_Warn", new Color(1f, 0.7f, 0.1f), 0.2f, 0.4f);
        warnMat.EnableKeyword("_EMISSION");
        warnMat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0f) * 1.5f);
        EditorUtility.SetDirty(warnMat);

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 1.4f, 0.15f, Mathf.Sin(angle) * 1.4f);
            AddPrimChild(root, $"WarnPost{i}", PrimitiveType.Cylinder,
                pos, Quaternion.identity,
                new Vector3(0.15f, 0.3f, 0.15f), warnMat);
        }

        // Trigger collider
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(3f, 2f, 3f);

        VerticalDrop drop = root.AddComponent<VerticalDrop>();
        drop.ringPrefab = ringPrefab;
        drop.dropDuration = 12f;
        drop.dropSpeed = 18f;
        drop.ringCount = 20;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Drop Zone prefab (freefall with rings)");
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
    /// Creates the Sh!tcoin collectible - a copper penny with raised $ emboss,
    /// ridged edge, and patina. Smaller and more detailed than the old gold disc.
    /// </summary>
    static GameObject CreateShitcoinPrefab()
    {
        string prefabPath = "Assets/Prefabs/CornCoin.prefab";
        GameObject root = new GameObject("Shitcoin");

        // === MATERIALS (semi-transparent, gentle glow) ===
        // Copper penny body - semi-transparent
        Material copperMat = MakeURPMat("Shitcoin_Copper", new Color(0.72f, 0.45f, 0.2f, 0.6f), 0.65f, 0.7f);
        copperMat.SetFloat("_Surface", 1f); // Transparent
        copperMat.SetFloat("_Blend", 0f);   // Alpha blend
        copperMat.SetFloat("_SrcBlend", 5f);
        copperMat.SetFloat("_DstBlend", 10f);
        copperMat.SetFloat("_ZWrite", 0f);
        copperMat.SetOverrideTag("RenderType", "Transparent");
        copperMat.renderQueue = 3000;
        copperMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        copperMat.EnableKeyword("_EMISSION");
        copperMat.SetColor("_EmissionColor", new Color(0.8f, 0.5f, 0.15f) * 0.4f);
        EditorUtility.SetDirty(copperMat);

        // Darker patina for the raised details - semi-transparent
        Material patinaMat = MakeURPMat("Shitcoin_Patina", new Color(0.5f, 0.32f, 0.12f, 0.5f), 0.5f, 0.5f);
        patinaMat.SetFloat("_Surface", 1f);
        patinaMat.SetFloat("_Blend", 0f);
        patinaMat.SetFloat("_SrcBlend", 5f);
        patinaMat.SetFloat("_DstBlend", 10f);
        patinaMat.SetFloat("_ZWrite", 0f);
        patinaMat.SetOverrideTag("RenderType", "Transparent");
        patinaMat.renderQueue = 3000;
        patinaMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        patinaMat.EnableKeyword("_EMISSION");
        patinaMat.SetColor("_EmissionColor", new Color(0.6f, 0.35f, 0.1f) * 0.2f);
        EditorUtility.SetDirty(patinaMat);

        // Gold for the $ symbol - slightly brighter but still transparent
        Material symbolMat = MakeURPMat("Shitcoin_Symbol", new Color(1f, 0.85f, 0.25f, 0.7f), 0.8f, 0.85f);
        symbolMat.SetFloat("_Surface", 1f);
        symbolMat.SetFloat("_Blend", 0f);
        symbolMat.SetFloat("_SrcBlend", 5f);
        symbolMat.SetFloat("_DstBlend", 10f);
        symbolMat.SetFloat("_ZWrite", 0f);
        symbolMat.SetOverrideTag("RenderType", "Transparent");
        symbolMat.renderQueue = 3000;
        symbolMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        symbolMat.EnableKeyword("_EMISSION");
        symbolMat.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.12f) * 0.5f);
        EditorUtility.SetDirty(symbolMat);

        // Edge ridges material - semi-transparent
        Material ridgeMat = MakeURPMat("Shitcoin_Ridge", new Color(0.6f, 0.38f, 0.15f, 0.5f), 0.7f, 0.6f);
        ridgeMat.SetFloat("_Surface", 1f);
        ridgeMat.SetFloat("_Blend", 0f);
        ridgeMat.SetFloat("_SrcBlend", 5f);
        ridgeMat.SetFloat("_DstBlend", 10f);
        ridgeMat.SetFloat("_ZWrite", 0f);
        ridgeMat.SetOverrideTag("RenderType", "Transparent");
        ridgeMat.renderQueue = 3000;
        ridgeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ridgeMat.EnableKeyword("_EMISSION");
        ridgeMat.SetColor("_EmissionColor", new Color(0.65f, 0.4f, 0.12f) * 0.3f);
        EditorUtility.SetDirty(ridgeMat);

        // Halo glow - soft transparent
        Material haloMat = MakeURPMat("CoinHalo_Glow", new Color(0.9f, 0.7f, 0.2f, 0.25f), 0f, 0.1f);
        haloMat.EnableKeyword("_EMISSION");
        haloMat.SetColor("_EmissionColor", new Color(0.9f, 0.65f, 0.15f) * 0.5f);
        haloMat.SetFloat("_Surface", 1f);
        haloMat.SetFloat("_Blend", 0f);
        haloMat.SetFloat("_SrcBlend", 5f);
        haloMat.SetFloat("_DstBlend", 10f);
        haloMat.SetFloat("_ZWrite", 0f);
        haloMat.SetOverrideTag("RenderType", "Transparent");
        haloMat.renderQueue = 3000;
        haloMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(haloMat);

        // === COIN BODY (compact see-through penny) ===
        float coinDiam = 0.12f;
        float coinThick = 0.035f;
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

        // === $ SYMBOL on both faces (scaled to coin size) ===
        float sc = coinDiam; // scale factor (was 1.0, now relative to coinDiam)
        float symbolHeight = coinDiam * 0.35f;
        float cubeSize = 0.06f * sc;
        float barWidth = 0.04f * sc;

        Vector3[] sPath = {
            new Vector3( 0.08f * sc, 0, -symbolHeight * 0.45f),
            new Vector3( 0.12f * sc, 0, -symbolHeight * 0.3f),
            new Vector3( 0.10f * sc, 0, -symbolHeight * 0.15f),
            new Vector3( 0.04f * sc, 0,  0f),
            new Vector3(-0.02f * sc, 0,  0f),
            new Vector3(-0.10f * sc, 0,  symbolHeight * 0.15f),
            new Vector3(-0.12f * sc, 0,  symbolHeight * 0.3f),
            new Vector3(-0.08f * sc, 0,  symbolHeight * 0.45f),
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
        col.radius = 0.5f; // generous trigger radius - still easy to collect

        root.AddComponent<Collectible>();
        root.tag = "Collectible";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sh!tcoin penny with $ emboss and ridged edge");
        return prefab;
    }

    /// <summary>Creates the bonus Sh!tcoin - bigger, flashier gold penny worth 10x. Only after ramps.</summary>
    static GameObject CreateBonusCoinPrefab()
    {
        string prefabPath = "Assets/Prefabs/BonusCoin.prefab";
        GameObject root = new GameObject("BonusCoin");

        // Gold material - semi-transparent with gentle glow
        Material goldMat = MakeURPMat("BonusCoin_Gold", new Color(1f, 0.9f, 0.15f, 0.65f), 0.9f, 0.9f);
        goldMat.SetFloat("_Surface", 1f);
        goldMat.SetFloat("_Blend", 0f);
        goldMat.SetFloat("_SrcBlend", 5f);
        goldMat.SetFloat("_DstBlend", 10f);
        goldMat.SetFloat("_ZWrite", 0f);
        goldMat.SetOverrideTag("RenderType", "Transparent");
        goldMat.renderQueue = 3000;
        goldMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        goldMat.EnableKeyword("_EMISSION");
        goldMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 0.6f);
        EditorUtility.SetDirty(goldMat);

        Material symbolMat = MakeURPMat("BonusCoin_Symbol", new Color(1f, 0.95f, 0.4f, 0.75f), 0.95f, 0.95f);
        symbolMat.SetFloat("_Surface", 1f);
        symbolMat.SetFloat("_Blend", 0f);
        symbolMat.SetFloat("_SrcBlend", 5f);
        symbolMat.SetFloat("_DstBlend", 10f);
        symbolMat.SetFloat("_ZWrite", 0f);
        symbolMat.SetOverrideTag("RenderType", "Transparent");
        symbolMat.renderQueue = 3000;
        symbolMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        symbolMat.EnableKeyword("_EMISSION");
        symbolMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 0.7f);
        EditorUtility.SetDirty(symbolMat);

        // Bonus coin body (slightly bigger than regular, but not giant)
        float coinDiam = 0.25f;
        float coinThick = 0.07f;
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
        Debug.Log("TTR: Created Bonus Sh!tcoin (gold penny, 10x value, post-ramp reward)");
        return prefab;
    }

    // ===== SMOOTH SNAKE AI RACER =====
    static void CreateSmoothSnake(PipeGenerator pipeGen)
    {
        // Empty root for AI mechanics (same pattern as player - prevents rotation bugs)
        GameObject snake = new GameObject("SmoothSnake");
        snake.transform.position = new Vector3(1f, -3f, -5f);

        // Load visual model as child (uses LoadModel for GLB preference)
        GameObject modelAsset = LoadModel("Assets/Models/MrCorny.fbx");

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
        GameObject modelAsset = LoadModel("Assets/Models/MrCorny.fbx");

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
                model.transform.localScale = Vector3.one * 0.15f; // slightly smaller than player

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
            }
            else
            {
                // Fallback: primitive body
                Material mat = MakeURPMat($"Racer_{presets[i]}_Body", racerColors[i], 0.1f, 0.65f);
                AddPrimChild(racer, "Body", PrimitiveType.Capsule, Vector3.zero,
                    Quaternion.Euler(90, 0, 0), new Vector3(0.5f, 1f, 0.4f), mat);
            }

            // Collider
            CapsuleCollider col = racer.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.25f;
            col.height = 1.2f;
            col.direction = 2;

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
        rm.raceDistance = 1000f;

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
            lbRect.anchorMin = new Vector2(0.01f, 0.45f);
            lbRect.anchorMax = new Vector2(0.22f, 0.75f);
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

    /// <summary>Add unique face features per AI racer personality.</summary>
    static void AddRacerFace(GameObject model, string preset, Color bodyColor)
    {
        FaceBounds fb = ComputeFaceBounds(model, rear: true);
        Vector3 fwd = fb.fwd, upV = fb.upV, sideV = fb.sideV;
        float upExt = fb.upExt, sideExt = fb.sideExt;

        float eyeSize = Mathf.Max(sideExt, upExt) * 0.32f;
        eyeSize = Mathf.Clamp(eyeSize, 0.1f, 0.7f);
        float eyeGap = Mathf.Max(sideExt * 0.22f, 0.07f);
        Vector3 eyeBase = fb.frontPos + upV * (upExt * 0.3f);

        // Shared materials
        Material whiteMat = MakeURPMat($"Racer_{preset}_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat($"Racer_{preset}_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);

        switch (preset)
        {
            case "SkidmarkSteve":
                // Angry brow, big wide eyes, mean look
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
                // Angry brows
                Material browMat = MakeURPMat($"Racer_Steve_Brow", new Color(0.3f, 0.15f, 0.05f), 0f, 0.5f);
                AddPrimChild(model, "LeftBrow", PrimitiveType.Cube,
                    eyeBase - sideV * eyeGap + upV * (eyeSize * 0.55f),
                    Quaternion.Euler(0, 0, -20),
                    new Vector3(eyeSize * 0.8f, eyeSize * 0.15f, eyeSize * 0.3f), browMat);
                AddPrimChild(model, "RightBrow", PrimitiveType.Cube,
                    eyeBase + sideV * eyeGap + upV * (eyeSize * 0.55f),
                    Quaternion.Euler(0, 0, 20),
                    new Vector3(eyeSize * 0.8f, eyeSize * 0.15f, eyeSize * 0.3f), browMat);
                break;

            case "PrincessPlop":
                // Big sparkly eyes, eyelashes
                float pEyeSize = eyeSize * 1.2f;
                AddPrimChild(model, "LeftEye", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap, Quaternion.identity,
                    new Vector3(pEyeSize, pEyeSize * 1.1f, pEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "LeftPupil", PrimitiveType.Sphere,
                    eyeBase - sideV * eyeGap + fwd * (pEyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.5f,
                    MakeURPMat("Racer_Plop_Pupil", new Color(0.3f, 0.1f, 0.4f), 0f, 0.9f));
                AddPrimChild(model, "RightEye", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap, Quaternion.identity,
                    new Vector3(pEyeSize, pEyeSize * 1.1f, pEyeSize * 0.7f), whiteMat);
                AddPrimChild(model, "RightPupil", PrimitiveType.Sphere,
                    eyeBase + sideV * eyeGap + fwd * (pEyeSize * 0.3f),
                    Quaternion.identity, Vector3.one * pEyeSize * 0.5f,
                    MakeURPMat("Racer_Plop_Pupil2", new Color(0.3f, 0.1f, 0.4f), 0f, 0.9f));
                // Eyelashes (thin cubes)
                Material lashMat = MakeURPMat("Racer_Plop_Lash", new Color(0.1f, 0.05f, 0.05f), 0f, 0.5f);
                AddPrimChild(model, "LeftLash", PrimitiveType.Cube,
                    eyeBase - sideV * eyeGap + upV * (pEyeSize * 0.5f),
                    Quaternion.Euler(0, 0, -10),
                    new Vector3(pEyeSize * 0.9f, pEyeSize * 0.08f, pEyeSize * 0.3f), lashMat);
                AddPrimChild(model, "RightLash", PrimitiveType.Cube,
                    eyeBase + sideV * eyeGap + upV * (pEyeSize * 0.5f),
                    Quaternion.Euler(0, 0, 10),
                    new Vector3(pEyeSize * 0.9f, pEyeSize * 0.08f, pEyeSize * 0.3f), lashMat);
                break;

            case "TheLog":
                // Small, squinty, unfazed eyes - half-lid
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
                break;

            case "LilSquirt":
                // Huge wild googly eyes, cross-eyed
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
                break;
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

        // === EXPRESSIVE MOUTH WITH JAW GROUPS ===
        Material mouthMat = MakeURPMat("MrCorny_Mouth", new Color(0.15f, 0.02f, 0.02f), 0f, 0.4f);
        Material toothMat = MakeURPMat("MrCorny_Tooth", new Color(0.95f, 0.95f, 0.88f), 0.1f, 0.9f);
        toothMat.EnableKeyword("_EMISSION");
        toothMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.85f) * 0.3f);
        EditorUtility.SetDirty(toothMat);
        Material lipMat = MakeURPMat("MrCorny_Lip", new Color(0.55f, 0.22f, 0.18f), 0f, 0.3f);
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

        Debug.Log($"TTR: Added MrCorny face with expressive mouth+jaws+hypno via ComputeFaceBounds (eyeSize={eyeSize:F2}, " +
            $"frontPos={fb.frontPos}, fwdExt={fb.fwdExt:F2})");
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
        // Step 1: Get world-space bounds of the body mesh only (skip face feature primitives)
        Bounds worldBounds = new Bounds(model.transform.position, Vector3.one * 0.01f);
        bool hasBounds = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string mn = mf.sharedMesh.name;
                if (mn == "Sphere" || mn == "Capsule" || mn == "Cube" ||
                    mn == "Quad" || mn == "Cylinder")
                    continue; // Skip Unity primitives = face features
            }
            if (!hasBounds) { worldBounds = r.bounds; hasBounds = true; }
            else worldBounds.Encapsulate(r.bounds);
        }
        if (!hasBounds)
            worldBounds = new Bounds(model.transform.position, Vector3.one * 0.1f);

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

        Material barrelMat = MakeURPMat("Barrel_Body", new Color(0.2f, 0.55f, 0.12f), 0.3f, 0.4f);
        Material bandMat = MakeURPMat("Barrel_Band", new Color(0.35f, 0.32f, 0.25f), 0.7f, 0.5f);
        Material hazardYellow = MakeURPMat("Barrel_Hazard", new Color(0.95f, 0.8f, 0.1f), 0.1f, 0.3f);
        hazardYellow.EnableKeyword("_EMISSION");
        hazardYellow.SetColor("_EmissionColor", new Color(0.8f, 0.65f, 0.05f) * 0.5f);
        EditorUtility.SetDirty(hazardYellow);
        Material slimeMat = MakeURPMat("Barrel_Slime", new Color(0.15f, 0.8f, 0.1f), 0.2f, 0.9f);
        slimeMat.EnableKeyword("_EMISSION");
        slimeMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.05f) * 2f);
        EditorUtility.SetDirty(slimeMat);
        Material eyeMat = MakeURPMat("Barrel_Eye", new Color(1f, 0.2f, 0.1f), 0f, 0.85f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.05f) * 1.5f);
        EditorUtility.SetDirty(eyeMat);
        Material skullMat = MakeURPMat("Barrel_Skull", new Color(0.9f, 0.88f, 0.8f), 0f, 0.5f);
        Material darkMat = MakeURPMat("Barrel_Dark", new Color(0.06f, 0.06f, 0.04f), 0f, 0.3f);

        // Main barrel body
        AddPrimChild(root, "Barrel", PrimitiveType.Cylinder, Vector3.zero,
            Quaternion.identity, new Vector3(1.4f, 1.2f, 1.4f), barrelMat);
        // Dented top
        AddPrimChild(root, "Dent", PrimitiveType.Sphere, new Vector3(0.2f, 0.7f, 0.15f),
            Quaternion.identity, new Vector3(0.6f, 0.3f, 0.6f), barrelMat);

        // Metal bands
        AddPrimChild(root, "TopBand", PrimitiveType.Cylinder, new Vector3(0, 0.9f, 0),
            Quaternion.identity, new Vector3(1.5f, 0.06f, 1.5f), bandMat);
        AddPrimChild(root, "MidBand", PrimitiveType.Cylinder, new Vector3(0, 0, 0),
            Quaternion.identity, new Vector3(1.5f, 0.06f, 1.5f), bandMat);
        AddPrimChild(root, "BotBand", PrimitiveType.Cylinder, new Vector3(0, -0.9f, 0),
            Quaternion.identity, new Vector3(1.5f, 0.06f, 1.5f), bandMat);

        // Hazard warning stripes on front
        for (int i = 0; i < 3; i++)
        {
            float y = -0.4f + i * 0.4f;
            AddPrimChild(root, $"Stripe{i}", PrimitiveType.Cube,
                new Vector3(0, y, 0.68f), Quaternion.Euler(0, 0, 45),
                new Vector3(0.5f, 0.12f, 0.04f), (i % 2 == 0) ? hazardYellow : darkMat);
        }

        // Skull face painted on barrel
        // Eye sockets (dark circles with glowing red centers)
        AddPrimChild(root, "LeftSocket", PrimitiveType.Sphere,
            new Vector3(-0.22f, 0.25f, 0.7f), Quaternion.identity,
            new Vector3(0.28f, 0.32f, 0.1f), darkMat);
        AddPrimChild(root, "RightSocket", PrimitiveType.Sphere,
            new Vector3(0.22f, 0.25f, 0.7f), Quaternion.identity,
            new Vector3(0.28f, 0.32f, 0.1f), darkMat);
        // Glowing red eyes inside sockets
        AddPrimChild(root, "LeftGlow", PrimitiveType.Sphere,
            new Vector3(-0.22f, 0.25f, 0.72f), Quaternion.identity,
            new Vector3(0.15f, 0.18f, 0.08f), eyeMat);
        AddPrimChild(root, "RightGlow", PrimitiveType.Sphere,
            new Vector3(0.22f, 0.25f, 0.72f), Quaternion.identity,
            new Vector3(0.15f, 0.18f, 0.08f), eyeMat);
        // Nose hole
        AddPrimChild(root, "NoseHole", PrimitiveType.Sphere,
            new Vector3(0, 0.02f, 0.72f), Quaternion.identity,
            new Vector3(0.12f, 0.15f, 0.06f), darkMat);
        // Jagged teeth/grimace
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.12f;
            float h = (i % 2 == 0) ? 0.12f : 0.08f;
            AddPrimChild(root, $"SkullTooth{i}", PrimitiveType.Cube,
                new Vector3(x, -0.22f, 0.71f), Quaternion.identity,
                new Vector3(0.08f, h, 0.04f), skullMat);
        }

        // Oozing slime over the top
        AddPrimChild(root, "SlimeTop", PrimitiveType.Sphere,
            new Vector3(-0.1f, 1.1f, 0.1f), Quaternion.identity,
            new Vector3(0.9f, 0.4f, 0.9f), slimeMat);
        // Dripping slime
        AddPrimChild(root, "Drip1", PrimitiveType.Capsule,
            new Vector3(0.35f, 0.5f, 0.55f), Quaternion.identity,
            new Vector3(0.12f, 0.4f, 0.12f), slimeMat);
        AddPrimChild(root, "Drip2", PrimitiveType.Capsule,
            new Vector3(-0.4f, 0.6f, -0.3f), Quaternion.identity,
            new Vector3(0.1f, 0.35f, 0.1f), slimeMat);
        AddPrimChild(root, "Drip3", PrimitiveType.Capsule,
            new Vector3(0.1f, 0.3f, -0.6f), Quaternion.identity,
            new Vector3(0.09f, 0.3f, 0.09f), slimeMat);

        // Puddle at base
        AddPrimChild(root, "Puddle", PrimitiveType.Sphere,
            new Vector3(0.2f, -1.15f, 0.3f), Quaternion.identity,
            new Vector3(1.1f, 0.15f, 1.1f), slimeMat);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.9f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<ToxicBarrelBehavior>();
        root.transform.localScale = Vector3.one * 1.1f;

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
    /// Spiky sea-urchin mine with angry squinted eyes, grimacing mouth, and warning light.
    /// </summary>
    static GameObject CreateSewerMinePrefab()
    {
        string prefabPath = "Assets/Prefabs/SewerMine.prefab";
        GameObject root = new GameObject("SewerMine");

        Material metalMat = MakeURPMat("Mine_Metal", new Color(0.25f, 0.25f, 0.25f), 0.7f, 0.45f);
        Material rustMat = MakeURPMat("Mine_Rust", new Color(0.45f, 0.25f, 0.12f), 0.4f, 0.3f);
        Material spikeMat = MakeURPMat("Mine_Spike", new Color(0.3f, 0.3f, 0.28f), 0.8f, 0.5f);
        Material eyeMat = MakeURPMat("Mine_Eye", new Color(1f, 0.15f, 0.05f), 0f, 0.9f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.02f) * 2.5f);
        EditorUtility.SetDirty(eyeMat);
        Material warnMat = MakeURPMat("Mine_Warn", new Color(1f, 0.2f, 0.1f), 0f, 0.9f);
        warnMat.EnableKeyword("_EMISSION");
        warnMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.05f) * 3f);
        EditorUtility.SetDirty(warnMat);
        Material mouthMat = MakeURPMat("Mine_Mouth", new Color(0.05f, 0.05f, 0.04f), 0f, 0.3f);
        Material toothMat = MakeURPMat("Mine_Teeth", new Color(0.7f, 0.7f, 0.65f), 0.5f, 0.5f);
        Material chainMat = MakeURPMat("Mine_Chain", new Color(0.35f, 0.3f, 0.25f), 0.6f, 0.4f);

        // Core sphere
        AddPrimChild(root, "Core", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, Vector3.one * 1.6f, metalMat);
        // Rust patches
        AddPrimChild(root, "Rust1", PrimitiveType.Sphere, new Vector3(0.3f, 0.2f, 0.5f),
            Quaternion.identity, Vector3.one * 0.5f, rustMat);
        AddPrimChild(root, "Rust2", PrimitiveType.Sphere, new Vector3(-0.4f, -0.3f, -0.2f),
            Quaternion.identity, Vector3.one * 0.45f, rustMat);

        // Protruding spikes all around (sea urchin style)
        int spikeCount = 0;
        for (int lat = -2; lat <= 2; lat++)
        {
            float latAngle = lat * 35f * Mathf.Deg2Rad;
            int spikesInRing = lat == 0 ? 8 : (Mathf.Abs(lat) == 1 ? 6 : 3);
            for (int i = 0; i < spikesInRing; i++)
            {
                float lonAngle = (i + (lat % 2) * 0.5f) / spikesInRing * Mathf.PI * 2f;
                float y = Mathf.Sin(latAngle) * 0.8f;
                float r = Mathf.Cos(latAngle) * 0.8f;
                Vector3 dir = new Vector3(Mathf.Cos(lonAngle) * r, y, Mathf.Sin(lonAngle) * r);
                Vector3 pos = dir.normalized * 0.75f;
                Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
                float spikeLen = 0.25f + (spikeCount % 3) * 0.08f;
                AddPrimChild(root, $"Spike{spikeCount++}", PrimitiveType.Capsule, pos,
                    rot, new Vector3(0.12f, spikeLen, 0.12f), spikeMat);
            }
        }

        // Angry squinted eyes (thin horizontal slits that glow red)
        AddPrimChild(root, "LeftEye", PrimitiveType.Sphere,
            new Vector3(-0.28f, 0.15f, 0.72f), Quaternion.identity,
            new Vector3(0.25f, 0.1f, 0.1f), eyeMat);
        AddPrimChild(root, "RightEye", PrimitiveType.Sphere,
            new Vector3(0.28f, 0.15f, 0.72f), Quaternion.identity,
            new Vector3(0.25f, 0.1f, 0.1f), eyeMat);
        // Angry brow lines
        AddPrimChild(root, "LeftBrow", PrimitiveType.Cube,
            new Vector3(-0.28f, 0.28f, 0.73f), Quaternion.Euler(0, 0, -20),
            new Vector3(0.28f, 0.05f, 0.05f), metalMat);
        AddPrimChild(root, "RightBrow", PrimitiveType.Cube,
            new Vector3(0.28f, 0.28f, 0.73f), Quaternion.Euler(0, 0, 20),
            new Vector3(0.28f, 0.05f, 0.05f), metalMat);

        // Grimacing mouth with metal teeth
        AddPrimChild(root, "MouthBG", PrimitiveType.Sphere,
            new Vector3(0, -0.15f, 0.72f), Quaternion.identity,
            new Vector3(0.45f, 0.2f, 0.1f), mouthMat);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.09f;
            float h = (i % 2 == 0) ? 0.09f : 0.06f;
            AddPrimChild(root, $"Tooth{i}", PrimitiveType.Cube,
                new Vector3(x, -0.12f + (i % 2 == 0 ? 0 : -0.03f), 0.76f),
                Quaternion.identity, new Vector3(0.06f, h, 0.04f), toothMat);
        }

        // Warning light on top - pulsing red glow
        AddPrimChild(root, "WarnLight", PrimitiveType.Sphere,
            new Vector3(0, 0.9f, 0), Quaternion.identity,
            Vector3.one * 0.2f, warnMat);
        // Light stem
        AddPrimChild(root, "LightStem", PrimitiveType.Cylinder,
            new Vector3(0, 0.8f, 0), Quaternion.identity,
            new Vector3(0.06f, 0.12f, 0.06f), metalMat);

        // Chain hanging below
        for (int i = 0; i < 4; i++)
        {
            AddPrimChild(root, $"Chain{i}", PrimitiveType.Cube,
                new Vector3(0, -0.9f - i * 0.15f, 0),
                Quaternion.Euler(0, i * 90, 0),
                new Vector3(0.12f, 0.08f, 0.06f), chainMat);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.0f;
        root.AddComponent<Obstacle>(); root.tag = "Obstacle";
        root.AddComponent<SewerMineBehavior>();
        root.transform.localScale = Vector3.one * 0.95f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Sewer Mine with angry eyes, spikes, and warning light!");
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

        // Shiny chitin
        Material chitinMat = MakeURPMat("Roach_Chitin", new Color(0.22f, 0.14f, 0.06f), 0.15f, 0.7f);
        Material darkMat = MakeURPMat("Roach_Dark", new Color(0.12f, 0.08f, 0.04f), 0.1f, 0.65f);
        Material bellyMat = MakeURPMat("Roach_Belly", new Color(0.35f, 0.22f, 0.1f), 0.05f, 0.5f);
        Material whiteMat = MakeURPMat("Roach_EyeW", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f));
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
        root.transform.localScale = Vector3.one * 0.85f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Cockroach with panicked googly eyes, antennae, and 6 legs!");
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
        EnsureMaterialsFolder();
        string safeName = m.name.Replace(" ", "_").Replace("/", "_");
        string path = $"Assets/Materials/{safeName}_{_matCounter++}.mat";
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
            AssetDatabase.CreateAsset(outlineMat, $"Assets/Materials/OutlineEdgeDetect_{_matCounter++}.mat");
            outlineMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/OutlineEdgeDetect_{_matCounter - 1}.mat");

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

        // Gallery light (3-point for good model viewing)
        GameObject lightObj = new GameObject("GalleryLight");
        lightObj.transform.SetParent(galleryRoot.transform);
        lightObj.transform.position = stagePos + new Vector3(-2, 4, -3);
        lightObj.transform.LookAt(stagePos);
        Light galleryLight = lightObj.AddComponent<Light>();
        galleryLight.type = LightType.Directional;
        galleryLight.intensity = 2f;
        galleryLight.color = new Color(1f, 0.95f, 0.9f);
        galleryLight.enabled = false;
        gallery.galleryLight = galleryLight;

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
        gallery.RegisterAsset("Sh!tcoin", "Brown Town's official currency. Spin big and shiny like Sonic rings!",
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

        // Bonus Sh!tcoin
        gallery.RegisterAsset("Bonus Sh!tcoin", "Special golden coin only reachable after ramp jumps. Worth 10 Sh!tcoins!",
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

        // === RIGHT-SIDE PUFFY COMIC SCORE HUD (percentage anchors for any resolution) ===
        // Pooper Snooper occupies top strip (0.895-0.995), HUD starts below at 0.84
        // Max X anchor 0.88 to keep outlines well within screen edge

        // "SCORE" label
        Text scoreLabelText = MakeStretchText(canvasObj.transform, "ScoreLabel", "SCORE",
            26, TextAnchor.LowerRight, new Color(1f, 0.85f, 0.4f, 0.8f),
            new Vector2(0.72f, 0.84f), new Vector2(0.88f, 0.87f), true);

        // Score number - big puffy golden text
        Text scoreText = MakeStretchText(canvasObj.transform, "ScoreText", "0",
            64, TextAnchor.UpperRight, new Color(1f, 0.92f, 0.2f),
            new Vector2(0.55f, 0.78f), new Vector2(0.88f, 0.845f), true);
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

        // Distance - right side below score
        Text distanceText = MakeStretchText(canvasObj.transform, "DistanceText", "0m",
            36, TextAnchor.UpperRight, new Color(0.6f, 1f, 0.5f),
            new Vector2(0.68f, 0.73f), new Vector2(0.88f, 0.78f), true);
        {
            Outline dOut = distanceText.gameObject.AddComponent<Outline>();
            dOut.effectColor = new Color(0f, 0.2f, 0f, 0.8f);
            dOut.effectDistance = new Vector2(2, -2);
        }

        // Combo counter (center screen, hidden until active) - extra large and punchy
        Text comboText = MakeText(canvasObj.transform, "ComboText", "",
            68, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.2f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(600, 100), true);
        Outline comboOutline2 = comboText.gameObject.AddComponent<Outline>();
        comboOutline2.effectColor = new Color(0, 0, 0, 0.6f);
        comboOutline2.effectDistance = new Vector2(-3, 3);
        comboText.gameObject.SetActive(false);

        // Start Panel
        GameObject startPanel = MakePanel(canvasObj.transform, "StartPanel",
            new Color(0.04f, 0.07f, 0.03f, 0.93f),
            new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));

        Text titleText = MakeStretchText(startPanel.transform, "Title", "TURD\nTUNNEL\nRUSH",
            88, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.1f),
            new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.95f), true);
        // Double outline for thick comic look
        Outline titleOutline2 = titleText.gameObject.AddComponent<Outline>();
        titleOutline2.effectColor = new Color(0.4f, 0.2f, 0f, 0.9f);
        titleOutline2.effectDistance = new Vector2(4, -4);

        MakeStretchText(startPanel.transform, "Subtitle", "Sewer Surf Showdown",
            32, TextAnchor.MiddleCenter, new Color(0.5f, 0.85f, 0.35f),
            new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.5f), true);

        Button startButton = MakeButton(startPanel.transform, "StartButton", "FLUSH!",
            46, new Color(0.15f, 0.55f, 0.1f), Color.white,
            new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.3f));

        MakeStretchText(startPanel.transform, "HintText", "or press SPACE",
            22, TextAnchor.MiddleCenter, new Color(0.45f, 0.45f, 0.4f),
            new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.11f), false);

        // Wallet display on start screen
        Text startWalletText = MakeStretchText(startPanel.transform, "StartWallet", "0 coins",
            24, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.5f, 0.88f), new Vector2(0.95f, 0.97f), false);

        // Daily challenge on start screen
        Text challengeText = MakeStretchText(startPanel.transform, "ChallengeText", "DAILY: ...",
            20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
            new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.38f), false);

        // Shop button on start screen (left half)
        Button shopButton = MakeButton(startPanel.transform, "ShopButton", "SHOP",
            28, new Color(0.6f, 0.45f, 0.1f), Color.white,
            new Vector2(0.05f, 0.0f), new Vector2(0.48f, 0.06f));

        // Gallery button on start screen (right half)
        Button galleryButton = MakeButton(startPanel.transform, "GalleryButton", "GALLERY",
            28, new Color(0.15f, 0.4f, 0.55f), Color.white,
            new Vector2(0.52f, 0.0f), new Vector2(0.95f, 0.06f));

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

        // Multiplier text (left side, below tracker)
        Text multiplierText = MakeStretchText(canvasObj.transform, "MultiplierText", "x1.0",
            42, TextAnchor.MiddleLeft, new Color(1f, 1f, 0.7f),
            new Vector2(0.02f, 0.78f), new Vector2(0.18f, 0.84f), true);
        multiplierText.gameObject.SetActive(false);

        // "SH!TCOINS" label
        Text coinLabel = MakeStretchText(canvasObj.transform, "CoinLabel", "SH!TCOINS",
            20, TextAnchor.LowerRight, new Color(0.85f, 0.6f, 0.15f, 0.85f),
            new Vector2(0.68f, 0.675f), new Vector2(0.88f, 0.71f), true);

        // HUD coin counter - puffy copper style, right side below distance
        Text coinCountText = MakeStretchText(canvasObj.transform, "CoinCountText", "0",
            46, TextAnchor.UpperRight, new Color(1f, 0.75f, 0.25f),
            new Vector2(0.62f, 0.61f), new Vector2(0.88f, 0.68f), true);
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

        // HUD wallet text (left side, below tracker)
        Text walletText = MakeStretchText(canvasObj.transform, "WalletText", "0",
            28, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.02f, 0.84f), new Vector2(0.18f, 0.875f), true);

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

        return ui;
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

        return btn;
    }
}
