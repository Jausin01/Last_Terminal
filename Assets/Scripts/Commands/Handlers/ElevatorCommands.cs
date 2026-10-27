using System;
using UnityEngine;

// Elevators: "take the elevator (to the casino)", "go to the elevator", "the red elevator".
internal sealed class ElevatorCommands : CommandHandler
{
    public ElevatorCommands(CommandContext ctx) : base(ctx) { }

    // Is the command about an elevator (the word, or a destination of one)?
    internal bool IsElevatorCommand(ParsedCommand cmd) =>
        cmd.HasKey(CommandTargets.ElevatorKey) || cmd.Keys.Exists(k => k.StartsWith(CommandTargets.StopKeyPrefix, StringComparison.Ordinal));

    // "Take the elevator (to the casino)": finds the elevator and rides it (or just walks up to it on "go to the elevator").
    internal void HandleElevator(ParsedCommand cmd)
    {
        if (ElevatorUser.IsBusy) { Voice.Say(PartnerLine.Busy); return; }
        if (IsBusy || Evasion.IsCrouched) { Voice.Say(PartnerLine.Busy); return; }
        Elevator e = Targets.FindElevator(cmd, Transform.position);
        if (e == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoElevator); return; }
        CombatCommands.LowerGuard();
        LastTarget = e;
        // "go to the elevator": she only walks up to it; a ride is "take / use the elevator" or a destination ("go to the casino")
        if (cmd.Verb == CommandVerb.GoTo && !cmd.Keys.Exists(k => k.StartsWith(CommandTargets.StopKeyPrefix, StringComparison.Ordinal)))
        {
            Voice.Say(Interactor.MoveTo(e.Outside.position) ? PartnerLine.OnMyWay : PartnerLine.CantGetThere);
            return;
        }
        switch (ElevatorUser.Ride(e))
        {
            case PartnerElevatorUser.Result.Ok: Voice.Say(PartnerLine.ElevatorGoing); break;
            case PartnerElevatorUser.Result.NoKey: Voice.Say(PartnerLine.ElevatorNeedKey); break;
            case PartnerElevatorUser.Result.OutOfService: Voice.Say(PartnerLine.ElevatorOutOfService); break;
            default: Voice.Say(PartnerLine.Busy); break;
        }
    }

    // The elevator reached its stop.
    internal void OnElevatorArrived(Elevator e) => Voice.Say(PartnerLine.ElevatorArrived);
}
