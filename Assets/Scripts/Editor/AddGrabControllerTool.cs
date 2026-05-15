using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Tools > Add Grab Controller to All Parts
///
/// Finds every active EnginePart in the scene (or in a selected prefab root)
/// and adds:
///   • Rigidbody  (Is Kinematic = true, no gravity)
///   • EnginePartGrabController
///
/// Safe to run multiple times — skips parts that already have the components.
/// Run this AFTER the Engine Part Setup Tool has already added EnginePart components.
/// </summary>
public class AddGrabControllerTool : EditorWindow
{
    private GameObject _engineRoot;   // optional: target a specific root
    private bool       _searchScene = true;

    [MenuItem("Tools/Add Grab Controller to All Parts")]
    public static void Open() => GetWindow<AddGrabControllerTool>("Add Grab Controller");

    void OnGUI()
    {
        GUILayout.Label("Add Grab Controller to All Parts", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _searchScene = EditorGUILayout.Toggle("Search Entire Scene", _searchScene);

        if (!_searchScene)
        {
            _engineRoot = (GameObject)EditorGUILayout.ObjectField(
                "Engine Root (optional)", _engineRoot, typeof(GameObject), true);
            EditorGUILayout.HelpBox(
                "Drag the engine root here to limit the search to its children only.",
                MessageType.Info);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("ADD TO ALL ENGINE PARTS", GUILayout.Height(40)))
            Run();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "What this does to every EnginePart found:\n" +
            "  • Removes Rigidbody (caused whole-engine grab)\n" +
            "  • Removes XRGrabInteractable (same problem)\n" +
            "  • Adds EnginePartGrabController (lightweight marker)\n\n" +
            "Already has EnginePartGrabController? Skipped — safe to re-run.\n\n" +
            "After running:\n" +
            "  1. Add ONE EngineGrabManager to your scene\n" +
            "  2. Assign XRRayInteractor + grab trigger in its Inspector\n" +
            "  3. Assign depth thumbstick (same Move action as EngineInteractor)\n" +
            "  4. Set the EngineParts LayerMask to match your layer",
            MessageType.Info);
    }

    void Run()
    {
        // Collect all EnginePart components
        EnginePart[] parts;

        if (_searchScene || _engineRoot == null)
        {
            // FindObjectsByType finds all active EngineParts in the open scene(s)
            parts = FindObjectsByType<EnginePart>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        else
        {
            parts = _engineRoot.GetComponentsInChildren<EnginePart>(true);
        }

        if (parts == null || parts.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing Found",
                "No EnginePart components found.\n\n" +
                "Run the Engine Part Setup Tool first to add EnginePart components,\n" +
                "then come back here.",
                "OK");
            return;
        }

        int addedGrab = 0;
        int skipped   = 0;

        // Register undo so the whole operation can be undone in one step
        Undo.SetCurrentGroupName("Add Grab Controllers");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var part in parts)
        {
            if (part == null) continue;
            GameObject go = part.gameObject;

            // ── Remove any stale Rigidbody — it causes the whole engine to move ──
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Undo.DestroyObjectImmediate(rb);
                Debug.Log($"[AddGrabController] Removed Rigidbody from '{go.name}' (would cause whole-engine grab).");
            }

            // ── Remove any stale XRGrabInteractable for the same reason ──────
            var xrGrab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
            if (xrGrab != null)
            {
                Undo.DestroyObjectImmediate(xrGrab);
                Debug.Log($"[AddGrabController] Removed XRGrabInteractable from '{go.name}'.");
            }

            // ── EnginePartGrabController ──────────────────────────────────────
            var grab = go.GetComponent<EnginePartGrabController>();
            if (grab == null)
            {
                Undo.AddComponent<EnginePartGrabController>(go);
                addedGrab++;
            }
            else
            {
                skipped++;
            }

            EditorUtility.SetDirty(go);
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        string msg =
            $"Done — processed {parts.Length} EngineParts.\n\n" +
            $"  • EnginePartGrabController added: {addedGrab}\n" +
            $"  • Already had it (skipped):       {skipped}\n\n" +
            "Rigidbody + XRGrabInteractable removed from any parts that had them\n" +
            "(they caused the whole engine to move as one piece).\n\n" +
            "Next: add ONE EngineGrabManager to your scene and assign\n" +
            "the XRRayInteractor, grab trigger, and depth thumbstick (Move action)\n" +
            "in its Inspector.\n\n" +
            "You can Ctrl+Z to undo the entire batch.";

        Debug.Log($"[AddGrabController] {msg}");
        EditorUtility.DisplayDialog("Done ✓", msg, "OK");
    }
}
