using UnityEngine;
using UnityEngine.Rendering;

public enum ExplodePosition { Auto, LeftTop, Left, Center, Right, RightTop }

public enum OutlineColorPreset
{
    Custom,
    Red,
    Orange,
    Yellow,
    LightGreen,
    Green,
    Cyan,
    Blue,
    Purple,
    White,
    Pink
}

public class EnginePart : MonoBehaviour
{
    [Header("Part Info")]
    [Tooltip("Assign a PartData asset to drive this part's name, description and audio. If set, overrides the fields below.")]
    public PartData partData;

    [Tooltip("Used only if partData is not assigned.")]
    public string partName = "Engine Part";
    [TextArea, Tooltip("Used only if partData is not assigned.")]
    public string description = "Part description here.";
    [Tooltip("Used only if partData is not assigned.")]
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
    [Range(0f, 1f)] public float ghostAlpha = 0.08f;
    [Range(0f, 1f)] public float ghostFadeDuration = 0.25f;

    [Header("X-Ray View")]
    public Color xrayColor = new Color(0f, 0.8f, 0.8f, 1f); // Teal blue
    [Range(0f, 1f)] public float xrayAlpha = 0.3f;
    [Range(0f, 4f)] public float xrayGlowIntensity = 1.0f;

    [Header("Outline")]
    public OutlineColorPreset outlineColorPreset = OutlineColorPreset.Custom;
    public Color outlineColor = Color.red;
    [Range(0.001f, 0.02f)] public float outlineWidth = 0.004f;

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
    private Coroutine _ghostCoroutine;

    // ── Data accessors (prefer PartData SO if assigned) ───────────────────────
    public string PartName        => partData != null ? partData.partName        : partName;
    public string Description     => partData != null ? partData.description     : description;
    public AudioClip AudioClip    => partData != null ? partData.audioExplanation : audioExplanation;

    // ── Resolves the active outline color (preset overrides custom) ───────────
    public Color ActiveOutlineColor
    {
        get
        {
            switch (outlineColorPreset)
            {
                case OutlineColorPreset.Red:        return new Color(1.00f, 0.15f, 0.15f);
                case OutlineColorPreset.Orange:     return new Color(1.00f, 0.50f, 0.05f);
                case OutlineColorPreset.Yellow:     return new Color(1.00f, 0.92f, 0.10f);
                case OutlineColorPreset.LightGreen: return new Color(0.50f, 1.00f, 0.30f);
                case OutlineColorPreset.Green:      return new Color(0.10f, 0.85f, 0.20f);
                case OutlineColorPreset.Cyan:       return new Color(0.00f, 0.90f, 1.00f);
                case OutlineColorPreset.Blue:       return new Color(0.15f, 0.40f, 1.00f);
                case OutlineColorPreset.Purple:     return new Color(0.70f, 0.20f, 1.00f);
                case OutlineColorPreset.White:      return new Color(0.95f, 0.95f, 0.95f);
                case OutlineColorPreset.Pink:       return new Color(1.00f, 0.40f, 0.70f);
                default:                            return outlineColor; // Custom
            }
        }
    }

    void Awake()
    {
        InitializePart();
    }

    void OnDestroy()
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_ghostCoroutine != null)   StopCoroutine(_ghostCoroutine);
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
            _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
            _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
            _outlineMat.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            Debug.LogError($"[EnginePart] '{gameObject.name}': Custom/Outline shader NOT found. " +
                           "Make sure Outline.shader is in Assets/Shaders/ and has compiled.");
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
            // Refresh outline mat properties in case Inspector values changed
            if (_outlineMat != null)
            {
                _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
                _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
            }
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

        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);

        if (ghostFadeDuration > 0.01f && gameObject.activeInHierarchy)
            _ghostCoroutine = StartCoroutine(FadeToGhost());
        else
            ApplyGhostImmediate();
    }

    private System.Collections.IEnumerator FadeToGhost()
    {
        // Switch blend mode immediately before animating alpha
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);
            ClearEmission(mat);
        }

        float elapsed = 0f;
        while (elapsed < ghostFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, ghostAlpha, elapsed / ghostFadeDuration);
            SetAlphaOnMaterials(alpha);
            yield return null;
        }

        SetAlphaOnMaterials(ghostAlpha);
        _ghostCoroutine = null;
    }

    private void ApplyGhostImmediate()
    {
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);
            ClearEmission(mat);
        }
        SetAlphaOnMaterials(ghostAlpha);
    }

    private void SetAlphaOnMaterials(float alpha)
    {
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColorFactor"))
            {
                var c = mat.GetColor("_BaseColorFactor");
                mat.SetColor("_BaseColorFactor", new Color(c.r, c.g, c.b, alpha));
            }
            else if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                mat.SetColor("_Color", new Color(c.r, c.g, c.b, alpha));
            }
            else
            {
                var c = mat.color;
                mat.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }

    private static void ClearEmission(Material mat)
    {
        if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor", Color.black);
        if (mat.HasProperty("_EmissionColor"))   { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }
        if (mat.HasProperty("_EmissiveColor"))   mat.SetColor("_EmissiveColor", Color.black);
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

        if (_ghostCoroutine != null) { StopCoroutine(_ghostCoroutine); _ghostCoroutine = null; }

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
        // Inactive GameObjects can't run coroutines — snap directly
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        {
            transform.localPosition = _explodedLocalPos;
            return;
        }
        _explodeCoroutine = StartCoroutine(AnimatePosition(_explodedLocalPos, duration));
    }

    public void AnimateToAssembled(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        // Inactive GameObjects can't run coroutines — snap directly
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        {
            transform.localPosition = _assembledLocalPos;
            return;
        }
        _explodeCoroutine = StartCoroutine(AnimatePosition(_assembledLocalPos, duration));
    }

    private System.Collections.IEnumerator AnimatePosition(Vector3 targetLocal, float duration)
    {
        Vector3 start = transform.localPosition;
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
    // The outline shader is a 2-pass stencil shader stored as a SEPARATE material
    // appended to the renderer's material array. This works on any mesh.
    void ShowOutline()
    {
        if (_outlineMat == null)
        {
            // Lazy re-init in case shader compiled after Awake
            var shader = Shader.Find("Custom/Outline");
            if (shader == null) { Debug.LogError($"[EnginePart] Outline shader still not found on '{gameObject.name}'"); return; }
            _outlineMat = new Material(shader);
            _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
            _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
            _outlineMat.hideFlags = HideFlags.HideAndDontSave;
        }

        if (_outlineActive) return;
        if (_renderers == null || _renderers.Length == 0) return;

        _outlineActive = true;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var current  = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            // Only append if not already there
            bool alreadyHas = false;
            foreach (var m in current)
                if (m != null && m.shader == _outlineMat.shader) { alreadyHas = true; break; }
            if (alreadyHas) continue;

            var extended = new Material[current.Length + 1];
            current.CopyTo(extended, 0);
            extended[extended.Length - 1] = _outlineMat;
            _renderers[i].materials = extended;
        }
    }

    void HideOutline()
    {
        if (!_outlineActive) return;
        if (_renderers == null || _renderers.Length == 0) return;

        _outlineActive = false;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var current = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            // Remove any material using the outline shader
            var trimmed = new System.Collections.Generic.List<Material>();
            foreach (var m in current)
                if (m == null || _outlineMat == null || m.shader != _outlineMat.shader)
                    trimmed.Add(m);

            _renderers[i].materials = trimmed.ToArray();

            // Keep _materials[i] in sync
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
