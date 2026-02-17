# Turd Tunnel Rush: Sewer Surf Showdown

## Project Overview
Unity 6 (6000.3.5f1) URP mobile game. Player controls a turd sliding through procedural sewer pipes, collecting coins, dodging obstacles, doing tricks. Cel-shaded comic art style.

## Tech Stack
- **Engine**: Unity 6000.3.5f1 with Universal Render Pipeline (URP)
- **Input**: New Input System only (`activeInputHandler: 1`)
- **Rendering**: Custom ToonLit.shader + OutlineEdgeDetect.shader (cel-shaded)
- **Audio**: All procedural (ProceduralAudio.cs) - no audio files
- **Scene**: Built entirely from code via SceneBootstrapper (Editor menu: TTR > Setup Game Scene)
- **Bundle ID**: com.browntownstudios.turdtunnelrush

## Development Workflow
- **Windows PC**: Main development (code, Blender models, testing, Android builds)
- **Mac**: iOS builds only (clone → Unity → Build iOS → Xcode → TestFlight)
- **GitHub**: Bridge between machines (`https://github.com/gnoobs75/TTR.git`)

## Mac iOS Build Steps
1. Pull latest: `git pull`
2. Open project in Unity Hub (version **6000.3.5f1** with iOS Build Support module)
3. File → Build Settings → iOS → Switch Platform (first time only)
4. Player Settings to verify:
   - Bundle ID: `com.browntownstudios.turdtunnelrush`
   - Minimum iOS: 15.0
   - Scripting Backend: IL2CPP
   - Target Architecture: ARM64
5. Build → choose output folder (e.g. `~/Builds/TTR_iOS`)
6. Open generated `.xcodeproj` in Xcode
7. Signing & Capabilities → Automatically manage signing → select Team
8. Set device to "Any iOS Device (arm64)"
9. Product → Archive
10. Distribute App → App Store Connect → Upload
11. Wait for processing on App Store Connect, then enable in TestFlight

## Key Architecture
- **SceneBootstrapper.cs** (Editor script): Creates the entire game scene from code
- **TurdController.cs**: Player movement, hit states, tricks, stomps
- **PipeGenerator.cs**: Infinite procedural pipe with structural geometry
- **GameManager.cs**: Game loop, scoring, multiplier, milestones
- **PipeZoneSystem.cs**: 5 themed zones with blending transitions
- **SkinManager.cs**: 5 character skins with per-skin face builders

## Critical Rules
- NEVER add Light components to spawned prefabs (gizmo icons show in game view)
- NEVER use `* 100f` GLB scale correction (it's wrong)
- GLB preferred over FBX (embeds textures), requires `com.unity.cloud.gltfast` package
- ALL materials must be saved as .mat assets via SaveMaterial()
- Camera setup is fragile - don't change followDistance/playerBias without testing
- SkinManager must skip CornKernel + primitive mesh renderers
- Uses `Keyboard.current` (New Input System) - NOT `Input.GetKey()`

## Required Packages (already in manifest.json)
- com.unity.cloud.gltfast (6.6.0) - GLB model import
- com.unity.inputsystem - New Input System
- com.unity.render-pipelines.universal - URP

## iOS-Specific Code
- `Assets/Plugins/iOS/HapticBridge.mm` - Native iOS haptics
- `Assets/Scripts/Editor/iOSBuildConfig.cs` - iOS build post-processing
- `Assets/Scripts/SafeAreaHandler.cs` - iPhone notch/Dynamic Island handling
- `Assets/Scripts/GameCenterManager.cs` - Game Center leaderboards
- `Assets/Scripts/TouchInput.cs` - Touch controls for mobile
