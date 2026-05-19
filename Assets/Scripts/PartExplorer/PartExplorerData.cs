using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stores the list of parts to explore for a specific engine.
/// Create one per engine type and assign in the scene.
/// </summary>
[CreateAssetMenu(fileName = "New Part Explorer Data", menuName = "Engine VR/Part Explorer/Part Explorer Data")]
public class PartExplorerData : ScriptableObject
{
    [System.Serializable]
    public class ExplorerPart
    {
        [Tooltip("Name of the part (e.g., 'Cylinder Block')")]
        public string partName;
        
        [TextArea(2, 5)]
        [Tooltip("Description of what this part does")]
        public string partDescription;
        
        [Tooltip("The EnginePart component for this part")]
        public EnginePart enginePart;
    }

    [SerializeField] public List<ExplorerPart> parts = new List<ExplorerPart>();

    public int GetPartCount() => parts.Count;

    public ExplorerPart GetPart(int index)
    {
        if (index >= 0 && index < parts.Count)
            return parts[index];
        return null;
    }
}
