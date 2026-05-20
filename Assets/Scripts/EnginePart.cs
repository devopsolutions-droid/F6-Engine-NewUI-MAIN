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
    [Tooltip("Emissive tint applied to the mesh on hover. Set intensity to 0 to show outline-only (recommended).")]
    public Color highlightColor = new Color(1f, 0.6f, 0f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Set to 0 for a clean outline-only hover with no colour tint on the mesh.")]
    public float highlightIntensity = 0f;

    [Header("Selection Glow")]
    public Color glowColor = new Color(0f, 0.8f, 1f, 1f);
    [Range(0f, 4f)] public float glowIntensity = 2.5f;

    [Header("Ghost")]
    [Range(0f, 1f)] public float ghostAlpha = 0.08f;
    [Range(0f, 1f)] public float ghostFadeDuration = 0.25f;

    [Header("X-Ray View")]
    [Tooltip("Color of the X-Ray effect. Bright cyan gives the best sci-fi CT-scan look.")]
    public Color xrayColor = new Color(0f, 0.95f, 1f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Base transparency of face centres. Keep very low (0.04-0.08) so you can see through the mesh. Fresnel rim handles edge brightness automatically.")]
    public float xrayAlpha = 0.05f;
    [Range(0f, 1f)]
    [Tooltip("How bright the silhouette rim glows. 0.55 gives a clean CT-scan edge.")]
    public float xrayRimAlpha = 0.55f;
    [Range(0f, 3f)]
    [Tooltip("Inner glow intensity. 0.6 gives a professional medical-scan look.")]
    public float xrayGlowIntensity = 0.6f;
    [Range(1f, 10f)]
    [Tooltip("Outline width in X-Ray mode. Slightly thicker than normal for visibility through geometry.")]
    public float xrayOutlineWidth = 5f;

    [Header("Outline")]
    [Tooltip("Outline color preset for hover/select. Cyan matches the X-Ray theme.")]
    public OutlineColorPreset outlineColorPreset = OutlineColorPreset.Cyan;
    public Color outlineColor = new Color(0f, 0.95f, 1f, 1f);
    [Range(1f, 10f)]
    [Tooltip("Pixel width of the outline. 3-5 px looks solid and clean at typical VR viewing distance.")]
    public float outlineWidth = 2.5f;

    [Header("Exploded View")]
    public ExplodePosition explodePosition = ExplodePosition.Auto;
    [Range(0f, 6f)] public float explodeDistance = 6f;
    public Vector3 explodeDirectionOverride = Vector3.zero;
    [Tooltip("Width of the middle zone (in meters). Parts within this distance from the center line will explode upwards.")]
    public float midZoneThreshold = 0.18f;

    private Renderer[]  _renderers;
    private Material[]  _materials;
    private Material[]  _originalMaterialsBackup;
    private Material    _outlineMat;
    private bool        _outlineActive;
    private bool        _isInitialized;
    private Coroutine   _liftCoroutine;
    private bool        _isSelected;

    private Vector3   _assembledLocalPos;
    private Vector3   _explodedLocalPos;
    private Coroutine _explodeCoroutine;
    private Coroutine _ghostCoroutine;

    // ── Data accessors ────────────────────────────────────────────────────────
    public string    PartName  => partData != null ? partData.partName        : partName;
    public string    Description => partData != null ? partData.description   : description;
    public AudioClip AudioClip => partData != null ? partData.audioExplanation : audioExplanation;

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
                default:                            return outlineColor;
            }
        }
    }

    void Awake()   => InitializePart();

    void OnDestroy()
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_ghostCoroutine   != null) StopCoroutine(_ghostCoroutine);
    }

    private void InitializePart()
    {
        if (_isInitialized) return;

        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers == null || _renderers.Length == 0)
        {
            Debug.LogWarning($"[EnginePart] {gameObject.name} has no renderers!");
            _isInitialized = true;
            return;
        }

        _materials              = new Material[_renderers.Length];
        _originalMaterialsBackup = new Material[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _materials[i]               = _renderers[i].material;
            _originalMaterialsBackup[i] = new Material(_materials[i]);
        }

        var outlineShader = Shader.Find("Custom/Outline");
        if (outlineShader != null)
        {
            _outlineMat = new Material(outlineShader);
            _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
            _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
            _outlineMat.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            Debug.LogError($"[EnginePart] '{gameObject.name}': Custom/Outline shader NOT found.");
        }

        _assembledLocalPos = transform.localPosition;
        _isInitialized = true;
    }

    // ── Explode target computation ────────────────────────────────────────────
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
                case ExplodePosition.LeftTop:  dir = new Vector3(-0.5f, 0.5f, 0).normalized; break;
                case ExplodePosition.Left:     dir = Vector3.left;  break;
                case ExplodePosition.Right:    dir = Vector3.right; break;
                case ExplodePosition.RightTop: dir = new Vector3(0.5f, 0.5f, 0).normalized;  break;
                default:                       dir = (transform.position - engineWorldCenter).normalized; break;
            }
        }
        else
        {
            Vector3 relativePos = transform.position - engineWorldCenter;
            float   dx          = relativePos.x;
            dir = Mathf.Abs(dx) < midZoneThreshold
                ? new Vector3(dx * 0.3f, 1.0f, relativePos.z * 0.2f).normalized
                : relativePos.normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
        }

        dir.y = Mathf.Abs(dir.y);

        Vector3 worldOffset = dir * explodeDistance;
        Vector3 localOffset = transform.parent != null
            ? transform.parent.InverseTransformDirection(worldOffset)
            : worldOffset;

        _explodedLocalPos = _assembledLocalPos + localOffset;
    }

    public void SetExplodeWorldTarget(Vector3 worldPosition)
    {
        if (!_isInitialized) InitializePart();
        _explodedLocalPos = transform.parent != null
            ? transform.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    public void SetExplodeLocalTarget(Vector3 localPosition)
    {
        if (!_isInitialized) InitializePart();
        _explodedLocalPos = localPosition;
    }

    // ── Hover highlight ───────────────────────────────────────────────────────
    public void SetHighlight(bool on)
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        if (on)
        {
            if (_outlineMat != null)
            {
                _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
                _outlineMat.SetFloat("_OutlineWidth",  outlineWidth);
            }
            if (highlightIntensity > 0f)
                foreach (var mat in _materials)
                    if (mat != null) ApplyGlowToMat(mat, highlightColor, highlightIntensity);

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

            if (_isSelected)
            {
                if (_outlineMat != null)
                {
                    _outlineMat.SetColor("_OutlineColor", ActiveOutlineColor);
                    _outlineMat.SetFloat("_OutlineWidth", outlineWidth);
                }
                ShowOutline();
            }
            else if (EngineViewManager.IsXRayActive)
            {
                // In X-Ray mode keep the cyan outline visible on hover-off
                if (_outlineMat != null)
                {
                    _outlineMat.SetColor("_OutlineColor", xrayColor);
                    _outlineMat.SetFloat("_OutlineWidth", xrayOutlineWidth);
                }
                ShowOutline();
            }
            else
            {
                HideOutline();
            }
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────
    public void SetSelected()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        _isSelected    = true;
        _outlineActive = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;
            _renderers[i].enabled   = true;
            _renderers[i].materials = new Material[] { new Material(_originalMaterialsBackup[i]) };
            _materials[i]           = _renderers[i].materials[0];
        }
        ShowOutline();
    }

    // ── Ghost ─────────────────────────────────────────────────────────────────
    public void SetGhost()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        _isSelected = false;
        HideOutline();

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var mats = _renderers[i].materials;
            if (mats != null && mats.Length > 0) _materials[i] = mats[0];
        }

        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);

        if (ghostFadeDuration > 0.01f && gameObject.activeInHierarchy)
            _ghostCoroutine = StartCoroutine(FadeToGhost());
        else
            ApplyGhostImmediate();
    }

    private System.Collections.IEnumerator FadeToGhost()
    {
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
            SetAlphaOnMaterials(Mathf.Lerp(1f, ghostAlpha, elapsed / ghostFadeDuration));
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

    // ── X-Ray View ────────────────────────────────────────────────────────────
    // Uses the dedicated Custom/XRay shader which renders through geometry (ZTest Always),
    // applies Fresnel rim glow, animated scanline sweep, and pulse breathing.
    // XRayPulseController feeds per-renderer Y bounds every frame for the scanline.
    public void SetXRayView()
    {
        if (!_isInitialized) InitializePart();
        if (_renderers == null || _renderers.Length == 0) return;

        _isSelected = false;

        var xrayShader = Shader.Find("Custom/XRay");
        if (xrayShader == null)
        {
            Debug.LogError("[EnginePart] Custom/XRay shader not found. Make sure XRay.shader is in Assets/Shaders/ and has compiled.");
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            // Compute world-space Y bounds for this renderer so the scanline
            // sweeps correctly from top to bottom of this specific mesh
            Bounds  b    = _renderers[i].bounds;
            float   minY = b.min.y;
            float   maxY = b.max.y;

            var xrayMat = new Material(xrayShader);
            xrayMat.SetColor("_XRayColor",      xrayColor);
            xrayMat.SetFloat("_Alpha",          xrayAlpha);
            xrayMat.SetFloat("_RimAlpha",       xrayRimAlpha);
            xrayMat.SetFloat("_GlowIntensity",  xrayGlowIntensity);
            xrayMat.SetFloat("_ObjectMinY",     minY);
            xrayMat.SetFloat("_ObjectMaxY",     maxY);
            xrayMat.hideFlags = HideFlags.HideAndDontSave;

            _renderers[i].materials = new Material[] { xrayMat };
            _materials[i]           = xrayMat;
        }

        // Bright cyan outline — wider than normal so it reads clearly through geometry
        if (_outlineMat != null)
        {
            _outlineMat.SetColor("_OutlineColor", xrayColor);
            _outlineMat.SetFloat("_OutlineWidth", xrayOutlineWidth);
        }
        ShowOutline();
    }

    // ── Restore ───────────────────────────────────────────────────────────────
    public void RestoreOriginal()
    {
        if (!_isInitialized) InitializePart();

        _isSelected = false;
        if (_ghostCoroutine != null) { StopCoroutine(_ghostCoroutine); _ghostCoroutine = null; }

        _outlineActive = false;
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;
            _renderers[i].enabled   = true;
            _renderers[i].materials = new Material[] { new Material(_originalMaterialsBackup[i]) };
            _materials[i]           = _renderers[i].materials[0];
        }
    }

    // ── Explode animations ────────────────────────────────────────────────────
    public void AnimateToExploded(float duration)
    {
        if (_liftCoroutine    != null) { StopCoroutine(_liftCoroutine);    _liftCoroutine    = null; }
        if (_explodeCoroutine != null)   StopCoroutine(_explodeCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f) { transform.localPosition = _explodedLocalPos; return; }
        _explodeCoroutine = StartCoroutine(AnimatePosition(_explodedLocalPos, duration));
    }

    public void AnimateToAssembled(float duration)
    {
        if (_liftCoroutine    != null) { StopCoroutine(_liftCoroutine);    _liftCoroutine    = null; }
        if (_explodeCoroutine != null)   StopCoroutine(_explodeCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f) { transform.localPosition = _assembledLocalPos; return; }
        _explodeCoroutine = StartCoroutine(AnimatePosition(_assembledLocalPos, duration));
    }

    private System.Collections.IEnumerator AnimatePosition(Vector3 targetLocal, float duration)
    {
        Vector3 start   = transform.localPosition;
        float   elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, targetLocal, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        transform.localPosition = targetLocal;
        _explodeCoroutine = null;
    }

    // ── Show Working lift animations ──────────────────────────────────────────
    public void LiftUp(float amount, float duration)
    {
        if (!_isInitialized) InitializePart();
        if (_liftCoroutine    != null) StopCoroutine(_liftCoroutine);
        if (_explodeCoroutine != null) { StopCoroutine(_explodeCoroutine); _explodeCoroutine = null; }

        Vector3 targetLocal = _assembledLocalPos + new Vector3(0f, amount, 0f);
        if (!gameObject.activeInHierarchy || duration <= 0.01f) { transform.localPosition = targetLocal; return; }
        _liftCoroutine = StartCoroutine(AnimateLift(targetLocal, duration));
    }

    public void LowerDown(float duration)
    {
        if (!_isInitialized) InitializePart();
        if (_liftCoroutine    != null) StopCoroutine(_liftCoroutine);
        if (_explodeCoroutine != null) { StopCoroutine(_explodeCoroutine); _explodeCoroutine = null; }

        if (!gameObject.activeInHierarchy || duration <= 0.01f) { transform.localPosition = _assembledLocalPos; return; }
        _liftCoroutine = StartCoroutine(AnimateLift(_assembledLocalPos, duration));
    }

    private System.Collections.IEnumerator AnimateLift(Vector3 targetLocal, float duration)
    {
        Vector3 start   = transform.localPosition;
        float   elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, targetLocal, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        transform.localPosition = targetLocal;
        _liftCoroutine = null;
    }

    // ── Outline ───────────────────────────────────────────────────────────────
    void ShowOutline()
    {
        if (_outlineMat == null)
        {
            var shader = Shader.Find("Custom/Outline");
            if (shader == null) { Debug.LogError($"[EnginePart] Outline shader not found on '{gameObject.name}'"); return; }
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
            var current = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

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

            var trimmed = new System.Collections.Generic.List<Material>();
            foreach (var m in current)
                if (m == null || _outlineMat == null || m.shader != _outlineMat.shader)
                    trimmed.Add(m);

            _renderers[i].materials = trimmed.ToArray();

            if (i < _materials.Length && _renderers[i].materials.Length > 0)
                _materials[i] = _renderers[i].materials[0];
        }
    }

    // ── Hover Panel ───────────────────────────────────────────────────────────
    public void ShowPanel() { if (hoverPanel != null) hoverPanel.SetActive(true);  }
    public void HidePanel() { if (hoverPanel != null) hoverPanel.SetActive(false); }

    public void SetVisible(bool visible)
    {
        foreach (var r in _renderers) r.enabled = visible;
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private static void ApplyGlowToMat(Material mat, Color color, float intensity)
    {
        if (mat.HasProperty("_BaseColorFactor"))
            mat.SetColor("_BaseColorFactor", new Color(color.r, color.g, color.b, 1f));
        if (mat.HasProperty("_EmissiveFactor"))
            mat.SetColor("_EmissiveFactor", color * intensity);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
        }
        if (mat.HasProperty("_EmissiveColor"))
            mat.SetColor("_EmissiveColor", color * intensity);
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
            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
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

    private static void SetTransparent(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_AlphaMode"))
        {
            mat.SetFloat("_AlphaMode", 1);
            mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.SetInt("_AlphaClip", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            return;
        }

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return;
        }

        mat.SetFloat("_Mode",    2);
        mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",    0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
