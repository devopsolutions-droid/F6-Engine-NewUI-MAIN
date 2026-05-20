using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Simple Part Explorer - Shows one engine part at a time.
/// Automatically detects which engine is active and uses its pre-existing ScriptableObject manifest.
/// Only the active part is visible; all other parts are completely invisible.
/// Plays audio explanations and displays text on the tablet and wall monitor.
/// </summary>
public class SimplePartExplorer : MonoBehaviour, IPointerClickHandler
{
    [Header("UI - Parent Panel")]
    [Tooltip("Optional parent panel for the explorer UI. Activated when explorer starts, deactivated when stops.")]
    [SerializeField] private GameObject explorerPanel;

    [Header("UI - Tablet")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image previousButtonImage;  // Assign if using raw Image buttons instead of Button components
    [SerializeField] private Image nextButtonImage;      // Assign if using raw Image buttons instead of Button components
    [SerializeField] private TextMeshProUGUI tabletPartName;
    [SerializeField] private TextMeshProUGUI tabletPartDescription;
    [SerializeField] private TextMeshProUGUI stepCounter;
    
    [Header("UI - Wall Monitor")]
    [SerializeField] private TextMeshProUGUI monitorPartName;
    [SerializeField] private TextMeshProUGUI monitorPartDescription;
    [SerializeField] private AudioSource audioSource;
    
    private EnginePartManifest enginePartManifest;
    private Transform engineRoot;
    private List<PartData> partsList = new List<PartData>();
    private List<EnginePart> enginePartsList = new List<EnginePart>();
    private int currentPartIndex = -1;
    private bool isExplorerActive = false;
    
    private CanvasGroup previousButtonCanvasGroup;
    private CanvasGroup nextButtonCanvasGroup;

    private void Start()
    {
        // ── Set up CanvasGroups for fading out disabled buttons ──
        Image prevImg = previousButtonImage != null ? previousButtonImage : (previousButton != null ? previousButton.GetComponent<Image>() : null);
        if (prevImg != null)
        {
            previousButtonCanvasGroup = prevImg.GetComponent<CanvasGroup>();
            if (previousButtonCanvasGroup == null)
                previousButtonCanvasGroup = prevImg.gameObject.AddComponent<CanvasGroup>();
        }

        Image nextImg = nextButtonImage != null ? nextButtonImage : (nextButton != null ? nextButton.GetComponent<Image>() : null);
        if (nextImg != null)
        {
            nextButtonCanvasGroup = nextImg.GetComponent<CanvasGroup>();
            if (nextButtonCanvasGroup == null)
                nextButtonCanvasGroup = nextImg.gameObject.AddComponent<CanvasGroup>();
        }

        // Hide the explorer panel by default at start if it's assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
    }

    /// <summary>
    /// Keep IPointerClickHandler as secondary fallback for legacy image clicking
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isExplorerActive) return;

        if (previousButtonImage != null && eventData.pointerCurrentRaycast.gameObject == previousButtonImage.gameObject)
        {
            PreviousPart();
        }
        else if (nextButtonImage != null && eventData.pointerCurrentRaycast.gameObject == nextButtonImage.gameObject)
        {
            NextPart();
        }
    }

    /// <summary>
    /// Start the explorer - call this from "Show Working" button
    /// </summary>
    public void StartExplorer()
    {
        // Automatically sync with EngineViewManager if it is not already in show working mode
        EngineViewManager viewManager = FindFirstObjectByType<EngineViewManager>();
        if (viewManager != null && !EngineViewManager.IsShowWorkingActive)
        {
            Debug.Log("[SimplePartExplorer] Delegating StartExplorer to EngineViewManager to handle view transition and button states.");
            viewManager.ActivateShowWorkingView();
            return;
        }

        // Initialize audio source if null
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        if (!LoadPartsFromManifest())
        {
            Debug.LogError("SimplePartExplorer: Failed to load parts from manifest!");
            return;
        }

        if (partsList.Count == 0)
        {
            Debug.LogError("SimplePartExplorer: No parts found in manifest!");
            return;
        }

        isExplorerActive = true;
        currentPartIndex = -1;

        // Disable standard raycast/hover interactions while in step-by-step mode
        EngineInteractor interactor = FindFirstObjectByType<EngineInteractor>();
        if (interactor != null)
        {
            interactor.DisableInteraction();
        }

        // Show the parent panel if assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(true);
        }

        // Show first part
        NextPart();
    }

    /// <summary>
    /// Stop the explorer and restore all parts to be fully visible and original
    /// </summary>
    public void StopExplorer()
    {
        // Automatically sync with EngineViewManager if it is in show working mode
        EngineViewManager viewManager = FindFirstObjectByType<EngineViewManager>();
        if (viewManager != null && EngineViewManager.IsShowWorkingActive)
        {
            Debug.Log("[SimplePartExplorer] Delegating StopExplorer to EngineViewManager to restore view states.");
            viewManager.ActivateDefaultView();
            return;
        }

        isExplorerActive = false;
        currentPartIndex = -1;

        // Restore all parts to fully visible, their original looks, and original positions
        foreach (var part in enginePartsList)
        {
            if (part != null)
            {
                part.SetVisible(true);
                part.RestoreOriginal();
                part.LowerDown(0f);
            }
        }

        // Re-enable standard raycast/hover interactions
        EngineInteractor interactor = FindFirstObjectByType<EngineInteractor>();
        if (interactor != null)
        {
            interactor.EnableInteraction();
        }

        // Hide UI
        if (tabletPartName != null) tabletPartName.gameObject.SetActive(false);
        if (tabletPartDescription != null) tabletPartDescription.gameObject.SetActive(false);
        if (monitorPartName != null) monitorPartName.gameObject.SetActive(false);
        if (monitorPartDescription != null) monitorPartDescription.gameObject.SetActive(false);
        if (stepCounter != null) stepCounter.gameObject.SetActive(false);

        // Hide previous/next buttons if not using a parent panel
        if (previousButton != null && explorerPanel == null) previousButton.gameObject.SetActive(false);
        if (nextButton != null && explorerPanel == null) nextButton.gameObject.SetActive(false);
        if (previousButtonImage != null && explorerPanel == null) previousButtonImage.gameObject.SetActive(false);
        if (nextButtonImage != null && explorerPanel == null) nextButtonImage.gameObject.SetActive(false);

        // Hide the parent panel if assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(false);
        }
        
        // Initialize audio source if null
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        // Stop audio
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    /// <summary>
    /// Move to next part
    /// </summary>
    public void NextPart()
    {
        if (!isExplorerActive) return;

        int nextIndex = currentPartIndex + 1;

        if (nextIndex >= partsList.Count)
        {
            // End of explorer, restore all
            StopExplorer();
            return;
        }

        ShowPart(nextIndex);
    }

    /// <summary>
    /// Move to previous part
    /// </summary>
    public void PreviousPart()
    {
        if (!isExplorerActive) return;

        int prevIndex = currentPartIndex - 1;

        if (prevIndex < 0)
        {
            Debug.LogWarning("SimplePartExplorer: Already at first part!");
            return;
        }

        ShowPart(prevIndex);
    }

    /// <summary>
    /// Show a specific part: makes only this part visible, others invisible.
    /// </summary>
    private void ShowPart(int index)
    {
        if (index < 0 || index >= partsList.Count) return;

        currentPartIndex = index;
        var partData = partsList[index];
        var enginePart = enginePartsList[index];

        if (enginePart == null)
        {
            Debug.LogWarning($"SimplePartExplorer: EnginePart at index {index} is null!");
            return;
        }

        // Make the active part visible and show its original look with a solid outline
        enginePart.SetVisible(true);
        enginePart.SetSelected();
        enginePart.LiftUp(0.15f, 0.25f);

        // Make all other parts transparent/ghosted (alpha 0.1 - 0.3)
        for (int i = 0; i < enginePartsList.Count; i++)
        {
            if (i != index && enginePartsList[i] != null)
            {
                enginePartsList[i].SetVisible(true);
                enginePartsList[i].SetGhost();
                enginePartsList[i].LowerDown(0.25f);
            }
        }

        // Update tablet UI
        if (tabletPartName != null)
        {
            tabletPartName.gameObject.SetActive(true);
            tabletPartName.text = partData.partName;
        }

        if (tabletPartDescription != null)
        {
            tabletPartDescription.gameObject.SetActive(true);
            tabletPartDescription.text = partData.description;
        }

        // Update monitor UI
        if (monitorPartName != null)
        {
            monitorPartName.gameObject.SetActive(true);
            monitorPartName.text = partData.partName;
        }

        if (monitorPartDescription != null)
        {
            monitorPartDescription.gameObject.SetActive(true);
            monitorPartDescription.text = partData.description;
        }

        // Update step counter
        if (stepCounter != null)
        {
            stepCounter.gameObject.SetActive(true);
            stepCounter.text = $"Part {index + 1} / {partsList.Count}";
        }

        // Play audio explanation
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        if (audioSource != null)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            // Find matching audio clip (prefer PartData, fallback to EnginePart)
            AudioClip clipToPlay = null;
            if (partData != null && partData.audioExplanation != null)
            {
                clipToPlay = partData.audioExplanation;
            }
            else if (enginePart != null)
            {
                clipToPlay = enginePart.AudioClip;
            }

            if (clipToPlay != null)
            {
                audioSource.clip = clipToPlay;
                audioSource.Play();
            }
        }

        // Update button states
        UpdateButtonStates();
    }

    /// <summary>
    /// Update button enabled/disabled states (alpha control)
    /// </summary>
    private void UpdateButtonStates()
    {
        bool canGoPrevious = (currentPartIndex > 0);
        bool canGoNext = (currentPartIndex < partsList.Count - 1);

        // Update Previous button
        if (previousButton != null)
        {
            previousButton.interactable = canGoPrevious;
        }
        if (previousButtonCanvasGroup != null)
        {
            previousButtonCanvasGroup.alpha = canGoPrevious ? 1f : 0.5f;
            previousButtonCanvasGroup.interactable = canGoPrevious;
        }

        // Update Next button
        if (nextButton != null)
        {
            nextButton.interactable = canGoNext;
        }
        if (nextButtonCanvasGroup != null)
        {
            nextButtonCanvasGroup.alpha = canGoNext ? 1f : 0.5f;
            nextButtonCanvasGroup.interactable = canGoNext;
        }
    }

    /// <summary>
    /// Load parts dynamically from the currently active engine's manifest
    /// </summary>
    private bool LoadPartsFromManifest()
    {
        // Find the EngineSceneLoader in the scene
        EngineSceneLoader sceneLoader = FindFirstObjectByType<EngineSceneLoader>();
        if (sceneLoader == null)
        {
            Debug.LogError("SimplePartExplorer: Could not find EngineSceneLoader in scene!");
            return false;
        }

        // Get the active root and active engine data
        if (sceneLoader.ActiveEngineRoot == null || sceneLoader.ActiveEngineData == null)
        {
            Debug.LogError("SimplePartExplorer: EngineSceneLoader has no active engine loaded!");
            return false;
        }

        engineRoot = sceneLoader.ActiveEngineRoot.transform;
        enginePartManifest = sceneLoader.ActiveEngineData.partManifest;

        if (enginePartManifest == null)
        {
            Debug.LogError($"SimplePartExplorer: Engine '{sceneLoader.ActiveEngineData.engineName}' has no part manifest assigned!");
            return false;
        }

        Debug.Log($"SimplePartExplorer: Hooked into active engine '{sceneLoader.ActiveEngineData.engineName}' using manifest '{enginePartManifest.name}'");

        partsList.Clear();
        enginePartsList.Clear();

        var partEntries = enginePartManifest.parts;
        if (partEntries == null || partEntries.Count == 0)
        {
            Debug.LogError("SimplePartExplorer: No parts defined in the active engine's manifest!");
            return false;
        }

        // Load each part in the exact index order specified in the manifest ScriptableObject
        foreach (var entry in partEntries)
        {
            if (entry == null || entry.partData == null) continue;

            // Find EnginePart child under the active engine root
            Transform partTransform = engineRoot.Find(entry.gameObjectName);
            if (partTransform == null)
            {
                // Fallback: search recursively if not a direct child
                partTransform = FindChildRecursive(engineRoot, entry.gameObjectName);
            }

            if (partTransform == null)
            {
                Debug.LogWarning($"SimplePartExplorer: Could not find part GameObject '{entry.gameObjectName}' under root '{engineRoot.name}'");
                continue;
            }

            EnginePart enginePart = partTransform.GetComponent<EnginePart>();
            if (enginePart == null)
            {
                Debug.LogWarning($"SimplePartExplorer: Part GameObject '{entry.gameObjectName}' is missing the EnginePart component!");
                continue;
            }

            partsList.Add(entry.partData);
            enginePartsList.Add(enginePart);
        }

        Debug.Log($"SimplePartExplorer: Loaded {partsList.Count} parts successfully.");
        return partsList.Count > 0;
    }

    /// <summary>
    /// Recursive child search helper
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;
            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Context menu to automatically find and wire up UI elements in the scene.
    /// Right-click the SimplePartExplorer component in the Inspector to run.
    /// </summary>
    [ContextMenu("Auto Wire UI References")]
    private void AutoWireUI()
    {
        if (audioSource == null)
            audioSource = FindFirstObjectByType<AudioSource>();

        var allText = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in allText)
        {
            string lowerName = txt.gameObject.name.ToLower();
            if (tabletPartName == null && lowerName.Contains("tablet") && lowerName.Contains("name"))
                tabletPartName = txt;
            else if (tabletPartDescription == null && lowerName.Contains("tablet") && (lowerName.Contains("desc") || lowerName.Contains("info") || lowerName.Contains("body")))
                tabletPartDescription = txt;
            else if (monitorPartName == null && (lowerName.Contains("monitor") || lowerName.Contains("wall") || lowerName.Contains("screen")) && lowerName.Contains("name"))
                monitorPartName = txt;
            else if (monitorPartDescription == null && (lowerName.Contains("monitor") || lowerName.Contains("wall") || lowerName.Contains("screen")) && (lowerName.Contains("desc") || lowerName.Contains("info") || lowerName.Contains("body")))
                monitorPartDescription = txt;
            else if (stepCounter == null && (lowerName.Contains("counter") || lowerName.Contains("step") || lowerName.Contains("index") || lowerName.Contains("number")))
                stepCounter = txt;
        }

        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            string lowerName = btn.gameObject.name.ToLower();
            if (previousButton == null && (lowerName.Contains("prev") || lowerName.Contains("back") || lowerName.Contains("left")))
                previousButton = btn;
            else if (nextButton == null && (lowerName.Contains("next") || lowerName.Contains("forward") || lowerName.Contains("right")))
                nextButton = btn;
        }

        var allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in allImages)
        {
            string lowerName = img.gameObject.name.ToLower();
            if (previousButton == null && previousButtonImage == null && (lowerName.Contains("prev") || lowerName.Contains("back") || lowerName.Contains("left")))
                previousButtonImage = img;
            else if (nextButton == null && nextButtonImage == null && (lowerName.Contains("next") || lowerName.Contains("forward") || lowerName.Contains("right")))
                nextButtonImage = img;
        }

        // Try to find the parent panel
        if (explorerPanel == null)
        {
            var allGo = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                string lowerName = go.name.ToLower();
                if (lowerName.Contains("explorerpanel") || lowerName.Contains("workingpanel") || lowerName.Contains("showworking"))
                {
                    explorerPanel = go;
                    break;
                }
            }
        }

        Debug.Log("SimplePartExplorer: Finished auto-wiring UI references!");
    }
}
