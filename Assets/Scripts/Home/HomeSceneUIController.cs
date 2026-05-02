using UnityEngine;
using UnityEngine.UI;

public class HomeSceneUIController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject startUI;
    public GameObject scrollCanvas;

    [Header("Buttons")]
    public Button startButton;
    public Button backButton;

    [Header("Scroll")]
    public ScrollNavigator scrollNavigator;

    // Set to true by EngineSceneLoader.GoHome() before loading Home Scene
    public static bool ReturnToScroll = false;

    void Start()
    {
        if (ReturnToScroll)
        {
            // Coming back from engine scene — show scroll directly
            ReturnToScroll = false;
            startUI.SetActive(false);
            scrollCanvas.SetActive(true);
            scrollNavigator?.InitNow();
        }
        else
        {
            startUI.SetActive(true);
            scrollCanvas.SetActive(false);
        }

        startButton.onClick.AddListener(OnStartButtonClicked);
        backButton?.onClick.AddListener(OnBackButtonClicked);
    }

    public void OnStartButtonClicked()
    {
        startUI.SetActive(false);
        scrollCanvas.SetActive(true);
        scrollNavigator?.InitNow();
    }

    public void OnBackButtonClicked()
    {
        scrollCanvas.SetActive(false);
        startUI.SetActive(true);
    }
}
