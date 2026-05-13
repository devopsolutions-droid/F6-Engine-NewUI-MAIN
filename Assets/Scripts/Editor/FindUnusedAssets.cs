using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans all scenes and prefabs to find which 3D model assets (GLB, FBX, OBJ)
/// are never referenced anywhere in the project.
/// 
/// Usage: Top menu → Tools → Find Unused 3D Models
/// Results are printed to Console and saved to Assets/UnusedAssets.txt
/// </summary>
public class FindUnusedAssets : EditorWindow
{
    private static readonly string[] ModelExtensions = { ".glb", ".fbx", ".obj", ".gltf" };
    private static readonly string[] LargeExtensions = { ".glb", ".fbx", ".obj", ".gltf", ".mp4", ".mp3", ".wav", ".png", ".jpg", ".exr", ".psd" };

    private Vector2 _scroll;
    private List<string> _unusedPaths = new();
    private List<string> _unusedLargePaths = new();
    private bool _scanned = false;
    private bool _showLarge = false;

    [MenuItem("Tools/Find Unused 3D Models")]
    public static void ShowWindow()
    {
        GetWindow<FindUnusedAssets>("Unused Assets");
    }

    void OnGUI()
    {
        GUILayout.Label("Find Unused Assets", EditorStyles.boldLabel);
        GUILayout.Space(5);

        _showLarge = GUILayout.Toggle(_showLarge, "Include all large files (audio, textures, video)");
        GUILayout.Space(5);

        if (GUILayout.Button("Scan Project", GUILayout.Height(30)))
            Scan(_showLarge ? LargeExtensions : ModelExtensions);

        if (_scanned)
        {
            var list = _showLarge ? _unusedLargePaths : _unusedPaths;
            GUILayout.Space(5);

            long totalBytes = list.Sum(p =>
            {
                var fi = new FileInfo(p);
                return fi.Exists ? fi.Length : 0;
            });

            GUILayout.Label($"Found {list.Count} unused files  ({totalBytes / 1024 / 1024} MB that can be removed)",
                EditorStyles.boldLabel);

            GUILayout.Space(5);

            if (GUILayout.Button("Save list to Assets/UnusedAssets.txt"))
                SaveToFile(list);

            GUILayout.Space(5);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var path in list)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(path, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    EditorGUIUtility.PingObject(obj);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }

    void Scan(string[] extensions)
    {
        _unusedPaths.Clear();
        _unusedLargePaths.Clear();

        // ── Step 1: Collect all asset paths with target extensions ────────────
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        var candidates = allAssets
            .Where(p => p.StartsWith("Assets/") &&
                        extensions.Contains(Path.GetExtension(p).ToLower()))
            .ToList();

        Debug.Log($"[FindUnusedAssets] Scanning {candidates.Count} candidate files...");

        // ── Step 2: Collect all GUIDs referenced by scenes + prefabs ─────────
        var referencedGuids = new HashSet<string>();

        // All scenes in build settings + all scenes in project
        var scenePaths = allAssets.Where(p => p.EndsWith(".unity")).ToList();
        // All prefabs
        var prefabPaths = allAssets.Where(p => p.EndsWith(".prefab")).ToList();
        // All scriptable objects
        var soPaths = allAssets.Where(p => p.EndsWith(".asset")).ToList();

        var filesToSearch = scenePaths
            .Concat(prefabPaths)
            .Concat(soPaths)
            .ToList();

        int total = filesToSearch.Count;
        int done  = 0;

        foreach (var filePath in filesToSearch)
        {
            done++;
            if (done % 50 == 0)
                EditorUtility.DisplayProgressBar("Scanning...", filePath, (float)done / total);

            // Read raw file text and extract GUIDs
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            if (!File.Exists(fullPath)) continue;

            string content;
            try { content = File.ReadAllText(fullPath); }
            catch { continue; }

            // GUIDs appear as: guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
            int idx = 0;
            while ((idx = content.IndexOf("guid: ", idx)) != -1)
            {
                idx += 6;
                if (idx + 32 <= content.Length)
                {
                    string guid = content.Substring(idx, 32);
                    if (guid.All(c => "0123456789abcdefABCDEF".Contains(c)))
                        referencedGuids.Add(guid);
                }
            }
        }

        EditorUtility.ClearProgressBar();

        // ── Step 3: Check which candidates are NOT referenced ─────────────────
        var result = new List<string>();
        foreach (var assetPath in candidates)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!referencedGuids.Contains(guid))
                result.Add(assetPath);
        }

        result.Sort();

        if (_showLarge)
            _unusedLargePaths = result;
        else
            _unusedPaths = result;

        _scanned = true;

        long totalMB = result.Sum(p =>
        {
            var fi = new FileInfo(p);
            return fi.Exists ? fi.Length : 0L;
        }) / 1024 / 1024;

        Debug.Log($"[FindUnusedAssets] Done. {result.Count} unused files found ({totalMB} MB).");
    }

    void SaveToFile(List<string> list)
    {
        string output = string.Join("\n", list);
        File.WriteAllText("Assets/UnusedAssets.txt", output);
        AssetDatabase.Refresh();
        Debug.Log("[FindUnusedAssets] Saved to Assets/UnusedAssets.txt");
    }
}
