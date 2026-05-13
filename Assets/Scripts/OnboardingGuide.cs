using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Plays a 3-step onboarding sequence when the scene loads:
///   Step 1 — Welcome message  (TMP + Audio)
///   Step 2 — Grab Tablet hint (TMP + Audio)
///   Step 3 — Thank You        (TMP + Audio)
///
/// Each step waits for its audio clip to finish before moving to the next.
/// Hall reverb is handled globally by HallReverbManager — no reverb code here.
/// Wire everything in the Inspector, then call nothing — it runs automatically on Start.
/// </summary>
public class OnboardingGuide : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The root panel GameObject. Will be shown at start and hidden after the last step.")]
    public GameObject panelRoot;

    [Header("Step 1 — Welcome")]
    public TextMeshProUGUI welcomeText;
    public AudioClip       welcomeClip;

    [Header("Step 2 — Grab Tablet")]
    public TextMeshProUGUI grabTabletText;
    public AudioClip       grabTabletClip;

    [Header("Step 3 — Thank You")]
    public TextMeshProUGUI thankYouText;
    public AudioClip       thankYouClip;

    [Header("Timing")]
    [Tooltip("Seconds to wait before the sequence begins (gives the scene time to fully load).")]
    public float startDelay = 1f;

    [Tooltip("Seconds to wait between each step (after audio finishes, before next step starts).")]
    public float stepGapDuration = 0.5f;

    [Tooltip("Seconds to keep the last step visible before fading out.")]
    public float endHoldDuration = 1.5f;

    [Tooltip("Duration of the fade-out animation in seconds.")]
    public float fadeOutDuration = 1f;

    private AudioSource _audioSource;
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        // ── AudioSource ───────────────────────────────────────────────────────
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake  = false;
        _audioSource.spatialBlend = 0f; // 2D — UI audio

        // ── CanvasGroup for panel fade ────────────────────────────────────────
        if (panelRoot != null)
        {
            _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        // Hide all text labels at the start
        SetAllTextsVisible(false);

        // Show the panel fully opaque
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // Wait for scene to settle
        yield return new WaitForSeconds(startDelay);

        // ── Step 1: Welcome ───────────────────────────────────────────────────
        yield return PlayStep(welcomeText, welcomeClip);
        yield return new WaitForSeconds(stepGapDuration);

        // ── Step 2: Grab Tablet ───────────────────────────────────────────────
        yield return PlayStep(grabTabletText, grabTabletClip);
        yield return new WaitForSeconds(stepGapDuration);

        // ── Step 3: Thank You ─────────────────────────────────────────────────
        yield return PlayStep(thankYouText, thankYouClip);

        // Hold the last step briefly, then fade out
        yield return new WaitForSeconds(endHoldDuration);

        // Fade out the entire panel (text + background + everything)
        yield return FadeOutPanel();

        // Hide the panel after fade completes
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Shows the given TMP label, plays the audio clip, waits for it to finish.
    /// </summary>
    IEnumerator PlayStep(TextMeshProUGUI label, AudioClip clip)
    {
        // Hide all, then show only this step's label
        SetAllTextsVisible(false);

        if (label != null)
            label.gameObject.SetActive(true);

        if (clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            // No clip assigned — hold the text for 2 seconds as fallback
            yield return new WaitForSeconds(2f);
        }
    }

    void SetAllTextsVisible(bool visible)
    {
        if (welcomeText    != null) welcomeText.gameObject.SetActive(visible);
        if (grabTabletText != null) grabTabletText.gameObject.SetActive(visible);
        if (thankYouText   != null) thankYouText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Fades the entire panel (via CanvasGroup) from alpha 1 to 0.
    /// </summary>
    IEnumerator FadeOutPanel()
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        _canvasGroup.alpha = 1f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
