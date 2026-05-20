using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(AudioSource))]
public class EngineInteractor : MonoBehaviour
{
    [Header("References")]
    public XRRayInteractor rayInteractor;
    public PartInfoPanel infoPanel;

    [Header("Input")]
    public InputActionReference selectAction;
    public InputActionReference moveAction;

    [Header("Layer")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Movement Detection")]
    public float moveThreshold = 0.1f;

    [Header("Haptics")]
    [Range(0f, 1f)] public float hoverHapticAmplitude = 0.2f;
    public float hoverHapticDuration = 0.05f;
    [Range(0f, 1f)] public float selectHapticAmplitude = 0.4f;
    public float selectHapticDuration = 0.08f;

    // ── Events for tablet to subscribe to ────────────────────────────────────
    /// <summary>Fired when a part is selected (trigger pressed). Null = deselected.</summary>
    public event Action<EnginePart> OnPartSelected;

    /// <summary>Fired when the ray hovers over a part. Null = no hover.</summary>
    public event Action<EnginePart> OnPartHovered;

    /// <summary>True when a part is currently isolated via trigger press.</summary>
    public bool HasActivePart => _activePart != null;

    /// <summary>
    /// Locks all engine interaction (hover + selection).
    /// Called at scene start — unlocked only after the loading sequence completes.
    /// </summary>
    public bool InteractionEnabled { get; private set; } = false;

    public void EnableInteraction()
    {
        InteractionEnabled = true;
        Debug.Log("[EngineInteractor] Interaction ENABLED.");
    }

    public void DisableInteraction()
    {
        InteractionEnabled = false;
        ClearHover();
        Debug.Log("[EngineInteractor] Interaction DISABLED.");
    }

    private static readonly Color HoverLineColor = new Color(1f, 0.65f, 0.3f);
    private XRInteractorLineVisual _lineVisual;
    private Gradient _defaultLineColorGradient;
    private Gradient _hoverLineColorGradient;
    private Gradient _whiteToBlueGradient;
    private EnginePart _stablePart;
    private EnginePart _pendingPart;
    private EnginePart _activePart;
    private EnginePart[] _allParts;
    private AudioSource _audioSource;
    private float _hoverChangeTime = -1f;
    private float _lastSelectTime = -1f;
    private const float HoverDebounce = 0.08f;
    private const float SelectCooldown = 0.4f;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (selectAction == null) { Debug.LogError("[EngineInteractor] selectAction is NOT assigned!"); return; }
        selectAction.action.performed += OnSelect;
        selectAction.action.Enable();
        moveAction?.action.Enable();
    }

    void OnDisable()
    {
        if (selectAction == null) return;
        selectAction.action.performed -= OnSelect;
        moveAction?.action.Disable();
    }

    void Start()
    {
        if (rayInteractor == null)   Debug.LogError("[EngineInteractor] rayInteractor is NOT assigned!");
        if (infoPanel == null)       Debug.LogError("[EngineInteractor] infoPanel is NOT assigned!");

        // Don't scan here — engine parts may still be inactive if EngineSceneLoader
        // hasn't run yet. RefreshParts() is called by EngineSceneLoader via
        // EngineViewManager.RefreshAfterLoad() once the engine is active.

        if (rayInteractor != null)
        {
            _lineVisual = rayInteractor.GetComponent<XRInteractorLineVisual>();
            if (_lineVisual != null) _defaultLineColorGradient = _lineVisual.invalidColorGradient;
        }

        // White to Blue gradient (white at start, deep blue at end)
        var whiteToBlue = new Gradient();
        whiteToBlue.SetKeys(
            new[] { 
                new GradientColorKey(Color.white, 0f),                                  // White at start
                new GradientColorKey(new Color(0f, 0.2f, 1f), 0.5f),                    // Blue in middle
                new GradientColorKey(new Color(0f, 0f, 0.6f), 1f)                       // Deep blue at end
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        _whiteToBlueGradient = whiteToBlue;

        // Apply white-to-blue gradient immediately on start
        if (_lineVisual != null)
        {
            _lineVisual.invalidColorGradient = _whiteToBlueGradient;
            _lineVisual.validColorGradient = _whiteToBlueGradient;
        }

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(HoverLineColor, 0f), new GradientColorKey(HoverLineColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        _hoverLineColorGradient = g;
    }

    /// <summary>
    /// Called by EngineSceneLoader (via EngineViewManager.RefreshAfterLoad) after the
    /// engine root is activated. Guarantees all EngineParts are awake before scanning.
    /// </summary>
    public void RefreshParts()
    {
        _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Debug.Log($"[EngineInteractor] RefreshParts — found {_allParts.Length} EngineParts.");
    }

    private bool IsMoving()
    {
        if (moveAction == null || moveAction.action == null) return false;
        try
        {
            return moveAction.action.ReadValue<Vector2>().magnitude > moveThreshold;
        }
        catch
        {
            return moveAction.action.ReadValue<float>() > moveThreshold;
        }
    }

    void Update()
    {
        if (!InteractionEnabled) return;

        if (_activePart != null || IsMoving()) { ClearHover(); return; }

        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
        {
            ClearHover();
            return;
        }

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            (enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
        {
            var part = hit.collider.GetComponentInParent<EnginePart>();

            // ── HOVER PANEL DEBUG ─────────────────────────────────────────────
            // Uncomment the block below if panels aren't showing.
            // It logs every frame while the ray is on a part — check the Console.
            /*
            Debug.Log($"[HoverDebug] Hit: '{hit.collider.gameObject.name}' " +
                      $"layer={hit.collider.gameObject.layer} " +
                      $"layerName={LayerMask.LayerToName(hit.collider.gameObject.layer)} | " +
                      $"EnginePart found: {(part != null ? part.gameObject.name : "NULL ← GetComponentInParent failed!")} | " +
                      $"hoverPanel: {(part != null ? (part.hoverPanel != null ? part.hoverPanel.name : "NULL ← not assigned in Inspector!") : "n/a")}");
            */
            // ─────────────────────────────────────────────────────────────────

            if (part != _pendingPart)
            {
                _pendingPart = part;
                _hoverChangeTime = Time.time;
            }

            if (_pendingPart != _stablePart && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                _stablePart?.SetHighlight(false);
                _stablePart?.HidePanel();
                _stablePart = _pendingPart;
                _stablePart?.SetHighlight(true);
                SetLineColor(_stablePart != null);

                if (_stablePart != null)
                {
                    // Trigger haptic impulse on the active controller
                    if (rayInteractor != null && rayInteractor.xrController != null)
                    {
                        rayInteractor.xrController.SendHapticImpulse(hoverHapticAmplitude, hoverHapticDuration);
                    }

                    // Don't show hover panels while the engine is in exploded view
                    if (!EngineViewManager.IsExplodedActive)
                    {
                        _stablePart.ShowPanel();
                        Debug.Log($"[EngineInteractor] ShowPanel called on '{_stablePart.gameObject.name}' | " +
                                  $"hoverPanel={((_stablePart.hoverPanel != null) ? _stablePart.hoverPanel.name + " → SetActive(true)" : "NULL ← panel not assigned!")}");
                    }
                }
                else
                    _pendingPart?.HidePanel();

                OnPartHovered?.Invoke(_stablePart);
            }
            else if (_stablePart != null)
            {
                // panel is already visible — nothing to update
            }
        }
        else
        {
            // ray is not on any engine part — start debounce timer to clear
            if (_pendingPart != null)
            {
                _pendingPart = null;
                _hoverChangeTime = Time.time;
            }

            if (_stablePart != null && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                ClearHover();
                OnPartHovered?.Invoke(null);
            }
        }
    }

    void ClearHover()
    {
        if (_stablePart != null)
        {
            _stablePart.SetHighlight(false);

            // Don't hide the panel if this part is currently selected
            // (selection keeps its panel visible intentionally)
            if (_stablePart != _activePart)
                _stablePart.HidePanel();

            _stablePart = null;
            SetLineColor(false);
        }
        _pendingPart = null;
    }

    // Line color is always white-to-blue gradient (set in Start)
    void SetLineColor(bool hovering)
    {
        if (_lineVisual == null) return;
        var g = hovering ? _hoverLineColorGradient : _whiteToBlueGradient;
        _lineVisual.invalidColorGradient = g;
        _lineVisual.validColorGradient = g;
    }

    // Returns the part the ray is pointing at RIGHT NOW — no stale state
    EnginePart GetCurrentRaycastPart()
    {
        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
            return null;
        if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            return null;
        if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) == 0)
            return null;
        return hit.collider.GetComponentInParent<EnginePart>();
    }

    void OnSelect(InputAction.CallbackContext ctx)
    {
        if (!InteractionEnabled) return;

        if (Time.time - _lastSelectTime < SelectCooldown) return;
        _lastSelectTime = Time.time;

        // X-Ray: parts are transparent wireframes — selection makes no sense, block it
        if (EngineViewManager.IsXRayActive) return;

        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
            return;

        // ── Exploded View: audio + info only, no ghosting, no position changes ──
        if (EngineViewManager.IsExplodedActive)
        {
            EnginePart target = GetCurrentRaycastPart();
            if (target == null) return;

            // Trigger select haptic
            if (rayInteractor.xrController != null)
                rayInteractor.xrController.SendHapticImpulse(selectHapticAmplitude, selectHapticDuration);

            // Stop any currently playing explanation
            _audioSource.Stop();

            if (target.AudioClip != null)
            {
                _audioSource.clip = target.AudioClip;
                _audioSource.Play();
            }

            infoPanel.Show(target);
            OnPartSelected?.Invoke(target);
            Debug.Log($"[EngineInteractor] Exploded audio play: {target.PartName}");
            return;
        }

        // ── Normal mode: full isolation — ghost others, show panel ──────────────

        // toggle off — restore full engine view
        if (_activePart != null)
        {
            // Trigger select haptic
            if (rayInteractor.xrController != null)
                rayInteractor.xrController.SendHapticImpulse(selectHapticAmplitude, selectHapticDuration);

            _activePart.HidePanel();
            _activePart = null;
            _audioSource.Stop();
            foreach (var p in _allParts) p.RestoreOriginal();
            infoPanel.Hide();
            OnPartSelected?.Invoke(null);
            return;
        }

        // live check — ray must be on a part RIGHT NOW at the moment of the button press
        EnginePart selected = GetCurrentRaycastPart();
        if (selected == null)
        {
            Debug.LogWarning("[EngineInteractor] Ray is not on an engine part — ignoring select");
            return;
        }

        // Trigger select haptic
        if (rayInteractor.xrController != null)
            rayInteractor.xrController.SendHapticImpulse(selectHapticAmplitude, selectHapticDuration);

        _activePart = selected;

        // Safety: if parts weren't scanned yet (race condition on Start), scan now
        if (_allParts == null || _allParts.Length == 0)
        {
            _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Debug.LogWarning($"[EngineInteractor] _allParts was empty at select-time — re-scanned, found {_allParts.Length}.");
        }

        foreach (var p in _allParts)
        {
            if (p == _activePart)
            {
                p.SetSelected();
                p.ShowPanel();
            }
            else
            {
                p.SetGhost();
                p.HidePanel();
            }
        }

        _audioSource.Stop();
        if (_activePart.AudioClip != null)
        {
            _audioSource.clip = _activePart.AudioClip;
            _audioSource.Play();
        }

        infoPanel.Show(_activePart);
        OnPartSelected?.Invoke(_activePart);
        Debug.Log($"[EngineInteractor] Isolated: {_activePart.PartName}");
    }
}
