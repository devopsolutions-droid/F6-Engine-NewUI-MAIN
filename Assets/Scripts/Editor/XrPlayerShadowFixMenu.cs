#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;

public static class XrPlayerShadowFixMenu
{
    [MenuItem("Tools/Fix XR Rig Floor Shadow")]
    public static void FixXrRigShadows()
    {
        var origin = Object.FindObjectOfType<XROrigin>();
        if (origin == null)
        {
            EditorUtility.DisplayDialog(
                "XR Origin not found",
                "Open a scene that contains XR Origin (XR Rig).",
                "OK");
            return;
        }

        var renderers = origin.GetComponentsInChildren<Renderer>(true);
        var count = 0;
        Undo.SetCurrentGroupName("Fix XR Rig Floor Shadow");

        foreach (var renderer in renderers)
        {
            Undo.RecordObject(renderer, "Fix XR Rig Floor Shadow");
            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                count++;
            }
        }

        var fix = origin.GetComponent<XrPlayerShadowFix>();
        if (fix == null)
        {
            Undo.AddComponent<XrPlayerShadowFix>(origin.gameObject);
        }

        EditorUtility.SetDirty(origin.gameObject);
        Debug.Log($"[XR Shadow Fix] Turned off cast shadows on {count} renderer(s) under '{origin.name}'.");

        EditorUtility.DisplayDialog(
            "XR shadow fix applied",
            $"Disabled cast shadows on {count} renderer(s) under:\n{origin.name}\n\n" +
            "Added XrPlayerShadowFix so new hand meshes stay fixed at runtime.",
            "OK");
    }
}
#endif
