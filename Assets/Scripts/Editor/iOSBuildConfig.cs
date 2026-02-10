using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Configures iOS build settings for App Store submission.
/// Menu: TTR > Configure iOS Build
/// </summary>
public class iOSBuildConfig
{
    [MenuItem("TTR/Configure iOS Build")]
    public static void ConfigureiOS()
    {
        // === PLAYER SETTINGS ===
        PlayerSettings.companyName = "TTR Games";
        PlayerSettings.productName = "Turd Tunnel Rush";

        // Bundle ID - CHANGE THIS to your Apple Developer Team ID
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.ttrgames.turdtunnelrush");

        // Version
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.iOS.buildNumber = "1";

        // iOS specific
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.requiresPersistentWiFi = false;

        // Architecture
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64

        // Graphics
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });

        // Orientation
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // Status bar
        PlayerSettings.statusBarHidden = true;

        // Performance
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_Standard);

        // Icon and splash will need to be set manually with actual images

        // === QUALITY ===
        // Ensure we're not wasting battery
        Application.targetFrameRate = 60;

        Debug.Log("TTR: iOS build configured!");
        EditorUtility.DisplayDialog("iOS Build Config",
            "Build settings configured for iOS!\n\n" +
            "Bundle ID: com.ttrgames.turdtunnelrush\n" +
            "Target: iOS 15.0+\n" +
            "Architecture: ARM64\n" +
            "Graphics: Metal\n" +
            "Orientation: Portrait\n\n" +
            "Next steps:\n" +
            "1. Set App Icon in Player Settings\n" +
            "2. Set Launch Screen\n" +
            "3. Build & Run to Xcode\n" +
            "4. Archive and upload to TestFlight", "Got it!");
    }

    [MenuItem("TTR/Build iOS")]
    public static void BuildiOS()
    {
        ConfigureiOS();

        string buildPath = "Builds/iOS";

        // Ensure the build directory exists
        if (!System.IO.Directory.Exists(buildPath))
            System.IO.Directory.CreateDirectory(buildPath);

        // Get scenes in build
        string[] scenes = new string[0];
        var buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length > 0)
        {
            var sceneList = new System.Collections.Generic.List<string>();
            foreach (var s in buildScenes)
                if (s.enabled) sceneList.Add(s.path);
            scenes = sceneList.ToArray();
        }

        if (scenes.Length == 0)
        {
            // Use the current scene
            scenes = new[] { UnityEngine.SceneManagement.SceneManager.GetActiveScene().path };
        }

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iOS, BuildOptions.None);
        Debug.Log($"TTR: iOS build created at {buildPath}");
    }
}
