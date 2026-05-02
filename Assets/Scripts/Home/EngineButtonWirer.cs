using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Auto-wires every button in the scroll panel to its EngineData from the registry.
/// 
/// Setup (one-time, works for any number of models):
///   1. Add this component to any GameObject in the scene
///   2. Assign EngineRegistry and SessionData in Inspector
///   3. Assign the Content transform (parent of all buttons)
///   4. Each button must have a child TextMeshProUGUI for the label (auto-set)
///   5. To add a new model: create EngineData asset, add to EngineRegistry — done
/// </summary>
public class EngineButtonWirer : MonoBehaviour
{
    [Header("Data")]
    public EngineRegistry engineRegistry;
    public EngineSessionData sessionData;

    [Header("Scene")]
    [Tooltip("Name of the engine view scene in Build Settings.")]
    public string engineSceneName = "SampleScene";

    [Header("Scroll Panel")]
    [Tooltip("The Content transform that contains all the buttons.")]
    public Transform buttonContainer;

    [Header("Optional")]
    [Tooltip("If assigned, sets button thumbnail image. Button must have a child Image named 'Thumbnail'.")]
    public bool setThumbnails = true;

    void Start()
    {
        if (engineRegistry == null) { Debug.LogError("[EngineButtonWirer] EngineRegistry not assigned!"); return; }
        if (sessionData == null)    { Debug.LogError("[EngineButtonWirer] SessionData not assigned!"); return; }
        if (buttonContainer == null){ Debug.LogError("[EngineButtonWirer] ButtonContainer not assigned!"); return; }

        WireButtons();
    }

    void WireButtons()
    {
        // Collect all active buttons in the container
        var buttons = new List<Button>();
        foreach (Transform child in buttonContainer)
        {
            var btn = child.GetComponent<Button>();
            if (btn != null) buttons.Add(btn);
        }

        int engineCount = engineRegistry.Count;
        Debug.Log($"[EngineButtonWirer] Wiring {engineCount} engines to {buttons.Count} buttons.");

        for (int i = 0; i < buttons.Count; i++)
        {
            if (i >= engineCount)
            {
                // No engine for this button — hide it
                buttons[i].gameObject.SetActive(false);
                continue;
            }

            EngineData data = engineRegistry.Get(i);
            Button btn = buttons[i];

            // Set label
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = data.engineName;

            // Set thumbnail if available
            if (setThumbnails && data.thumbnail != null)
            {
                var img = btn.transform.Find("Thumbnail")?.GetComponent<Image>();
                if (img != null) img.sprite = data.thumbnail;
            }

            // Wire click — capture by value
            EngineData captured = data;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEngineSelected(captured));

            Debug.Log($"[EngineButtonWirer] Button[{i}] wired to '{data.engineName}'");
        }
    }

    void OnEngineSelected(EngineData data)
    {
        Debug.Log($"[EngineButtonWirer] Selected: {data.engineName}");
        sessionData.Select(data);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(engineSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(engineSceneName);
    }
}
