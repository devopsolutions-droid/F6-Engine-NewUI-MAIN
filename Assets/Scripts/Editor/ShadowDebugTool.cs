using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Debug tool to identify all shadow-casting objects and lighting configuration.
/// Run from Unity Editor menu: Tools > Shadow Debug > Log All Shadow Info
/// </summary>
public class ShadowDebugTool
{
    [MenuItem("Tools/Shadow Debug/Log All Shadow Info")]
    public static void LogShadowInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                    SHADOW DEBUG REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Scene Info
        sb.AppendLine("📍 SCENE INFO");
        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine();

        // Lights
        sb.AppendLine("💡 LIGHTS IN SCENE");
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int realTimeLights = 0;
        int bakedLights = 0;
        int mixedLights = 0;

        foreach (Light light in lights)
        {
            string lightType = light.type.ToString();
            string mode = light.lightmapBakeType.ToString();
            string shadows = light.shadows.ToString();
            int shadowResolution = (int)light.shadowResolution;

            if (mode == "Realtime") realTimeLights++;
            else if (mode == "Baked") bakedLights++;
            else mixedLights++;

            sb.AppendLine($"  • {light.name}");
            sb.AppendLine($"    Type: {lightType} | Mode: {mode} | Shadows: {shadows}");
            sb.AppendLine($"    Resolution: {shadowResolution}");
            sb.AppendLine($"    Intensity: {light.intensity:F2} | Range: {light.range:F1}");
            sb.AppendLine();
        }

        sb.AppendLine($"  Summary: {lights.Length} total lights ({realTimeLights} realtime, {bakedLights} baked, {mixedLights} mixed)");
        sb.AppendLine();

        // Shadow-Casting Renderers
        sb.AppendLine("🔲 SHADOW-CASTING OBJECTS");
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Renderer> shadowCasters = new List<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                shadowCasters.Add(renderer);
            }
        }

        sb.AppendLine($"  Found {shadowCasters.Count} objects casting shadows");
        sb.AppendLine();

        // Group by layer for cleaner output
        Dictionary<string, List<Renderer>> byLayer = new Dictionary<string, List<Renderer>>();
        foreach (Renderer r in shadowCasters)
        {
            string layerName = LayerMask.LayerToName(r.gameObject.layer);
            if (!byLayer.ContainsKey(layerName))
                byLayer[layerName] = new List<Renderer>();
            byLayer[layerName].Add(r);
        }

        foreach (var kvp in byLayer)
        {
            sb.AppendLine($"  Layer: {kvp.Key} ({kvp.Value.Count} objects)");
            foreach (Renderer r in kvp.Value)
            {
                string shadowMode = r.shadowCastingMode.ToString();
                string receiveShadows = r.receiveShadows ? "Yes" : "No";
                string motionVectors = r.motionVectorGenerationMode.ToString();
                bool isStatic = r.gameObject.isStatic;

                sb.AppendLine($"    • {r.name}");
                sb.AppendLine($"      ShadowMode: {shadowMode} | ReceiveShadows: {receiveShadows} | Static: {isStatic}");
                sb.AppendLine($"      MotionVectors: {motionVectors}");
            }
            sb.AppendLine();
        }

        // XR Origin Info
        sb.AppendLine("🥽 XR ORIGIN INFO");
        var xrOrigin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            sb.AppendLine($"  XR Origin: {xrOrigin.name}");
            sb.AppendLine($"  Position: {xrOrigin.transform.position}");
            sb.AppendLine($"  Rotation: {xrOrigin.transform.rotation.eulerAngles}");

            // Check for any components that might affect shadows
            var components = xrOrigin.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null && comp.GetType().Name.Contains("Shadow"))
                {
                    sb.AppendLine($"  ⚠️ Has shadow-related component: {comp.GetType().Name}");
                }
            }
        }
        else
        {
            sb.AppendLine("  No XR Origin found in scene");
        }
        sb.AppendLine();

        // Camera Info
        sb.AppendLine("📷 CAMERA INFO");
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            string camType = "Game";
            if (cam.CompareTag("MainCamera")) camType = "Main Camera";
            else if (cam.stereoTargetEye != StereoTargetEyeMask.None) camType = "VR Camera";

            sb.AppendLine($"  • {cam.name}");
            sb.AppendLine($"    Type: {camType}");
            sb.AppendLine($"    Depth: {cam.depth}");
            sb.AppendLine($"    ClearFlags: {cam.clearFlags}");
            sb.AppendLine($"    CullingMask: {cam.cullingMask}");
            sb.AppendLine($"    Near: {cam.nearClipPlane:F2} | Far: {cam.farClipPlane:F1}");
        }
        sb.AppendLine();

        // Render Pipeline Settings
        sb.AppendLine("⚙️ RENDER PIPELINE SETTINGS");
        var renderPipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (renderPipelineAsset != null)
        {
            sb.AppendLine($"  Pipeline: {renderPipelineAsset.name}");
            sb.AppendLine($"  Type: {renderPipelineAsset.GetType().Name}");

            if (renderPipelineAsset is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpAsset)
            {
                sb.AppendLine($"  Shadow Distance: {urpAsset.shadowDistance:F1}");
                sb.AppendLine($"  Shadow Cascade Count: {urpAsset.shadowCascadeCount}");
                sb.AppendLine($"  Max Shadow Lights: {urpAsset.maxAdditionalLightsCount}");
                sb.AppendLine($"  Main Light Shadow Resolution: {urpAsset.mainLightShadowmapResolution}");
                sb.AppendLine($"  Additional Light Shadow Resolution: {urpAsset.additionalLightsShadowmapResolution}");
            }
        }
        else
        {
            sb.AppendLine("  Pipeline: Built-in Render Pipeline");
        }
        sb.AppendLine();

        // Quality Settings
        sb.AppendLine("🎨 QUALITY SETTINGS");
        sb.AppendLine($"  Shadow Quality: {QualitySettings.shadows}");
        sb.AppendLine($"  Shadow Distance: {QualitySettings.shadowDistance:F1}");
        sb.AppendLine($"  Shadow Cascades: {QualitySettings.shadowCascades}");
        sb.AppendLine($"  Shadow Projection: {QualitySettings.shadowProjection}");
        sb.AppendLine($"  Shadow Resolution: {QualitySettings.shadowResolution}");
        sb.AppendLine($"  Soft Vegetation: {QualitySettings.softVegetation}");
        sb.AppendLine();

        // Potential Issues
        sb.AppendLine("⚠️ POTENTIAL ISSUES");
        bool hasIssues = false;

        if (realTimeLights > 4)
        {
            sb.AppendLine("  • Too many realtime shadow-casting lights (recommend ≤ 4)");
            hasIssues = true;
        }

        if (QualitySettings.shadowDistance < 10)
        {
            sb.AppendLine("  • Shadow distance is very low - shadows may disappear abruptly");
            hasIssues = true;
        }

        if (shadowCasters.Count > 100)
        {
            sb.AppendLine($"  • High shadow caster count ({shadowCasters.Count}) - may impact performance");
            hasIssues = true;
        }

        if (!hasIssues)
        {
            sb.AppendLine("  No obvious configuration issues detected");
        }
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Shadow Debug/Select All Shadow Casters")]
    public static void SelectAllShadowCasters()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> shadowCasters = new List<GameObject>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                shadowCasters.Add(renderer.gameObject);
            }
        }

        Selection.objects = shadowCasters.ToArray();
        Debug.Log($"Selected {shadowCasters.Count} shadow-casting objects");
    }
}
