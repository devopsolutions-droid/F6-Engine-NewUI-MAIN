using UnityEngine;

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
    [Tooltip("Global multiplier for explosion distance.")]
    [Range(0.1f, 5f)] public float globalExplodeDistance = 1.0f;

    /// <summary>True while X-Ray mode is active. EngineInteractor reads this to block selection.</summary>
    public static bool IsXRayActive { get; private set; } = false;

    /// <summary>True while Exploded View mode is active. EngineInteractor reads this to block selection.</summary>
    public static bool IsExplodedActive { get; private set; } = false;

    void Start()
    {
        RefreshParts();
        if (_allParts != null && _allParts.Length > 0)
        {
            InitExplodeTargets();
        }
        else
        {
            Debug.LogError("[EngineViewManager] No EnginePart components found in scene!");
        }
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

        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;
        
        // Apply global distance to each part
        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.explodeDistance = globalExplodeDistance;
        }
        
        // Compute each part's target based on engine centre
        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.ComputeExplodeTarget(center);
        }
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

        // Reset button states
        if (xrayButton != null)      xrayButton.SetActive(true);
        if (xrayResetButton != null) xrayResetButton.SetActive(false);
        if (explodeButton != null)   explodeButton.SetActive(true);
        if (assembleButton != null)  assembleButton.SetActive(false);

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
