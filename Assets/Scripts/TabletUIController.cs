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
///   Auto Explain Button     → OnAutoExplainClicked
///   Auto Explain Button Off → OnAutoExplainOffClicked
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
    public AudioSource       engineAudioSource;
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

    // ── Main Menu Button GameObjects ──────────────────────────────────────────
    [Header("Main Menu — View Buttons")]
    public GameObject xrayButtonGO;
    public GameObject xrayResetButtonGO;
    public GameObject explodeButtonGO;
    public GameObject defaultViewButtonGO;
    public GameObject disassembleButtonGO;
    public GameObject assembleButtonGO;

    [Header("Main Menu — Audio Buttons")]
    public GameObject autoExplainButtonGO;
    public GameObject autoExplainOffButtonGO;

    // ── Engine Display Area ───────────────────────────────────────────────────
    [Header("Engine Display Area")]
    public Image           engineDisplayImage;

    [Tooltip("Always shows the engine name — never changes.")]
    public TextMeshProUGUI engineNameText;

    [Tooltip("Shows engine description by default. Switches to part description on selection.")]
    public TextMeshProUGUI engineDescriptionText;

    [Tooltip("Shows selected part name. Empty by default.")]
    public TextMeshProUGUI partNameText;

    // ── Internal ──────────────────────────────────────────────────────────────
    private bool      _isExplaining;
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

        RefreshButtonStates();

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

    void Update()
    {
        if (_isExplaining && engineAudioSource != null && !engineAudioSource.isPlaying)
        {
            _isExplaining = false;
            RefreshButtonStates();
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
        RefreshButtonStates();
    }

    public void OnXRayResetClicked()
    {
        engineViewManager?.ActivateDefaultView();
        RefreshButtonStates();
    }

    public void OnExplodeClicked()
    {
        engineViewManager?.ActivateExplodedView();
        RefreshButtonStates();
    }

    public void OnDefaultViewClicked()
    {
        engineViewManager?.ActivateAssembledView();
        RefreshButtonStates();
    }

    public void OnAutoExplainClicked()
    {
        if (_isExplaining) return;
        _isExplaining = true;
        if (engineAudioSource != null && !engineAudioSource.isPlaying)
            engineAudioSource.Play();
        RefreshButtonStates();
    }

    public void OnAutoExplainOffClicked()
    {
        _isExplaining = false;
        if (engineAudioSource != null && engineAudioSource.isPlaying)
            engineAudioSource.Stop();
        RefreshButtonStates();
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

        // Unlock all engine interactions now that loading is complete
        engineInteractor?.EnableInteraction();
        engineViewManager?.EnableViewButtons();

        _loadingCoroutine = null;
    }

    // ── Button State Sync ─────────────────────────────────────────────────────

    void RefreshButtonStates()
    {
        bool xray    = EngineViewManager.IsXRayActive;
        bool explode = EngineViewManager.IsExplodedActive;

        if (xrayButtonGO != null)        xrayButtonGO.SetActive(!xray);
        if (xrayResetButtonGO != null)   xrayResetButtonGO.SetActive(xray);

        if (explodeButtonGO != null)     explodeButtonGO.SetActive(!explode);
        if (disassembleButtonGO != null) disassembleButtonGO.SetActive(!explode);
        if (defaultViewButtonGO != null) defaultViewButtonGO.SetActive(explode);
        if (assembleButtonGO != null)    assembleButtonGO.SetActive(explode);

        if (autoExplainButtonGO != null)    autoExplainButtonGO.SetActive(!_isExplaining);
        if (autoExplainOffButtonGO != null) autoExplainOffButtonGO.SetActive(_isExplaining);
    }

    // ── Engine Display ────────────────────────────────────────────────────────

    void PopulateEngineDisplay()
    {
        if (currentEngineData == null) return;

        if (engineNameText != null)
            engineNameText.text = currentEngineData.engineName;

        if (engineDescriptionText != null)
            engineDescriptionText.text = currentEngineData.engineDescription;

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
