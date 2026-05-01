using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > Engine Part Setup
/// Drag a raw engine prefab/model in, click Setup — it auto-assigns EnginePart
/// components to every mesh child and generates an EngineData ScriptableObject asset.
/// </summary>
public class EnginePartSetupTool : EditorWindow
{
    private GameObject _engineModel;
    private EngineRegistry _registry;
    private string _engineName = "New Engine";
    private string _engineCategory = "General";
    private string _savePath = "Assets/ScriptableObjects/Data/Engines";

    [MenuItem("Tools/Engine Part Setup")]
    public static void Open() => GetWindow<EnginePartSetupTool>("Engine Part Setup");

    void OnGUI()
    {
        GUILayout.Label("Engine Part Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _engineModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine Model / Prefab", _engineModel, typeof(GameObject), true);

        _engineName = EditorGUILayout.TextField("Engine Name", _engineName);
        _engineCategory = EditorGUILayout.TextField("Category", _engineCategory);

        EditorGUILayout.Space(4);
        _registry = (EngineRegistry)EditorGUILayout.ObjectField(
            "Engine Registry (optional)", _registry, typeof(EngineRegistry), false);

        EditorGUILayout.Space(4);
        _savePath = EditorGUILayout.TextField("Save Path", _savePath);

        EditorGUILayout.Space(10);

        GUI.enabled = _engineModel != null;

        if (GUILayout.Button("1 — Assign EnginePart Components", GUILayout.Height(32)))
            AssignEnginePartComponents();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("2 — Generate EngineData Asset", GUILayout.Height(32)))
            GenerateEngineDataAsset();

        GUI.enabled = true;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Step 1: Assigns EnginePart to every mesh child of the model.\n" +
            "Step 2: Creates an EngineData .asset and optionally adds it to the registry.\n\n" +
            "After Step 2, assign the thumbnail and prefab in the generated asset.",
            MessageType.Info);
    }

    void AssignEnginePartComponents()
    {
        if (_engineModel == null) return;

        var renderers = _engineModel.GetComponentsInChildren<Renderer>(true);
        int added = 0;

        foreach (var r in renderers)
        {
            var go = r.gameObject;
            if (go.GetComponent<EnginePart>() != null) continue;

            var part = go.AddComponent<EnginePart>();
            part.partName = go.name;
            added++;
        }

        EditorUtility.SetDirty(_engineModel);
        Debug.Log($"[EnginePartSetupTool] Added EnginePart to {added} objects on '{_engineModel.name}'.");
        EditorUtility.DisplayDialog("Done", $"Added EnginePart to {added} mesh objects.", "OK");
    }

    void GenerateEngineDataAsset()
    {
        if (_engineModel == null) return;

        // Ensure save directory exists
        if (!Directory.Exists(_savePath))
            Directory.CreateDirectory(_savePath);

        string assetName = _engineName.Replace(" ", "") + "Data";
        string fullPath = $"{_savePath}/{assetName}.asset";

        // Avoid overwriting
        if (File.Exists(fullPath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Asset Exists",
                $"'{assetName}.asset' already exists. Overwrite?",
                "Overwrite", "Cancel");
            if (!overwrite) return;
        }

        var data = ScriptableObject.CreateInstance<EngineData>();
        data.engineName = _engineName;
        data.engineCategory = _engineCategory;

        // Try to link the prefab if it's already a prefab asset
        string prefabPath = AssetDatabase.GetAssetPath(_engineModel);
        if (!string.IsNullOrEmpty(prefabPath))
            data.enginePrefab = _engineModel;

        AssetDatabase.CreateAsset(data, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Optionally add to registry
        if (_registry != null)
        {
            if (!_registry.engines.Contains(data))
            {
                _registry.engines.Add(data);
                EditorUtility.SetDirty(_registry);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnginePartSetupTool] Added '{data.engineName}' to registry.");
            }
        }

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = data;

        Debug.Log($"[EnginePartSetupTool] Created EngineData at '{fullPath}'.");
        EditorUtility.DisplayDialog("Done",
            $"EngineData asset created at:\n{fullPath}\n\nNow assign the thumbnail and prefab in the Inspector.",
            "OK");
    }
}
