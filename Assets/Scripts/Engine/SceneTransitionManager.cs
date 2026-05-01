using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles smooth fade transitions between scenes.
/// Attach to a persistent GameObject in each scene.
/// Uses a full-screen quad parented to the camera for VR compatibility.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade Settings")]
    [Range(0.1f, 2f)] public float fadeDuration = 0.5f;
    public Color fadeColor = Color.black;

    private GameObject _fadeQuad;
    private Material _fadeMat;
    private bool _isFading;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeQuad();
    }

    void BuildFadeQuad()
    {
        _fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fadeQuad.name = "[FadeQuad]";
        Destroy(_fadeQuad.GetComponent<MeshCollider>());

        _fadeMat = new Material(Shader.Find("Unlit/Color")) { color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f) };
        // Enable transparency
        _fadeMat.SetFloat("_Mode", 2);
        _fadeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _fadeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _fadeMat.SetInt("_ZWrite", 0);
        _fadeMat.EnableKeyword("_ALPHABLEND_ON");
        _fadeMat.renderQueue = 5000;

        _fadeQuad.GetComponent<Renderer>().material = _fadeMat;
        _fadeQuad.SetActive(false);
    }

    /// <summary>Load a scene by name with a fade transition.</summary>
    public void LoadScene(string sceneName)
    {
        if (_isFading) return;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        _isFading = true;

        // Attach quad to camera
        var cam = Camera.main;
        if (cam != null)
        {
            _fadeQuad.transform.SetParent(cam.transform, false);
            _fadeQuad.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.01f);
            _fadeQuad.transform.localRotation = Quaternion.identity;
            _fadeQuad.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        }
        _fadeQuad.SetActive(true);

        // Fade out
        yield return Fade(0f, 1f);

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        yield return null; // wait one frame for scene to activate

        // Re-attach to new camera
        cam = Camera.main;
        if (cam != null)
        {
            _fadeQuad.transform.SetParent(cam.transform, false);
            _fadeQuad.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.01f);
            _fadeQuad.transform.localRotation = Quaternion.identity;
            _fadeQuad.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        }

        // Fade in
        yield return Fade(1f, 0f);

        _fadeQuad.SetActive(false);
        _isFading = false;
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        var c = _fadeMat.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
            _fadeMat.color = c;
            yield return null;
        }
        c.a = to;
        _fadeMat.color = c;
    }
}
