using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

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

    private static readonly Color HoverLineColor = new Color(1f, 0.65f, 0.3f);
    private XRInteractorLineVisual _lineVisual;
    private Gradient _defaultLineColorGradient;
    private Gradient _hoverLineColorGradient;
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

        _allParts = FindObjectsByType<EnginePart>(FindObjectsSortMode.None);

        if (rayInteractor != null)
        {
            _lineVisual = rayInteractor.GetComponent<XRInteractorLineVisual>();
            if (_lineVisual != null) _defaultLineColorGradient = _lineVisual.invalidColorGradient;
        }

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(HoverLineColor, 0f), new GradientColorKey(HoverLineColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        _hoverLineColorGradient = g;
    }

    private bool IsMoving()
    {
        if (moveAction == null || moveAction.action == null) return false;
        return moveAction.action.ReadValue<Vector2>().magnitude > moveThreshold;
    }

    void Update()
    {
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

            if (part != _pendingPart)
            {
                _pendingPart = part;
                _hoverChangeTime = Time.time;
            }

            if (_pendingPart != _stablePart && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                _stablePart?.SetHighlight(false);
                _stablePart?.HidePanel();          // hide previous panel before switching
                _stablePart = _pendingPart;
                _stablePart?.SetHighlight(true);
                SetLineColor(_stablePart != null);

                if (_stablePart != null)
                    _stablePart.ShowPanel();
                else
                    _pendingPart?.HidePanel();
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
                ClearHover();
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

    void SetLineColor(bool hovering)
    {
        if (_lineVisual == null) return;
        var g = hovering ? _hoverLineColorGradient : _defaultLineColorGradient;
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
        if (Time.time - _lastSelectTime < SelectCooldown) return;
        _lastSelectTime = Time.time;

        // Block selection while X-Ray or Exploded View is active
        if (EngineViewManager.IsXRayActive || EngineViewManager.IsExplodedActive) return;

        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
            return;

        // toggle off — restore full engine view
        if (_activePart != null)
        {
            _activePart.HidePanel();          // hide the selected part's panel on deselect
            _activePart = null;
            _audioSource.Stop();
            foreach (var p in _allParts) p.RestoreOriginal();
            infoPanel.Hide();
            return;
        }

        // live check — ray must be on a part RIGHT NOW at the moment of the button press
        EnginePart target = GetCurrentRaycastPart();
        if (target == null)
        {
            Debug.LogWarning("[EngineInteractor] Ray is not on an engine part — ignoring select");
            return;
        }

        _activePart = target;

        foreach (var p in _allParts)
        {
            if (p == _activePart)
            {
                p.SetSelected();
                p.ShowPanel();    // keep THIS part's panel visible
            }
            else
            {
                p.SetGhost();
                p.HidePanel();    // hide every other part's panel
            }
        }

        _audioSource.Stop();
        if (_activePart.AudioClip != null)
        {
            _audioSource.clip = _activePart.AudioClip;
            _audioSource.Play();
        }

        infoPanel.Show(_activePart);
        Debug.Log($"[EngineInteractor] Isolated: {_activePart.PartName}");
    }
}
