using UnityEngine;

/// <summary>
/// Applies a background image as the scene skybox.
/// 
/// Mode A — Panoramic (equirectangular 360° image):
///   Set mode to Panoramic and assign your 2:1 ratio texture.
///   Gives full immersive 360° VR background.
///
/// Mode B — Sphere Backdrop (any flat/regular image):
///   Set mode to SphereBackdrop. A large inverted sphere is created around
///   the player with the image mapped onto it.
/// </summary>
public class SkyboxSetup : MonoBehaviour
{
    public enum BackgroundMode { Panoramic, SphereBackdrop, SolidColor }

    [Header("Mode")]
    public BackgroundMode mode = BackgroundMode.Panoramic;

    [Header("Panoramic / Sphere Backdrop")]
    [Tooltip("Your background image texture. For Panoramic: must be equirectangular (2:1 ratio). For SphereBackdrop: any image works.")]
    public Texture2D backgroundTexture;

    [Header("Solid Color (fallback)")]
    public Color solidColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Sphere Backdrop Settings")]
    [Tooltip("Radius of the backdrop sphere. Should be larger than your scene.")]
    public float sphereRadius = 50f;

    void Start()
    {
        switch (mode)
        {
            case BackgroundMode.Panoramic:
                ApplyPanoramicSkybox();
                break;
            case BackgroundMode.SphereBackdrop:
                ApplySphereBackdrop();
                break;
            case BackgroundMode.SolidColor:
                ApplySolidColor();
                break;
        }
    }

    void ApplyPanoramicSkybox()
    {
        if (backgroundTexture == null)
        {
            Debug.LogWarning("[SkyboxSetup] No texture assigned for Panoramic mode. Falling back to solid color.");
            ApplySolidColor();
            return;
        }

        var mat = new Material(Shader.Find("Skybox/Panoramic"));
        mat.SetTexture("_MainTex", backgroundTexture);
        mat.SetFloat("_Exposure", 1f);
        mat.SetFloat("_Rotation", 0f);
        RenderSettings.skybox = mat;
        DynamicGI.UpdateEnvironment();
    }

    void ApplySphereBackdrop()
    {
        if (backgroundTexture == null)
        {
            Debug.LogWarning("[SkyboxSetup] No texture assigned for SphereBackdrop mode. Falling back to solid color.");
            ApplySolidColor();
            return;
        }

        // Create inverted sphere
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "[BackgroundSphere]";
        sphere.transform.position = Vector3.zero;
        sphere.transform.localScale = Vector3.one * sphereRadius;
        Destroy(sphere.GetComponent<SphereCollider>());

        var mat = new Material(Shader.Find("Unlit/Texture"));
        mat.SetTexture("_MainTex", backgroundTexture);
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front); // render inside

        sphere.GetComponent<Renderer>().material = mat;

        // Solid color skybox so the sphere is the only background
        ApplySolidColor();
    }

    void ApplySolidColor()
    {
        var mat = new Material(Shader.Find("Skybox/Panoramic"));
        // Use a 1x1 solid color texture
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, solidColor);
        tex.Apply();
        mat.SetTexture("_MainTex", tex);
        RenderSettings.skybox = mat;
        DynamicGI.UpdateEnvironment();
    }
}
