using UnityEngine;

// His PC: used, he sits down and the camera program (operator mode) opens (OperatorStation).
public class OperatorPc : OperatorInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string StationField = nameof(station);

    // His station (switches to the PC view).
    [SerializeField] private OperatorStation station;

    // No arm animation: he sits down.
    public override HandMotion Motion => HandMotion.None;

    // Sits at the PC.
    public override void Interact(FirstPersonPlayer player)
    {
        if (station != null) station.SitAtPc();
    }
}
