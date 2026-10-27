using System;
using System.Collections.Generic;
using UnityEngine;

// The roulette, with her as the croupier: bets said in many forms ("25 on red", "10 on 17", "zero", "same bet"),
// spin, clear, leaving; she calls "no more bets" and the result. Closes the wheel when she walks off it (Tick).
internal sealed class RouletteCommands : CommandHandler
{
    private readonly List<int> rouletteNumbers = new List<int>();   // numbers said in the current roulette command

    // She is the croupier at an open wheel.
    internal bool RouletteOn => Targets.Roulette != null && Targets.Roulette.IsOpen;

    public RouletteCommands(CommandContext ctx) : base(ctx) { }

    // "let's play roulette": she goes to the wheel (the croupier's place) and calls for bets
    internal void JoinRoulette(RouletteGame wheel)
    {
        if (InCombat) { Voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || Evasion.IsCrouched || KeypadUser == null) { Voice.Say(PartnerLine.Busy); return; }
        CombatCommands.LowerGuard();
        if (!KeypadUser.Use(wheel, "")) { Voice.Say(PartnerLine.CantGetThere); return; }
        wheel.Open();
        LastTarget = wheel;
        Voice.Say(PartnerLine.RouletteBets);
    }

    // Closes the roulette (bets back) and takes her hand off the wheel, then the next order.
    private void LeaveRoulette(Action then)
    {
        RouletteGame wheel = Targets.Roulette;
        wheel.Close();
        if (KeypadUser.Current == wheel) KeypadUser.Leave(then);
        else then?.Invoke();
    }

    // Commands while she is the croupier (bets, spin, clear, leaving).
    internal void Roulette(ParsedCommand cmd)
    {
        RouletteGame wheel = Targets.Roulette;
        if (KeypadUser.Current != wheel) { wheel.Close(); Rerun(cmd); return; }   // she left the wheel
        ParseNumbers(cmd, rouletteNumbers);
        RouletteGame.BetKind kind;
        bool hasKind = TryBetKind(cmd, out kind);

        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); return;
            case CommandVerb.Spin:
            case CommandVerb.Again:
            case CommandVerb.Yes:
            case CommandVerb.Deal:
            case CommandVerb.Play:
                if (wheel.IsSpinning) { Voice.Say(PartnerLine.Busy); return; }
                if (hasKind && !PlaceRouletteBet(wheel, kind, cmd)) return;
                if (wheel.Bets.Count == 0 && (cmd.Verb == CommandVerb.Again || cmd.HasKey(CommandTargets.SameBetKey)) && wheel.HasLastBets)
                    if (!SayBetResult(wheel.RepeatLastBets(), false)) return;
                if (wheel.Bets.Count == 0) { Voice.Say(PartnerLine.RouletteNeedBet); return; }
                KeypadUser.Use(wheel, RouletteGame.SpinKey.ToString());
                return;
            case CommandVerb.ClearBets:
                if (wheel.IsSpinning) { Voice.Say(PartnerLine.Busy); return; }
                wheel.ClearBets();
                Voice.Say(PartnerLine.Acknowledge);
                return;
            case CommandVerb.Quit:
            case CommandVerb.No:
            case CommandVerb.Stop:
                if (wheel.IsSpinning) { Voice.Say(PartnerLine.BjFinishHand); return; }
                LeaveRoulette(null);
                Voice.Say(PartnerLine.BjGameOver);
                return;
        }

        if (hasKind) { PlaceRouletteBet(wheel, kind, cmd); return; }
        if (cmd.HasKey(CommandTargets.SameBetKey) && wheel.HasLastBets) { SayBetResult(wheel.RepeatLastBets(), true); return; }
        // numbers alone: "bet 25" = the stake; "17" / "10 on 17" = on that number
        if (rouletteNumbers.Count > 0 && (cmd.Verb == CommandVerb.Bet || cmd.Verb == CommandVerb.None))
        {
            if (cmd.Verb == CommandVerb.Bet && rouletteNumbers.Count == 1) { wheel.SetStake(rouletteNumbers[0]); Voice.Say(PartnerLine.Acknowledge); return; }
            PlaceRouletteBet(wheel, RouletteGame.BetKind.Straight, cmd);
            return;
        }
        if (cmd.Verb == CommandVerb.Bet) { Voice.Say(PartnerLine.RouletteBets); return; }
        // anything else: the game ends, then the order (not while the ball runs)
        if (wheel.IsSpinning) { Voice.Say(PartnerLine.BjFinishHand); return; }
        LeaveRoulette(() => Rerun(cmd));
    }

    // the numbers said: digits in the text ("10 on 17"), else the spoken one
    private static void ParseNumbers(ParsedCommand cmd, List<int> into)
    {
        into.Clear();
        string t = cmd.Text ?? "";
        int value = -1;
        for (int i = 0; i <= t.Length; i++)
        {
            char c = i < t.Length ? t[i] : ' ';
            if (char.IsDigit(c)) { value = (value < 0 ? 0 : value * 10) + (c - '0'); continue; }
            if (value >= 0) { into.Add(value); value = -1; }
        }
        if (into.Count == 0 && cmd.Number > 0) into.Add(cmd.Number);
    }

    // Which roulette bet the command names (colors, even / odd, low / high, dozens, a number).
    private static bool TryBetKind(ParsedCommand cmd, out RouletteGame.BetKind kind)
    {
        kind = RouletteGame.BetKind.Straight;
        if (cmd.HasKey(CommandTargets.ColorKeyPrefix + "red")) { kind = RouletteGame.BetKind.Red; return true; }
        if (cmd.HasKey(CommandTargets.ColorKeyPrefix + "black")) { kind = RouletteGame.BetKind.Black; return true; }
        if (cmd.HasKey(CommandTargets.EvenKey)) { kind = RouletteGame.BetKind.Even; return true; }
        if (cmd.HasKey(CommandTargets.OddKey)) { kind = RouletteGame.BetKind.Odd; return true; }
        if (cmd.HasKey(CommandTargets.LowKey)) { kind = RouletteGame.BetKind.Low; return true; }
        if (cmd.HasKey(CommandTargets.HighKey)) { kind = RouletteGame.BetKind.High; return true; }
        if (cmd.HasKey(CommandTargets.Dozen1Key)) { kind = RouletteGame.BetKind.Dozen1; return true; }
        if (cmd.HasKey(CommandTargets.Dozen2Key)) { kind = RouletteGame.BetKind.Dozen2; return true; }
        if (cmd.HasKey(CommandTargets.Dozen3Key)) { kind = RouletteGame.BetKind.Dozen3; return true; }
        if (cmd.HasKey(CommandTargets.ZeroKey) || cmd.HasKey(CommandTargets.NumberBetKey)) return true;
        return false;
    }

    // amount and number from what was said: "25 on red" (25), "on red" (the stake), "10 on 17", "number 17" (the stake),
    // "zero" / "5 on zero"
    private bool PlaceRouletteBet(RouletteGame wheel, RouletteGame.BetKind kind, ParsedCommand cmd)
    {
        int amount = wheel.Stake, number = 0;
        if (kind == RouletteGame.BetKind.Straight)
        {
            if (cmd.HasKey(CommandTargets.ZeroKey)) { number = 0; if (rouletteNumbers.Count > 0) amount = rouletteNumbers[0]; }
            else if (rouletteNumbers.Count >= 2) { amount = rouletteNumbers[0]; number = rouletteNumbers[1]; }
            else if (rouletteNumbers.Count == 1) number = rouletteNumbers[0];
            else { Voice.Say(PartnerLine.RouletteBets); return false; }
        }
        else if (rouletteNumbers.Count > 0) amount = rouletteNumbers[0];
        return SayBetResult(wheel.PlaceBet(kind, number, amount), true);
    }

    // Her answer to a roulette bet result (false = it wasn't placed).
    private bool SayBetResult(RouletteGame.BetResult r, bool sayOk)
    {
        switch (r)
        {
            case RouletteGame.BetResult.Ok: if (sayOk) Voice.Say(PartnerLine.RouletteBetDown); return true;
            case RouletteGame.BetResult.Spinning: Voice.Say(PartnerLine.Busy); return false;
            case RouletteGame.BetResult.NotEnoughChips: Voice.Say(PartnerLine.BjNotEnough); return false;
            case RouletteGame.BetResult.NoSuchNumber: Voice.Say(PartnerLine.NothingThere); return false;
            default: Voice.Say(PartnerLine.BjBetLimits); return false;
        }
    }

    // The ball is rolling: "no more bets".
    internal void OnRouletteSpin(RouletteGame wheel) => Voice.Say(PartnerLine.RouletteNoMoreBets);

    // No bets when the spin was asked for.
    internal void OnRouletteNoBets(RouletteGame wheel) => Voice.Say(PartnerLine.RouletteNeedBet);

    // The ball landed: win / even / lose.
    internal void OnRouletteLanded(RouletteGame wheel, int result, int won, int staked) =>
        Voice.Say(won > staked ? PartnerLine.RouletteWin : won > 0 ? PartnerLine.BjPush : PartnerLine.RouletteLose);

    // Each frame: she walked off the wheel (sent away, a fight, a long wait): the roulette closes, the bets come back.
    internal void Tick()
    {
        if (RouletteOn && KeypadUser.Current != Targets.Roulette && !Targets.Roulette.IsSpinning) Targets.Roulette.Close();
    }
}
