using UnityEngine;

// The card tables: "let's play" picks the closest game (blackjack, Casino Hold'em or the roulette); while a hand is
// on, the table takes the commands (bets, hit / stand / double / split..., call / fold, leaving).
internal sealed class CardTableCommands : CommandHandler
{
    public CardTableCommands(CommandContext ctx) : base(ctx) { }

    // "let's play": the game named ("poker", "blackjack", "roulette"), else the closest one in reach
    internal void JoinTable(ParsedCommand cmd)
    {
        Vector3 from = Transform.position;
        bool wantsBlackjack = cmd.HasKey(CommandTargets.BlackjackKey);
        bool wantsPoker = cmd.HasKey(CommandTargets.PokerKey);
        bool wantsRoulette = cmd.HasKey(CommandTargets.RouletteKey);
        bool any = !wantsBlackjack && !wantsPoker && !wantsRoulette;
        bool tooFar = false;

        BlackjackGame bj = any || wantsBlackjack ? Targets.FindTable(from) : null;
        tooFar |= Targets.LastSearchOutOfReach;
        HoldemGame holdem = any || wantsPoker ? Targets.FindHoldem(from) : null;
        tooFar |= Targets.LastSearchOutOfReach;
        RouletteGame wheel = (any || wantsRoulette) && Targets.Roulette != null ? Targets.Roulette : null;
        if (wheel != null && !Targets.InReach(from, wheel.transform.position)) { wheel = null; tooFar = true; }

        float dBj = bj != null ? (bj.transform.position - from).sqrMagnitude : float.MaxValue;
        float dPoker = holdem != null ? (holdem.transform.position - from).sqrMagnitude : float.MaxValue;
        float dWheel = wheel != null ? (wheel.transform.position - from).sqrMagnitude : float.MaxValue;
        if (wheel != null && dWheel <= dBj && dWheel <= dPoker) { RouletteCommands.JoinRoulette(wheel); return; }
        if (holdem != null && dPoker <= dBj) { PlayHoldem(holdem); return; }
        if (bj == null && tooFar) { Voice.Say(PartnerLine.TooFar); return; }
        PlayAt(bj);
    }

    // "let's play" (or a click on the table): she goes to sit as the dealer
    public void PlayAt(BlackjackGame table)
    {
        if (!IsActive) return;
        if (InCombat) { Voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || Evasion.IsCrouched) { Voice.Say(PartnerLine.Busy); return; }
        if (table == null) { Voice.Say(PartnerLine.BjNoTable); return; }
        if (!PathExists(table.Table.Seat.GetWalkInPosition(Transform.position))) { Voice.Say(PartnerLine.CantGetThere); return; }
        Voice.Say(table.Join() ? PartnerLine.BjAccept : PartnerLine.Busy);
    }

    // "Let's play poker": she goes to sit as the dealer of a Hold'em table.
    public void PlayHoldem(HoldemGame table)
    {
        if (!IsActive) return;
        if (InCombat) { Voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || Evasion.IsCrouched) { Voice.Say(PartnerLine.Busy); return; }
        if (!PathExists(table.Table.Seat.GetWalkInPosition(Transform.position))) { Voice.Say(PartnerLine.CantGetThere); return; }
        Voice.Say(table.Join() ? PartnerLine.BjAccept : PartnerLine.Busy);
    }

    // Commands while a Hold'em game is on (bets, call / fold, leaving).
    internal void Holdem(HoldemGame game, ParsedCommand cmd)
    {
        int ante = HoldemAmount(game, cmd);
        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); break;
            case CommandVerb.Bet:
                if (ante > 0) game.Bet(ante);
                else if (cmd.HasKey(CommandTargets.SameBetKey)) game.Continue();
                else Voice.Say(PartnerLine.BjPlaceBet);
                break;
            case CommandVerb.Play:
            case CommandVerb.Deal:
            case CommandVerb.Yes:
            case CommandVerb.Again:
            case CommandVerb.Draw:
                if (ante > 0 && game.IsBetweenRounds) game.Bet(ante);
                else game.Continue();
                break;
            case CommandVerb.Call: game.Call(); break;
            case CommandVerb.Surrender: game.Fold(); break;   // "fold"
            case CommandVerb.No: game.Decline(); break;
            case CommandVerb.Quit:
            case CommandVerb.Stop: game.Quit(); break;
            case CommandVerb.None:
                if (ante > 0 && game.IsBetweenRounds) game.Bet(ante);
                else Voice.Say(PartnerLine.NotUnderstood);
                break;
            default:
                if (game.IsBetweenRounds) game.Quit();
                else Voice.Say(PartnerLine.BjFinishHand);
                break;
        }
    }

    // Ante named in the command ("all in", "minimum", "same bet", a number); 0 = none.
    private static int HoldemAmount(HoldemGame game, ParsedCommand cmd)
    {
        if (cmd.HasKey(CommandTargets.AllInKey) || cmd.HasKey(CommandTargets.MaxBetKey)) return Mathf.Min(game.Chips / 3, game.MaxBet);
        if (cmd.HasKey(CommandTargets.MinBetKey)) return game.MinBet;
        if (cmd.HasKey(CommandTargets.SameBetKey) && game.Engine != null && game.Engine.LastAnte > 0) return game.Engine.LastAnte;
        return cmd.Number;
    }

    // Commands while a blackjack game is on (bets, hit / stand / double / split / surrender / insurance, leaving).
    internal void Blackjack(BlackjackGame game, ParsedCommand cmd)
    {
        int bet = BetAmount(game, cmd);
        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); break;
            case CommandVerb.Bet:
                if (bet > 0) game.Bet(bet);
                else if (cmd.HasKey(CommandTargets.SameBetKey)) game.Continue();
                else Voice.Say(PartnerLine.BjPlaceBet);
                break;
            case CommandVerb.Play: game.Continue(); break;
            case CommandVerb.Deal:
            case CommandVerb.Yes:
            case CommandVerb.Again:
            case CommandVerb.Draw:    // "ready"
                if (bet > 0 && game.IsBetweenRounds) game.Bet(bet);
                else game.Continue();
                break;
            case CommandVerb.Hit:
            case CommandVerb.Attack:  // "hit" is also an attack word
                game.Decide(BlackjackGame.Decision.Hit); break;
            case CommandVerb.Stand:
            case CommandVerb.Stop: game.Decide(BlackjackGame.Decision.Stand); break;
            case CommandVerb.Double: game.Decide(BlackjackGame.Decision.Double); break;
            case CommandVerb.Split: game.Decide(BlackjackGame.Decision.Split); break;
            case CommandVerb.Surrender: game.Decide(BlackjackGame.Decision.Surrender); break;
            case CommandVerb.Insurance: game.Insurance(true); break;
            case CommandVerb.No: game.Decline(); break;
            case CommandVerb.Quit: game.Quit(); break;
            case CommandVerb.None:
                if (bet > 0 && game.IsBetweenRounds) game.Bet(bet);
                else Voice.Say(PartnerLine.NotUnderstood);
                break;
            default:
                // anything else: between rounds she leaves the table (say it again once she is up), mid-hand she finishes first
                if (game.IsBetweenRounds) game.Quit();
                else Voice.Say(PartnerLine.BjFinishHand);
                break;
        }
    }

    // chips named in the command ("bet 50", "all in", "minimum", "same bet"); 0 = none
    private static int BetAmount(BlackjackGame game, ParsedCommand cmd)
    {
        if (cmd.HasKey(CommandTargets.AllInKey) || cmd.HasKey(CommandTargets.MaxBetKey)) return Mathf.Min(game.Chips, game.MaxBet);
        if (cmd.HasKey(CommandTargets.MinBetKey)) return game.MinBet;
        if (cmd.HasKey(CommandTargets.SameBetKey) && game.Engine != null && game.Engine.LastBet > 0) return game.Engine.LastBet;
        return cmd.Number;
    }
}
