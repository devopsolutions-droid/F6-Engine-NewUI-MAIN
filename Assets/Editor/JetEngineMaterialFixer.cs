// using UnityEngine;
// using UnityEditor;

// public class JetEngineMaterialFixer : EditorWindow
// {
//     [MenuItem("Tools/Fix Jet Engine Materials (Built-in to URP)")]
//     static void FixMaterials()
//     {
//         Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
//         Shader urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");

//         if (urpLit == null)
//         {
//             Debug.LogError("URP Lit shader not found. Make sure URP is installed.");
//             return;
//         }

//         string[] matPaths = new[]
//         {
//             "Assets/All Engines/Jet Engine/Materials/Front_fan.mat",
//             "Assets/All Engines/Jet Engine/Materials/Outer_cover.mat",
//             "Assets/All Engines/Jet Engine/Materials/highpressure_shaft.mat",
//             "Assets/All Engines/Jet Engine/Materials/Inside_cover1.mat",
//             "Assets/All Engines/Jet Engine/Materials/Wires_and_pipes.mat",
//             "Assets/All Engines/Jet Engine/Materials/FuelInjector.mat",
//             "Assets/All Engines/Jet Engine/Materials/Jet_Metal.mat",
//             "Assets/All Engines/Jet Engine/Materials/Jet_Metal_Dark.mat",
//             "Assets/All Engines/Jet Engine/Materials/Shadow.mat",
//             "Assets/All Engines/Jet Engine/Materials/Blades2.mat",
//             "Assets/All Engines/Jet Engine/Materials/Shaft1 1.mat",
//             "Assets/All Engines/Jet Engine/Materials/Blades_texture 1.mat",
//         };

//         int fixed = 0;
//         foreach (string path in matPaths)
//         {
//             Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
//             if (mat == null) { Debug.LogWarning($"Not found: {path}"); continue; }

//             string shaderName = mat.shader.name;
//             if (shaderName == "Standard" || shaderName == "Standard (Specular setup)")
//             {
//                 ConvertStandardToURPLit(mat, urpLit);
//                 fixed++;
//             }
//             else if (shaderName.StartsWith("Particles/") || shaderName == "Legacy Shaders/Particles/Alpha Blended")
//             {
//                 Shader target = urpParticlesUnlit ?? urpLit;
//                 mat.shader = target;
//                 EditorUtility.SetDirty(mat);
//                 fixed++;
//             }
//             else if (shaderName == "Universal Render Pipeline/Lit")
//             {
//                 Debug.Log($"Already URP: {mat.name}");
//             }
//             else
//             {
//                 Debug.LogWarning($"Unknown shader '{shaderName}' on {mat.name} — skipped.");
//             }
//         }

//         AssetDatabase.SaveAssets();
//         Debug.Log($"Done. Fixed {fixed} materials.");
//         EditorUtility.DisplayDialog("Jet Engine Material Fixer", $"Fixed {fixed} materials successfully!", "OK");
//     }

//     static void ConvertStandardToURPLit(Material mat, Shader urpLit)
//     {
//         // Cache values before shader swap
//         Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
//         Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
//         Vector2 mainTexScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
//         Vector2 mainTexOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
//         Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
//         float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
//         Texture metallicGlossMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
//         float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
//         float glossiness = mat.HasProperty("_GlossMapScale") ? mat.GetFloat("_GlossMapScale") :
//                            (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f);
//         Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
//         float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;
//         Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
//         Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;

//         mat.shader = urpLit;

//         mat.SetColor("_BaseColor", baseColor);
//         if (mainTex != null)
//         {
//             mat.SetTexture("_BaseMap", mainTex);
//             mat.SetTextureScale("_BaseMap", mainTexScale);
//             mat.SetTextureOffset("_BaseMap", mainTexOffset);
//         }
//         if (bumpMap != null)
//         {
//             mat.SetTexture("_BumpMap", bumpMap);
//             mat.SetFloat("_BumpScale", bumpScale);
//             mat.EnableKeyword("_NORMALMAP");
//         }
//         if (metallicGlossMap != null)
//         {
//             mat.SetTexture("_MetallicGlossMap", metallicGlossMap);
//             mat.EnableKeyword("_METALLICSPECGLOSSMAP");
//         }
//         mat.SetFloat("_Metallic", metallic);
//         mat.SetFloat("_Smoothness", glossiness);
//         if (occlusionMap != null)
//         {
//             mat.SetTexture("_OcclusionMap", occlusionMap);
//             mat.SetFloat("_OcclusionStrength", occlusionStrength);
//         }
//         if (emissionMap != null || emissionColor != Color.black)
//         {
//             mat.SetColor("_EmissionColor", emissionColor);
//             if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
//             mat.EnableKeyword("_EMISSION");
//             mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
//         }

//         mat.SetFloat("_WorkflowMode", 1); // Metallic workflow
//         mat.SetFloat("_Surface", 0);      // Opaque
//         mat.renderQueue = -1;

//         EditorUtility.SetDirty(mat);
//         Debug.Log($"Converted: {mat.name}");
//     }
// }
