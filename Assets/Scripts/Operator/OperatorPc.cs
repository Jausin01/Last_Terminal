using UnityEngine;

// His PC: used, he sits down and the camera program (operator mode) opens (OperatorStation).
public class OperatorPc : OperatorInteractable
{
    public const string StationField = nameof(station);

    [SerializeField] private OperatorStation station;

    public override HandMotion Motion => HandMotion.None;

    public override void Interact(FirstPersonPlayer player)
    {
        if (station != null) station.SitAtPc();
    }
}
