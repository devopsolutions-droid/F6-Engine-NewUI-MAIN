using UnityEngine;
using UnityEditor;

public class FixLocomotionSetup
{
    [MenuItem("Tools/Fix Locomotion - Snap Turn 90")]
    static void Fix()
    {
        var locomotionGO = GameObject.Find("Locomotion");
        if (locomotionGO == null)
        {
            Debug.LogError("[FixLocomotion] Could not find 'Locomotion' GameObject.");
            return;
        }

        Debug.Log("[FixLocomotion] Found: " + locomotionGO.name);

        var components = locomotionGO.GetComponents<Component>();
        foreach (var c in components)
            Debug.Log("[FixLocomotion] Component: " + c.GetType().FullName);

        // also check children
        foreach (Transform child in locomotionGO.transform)
        {
            Debug.Log("[FixLocomotion] Child: " + child.name);
            foreach (var c in child.GetComponents<Component>())
                Debug.Log("   -> " + c.GetType().FullName);
        }
    }
}
