using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Configures the XRRayInteractor line appearance and distance.
/// - Makes the line thicker for distant objects
/// - Increases the raycast distance
/// - Applies dynamic line width based on distance
/// </summary>
public class RaycastLineConfigurator : MonoBehaviour
{
    [Header("Raycast Distance")]
    [Tooltip("Maximum distance the raycast can reach.")]
    [Min(1f)]
    public float maxRaycastDistance = 500f;

    [Header("Line Width")]
    [Tooltip("Base line width at the controller.")]
    [Range(0.001f, 0.05f)]
    public float baseLineWidth = 0.008f;

    [Tooltip("Maximum line width at the end of the ray.")]
    [Range(0.001f, 0.1f)]
    public float maxLineWidth = 0.02f;

    [Tooltip("How much the line width increases with distance (0 = constant width).")]
    [Range(0f, 1f)]
    public float lineWidthGrowth = 0.5f;

    private XRRayInteractor _rayInteractor;
    private XRInteractorLineVisual _lineVisual;

    void Start()
    {
        _rayInteractor = GetComponent<XRRayInteractor>();
        if (_rayInteractor == null)
        {
            Debug.LogError("[RaycastLineConfigurator] XRRayInteractor not found on this GameObject!");
            enabled = false;
            return;
        }

        _lineVisual = GetComponent<XRInteractorLineVisual>();
        if (_lineVisual == null)
        {
            Debug.LogWarning("[RaycastLineConfigurator] XRInteractorLineVisual not found. Line width won't be dynamic.");
        }

        // Set the max raycast distance
        _rayInteractor.maxRaycastDistance = maxRaycastDistance;
        Debug.Log($"[RaycastLineConfigurator] Set maxRaycastDistance to {maxRaycastDistance}m");
    }

    void Update()
    {
        if (_rayInteractor == null || _lineVisual == null) return;

        // Get current raycast hit distance
        if (_rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            float distance = hit.distance;
            UpdateLineWidth(distance);
        }
        else
        {
            // No hit — use max distance for line width calculation
            UpdateLineWidth(maxRaycastDistance);
        }
    }

    void UpdateLineWidth(float distance)
    {
        if (_lineVisual == null) return;

        // Calculate line width based on distance
        // Grows from baseLineWidth to maxLineWidth as distance increases
        float normalizedDistance = Mathf.Clamp01(distance / maxRaycastDistance);
        float dynamicWidth = Mathf.Lerp(baseLineWidth, maxLineWidth, normalizedDistance * lineWidthGrowth);

        _lineVisual.lineWidth = dynamicWidth;
    }

    /// <summary>Adjust raycast distance at runtime.</summary>
    public void SetMaxRaycastDistance(float distance)
    {
        maxRaycastDistance = Mathf.Max(1f, distance);
        if (_rayInteractor != null)
            _rayInteractor.maxRaycastDistance = maxRaycastDistance;
        Debug.Log($"[RaycastLineConfigurator] Updated maxRaycastDistance to {maxRaycastDistance}m");
    }

    /// <summary>Adjust line width growth at runtime.</summary>
    public void SetLineWidthGrowth(float growth)
    {
        lineWidthGrowth = Mathf.Clamp01(growth);
        Debug.Log($"[RaycastLineConfigurator] Updated lineWidthGrowth to {lineWidthGrowth}");
    }
}
