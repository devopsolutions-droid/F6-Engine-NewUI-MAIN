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
/// Everything else (layer, PartData wiring, manifest, hover panels) is automatic.
/// </summary>
public class EnginePartSetupTool : EditorWindow
{
    private const string EnginePartsLayerName = "EngineParts";

    private GameObject    _engineModel;
    private EngineRegistry _registry;
    private GameObject    _hoverPanelTemplate;
    private string        _engineName     = "New Engine";
    private string        _engineCategory = "General";
    private string        _savePath       = "Assets/ScriptableObjects/Data/Engines";

    [MenuItem("Tools/Engine Part Setup")]
    public static void Open() => GetWindow<EnginePartSetupTool>("Engine Part Setup");

    void OnGUI()
    {
        GUILayout.Label("Engine Part Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _engineModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine Model / Prefab", _engineModel, typeof(GameObject), true);

        _engineName     = EditorGUILayout.TextField("Engine Name",     _engineName);
        _engineCategory = EditorGUILayout.TextField("Category",        _engineCategory);

        EditorGUILayout.Space(4);
        _hoverPanelTemplate = (GameObject)EditorGUILayout.ObjectField(
            "Hover Panel Template (optional)", _hoverPanelTemplate, typeof(GameObject), false);

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
            "  • Creates a PartData asset per part AND wires it directly into EnginePart\n" +
            "  • Creates EnginePartManifest\n" +
            "  • Creates hover panel prefab per part\n" +
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

        string engineFolder  = $"{_savePath}/{_engineName.Replace(" ", "")}";
        string partsFolder   = $"{engineFolder}/Parts";
        string panelsFolder  = $"{engineFolder}/HoverPanels";

        EnsureFolder(_savePath);
        EnsureFolder(engineFolder);
        EnsureFolder(partsFolder);
        EnsureFolder(panelsFolder);

        var meshChildren = CollectMeshChildren(_engineModel);

        // ── Step 1 & 2: EnginePart + layer + PartData — all wired directly ───
        var manifest        = ScriptableObject.CreateInstance<EnginePartManifest>();
        int addedParts      = 0;
        int createdPartData = 0;

        // Palette cycles through distinct colors automatically
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

        bool isPrefabAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_engineModel));

        foreach (var go in meshChildren)
        {
            // Set layer
            go.layer = enginePartsLayer;

            // Add MeshCollider (convex) if missing
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) mc = go.AddComponent<MeshCollider>();
            mc.convex = true;

            // Add EnginePart if missing
            var ep = go.GetComponent<EnginePart>();
            if (ep == null)
            {
                ep = go.AddComponent<EnginePart>();
                ep.partName = go.name;
                addedParts++;
            }

            // Assign cycling outline color preset
            ep.outlineColorPreset = palette[paletteIndex % palette.Length];
            paletteIndex++;
            EditorUtility.SetDirty(ep);

            // Create or reuse PartData asset
            string safeName  = SanitizeName(go.name);
            string assetPath = $"{partsFolder}/{safeName}.asset";

            PartData pd;
            if (File.Exists(assetPath))
            {
                pd = AssetDatabase.LoadAssetAtPath<PartData>(assetPath);
            }
            else
            {
                pd = ScriptableObject.CreateInstance<PartData>();
                pd.partName    = go.name;
                pd.description = $"Description for {go.name}.";
                AssetDatabase.CreateAsset(pd, assetPath);
                createdPartData++;
            }

            // ── directly assign PartData into the EnginePart component ──
            ep.partData = pd;

            manifest.parts.Add(new EnginePartManifest.PartEntry
            {
                gameObjectName = go.name,
                partData       = pd
            });
        }

        // Mark root dirty so prefab saves the layer + component changes
        EditorUtility.SetDirty(_engineModel);
        Debug.Log($"[Setup] {addedParts} EnginePart components added, {createdPartData} PartData assets created.");

        // ── Step 3: Save manifest ─────────────────────────────────────────────
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
        {
            AssetDatabase.CreateAsset(manifest, manifestPath);
        }

        // ── Step 4: Hover panel prefabs ───────────────────────────────────────
        int createdPanels = 0;
        foreach (var go in meshChildren)
        {
            string panelPath = $"{panelsFolder}/{SanitizeName(go.name)}_Panel.prefab";
            if (File.Exists(panelPath)) continue;

            GameObject panelGO = _hoverPanelTemplate != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(_hoverPanelTemplate)
                : BuildDefaultHoverPanel(go.name);

            panelGO.name = $"{go.name}_Panel";
            if (panelGO.GetComponent<PartHoverPanel>() == null)
                panelGO.AddComponent<PartHoverPanel>();

            PrefabUtility.SaveAsPrefabAsset(panelGO, panelPath);
            DestroyImmediate(panelGO);
            createdPanels++;
        }
        Debug.Log($"[Setup] {createdPanels} hover panel prefabs created.");

        // ── Step 5: EngineData asset ──────────────────────────────────────────
        string dataPath   = $"{engineFolder}/{_engineName.Replace(" ", "")}Data.asset";
        EngineData engineData = AssetDatabase.LoadAssetAtPath<EngineData>(dataPath);
        if (engineData == null)
        {
            engineData = ScriptableObject.CreateInstance<EngineData>();
            AssetDatabase.CreateAsset(engineData, dataPath);
        }

        engineData.engineName     = _engineName;
        engineData.engineCategory = _engineCategory;
        engineData.partManifest   = manifest;

        string prefabPath = AssetDatabase.GetAssetPath(_engineModel);
        if (!string.IsNullOrEmpty(prefabPath))
            engineData.enginePrefab = _engineModel;

        EditorUtility.SetDirty(engineData);

        // ── Step 6: Registry ──────────────────────────────────────────────────
        if (_registry != null && !_registry.engines.Contains(engineData))
        {
            _registry.engines.Add(engineData);
            EditorUtility.SetDirty(_registry);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // If it's a prefab asset, apply all changes
        if (isPrefabAsset)
            PrefabUtility.SavePrefabAsset(_engineModel);

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = engineData;

        EditorUtility.DisplayDialog("Setup Complete ✓",
            $"Engine: {_engineName}\n\n" +
            $"  • Layer '{EnginePartsLayerName}' (index {enginePartsLayer}) applied to all parts\n" +
            $"  • {addedParts} EnginePart components assigned\n" +
            $"  • {createdPartData} PartData assets created & wired\n" +
            $"  • {createdPanels} hover panel prefabs created\n" +
            $"  • EngineData asset ready\n\n" +
            "Remaining steps:\n" +
            "1. Open Parts/ folder → fill description + drag audio into each PartData\n" +
            "2. Assign thumbnail in EngineData\n" +
            "3. Set spawnPosition / spawnRotation in EngineData\n" +
            "4. Wire hover panel prefabs to EnginePart.hoverPanel in the prefab",
            "OK");
    }

    // ── Layer helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the layer index for layerName. If it doesn't exist, creates it
    /// in the first available slot (8–31). Returns -1 if no slot is free.
    /// </summary>
    static int EnsureLayer(string layerName)
    {
        // Check if it already exists
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        // Add it via SerializedObject on TagManager
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");
        if (layersProp == null || !layersProp.isArray) return -1;

        // User layers start at index 8
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

        return -1; // no free slot
    }

    // ── Mesh child collector ──────────────────────────────────────────────────

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

    // ── Default hover panel builder ───────────────────────────────────────────

    GameObject BuildDefaultHoverPanel(string partName)
    {
        var root   = new GameObject($"{partName}_Panel");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(0.3f, 0.12f);
        root.AddComponent<UnityEngine.UI.CanvasScaler>();
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Background
        var bg    = new GameObject("Background");
        bg.transform.SetParent(root.transform, false);
        var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.15f, 0.88f);
        var bgRt  = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Label
        var labelGO = new GameObject("PartNameLabel");
        labelGO.transform.SetParent(root.transform, false);
        var tmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = partName;
        tmp.fontSize  = 0.06f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color     = new Color(0f, 0.85f, 1f);
        var labelRt   = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(0.01f, 0.01f);
        labelRt.offsetMax = new Vector2(-0.01f, -0.01f);

        root.transform.localScale = Vector3.one * 0.01f;
        return root;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

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
