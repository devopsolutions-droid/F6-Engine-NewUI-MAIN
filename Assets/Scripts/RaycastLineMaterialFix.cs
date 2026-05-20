using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Fixes the invisible raycast line in URP projects.
///
/// ROOT CAUSE: The LineRenderer on XR Ray Interactors uses Unity's built-in
/// legacy "Default-Line" material (fileID 10306). This shader is NOT supported
/// by the Universal Render Pipeline (URP), making the ray line completely
/// invisible even though the raycasting logic still works.
///
/// FIX: On Awake(), this script finds ALL XRRayInteractors in the scene and
/// replaces their LineRenderer material with a URP-compatible shader
/// ("Sprites/Default") that supports vertex colours and alpha — exactly what
/// XRInteractorLineVisual needs.
///
/// HOW TO USE:
///   1. Create an empty GameObject in the scene (e.g. "Raycast Fix")
///   2. Attach this script to it
///   That's it — it auto-patches every XR ray line in the scene.
/// </summary>
public class RaycastLineMaterialFix : MonoBehaviour
{
    [Header("Line Appearance")]
    [Tooltip("Width of the ray line in world-space metres.")]
    [Range(0.001f, 0.05f)]
    public float lineWidth = 0.008f;

    [Tooltip("If true, also patches controllers that are spawned later.")]
    public bool continuousCheck = true;

    private Material _urpLineMaterial;
    private float _nextScanTime;

    void Awake()
    {
        // Build the replacement material once
        _urpLineMaterial = CreateURPLineMaterial();

        if (_urpLineMaterial == null)
        {
            Debug.LogError("[RaycastLineMaterialFix] Failed to create URP material — raycast lines may stay invisible.");
            enabled = false;
            return;
        }

        // Fix all existing interactors right away
        PatchAllInteractors();
    }

    void Update()
    {
        // Periodically scan for new interactors (e.g. spawned late by XR Origin)
        if (!continuousCheck) return;
        if (Time.time < _nextScanTime) return;
        _nextScanTime = Time.time + 2f; // check every 2 seconds
        PatchAllInteractors();
    }

    void PatchAllInteractors()
    {
        var interactors = FindObjectsByType<XRRayInteractor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var interactor in interactors)
        {
            LineRenderer lr = interactor.GetComponent<LineRenderer>();
            if (lr == null) continue;

            // Skip if already patched
            if (lr.sharedMaterial != null &&
                lr.sharedMaterial.name == "RaycastLine_URP_Fix")
                continue;

            string oldShader = lr.sharedMaterial != null
                ? lr.sharedMaterial.shader.name
                : "NULL";

            lr.material = _urpLineMaterial;
            lr.widthMultiplier = lineWidth;
            lr.enabled = true;

            Debug.Log($"[RaycastLineMaterialFix] Patched '{interactor.gameObject.name}' — " +
                      $"replaced shader '{oldShader}' with '{_urpLineMaterial.shader.name}'");
        }
    }

    Material CreateURPLineMaterial()
    {
        // "Sprites/Default" works on Built-in, URP, and HDRP.
        // It supports vertex colours and transparency out of the box.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            // Fallback: URP particles unlit
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            Debug.LogError("[RaycastLineMaterialFix] No compatible shader found " +
                           "('Sprites/Default' or 'URP/Particles/Unlit').");
            return null;
        }

        var mat = new Material(shader);
        mat.name = "RaycastLine_URP_Fix";

        if (mat.HasProperty("_Color"))
            mat.color = Color.white;

        return mat;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f,
            "[RaycastLineMaterialFix] Active");
    }
#endif
}
