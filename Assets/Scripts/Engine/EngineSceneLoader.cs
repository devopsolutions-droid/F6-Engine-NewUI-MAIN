using System;
using UnityEngine;

[Serializable]
public class EngineSceneEntry
{
    public EngineData engineData;
    public GameObject sceneRoot;
}

/// <summary>
/// Place this in the Main Scene.
/// Maps each EngineData to its pre-placed scene root via EngineSceneEntry list.
/// On Start, activates only the selected engine and deactivates all others.
/// </summary>
public class EngineSceneLoader : MonoBehaviour
{
    [Header("Session")]
    public EngineSessionData sessionData;

    [Header("Engine Scene Roots")]
    [Tooltip("Pair each EngineData asset with its pre-placed root GameObject in this scene.")]
    public EngineSceneEntry[] engineEntries;

    [Header("Scene References")]
    public EngineViewManager engineViewManager;
    public EngineInteractor engineInteractor;
    public PartInfoPanel infoPanel;

    [Header("Fallback")]
    [Tooltip("Used when entering the scene directly in Editor without a selection.")]
    public EngineData fallbackEngine;

    [Header("Back Button")]
    public string homeSceneName = "HomeScene";

    [Header("XR")]
    [Tooltip("Drag the XR Origin (or XR Rig) root GameObject here. Its rotation is reset to 0,0,0 on every scene load.")]
    public Transform xrOrigin;

    void Start()
    {
        // Reset XR Origin rotation so the player always faces forward in the engine scene
        if (xrOrigin != null)
            xrOrigin.rotation = Quaternion.identity;

        EngineData toLoad = (sessionData != null && sessionData.HasSelection)
            ? sessionData.selectedEngine
            : fallbackEngine;

        if (toLoad == null)
        {
            Debug.LogError("[EngineSceneLoader] No engine selected and no fallback assigned!");
            return;
        }

        ActivateEngine(toLoad);
        sessionData?.Clear();
    }

    void ActivateEngine(EngineData selected)
    {
        GameObject activeRoot = null;

        foreach (var entry in engineEntries)
        {
            if (entry.sceneRoot == null) continue;
            bool isSelected = entry.engineData == selected;
            entry.sceneRoot.SetActive(isSelected);
            if (isSelected) activeRoot = entry.sceneRoot;
        }

        if (activeRoot == null)
        {
            Debug.LogError($"[EngineSceneLoader] No sceneRoot found for '{selected.engineName}'. Check engineEntries list.");
            return;
        }

        if (infoPanel != null)
            infoPanel.SetDefault(selected.engineName, selected.engineDescription);

        if (engineViewManager != null)
            engineViewManager.RefreshAfterLoad();

        Debug.Log($"[EngineSceneLoader] Activated: {selected.engineName}");
    }

    /// <summary>Called by the Back button in the engine scene.</summary>
    public void GoHome()
    {
        HomeSceneUIController.ReturnToScroll = true;

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(homeSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(homeSceneName);
    }
}
