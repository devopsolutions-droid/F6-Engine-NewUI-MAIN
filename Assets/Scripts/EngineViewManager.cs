using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the overall visual state of the entire engine.
/// Attach to any active GameObject in the scene.
/// Link its public methods to your UI buttons via OnClick().
/// </summary>
public class EngineViewManager : MonoBehaviour
{
    private EnginePart[] _allParts;

    [Header("Button References - X-Ray")]
    [Tooltip("Drag your X-Ray View button here.")]
    public GameObject xrayButton;
    [Tooltip("Drag your X-Ray View Reset button here.")]
    public GameObject xrayResetButton;

    [Header("Button References - Exploded View")]
    [Tooltip("Drag your Exploded View button here.")]
    public GameObject explodeButton;
    [Tooltip("Drag your Assemble (reset) button here.")]
    public GameObject assembleButton;

    [Header("Exploded View Settings")]
    [Tooltip("Assign the root engine GameObject. Used to calculate the explosion center.")]
    public Transform engineRoot;
    [Tooltip("Duration of the explode/assemble animation in seconds.")]
    [Range(0.1f, 3f)] public float explodeDuration = 1.2f;
    [Tooltip("Global multiplier for explosion distance (only used when no Dismantled Prefab is set).")]
    [Range(0.1f, 5f)] public float globalExplodeDistance = 1.0f;

    [Header("Dismantled Scene Root (Optional)")]
    [Tooltip("Drag the 'Car Engine Dismantled' root GameObject from the Hierarchy here (keep it inactive). " +
             "When set, Explode animates each part to its matching position in this object. " +
             "Leave empty to use auto-calculated explosion. Set automatically by EngineSceneLoader.")]
    public GameObject dismantledSceneRoot;

    [Header("References")]
    [Tooltip("Drag the EngineInteractor here so it gets its parts list refreshed at the same time.")]
    public EngineInteractor engineInteractor;

    /// <summary>True while X-Ray mode is active. EngineInteractor reads this to block selection.</summary>
    public static bool IsXRayActive { get; private set; } = false;

    /// <summary>True while Exploded View mode is active. EngineInteractor reads this to block selection.</summary>
    public static bool IsExplodedActive { get; private set; } = false;

    /// <summary>
    /// Hides all view-mode buttons. Called at scene start until the loading sequence completes.
    /// </summary>
    public void DisableViewButtons()
    {
        if (xrayButton != null)      xrayButton.SetActive(false);
        if (xrayResetButton != null) xrayResetButton.SetActive(false);
        if (explodeButton != null)   explodeButton.SetActive(false);
        if (assembleButton != null)  assembleButton.SetActive(false);
    }

    /// <summary>
    /// Restores view-mode buttons to their default visible state.
    /// Called after the loading sequence completes.
    /// </summary>
    public void EnableViewButtons()
    {
        if (xrayButton != null)      xrayButton.SetActive(true);
        if (xrayResetButton != null) xrayResetButton.SetActive(false);
        if (explodeButton != null)   explodeButton.SetActive(true);
        if (assembleButton != null)  assembleButton.SetActive(false);
    }

    void Start()
    {
        // Do NOT scan here — EngineSceneLoader.Start() activates the engine
        // and then calls RefreshAfterLoad(), which does the scan.
        // Scanning here would find 0 parts if the engine isn't active yet.

        // Hide all view buttons until the loading sequence completes
        DisableViewButtons();
    }

    /// <summary>
    /// Scans the scene for all EnginePart components on ACTIVE objects only.
    /// Inactive objects haven't had Awake() called so their state is uninitialized.
    /// </summary>
    void RefreshParts()
    {
        _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (_allParts == null || _allParts.Length == 0)
            Debug.LogError("[EngineViewManager] No active EnginePart components found in scene!");
        else
            Debug.Log($"[EngineViewManager] Found {_allParts.Length} EngineParts in scene.");
    }

    void InitExplodeTargets()
    {
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogError("[EngineViewManager] Cannot compute explode targets: no parts found!");
            return;
        }

        // ── Path A: Dismantled scene root is assigned — use its part positions ──
        if (dismantledSceneRoot != null)
        {
            ApplyDismantledPositions();
            return;
        }

        // ── Path B: No dismantled prefab — auto-calculate from engine center ──
        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.explodeDistance = globalExplodeDistance;
        }

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.ComputeExplodeTarget(center);
        }
    }

    /// <summary>
    /// Reads each child's WORLD position from the dismantled scene root,
    /// maps them to the live EngineParts by GameObject name, then converts
    /// each world position into the live part's parent local space.
    ///
    /// World positions are used because the dismantled root and the live engine root
    /// are separate GameObjects that may be at different positions/rotations in the scene.
    /// Local positions are relative to their own parent, so they cannot be transferred
    /// directly between two different hierarchies. World → local conversion handles this correctly.
    ///
    /// Parts that have no match fall back to auto-calculation.
    /// </summary>
    void ApplyDismantledPositions()
    {
        // Temporarily activate the dismantled root so Unity can compute world positions
        bool wasActive = dismantledSceneRoot.activeSelf;
        dismantledSceneRoot.SetActive(true);

        // Build name → WORLD position map from every Transform in the dismantled root
        var worldPosMap = new Dictionary<string, Vector3>();
        foreach (Transform t in dismantledSceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!worldPosMap.ContainsKey(t.gameObject.name))
                worldPosMap[t.gameObject.name] = t.position; // world position
        }

        // Restore original active state
        dismantledSceneRoot.SetActive(wasActive);

        int matched = 0;
        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;

        foreach (var part in _allParts)
        {
            if (part == null) continue;

            if (worldPosMap.TryGetValue(part.gameObject.name, out Vector3 targetWorldPos))
            {
                // Convert world position into the live part's parent local space
                part.SetExplodeWorldTarget(targetWorldPos);
                matched++;
            }
            else
            {
                // Fallback: auto-calculate for any part not found in the dismantled root
                part.explodeDistance = globalExplodeDistance;
                part.ComputeExplodeTarget(center);
                Debug.LogWarning($"[EngineViewManager] Part '{part.gameObject.name}' not found in dismantled root — using auto-explode.");
            }
        }

        Debug.Log($"[EngineViewManager] Dismantled root matched {matched}/{_allParts.Length} parts.");
    }

    /// <summary>
    /// Restores the engine to its default look.
    /// Link to your "Default View" button.
    /// </summary>
    public void ActivateDefaultView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogError("[EngineViewManager] No parts available!");
            return;
        }

        IsXRayActive = false;
        IsExplodedActive = false;
        Debug.Log("[EngineViewManager] ActivateDefaultView CALLED");

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.RestoreOriginal();
        }

        // Swap buttons back
        if (xrayButton != null)      xrayButton.SetActive(true);
        if (xrayResetButton != null) xrayResetButton.SetActive(false);
        if (explodeButton != null)   explodeButton.SetActive(true);
        if (assembleButton != null)  assembleButton.SetActive(false);
    }

    /// <summary>
    /// Turns all engine parts into teal transparent X-Ray mesh.
    /// Link to your "X-Ray View" button.
    /// </summary>
    public void ActivateXRayView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogError("[EngineViewManager] No parts available!");
            return;
        }

        // Clear exploded state if it was active
        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                part.AnimateToAssembled(0f);  // Snap back instantly
            }
        }

        IsXRayActive = true;
        Debug.Log($"[EngineViewManager] ActivateXRayView CALLED — applying to {_allParts.Length} parts");

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.SetXRayView();
        }

        // Swap buttons
        if (xrayButton != null)      xrayButton.SetActive(false);
        if (xrayResetButton != null) xrayResetButton.SetActive(true);
        if (explodeButton != null)   explodeButton.SetActive(true);
        if (assembleButton != null)  assembleButton.SetActive(false);
    }

    // ── Exploded View ─────────────────────────────────────────────────────────

    /// <summary>
    /// Explodes all engine parts outward with smooth animation.
    /// Link to your "Exploded View" button.
    /// </summary>
    public void ActivateExplodedView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogError("[EngineViewManager] No parts available!");
            return;
        }

        // Clear X-Ray state if it was active
        if (IsXRayActive)
        {
            IsXRayActive = false;
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                part.RestoreOriginal();
            }
        }

        IsExplodedActive = true;
        InitExplodeTargets();   // recompute in case engine moved/rotated
        Debug.Log("[EngineViewManager] Exploded View ON");

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.HidePanel();              // hide any hover panel that was open
            part.AnimateToExploded(explodeDuration);
        }

        if (explodeButton != null)  explodeButton.SetActive(false);
        if (assembleButton != null) assembleButton.SetActive(true);
        // Keep X-Ray button visible so user can switch modes
        if (xrayButton != null)     xrayButton.SetActive(true);
        if (xrayResetButton != null) xrayResetButton.SetActive(false);
    }

    /// <summary>
    /// Reassembles all engine parts back to original positions.
    /// Link to your "Assemble" button.
    /// </summary>
    public void ActivateAssembledView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogError("[EngineViewManager] No parts available!");
            return;
        }

        IsExplodedActive = false;
        Debug.Log("[EngineViewManager] Exploded View OFF");

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.AnimateToAssembled(explodeDuration);
        }

        if (explodeButton != null)  explodeButton.SetActive(true);
        if (assembleButton != null) assembleButton.SetActive(false);
    }

    /// <summary>
    /// Re-scans if parts array is missing or empty (safety fallback).
    /// </summary>
    void EnsureParts()
    {
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogWarning("[EngineViewManager] Parts array was null/empty. Re-scanning scene...");
            RefreshParts();
        }
    }

    /// <summary>
    /// Called by EngineSceneLoader after a new engine prefab is spawned.
    /// Re-scans all parts and recomputes explode targets.
    /// </summary>
    public void RefreshAfterLoad()
    {
        // Reset view state flags
        IsXRayActive = false;
        IsExplodedActive = false;

        RefreshParts();

        if (_allParts != null && _allParts.Length > 0)
            InitExplodeTargets();

        // Also refresh the EngineInteractor's parts list — it scans at Start()
        // which may run before the engine root is activated (race condition).
        engineInteractor?.RefreshParts();

        // Keep buttons hidden — EnableViewButtons() is called after the loading sequence
        DisableViewButtons();

        Debug.Log($"[EngineViewManager] Refreshed after load — {_allParts?.Length ?? 0} parts found.");
    }

    /// <summary>
    /// Logs the current world positions of all engine parts to the console.
    /// Call this from the Inspector's context menu or via code.
    /// </summary>
    [ContextMenu("Log All Part Positions")]
    public void LogAllPartPositions()
    {
        EnsureParts();
        Debug.Log("[EngineViewManager] Current positions of all EngineParts:");
        foreach (var part in _allParts)
        {
            Vector3 pos = part.transform.position;
            Debug.Log($"{part.partName}: X={pos.x:F2}, Y={pos.y:F2}, Z={pos.z:F2}");
        }
    }
}
