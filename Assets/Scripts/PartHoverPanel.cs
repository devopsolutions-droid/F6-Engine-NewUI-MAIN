using UnityEngine;

/// <summary>
/// Attach this to each engine part's dedicated hover panel (the one YOU design in Unity).
/// The panel starts hidden. EngineInteractor will call Show/Hide via EnginePart.
/// 
/// Line connects from this panel to a specific anchor point on the engine part.
/// It updates every LateUpdate so it stays correct even while the engine rotates.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PartHoverPanel : MonoBehaviour
{
    [Header("Line Connection")]
    [Tooltip("Assign an empty child GameObject on the engine part as the line endpoint.")]
    public Transform partAnchor;

    [Tooltip("Optional: a child Transform on this panel where the line starts. Leave empty to use panel center.")]
    public Transform panelAnchor;

    [Header("Line Style")]
    public float lineWidth = 0.003f;
    public Color lineColor = new Color(1f, 0.65f, 0.3f, 1f);

    private LineRenderer _line;

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

        // Hidden by default — activated only on hover
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_line == null) return;

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
