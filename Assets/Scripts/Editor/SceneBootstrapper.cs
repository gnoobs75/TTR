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

    [MenuItem("TTR/Setup Game Scene")]
    public static void SetupGameScene()
    {
        // Ensure asset database is up to date (picks up newly exported GLBs)
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Start with a completely fresh scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        _font = GetFont();
        _urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (_urpLit == null)
            _urpLit = Shader.Find("Standard"); // fallback
        _matCounter = 0;

        // Clean old generated materials
        if (AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.DeleteAsset("Assets/Materials");
        AssetDatabase.CreateFolder("Assets", "Materials");

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
        CreatePrefabs();
        CreateScenerySpawner(player.transform);
        CreatePowerUpSpawner(player.transform);
        CreateSmoothSnake(pipeGenObj.GetComponent<PipeGenerator>());
        CreateWaterCreatureSpawner(player.transform);
        CreateWaterAnimator(player.transform, pipeGenObj);
        CreateSewerWaterEffects(player.transform, pipeGenObj);
        CreateBrownStreakTrail(player.transform);
        CreatePooperSnooper(player.GetComponent<TurdController>());
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

            // Remove imported colliders (we add our own on root)
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // Upgrade materials to URP (preserves textures for corn kernels!)
            UpgradeToURP(model);

            // Add big cartoonish googly eyes and handlebar mustache
            AddMrCornyFace(model);

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
            AddMrCornyFace(capsule);
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

        // Forward spotlight - warm headlamp illuminating the path
        GameObject spotObj = new GameObject("PlayerSpotlight");
        spotObj.transform.SetParent(player.transform);
        spotObj.transform.localPosition = new Vector3(0, 0.3f, 0.5f);
        spotObj.transform.localRotation = Quaternion.identity;
        Light spotLight = spotObj.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.color = new Color(0.95f, 0.85f, 0.6f); // warm, bright
        spotLight.intensity = 4f;
        spotLight.range = 35f;
        spotLight.spotAngle = 70f;
        spotLight.innerSpotAngle = 40f;

        // Ambient glow around player
        GameObject glowObj = new GameObject("PlayerGlow");
        glowObj.transform.SetParent(player.transform);
        glowObj.transform.localPosition = Vector3.zero;
        Light glowLight = glowObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.65f, 0.55f, 0.35f); // warm
        glowLight.intensity = 2.5f;
        glowLight.range = 18f;

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

        // Sewer pipe - warm brownish concrete with subtle green emissive accents
        Material pipeMat = MakeURPMat("SewerPipe_Mat", new Color(0.25f, 0.2f, 0.16f), 0.05f, 0.35f);
        pipeMat.EnableKeyword("_EMISSION");
        pipeMat.SetColor("_EmissionColor", new Color(0.05f, 0.12f, 0.03f) * 0.5f);
        EditorUtility.SetDirty(pipeMat);
        gen.pipeMaterial = pipeMat;

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
        pipeCam.followDistance = 4f;         // behind player
        pipeCam.heightAbovePlayer = 1.2f;    // above player toward pipe center
        pipeCam.lookAhead = 5f;              // look forward
        pipeCam.pipeRadius = 3.5f;
        pipeCam.angleLag = 3f;               // camera angle lags so steering is visible
        pipeCam.baseFOV = 68f;               // wide for speed feel
        pipeCam.speedFOVBoost = 8f;          // more FOV at high speed

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

    // ===== LIGHTING =====
    static void CreateLighting()
    {
        // Main directional - bright, warm, cartoon-like (think Mario Kart underground)
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.85f, 0.75f, 0.55f); // warm golden
        light.intensity = 1.2f; // bright enough to see everything clearly
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.3f; // soft, cartoon-friendly shadows
        lightObj.transform.rotation = Quaternion.Euler(45, -30, 0);

        // Fill light from below - illuminates inside of pipe (removes harsh shadows)
        GameObject fillObj = new GameObject("Fill Light");
        Light fill = fillObj.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.35f, 0.4f, 0.25f); // green sewer fill
        fill.intensity = 0.5f; // stronger fill for visibility
        fill.shadows = LightShadows.None;
        fillObj.transform.rotation = Quaternion.Euler(-30, 45, 0);

        // Back fill - prevents anything from being pitch black
        GameObject backObj = new GameObject("Back Fill Light");
        Light back = backObj.AddComponent<Light>();
        back.type = LightType.Directional;
        back.color = new Color(0.3f, 0.25f, 0.2f);
        back.intensity = 0.3f;
        back.shadows = LightShadows.None;
        backObj.transform.rotation = Quaternion.Euler(15, 160, 0);

        // Ambient - bright and warm, cartoonish (not muddy dark)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.3f, 0.2f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.16f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.17f, 0.1f);

        // Fog - lighter, see further, warmer tint
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.05f);
        RenderSettings.fogDensity = 0.004f; // much less fog - see obstacles clearly
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
                SetVolumeParam(bloom, "threshold", 0.7f);  // bloom on brighter things only
                SetVolumeParam(bloom, "intensity", 1.8f);   // moderate bloom, not overwhelming
                SetVolumeParam(bloom, "scatter", 0.7f);     // medium spread
                Debug.Log("TTR: Bloom added to post-processing");
            }

            if (vignetteType != null)
            {
                var vignette = (VolumeComponent)profile.Add(vignetteType);
                SetVolumeParam(vignette, "intensity", 0.3f);  // subtle edge darkening
                SetVolumeParam(vignette, "smoothness", 0.5f);
                SetVolumeParamColor(vignette, "color", new Color(0.03f, 0.04f, 0.02f));
                Debug.Log("TTR: Vignette added to post-processing");
            }

            if (colorAdjType != null)
            {
                var colorAdj = (VolumeComponent)profile.Add(colorAdjType);
                SetVolumeParam(colorAdj, "saturation", 30f);   // punchy, cartoonish colors
                SetVolumeParam(colorAdj, "contrast", 20f);     // strong depth like Mario Kart
                SetVolumeParam(colorAdj, "postExposure", 0.4f); // brighter overall
                Debug.Log("TTR: Color adjustments added to post-processing");
            }

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
                new Vector3(0.75f, 0.75f, 0.75f), PrimitiveType.Capsule, new Color(0.4f, 0.3f, 0.18f), true));
        else
            obstaclePrefabs.Add(CreateSewerRatPrefab());

        GameObject barrelModel = LoadModel("Assets/Models/ToxicBarrel.fbx");
        if (barrelModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("ToxicBarrel", "Assets/Models/ToxicBarrel.fbx",
                new Vector3(1f, 1f, 1f), PrimitiveType.Cylinder, new Color(0.15f, 0.5f, 0.1f), true));
        else
            obstaclePrefabs.Add(CreateToxicBarrelPrefab());

        GameObject blobModel = LoadModel("Assets/Models/PoopBlob.fbx");
        if (blobModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("PoopBlob", "Assets/Models/PoopBlob.fbx",
                new Vector3(1.1f, 0.8f, 1.1f), PrimitiveType.Sphere, new Color(0.4f, 0.25f, 0.1f), true));
        else
            obstaclePrefabs.Add(CreatePoopBlobPrefab());

        GameObject mineModel = LoadModel("Assets/Models/SewerMine.fbx");
        if (mineModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("SewerMine", "Assets/Models/SewerMine.fbx",
                new Vector3(0.85f, 0.85f, 0.85f), PrimitiveType.Sphere, new Color(0.25f, 0.25f, 0.25f), true));
        else
            obstaclePrefabs.Add(CreateSewerMinePrefab());

        GameObject roachModel = LoadModel("Assets/Models/Cockroach.fbx");
        if (roachModel != null)
            obstaclePrefabs.Add(EnsureModelPrefab("Cockroach", "Assets/Models/Cockroach.fbx",
                new Vector3(0.7f, 0.5f, 0.7f), PrimitiveType.Capsule, new Color(0.22f, 0.14f, 0.06f), true));
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

        // Corn Coin - BIG bright gold
        GameObject coinPrefab = EnsureModelPrefab("CornCoin", "Assets/Models/CornCoin.fbx",
            new Vector3(1.2f, 1.2f, 1.2f), PrimitiveType.Sphere,
            new Color(1f, 0.85f, 0.1f), false);

        // Force ALL coin materials to bright emissive gold, saved to disk
        if (coinPrefab != null)
        {
            GameObject coinInstance = (GameObject)Object.Instantiate(coinPrefab);
            Material goldMat = MakeURPMat("CornCoin_Gold", new Color(1f, 0.85f, 0.15f), 0.85f, 0.75f);
            goldMat.EnableKeyword("_EMISSION");
            goldMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 2.5f);
            // Re-save since we modified after MakeURPMat saved it
            EditorUtility.SetDirty(goldMat);

            foreach (Renderer r in coinInstance.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int mi = 0; mi < mats.Length; mi++)
                    mats[mi] = goldMat;
                r.sharedMaterials = mats;
            }

            PrefabUtility.SaveAsPrefabAsset(coinInstance, "Assets/Prefabs/CornCoin.prefab");
            Object.DestroyImmediate(coinInstance);
            coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CornCoin.prefab");
        }

        // Wire to spawner
        ObstacleSpawner spawner = Object.FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null)
        {
            spawner.obstaclePrefabs = obstaclePrefabs.ToArray();
            spawner.coinPrefab = coinPrefab;
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

        // Gross pipe character prefabs (slime, worms, stains, graffiti, bubbles, mold)
        List<GameObject> grossPrefabs = new List<GameObject>();
        grossPrefabs.Add(CreateSlimeDripPrefab());
        grossPrefabs.Add(CreateGrimeStainPrefab());
        grossPrefabs.Add(CreateWormClusterPrefab());
        grossPrefabs.Add(CreateMoldPatchPrefab());
        grossPrefabs.Add(CreateBubbleClusterPrefab());
        grossPrefabs.Add(CreateGraffitiSignPrefab());
        grossPrefabs.Add(CreateCrackDecalPrefab());
        grossPrefabs.Add(CreateRustDripPrefab());

        // Pipe structure variety (side pipes, waterfalls, large rust, sewer ads)
        grossPrefabs.Add(CreateSidePipePrefab());
        grossPrefabs.Add(CreateWaterfallPipePrefab());
        grossPrefabs.Add(CreateLargeRustPatchPrefab());
        grossPrefabs.Add(CreateSewerAdPrefab());

        spawner.grossPrefabs = grossPrefabs.ToArray();
        Debug.Log("TTR: Created 12 gross pipe character prefab types for full sewer atmosphere!");
    }

    // ===== GROSS PIPE CHARACTER PREFABS =====

    /// <summary>Green slime drip hanging from pipe surface with gooey droplets.</summary>
    static GameObject CreateSlimeDripPrefab()
    {
        string path = "Assets/Prefabs/SlimeDrip_Gross.prefab";
        GameObject root = new GameObject("SlimeDrip");

        Material slimeMat = MakeURPMat("Gross_Slime", new Color(0.15f, 0.55f, 0.08f), 0.1f, 0.92f);
        slimeMat.EnableKeyword("_EMISSION");
        slimeMat.SetColor("_EmissionColor", new Color(0.08f, 0.35f, 0.04f) * 0.8f);
        EditorUtility.SetDirty(slimeMat);

        // Main drip blob on wall
        AddPrimChild(root, "Blob", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.25f, 0.15f, 0.2f), slimeMat);
        // Dripping strand hanging down
        AddPrimChild(root, "Strand1", PrimitiveType.Capsule, new Vector3(0, -0.2f, 0),
            Quaternion.identity, new Vector3(0.06f, 0.2f, 0.06f), slimeMat);
        AddPrimChild(root, "Strand2", PrimitiveType.Capsule, new Vector3(0.06f, -0.35f, 0.02f),
            Quaternion.identity, new Vector3(0.04f, 0.12f, 0.04f), slimeMat);
        // Droplet at tip
        AddPrimChild(root, "Drop", PrimitiveType.Sphere, new Vector3(0.04f, -0.48f, 0.01f),
            Quaternion.identity, Vector3.one * 0.07f, slimeMat);

        RemoveAllColliders(root);
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

        // Irregular splatter from overlapping flat spheres
        AddPrimChild(root, "Splat0", PrimitiveType.Sphere, Vector3.zero,
            Quaternion.identity, new Vector3(0.35f, 0.05f, 0.3f), grimeMat);
        AddPrimChild(root, "Splat1", PrimitiveType.Sphere, new Vector3(0.12f, 0, 0.08f),
            Quaternion.Euler(0, 25, 0), new Vector3(0.2f, 0.04f, 0.25f), grimeMat);
        AddPrimChild(root, "Splat2", PrimitiveType.Sphere, new Vector3(-0.1f, 0, -0.06f),
            Quaternion.Euler(0, -15, 0), new Vector3(0.18f, 0.04f, 0.22f), grimeMat);

        RemoveAllColliders(root);
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
        root.transform.localScale = Vector3.one * 1.3f;
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
    static GameObject CreateGraffitiSignPrefab()
    {
        string path = "Assets/Prefabs/GraffitiSign_Gross.prefab";
        GameObject root = new GameObject("GraffitiSign");

        // Faded paint background
        Material signMat = MakeURPMat("Gross_Sign", new Color(0.6f, 0.55f, 0.4f), 0f, 0.2f);
        Material paintMat = MakeURPMat("Gross_Paint", new Color(0.8f, 0.2f, 0.15f), 0f, 0.15f);
        paintMat.EnableKeyword("_EMISSION");
        paintMat.SetColor("_EmissionColor", new Color(0.4f, 0.1f, 0.05f) * 0.3f);
        EditorUtility.SetDirty(paintMat);
        Material frameMat = MakeURPMat("Gross_Frame", new Color(0.3f, 0.28f, 0.22f), 0.4f, 0.3f);

        // Sign plate
        AddPrimChild(root, "Plate", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(0.4f, 0.25f, 0.02f), signMat);
        // Frame edges
        AddPrimChild(root, "Top", PrimitiveType.Cube, new Vector3(0, 0.12f, 0),
            Quaternion.identity, new Vector3(0.42f, 0.02f, 0.03f), frameMat);
        AddPrimChild(root, "Bot", PrimitiveType.Cube, new Vector3(0, -0.12f, 0),
            Quaternion.identity, new Vector3(0.42f, 0.02f, 0.03f), frameMat);
        // Red paint stripes (crude graffiti)
        AddPrimChild(root, "Stripe1", PrimitiveType.Cube, new Vector3(-0.05f, 0.02f, 0.012f),
            Quaternion.Euler(0, 0, -20), new Vector3(0.25f, 0.03f, 0.01f), paintMat);
        AddPrimChild(root, "Stripe2", PrimitiveType.Cube, new Vector3(0.05f, -0.02f, 0.012f),
            Quaternion.Euler(0, 0, 15), new Vector3(0.2f, 0.025f, 0.01f), paintMat);
        // Drip from paint
        AddPrimChild(root, "Drip", PrimitiveType.Capsule, new Vector3(0.1f, -0.08f, 0.012f),
            Quaternion.identity, new Vector3(0.015f, 0.05f, 0.015f), paintMat);

        RemoveAllColliders(root);
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
    static GameObject CreateSewerAdPrefab()
    {
        string path = "Assets/Prefabs/SewerAd_Gross.prefab";
        GameObject root = new GameObject("SewerAd");

        // Billboard frame
        Material frameMat = MakeURPMat("Gross_AdFrame", new Color(0.3f, 0.28f, 0.25f), 0.4f, 0.3f);
        Material boardMat = MakeURPMat("Gross_AdBoard", new Color(0.85f, 0.8f, 0.65f), 0.02f, 0.15f);
        boardMat.EnableKeyword("_EMISSION");
        boardMat.SetColor("_EmissionColor", new Color(0.15f, 0.13f, 0.08f));
        EditorUtility.SetDirty(boardMat);

        // Background board
        AddPrimChild(root, "Board", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(0.5f, 0.35f, 0.01f), boardMat);
        // Frame border
        AddPrimChild(root, "FrameTop", PrimitiveType.Cube, new Vector3(0, 0.18f, 0.005f),
            Quaternion.identity, new Vector3(0.52f, 0.02f, 0.02f), frameMat);
        AddPrimChild(root, "FrameBot", PrimitiveType.Cube, new Vector3(0, -0.18f, 0.005f),
            Quaternion.identity, new Vector3(0.52f, 0.02f, 0.02f), frameMat);
        AddPrimChild(root, "FrameL", PrimitiveType.Cube, new Vector3(-0.26f, 0, 0.005f),
            Quaternion.identity, new Vector3(0.02f, 0.38f, 0.02f), frameMat);
        AddPrimChild(root, "FrameR", PrimitiveType.Cube, new Vector3(0.26f, 0, 0.005f),
            Quaternion.identity, new Vector3(0.02f, 0.38f, 0.02f), frameMat);

        // "Ad content" - colored blocks suggesting text/images (procedural)
        Material titleMat = MakeURPMat("Gross_AdTitle", new Color(0.6f, 0.2f, 0.1f), 0f, 0.2f);
        Material textMat = MakeURPMat("Gross_AdText", new Color(0.3f, 0.25f, 0.2f), 0f, 0.15f);
        Material accentMat = MakeURPMat("Gross_AdAccent", new Color(0.8f, 0.6f, 0.1f), 0f, 0.3f);
        accentMat.EnableKeyword("_EMISSION");
        accentMat.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.02f));
        EditorUtility.SetDirty(accentMat);

        // Title bar
        AddPrimChild(root, "Title", PrimitiveType.Cube, new Vector3(0, 0.1f, 0.006f),
            Quaternion.identity, new Vector3(0.4f, 0.06f, 0.002f), titleMat);
        // "Text" lines
        for (int i = 0; i < 3; i++)
        {
            float w = 0.35f - i * 0.05f;
            AddPrimChild(root, $"TextLine{i}", PrimitiveType.Cube,
                new Vector3(-0.02f + i * 0.01f, -0.02f - i * 0.05f, 0.006f),
                Quaternion.identity, new Vector3(w, 0.02f, 0.001f), textMat);
        }
        // Poop emoji icon (brown circle)
        Material poopIconMat = MakeURPMat("Gross_PoopIcon", new Color(0.4f, 0.25f, 0.1f), 0f, 0.4f);
        AddPrimChild(root, "PoopIcon", PrimitiveType.Sphere, new Vector3(0.15f, -0.05f, 0.008f),
            Quaternion.identity, new Vector3(0.08f, 0.1f, 0.01f), poopIconMat);
        // Star burst accent
        AddPrimChild(root, "Star", PrimitiveType.Sphere, new Vector3(-0.15f, 0.1f, 0.008f),
            Quaternion.identity, Vector3.one * 0.05f, accentMat);
        // Grime/stain on the ad (it's in a sewer after all)
        Material grimeMat = MakeURPMat("Gross_AdGrime", new Color(0.15f, 0.12f, 0.08f, 0.6f), 0f, 0.5f);
        AddPrimChild(root, "Grime", PrimitiveType.Sphere, new Vector3(0.08f, -0.1f, 0.007f),
            Quaternion.identity, new Vector3(0.12f, 0.08f, 0.003f), grimeMat);

        RemoveAllColliders(root);
        root.transform.localScale = Vector3.one * 2.5f;
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
            Debug.Log($"TTR: Loaded scenery {name} from Blender model");
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
    }

    static GameObject CreateSpeedBoostPrefab()
    {
        string prefabPath = "Assets/Prefabs/SpeedBoost.prefab";
        GameObject root = new GameObject("SpeedBoost");

        // Glowing cyan pad - VERY bright and large
        Material boostMat = MakeURPMat("SpeedBoost_Mat", new Color(0.1f, 0.9f, 1f), 0.4f, 0.9f);
        boostMat.EnableKeyword("_EMISSION");
        boostMat.SetColor("_EmissionColor", new Color(0.3f, 1.2f, 1.5f) * 4f);
        EditorUtility.SetDirty(boostMat);

        // Base pad - big and visible
        AddPrimChild(root, "Pad", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(2.5f, 0.15f, 3f), boostMat);

        // Raised side rails (helps player see it from a distance)
        Material railMat = MakeURPMat("SpeedBoost_Rail", new Color(0.05f, 0.5f, 0.8f), 0.3f, 0.7f);
        railMat.EnableKeyword("_EMISSION");
        railMat.SetColor("_EmissionColor", new Color(0.1f, 0.7f, 1f) * 2f);
        EditorUtility.SetDirty(railMat);

        AddPrimChild(root, "LeftRail", PrimitiveType.Cube,
            new Vector3(-1.1f, 0.2f, 0), Quaternion.identity,
            new Vector3(0.15f, 0.35f, 3f), railMat);
        AddPrimChild(root, "RightRail", PrimitiveType.Cube,
            new Vector3(1.1f, 0.2f, 0), Quaternion.identity,
            new Vector3(0.15f, 0.35f, 3f), railMat);

        // Arrow chevrons - big bright white arrows
        Material arrowMat = MakeURPMat("SpeedBoost_Arrow", new Color(1f, 1f, 1f), 0.6f, 0.95f);
        arrowMat.EnableKeyword("_EMISSION");
        arrowMat.SetColor("_EmissionColor", Color.white * 5f);
        EditorUtility.SetDirty(arrowMat);

        for (int i = 0; i < 4; i++)
        {
            float z = -0.9f + i * 0.6f;
            AddPrimChild(root, $"Arrow{i}", PrimitiveType.Cube,
                new Vector3(0, 0.12f, z), Quaternion.Euler(0, 0, 45),
                new Vector3(0.7f, 0.06f, 0.18f), arrowMat);
        }

        // Trigger collider
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(3f, 1.5f, 3.5f);

        root.AddComponent<SpeedBoost>();
        root.transform.localScale = Vector3.one * 1.1f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("TTR: Created Speed Boost prefab");
        return prefab;
    }

    static GameObject CreateJumpRampPrefab()
    {
        string prefabPath = "Assets/Prefabs/JumpRamp.prefab";
        GameObject root = new GameObject("JumpRamp");

        // Orange angled ramp
        Material rampMat = MakeURPMat("JumpRamp_Mat", new Color(0.9f, 0.5f, 0.1f), 0.2f, 0.5f);
        rampMat.EnableKeyword("_EMISSION");
        rampMat.SetColor("_EmissionColor", new Color(0.8f, 0.4f, 0.05f) * 1.5f);
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

    // ===== SMOOTH SNAKE AI RACER =====
    static void CreateSmoothSnake(PipeGenerator pipeGen)
    {
        // Empty root for AI mechanics (same pattern as player - prevents rotation bugs)
        GameObject snake = new GameObject("SmoothSnake");
        snake.transform.position = new Vector3(1f, -3f, -5f);

        // Load visual model as child
        string modelPath = "Assets/Models/MrCorny.fbx";
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

        if (modelAsset != null)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(snake.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 0.16f; // slightly smaller than player

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

        // Brighter glow so AI is visible
        GameObject glowObj = new GameObject("SnakeGlow");
        glowObj.transform.SetParent(snake.transform);
        glowObj.transform.localPosition = new Vector3(0, 0.2f, 0.5f);
        Light glow = glowObj.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.9f, 0.4f, 0.15f); // bright orange glow
        glow.intensity = 1.5f;
        glow.range = 10f;

        Debug.Log("TTR: Created Smooth Snake AI racer (dark MrCorny model, root/child)!");
    }

    static void AddSmugFace(GameObject snake)
    {
        // Same bounds-based approach as AddMrCornyFace but with half-lidded smug eyes
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
        bool first = true;
        foreach (Renderer r in snake.GetComponentsInChildren<Renderer>())
        {
            Bounds wb = r.bounds;
            Vector3 localMin = snake.transform.InverseTransformPoint(wb.min);
            Vector3 localMax = snake.transform.InverseTransformPoint(wb.max);
            Bounds lb = new Bounds((localMin + localMax) * 0.5f,
                new Vector3(Mathf.Abs(localMax.x - localMin.x),
                            Mathf.Abs(localMax.y - localMin.y),
                            Mathf.Abs(localMax.z - localMin.z)));
            if (first) { localBounds = lb; first = false; }
            else localBounds.Encapsulate(lb);
        }

        Vector3 lc = localBounds.center;
        Vector3 ls = localBounds.size;
        int longAxis = 2;
        if (ls.x > ls.y && ls.x > ls.z) longAxis = 0;
        else if (ls.y > ls.x && ls.y > ls.z) longAxis = 1;

        Vector3 fwd, upV, sideV;
        float fwdExt, upExt, sideExt;
        switch (longAxis)
        {
            case 0: fwd = Vector3.right; upV = Vector3.up; sideV = Vector3.forward;
                fwdExt = ls.x * 0.5f; upExt = ls.y * 0.5f; sideExt = ls.z * 0.5f; break;
            case 1: fwd = Vector3.up; upV = Vector3.forward; sideV = Vector3.right;
                fwdExt = ls.y * 0.5f; upExt = ls.z * 0.5f; sideExt = ls.x * 0.5f; break;
            default: fwd = Vector3.forward; upV = Vector3.up; sideV = Vector3.right;
                fwdExt = ls.z * 0.5f; upExt = ls.y * 0.5f; sideExt = ls.x * 0.5f; break;
        }

        Material whiteMat = MakeURPMat("SmoothSnake_EyeWhite", Color.white, 0f, 0.9f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(whiteMat);
        Material pupilMat = MakeURPMat("SmoothSnake_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.95f);
        Material lidMat = MakeURPMat("SmoothSnake_Lid", new Color(0.15f, 0.08f, 0.03f), 0f, 0.6f);

        Vector3 frontPos = lc + fwd * fwdExt * 0.85f;
        float eyeGap = Mathf.Max(sideExt * 0.28f, 0.1f);
        float eyeSize = Mathf.Max(sideExt, upExt) * 0.4f;
        eyeSize = Mathf.Clamp(eyeSize, 0.15f, 1.0f);
        Vector3 eyeBase = frontPos + upV * (upExt * 0.3f);

        // Smug narrowed eyes
        GameObject leftEye = AddPrimChild(snake, "LeftEye", PrimitiveType.Sphere,
            eyeBase - sideV * eyeGap, Quaternion.identity,
            new Vector3(eyeSize, eyeSize * 0.7f, eyeSize * 0.8f), whiteMat); // squished vertically = smug
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.5f, pupilMat);
        // Heavy eyelid
        AddPrimChild(snake, "LeftLid", PrimitiveType.Sphere,
            eyeBase - sideV * eyeGap + upV * (eyeSize * 0.25f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.1f, eyeSize * 0.35f, eyeSize * 0.9f), lidMat);

        GameObject rightEye = AddPrimChild(snake, "RightEye", PrimitiveType.Sphere,
            eyeBase + sideV * eyeGap, Quaternion.identity,
            new Vector3(eyeSize, eyeSize * 0.7f, eyeSize * 0.8f), whiteMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.5f, pupilMat);
        AddPrimChild(snake, "RightLid", PrimitiveType.Sphere,
            eyeBase + sideV * eyeGap + upV * (eyeSize * 0.25f),
            Quaternion.identity,
            new Vector3(eyeSize * 1.1f, eyeSize * 0.35f, eyeSize * 0.9f), lidMat);

        // Smirk mouth
        Material mouthMat = MakeURPMat("SmoothSnake_Mouth", new Color(0.5f, 0.1f, 0.1f), 0f, 0.5f);
        Vector3 mouthPos = frontPos - upV * (upExt * 0.1f) + sideV * (eyeGap * 0.2f);
        AddPrimChild(snake, "Mouth", PrimitiveType.Capsule,
            mouthPos, Quaternion.Euler(0, 0, 15),
            new Vector3(eyeSize * 0.15f, eyeSize * 0.5f, eyeSize * 0.15f), mouthMat);
    }

    // ===== MR. CORNY FACE FEATURES =====
    /// <summary>
    /// Adds comical googly eyes and handlebar mustache to Mr. Corny.
    /// Big, cartoonish, Mario-style features for maximum personality.
    /// Features are children of the model so they move/rotate with it.
    /// </summary>
    static void AddMrCornyFace(GameObject model)
    {
        // Bright white emissive eyes - always visible even in dark sewer
        Material whiteMat = MakeURPMat("MrCorny_EyeWhite", Color.white, 0f, 0.85f);
        whiteMat.EnableKeyword("_EMISSION");
        whiteMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
        EditorUtility.SetDirty(whiteMat);

        Material pupilMat = MakeURPMat("MrCorny_Pupil", new Color(0.02f, 0.02f, 0.02f), 0f, 0.9f);
        Material stacheMat = MakeURPMat("MrCorny_Stache", new Color(0.25f, 0.14f, 0.06f), 0f, 0.3f);

        // Use LOCAL bounds from all renderers
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
        bool first = true;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Bounds wb = r.bounds;
            Vector3 localMin = model.transform.InverseTransformPoint(wb.min);
            Vector3 localMax = model.transform.InverseTransformPoint(wb.max);
            Bounds lb = new Bounds((localMin + localMax) * 0.5f,
                new Vector3(Mathf.Abs(localMax.x - localMin.x),
                            Mathf.Abs(localMax.y - localMin.y),
                            Mathf.Abs(localMax.z - localMin.z)));
            if (first) { localBounds = lb; first = false; }
            else localBounds.Encapsulate(lb);
        }

        Vector3 lc = localBounds.center;
        Vector3 ls = localBounds.size;

        Debug.Log($"TTR: MrCorny local bounds center={lc}, size={ls}");

        // Find LONGEST axis (turd is elongated along its body axis)
        int longAxis = 2; // Z default
        if (ls.x > ls.y && ls.x > ls.z) longAxis = 0;
        else if (ls.y > ls.x && ls.y > ls.z) longAxis = 1;

        Debug.Log($"TTR: MrCorny longest axis = {(longAxis == 0 ? "X" : longAxis == 1 ? "Y" : "Z")}");

        // Compute face position vectors based on longest axis
        Vector3 fwd, upV, sideV;
        float fwdExt, upExt, sideExt;
        switch (longAxis)
        {
            case 0: fwd = Vector3.right; upV = Vector3.up; sideV = Vector3.forward;
                fwdExt = ls.x * 0.5f; upExt = ls.y * 0.5f; sideExt = ls.z * 0.5f; break;
            case 1: fwd = Vector3.up; upV = Vector3.forward; sideV = Vector3.right;
                fwdExt = ls.y * 0.5f; upExt = ls.z * 0.5f; sideExt = ls.x * 0.5f; break;
            default: fwd = Vector3.forward; upV = Vector3.up; sideV = Vector3.right;
                fwdExt = ls.z * 0.5f; upExt = ls.y * 0.5f; sideExt = ls.x * 0.5f; break;
        }

        // Front of model - push features slightly INTO the surface for connection
        Vector3 frontPos = lc + fwd * fwdExt * 0.85f;
        float eyeGap = Mathf.Max(sideExt * 0.35f, 0.15f);
        // BIGGER eyes for cartoon Mario Kart style
        float eyeSize = Mathf.Max(sideExt, upExt) * 0.6f;
        eyeSize = Mathf.Clamp(eyeSize, 0.2f, 1.5f);

        // === BIG GOOGLY EYES ===
        Vector3 eyeBase = frontPos + upV * (upExt * 0.35f);

        // Left eye (slightly bigger for goofiness)
        GameObject leftEye = AddPrimChild(model, "LeftGooglyEye", PrimitiveType.Sphere,
            eyeBase - sideV * eyeGap, Quaternion.identity,
            Vector3.one * eyeSize, whiteMat);
        AddPrimChild(leftEye, "LeftPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.05f, Quaternion.identity,
            Vector3.one * 0.4f, pupilMat);

        // Right eye (slightly different size for asymmetric googly look)
        GameObject rightEye = AddPrimChild(model, "RightGooglyEye", PrimitiveType.Sphere,
            eyeBase + sideV * eyeGap + upV * (eyeSize * 0.15f),
            Quaternion.identity, Vector3.one * eyeSize * 0.88f, whiteMat);
        AddPrimChild(rightEye, "RightPupil", PrimitiveType.Sphere,
            fwd * 0.35f - upV * 0.07f + sideV * 0.05f, Quaternion.identity,
            Vector3.one * 0.45f, pupilMat);

        // === HANDLEBAR MUSTACHE ===
        Vector3 stacheBase = frontPos - upV * (upExt * 0.05f);
        float stacheThick = eyeSize * 0.22f;

        AddPrimChild(model, "StacheLeft", PrimitiveType.Capsule,
            stacheBase - sideV * (eyeGap * 0.5f),
            Quaternion.Euler(0, 0, -30),
            new Vector3(stacheThick, stacheThick * 2.5f, stacheThick), stacheMat);
        AddPrimChild(model, "StacheRight", PrimitiveType.Capsule,
            stacheBase + sideV * (eyeGap * 0.5f),
            Quaternion.Euler(0, 0, 30),
            new Vector3(stacheThick, stacheThick * 2.5f, stacheThick), stacheMat);

        Debug.Log($"TTR: Added big googly eyes and mustache (axis={longAxis}, eyeSize={eyeSize:F2})");
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

        // Materials
        Material hairMat = MakeURPMat($"{name}_Hair", hairColor, 0f, 0.15f);
        Material darkMat = MakeURPMat($"{name}_Dark", darkColor, 0f, 0.1f);
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

        // Hair strands sticking out in all directions
        float[] strandAngles = { 0, 40, 80, 120, 160, 200, 240, 280, 320 };
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
            float len = 0.4f + (i % 4) * 0.12f;
            AddPrimChild(root, $"Strand{i}", PrimitiveType.Capsule, pos,
                rot, new Vector3(0.08f, len, 0.08f), (i % 2 == 0) ? hairMat : darkMat);
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

    static GameObject LoadModel(string path)
    {
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
                if (normalMap != null) m.SetTexture("_BumpMap", normalMap);
                if (metallicMap != null) m.SetTexture("_MetallicGlossMap", metallicMap);
                if (occlusionMap != null) m.SetTexture("_OcclusionMap", occlusionMap);

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
        return SaveMaterial(mat);
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

        // HUD
        Text scoreText = MakeText(canvasObj.transform, "ScoreText", "0",
            52, TextAnchor.UpperLeft, Color.white,
            new Vector2(0, 1), new Vector2(180, -40), new Vector2(350, 70), true);

        Text distanceText = MakeText(canvasObj.transform, "DistanceText", "0m",
            36, TextAnchor.UpperRight, new Color(0.8f, 0.9f, 0.7f),
            new Vector2(1, 1), new Vector2(-180, -40), new Vector2(350, 60), true);

        // Combo counter (center screen, hidden until active)
        Text comboText = MakeText(canvasObj.transform, "ComboText", "",
            56, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.2f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(500, 80), true);
        comboText.gameObject.SetActive(false);

        // Start Panel
        GameObject startPanel = MakePanel(canvasObj.transform, "StartPanel",
            new Color(0.04f, 0.07f, 0.03f, 0.93f),
            new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));

        MakeStretchText(startPanel.transform, "Title", "TURD\nTUNNEL\nRUSH",
            78, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.1f),
            new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.95f), true);

        MakeStretchText(startPanel.transform, "Subtitle", "Sewer Surf Showdown",
            30, TextAnchor.MiddleCenter, new Color(0.5f, 0.75f, 0.35f),
            new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.5f), false);

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

        // Shop button on start screen
        Button shopButton = MakeButton(startPanel.transform, "ShopButton", "SHOP",
            32, new Color(0.6f, 0.45f, 0.1f), Color.white,
            new Vector2(0.3f, 0.0f), new Vector2(0.7f, 0.06f));

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

        // HUD wallet text (top center during gameplay)
        Text walletText = MakeText(canvasObj.transform, "WalletText", "0",
            28, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(200, 40), true);

        // Game Over Panel
        GameObject gameOverPanel = MakePanel(canvasObj.transform, "GameOverPanel",
            new Color(0.12f, 0.02f, 0.02f, 0.93f),
            new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));
        gameOverPanel.SetActive(false);

        MakeStretchText(gameOverPanel.transform, "GOTitle", "CLOGGED!",
            68, TextAnchor.MiddleCenter, new Color(1f, 0.25f, 0.2f),
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f), true);

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
        ui.shopPanel = shopPanel;
        ui.shopContent = shopContentObj.transform;
        ui.shopCloseButton = shopCloseButton;
        ui.gameOverPanel = gameOverPanel;
        ui.finalScoreText = finalScoreText;
        ui.highScoreText = highScoreText;
        ui.runStatsText = runStatsText;
        ui.restartButton = restartButton;

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
