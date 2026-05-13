using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Tools > Static Setup Tool
///
/// Marks environment/room objects as Static for lightmap baking,
/// while keeping engine parts and VR interaction objects non-static.
///
/// WHY THIS IS NEEDED:
/// The Main Scene has 1098 objects all with StaticEditorFlags=0.
/// The lightmapper skips everything, bakes 0 lightmaps, and the scene
/// looks flat after scene transitions because there's no baked GI.
///
/// WHAT GETS MARKED STATIC:
/// - Room geometry (walls, floor, ceiling, furniture, props)
/// - Lights that are set to Mixed or Baked mode
///
/// WHAT STAYS NON-STATIC:
/// - Objects with EnginePart component (they animate/move)
/// - Objects under engine root GameObjects
/// - XR Rig and camera objects
/// - UI elements
/// </summary>
public class StaticSetupTool : EditorWindow
{
    // Objects/names to EXCLUDE from static marking
    private static readonly string[] ExcludeNameContains = new[]
    {
        "engine", "Engine", "XR", "Camera", "Canvas", "UI", "Button",
        "Panel", "Tablet", "Hand", "Controller", "Rig", "Player",
        "FadeQuad", "EventSystem", "Locomotion"
    };

    private static readonly string[] ExcludeTagContains = new[]
    {
        "MainCamera", "Player"
    };

    private bool _previewOnly = true;
    private Vector2 _scroll;
    private List<GameObject> _wouldMark = new();
    private List<GameObject> _wouldSkip = new();

    // Which static flags to apply
    private bool _flagContributeGI       = true;
    private bool _flagOccluderStatic     = true;
    private bool _flagOccludeeStatic     = true;
    private bool _flagBatchingStatic     = true;
    private bool _flagReflectionProbe    = true;

    [MenuItem("Tools/Static Setup Tool")]
    public static void Open() => GetWindow<StaticSetupTool>("Static Setup");

    void OnGUI()
    {
        GUILayout.Label("Static Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Marks environment objects as Static for lightmap baking.\n" +
            "Engine parts, XR objects, and UI are automatically excluded.",
            MessageType.Info);

        EditorGUILayout.Space(6);
        GUILayout.Label("Static Flags to Apply:", EditorStyles.boldLabel);
        _flagContributeGI    = EditorGUILayout.Toggle("Contribute GI (lightmaps)", _flagContributeGI);
        _flagOccluderStatic  = EditorGUILayout.Toggle("Occluder Static",           _flagOccluderStatic);
        _flagOccludeeStatic  = EditorGUILayout.Toggle("Occludee Static",           _flagOccludeeStatic);
        _flagBatchingStatic  = EditorGUILayout.Toggle("Batching Static",           _flagBatchingStatic);
        _flagReflectionProbe = EditorGUILayout.Toggle("Reflection Probe Static",   _flagReflectionProbe);

        EditorGUILayout.Space(6);

        if (GUILayout.Button("Scan Scene (Preview)", GUILayout.Height(32)))
            ScanScene();

        if (_wouldMark.Count > 0 || _wouldSkip.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Will mark STATIC:  {_wouldMark.Count} objects\n" +
                $"Will skip:         {_wouldSkip.Count} objects (engine parts / XR / UI)",
                MessageType.Warning);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));
            GUILayout.Label($"Objects that WILL be marked Static ({_wouldMark.Count}):", EditorStyles.boldLabel);
            foreach (var go in _wouldMark)
            {
                if (go == null) continue;
                EditorGUILayout.ObjectField(go, typeof(GameObject), true);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button($"APPLY — Mark {_wouldMark.Count} Objects as Static", GUILayout.Height(40)))
            {
                ApplyStatic();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "After applying:\n" +
            "1. Open Window → Rendering → Lighting\n" +
            "2. Click 'Generate Lighting'\n" +
            "3. Wait for bake to complete\n" +
            "4. Right-click LightingRestorer → 'Auto-Fill Lightmaps From Current Scene'\n" +
            "5. Save the scene (Ctrl+S)",
            MessageType.Info);
    }

    void ScanScene()
    {
        _wouldMark.Clear();
        _wouldSkip.Clear();

        var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var go in allObjects)
        {
            // Must have a renderer to contribute to lightmaps
            if (go.GetComponent<MeshRenderer>() == null) continue;

            if (ShouldExclude(go))
                _wouldSkip.Add(go);
            else
                _wouldMark.Add(go);
        }

        Debug.Log($"[StaticSetupTool] Scan complete: {_wouldMark.Count} to mark, {_wouldSkip.Count} to skip.");
        Repaint();
    }

    void ApplyStatic()
    {
        if (_wouldMark.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to mark", "Run Scan first.", "OK");
            return;
        }

        StaticEditorFlags flags = 0;
        if (_flagContributeGI)    flags |= StaticEditorFlags.ContributeGI;
        if (_flagOccluderStatic)  flags |= StaticEditorFlags.OccluderStatic;
        if (_flagOccludeeStatic)  flags |= StaticEditorFlags.OccludeeStatic;
        if (_flagBatchingStatic)  flags |= StaticEditorFlags.BatchingStatic;
        if (_flagReflectionProbe) flags |= StaticEditorFlags.ReflectionProbeStatic;

        Undo.SetCurrentGroupName("Mark Objects Static");
        int group = Undo.GetCurrentGroup();

        int count = 0;
        foreach (var go in _wouldMark)
        {
            if (go == null) continue;
            Undo.RecordObject(go, "Mark Static");
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            count++;
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[StaticSetupTool] ✓ Marked {count} objects as Static. Save the scene then Generate Lighting.");

        EditorUtility.DisplayDialog("Done",
            $"Marked {count} objects as Static.\n\n" +
            "Next steps:\n" +
            "1. Save the scene (Ctrl+S)\n" +
            "2. Window → Rendering → Lighting → Generate Lighting\n" +
            "3. Wait for bake\n" +
            "4. Right-click LightingRestorer → Auto-Fill Lightmaps",
            "OK");

        _wouldMark.Clear();
        _wouldSkip.Clear();
    }

    static bool ShouldExclude(GameObject go)
    {
        // Exclude if it has EnginePart (moves during explode animation)
        if (go.GetComponent<EnginePart>() != null) return true;

        // Exclude by tag
        foreach (var tag in ExcludeTagContains)
        {
            try { if (go.CompareTag(tag)) return true; } catch { }
        }

        // Exclude by name (check self and all parents)
        var t = go.transform;
        while (t != null)
        {
            foreach (var keyword in ExcludeNameContains)
            {
                if (t.gameObject.name.Contains(keyword))
                    return true;
            }
            t = t.parent;
        }

        return false;
    }
}
