#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

/// <summary>
/// Finds which Ultimate Clean GUI Pack files are actually referenced by the rest of the project.
/// Use before deleting unused pack content from GitHub-sized repos.
/// </summary>
public static class UltimateCleanGUIPackUsage
{
    const string PackRoot = "Assets/Room Assets/UI Assets/UltimateCleanGUIPack";

    [MenuItem("Tools/Ultimate Clean GUI Pack/Analyze Used Assets (writes report)")]
    static void Analyze()
    {
        var used = CollectUsedPackPaths();
        var allPack = AllAssetPathsUnder(PackRoot).Where(p => !p.EndsWith(".meta")).ToList();
        var unused = allPack.Where(p => !used.Contains(p)).OrderBy(p => p).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Pack assets total: {allPack.Count}");
        sb.AppendLine($"Referenced from elsewhere (Unity AssetDatabase): {used.Count}");
        sb.AppendLine($"Safe deletion candidates (verify first): {unused.Count}");
        sb.AppendLine();
        sb.AppendLine("=== USED ===");
        foreach (var p in used.OrderBy(x => x))
            sb.AppendLine(p);
        sb.AppendLine();
        sb.AppendLine("=== UNUSED (not referenced by any asset outside pack) ===");
        foreach (var p in unused)
            sb.AppendLine(p);

        string outPath = "ProjectSettings/UltimateCleanGUIPack_usage_report.txt";
        File.WriteAllText(outPath, sb.ToString());
        EditorUtility.DisplayDialog(
            "Ultimate Clean GUI Pack",
            $"Done.\n\nUsed in pack: {used.Count}\nUnused candidates: {unused.Count}\n\nReport:\n{outPath}",
            "OK");
    }

    static HashSet<string> CollectUsedPackPaths()
    {
        var used = new HashSet<string>();
        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/")) continue;
            if (path.StartsWith(PackRoot)) continue;
            if (path.EndsWith(".meta")) continue;

            foreach (var dep in AssetDatabase.GetDependencies(path, true))
            {
                if (dep.StartsWith(PackRoot))
                    used.Add(dep);
            }
        }

        return used;
    }

    static IEnumerable<string> AllAssetPathsUnder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            yield break;

        foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p))
                yield return p;
        }
    }
}
#endif
