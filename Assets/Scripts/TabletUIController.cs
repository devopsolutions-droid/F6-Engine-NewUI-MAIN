using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the full tablet UI flow in the Main Scene.
///
/// Inspector wiring for each button's On Clicked ():
///   START Button            → OnStartClicked
///   X-Ray Button            → OnXRayClicked
///   X-Ray Button Reset      → OnXRayResetClicked
///   Explode                 → OnExplodeClicked
///   Disassemble Button      → OnExplodeClicked
///   Default View            → OnDefaultViewClicked
///   Assemble Button         → OnDefaultViewClicked
///   Exit Button             → OnExitClicked
///   BACK Button             → OnBackClicked
/// </summary>
public class TabletUIController : MonoBehaviour
{
    // ── Scene Systems ─────────────────────────────────────────────────────────
    [Header("Scene Systems")]
    public EngineViewManager engineViewManager;
    public EngineInteractor  engineInteractor;
    public EngineData        currentEngineData;

    // ── Panels ────────────────────────────────────────────────────────────────
    [Header("Panels")]
    public GameObject loadingScreenPanel;
    public GameObject mainMenuPanel;

    // ── Loading Screen ────────────────────────────────────────────────────────
    [Header("Loading Screen")]
    public GameObject startButtonGO;
    public GameObject progressBarGO;
    [Tooltip("How long the loading bar runs before the main menu appears and interactions unlock.")]
    public float      loadingDuration = 6f;

    // ── Engine Display Area ───────────────────────────────────────────────────
    [Header("Engine Display Area")]
    public Image           engineDisplayImage;

    [Tooltip("Header on the tablet; set from EngineData.engineName when the scene loads.")]
    public TextMeshProUGUI engineNameText;

    [Tooltip("Engine overview from EngineData (what it is, role, typical use). Switches to part description on hover/select.")]
    public TextMeshProUGUI engineDescriptionText;

    [Tooltip("Shows selected part name. Empty by default.")]
    public TextMeshProUGUI partNameText;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Coroutine _loadingCoroutine;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(false);
        if (progressBarGO != null)      progressBarGO.SetActive(false);

        if (engineInteractor != null)
        {
            engineInteractor.OnPartSelected += HandlePartSelected;
            engineInteractor.OnPartHovered  += HandlePartHovered;
        }

        // Wait one frame for EngineSceneLoader to call SetEngineData()
        // before populating the display
        StartCoroutine(PopulateAfterLoad());
    }

    System.Collections.IEnumerator PopulateAfterLoad()
    {
        yield return null; // wait one frame
        PopulateEngineDisplay();
    }

    void OnDestroy()
    {
        if (engineInteractor != null)
        {
            engineInteractor.OnPartSelected -= HandlePartSelected;
            engineInteractor.OnPartHovered  -= HandlePartHovered;
        }
    }

    // ── Button Methods ────────────────────────────────────────────────────────

    public void OnStartClicked()
    {
        if (startButtonGO != null) startButtonGO.SetActive(false);

        // Disable then re-enable forces SliderAnimation.Awake() to run again → resets to 0
        if (progressBarGO != null)
        {
            progressBarGO.SetActive(false);
            progressBarGO.SetActive(true);
        }

        if (_loadingCoroutine != null) StopCoroutine(_loadingCoroutine);
        _loadingCoroutine = StartCoroutine(RunLoadingBar());
    }

    public void OnXRayClicked()
    {
        engineViewManager?.ActivateXRayView();
    }

    public void OnXRayResetClicked()
    {
        engineViewManager?.ActivateDefaultView();
    }

    public void OnExplodeClicked()
    {
        engineViewManager?.ActivateExplodedView();
    }

    public void OnDefaultViewClicked()
    {
        engineViewManager?.ActivateDefaultView();
    }

    public void OnGrabClicked()
    {
        engineViewManager?.ActivateGrabMode();
    }

    public void OnReassembleClicked()
    {
        engineViewManager?.DeactivateGrabMode();
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnBackClicked()
    {
        var loader = FindFirstObjectByType<EngineSceneLoader>();
        loader?.GoHome();
    }

    /// <summary>Wire to the Back button inside Main Menu to return to Loading Screen.</summary>
    public void OnBackToLoadingScreen()
    {
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);

        // Reset loading screen state
        if (startButtonGO != null)  startButtonGO.SetActive(true);

        // Fully reset the progress bar by disabling then re-enabling
        // SliderAnimation resets to 0 on Awake, so toggling forces a clean restart
        if (progressBarGO != null)
        {
            progressBarGO.SetActive(false);
            // Keep it hidden — it will show again when START is clicked
        }

        // Stop any running loading coroutine
        if (_loadingCoroutine != null)
        {
            StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = null;
        }
    }

    // ── Loading Bar ───────────────────────────────────────────────────────────

    IEnumerator RunLoadingBar()
    {
        yield return new WaitForSeconds(loadingDuration);

        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(true);

        // Tablet was hidden during loading — refresh engine copy so TMP layout is correct
        PopulateEngineDisplay();

        // Unlock all engine interactions now that loading is complete
        engineInteractor?.EnableInteraction();
        engineViewManager?.EnableViewButtons();

        _loadingCoroutine = null;
    }

    // ── Button State Sync ─────────────────────────────────────────────────────
    // Button visibility is now controlled entirely by EngineViewManager.cs
    // No button state management here.

    // ── Engine Display ────────────────────────────────────────────────────────

    void PopulateEngineDisplay()
    {
        if (currentEngineData == null) return;

        if (engineNameText != null)
            engineNameText.text = currentEngineData.engineName;

        if (engineDescriptionText != null)
        {
            engineDescriptionText.text = currentEngineData.engineDescription ?? "";
            engineDescriptionText.ForceMeshUpdate(true);
        }

        if (partNameText != null)
            partNameText.text = "";

        if (engineDisplayImage != null && currentEngineData.thumbnail != null)
            engineDisplayImage.sprite = currentEngineData.thumbnail;
    }

    /// <summary>Called by EngineSceneLoader after engine activates.</summary>
    public void SetEngineData(EngineData data)
    {
        currentEngineData = data;
        PopulateEngineDisplay();
    }

    // ── Part Info Handlers ────────────────────────────────────────────────────

    void HandlePartSelected(EnginePart part)
    {
        if (part == null)
        {
            // Deselected — reset to engine defaults
            if (partNameText != null)
                partNameText.text = "";
            if (engineDescriptionText != null && currentEngineData != null)
                engineDescriptionText.text = currentEngineData.engineDescription;
        }
        else
        {
            // Part selected — lock name and description to this part
            if (partNameText != null)
                partNameText.text = part.PartName;
            if (engineDescriptionText != null)
                engineDescriptionText.text = part.Description;
        }
    }

    void HandlePartHovered(EnginePart part)
    {
        // NEVER override display when a part is already selected
        if (engineInteractor != null && engineInteractor.HasActivePart) return;

        if (part == null)
        {
            if (partNameText != null)
                partNameText.text = "";
            if (engineDescriptionText != null && currentEngineData != null)
                engineDescriptionText.text = currentEngineData.engineDescription;
        }
        else
        {
            if (partNameText != null)
                partNameText.text = part.PartName;
            if (engineDescriptionText != null)
                engineDescriptionText.text = part.Description;
        }
    }
}
