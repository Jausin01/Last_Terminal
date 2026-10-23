using System;
using UnityEngine;

// Joins his room (Operator.unity, OperatorStation) and a location scene loaded over it (OperatorLocation: the CCTV,
// Helena, the commands): the station puts itself here; a location that wakes up finds it and hands itself over.
// One asset shared by both scenes (Assets/Data/OperatorLink.asset). Runtime only: nothing is saved in the asset.
[CreateAssetMenu(menuName = "Last Terminal/Operator Link", fileName = "OperatorLink")]
public class OperatorLink : ScriptableObject
{
    [NonSerialized] private OperatorStation station;
    [NonSerialized] private OperatorLocation location;

    public OperatorStation Station => station;
    public OperatorLocation Location => location;

    // a location handed itself over (the station takes its camera program)
    public event Action<OperatorLocation> LocationArrived;

    public void SetStation(OperatorStation value) => station = value;

    public void ClearStation(OperatorStation value)
    {
        if (station == value) station = null;
    }

    public void SetLocation(OperatorLocation value)
    {
        location = value;
        LocationArrived?.Invoke(value);
    }

    public void ClearLocation(OperatorLocation value)
    {
        if (location == value) location = null;
    }
}
