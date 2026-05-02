using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Tools > Groq Part Description Generator
///
/// Usage:
///   1. Drop your GLB / prefab into the "3D Model" field
///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
///   3. Click Generate
///
/// The tool auto-finds the matching EngineData + Parts folder,
/// extracts real mesh geometry (position, size, shape, vertices, material),
/// sends everything to Groq llama-3.3-70b, and writes accurate names +
/// descriptions back into every PartData asset automatically.
/// </summary>
public class GroqPartDescriptionGenerator : EditorWindow
{
    private const string GroqEndpoint   = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model          = "llama-3.3-70b-versatile";
    private const string PrefKeyApi     = "GroqAPIKey";
    private const string PrefKeyType    = "GroqModelType";

    private string      _apiKey     = "";
    private string      _modelType  = "";
    private GameObject  _droppedModel;
    private string      _status     = "";
    private bool        _running    = false;
    private Vector2     _scroll;

    [MenuItem("Tools/Groq Part Description Generator")]
    public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

    void OnEnable()
    {
        _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
        _modelType = EditorPrefs.GetString(PrefKeyType, "");
    }

    void OnGUI()
    {
        GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        // ── API Key ───────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
        string newKey = EditorGUILayout.PasswordField(_apiKey);
        if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }

        EditorGUILayout.Space(6);

        // ── Drop model ────────────────────────────────────────────────────────
        var newModel = (GameObject)EditorGUILayout.ObjectField(
            "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
        if (newModel != _droppedModel)
        {
            _droppedModel = newModel;
            // Auto-fill model type from asset name if field is empty
            if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
            {
                _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
                EditorPrefs.SetString(PrefKeyType, _modelType);
            }
        }

        // ── Model type ────────────────────────────────────────────────────────
        EditorGUILayout.Space(2);
        string newType = EditorGUILayout.TextField("What is this model?", _modelType);
        if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }

        EditorGUILayout.HelpBox(
            "Be specific for best accuracy.\n" +
            "Examples:\n" +
            "  • V8 Twin Turbo gasoline engine\n" +
            "  • F6 Boxer engine (Porsche 911)\n" +
            "  • CFM56 Turbofan jet engine\n" +
            "  • Ferrari 458 Italia V8 engine\n" +
            "  • Diesel truck engine",
            MessageType.Info);

        EditorGUILayout.Space(8);

        // ── Generate button ───────────────────────────────────────────────────
        bool canRun = !_running
                   && !string.IsNullOrEmpty(_apiKey)
                   && _droppedModel != null
                   && !string.IsNullOrEmpty(_modelType);

        GUI.enabled = canRun;
        if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
            RunGeneration();
        GUI.enabled = true;

        if (_droppedModel == null)
            EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_modelType))
            EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

        EditorGUILayout.Space(6);

        // ── Status ────────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_status))
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
    }

    // ─────────────────────────────────────────────────────────────────────────

    async void RunGeneration()
    {
        _running = true;
        _status  = "Locating Parts folder for this model...";
        Repaint();

        // ── Find the Parts folder that belongs to this prefab ─────────────────
        string partsFolder = FindPartsFolderForModel(_droppedModel);
        if (partsFolder == null)
        {
            _status  = "ERROR: Could not find a Parts folder for this model.\n\n" +
                       "Make sure you ran  Tools → Engine Part Setup  on this model first.\n" +
                       "That tool creates the Parts/ folder and PartData assets automatically.";
            _running = false; Repaint(); return;
        }

        _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
        Repaint();

        // ── Load PartData assets ──────────────────────────────────────────────
        var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
        if (guids.Length == 0)
        {
            _status  = $"No PartData assets found in:\n{partsFolder}\n\nRun Engine Part Setup first.";
            _running = false; Repaint(); return;
        }

        var parts = new List<PartData>();
        foreach (var g in guids)
        {
            var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
            if (pd != null) parts.Add(pd);
        }

        _status = $"Found {parts.Count} parts.\nExtracting mesh geometry...";
        Repaint();

        // ── Extract geometry from the prefab ──────────────────────────────────
        var geoData = ExtractGeometry(_droppedModel, parts);

        _status = $"Geometry extracted.\nSending {parts.Count} parts to Groq AI...";
        Repaint();

        // ── Call Groq ─────────────────────────────────────────────────────────
        string prompt  = BuildPrompt(geoData, _modelType);
        var    results = await CallGroq(prompt);

        if (results == null || results.Count == 0)
        {
            _status  = "ERROR: Groq request failed or returned empty response.\n" +
                       "Check your API key and internet connection.\nSee Console for details.";
            _running = false; Repaint(); return;
        }

        // ── Write results back ────────────────────────────────────────────────
        int applied = 0;
        for (int i = 0; i < parts.Count && i < results.Count; i++)
        {
            if (!string.IsNullOrEmpty(results[i].name))
                parts[i].partName = results[i].name;
            if (!string.IsNullOrEmpty(results[i].description))
                parts[i].description = results[i].description;
            EditorUtility.SetDirty(parts[i]);
            applied++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _status  = $"✓ Done!  {applied} / {parts.Count} parts updated.\n\n" +
                   $"All PartData assets saved to:\n{partsFolder}";
        _running = false;
        Repaint();
    }

    // ── Auto-find Parts folder ────────────────────────────────────────────────

    /// <summary>
    /// Searches all EngineData assets in the project for one whose enginePrefab
    /// matches the dropped model, then returns its sibling Parts/ folder.
    /// Falls back to searching by prefab name if no exact match found.
    /// </summary>
    static string FindPartsFolderForModel(GameObject model)
    {
        string modelAssetPath = AssetDatabase.GetAssetPath(model);
        string modelName      = model.name;

        // Search all EngineData assets
        string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
        foreach (var guid in edGuids)
        {
            var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ed == null) continue;

            bool match = false;

            // Exact prefab reference match
            if (ed.enginePrefab != null)
            {
                string edPrefabPath = AssetDatabase.GetAssetPath(ed.enginePrefab);
                match = edPrefabPath == modelAssetPath;
            }

            // Name-based fallback
            if (!match && ed.engineName != null)
                match = ed.engineName.Replace(" ", "").ToLower() == modelName.Replace(" ", "").ToLower();

            if (match)
            {
                string edPath    = AssetDatabase.GetAssetPath(ed);
                string edDir     = Path.GetDirectoryName(edPath).Replace("\\", "/");
                string partsPath = edDir + "/Parts";
                if (Directory.Exists(partsPath)) return partsPath;
            }
        }

        // Last resort: look for a Parts folder whose parent folder name matches the model name
        string[] allPartGuids = AssetDatabase.FindAssets("t:PartData");
        foreach (var guid in allPartGuids)
        {
            string path      = AssetDatabase.GUIDToAssetPath(guid);
            string partsDir  = Path.GetDirectoryName(path).Replace("\\", "/");
            string parentDir = Path.GetFileName(Path.GetDirectoryName(partsDir));
            if (parentDir.Replace(" ", "").ToLower().Contains(modelName.Replace(" ", "").ToLower()))
                return partsDir;
        }

        return null;
    }

    // ── Geometry Extraction ───────────────────────────────────────────────────

    struct PartGeo
    {
        public string rawName;
        public string position;   // e.g. "top-center-front"
        public string size;       // large / medium / small / tiny
        public string shape;      // complex / elongated / thin-flat / compact
        public int    vertices;
        public string material;
    }

    static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
    {
        var result   = new List<PartGeo>();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            // Overall engine bounds
            var allR = instance.GetComponentsInChildren<Renderer>(true);
            var engBounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (var r in allR) engBounds.Encapsulate(r.bounds);
            Vector3 ec = engBounds.center;
            Vector3 es = engBounds.size;

            // Name → (MeshFilter, Renderer) lookup
            var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
                if (!lookup.ContainsKey(mf.gameObject.name))
                    lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

            foreach (var pd in parts)
            {
                var geo = new PartGeo { rawName = pd.partName };

                if (lookup.TryGetValue(pd.partName, out var entry)
                    && entry.mf != null && entry.mf.sharedMesh != null)
                {
                    var mesh = entry.mf.sharedMesh;
                    var rend = entry.rend;
                    var b    = rend != null ? rend.bounds : new Bounds(entry.mf.transform.position, Vector3.zero);

                    // Relative position
                    Vector3 rel = b.center - ec;
                    float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
                    float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
                    float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
                    string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
                    string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
                    string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
                    geo.position = $"{py}-{px}-{pz}";

                    // Relative size
                    float pv   = b.size.x * b.size.y * b.size.z;
                    float ev   = es.x * es.y * es.z;
                    float rat  = ev > 0 ? pv / ev : 0;
                    geo.size   = rat > 0.15f ? "large" : rat > 0.04f ? "medium" : rat > 0.008f ? "small" : "tiny";

                    // Shape
                    float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
                    float asp  = maxD > 0 ? minD / maxD : 1;
                    geo.shape  = asp < 0.15f ? "thin-flat"
                               : asp < 0.4f  ? "elongated"
                               : mesh.vertexCount > 2000 ? "complex" : "compact";

                    geo.vertices = mesh.vertexCount;

                    // Material hint
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        string mn = rend.sharedMaterial.name
                            .Replace("(Instance)", "").Replace("_", " ").Trim();
                        geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
                    }
                }
                else
                {
                    geo.position = "unknown";
                    geo.size     = "unknown";
                    geo.shape    = "unknown";
                }

                result.Add(geo);
            }
        }
        finally { DestroyImmediate(instance); }

        return result;
    }

    // ── Prompt ────────────────────────────────────────────────────────────────

    static string BuildPrompt(List<PartGeo> geoData, string modelType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert mechanical engineer and technical writer.");
        sb.AppendLine($"You are analyzing a 3D model of: {modelType}");
        sb.AppendLine();
        sb.AppendLine("Each part below has geometric data extracted from the actual 3D mesh:");
        sb.AppendLine("  position = location relative to model center (vertical-horizontal-depth)");
        sb.AppendLine("  size     = relative to whole model (large/medium/small/tiny)");
        sb.AppendLine("  shape    = mesh shape classification");
        sb.AppendLine("  vertices = mesh complexity");
        sb.AppendLine("  material = material name hint from the 3D file");
        sb.AppendLine();
        sb.AppendLine("PARTS:");

        for (int i = 0; i < geoData.Count; i++)
        {
            var g = geoData[i];
            sb.Append($"{i + 1}.");
            sb.Append($" position={g.position}");
            sb.Append($" size={g.size}");
            sb.Append($" shape={g.shape}");
            sb.Append($" vertices={g.vertices}");
            if (!string.IsNullOrEmpty(g.material))
                sb.Append($" material=\"{g.material}\"");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Return a JSON array of exactly {geoData.Count} objects.");
        sb.AppendLine("Each object: {\"name\": \"Part Name\", \"description\": \"2-3 sentence technical description.\"}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Every part name must be UNIQUE — no duplicates");
        sb.AppendLine("- Names must be real mechanical part names for this specific model type");
        sb.AppendLine("- Descriptions must be technically accurate and educational");
        sb.AppendLine("- Use geometry data to reason: large+top+complex = main block/head, tiny+scattered = bolts/sensors, thin-flat = gaskets/shields, bottom = oil pan/sump, rear = flywheel/clutch");
        sb.AppendLine("- Return ONLY the raw JSON array, no markdown, no explanation");

        return sb.ToString();
    }

    // ── Groq API ──────────────────────────────────────────────────────────────

    async Task<List<(string name, string description)>> CallGroq(string prompt)
    {
        string body = "{\"model\":\"" + Model + "\"," +
                      "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
                      "\"temperature\":0.2,\"max_tokens\":6000}";
        try
        {
            using var req = new UnityWebRequest(GroqEndpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            string content = ExtractContent(req.downloadHandler.text);
            Debug.Log($"[Groq] Response:\n{content}");
            return string.IsNullOrEmpty(content) ? null : ParseArray(content);
        }
        catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
    }

    // ── JSON Helpers ──────────────────────────────────────────────────────────

    static List<(string, string)> ParseArray(string content)
    {
        var list  = new List<(string, string)>();
        int start = content.IndexOf('[');
        int end   = content.LastIndexOf(']');
        if (start < 0 || end <= start) return null;

        string json = content.Substring(start, end - start + 1);
        int i = 0;
        while (i < json.Length)
        {
            int os = json.IndexOf('{', i); if (os < 0) break;
            int oe = FindBrace(json, os);  if (oe < 0) break;
            string obj = json.Substring(os, oe - os + 1);
            list.Add((ReadStr(obj, "name") ?? "", ReadStr(obj, "description") ?? ""));
            i = oe + 1;
        }
        return list;
    }

    static int FindBrace(string s, int from)
    {
        int d = 0; bool inS = false;
        for (int i = from; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && inS) { i++; continue; }
            if (c == '"') { inS = !inS; continue; }
            if (inS) continue;
            if (c == '{') d++; else if (c == '}') { d--; if (d == 0) return i; }
        }
        return -1;
    }

    static string ExtractContent(string resp)
    {
        int idx = resp.IndexOf("\"content\":");
        if (idx < 0) return null;
        idx += 10;
        while (idx < resp.Length && resp[idx] != '"') idx++;
        if (idx >= resp.Length) return null;
        idx++;
        var sb = new StringBuilder();
        while (idx < resp.Length)
        {
            char c = resp[idx];
            if (c == '\\' && idx + 1 < resp.Length)
            {
                char n = resp[++idx];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                idx++; continue;
            }
            if (c == '"') break;
            sb.Append(c); idx++;
        }
        return sb.ToString();
    }

    static string ReadStr(string json, string key)
    {
        int idx = json.IndexOf($"\"{key}\"");
        if (idx < 0) return null;
        idx += key.Length + 2;
        while (idx < json.Length && json[idx] != '"') idx++;
        if (idx >= json.Length) return null;
        idx++;
        var sb = new StringBuilder();
        while (idx < json.Length)
        {
            char c = json[idx];
            if (c == '\\' && idx + 1 < json.Length)
            {
                char n = json[++idx];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                idx++; continue;
            }
            if (c == '"') break;
            sb.Append(c); idx++;
        }
        return sb.ToString().Trim();
    }

    static string JsonEscape(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}
