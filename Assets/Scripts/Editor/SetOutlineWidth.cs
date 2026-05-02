using UnityEditor;
using UnityEngine;

public static class SetOutlineWidth
{
    private const float TargetWidth = 0.01343f;

    [MenuItem("Tools/Set Outline Width on All Prefabs")]
    static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int changed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var parts = prefab.GetComponentsInChildren<EnginePart>(true);
            if (parts.Length == 0) continue;

            bool dirty = false;
            foreach (var part in parts)
            {
                if (Mathf.Approximately(part.outlineWidth, TargetWidth)) continue;
                part.outlineWidth = TargetWidth;
                EditorUtility.SetDirty(part);
                dirty = true;
            }

            if (dirty)
            {
                PrefabUtility.SavePrefabAsset(prefab);
                changed++;
                Debug.Log($"[SetOutlineWidth] Updated: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetOutlineWidth] Done — {changed} prefab(s) updated to outlineWidth = {TargetWidth}");
    }
}
