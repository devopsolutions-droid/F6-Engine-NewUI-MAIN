using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > Engine Remover
///
/// Completely removes a selected engine from the project:
///   • Removes EngineData from EngineRegistry
///   • Deletes the entire ScriptableObjects/Data/Engines/<Name>/ folder
///     (PartData assets, HoverPanel prefabs, EngineData, Manifest)
///   • Deletes the engine prefab from Assets/Prefabs/Engine Prefabs/
///   • Optionally deletes the raw GLB from Assets/All Engines/
///
/// Manual steps printed to Console after running:
///   • Remove the engine root GameObject from the Engine View Scene
///   • Remove the EngineSceneEntry from EngineSceneLoader in the scene
/// </summary>
public class EngineRemoverTool : EditorWindow
{
    private EngineData     _engineData;
    private EngineRegistry _registry;
    private bool           _deleteGlb     = false;
    private bool           _deletePrefab  = true;
    private string         _status        = "";

    [MenuItem("Tools/Engine Remover")]
    public static void Open() => GetWindow<EngineRemoverTool>("Engine Remover");

    void OnGUI()
    {
        GUILayout.Label("Engine Remover", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _engineData = (EngineData)EditorGUILayout.ObjectField(
            "Engine Data Asset", _engineData, typeof(EngineData), false);

        _registry = (EngineRegistry)EditorGUILayout.ObjectField(
            "Engine Registry", _registry, typeof(EngineRegistry), false);

        EditorGUILayout.Space(4);
        _deletePrefab = EditorGUILayout.Toggle("Delete Engine Prefab", _deletePrefab);
        _deleteGlb    = EditorGUILayout.Toggle("Delete Raw GLB File",  _deleteGlb);

        EditorGUILayout.Space(6);

        // Preview what will be deleted
        if (_engineData != null)
        {
            string engineFolder = GetEngineFolder(_engineData);
            string prefabPath   = _engineData.enginePrefab != null
                ? AssetDatabase.GetAssetPath(_engineData.enginePrefab) : "—";
            string glbPath      = FindGlbPath(_engineData);

            EditorGUILayout.HelpBox(
                $"Will delete:\n" +
                $"  • Registry entry: {_engineData.engineName}\n" +
                $"  • Data folder:    {engineFolder ?? "not found"}\n" +
                $"  • Prefab:         {(_deletePrefab ? prefabPath : "skipped")}\n" +
                $"  • GLB:            {(_deleteGlb ? (glbPath ?? "not found") : "skipped")}",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6);

        bool canRun = _engineData != null && _registry != null;
        GUI.enabled = canRun;

        if (GUILayout.Button("REMOVE ENGINE  (irreversible)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm Removal",
                $"This will permanently delete all assets for:\n\n\"{_engineData.engineName}\"\n\nThis cannot be undone.",
                "Delete", "Cancel"))
            {
                RunRemoval();
            }
        }

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "After running, manually:\n" +
            "  1. Open the Engine View Scene\n" +
            "  2. Delete the engine root GameObject from the hierarchy\n" +
            "  3. Remove its entry from EngineSceneLoader → Engine Entries list",
            MessageType.Info);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void RunRemoval()
    {
        string engineName = _engineData.engineName;
        var    log        = new System.Text.StringBuilder();
        log.AppendLine($"[EngineRemover] Starting removal of: {engineName}");

        // ── Step 1: Remove from EngineRegistry ───────────────────────────────
        if (_registry.engines.Contains(_engineData))
        {
            _registry.engines.Remove(_engineData);
            EditorUtility.SetDirty(_registry);
            log.AppendLine("  ✓ Removed from EngineRegistry");
        }
        else
        {
            log.AppendLine("  ! EngineData was not found in EngineRegistry (skipped)");
        }

        // ── Step 2: Delete engine prefab ─────────────────────────────────────
        if (_deletePrefab && _engineData.enginePrefab != null)
        {
            string prefabPath = AssetDatabase.GetAssetPath(_engineData.enginePrefab);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                bool deleted1 = AssetDatabase.DeleteAsset(prefabPath);
                if (deleted1)
                    log.AppendLine($"  ✓ Deleted prefab: {prefabPath}");
                else
                    log.AppendLine($"  ! Failed to delete prefab: {prefabPath}");
            }
        }
        else if (_deletePrefab)
        {
            log.AppendLine("  ! No prefab reference found on EngineData (skipped)");
        }

        // ── Step 3: Delete raw GLB ────────────────────────────────────────────
        if (_deleteGlb)
        {
            string glbPath = FindGlbPath(_engineData);
            if (!string.IsNullOrEmpty(glbPath))
            {
                bool deleted2 = AssetDatabase.DeleteAsset(glbPath);
                if (deleted2)
                    log.AppendLine($"  ✓ Deleted GLB: {glbPath}");
                else
                    log.AppendLine($"  ! Failed to delete GLB: {glbPath}");
            }
            else
            {
                log.AppendLine("  ! GLB file not found (skipped)");
            }
        }

        // ── Step 4: Delete entire engine data folder ──────────────────────────
        string engineFolder = GetEngineFolder(_engineData);
        if (!string.IsNullOrEmpty(engineFolder) && Directory.Exists(engineFolder))
        {
            // Use AssetDatabase to delete so Unity tracks it properly
            string unityPath = engineFolder.Replace("\\", "/");
            // Strip leading project path if needed
            if (unityPath.StartsWith(Application.dataPath.Replace("\\", "/")))
                unityPath = "Assets" + unityPath.Substring(Application.dataPath.Length);

            bool deleted = AssetDatabase.DeleteAsset(unityPath);
            if (deleted)
                log.AppendLine($"  ✓ Deleted data folder: {unityPath}");
            else
            {
                // Fallback: delete via FileUtil
                FileUtil.DeleteFileOrDirectory(engineFolder);
                FileUtil.DeleteFileOrDirectory(engineFolder + ".meta");
                log.AppendLine($"  ✓ Deleted data folder (via FileUtil): {engineFolder}");
            }
        }
        else
        {
            log.AppendLine($"  ! Data folder not found: {engineFolder ?? "null"}");
        }

        // ── Step 5: Save and refresh ──────────────────────────────────────────
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Step 6: Manual steps reminder ────────────────────────────────────
        log.AppendLine();
        log.AppendLine("  ── MANUAL STEPS REQUIRED ──────────────────────────────");
        log.AppendLine($"  1. Open the Engine View Scene");
        log.AppendLine($"  2. Find and DELETE the '{engineName}' root GameObject in the hierarchy");
        log.AppendLine($"  3. Select the EngineSceneLoader GameObject");
        log.AppendLine($"  4. Remove the '{engineName}' entry from the Engine Entries list");

        Debug.Log(log.ToString());

        _status = $"✓ Done — {engineName} removed.\n\nSee Console for manual scene steps.";
        _engineData = null;
        Repaint();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the engine's data folder by looking at the EngineData asset's own path.
    /// e.g. Assets/ScriptableObjects/Data/Engines/F6BoxerEngine/F6BoxerEngineData.asset
    ///   → Assets/ScriptableObjects/Data/Engines/F6BoxerEngine
    /// </summary>
    static string GetEngineFolder(EngineData data)
    {
        string assetPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(assetPath)) return null;
        return Path.GetDirectoryName(assetPath).Replace("\\", "/");
    }

    /// <summary>
    /// Searches Assets/All Engines/ for a GLB whose name matches the engine prefab name.
    /// </summary>
    static string FindGlbPath(EngineData data)
    {
        string searchFolder = "Assets/All Engines";
        if (!Directory.Exists(searchFolder)) return null;

        // Try matching by prefab name first
        string prefabName = data.enginePrefab != null ? data.enginePrefab.name : null;
        string engineName = data.engineName;

        string[] guids = AssetDatabase.FindAssets("t:Object", new[] { searchFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (prefabName != null && fileName.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                return path;
            if (fileName.Equals(engineName, System.StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }
}
