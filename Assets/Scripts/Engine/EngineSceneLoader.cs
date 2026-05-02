using UnityEngine;

/// <summary>
/// Place this in the EngineViewScene.
/// On Start it reads EngineSessionData, spawns the selected engine prefab,
/// and wires it into EngineViewManager and EngineInteractor automatically.
/// </summary>
public class EngineSceneLoader : MonoBehaviour
{
    [Header("Session")]
    public EngineSessionData sessionData;

    [Header("Scene References")]
    public EngineViewManager engineViewManager;
    public EngineInteractor engineInteractor;
    public PartInfoPanel infoPanel;

    [Header("Spawn")]
    [Tooltip("If sessionData has no selection, this fallback prefab is used (for testing).")]
    public EngineData fallbackEngine;

    [Header("Back Button")]
    [Tooltip("Name of the Home scene in Build Settings.")]
    public string homeSceneName = "HomeScene";

    private GameObject _spawnedEngine;

    void Start()
    {
        EngineData toLoad = (sessionData != null && sessionData.HasSelection)
            ? sessionData.selectedEngine
            : fallbackEngine;

        if (toLoad == null)
        {
            Debug.LogError("[EngineSceneLoader] No engine selected and no fallback assigned!");
            return;
        }

        LoadEngine(toLoad);

        // Clear session so re-entering home doesn't auto-load
        sessionData?.Clear();
    }

    void LoadEngine(EngineData data)
    {
        if (data.enginePrefab == null)
        {
            Debug.LogError($"[EngineSceneLoader] EngineData '{data.engineName}' has no prefab assigned!");
            return;
        }

        if (_spawnedEngine != null) Destroy(_spawnedEngine);

        _spawnedEngine = Instantiate(
            data.enginePrefab,
            data.spawnPosition,
            Quaternion.Euler(data.spawnRotation)
        );
        _spawnedEngine.name = $"[Engine] {data.engineName}";

        // Apply PartData from manifest to every EnginePart on the spawned prefab
        if (data.partManifest != null)
        {
            var parts = _spawnedEngine.GetComponentsInChildren<EnginePart>(true);
            foreach (var part in parts)
            {
                var pd = data.partManifest.GetPartData(part.gameObject.name);
                if (pd != null) part.partData = pd;
            }
            Debug.Log($"[EngineSceneLoader] Applied manifest to {parts.Length} parts.");
        }

        if (infoPanel != null)
            infoPanel.SetDefault(data.engineName, data.engineDescription);

        if (engineViewManager != null)
            engineViewManager.RefreshAfterLoad();

        Debug.Log($"[EngineSceneLoader] Loaded engine: {data.engineName}");
    }

    /// <summary>Called by the Back button in the engine scene.</summary>
    public void GoHome()
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(homeSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(homeSceneName);
    }
}
