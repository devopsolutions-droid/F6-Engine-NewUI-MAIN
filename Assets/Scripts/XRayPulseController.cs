using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to the same GameObject as EngineViewManager (or any persistent object).
/// When X-Ray is active this script feeds each part's world-space Y bounds into
/// the XRay shader every frame so the scanline sweeps correctly across the engine.
/// It is completely dormant when X-Ray is off — zero overhead.
/// </summary>
public class XRayPulseController : MonoBehaviour
{
    // Cached list of (material, renderer) pairs built when X-Ray activates
    private readonly List<(Material mat, Renderer rend)> _xrayPairs = new();
    private bool _isActive;

    // Shader property IDs — cached for performance
    private static readonly int PropMinY = Shader.PropertyToID("_ObjectMinY");
    private static readonly int PropMaxY = Shader.PropertyToID("_ObjectMaxY");

    /// <summary>Call this right after EngineViewManager.ActivateXRayView() finishes.</summary>
    public void Activate(EnginePart[] parts)
    {
        _xrayPairs.Clear();

        foreach (var part in parts)
        {
            if (part == null) continue;
            foreach (var rend in part.GetComponentsInChildren<Renderer>())
            {
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                {
                    if (mat != null && mat.shader != null &&
                        mat.shader.name == "Custom/XRay")
                    {
                        _xrayPairs.Add((mat, rend));
                    }
                }
            }
        }

        _isActive = _xrayPairs.Count > 0;
    }

    /// <summary>Call this when X-Ray is deactivated.</summary>
    public void Deactivate()
    {
        _xrayPairs.Clear();
        _isActive = false;
    }

    void Update()
    {
        if (!_isActive) return;

        for (int i = _xrayPairs.Count - 1; i >= 0; i--)
        {
            var (mat, rend) = _xrayPairs[i];

            // Renderer may have been destroyed (scene reload etc.)
            if (mat == null || rend == null)
            {
                _xrayPairs.RemoveAt(i);
                continue;
            }

            Bounds b = rend.bounds;
            mat.SetFloat(PropMinY, b.min.y);
            mat.SetFloat(PropMaxY, b.max.y);
        }

        if (_xrayPairs.Count == 0) _isActive = false;
    }
}
