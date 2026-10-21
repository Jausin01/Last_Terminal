using System;
using UnityEngine;

// What the operator sees once he sits at his PC (OperatorStation, Desktop mode): the simple desktop of the location
// scenes (OperatorDesktop) or the full fake OS of his own room (OperatorOS). The station only talks to this.
public abstract class DesktopScreen : MonoBehaviour
{
    // the camera program (CCTV.EXE), getting up, turning the PC off
    public event Action ProgramRequested;
    public event Action StandRequested;
    public event Action ShutdownRequested;

    public abstract bool IsOpen { get; }

    public abstract void SetOpen(bool value);

    // Esc: closes the top window; false = nothing to close (the station gets him up)
    public abstract bool CloseTopWindow();

    // a line on the taskbar for a while
    public virtual void SetStatus(string text, float seconds = 4f) { }

    // whether the camera program can open here (a location linked to the room)
    public virtual void SetProgramAvailable(bool available) { }

    protected void RaiseProgram() => ProgramRequested?.Invoke();
    protected void RaiseStand() => StandRequested?.Invoke();
    protected void RaiseShutdown() => ShutdownRequested?.Invoke();
}
