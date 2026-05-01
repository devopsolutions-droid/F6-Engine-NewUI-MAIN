using UnityEngine;
using UnityEngine.Rendering;

public enum ExplodePosition { Auto, LeftTop, Left, Center, Right, RightTop }

public class EnginePart : MonoBehaviour
{
    [Header("Part Info")]
    public string partName = "Engine Part";
    [TextArea] public string description = "Part description here.";
    public AudioClip audioExplanation;

    [Header("Hover Panel")]
    [Tooltip("Drag the pre-designed panel for THIS part here.")]
    public GameObject hoverPanel;

    [Header("Hover Highlight")]
    public Color highlightColor = new Color(1f, 0.6f, 0f, 1f);
    [Range(0f, 1f)] public float highlightIntensity = 0.4f;

    [Header("Selection Glow")]
    public Color glowColor = new Color(0f, 0.8f, 1f, 1f); // cyan-blue
    [Range(0f, 4f)] public float glowIntensity = 2.5f;

    [Header("Ghost")]
    [Range(0f, 1f)] public float ghostAlpha = 0.12f;

    [Header("X-Ray View")]
    public Color xrayColor = new Color(0f, 0.8f, 0.8f, 1f); // Teal blue
    [Range(0f, 1f)] public float xrayAlpha = 0.3f;
    [Range(0f, 4f)] public float xrayGlowIntensity = 1.0f;

    [Header("Outline")]
    public Color outlineColor = Color.red;
    [Range(0.001f, 0.1f)] public float outlineWidth = 0.05f;

    [Header("Exploded View")]
    [Tooltip("Preset position for organized explosion layout.")]
    public ExplodePosition explodePosition = ExplodePosition.Auto;
    [Tooltip("How far this part moves outward when exploded. Set to 0 to keep it in place.")]
    [Range(0f, 6f)] public float explodeDistance = 6f;
    [Tooltip("Optional: override the direction this part explodes. Leave at zero to use preset or auto-calculate from engine center.")]
    public Vector3 explodeDirectionOverride = Vector3.zero;

    private Renderer[] _renderers;
    private Material[] _materials;
    private Material[] _originalMaterials;
    private Material[] _originalMaterialsBackup;
    private int[]      _originalMatCounts;
    private Material   _outlineMat;
    private bool       _outlineActive;
    private bool       _isInitialized;

    // Explode state
    private Vector3 _assembledLocalPos;
    private Vector3 _explodedLocalPos;
    private Coroutine _explodeCoroutine;

    void Awake()
    {
        InitializePart();
    }

    void OnDestroy()
    {
        if (_explodeCoroutine != null)
            StopCoroutine(_explodeCoroutine);
    }

    /// <summary>Safely initialize all material caches and outline.</summary>
    private void InitializePart()
    {
        if (_isInitialized) return;

        _renderers          = GetComponentsInChildren<Renderer>();
        if (_renderers == null || _renderers.Length == 0)
        {
            Debug.LogWarning($"[EnginePart] {gameObject.name} has no renderers!");
            _isInitialized = true;
            return;
        }

        _materials          = new Material[_renderers.Length];
        _originalMaterials  = new Material[_renderers.Length];
        _originalMaterialsBackup = new Material[_renderers.Length];
        _originalMatCounts  = new int[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            _materials[i]         = _renderers[i].material;           // live instance
            _originalMaterials[i] = new Material(_materials[i]);      // deep copy
            _originalMaterialsBackup[i] = new Material(_materials[i]); // backup copy
            _originalMatCounts[i] = _renderers[i].sharedMaterials.Length;
        }

        // Build the shared outline material once per part
        var shader = Shader.Find("Custom/Outline");
        if (shader != null)
        {
            _outlineMat = new Material(shader);
            _outlineMat.SetColor("_OutlineColor", outlineColor);
            _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
        }
        else
        {
            Debug.LogWarning("[EnginePart] Custom/Outline shader not found — make sure Outline.shader is in the project.");
        }

        // Store assembled position for explode
        _assembledLocalPos = transform.localPosition;
        _isInitialized = true;
    }

    /// <summary>Call once after all parts are initialized to compute explode targets.</summary>
    public void ComputeExplodeTarget(Vector3 engineWorldCenter)
    {
        if (explodePosition == ExplodePosition.Center)
        {
            _explodedLocalPos = _assembledLocalPos;
            return;
        }

        Vector3 dir;
        if (explodeDirectionOverride.sqrMagnitude > 0.001f)
            dir = explodeDirectionOverride.normalized;
        else if (explodePosition != ExplodePosition.Auto)
        {
            switch (explodePosition)
            {
                case ExplodePosition.LeftTop: dir = new Vector3(-0.5f, 0.5f, 0).normalized; break;
                case ExplodePosition.Left: dir = Vector3.left; break;
                case ExplodePosition.Right: dir = Vector3.right; break;
                case ExplodePosition.RightTop: dir = new Vector3(0.5f, 0.5f, 0).normalized; break;
                default: dir = (transform.position - engineWorldCenter).normalized; break;
            }
        }
        else
        {
            dir = (transform.position - engineWorldCenter).normalized;
            // If part is exactly at center, push it upward
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
        }

        // Ensure Y direction is positive to prevent parts from going underground
        dir.y = Mathf.Abs(dir.y);

        // Convert world direction to local-space offset
        Vector3 worldOffset = dir * explodeDistance;
        Vector3 localOffset = transform.parent != null
            ? transform.parent.InverseTransformDirection(worldOffset)
            : worldOffset;

        _explodedLocalPos = _assembledLocalPos + localOffset;
    }

    // ── Hover ────────────────────────────────────────────────────────────────
    public void SetHighlight(bool on)
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        if (on)
        {
            foreach (var mat in _materials)
            {
                if (mat == null) continue;
                ApplyGlowToMat(mat, highlightColor, highlightIntensity);
            }
            ShowOutline();
        }
        else
        {
            foreach (var mat in _materials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor", Color.black);
                if (mat.HasProperty("_EmissionColor"))   { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }
                if (mat.HasProperty("_EmissiveColor"))   mat.SetColor("_EmissiveColor", Color.black);
            }
            HideOutline();
        }
    }

    // ── Selected: solid blue glow ─────────────────────────────────────────────
    public void SetGlowSelected()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetOpaque(mat);
            ApplyGlowToMat(mat, glowColor, glowIntensity);
        }
        ShowOutline();
    }

    private static void ApplyGlowToMat(Material mat, Color color, float intensity)
    {
        // glTF/PbrMetallicRoughness — base color tint
        if (mat.HasProperty("_BaseColorFactor"))
            mat.SetColor("_BaseColorFactor", new Color(color.r, color.g, color.b, 1f));

        // glTF emissive — this is the correct emission property for glTFast
        if (mat.HasProperty("_EmissiveFactor"))
            mat.SetColor("_EmissiveFactor", color * intensity);

        // fallbacks for Standard / URP
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
        }
        if (mat.HasProperty("_EmissiveColor"))
            mat.SetColor("_EmissiveColor", color * intensity);
    }

    // ── Others: ghost / semi-transparent ─────────────────────────────────────
    public void SetGhost()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        HideOutline();
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);

            if (mat.HasProperty("_BaseColorFactor"))
            {
                var c = mat.GetColor("_BaseColorFactor");
                mat.SetColor("_BaseColorFactor", new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, ghostAlpha));
            }
            else if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                mat.SetColor("_Color", new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, ghostAlpha));
            }
            else
            {
                var c = mat.color;
                mat.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, ghostAlpha);
            }

            if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor", Color.black);
            if (mat.HasProperty("_EmissionColor"))   mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
    }

    // ── X-Ray View ────────────────────────────────────────────────────────────
    public void SetXRayView()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        HideOutline();
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);

            Color baseColor = new Color(xrayColor.r, xrayColor.g, xrayColor.b, xrayAlpha);
            Color emissiveColor = xrayColor * xrayGlowIntensity;

            // Set base transparent color
            if (mat.HasProperty("_BaseColorFactor"))
                mat.SetColor("_BaseColorFactor", baseColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", baseColor);
            else
                mat.color = baseColor;

            // Apply Teal Emission
            if (mat.HasProperty("_EmissiveFactor"))
                mat.SetColor("_EmissiveFactor", emissiveColor);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissiveColor);
            }
            if (mat.HasProperty("_EmissiveColor"))
                mat.SetColor("_EmissiveColor", emissiveColor);
        }
    }

    // ── Restore original look ─────────────────────────────────────────────────
    public void RestoreOriginal()
    {
        if (!_isInitialized) InitializePart();

        _outlineActive = false;
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;

            _renderers[i].enabled = true;
            
            // Create a fresh copy from the backup to ensure clean state
            Material restoredMat = new Material(_originalMaterialsBackup[i]);
            _renderers[i].materials = new Material[] { restoredMat };
            _materials[i] = _renderers[i].materials[0];
        }
    }

    // ── Exploded View Animation ─────────────────────────────────────────────
    public void AnimateToExploded(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        _explodeCoroutine = StartCoroutine(AnimatePosition(_explodedLocalPos, duration));
    }

    public void AnimateToAssembled(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        _explodeCoroutine = StartCoroutine(AnimatePosition(_assembledLocalPos, duration));
    }

    private System.Collections.IEnumerator AnimatePosition(Vector3 targetLocal, float duration)
    {
        Vector3 start = transform.localPosition;
        
        // Snap instantly if duration is 0 or very small
        if (duration <= 0.01f)
        {
            transform.localPosition = targetLocal;
            _explodeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localPosition = Vector3.Lerp(start, targetLocal, t);
            yield return null;
        }

        transform.localPosition = targetLocal;
        _explodeCoroutine = null;
    }

    // ── Outline ───────────────────────────────────────────────────────────────
    void ShowOutline()
    {
        if (_outlineMat == null || _outlineActive) return;
        if (_renderers == null || _renderers.Length == 0) return;

        _outlineActive = true;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            var current  = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            var extended = new Material[current.Length + 1];
            current.CopyTo(extended, 0);
            extended[extended.Length - 1] = _outlineMat;
            _renderers[i].materials = extended;
        }
    }

    void HideOutline()
    {
        if (_outlineMat == null || !_outlineActive) return;
        if (_renderers == null || _renderers.Length == 0) return;

        _outlineActive = false;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            var current = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            int original = i < _originalMatCounts.Length ? _originalMatCounts[i] : current.Length;
            if (current.Length <= original) continue;

            var trimmed = new Material[original];
            System.Array.Copy(current, trimmed, original);
            _renderers[i].materials = trimmed;
            if (i < _materials.Length && _renderers[i].materials.Length > 0)
                _materials[i] = _renderers[i].materials[0];
        }
    }

    // ── Hover Panel ───────────────────────────────────────────────────────────
    public void ShowPanel() { if (hoverPanel != null) hoverPanel.SetActive(true); }
    public void HidePanel() { if (hoverPanel != null) hoverPanel.SetActive(false); }

    // ── Kept for compatibility ────────────────────────────────────────────────
    public void SetVisible(bool visible)
    {
        foreach (var r in _renderers)
            r.enabled = visible;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void SetOpaque(Material mat)
    {
        if (mat == null) return;

        mat.SetFloat("_Mode", 0);
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
        mat.SetInt("_DstBlend", (int)BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        
        // For glTF materials, also set _AlphaMode
        if (mat.HasProperty("_AlphaMode"))
            mat.SetFloat("_AlphaMode", 0);
        
        mat.renderQueue = -1;
    }

    private static void SetTransparent(Material mat)
    {
        if (mat == null) return;

        mat.SetFloat("_Mode", 2);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        
        // For glTF materials, also set _AlphaMode
        if (mat.HasProperty("_AlphaMode"))
            mat.SetFloat("_AlphaMode", 1);
        
        mat.renderQueue = 3000;
    }
}
