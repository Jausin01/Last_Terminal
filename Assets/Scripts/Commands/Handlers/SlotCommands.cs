using UnityEngine;

// Slot machines: "play the slots" takes her to the closest one; at it: spin / bet / leave; she reacts to wins.
internal sealed class SlotCommands : CommandHandler
{
    public SlotCommands(CommandContext ctx) : base(ctx) { }

    // true = handled. At a machine: spin / bet / leave; "play the slots" / "the slot machine": she goes to the closest.
    internal bool HandleSlot(ParsedCommand cmd)
    {
        var at = KeypadUser.Current as SlotMachine;
        bool named = cmd.HasKey(CommandTargets.SlotKey);
        if (at != null)
        {
            switch (cmd.Verb)
            {
                case CommandVerb.Spin: case CommandVerb.Again: case CommandVerb.Yes: case CommandVerb.Play: case CommandVerb.Deal:
                    if (cmd.Number > 0) at.SetBet(cmd.Number);
                    if (at.IsSpinning) { Voice.Say(PartnerLine.Busy); return true; }
                    KeypadUser.Use(at, SlotMachine.Spin.ToString());
                    return true;
                case CommandVerb.Bet:
                    if (cmd.Number <= 0) { Voice.Say(PartnerLine.BjPlaceBet); return true; }
                    at.SetBet(cmd.Number);
                    Voice.Say(PartnerLine.Acknowledge);
                    return true;
                case CommandVerb.Quit: case CommandVerb.Stop: case CommandVerb.No:
                    KeypadUser.Leave();
                    Voice.Say(PartnerLine.Acknowledge);
                    return true;
                case CommandVerb.Status:
                    return false;
                default:
                    if (cmd.Verb == CommandVerb.None && cmd.Number > 0 && cmd.Keys.Count == 0)
                    {
                        at.SetBet(cmd.Number);   // a bare number at the machine: the bet
                        Voice.Say(PartnerLine.Acknowledge);
                        return true;
                    }
                    if (named) return true;
                    KeypadUser.Leave(() => Rerun(cmd));
                    return true;
            }
        }
        // "spin" again after she let go of the machine: the one she played
        if (!named && cmd.Verb == CommandVerb.Spin && LastTarget is SlotMachine last && Targets.Slots.Count > 0)
        {
            if (KeypadUser.IsBusy || IsBusy) { Voice.Say(PartnerLine.Busy); return true; }
            if (KeypadUser.Use(last, SlotMachine.Spin.ToString())) return true;
        }
        if (!named || Targets.Slots.Count == 0) return false;
        if (KeypadUser.IsBusy || IsBusy || Evasion.IsCrouched) { Voice.Say(PartnerLine.Busy); return true; }
        SlotMachine machine = Targets.FindSlot(Transform.position);
        if (machine == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return true; }
        if (cmd.Number > 0) machine.SetBet(cmd.Number);
        CombatCommands.LowerGuard();
        if (!KeypadUser.Use(machine, cmd.Verb == CommandVerb.Spin ? SlotMachine.Spin.ToString() : "")) { Voice.Say(PartnerLine.CantGetThere); return true; }
        LastTarget = machine;
        Voice.Say(PartnerLine.SlotReady);
        return true;
    }

    // What she says after a slot spin (only for wins).
    internal void OnSpinEnded(SlotMachine machine, int win)
    {
        if (win >= machine.Bet * 25) Voice.Say(PartnerLine.SlotJackpot);
        else if (win > machine.Bet) Voice.Say(PartnerLine.SlotWin);
    }

    // Not enough chips for the slot bet.
    internal void OnNoChips(SlotMachine machine) => Voice.Say(PartnerLine.SlotNoChips);
}
