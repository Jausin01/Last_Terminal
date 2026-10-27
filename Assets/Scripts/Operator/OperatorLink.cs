using System;
using UnityEngine;

// Joins his room (Operator.unity, OperatorStation) and a location scene loaded over it (OperatorLocation: the CCTV,
// Helena, the commands): the station puts itself here; a location that wakes up finds it and hands itself over.
// One asset shared by both scenes (Assets/Data/OperatorLink.asset). Runtime only: nothing is saved in the asset.
[CreateAssetMenu(menuName = "Last Terminal/Operator Link", fileName = "OperatorLink")]
public class OperatorLink : ScriptableObject
{
    // The room's station and the location loaded over it (runtime only, never saved in the asset).
    [NonSerialized] private OperatorStation station;
    [NonSerialized] private OperatorLocation location;

    // Read by both sides.
    public OperatorStation Station => station;
    public OperatorLocation Location => location;

    // a location handed itself over (the station takes its camera program)
    public event Action<OperatorLocation> LocationArrived;

    // The station registers / unregisters itself.
    public void SetStation(OperatorStation value) => station = value;

    // Unregisters the station (only if it is the one registered).
    public void ClearStation(OperatorStation value)
    {
        if (station == value) station = null;
    }

    // A location registers itself (the station is told) / unregisters.
    public void SetLocation(OperatorLocation value)
    {
        location = value;
        LocationArrived?.Invoke(value);
    }

    // Unregisters the location (only if it is the one registered).
    public void ClearLocation(OperatorLocation value)
    {
        if (location == value) location = null;
    }
}
