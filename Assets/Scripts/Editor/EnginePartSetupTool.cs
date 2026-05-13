using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Tools > Engine Part Setup
///
/// One-click pipeline. After running, the developer only needs to:
///   1. Open each PartData asset and fill in description + drag audio clip
///   2. Assign thumbnail in EngineData asset
///   3. Set spawnPosition / spawnRotation in EngineData asset
/// </summary>
public class EnginePartSetupTool : EditorWindow
{
    private const string EnginePartsLayerName = "EngineParts";

    private GameObject     _engineModel;
    private EngineRegistry _registry;
    private string         _engineName     = "New Engine";
    private string         _engineCategory = "General";
    private string         _savePath       = "Assets/ScriptableObjects/Data/Engines";

    [MenuItem("Tools/Engine Part Setup")]
    public static void Open() => GetWindow<EnginePartSetupTool>("Engine Part Setup");

    void OnGUI()
    {
        GUILayout.Label("Engine Part Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _engineModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine Model / Prefab", _engineModel, typeof(GameObject), true);

        _engineName     = EditorGUILayout.TextField("Display Button Name", _engineName);
        _engineCategory = EditorGUILayout.TextField("Category",            _engineCategory);

        EditorGUILayout.Space(4);

        _registry = (EngineRegistry)EditorGUILayout.ObjectField(
            "Engine Registry (optional)", _registry, typeof(EngineRegistry), false);

        _savePath = EditorGUILayout.TextField("Save Path", _savePath);

        EditorGUILayout.Space(10);

        GUI.enabled = _engineModel != null;
        if (GUILayout.Button("RUN FULL SETUP  (one click)", GUILayout.Height(40)))
            RunFullSetup();
        GUI.enabled = true;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "One click does everything:\n" +
            "  • Ensures 'EngineParts' layer exists\n" +
            "  • Sets every mesh child to EngineParts layer\n" +
            "  • Adds MeshCollider (convex) to every mesh child\n" +
            "  • Assigns EnginePart component to every mesh child\n" +
            "  • Creates a PartData asset per part AND wires it into EnginePart\n" +
            "  • Creates EnginePartManifest\n" +
            "  • Creates EngineData asset\n\n" +
            "After running:\n" +
            "  1. Open each PartData in Parts/ folder → fill description + drag audio\n" +
            "  2. Assign thumbnail in EngineData asset\n" +
            "  3. Set spawnPosition / spawnRotation in EngineData asset",
            MessageType.Info);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void RunFullSetup()
    {
        if (_engineModel == null) return;

        // ── Ensure EngineParts layer exists ───────────────────────────────────
        int enginePartsLayer = EnsureLayer(EnginePartsLayerName);
        if (enginePartsLayer < 0)
        {
            EditorUtility.DisplayDialog("Layer Error",
                $"Could not create or find layer '{EnginePartsLayerName}'.\n" +
                "Please add it manually in Edit > Project Settings > Tags and Layers, then re-run.",
                "OK");
            return;
        }

        string engineFolder = $"{_savePath}/{_engineName.Replace(" ", "")}";
        string partsFolder  = $"{engineFolder}/Parts";

        EnsureFolder(_savePath);
        EnsureFolder(engineFolder);
        EnsureFolder(partsFolder);

        var manifest        = ScriptableObject.CreateInstance<EnginePartManifest>();
        int addedParts      = 0;
        int createdPartData = 0;

        var palette = new OutlineColorPreset[]
        {
            OutlineColorPreset.Cyan,
            OutlineColorPreset.Orange,
            OutlineColorPreset.LightGreen,
            OutlineColorPreset.Yellow,
            OutlineColorPreset.Pink,
            OutlineColorPreset.Green,
            OutlineColorPreset.Purple,
            OutlineColorPreset.Red,
            OutlineColorPreset.Blue,
            OutlineColorPreset.White
        };
        int paletteIndex = 0;

        bool   isPrefabAsset   = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_engineModel));
        string prefabAssetPath = AssetDatabase.GetAssetPath(_engineModel);
        bool   canEditPrefab   = !string.IsNullOrEmpty(prefabAssetPath);

        PrefabUtility.EditPrefabContentsScope? scope = canEditPrefab
            ? new PrefabUtility.EditPrefabContentsScope(prefabAssetPath)
            : (PrefabUtility.EditPrefabContentsScope?)null;

        try
        {
            GameObject prefabRoot  = canEditPrefab ? scope.Value.prefabContentsRoot : _engineModel;
            var        scopedChildren = CollectMeshChildren(prefabRoot);

            // ── EnginePart + layer + PartData ─────────────────────────────────
            foreach (var go in scopedChildren)
            {
                go.layer = enginePartsLayer;

                var mc = go.GetComponent<MeshCollider>();
                if (mc == null) mc = go.AddComponent<MeshCollider>();
                mc.convex = true;

                var ep = go.GetComponent<EnginePart>();
                if (ep == null)
                {
                    ep = go.AddComponent<EnginePart>();
                    ep.partName = go.name;
                    addedParts++;
                }

                ep.outlineColorPreset = palette[paletteIndex % palette.Length];
                paletteIndex++;

                string safeName  = SanitizeName(go.name);
                string assetPath = $"{partsFolder}/{safeName}.asset";

                PartData pd;
                if (File.Exists(assetPath))
                    pd = AssetDatabase.LoadAssetAtPath<PartData>(assetPath);
                else
                {
                    pd = ScriptableObject.CreateInstance<PartData>();
                    pd.partName    = go.name;
                    pd.description = $"Description for {go.name}.";
                    AssetDatabase.CreateAsset(pd, assetPath);
                    createdPartData++;
                }

                ep.partData = pd;

                manifest.parts.Add(new EnginePartManifest.PartEntry
                {
                    gameObjectName = go.name,
                    partData       = pd
                });
            }

            Debug.Log($"[Setup] {addedParts} EnginePart components added, {createdPartData} PartData assets created.");

            // ── Save manifest ─────────────────────────────────────────────────
            string manifestPath = $"{engineFolder}/{_engineName.Replace(" ", "")}Manifest.asset";
            EnginePartManifest existingManifest = AssetDatabase.LoadAssetAtPath<EnginePartManifest>(manifestPath);
            if (existingManifest != null)
            {
                foreach (var newEntry in manifest.parts)
                {
                    bool found = false;
                    foreach (var e in existingManifest.parts)
                        if (e.gameObjectName == newEntry.gameObjectName) { found = true; break; }
                    if (!found) existingManifest.parts.Add(newEntry);
                }
                EditorUtility.SetDirty(existingManifest);
                manifest = existingManifest;
            }
            else
                AssetDatabase.CreateAsset(manifest, manifestPath);
        }
        finally
        {
            scope?.Dispose();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── EngineData asset ──────────────────────────────────────────────────
        string dataPath = $"{engineFolder}/{_engineName.Replace(" ", "")}Data.asset";
        EngineData engineData = AssetDatabase.LoadAssetAtPath<EngineData>(dataPath);
        if (engineData == null)
        {
            engineData = ScriptableObject.CreateInstance<EngineData>();
            AssetDatabase.CreateAsset(engineData, dataPath);
        }
        engineData.engineName     = _engineName;
        engineData.engineCategory = _engineCategory;
        engineData.partManifest   = manifest;
        if (!string.IsNullOrEmpty(prefabAssetPath))
            engineData.enginePrefab = _engineModel;
        EditorUtility.SetDirty(engineData);

        // ── Registry ──────────────────────────────────────────────────────────
        if (_registry != null && !_registry.engines.Contains(engineData))
        {
            _registry.engines.Add(engineData);
            EditorUtility.SetDirty(_registry);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (isPrefabAsset)
            PrefabUtility.SavePrefabAsset(_engineModel);

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = engineData;

        EditorUtility.DisplayDialog("Setup Complete ✓",
            $"Engine: {_engineName}\n\n" +
            $"  • Layer '{EnginePartsLayerName}' applied to all parts\n" +
            $"  • {addedParts} EnginePart components assigned\n" +
            $"  • {createdPartData} PartData assets created & wired\n" +
            $"  • EngineData asset ready\n\n" +
            "Remaining steps:\n" +
            "1. Open Parts/ folder → fill description + drag audio into each PartData\n" +
            "2. Assign thumbnail in EngineData\n" +
            "3. Set spawnPosition / spawnRotation in EngineData",
            "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");
        if (layersProp == null || !layersProp.isArray) return -1;

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var layerProp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[Setup] Created layer '{layerName}' at index {i}.");
                return i;
            }
        }
        return -1;
    }

    List<GameObject> CollectMeshChildren(GameObject root)
    {
        var result = new List<GameObject>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject == root) continue;
            if (!result.Contains(r.gameObject))
                result.Add(r.gameObject);
        }
        return result;
    }

    void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    string SanitizeName(string name) =>
        name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_")
            .Replace(":", "_").Replace("*", "_").Replace("?", "_")
            .Replace("\"", "_").Replace("<", "_").Replace(">", "_")
            .Replace("|", "_");
}
