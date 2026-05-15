using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the grab loop for all engine parts.
/// Place ONE instance of this anywhere in the engine scene.
///
/// How it works:
///   Every frame, casts a ray from the XRRayInteractor.
///   On trigger down  → records the hit part + the world-space depth of the hit point
///   Every frame held → X/Y follow the ray (position only, no rotation)
///                      Z is driven by the joystick (thumbstick forward / back)
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

    [Tooltip("Optional thumbstick action for Z (XRI Left/Right Hand Move). " +
             "Leave empty to read controllers directly — recommended.")]
    public InputActionReference depthAction;

    [Header("Layer")]
    [Tooltip("Must match the EngineParts layer set in EngineInteractor.")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Settings")]
    [Tooltip("How smoothly the part follows the ray on X/Y. 1 = instant, 0.1 = very smooth/laggy.")]
    [Range(0.05f, 1f)]
    public float followSpeed = 0.35f;

    [Tooltip("World-space Z movement speed (m/s) at full thumbstick deflection.")]
    [Min(0.01f)]
    public float depthMoveSpeed = 0.8f;

    public enum DepthStickHand
    {
        Left,
        Right,
        BothUseStrongest
    }

    [Tooltip("Which controller thumbstick moves Z. 'Both' accepts either hand.")]
    public DepthStickHand depthStickHand = DepthStickHand.BothUseStrongest;

    [Tooltip("Which axis of the thumbstick Vector2 drives Z (0 = X, 1 = Y). Y = forward/back on most controllers.")]
    [Range(0, 1)]
    public int depthStickAxis = 1;

    [Tooltip("Flip thumbstick direction for Z.")]
    public bool invertDepthAxis = false;

    [Tooltip("Ignore thumbstick input below this magnitude.")]
    [Range(0f, 0.5f)]
    public float depthInputDeadzone = 0.08f;

    [Header("Locomotion")]
    [Tooltip("Disable XR walk/turn while a part is held so the thumbstick only moves the part on Z.")]
    public bool disableLocomotionWhileGrabbing = true;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private EnginePartGrabController _grabbed;      // currently grabbed part
    private Vector3                  _grabOffset;   // offset from part center to hit point
    private float                    _grabDepth;    // world-space depth at grab time
    private float                    _grabZ;        // Z position while grabbed (joystick-controlled)
    private EnginePartGrabController _hovered;      // part the ray is currently over

    private bool _triggerHeld = false;

    private bool _locomotionSuppressed;
    private readonly List<LocomotionBackup> _locomotionBackups = new();

    private struct LocomotionBackup
    {
        public MonoBehaviour Component;
        public bool WasEnabled;
        public float SavedSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        ResolveDepthActionFallback();
    }

    void OnEnable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed += OnTriggerDown;
            grabAction.action.canceled  += OnTriggerUp;
            grabAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed -= OnTriggerDown;
            grabAction.action.canceled  -= OnTriggerUp;
        }

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
        _grabZ     = grab.transform.position.z;

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
        SuppressLocomotion();
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

        // ── Z: joystick forward / back (independent of the ray) ─────────────────
        float stickInput = ReadDepthStickInput();
        if (Mathf.Abs(stickInput) > depthInputDeadzone)
            _grabZ += stickInput * depthMoveSpeed * Time.deltaTime;

        // ── X/Y: ray hit point (Z comes from _grabZ only) ─────────────────────
        Vector3 origin         = GetRayOrigin();
        Vector3 direction      = GetRayDirection();
        Vector3 hitPointTarget = origin + direction * _grabDepth;
        Vector3 rayTarget      = hitPointTarget - _grabOffset;
        rayTarget.z = _grabZ;

        Vector3 current = _grabbed.transform.position;
        Vector3 next = new Vector3(
            Mathf.Lerp(current.x, rayTarget.x, followSpeed),
            Mathf.Lerp(current.y, rayTarget.y, followSpeed),
            _grabZ
        );

        _grabbed.transform.position = next;
    }

    /// <summary>
    /// Reads thumbstick for Z. Prefers direct XR hardware (always works when a controller is connected),
    /// then falls back to the optional InputActionReference.
    /// </summary>
    private float ReadDepthStickInput()
    {
        float xr = ReadDepthFromXRDevices();
        if (Mathf.Abs(xr) > depthInputDeadzone)
            return xr;

        return ReadDepthFromAction();
    }

    private float ReadDepthFromXRDevices()
    {
        float best = 0f;

        switch (depthStickHand)
        {
            case DepthStickHand.Left:
                TryReadThumbstick(XRNode.LeftHand, ref best);
                break;
            case DepthStickHand.Right:
                TryReadThumbstick(XRNode.RightHand, ref best);
                break;
            default:
                TryReadThumbstick(XRNode.LeftHand, ref best);
                TryReadThumbstick(XRNode.RightHand, ref best);
                break;
        }

        return best;
    }

    private void TryReadThumbstick(XRNode node, ref float best)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;
        if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis)) return;

        float val = depthStickAxis == 0 ? axis.x : axis.y;
        if (invertDepthAxis) val = -val;

        if (Mathf.Abs(val) > Mathf.Abs(best))
            best = val;
    }

    private float ReadDepthFromAction()
    {
        if (depthAction == null || depthAction.action == null) return 0f;

        var action = depthAction.action;
        if (!action.enabled)
            action.Enable();

        float raw;
        try
        {
            Vector2 stick = action.ReadValue<Vector2>();
            raw = depthStickAxis == 0 ? stick.x : stick.y;
        }
        catch
        {
            raw = action.ReadValue<float>();
        }

        return invertDepthAxis ? -raw : raw;
    }

    /// <summary>
    /// If depthAction is not assigned, use the Move action from the other controller
    /// (ray is usually on the right hand → left thumbstick for Z).
    /// </summary>
    private void ResolveDepthActionFallback()
    {
        if (depthAction != null) return;

        EngineInteractor onRay = null;
        if (rayInteractor != null)
        {
            onRay = rayInteractor.GetComponent<EngineInteractor>();
            if (onRay == null)
                onRay = rayInteractor.GetComponentInParent<EngineInteractor>();
        }

        foreach (var ei in FindObjectsByType<EngineInteractor>(FindObjectsSortMode.None))
        {
            if (ei == null || ei.moveAction == null || ei == onRay) continue;
            depthAction = ei.moveAction;
            return;
        }

        if (onRay != null && onRay.moveAction != null)
            depthAction = onRay.moveAction;
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
        RestoreLocomotion();

        if (_grabbed == null) return;

        _grabbed.OnGrabEnd();
        Debug.Log($"[EngineGrabManager] Released: {_grabbed.gameObject.name}");
        _grabbed = null;
    }

    private void SuppressLocomotion()
    {
        if (!disableLocomotionWhileGrabbing || _locomotionSuppressed) return;

        foreach (var move in FindObjectsByType<ActionBasedContinuousMoveProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = move,
                WasEnabled = move.enabled,
                SavedSpeed = move.moveSpeed
            });
            move.moveSpeed = 0f;
            move.enabled = false;
        }

        foreach (var turn in FindObjectsByType<ActionBasedContinuousTurnProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = turn,
                WasEnabled = turn.enabled,
                SavedSpeed = turn.turnSpeed
            });
            turn.turnSpeed = 0f;
            turn.enabled = false;
        }

        foreach (var snap in FindObjectsByType<ActionBasedSnapTurnProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = snap,
                WasEnabled = snap.enabled,
                SavedSpeed = 0f
            });
            snap.enabled = false;
        }

        _locomotionSuppressed = true;
    }

    private void RestoreLocomotion()
    {
        if (!_locomotionSuppressed) return;

        foreach (var backup in _locomotionBackups)
        {
            if (backup.Component == null) continue;

            switch (backup.Component)
            {
                case ActionBasedContinuousMoveProvider move:
                    move.moveSpeed = backup.SavedSpeed;
                    break;
                case ActionBasedContinuousTurnProvider turn:
                    turn.turnSpeed = backup.SavedSpeed;
                    break;
            }

            backup.Component.enabled = backup.WasEnabled;
        }

        _locomotionBackups.Clear();
        _locomotionSuppressed = false;
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
