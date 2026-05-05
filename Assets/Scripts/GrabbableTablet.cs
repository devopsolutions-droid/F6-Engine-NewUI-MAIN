using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabbableTablet : MonoBehaviour
{
    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;

        _grab = GetComponent<XRGrabInteractable>();
        _grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        _grab.throwOnDetach = true;

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // keep physics on so it falls naturally after throw
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }
}
