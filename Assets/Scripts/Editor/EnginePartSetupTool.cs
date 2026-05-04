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
    private float         _panelOffset    = 0.4f;

    [MenuItem("Tools/Engine Part Setup")]
    public static void Open() => GetWindow<EnginePartSetupTool>("Engine Part Setup");

    void OnGUI()
    {
        GUILayout.Label("Engine Part Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _engineModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine Model / Prefab", _engineModel, typeof(GameObject), true);

        _engineName     = EditorGUILayout.TextField("Display Button Name",     _engineName);
        _engineCategory = EditorGUILayout.TextField("Category",        _engineCategory);

        EditorGUILayout.Space(4);
        _hoverPanelTemplate = (GameObject)EditorGUILayout.ObjectField(
            "Hover Panel Template (optional)", _hoverPanelTemplate, typeof(GameObject), false);

        _registry = (EngineRegistry)EditorGUILayout.ObjectField(
            "Engine Registry (optional)", _registry, typeof(EngineRegistry), false);

        _savePath = EditorGUILayout.TextField("Save Path", _savePath);

        _panelOffset = EditorGUILayout.Slider("Panel Offset Distance", _panelOffset, 0.05f, 2f);

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

        // ── Steps 1–4: All prefab modifications inside EditPrefabContentsScope ─
        var manifest        = ScriptableObject.CreateInstance<EnginePartManifest>();
        int addedParts      = 0;
        int createdPartData = 0;
        int createdPanels   = 0;

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

        // Compute engine bounds from a temp instance BEFORE opening the scope
        Vector3 engineCenter = Vector3.zero;
        float   autoOffset   = _panelOffset;
        if (canEditPrefab)
        {
            var tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(_engineModel);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;
            Bounds engineBounds = ComputeEngineBounds(tempInstance);
            engineCenter = engineBounds.center;
            autoOffset   = Mathf.Max(engineBounds.extents.magnitude * 0.35f, _panelOffset);
            DestroyImmediate(tempInstance);
        }

        PrefabUtility.EditPrefabContentsScope? scope = canEditPrefab
            ? new PrefabUtility.EditPrefabContentsScope(prefabAssetPath)
            : (PrefabUtility.EditPrefabContentsScope?)null;

        try
        {
            GameObject prefabRoot = canEditPrefab ? scope.Value.prefabContentsRoot : _engineModel;

            // Collect mesh children from the LIVE prefab root inside the scope
            var scopedChildren = CollectMeshChildren(prefabRoot);

            // ── Step 1 & 2: EnginePart + layer + PartData ─────────────────────
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

            // ── Step 3: Save manifest ─────────────────────────────────────────
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

            // ── Step 4: Hover panels ──────────────────────────────────────────
            Transform panelContainer = prefabRoot.transform.Find("_HoverPanels");
            if (panelContainer == null)
            {
                var containerGO = new GameObject("_HoverPanels");
                containerGO.transform.SetParent(prefabRoot.transform, false);
                panelContainer = containerGO.transform;
            }

            foreach (var go in scopedChildren)
            {
                string panelName = $"{go.name}_Panel";
                if (panelContainer.Find(panelName) != null) continue;

                Transform partTransform = FindDeepChild(prefabRoot.transform, go.name);

                Renderer partRend = partTransform != null
                    ? partTransform.GetComponent<Renderer>()
                    : go.GetComponent<Renderer>();
                Vector3 partCenter = partRend != null ? partRend.bounds.center : engineCenter;

                Vector3 outDir = partCenter - engineCenter;
                if (outDir.sqrMagnitude < 0.0001f) outDir = Vector3.up;
                outDir.Normalize();

                Vector3 panelWorldPos = partCenter + outDir * autoOffset;

                GameObject panelGO = _hoverPanelTemplate != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(_hoverPanelTemplate)
                    : BuildDefaultHoverPanel(go.name, panelWorldPos, partCenter);

                panelGO.name = panelName;
                panelGO.transform.SetParent(panelContainer, true);
                panelGO.transform.position = panelWorldPos;

                Vector3 faceDir = partCenter - panelWorldPos;
                if (faceDir.sqrMagnitude > 0.0001f)
                    panelGO.transform.rotation = Quaternion.LookRotation(faceDir.normalized);

                var php = panelGO.GetComponent<PartHoverPanel>();
                if (php == null) php = panelGO.AddComponent<PartHoverPanel>();

                // Wire partAnchor -> part's own transform (no _Anchor child needed)
                if (partTransform != null)
                    php.partAnchor = partTransform;

                EnginePart ep = partTransform != null
                    ? partTransform.GetComponent<EnginePart>()
                    : go.GetComponent<EnginePart>();
                if (ep != null)
                    ep.hoverPanel = panelGO;

                string backupPath = $"{panelsFolder}/{SanitizeName(go.name)}_Panel.prefab";
                if (!File.Exists(backupPath))
                    PrefabUtility.SaveAsPrefabAsset(panelGO, backupPath);

                createdPanels++;
            }

            Debug.Log($"[Setup] {createdPanels} hover panels created, positioned, and wired.");
        }
        finally
        {
            scope?.Dispose();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Step 5: EngineData asset ──────────────────────────────────────────
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

        // ── Step 6: Registry ───────────────────────────────────────────────
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
            $"  • Layer '{EnginePartsLayerName}' (index {enginePartsLayer}) applied to all parts\n" +
            $"  • {addedParts} EnginePart components assigned\n" +
            $"  • {createdPartData} PartData assets created & wired\n" +
            $"  • {createdPanels} hover panels created, positioned & wired\n" +
            $"  • EngineData asset ready\n\n" +
            "Remaining steps:\n" +
            "1. Open Parts/ folder → fill description + drag audio into each PartData\n" +
            "2. Assign thumbnail in EngineData\n" +
            "3. Set spawnPosition / spawnRotation in EngineData",
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

    // ── Deep child finder ────────────────────────────────────────────────────

    /// <summary>Recursively searches all children for a GameObject with the given name.</summary>
    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
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

    // ── Engine bounds calculator ──────────────────────────────────────────────

    /// <summary>
    /// Returns the combined world-space bounds of all renderers on the instance.
    /// Call on a temporarily instantiated copy of the prefab.
    /// </summary>
    static Bounds ComputeEngineBounds(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        var bounds    = new Bounds(instance.transform.position, Vector3.zero);
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    // ── Default hover panel builder ───────────────────────────────────────────

    /// <summary>
    /// Builds a world-space canvas panel placed at worldPos, facing lookTarget.
    /// </summary>
    GameObject BuildDefaultHoverPanel(string partName, Vector3 worldPos, Vector3 lookTarget)
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

        // Place and orient the panel
        root.transform.position = worldPos;
        Vector3 faceDir = lookTarget - worldPos;
        if (faceDir.sqrMagnitude > 0.0001f)
            root.transform.rotation = Quaternion.LookRotation(faceDir.normalized);

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
