using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

/// <summary>
/// Dumps detailed scene diagnostics to a file Claude can read.
/// Menu: TTR > Run Diagnostics
/// </summary>
public class SceneDiagnostics
{
    static string logPath = "C:/Claude/TTR/unity_diagnostics.txt";

    [MenuItem("TTR/Run Diagnostics")]
    public static void RunDiagnostics()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== TTR Scene Diagnostics ===");
        sb.AppendLine($"Time: {System.DateTime.Now}");
        sb.AppendLine();

        // Count all objects by type
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        sb.AppendLine($"Total GameObjects: {allObjects.Length}");

        // Check key components
        var player = Object.FindFirstObjectByType<TurdController>();
        sb.AppendLine($"Player found: {player != null}");
        if (player != null)
        {
            sb.AppendLine($"  Position: {player.transform.position}");
            sb.AppendLine($"  Scale: {player.transform.localScale}");
            sb.AppendLine($"  Distance: {player.DistanceTraveled}");
        }

        var pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        sb.AppendLine($"PipeGenerator found: {pipeGen != null}");
        if (pipeGen != null)
            sb.AppendLine($"  Pipe radius: {pipeGen.pipeRadius}, Segments: {pipeGen.visiblePipes}");

        var cam = Object.FindFirstObjectByType<PipeCamera>();
        sb.AppendLine($"PipeCamera found: {cam != null}");
        if (cam != null)
            sb.AppendLine($"  Follow dist: {cam.followDistance}, HeightAbovePlayer: {cam.heightAbovePlayer}, LookAhead: {cam.lookAhead}");

        var obstSpawner = Object.FindFirstObjectByType<ObstacleSpawner>();
        sb.AppendLine($"ObstacleSpawner found: {obstSpawner != null}");
        if (obstSpawner != null)
        {
            sb.AppendLine($"  Obstacle prefabs: {(obstSpawner.obstaclePrefabs != null ? obstSpawner.obstaclePrefabs.Length : 0)}");
            sb.AppendLine($"  Coin prefab: {(obstSpawner.coinPrefab != null ? obstSpawner.coinPrefab.name : "NULL")}");
            sb.AppendLine($"  Pipe radius: {obstSpawner.pipeRadius}");
            if (obstSpawner.obstaclePrefabs != null)
            {
                for (int i = 0; i < obstSpawner.obstaclePrefabs.Length; i++)
                {
                    var p = obstSpawner.obstaclePrefabs[i];
                    sb.AppendLine($"    [{i}] {(p != null ? p.name : "NULL")} scale={p?.transform.localScale}");
                }
            }
        }

        var scenerySpawner = Object.FindFirstObjectByType<ScenerySpawner>();
        sb.AppendLine($"ScenerySpawner found: {scenerySpawner != null}");
        if (scenerySpawner != null)
        {
            sb.AppendLine($"  Scenery prefabs: {(scenerySpawner.sceneryPrefabs != null ? scenerySpawner.sceneryPrefabs.Length : 0)}");
            sb.AppendLine($"  Gross prefabs: {(scenerySpawner.grossPrefabs != null ? scenerySpawner.grossPrefabs.Length : 0)}");
        }

        var powerUpSpawner = Object.FindFirstObjectByType<PowerUpSpawner>();
        sb.AppendLine($"PowerUpSpawner found: {powerUpSpawner != null}");
        if (powerUpSpawner != null)
        {
            sb.AppendLine($"  SpeedBoost prefab: {(powerUpSpawner.speedBoostPrefab != null ? powerUpSpawner.speedBoostPrefab.name : "NULL")}");
            sb.AppendLine($"  JumpRamp prefab: {(powerUpSpawner.jumpRampPrefab != null ? powerUpSpawner.jumpRampPrefab.name : "NULL")}");
        }

        var smoothSnake = Object.FindFirstObjectByType<SmoothSnakeAI>();
        sb.AppendLine($"SmoothSnake AI found: {smoothSnake != null}");
        if (smoothSnake != null)
            sb.AppendLine($"  Position: {smoothSnake.transform.position}, Speed: {smoothSnake.baseSpeed}");

        // Check all renderers for broken materials
        sb.AppendLine();
        sb.AppendLine("=== Material Report ===");
        int purpleCount = 0;
        int urpCount = 0;
        int otherCount = 0;
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { purpleCount++; continue; }
                string sn = m.shader != null ? m.shader.name : "NULL";
                if (sn.Contains("Universal")) urpCount++;
                else if (sn.Contains("Error") || sn.Contains("Hidden") || sn == "Standard")
                {
                    purpleCount++;
                    sb.AppendLine($"  BROKEN: {r.gameObject.name} -> shader: {sn}, mat: {m.name}");
                }
                else otherCount++;
            }
        }
        sb.AppendLine($"URP materials: {urpCount}, Broken/Purple: {purpleCount}, Other: {otherCount}");

        // Check for water meshes
        sb.AppendLine();
        sb.AppendLine("=== Water Meshes ===");
        int waterCount = 0;
        foreach (var go in allObjects)
        {
            if (go.name == "SewerWater")
            {
                waterCount++;
                var mr = go.GetComponent<MeshRenderer>();
                var mf = go.GetComponent<MeshFilter>();
                if (waterCount <= 3)  // just log first few
                {
                    sb.AppendLine($"  Water #{waterCount}: pos={go.transform.position}");
                    if (mr != null && mr.sharedMaterial != null)
                        sb.AppendLine($"    Material: {mr.sharedMaterial.name}, shader: {mr.sharedMaterial.shader.name}");
                    if (mf != null && mf.sharedMesh != null)
                        sb.AppendLine($"    Mesh verts: {mf.sharedMesh.vertexCount}");
                }
            }
        }
        sb.AppendLine($"Total water planes: {waterCount}");

        // Check lights
        sb.AppendLine();
        sb.AppendLine("=== Lights ===");
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            sb.AppendLine($"  {l.gameObject.name}: type={l.type}, intensity={l.intensity}, range={l.range}, color={l.color}");
        }

        // Coin count
        sb.AppendLine();
        int coinCount = 0;
        int obstacleCount = 0;
        foreach (var go in allObjects)
        {
            if (go.GetComponent<Collectible>() != null) coinCount++;
            if (go.GetComponent<Obstacle>() != null) obstacleCount++;
        }
        int boostCount = 0;
        int rampCount = 0;
        foreach (var go in allObjects)
        {
            if (go.GetComponent<SpeedBoost>() != null) boostCount++;
            if (go.GetComponent<JumpRamp>() != null) rampCount++;
        }
        sb.AppendLine($"Active coins: {coinCount}, Active obstacles: {obstacleCount}");
        sb.AppendLine($"Active speed boosts: {boostCount}, Active ramps: {rampCount}");

        // New systems
        sb.AppendLine();
        sb.AppendLine("=== Game Feel Systems ===");
        var combo = Object.FindFirstObjectByType<ComboSystem>();
        sb.AppendLine($"ComboSystem found: {combo != null}");
        if (combo != null)
            sb.AppendLine($"  ComboText assigned: {combo.comboText != null}");

        var particles = Object.FindFirstObjectByType<ParticleManager>();
        sb.AppendLine($"ParticleManager found: {particles != null}");

        var touchInput = Object.FindFirstObjectByType<TouchInput>();
        sb.AppendLine($"TouchInput found: {touchInput != null}");
        if (touchInput != null)
            sb.AppendLine($"  Control scheme: {touchInput.controlScheme}");

        int nearMissZones = 0;
        foreach (var go in allObjects)
            if (go.GetComponent<NearMissZone>() != null) nearMissZones++;
        sb.AppendLine($"NearMissZones (runtime only): {nearMissZones}");

        File.WriteAllText(logPath, sb.ToString());
        Debug.Log($"TTR Diagnostics written to {logPath}");
        EditorUtility.DisplayDialog("Diagnostics", $"Written to:\n{logPath}", "OK");
    }
}
