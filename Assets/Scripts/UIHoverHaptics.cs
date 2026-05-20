using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Attach this component to any UI button or UI element on your Canvas.
/// When hovered by an XR Ray Interactor, it will trigger a subtle haptic feedback vibration.
/// </summary>
public class UIHoverHaptics : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Hover Haptics Configuration")]
    [Range(0f, 1f)] public float hoverAmplitude = 0.15f;
    public float hoverDuration = 0.04f;

    [Header("Click Haptics Configuration")]
    [Range(0f, 1f)] public float clickAmplitude = 0.35f;
    public float clickDuration = 0.08f;

    /// <summary>
    /// Triggers haptics when the pointer enters the UI element (Hover).
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        TriggerHaptic(eventData, hoverAmplitude, hoverDuration);
    }

    /// <summary>
    /// Triggers haptics when the pointer clicks the UI element.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerHaptic(eventData, clickAmplitude, clickDuration);
    }

    private void TriggerHaptic(PointerEventData eventData, float amplitude, float duration)
    {
        if (eventData == null) return;

        // 1. Identify if the active input module is the XR Input Module
        if (EventSystem.current != null && EventSystem.current.currentInputModule is XRUIInputModule xrModule)
        {
            // 2. Fetch the specific interactor (controller hand) that performed the hover/click
            var interactor = xrModule.GetInteractor(eventData.pointerId);

            // 3. Send haptic impulse if it is an XR controller interactor
            if (interactor is XRBaseControllerInteractor controllerInteractor &&
                controllerInteractor.xrController != null)
            {
                controllerInteractor.xrController.SendHapticImpulse(amplitude, duration);
            }
        }
    }
}
