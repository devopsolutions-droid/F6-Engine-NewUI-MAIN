// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;
// using UnityEngine.Networking;

// /// <summary>
// /// Tools > Groq Part Description Generator
// ///
// /// Usage:
// ///   1. Drop your GLB / prefab into the "3D Model" field
// ///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
// ///   3. Click Generate
// ///
// /// The tool auto-finds the matching EngineData + Parts folder,
// /// extracts real mesh geometry (position, size, shape, vertices, material),
// /// sends everything to Groq llama-3.3-70b, and writes accurate names +
// /// descriptions back into every PartData asset automatically.
// /// </summary>
// public class GroqPartDescriptionGenerator : EditorWindow
// {
//     private const string GroqEndpoint   = "https://api.groq.com/openai/v1/chat/completions";
//     private const string Model          = "llama-3.3-70b-versatile";
//     private const string PrefKeyApi     = "GroqAPIKey";
//     private const string PrefKeyType    = "GroqModelType";

//     private string      _apiKey     = "";
//     private string      _modelType  = "";
//     private GameObject  _droppedModel;
//     private string      _status     = "";
//     private bool        _running    = false;
//     private Vector2     _scroll;

//     [MenuItem("Tools/Groq Part Description Generator")]
//     public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

//     void OnEnable()
//     {
//         _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
//         _modelType = EditorPrefs.GetString(PrefKeyType, "");
//     }

//     void OnGUI()
//     {
//         GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
//         EditorGUILayout.Space(6);

//         // ── API Key ───────────────────────────────────────────────────────────
//         EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
//         string newKey = EditorGUILayout.PasswordField(_apiKey);
//         if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }

//         EditorGUILayout.Space(6);

//         // ── Drop model ────────────────────────────────────────────────────────
//         var newModel = (GameObject)EditorGUILayout.ObjectField(
//             "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
//         if (newModel != _droppedModel)
//         {
//             _droppedModel = newModel;
//             // Auto-fill model type from asset name if field is empty
//             if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
//             {
//                 _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
//                 EditorPrefs.SetString(PrefKeyType, _modelType);
//             }
//         }

//         // ── Model type ────────────────────────────────────────────────────────
//         EditorGUILayout.Space(2);
//         string newType = EditorGUILayout.TextField("What is this model?", _modelType);
//         if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }

//         EditorGUILayout.HelpBox(
//             "Be specific for best accuracy.\n" +
//             "Examples:\n" +
//             "  • V8 Twin Turbo gasoline engine\n" +
//             "  • F6 Boxer engine (Porsche 911)\n" +
//             "  • CFM56 Turbofan jet engine\n" +
//             "  • Ferrari 458 Italia V8 engine\n" +
//             "  • Diesel truck engine",
//             MessageType.Info);

//         EditorGUILayout.Space(8);

//         // ── Generate button ───────────────────────────────────────────────────
//         bool canRun = !_running
//                    && !string.IsNullOrEmpty(_apiKey)
//                    && _droppedModel != null
//                    && !string.IsNullOrEmpty(_modelType);

//         GUI.enabled = canRun;
//         if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
//             RunGeneration();
//         GUI.enabled = true;

//         if (_droppedModel == null)
//             EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
//         else if (string.IsNullOrEmpty(_modelType))
//             EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

//         EditorGUILayout.Space(6);

//         // ── Status ────────────────────────────────────────────────────────────
//         if (!string.IsNullOrEmpty(_status))
//         {
//             _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
//             EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
//             EditorGUILayout.EndScrollView();
//         }

//         EditorGUILayout.Space(2);
//         EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
//     }

//     // ─────────────────────────────────────────────────────────────────────────

//     async void RunGeneration()
//     {
//         _running = true;
//         _status  = "Locating Parts folder for this model...";
//         Repaint();

//         // ── Find the Parts folder that belongs to this prefab ─────────────────
//         string partsFolder = FindPartsFolderForModel(_droppedModel);
//         if (partsFolder == null)
//         {
//             _status  = "ERROR: Could not find a Parts folder for this model.\n\n" +
//                        "Make sure you ran  Tools → Engine Part Setup  on this model first.\n" +
//                        "That tool creates the Parts/ folder and PartData assets automatically.";
//             _running = false; Repaint(); return;
//         }

//         _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
//         Repaint();

//         // ── Load PartData assets ──────────────────────────────────────────────
//         var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
//         if (guids.Length == 0)
//         {
//             _status  = $"No PartData assets found in:\n{partsFolder}\n\nRun Engine Part Setup first.";
//             _running = false; Repaint(); return;
//         }

//         var parts = new List<PartData>();
//         foreach (var g in guids)
//         {
//             var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
//             if (pd != null) parts.Add(pd);
//         }

//         _status = $"Found {parts.Count} parts.\nExtracting mesh geometry...";
//         Repaint();

//         // ── Extract geometry from the prefab ──────────────────────────────────
//         var geoData = ExtractGeometry(_droppedModel, parts);

//         _status = $"Geometry extracted.\nSending {parts.Count} parts to Groq AI...";
//         Repaint();

//         // ── Call Groq ─────────────────────────────────────────────────────────
//         string prompt  = BuildPrompt(geoData, _modelType);
//         var    results = await CallGroq(prompt);

//         if (results == null || results.Count == 0)
//         {
//             _status  = "ERROR: Groq request failed or returned empty response.\n" +
//                        "Check your API key and internet connection.\nSee Console for details.";
//             _running = false; Repaint(); return;
//         }

//         // ── Write results back ────────────────────────────────────────────────
//         int applied = 0;
//         for (int i = 0; i < parts.Count && i < results.Count; i++)
//         {
//             if (!string.IsNullOrEmpty(results[i].name))
//                 parts[i].partName = results[i].name;
//             if (!string.IsNullOrEmpty(results[i].description))
//                 parts[i].description = results[i].description;
//             EditorUtility.SetDirty(parts[i]);
//             applied++;
//         }

//         AssetDatabase.SaveAssets();
//         AssetDatabase.Refresh();

//         _status  = $"✓ Done!  {applied} / {parts.Count} parts updated.\n\n" +
//                    $"All PartData assets saved to:\n{partsFolder}";
//         _running = false;
//         Repaint();
//     }

//     // ── Auto-find Parts folder ────────────────────────────────────────────────

//     /// <summary>
//     /// Searches all EngineData assets in the project for one whose enginePrefab
//     /// matches the dropped model, then returns its sibling Parts/ folder.
//     /// Falls back to searching by prefab name if no exact match found.
//     /// </summary>
//     static string FindPartsFolderForModel(GameObject model)
//     {
//         string modelAssetPath = AssetDatabase.GetAssetPath(model);
//         string modelName      = model.name;
//         string modelNorm      = Normalize(modelName);

//         // Search all EngineData assets
//         string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
//         foreach (var guid in edGuids)
//         {
//             var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
//             if (ed == null) continue;

//             bool match = false;

//             // Exact prefab reference match
//             if (ed.enginePrefab != null)
//                 match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

//             // Name-based fallback — normalize spaces, underscores, hyphens
//             if (!match && ed.engineName != null)
//                 match = Normalize(ed.engineName) == modelNorm;

//             if (match)
//             {
//                 string edDir     = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
//                 string partsPath = edDir + "/Parts";
//                 if (Directory.Exists(partsPath)) return partsPath;
//             }
//         }

//         // Fallback: search all Parts folders whose engine folder name matches the model name
//         string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
//         foreach (var dir in allDirs)
//         {
//             string unityDir    = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder) == modelNorm)
//                 return unityDir;
//         }

//         // Last resort: any Parts folder under an engine folder whose name contains the model name
//         foreach (var dir in allDirs)
//         {
//             string unityDir     = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
//                 return unityDir;
//         }

//         return null;
//     }

//     /// <summary>Lowercases and strips spaces, underscores, and hyphens for fuzzy name matching.</summary>
//     static string Normalize(string s) =>
//         s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

//     // ── Geometry Extraction ───────────────────────────────────────────────────

//     struct PartGeo
//     {
//         public string rawName;
//         public string position;   // e.g. "top-center-front"
//         public string size;       // large / medium / small / tiny
//         public string shape;      // complex / elongated / thin-flat / compact
//         public int    vertices;
//         public string material;
//     }

//     static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
//     {
//         var result   = new List<PartGeo>();
//         var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
//         instance.hideFlags = HideFlags.HideAndDontSave;

//         try
//         {
//             // Overall engine bounds
//             var allR = instance.GetComponentsInChildren<Renderer>(true);
//             var engBounds = new Bounds(instance.transform.position, Vector3.zero);
//             foreach (var r in allR) engBounds.Encapsulate(r.bounds);
//             Vector3 ec = engBounds.center;
//             Vector3 es = engBounds.size;

//             // Name → (MeshFilter, Renderer) lookup
//             var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
//             foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
//                 if (!lookup.ContainsKey(mf.gameObject.name))
//                     lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

//             foreach (var pd in parts)
//             {
//                 var geo = new PartGeo { rawName = pd.partName };

//                 if (lookup.TryGetValue(pd.partName, out var entry)
//                     && entry.mf != null && entry.mf.sharedMesh != null)
//                 {
//                     var mesh = entry.mf.sharedMesh;
//                     var rend = entry.rend;
//                     var b    = rend != null ? rend.bounds : new Bounds(entry.mf.transform.position, Vector3.zero);

//                     // Relative position
//                     Vector3 rel = b.center - ec;
//                     float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
//                     float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
//                     float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
//                     string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
//                     string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
//                     string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
//                     geo.position = $"{py}-{px}-{pz}";

//                     // Relative size
//                     float pv   = b.size.x * b.size.y * b.size.z;
//                     float ev   = es.x * es.y * es.z;
//                     float rat  = ev > 0 ? pv / ev : 0;
//                     geo.size   = rat > 0.15f ? "large" : rat > 0.04f ? "medium" : rat > 0.008f ? "small" : "tiny";

//                     // Shape
//                     float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
//                     float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
//                     float asp  = maxD > 0 ? minD / maxD : 1;
//                     geo.shape  = asp < 0.15f ? "thin-flat"
//                                : asp < 0.4f  ? "elongated"
//                                : mesh.vertexCount > 2000 ? "complex" : "compact";

//                     geo.vertices = mesh.vertexCount;

//                     // Material hint
//                     if (rend != null && rend.sharedMaterial != null)
//                     {
//                         string mn = rend.sharedMaterial.name
//                             .Replace("(Instance)", "").Replace("_", " ").Trim();
//                         geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
//                     }
//                 }
//                 else
//                 {
//                     geo.position = "unknown";
//                     geo.size     = "unknown";
//                     geo.shape    = "unknown";
//                 }

//                 result.Add(geo);
//             }
//         }
//         finally { DestroyImmediate(instance); }

//         return result;
//     }

//     // ── Prompt ────────────────────────────────────────────────────────────────

//     static string BuildPrompt(List<PartGeo> geoData, string modelType)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("You are an expert mechanical engineer and technical writer.");
//         sb.AppendLine($"You are analyzing a 3D model of: {modelType}");
//         sb.AppendLine();
//         sb.AppendLine("Each part below has geometric data extracted from the actual 3D mesh:");
//         sb.AppendLine("  position = location relative to model center (vertical-horizontal-depth)");
//         sb.AppendLine("  size     = relative to whole model (large/medium/small/tiny)");
//         sb.AppendLine("  shape    = mesh shape classification");
//         sb.AppendLine("  vertices = mesh complexity");
//         sb.AppendLine("  material = material name hint from the 3D file");
//         sb.AppendLine();
//         sb.AppendLine("PARTS:");

//         for (int i = 0; i < geoData.Count; i++)
//         {
//             var g = geoData[i];
//             sb.Append($"{i + 1}.");
//             sb.Append($" position={g.position}");
//             sb.Append($" size={g.size}");
//             sb.Append($" shape={g.shape}");
//             sb.Append($" vertices={g.vertices}");
//             if (!string.IsNullOrEmpty(g.material))
//                 sb.Append($" material=\"{g.material}\"");
//             sb.AppendLine();
//         }

//         sb.AppendLine();
//         sb.AppendLine($"Return a JSON array of exactly {geoData.Count} objects.");
//         sb.AppendLine("Each object: {\"name\": \"Part Name\", \"description\": \"2-3 sentence technical description.\"}");
//         sb.AppendLine();
//         sb.AppendLine("Rules:");
//         sb.AppendLine("- Every part name must be UNIQUE — no duplicates");
//         sb.AppendLine("- Names must be real mechanical part names for this specific model type");
//         sb.AppendLine("- Descriptions must be technically accurate and educational");
//         sb.AppendLine("- Use geometry data to reason: large+top+complex = main block/head, tiny+scattered = bolts/sensors, thin-flat = gaskets/shields, bottom = oil pan/sump, rear = flywheel/clutch");
//         sb.AppendLine("- Return ONLY the raw JSON array, no markdown, no explanation");

//         return sb.ToString();
//     }

//     // ── Groq API ──────────────────────────────────────────────────────────────

//     async Task<List<(string name, string description)>> CallGroq(string prompt)
//     {
//         string body = "{\"model\":\"" + Model + "\"," +
//                       "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
//                       "\"temperature\":0.2,\"max_tokens\":6000}";
//         try
//         {
//             using var req = new UnityWebRequest(GroqEndpoint, "POST");
//             req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Content-Type",  "application/json");
//             req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

//             var op = req.SendWebRequest();
//             while (!op.isDone) await Task.Yield();

//             if (req.result != UnityWebRequest.Result.Success)
//             {
//                 Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
//                 return null;
//             }

//             string content = ExtractContent(req.downloadHandler.text);
//             Debug.Log($"[Groq] Response:\n{content}");
//             return string.IsNullOrEmpty(content) ? null : ParseArray(content);
//         }
//         catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
//     }

//     // ── JSON Helpers ──────────────────────────────────────────────────────────

//     static List<(string, string)> ParseArray(string content)
//     {
//         var list  = new List<(string, string)>();
//         int start = content.IndexOf('[');
//         int end   = content.LastIndexOf(']');
//         if (start < 0 || end <= start) return null;

//         string json = content.Substring(start, end - start + 1);
//         int i = 0;
//         while (i < json.Length)
//         {
//             int os = json.IndexOf('{', i); if (os < 0) break;
//             int oe = FindBrace(json, os);  if (oe < 0) break;
//             string obj = json.Substring(os, oe - os + 1);
//             list.Add((ReadStr(obj, "name") ?? "", ReadStr(obj, "description") ?? ""));
//             i = oe + 1;
//         }
//         return list;
//     }

//     static int FindBrace(string s, int from)
//     {
//         int d = 0; bool inS = false;
//         for (int i = from; i < s.Length; i++)
//         {
//             char c = s[i];
//             if (c == '\\' && inS) { i++; continue; }
//             if (c == '"') { inS = !inS; continue; }
//             if (inS) continue;
//             if (c == '{') d++; else if (c == '}') { d--; if (d == 0) return i; }
//         }
//         return -1;
//     }

//     static string ExtractContent(string resp)
//     {
//         int idx = resp.IndexOf("\"content\":");
//         if (idx < 0) return null;
//         idx += 10;
//         while (idx < resp.Length && resp[idx] != '"') idx++;
//         if (idx >= resp.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < resp.Length)
//         {
//             char c = resp[idx];
//             if (c == '\\' && idx + 1 < resp.Length)
//             {
//                 char n = resp[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString();
//     }

//     static string ReadStr(string json, string key)
//     {
//         int idx = json.IndexOf($"\"{key}\"");
//         if (idx < 0) return null;
//         idx += key.Length + 2;
//         while (idx < json.Length && json[idx] != '"') idx++;
//         if (idx >= json.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < json.Length)
//         {
//             char c = json[idx];
//             if (c == '\\' && idx + 1 < json.Length)
//             {
//                 char n = json[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString().Trim();
//     }

//     static string JsonEscape(string s) =>
//         "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
//                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
// }



// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;
// using UnityEngine.Networking;

// /// <summary>
// /// Tools > Groq Part Description Generator
// ///
// /// Usage:
// ///   1. Drop your GLB / prefab into the "3D Model" field
// ///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
// ///   3. Click Generate
// ///
// /// The tool auto-finds the matching EngineData + Parts folder,
// /// extracts real mesh geometry (position, size, shape, vertices, material),
// /// sends everything to Groq llama-3.3-70b, and writes accurate names +
// /// descriptions back into every PartData asset automatically.
// /// </summary>
// public class GroqPartDescriptionGenerator : EditorWindow
// {
//     private const string GroqEndpoint   = "https://api.groq.com/openai/v1/chat/completions";
//     private const string Model          = "llama-3.3-70b-versatile";
//     private const string PrefKeyApi     = "GroqAPIKey";
//     private const string PrefKeyType    = "GroqModelType";

//     private string      _apiKey     = "";
//     private string      _modelType  = "";
//     private GameObject  _droppedModel;
//     private string      _status     = "";
//     private bool        _running    = false;
//     private Vector2     _scroll;

//     [MenuItem("Tools/Groq Part Description Generator")]
//     public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

//     void OnEnable()
//     {
//         _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
//         _modelType = EditorPrefs.GetString(PrefKeyType, "");
//     }

//     void OnGUI()
//     {
//         GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
//         EditorGUILayout.Space(6);

//         // ── API Key ───────────────────────────────────────────────────────────
//         EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
//         string newKey = EditorGUILayout.PasswordField(_apiKey);
//         if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }

//         EditorGUILayout.Space(6);

//         // ── Drop model ────────────────────────────────────────────────────────
//         var newModel = (GameObject)EditorGUILayout.ObjectField(
//             "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
//         if (newModel != _droppedModel)
//         {
//             _droppedModel = newModel;
//             // Auto-fill model type from asset name if field is empty
//             if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
//             {
//                 _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
//                 EditorPrefs.SetString(PrefKeyType, _modelType);
//             }
//         }

//         // ── Model type ────────────────────────────────────────────────────────
//         EditorGUILayout.Space(2);
//         string newType = EditorGUILayout.TextField("What is this model?", _modelType);
//         if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }

//         EditorGUILayout.HelpBox(
//             "Be specific for best accuracy.\n" +
//             "Examples:\n" +
//             "  • V8 Twin Turbo gasoline engine\n" +
//             "  • F6 Boxer engine (Porsche 911)\n" +
//             "  • CFM56 Turbofan jet engine\n" +
//             "  • Ferrari 458 Italia V8 engine\n" +
//             "  • Diesel truck engine",
//             MessageType.Info);

//         EditorGUILayout.Space(8);

//         // ── Generate button ───────────────────────────────────────────────────
//         bool canRun = !_running
//                    && !string.IsNullOrEmpty(_apiKey)
//                    && _droppedModel != null
//                    && !string.IsNullOrEmpty(_modelType);

//         GUI.enabled = canRun;
//         if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
//             RunGeneration();
//         GUI.enabled = true;

//         if (_droppedModel == null)
//             EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
//         else if (string.IsNullOrEmpty(_modelType))
//             EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

//         EditorGUILayout.Space(6);

//         // ── Status ────────────────────────────────────────────────────────────
//         if (!string.IsNullOrEmpty(_status))
//         {
//             _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
//             EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
//             EditorGUILayout.EndScrollView();
//         }

//         EditorGUILayout.Space(2);
//         EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
//     }

//     // ─────────────────────────────────────────────────────────────────────────

//     async void RunGeneration()
//     {
//         _running = true;
//         _status  = "Locating Parts folder for this model...";
//         Repaint();

//         // ── Find the Parts folder that belongs to this prefab ─────────────────
//         string partsFolder = FindPartsFolderForModel(_droppedModel);
//         if (partsFolder == null)
//         {
//             _status  = "ERROR: Could not find a Parts folder for this model.\n\n" +
//                        "Make sure you ran  Tools → Engine Part Setup  on this model first.\n" +
//                        "That tool creates the Parts/ folder and PartData assets automatically.";
//             _running = false; Repaint(); return;
//         }

//         _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
//         Repaint();

//         // ── Load PartData assets ──────────────────────────────────────────────
//         var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
//         if (guids.Length == 0)
//         {
//             _status  = $"No PartData assets found in:\n{partsFolder}\n\nRun Engine Part Setup first.";
//             _running = false; Repaint(); return;
//         }

//         var parts = new List<PartData>();
//         foreach (var g in guids)
//         {
//             var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
//             if (pd != null) parts.Add(pd);
//         }

//         _status = $"Found {parts.Count} parts.\nExtracting mesh geometry...";
//         Repaint();

//         // ── Extract geometry from the prefab ──────────────────────────────────
//         var geoData = ExtractGeometry(_droppedModel, parts);

//         _status = $"Geometry extracted.\nSending {parts.Count} parts to Groq AI...";
//         Repaint();

//         // ── Call Groq ─────────────────────────────────────────────────────────
//         string prompt  = BuildPrompt(geoData, _modelType);
//         var    results = await CallGroq(prompt);

//         if (results == null || results.Count == 0)
//         {
//             _status  = "ERROR: Groq request failed or returned empty response.\n" +
//                        "Check your API key and internet connection.\nSee Console for details.";
//             _running = false; Repaint(); return;
//         }

//         // ── Write results back ────────────────────────────────────────────────
//         int applied = 0;
//         for (int i = 0; i < parts.Count && i < results.Count; i++)
//         {
//             if (!string.IsNullOrEmpty(results[i].name))
//                 parts[i].partName = results[i].name;
//             if (!string.IsNullOrEmpty(results[i].description))
//                 parts[i].description = results[i].description;
//             EditorUtility.SetDirty(parts[i]);
//             applied++;
//         }

//         AssetDatabase.SaveAssets();
//         AssetDatabase.Refresh();

//         _status  = $"✓ Done!  {applied} / {parts.Count} parts updated.\n\n" +
//                    $"All PartData assets saved to:\n{partsFolder}";
//         _running = false;
//         Repaint();
//     }

//     // ── Auto-find Parts folder ────────────────────────────────────────────────

//     /// <summary>
//     /// Searches all EngineData assets in the project for one whose enginePrefab
//     /// matches the dropped model, then returns its sibling Parts/ folder.
//     /// Falls back to searching by prefab name if no exact match found.
//     /// </summary>
//     static string FindPartsFolderForModel(GameObject model)
//     {
//         string modelAssetPath = AssetDatabase.GetAssetPath(model);
//         string modelName      = model.name;
//         string modelNorm      = Normalize(modelName);

//         // Search all EngineData assets
//         string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
//         foreach (var guid in edGuids)
//         {
//             var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
//             if (ed == null) continue;

//             bool match = false;

//             // Exact prefab reference match
//             if (ed.enginePrefab != null)
//                 match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

//             // Name-based fallback — normalize spaces, underscores, hyphens
//             if (!match && ed.engineName != null)
//                 match = Normalize(ed.engineName) == modelNorm;

//             if (match)
//             {
//                 string edDir     = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
//                 string partsPath = edDir + "/Parts";
//                 if (Directory.Exists(partsPath)) return partsPath;
//             }
//         }

//         // Fallback: search all Parts folders whose engine folder name matches the model name
//         string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
//         foreach (var dir in allDirs)
//         {
//             string unityDir    = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder) == modelNorm)
//                 return unityDir;
//         }

//         // Last resort: any Parts folder under an engine folder whose name contains the model name
//         foreach (var dir in allDirs)
//         {
//             string unityDir     = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
//                 return unityDir;
//         }

//         return null;
//     }

//     /// <summary>Lowercases and strips spaces, underscores, and hyphens for fuzzy name matching.</summary>
//     static string Normalize(string s) =>
//         s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

//     // ── Geometry Extraction ───────────────────────────────────────────────────

//     struct PartGeo
//     {
//         public string rawName;
//         public string position;   // e.g. "top-center-front"
//         public string size;       // large / medium / small / tiny
//         public string shape;      // complex / elongated / thin-flat / compact
//         public int    vertices;
//         public string material;
//     }

//     static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
//     {
//         var result   = new List<PartGeo>();
//         var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
//         instance.hideFlags = HideFlags.HideAndDontSave;

//         try
//         {
//             // Overall engine bounds
//             var allR = instance.GetComponentsInChildren<Renderer>(true);
//             var engBounds = new Bounds(instance.transform.position, Vector3.zero);
//             foreach (var r in allR) engBounds.Encapsulate(r.bounds);
//             Vector3 ec = engBounds.center;
//             Vector3 es = engBounds.size;

//             // Name → (MeshFilter, Renderer) lookup
//             var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
//             foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
//                 if (!lookup.ContainsKey(mf.gameObject.name))
//                     lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

//             foreach (var pd in parts)
//             {
//                 var geo = new PartGeo { rawName = pd.partName };

//                 if (lookup.TryGetValue(pd.partName, out var entry)
//                     && entry.mf != null && entry.mf.sharedMesh != null)
//                 {
//                     var mesh = entry.mf.sharedMesh;
//                     var rend = entry.rend;
//                     var b    = rend != null ? rend.bounds : new Bounds(entry.mf.transform.position, Vector3.zero);

//                     // Relative position
//                     Vector3 rel = b.center - ec;
//                     float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
//                     float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
//                     float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
//                     string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
//                     string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
//                     string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
//                     geo.position = $"{py}-{px}-{pz}";

//                     // Relative size
//                     float pv   = b.size.x * b.size.y * b.size.z;
//                     float ev   = es.x * es.y * es.z;
//                     float rat  = ev > 0 ? pv / ev : 0;
//                     geo.size   = rat > 0.15f ? "large" : rat > 0.04f ? "medium" : rat > 0.008f ? "small" : "tiny";

//                     // Shape
//                     float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
//                     float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
//                     float asp  = maxD > 0 ? minD / maxD : 1;
//                     geo.shape  = asp < 0.15f ? "thin-flat"
//                                : asp < 0.4f  ? "elongated"
//                                : mesh.vertexCount > 2000 ? "complex" : "compact";

//                     geo.vertices = mesh.vertexCount;

//                     // Material hint
//                     if (rend != null && rend.sharedMaterial != null)
//                     {
//                         string mn = rend.sharedMaterial.name
//                             .Replace("(Instance)", "").Replace("_", " ").Trim();
//                         geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
//                     }
//                 }
//                 else
//                 {
//                     geo.position = "unknown";
//                     geo.size     = "unknown";
//                     geo.shape    = "unknown";
//                 }

//                 result.Add(geo);
//             }
//         }
//         finally { DestroyImmediate(instance); }

//         return result;
//     }

//     // ── Prompt ────────────────────────────────────────────────────────────────

//     static string BuildPrompt(List<PartGeo> geoData, string modelType)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("You are an expert mechanical engineer and technical writer.");
//         sb.AppendLine($"You are analyzing a 3D model of: {modelType}");
//         sb.AppendLine();
//         sb.AppendLine("Each part below has geometric data extracted from the actual 3D mesh:");
//         sb.AppendLine("  position = location relative to model center (vertical-horizontal-depth)");
//         sb.AppendLine("  size     = relative to whole model (large/medium/small/tiny)");
//         sb.AppendLine("  shape    = mesh shape classification");
//         sb.AppendLine("  vertices = mesh complexity");
//         sb.AppendLine("  material = material name hint from the 3D file");
//         sb.AppendLine();
//         sb.AppendLine("PARTS:");

//         for (int i = 0; i < geoData.Count; i++)
//         {
//             var g = geoData[i];
//             sb.Append($"{i + 1}.");
//             sb.Append($" position={g.position}");
//             sb.Append($" size={g.size}");
//             sb.Append($" shape={g.shape}");
//             sb.Append($" vertices={g.vertices}");
//             if (!string.IsNullOrEmpty(g.material))
//                 sb.Append($" material=\"{g.material}\"");
//             sb.AppendLine();
//         }

//         sb.AppendLine();
//         sb.AppendLine($"Return a JSON array of exactly {geoData.Count} objects.");
//         sb.AppendLine("Each object: {\"name\": \"Part Name\", \"description\": \"2-3 sentence technical description.\"}");
//         sb.AppendLine();
//         sb.AppendLine("Rules:");
//         sb.AppendLine("- Every part name must be UNIQUE — no duplicates");
//         sb.AppendLine("- Names must be real mechanical part names for this specific model type");
//         sb.AppendLine("- Descriptions must be technically accurate and educational");
//         sb.AppendLine("- Use geometry data to reason: large+top+complex = main block/head, tiny+scattered = bolts/sensors, thin-flat = gaskets/shields, bottom = oil pan/sump, rear = flywheel/clutch");
//         sb.AppendLine("- Return ONLY the raw JSON array, no markdown, no explanation");

//         return sb.ToString();
//     }

//     // ── Groq API ──────────────────────────────────────────────────────────────

//     async Task<List<(string name, string description)>> CallGroq(string prompt)
//     {
//         string body = "{\"model\":\"" + Model + "\"," +
//                       "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
//                       "\"temperature\":0.2,\"max_tokens\":6000}";
//         try
//         {
//             using var req = new UnityWebRequest(GroqEndpoint, "POST");
//             req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Content-Type",  "application/json");
//             req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

//             var op = req.SendWebRequest();
//             while (!op.isDone) await Task.Yield();

//             if (req.result != UnityWebRequest.Result.Success)
//             {
//                 Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
//                 return null;
//             }

//             string content = ExtractContent(req.downloadHandler.text);
//             Debug.Log($"[Groq] Response:\n{content}");
//             return string.IsNullOrEmpty(content) ? null : ParseArray(content);
//         }
//         catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
//     }

//     // ── JSON Helpers ──────────────────────────────────────────────────────────

//     static List<(string, string)> ParseArray(string content)
//     {
//         var list  = new List<(string, string)>();
//         int start = content.IndexOf('[');
//         int end   = content.LastIndexOf(']');
//         if (start < 0 || end <= start) return null;

//         string json = content.Substring(start, end - start + 1);
//         int i = 0;
//         while (i < json.Length)
//         {
//             int os = json.IndexOf('{', i); if (os < 0) break;
//             int oe = FindBrace(json, os);  if (oe < 0) break;
//             string obj = json.Substring(os, oe - os + 1);
//             list.Add((ReadStr(obj, "name") ?? "", ReadStr(obj, "description") ?? ""));
//             i = oe + 1;
//         }
//         return list;
//     }

//     static int FindBrace(string s, int from)
//     {
//         int d = 0; bool inS = false;
//         for (int i = from; i < s.Length; i++)
//         {
//             char c = s[i];
//             if (c == '\\' && inS) { i++; continue; }
//             if (c == '"') { inS = !inS; continue; }
//             if (inS) continue;
//             if (c == '{') d++; else if (c == '}') { d--; if (d == 0) return i; }
//         }
//         return -1;
//     }

//     static string ExtractContent(string resp)
//     {
//         int idx = resp.IndexOf("\"content\":");
//         if (idx < 0) return null;
//         idx += 10;
//         while (idx < resp.Length && resp[idx] != '"') idx++;
//         if (idx >= resp.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < resp.Length)
//         {
//             char c = resp[idx];
//             if (c == '\\' && idx + 1 < resp.Length)
//             {
//                 char n = resp[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString();
//     }

//     static string ReadStr(string json, string key)
//     {
//         int idx = json.IndexOf($"\"{key}\"");
//         if (idx < 0) return null;
//         idx += key.Length + 2;
//         while (idx < json.Length && json[idx] != '"') idx++;
//         if (idx >= json.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < json.Length)
//         {
//             char c = json[idx];
//             if (c == '\\' && idx + 1 < json.Length)
//             {
//                 char n = json[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString().Trim();
//     }

//     static string JsonEscape(string s) =>
//         "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
//                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
// }





// Updated Claude code
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
/// Improvements over v1:
///   - Two-pass generation: Pass 1 names parts, Pass 2 writes descriptions
///   - Raw mesh name sent to AI as strong hint (biggest accuracy win)
///   - Mesh names cleaned/normalized before sending
///   - Neighbor proximity context (nearby tiny parts = bolts/studs)
///   - Duplicate name detection and auto-suffix resolution
///   - Markdown fence stripping before JSON parse
///   - Result count validation before any asset write (no partial corruption)
///   - 30-second request timeout
///   - Cancellation support (Cancel button during generation)
///   - Chunked batching for models with 30+ parts
///   - Pre-write backup to ProjectSettings/GroqBackup/
///   - Per-part progress reporting in status log
/// </summary>
public class GroqPartDescriptionGenerator : EditorWindow
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const string GroqEndpoint  = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model         = "llama-3.3-70b-versatile";
    private const string PrefKeyApi    = "GroqAPIKey";
    private const string PrefKeyType   = "GroqModelType";
    private const int    BatchSize     = 8;    // max parts per API call — keeps each request under ~3000 tokens
    private const int    TimeoutSecs   = 30;
    private const int    BatchDelayMs  = 8000; // wait 8s between batches to respect 12k TPM limit
    private const float  MinResultRatio = 0.8f;

    // ── State ─────────────────────────────────────────────────────────────────
    private string     _apiKey      = "";
    private string     _modelType   = "";
    private GameObject _droppedModel;
    private string     _status      = "";
    private bool       _running     = false;
    private Vector2    _scroll;

    private CancellationTokenSource _cts;

    // ── Menu ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Groq Part Description Generator")]
    public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

    void OnEnable()
    {
        _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
        _modelType = EditorPrefs.GetString(PrefKeyType, "");
    }

    void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        // API Key
        EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
        string newKey = EditorGUILayout.PasswordField(_apiKey);
        if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }
        EditorGUILayout.Space(6);

        // Drop model
        var newModel = (GameObject)EditorGUILayout.ObjectField(
            "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
        if (newModel != _droppedModel)
        {
            _droppedModel = newModel;
            if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
            {
                _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
                EditorPrefs.SetString(PrefKeyType, _modelType);
            }
        }

        // Model type
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

        // Buttons row
        bool canRun = !_running
                   && !string.IsNullOrEmpty(_apiKey)
                   && _droppedModel != null
                   && !string.IsNullOrEmpty(_modelType);

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = canRun;
        if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
            _ = RunGeneration();
        GUI.enabled = true;

        if (_running)
        {
            if (GUILayout.Button("Cancel", GUILayout.Height(44), GUILayout.Width(80)))
            {
                _cts?.Cancel();
                _status  = "Cancelling...";
                _running = false;
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (_droppedModel == null)
            EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_modelType))
            EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

        EditorGUILayout.Space(6);

        // Status log
        if (!string.IsNullOrEmpty(_status))
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(180));
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
    }

    // ── Main pipeline ─────────────────────────────────────────────────────────
    async Task RunGeneration()
    {
        _cts     = new CancellationTokenSource();
        _running = true;
        _status  = "Locating Parts folder for this model...";
        Repaint();

        try
        {
            // Step 1 — Find Parts folder
            string partsFolder = FindPartsFolderForModel(_droppedModel);
            if (partsFolder == null)
            {
                _status = "ERROR: Could not find a Parts folder for this model.\n\n" +
                          "Run  Tools → Engine Part Setup  on this model first.";
                return;
            }

            Log($"Parts folder:\n{partsFolder}\n\nLoading PartData assets...");

            // Step 2 — Load PartData assets
            var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
            if (guids.Length == 0)
            {
                _status = $"No PartData assets found in:\n{partsFolder}";
                return;
            }

            var parts = new List<PartData>();
            foreach (var g in guids)
            {
                var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
                if (pd != null) parts.Add(pd);
            }

            Log($"Found {parts.Count} parts.\nExtracting mesh geometry...");

            // Step 3 — Extract geometry (includes cleaned mesh names + neighbor context)
            var geoData = ExtractGeometry(_droppedModel, parts);
            AnnotateNeighbors(geoData);

            Log($"Geometry extracted.\n\nStarting Pass 1 — Naming ({parts.Count} parts in batches of {BatchSize})...");

            // Step 4 — Pass 1: name generation in batches
            var allNames = new List<string>();
            int totalBatches = Mathf.CeilToInt((float)parts.Count / BatchSize);

            for (int b = 0; b < parts.Count; b += BatchSize)
            {
                if (_cts.Token.IsCancellationRequested) { _status = "Cancelled."; return; }

                int batchNum  = b / BatchSize + 1;
                int batchEnd  = Mathf.Min(b + BatchSize, parts.Count);
                var batchGeo  = geoData.GetRange(b, batchEnd - b);

                Log($"Pass 1 — Batch {batchNum}/{totalBatches} " +
                    $"(parts {b + 1}–{batchEnd} of {parts.Count})...");

                string namePrompt = BuildNameOnlyPrompt(batchGeo, _modelType, b);
                var    names      = await CallGroq(namePrompt, _cts.Token);

                if (names == null)
                {
                    _status = $"ERROR: Pass 1 batch {batchNum} failed. Check API key / connection.";
                    return;
                }

                allNames.AddRange(ExtractNames(names));

                // Respect TPM rate limit — wait between batches
                if (b + BatchSize < parts.Count)
                {
                    Log($"Pass 1 — Batch {batchNum}/{totalBatches} done. Waiting {BatchDelayMs / 1000}s before next batch...");
                    await Task.Delay(BatchDelayMs, _cts.Token);
                }
            }

            // Deduplicate names across all batches
            allNames = DeduplicateNames(allNames);
            Log($"Pass 1 complete. {allNames.Count} unique names generated.\n\nStarting Pass 2 — Descriptions...");

            // Step 5 — Pass 2: description generation using confirmed names
            var allDescriptions = new List<string>();

            for (int b = 0; b < parts.Count; b += BatchSize)
            {
                if (_cts.Token.IsCancellationRequested) { _status = "Cancelled."; return; }

                int batchNum = b / BatchSize + 1;
                int batchEnd = Mathf.Min(b + BatchSize, parts.Count);
                var batchGeo = geoData.GetRange(b, batchEnd - b);

                int nameEnd = Mathf.Min(b + BatchSize, allNames.Count);
                var batchNames = b < allNames.Count
                    ? allNames.GetRange(b, nameEnd - b)
                    : new List<string>();

                Log($"Pass 2 — Batch {batchNum}/{totalBatches} " +
                    $"(parts {b + 1}–{batchEnd} of {parts.Count})...");

                string descPrompt = BuildDescriptionPrompt(batchGeo, batchNames, _modelType);
                var    results    = await CallGroq(descPrompt, _cts.Token);

                if (results == null)
                {
                    _status = $"ERROR: Pass 2 batch {batchNum} failed.";
                    return;
                }

                allDescriptions.AddRange(ExtractDescriptions(results));

                // Respect TPM rate limit — wait between batches
                if (b + BatchSize < parts.Count)
                {
                    Log($"Pass 2 — Batch {batchNum}/{totalBatches} done. Waiting {BatchDelayMs / 1000}s before next batch...");
                    await Task.Delay(BatchDelayMs, _cts.Token);
                }
            }

            // Step 6 — Validate before writing
            int nameCount = allNames.Count;
            int descCount = allDescriptions.Count;

            if (nameCount < parts.Count * MinResultRatio || descCount < parts.Count * MinResultRatio)
            {
                _status = $"ERROR: AI returned too few results.\n" +
                          $"  Names: {nameCount}/{parts.Count}\n" +
                          $"  Descriptions: {descCount}/{parts.Count}\n\n" +
                          $"Aborting — no assets were modified.";
                return;
            }

            // Step 7 — Backup existing data
            SaveBackup(parts, partsFolder);

            // Step 8 — Write results
            Log($"Validation passed. Writing results...");
            int applied = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                bool wroteAnything = false;

                if (i < allNames.Count && !string.IsNullOrEmpty(allNames[i]))
                {
                    parts[i].partName    = allNames[i];
                    wroteAnything        = true;
                }
                if (i < allDescriptions.Count && !string.IsNullOrEmpty(allDescriptions[i]))
                {
                    parts[i].description = allDescriptions[i];
                    wroteAnything        = true;
                }

                if (wroteAnything)
                {
                    EditorUtility.SetDirty(parts[i]);
                    applied++;
                }

                if (i % 5 == 0)
                {
                    Log($"Writing... {i + 1}/{parts.Count}");
                    await Task.Yield();
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _status = $"✓ Done!  {applied}/{parts.Count} parts updated.\n\n" +
                      $"Saved to:\n{partsFolder}\n\n" +
                      $"Backup at:\nProjectSettings/GroqBackup/";
        }
        catch (System.OperationCanceledException)
        {
            _status = "Generation cancelled by user.";
        }
        catch (System.Exception e)
        {
            _status = $"Unexpected error:\n{e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            _running = false;
            _cts?.Dispose();
            _cts = null;
            Repaint();
        }
    }

    void Log(string msg) { _status = msg; Repaint(); }

    // ── Parts folder search ───────────────────────────────────────────────────
    static string FindPartsFolderForModel(GameObject model)
    {
        string modelAssetPath = AssetDatabase.GetAssetPath(model);
        string modelNorm      = Normalize(model.name);

        string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
        foreach (var guid in edGuids)
        {
            var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ed == null) continue;

            bool match = false;
            if (ed.enginePrefab != null)
                match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;
            if (!match && ed.engineName != null)
                match = Normalize(ed.engineName) == modelNorm;

            if (match)
            {
                string edDir     = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace("\\", "/");
                string partsPath = edDir + "/Parts";
                if (Directory.Exists(partsPath)) return partsPath;
            }
        }

        string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            string ud = dir.Replace("\\", "/");
            string ef = Path.GetFileName(Path.GetDirectoryName(ud));
            if (Normalize(ef) == modelNorm) return ud;
        }
        foreach (var dir in allDirs)
        {
            string ud = dir.Replace("\\", "/");
            string ef = Path.GetFileName(Path.GetDirectoryName(ud));
            if (Normalize(ef).Contains(modelNorm) || modelNorm.Contains(Normalize(ef))) return ud;
        }

        return null;
    }

    static string Normalize(string s) =>
        s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

    // ── Geometry extraction ───────────────────────────────────────────────────
    struct PartGeo
    {
        public string rawName;       // original mesh node name
        public string cleanedName;   // normalized for AI hint
        public string position;
        public string size;
        public string shape;
        public int    vertices;
        public string material;
        public int    nearbyTinyCount; // how many tiny parts share the same position zone
    }

    static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
    {
        var result   = new List<PartGeo>();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var allR      = instance.GetComponentsInChildren<Renderer>(true);
            var engBounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (var r in allR) engBounds.Encapsulate(r.bounds);

            Vector3 ec = engBounds.center;
            Vector3 es = engBounds.size;

            var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
                if (!lookup.ContainsKey(mf.gameObject.name))
                    lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

            foreach (var pd in parts)
            {
                var geo = new PartGeo
                {
                    rawName     = pd.partName,
                    cleanedName = CleanMeshName(pd.partName)
                };

                if (lookup.TryGetValue(pd.partName, out var entry)
                    && entry.mf != null && entry.mf.sharedMesh != null)
                {
                    var mesh = entry.mf.sharedMesh;
                    var rend = entry.rend;
                    var b    = rend != null ? rend.bounds
                                           : new Bounds(entry.mf.transform.position, Vector3.zero);

                    // Position
                    Vector3 rel = b.center - ec;
                    float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
                    float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
                    float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
                    string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
                    string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
                    string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
                    geo.position = $"{py}-{px}-{pz}";

                    // Size
                    float pv  = b.size.x * b.size.y * b.size.z;
                    float ev  = es.x * es.y * es.z;
                    float rat = ev > 0 ? pv / ev : 0;
                    geo.size  = rat > 0.15f ? "large"
                              : rat > 0.04f ? "medium"
                              : rat > 0.008f ? "small"
                              : "tiny";

                    // Shape
                    float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
                    float asp  = maxD > 0 ? minD / maxD : 1;
                    geo.shape  = asp < 0.15f ? "thin-flat"
                               : asp < 0.4f  ? "elongated"
                               : mesh.vertexCount > 2000 ? "complex"
                               : "compact";

                    geo.vertices = mesh.vertexCount;

                    // Material
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

    /// <summary>
    /// Cleans up raw Unity/GLB mesh node names into readable hints.
    /// e.g. "SM_exhaust_manifold_L_LOD0" → "exhaust manifold L"
    /// </summary>
    static string CleanMeshName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        // Strip common prefixes/suffixes from 3D tools
        string s = Regex.Replace(raw,
            @"(?i)\b(SM_|UCX_|LOD\d|_LOD\d|Mesh_?|mesh_?|Object_?|Cube\.?\d*|Plane\.?\d*|Cylinder\.?\d*)\b",
            " ");

        // Strip trailing numbers like _001, .001
        s = Regex.Replace(s, @"[._]\d{2,}$", "");

        // Replace separators with spaces
        s = s.Replace("_", " ").Replace("-", " ").Replace(".", " ");

        // Collapse multiple spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    /// <summary>
    /// Counts how many tiny parts share the same positional zone as each part.
    /// Large parts with many tiny neighbours → mounting bolts/studs are nearby.
    /// </summary>
    static void AnnotateNeighbors(List<PartGeo> geoData)
    {
        for (int i = 0; i < geoData.Count; i++)
        {
            int count = 0;
            for (int j = 0; j < geoData.Count; j++)
            {
                if (i == j) continue;
                if (geoData[j].size == "tiny" && geoData[j].position == geoData[i].position)
                    count++;
            }
            var g = geoData[i];
            g.nearbyTinyCount = count;
            geoData[i] = g;
        }
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    /// <summary>
    /// Pass 1: asks the AI to identify part names ONLY.
    /// Includes raw mesh hint as the strongest signal.
    /// </summary>
    static string BuildNameOnlyPrompt(List<PartGeo> batch, string modelType, int globalOffset)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert mechanical engineer specializing in automotive and aerospace powertrains.");
        sb.AppendLine($"You are analyzing 3D mesh data from: {modelType}");
        sb.AppendLine();
        sb.AppendLine("Your task: identify the correct mechanical part name for each mesh.");
        sb.AppendLine();
        sb.AppendLine("Each entry contains:");
        sb.AppendLine("  mesh_hint     = cleaned-up mesh node name from the 3D file (STRONGEST signal — use it unless geometry contradicts it)");
        sb.AppendLine("  position      = location relative to engine center (vertical-horizontal-depth)");
        sb.AppendLine("  size          = relative to whole model (large/medium/small/tiny)");
        sb.AppendLine("  shape         = mesh bounding box classification");
        sb.AppendLine("  vertices      = mesh polygon complexity");
        sb.AppendLine("  material      = material name hint from the 3D file");
        sb.AppendLine("  nearby_tiny   = number of tiny parts in the same position zone (high count = mounting bolts nearby)");
        sb.AppendLine();
        sb.AppendLine("NAMING RULES (follow in this priority order):");
        sb.AppendLine("  1. mesh_hint is your primary signal. Clean it up and use it as the part name unless geometry clearly contradicts it.");
        sb.AppendLine("  2. If mesh_hint is generic (Mesh, Object, Cube, lambert, SM only) — ignore it and reason from geometry.");
        sb.AppendLine("  3. Use Left / Right / Upper / Lower suffixes for paired parts.");
        sb.AppendLine("  4. Every name must be UNIQUE across all parts — no two parts can share the same name.");
        sb.AppendLine("  5. Names must be real mechanical part names for this specific engine type.");
        sb.AppendLine("  6. Geometry reasoning fallbacks:");
        sb.AppendLine("       large + top + complex              → Engine Block or Cylinder Head");
        sb.AppendLine("       medium + top + complex             → Intake Manifold or Valve Cover");
        sb.AppendLine("       tiny + any position                → Bolt, Stud, Sensor, or Clip");
        sb.AppendLine("       thin-flat + any position           → Gasket, Heat Shield, or Cover Plate");
        sb.AppendLine("       bottom + medium + compact          → Oil Pan / Sump");
        sb.AppendLine("       rear + large + compact             → Flywheel or Flexplate");
        sb.AppendLine("       elongated + left or right          → Exhaust Manifold or Intake Runner");
        sb.AppendLine("       nearby_tiny >= 6 on large part     → that large part likely has mounting bolts");
        sb.AppendLine();
        sb.AppendLine("PARTS:");

        for (int i = 0; i < batch.Count; i++)
        {
            var g = batch[i];
            sb.Append($"{globalOffset + i + 1}.");
            if (!string.IsNullOrEmpty(g.cleanedName) && g.cleanedName.Length > 2)
                sb.Append($" mesh_hint=\"{g.cleanedName}\"");
            else
                sb.Append($" mesh_hint=GENERIC");
            sb.Append($" position={g.position}");
            sb.Append($" size={g.size}");
            sb.Append($" shape={g.shape}");
            sb.Append($" vertices={g.vertices}");
            if (!string.IsNullOrEmpty(g.material))
                sb.Append($" material=\"{g.material}\"");
            if (g.nearbyTinyCount > 0)
                sb.Append($" nearby_tiny={g.nearbyTinyCount}");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Return a JSON array of exactly {batch.Count} objects.");
        sb.AppendLine("Format: [{\"name\": \"Part Name\"}, {\"name\": \"Part Name\"}, ...]");
        sb.AppendLine("Return ONLY the raw JSON array. No markdown, no explanation, no extra text.");

        return sb.ToString();
    }

    /// <summary>
    /// Pass 2: given confirmed part names from Pass 1, generates accurate descriptions.
    /// </summary>
    static string BuildDescriptionPrompt(List<PartGeo> batch, List<string> confirmedNames, string modelType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert mechanical engineer and technical writer.");
        sb.AppendLine($"You are writing educational descriptions for parts of: {modelType}");
        sb.AppendLine();
        sb.AppendLine("The part names below have already been confirmed. Your job is to write a");
        sb.AppendLine("technically accurate 2-3 sentence description for each one.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("  - Use the confirmed part name as ground truth for what the part IS.");
        sb.AppendLine("  - Use geometry data to add detail: position, material, size context.");
        sb.AppendLine("  - Descriptions must explain: what the part does, why it matters, and one interesting detail.");
        sb.AppendLine("  - Be technically accurate for this specific engine type.");
        sb.AppendLine("  - Return ONLY the raw JSON array. No markdown, no explanation.");
        sb.AppendLine();
        sb.AppendLine("PARTS:");

        for (int i = 0; i < batch.Count; i++)
        {
            var    g    = batch[i];
            string name = i < confirmedNames.Count ? confirmedNames[i] : g.cleanedName;
            sb.Append($"{i + 1}. confirmed_name=\"{name}\"");
            sb.Append($" position={g.position}");
            sb.Append($" size={g.size}");
            sb.Append($" shape={g.shape}");
            if (!string.IsNullOrEmpty(g.material))
                sb.Append($" material=\"{g.material}\"");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Return a JSON array of exactly {batch.Count} objects.");
        sb.AppendLine("Format: [{\"description\": \"2-3 sentences.\"}, ...]");
        sb.AppendLine("Return ONLY the raw JSON array. No markdown, no explanation, no extra text.");

        return sb.ToString();
    }

    // ── Groq API call ─────────────────────────────────────────────────────────
    async Task<List<Dictionary<string, string>>> CallGroq(string prompt, CancellationToken ct)
    {
        string body = "{\"model\":\"" + Model + "\"," +
                      "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
                      "\"temperature\":0.15,\"max_tokens\":1500}";
        try
        {
            using var req = new UnityWebRequest(GroqEndpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            req.timeout = TimeoutSecs;

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    return null;
                }
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            string raw     = req.downloadHandler.text;
            string content = ExtractContent(raw);
            if (string.IsNullOrEmpty(content)) { Debug.LogError("[Groq] Empty content in response."); return null; }

            content = StripMarkdownFences(content);
            Debug.Log($"[Groq] Response:\n{content}");
            return ParseObjectArray(content);
        }
        catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
    }

    // ── Result extraction ─────────────────────────────────────────────────────
    static List<string> ExtractNames(List<Dictionary<string, string>> parsed)
    {
        var list = new List<string>();
        if (parsed == null) return list;
        foreach (var d in parsed)
            list.Add(d.TryGetValue("name", out var v) ? v.Trim() : "");
        return list;
    }

    static List<string> ExtractDescriptions(List<Dictionary<string, string>> parsed)
    {
        var list = new List<string>();
        if (parsed == null) return list;
        foreach (var d in parsed)
            list.Add(d.TryGetValue("description", out var v) ? v.Trim() : "");
        return list;
    }

    /// <summary>
    /// Ensures no two parts share the same name.
    /// Appends (2), (3), etc. to duplicates rather than leaving them identical.
    /// </summary>
    static List<string> DeduplicateNames(List<string> names)
    {
        var seen  = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(names.Count);

        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) { result.Add(n); continue; }

            if (!seen.ContainsKey(n))
            {
                seen[n] = 1;
                result.Add(n);
            }
            else
            {
                seen[n]++;
                result.Add($"{n} ({seen[n]})");
            }
        }
        return result;
    }

    // ── Backup ────────────────────────────────────────────────────────────────
    static void SaveBackup(List<PartData> parts, string partsFolder)
    {
        try
        {
            string backupDir = "ProjectSettings/GroqBackup";
            Directory.CreateDirectory(backupDir);

            string folderLabel = partsFolder.Replace("/", "_").Replace(":", "");
            string timestamp   = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path        = $"{backupDir}/{folderLabel}_{timestamp}.json";

            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < parts.Count; i++)
            {
                sb.Append($"  {{\"partName\":{JsonQuote(parts[i].partName)}," +
                           $"\"description\":{JsonQuote(parts[i].description)}}}");
                if (i < parts.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Groq] Backup saved to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Groq] Backup failed (non-fatal): {e.Message}");
        }
    }

    static string JsonQuote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ── JSON helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Strips markdown code fences that the model sometimes wraps around JSON.
    /// e.g. ```json [...] ``` → [...]
    /// </summary>
    static string StripMarkdownFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```")) return s;
        int firstNewline = s.IndexOf('\n');
        int lastFence    = s.LastIndexOf("```");
        if (firstNewline > 0 && lastFence > firstNewline)
            s = s.Substring(firstNewline, lastFence - firstNewline).Trim();
        return s;
    }

    /// <summary>
    /// Parses a JSON array of objects into a list of string dictionaries.
    /// Handles arbitrary key/value string pairs per object.
    /// </summary>
    static List<Dictionary<string, string>> ParseObjectArray(string content)
    {
        var list  = new List<Dictionary<string, string>>();
        int start = content.IndexOf('[');
        int end   = content.LastIndexOf(']');
        if (start < 0 || end <= start) return null;

        string json = content.Substring(start, end - start + 1);
        int i = 0;
        while (i < json.Length)
        {
            int os = json.IndexOf('{', i); if (os < 0) break;
            int oe = FindClosingBrace(json, os); if (oe < 0) break;

            string obj  = json.Substring(os, oe - os + 1);
            var    dict = ParseObject(obj);
            if (dict != null) list.Add(dict);

            i = oe + 1;
        }
        return list;
    }

    /// <summary>Parses all string key-value pairs from a single JSON object string.</summary>
    static Dictionary<string, string> ParseObject(string obj)
    {
        var dict = new Dictionary<string, string>();
        int i = 1; // skip opening {

        while (i < obj.Length)
        {
            // Find next key
            int ks = obj.IndexOf('"', i); if (ks < 0) break;
            int ke = FindClosingQuote(obj, ks + 1); if (ke < 0) break;
            string key = obj.Substring(ks + 1, ke - ks - 1);
            i = ke + 1;

            // Skip colon
            int colon = obj.IndexOf(':', i); if (colon < 0) break;
            i = colon + 1;

            // Skip whitespace
            while (i < obj.Length && (obj[i] == ' ' || obj[i] == '\n' || obj[i] == '\r' || obj[i] == '\t')) i++;
            if (i >= obj.Length) break;

            // Read value (only string values for our use case)
            if (obj[i] == '"')
            {
                int ve = FindClosingQuote(obj, i + 1); if (ve < 0) break;
                string val = UnescapeJson(obj.Substring(i + 1, ve - i - 1));
                dict[key] = val;
                i = ve + 1;
            }
            else
            {
                // Non-string value — skip to next comma or closing brace
                while (i < obj.Length && obj[i] != ',' && obj[i] != '}') i++;
            }

            // Skip comma
            while (i < obj.Length && (obj[i] == ',' || obj[i] == ' ')) i++;
        }

        return dict.Count > 0 ? dict : null;
    }

    static int FindClosingBrace(string s, int from)
    {
        int  depth = 0;
        bool inStr = false;
        for (int i = from; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && inStr) { i++; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    static int FindClosingQuote(string s, int from)
    {
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '"') return i;
        }
        return -1;
    }

    static string UnescapeJson(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString().Trim();
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
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                idx++; continue;
            }
            if (c == '"') break;
            sb.Append(c); idx++;
        }
        return sb.ToString();
    }

    static string JsonEscape(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}