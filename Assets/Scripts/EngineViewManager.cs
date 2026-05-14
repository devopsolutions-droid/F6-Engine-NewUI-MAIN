using UnityEngine;
using System.Collections.Generic;

public class EngineViewManager : MonoBehaviour
{
    private EnginePart[] _allParts;

    [Header("Button References")]
    public GameObject xrayButton;
    public GameObject xrayResetButton;
    public GameObject explodeButton;
    public GameObject defaultViewButton;
    public GameObject grabButton;
    public GameObject reassembleButton;

    [Header("Exploded View Settings")]
    public Transform engineRoot;
    [Range(0.1f, 3f)] public float explodeDuration = 1.2f;
    [Range(0.1f, 5f)] public float globalExplodeDistance = 1.0f;

    [Header("Dismantled Scene Root (Optional)")]
    public GameObject dismantledSceneRoot;

    [Header("References")]
    public EngineInteractor engineInteractor;

    public static bool IsXRayActive { get; private set; } = false;
    public static bool IsExplodedActive { get; private set; } = false;
    public static bool IsGrabModeActive { get; private set; } = false;

    void Start()
    {
        DisableViewButtons();
    }

    void RefreshParts()
    {
        _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Debug.Log($"[EngineViewManager] Found {_allParts.Length} EngineParts.");
    }

    void InitExplodeTargets()
    {
        if (_allParts == null || _allParts.Length == 0) return;

        if (dismantledSceneRoot != null)
        {
            ApplyDismantledPositions();
            return;
        }

        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;
        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.explodeDistance = globalExplodeDistance;
            part.ComputeExplodeTarget(center);
        }
    }

    void ApplyDismantledPositions()
    {
        bool wasActive = dismantledSceneRoot.activeSelf;
        dismantledSceneRoot.SetActive(true);

        var worldPosMap = new Dictionary<string, Vector3>();
        foreach (Transform t in dismantledSceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!worldPosMap.ContainsKey(t.gameObject.name))
                worldPosMap[t.gameObject.name] = t.position;
        }

        dismantledSceneRoot.SetActive(wasActive);

        int matched = 0;
        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;

        foreach (var part in _allParts)
        {
            if (part == null) continue;

            if (worldPosMap.TryGetValue(part.gameObject.name, out Vector3 targetWorldPos))
            {
                part.SetExplodeWorldTarget(targetWorldPos);
                matched++;
            }
            else
            {
                part.explodeDistance = globalExplodeDistance;
                part.ComputeExplodeTarget(center);
            }
        }

        Debug.Log($"[EngineViewManager] Dismantled root matched {matched}/{_allParts.Length} parts.");
    }

    public void DisableViewButtons()
    {
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
    }

    public void EnableViewButtons()
    {
        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
    }

    public void ActivateDefaultView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        IsXRayActive = false;
        IsExplodedActive = false;
        IsGrabModeActive = false;

        foreach (var part in _allParts)
            if (part != null) part.RestoreOriginal();

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
    }

    public void ActivateXRayView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        IsXRayActive = true;
        foreach (var part in _allParts)
            if (part != null) part.SetXRayView();

        // Hide ALL buttons
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        
        // Show ONLY Xray Reset button
        if (xrayResetButton != null)
        {
            xrayResetButton.SetActive(true);
            Debug.Log("[EngineViewManager] Xray Reset button activated");
        }
        else
        {
            Debug.LogError("[EngineViewManager] xrayResetButton is NOT assigned in Inspector!");
        }
    }

    public void ActivateExplodedView()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        if (IsXRayActive)
        {
            IsXRayActive = false;
            foreach (var part in _allParts)
                if (part != null) part.RestoreOriginal();
        }

        IsExplodedActive = true;
        IsGrabModeActive = false;
        InitExplodeTargets();

        foreach (var part in _allParts)
        {
            if (part != null)
            {
                part.HidePanel();
                part.AnimateToExploded(explodeDuration);
            }
        }

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        // Hide ALL buttons
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        
        // Show ONLY Default View button
        if (defaultViewButton != null)  defaultViewButton.SetActive(true);
    }

    public void ActivateGrabMode()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        if (IsXRayActive)
        {
            IsXRayActive = false;
            foreach (var part in _allParts)
                if (part != null) part.RestoreOriginal();
        }

        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        IsGrabModeActive = true;

        foreach (var part in _allParts)
        {
            if (part != null)
            {
                part.RestoreOriginal();
                part.HidePanel();
            }
        }

        if (engineInteractor != null) engineInteractor.DisableInteraction();

        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(true);
    }

    public void DeactivateGrabMode()
    {
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        IsGrabModeActive = false;

        foreach (var part in _allParts)
            if (part != null) part.AnimateToAssembled(explodeDuration);

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
    }

    void EnsureParts()
    {
        if (_allParts == null || _allParts.Length == 0)
        {
            RefreshParts();
        }
    }

    public void RefreshAfterLoad()
    {
        IsXRayActive = false;
        IsExplodedActive = false;
        IsGrabModeActive = false;

        RefreshParts();

        if (_allParts != null && _allParts.Length > 0)
            InitExplodeTargets();

        engineInteractor?.RefreshParts();
        DisableViewButtons();

        Debug.Log($"[EngineViewManager] Refreshed after load — {_allParts?.Length ?? 0} parts found.");
    }

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
