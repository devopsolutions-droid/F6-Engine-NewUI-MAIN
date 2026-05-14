using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the grab loop for all engine parts.
/// Place ONE instance of this anywhere in the engine scene.
///
/// How it works:
///   Every frame, casts a ray from the XRRayInteractor.
///   On trigger down  → records the hit part + the world-space depth of the hit point
///   Every frame held → moves the grabbed part to (rayOrigin + rayDir * savedDepth)
///                       POSITION ONLY — rotation is never touched
///   On trigger up    → releases, part stays in place
///
/// Rules enforced:
///   • Only ONE part grabbed at a time
///   • Part never jumps — it moves from where it already is
///   • Part never rotates — only position changes
///   • Grabbing is blocked while EngineInteractor has an active selected part
///     (i.e. isolation / ghost mode) so the two systems don't conflict
/// </summary>
public class EngineGrabManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The XRRayInteractor on the right controller.")]
    public XRRayInteractor rayInteractor;

    [Tooltip("The InputActionReference for the grab/trigger button.")]
    public InputActionReference grabAction;

    [Header("Layer")]
    [Tooltip("Must match the EngineParts layer set in EngineInteractor.")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Settings")]
    [Tooltip("How smoothly the part follows the ray. 1 = instant, 0.1 = very smooth/laggy.")]
    [Range(0.05f, 1f)]
    public float followSpeed = 0.35f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private EnginePartGrabController _grabbed;      // currently grabbed part
    private Vector3                  _grabOffset;   // offset from part center to hit point
    private float                    _grabDepth;    // world-space depth at grab time
    private EnginePartGrabController _hovered;      // part the ray is currently over

    private bool _triggerHeld = false;

    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (grabAction == null) return;
        grabAction.action.performed += OnTriggerDown;
        grabAction.action.canceled  += OnTriggerUp;
        grabAction.action.Enable();
    }

    void OnDisable()
    {
        if (grabAction == null) return;
        grabAction.action.performed -= OnTriggerDown;
        grabAction.action.canceled  -= OnTriggerUp;
        ReleaseGrab();
    }

    void Update()
    {
        if (rayInteractor == null) return;

        // ── While a part is grabbed: move it ──────────────────────────────────
        if (_grabbed != null && _triggerHeld)
        {
            MoveGrabbedPart();
            return;   // skip hover logic while holding
        }

        // ── No grab active: update hover highlight ────────────────────────────
        UpdateHover();
    }

    // ── Trigger input ─────────────────────────────────────────────────────────

    private void OnTriggerDown(InputAction.CallbackContext ctx)
    {
        _triggerHeld = true;

        // Only grab if Grab Mode is active
        if (!EngineViewManager.IsGrabModeActive) return;

        // Don't grab if EngineInteractor is in isolation mode
        if (IsInteractorBusy()) return;

        if (!TryRaycast(out RaycastHit hit)) return;

        var grab = hit.collider.GetComponentInParent<EnginePartGrabController>();
        if (grab == null) return;

        // Release any previously grabbed part first
        ReleaseGrab();

        // Record grab
        _grabbed = grab;
        
        // ── Key: save the offset from part center to the exact hit point ──
        // This way, if you click the bottom of the part, it stays anchored there
        _grabOffset = hit.point - grab.transform.position;
        
        // Calculate depth: where the hit point sits on the ray
        Vector3 rayOrigin = GetRayOrigin();
        Vector3 rayDir    = GetRayDirection();
        Vector3 toHitPoint = hit.point - rayOrigin;
        _grabDepth = Vector3.Dot(toHitPoint, rayDir);

        // Clear hover on the grabbed part
        if (_hovered == grab)
        {
            _hovered = null;
        }

        _grabbed.OnGrabStart();
        Debug.Log($"[EngineGrabManager] Grabbed: {grab.gameObject.name} at depth {_grabDepth:F2}m, offset {_grabOffset}");
    }

    private void OnTriggerUp(InputAction.CallbackContext ctx)
    {
        _triggerHeld = false;
        ReleaseGrab();
    }

    // ── Move grabbed part ─────────────────────────────────────────────────────

    private void MoveGrabbedPart()
    {
        if (_grabbed == null) return;

        // Target position for the HIT POINT on the ray
        Vector3 origin    = GetRayOrigin();
        Vector3 direction = GetRayDirection();
        Vector3 hitPointTarget = origin + direction * _grabDepth;

        // Part center should be offset from the hit point by the saved offset
        Vector3 target = hitPointTarget - _grabOffset;

        // Lock Z — only allow X and Y movement
        target.z = _grabbed.transform.position.z;

        // Smooth follow — lerp position only, never touch rotation
        _grabbed.transform.position = Vector3.Lerp(
            _grabbed.transform.position,
            target,
            followSpeed
        );
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    private void UpdateHover()
    {
        // In grab mode, don't show hover panels or audio — just outline
        if (EngineViewManager.IsGrabModeActive)
        {
            EnginePartGrabController hitGrab = null;

            if (TryRaycast(out RaycastHit hit))
                hitGrab = hit.collider.GetComponentInParent<EnginePartGrabController>();

            if (hitGrab == _hovered) return;

            // Exit previous hover
            _hovered?.OnHoverExit();

            // Enter new hover
            _hovered = hitGrab;
            _hovered?.OnHoverEnter();
            return;
        }

        // Normal mode: show hover panels (handled by EngineInteractor)
        // We don't manage hover in normal mode — EngineInteractor does
    }

    // ── Release ───────────────────────────────────────────────────────────────

    private void ReleaseGrab()
    {
        if (_grabbed == null) return;

        _grabbed.OnGrabEnd();
        Debug.Log($"[EngineGrabManager] Released: {_grabbed.gameObject.name}");
        _grabbed = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryRaycast(out RaycastHit hit)
    {
        hit = default;
        if (rayInteractor == null) return false;

        // Use the XRRayInteractor's current 3D raycast hit if available
        if (rayInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            // Only count hits on the EngineParts layer
            if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                return true;
        }

        return false;
    }

    private Vector3 GetRayOrigin()
    {
        // XRRayInteractor exposes the ray origin via its transform
        return rayInteractor.transform.position;
    }

    private Vector3 GetRayDirection()
    {
        // The ray points forward from the interactor's transform
        return rayInteractor.transform.forward;
    }

    /// <summary>
    /// Returns true if EngineInteractor currently has a part selected (isolation mode).
    /// In that state the trigger is owned by EngineInteractor, not us.
    /// </summary>
    private bool IsInteractorBusy()
    {
        var interactor = FindFirstObjectByType<EngineInteractor>();
        return interactor != null && interactor.HasActivePart;
    }
}
