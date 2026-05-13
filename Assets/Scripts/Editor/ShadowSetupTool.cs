using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool to configure Cast/Receive Shadows on dragged-in GameObjects.
/// Menu: Tools → Shadow Setup Tool
/// </summary>
public class ShadowSetupTool : EditorWindow
{
    // ── Draggable object list ─────────────────────────────────────────────────
    private List<GameObject> _targets = new List<GameObject>();
    private SerializedObject _serializedWindow;
    private Vector2 _scroll;

    // ── Shadow options ────────────────────────────────────────────────────────
    private UnityEngine.Rendering.ShadowCastingMode _castMode =
        UnityEngine.Rendering.ShadowCastingMode.On;
    private bool _receiveShadows = true;
    private bool _includeChildren = true;

    [MenuItem("Tools/Shadow Setup Tool")]
    public static void ShowWindow()
    {
        var w = GetWindow<ShadowSetupTool>("Shadow Setup");
        w.minSize = new Vector2(360, 480);
    }

    void OnGUI()
    {
        // ── Title ─────────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("Shadow Setup Tool", title);
        EditorGUILayout.Space(4);
        DrawLine();

        // ── Drop zone ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Drag GameObjects here:", EditorStyles.boldLabel);

        Rect dropRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, _targets.Count == 0
            ? "Drop GameObjects here"
            : $"{_targets.Count} object(s) added",
            new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize   = 11,
                normal     = { textColor = Color.cyan }
            });

        HandleDrop(dropRect);

        // ── Object list ───────────────────────────────────────────────────────
        if (_targets.Count > 0)
        {
            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.MaxHeight(160));

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                _targets[i] = (GameObject)EditorGUILayout.ObjectField(
                    _targets[i], typeof(GameObject), true);

                if (GUILayout.Button("✕", GUILayout.Width(24)))
                    _targets.RemoveAt(i);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear All"))
                _targets.Clear();
        }

        DrawLine();

        // ── Shadow settings ───────────────────────────────────────────────────
        EditorGUILayout.LabelField("Shadow Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        _castMode = (UnityEngine.Rendering.ShadowCastingMode)
            EditorGUILayout.EnumPopup("Cast Shadows", _castMode);

        _receiveShadows = EditorGUILayout.Toggle("Receive Shadows", _receiveShadows);
        _includeChildren = EditorGUILayout.Toggle("Include Children", _includeChildren);

        DrawLine();

        // ── Apply button ──────────────────────────────────────────────────────
        EditorGUILayout.Space(4);

        GUI.enabled = _targets.Count > 0;
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);

        if (GUILayout.Button("Apply Shadows", GUILayout.Height(36)))
            ApplyShadows();

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // ── Quick presets ─────────────────────────────────────────────────────
        DrawLine();
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Full Shadows\n(Cast + Receive)", GUILayout.Height(40)))
        {
            _castMode       = UnityEngine.Rendering.ShadowCastingMode.On;
            _receiveShadows = true;
            if (_targets.Count > 0) ApplyShadows();
        }

        if (GUILayout.Button("Cast Only\n(no receive)", GUILayout.Height(40)))
        {
            _castMode       = UnityEngine.Rendering.ShadowCastingMode.On;
            _receiveShadows = false;
            if (_targets.Count > 0) ApplyShadows();
        }

        if (GUILayout.Button("Shadows Off\n(both)", GUILayout.Height(40)))
        {
            _castMode       = UnityEngine.Rendering.ShadowCastingMode.Off;
            _receiveShadows = false;
            if (_targets.Count > 0) ApplyShadows();
        }

        EditorGUILayout.EndHorizontal();

        // ── Status ────────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        int rendererCount = CountRenderers();
        EditorGUILayout.HelpBox(
            _targets.Count == 0
                ? "No objects added yet. Drag GameObjects into the drop zone."
                : $"Ready — will affect {rendererCount} Mesh Renderer(s) across {_targets.Count} object(s).",
            _targets.Count == 0 ? MessageType.Info : MessageType.None);
    }

    // ── Drag & drop handler ───────────────────────────────────────────────────
    void HandleDrop(Rect dropRect)
    {
        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && !_targets.Contains(go))
                    _targets.Add(go);
            }
            evt.Use();
            Repaint();
        }
    }

    // ── Apply logic ───────────────────────────────────────────────────────────
    void ApplyShadows()
    {
        int count = 0;
        Undo.SetCurrentGroupName("Apply Shadow Settings");
        int group = Undo.GetCurrentGroup();

        foreach (var go in _targets)
        {
            if (go == null) continue;

            var renderers = _includeChildren
                ? go.GetComponentsInChildren<Renderer>(true)
                : go.GetComponents<Renderer>();

            foreach (var r in renderers)
            {
                Undo.RecordObject(r, "Shadow Settings");
                r.shadowCastingMode = _castMode;
                r.receiveShadows    = _receiveShadows;

                // Mark dirty — works for both scene objects and prefab instances
                EditorUtility.SetDirty(r);
                EditorUtility.SetDirty(r.gameObject);

                // If it's part of a prefab instance in the scene, mark the scene dirty
                var prefabRoot = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                if (prefabRoot != null)
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(r);

                count++;
            }
        }

        Undo.CollapseUndoOperations(group);

        // Force scene to save the changes
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[ShadowSetupTool] ✅ Applied to {count} renderers across {_targets.Count} objects. Cast={_castMode} Receive={_receiveShadows}");

        if (count == 0)
            Debug.LogWarning("[ShadowSetupTool] ⚠️ 0 renderers found! Make sure your objects have Mesh Renderer or Skinned Mesh Renderer components. Try enabling 'Include Children'.");
    }

    int CountRenderers()
    {
        int n = 0;
        foreach (var go in _targets)
        {
            if (go == null) continue;
            n += _includeChildren
                ? go.GetComponentsInChildren<Renderer>(true).Length
                : go.GetComponents<Renderer>().Length;
        }
        return n;
    }

    void DrawLine()
    {
        EditorGUILayout.Space(4);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(4);
    }
}
