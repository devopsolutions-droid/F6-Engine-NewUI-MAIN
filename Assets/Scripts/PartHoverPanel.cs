using UnityEngine;

/// <summary>
/// Attach this to each engine part's dedicated hover panel (the one YOU design in Unity).
/// The panel starts hidden. EngineInteractor will call Show/Hide via EnginePart.
/// 
/// This script anchors the panel to follow a specific engine part:
/// - If partAnchor is assigned: panel follows that anchor point
/// - If partAnchor is NOT assigned: panel is parented to the engine part automatically
/// 
/// Line connects from this panel to the engine part anchor point.
/// It updates every LateUpdate so it stays correct even while the engine rotates/moves.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PartHoverPanel : MonoBehaviour
{
    [Header("Line Connection")]
    [Tooltip("Assign an empty child GameObject on the engine part as the line endpoint. If empty, will auto-find from parent.")]
    public Transform partAnchor;

    [Tooltip("Optional: a child Transform on this panel where the line starts. Leave empty to use panel center.")]
    public Transform panelAnchor;

    [Header("Panel Offset")]
    [Tooltip("Offset from the part anchor where the panel should appear (in world space).")]
    public Vector3 panelOffset = new Vector3(0.3f, 0.3f, 0f);

    [Header("Line Style")]
    public float lineWidth = 0.003f;
    public Color lineColor = new Color(1f, 0.65f, 0.3f, 1f);

    private LineRenderer _line;
    private EnginePart _enginePart;
    private bool _autoFollowEnabled = false;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth;
        _line.useWorldSpace = true;
        _line.startColor = lineColor;
        _line.endColor   = lineColor;
        _line.material   = new Material(Shader.Find("Sprites/Default"));

        // Try to find the engine part this panel belongs to
        _enginePart = GetComponentInParent<EnginePart>();
        
        // If partAnchor is not assigned, try to find it on the engine part
        if (partAnchor == null && _enginePart != null)
        {
            // Look for a child named "PanelAnchor" or similar
            partAnchor = _enginePart.transform.Find("PanelAnchor");
            if (partAnchor == null)
            {
                // Fallback: use the engine part's transform itself
                partAnchor = _enginePart.transform;
            }
        }

        // Hidden by default — activated only on hover
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_line == null) return;

        // If we have an engine part, follow it
        if (_enginePart != null && partAnchor != null)
        {
            // Update panel position to follow the part anchor + offset
            Vector3 targetPos = partAnchor.position + panelOffset;
            transform.position = targetPos;
        }

        // Line start = panel anchor (or panel center if not set)
        Vector3 start = (panelAnchor != null) ? panelAnchor.position : transform.position;

        // Line end = engine part anchor (updates every frame so rotation is handled)
        Vector3 end = (partAnchor != null) ? partAnchor.position : transform.position;

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
