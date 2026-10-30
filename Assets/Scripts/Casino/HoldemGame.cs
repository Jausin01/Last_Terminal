using System;
using System.Collections.Generic;
using UnityEngine;

// A game of Casino Hold'em at one table (same table, seat, deck, cards and chips as blackjack: BlackjackTable).
// The operator plays, Helena deals:
//   "let's play poker" -> she sits as the dealer -> takes the deck -> "place your bet" (the ante)
//   ante -> two cards each (hers face down) + the flop -> "call or fold?"
//   call (2x the ante) -> turn + river, she shows her cards -> paid / taken (HoldemEngine has the rules)
//   fold -> the ante is lost.  Between rounds: "yes" / a new ante = next round, "that's enough" = she gets up.
// The seat / camera / deck routine, the animation steps and dealing are shared with blackjack (CardTableGame).
public class HoldemGame : CardTableGame
{
    // Names of the layout fields below (the casino builder sets them for its table model).
    public const string BoardField = nameof(board);
    public const string PlayerHandField = nameof(playerHand);
    public const string AnteSpotField = nameof(anteSpot);
    public const string CallSpotField = nameof(callSpot);

    // The game's flow: seating, opening, betting, dealing (events playing), deciding, round end, leaving.
    public enum State { Off, Joining, Opening, Betting, Dealing, Deciding, RoundEnd, AskAgain, Leaving }

    // Table limits and the ante used by "deal" when none was placed yet.
    [Header("Rules")]
    [SerializeField] private int minAnte = 5;
    [SerializeField] private int maxAnte = 200;
    [SerializeField] private int defaultAnte = 10;

    // Where the board, the player's two cards and the ante / call bets go (table space).
    [Header("Layout (table space, felt at 0.751: the cards side by side, the board between her and the player)")]
    [SerializeField] private Vector3 board = new Vector3(0f, 0.751f, 0.71f);
    [SerializeField] private float boardSpacing = 0.075f;
    [SerializeField] private Vector3 playerHand = new Vector3(0f, 0.751f, 0.92f);
    [SerializeField] private float handSpacing = 0.07f;
    [SerializeField] private Vector3 anteSpot = new Vector3(-0.06f, 0.751f, 1.04f);
    [SerializeField] private Vector3 callSpot = new Vector3(0.06f, 0.751f, 1.04f);

    // Pause before the next round.
    [Header("Round")]
    [SerializeField] private float pauseAfterRound = 1.6f;

    // The rules engine (made on the first Join) and where this game is.
    private HoldemEngine engine;
    private State state;
    // Her cards have been turned up this round.
    private bool dealerShown;

    // What is on the table: card objects (player, dealer, board) and chip piles (ante, call, winnings handed back with the sweep).
    private readonly List<BlackjackTable.CardVisual> playerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<BlackjackTable.CardVisual> dealerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<BlackjackTable.CardVisual> boardCards = new List<BlackjackTable.CardVisual>();
    private readonly List<GameObject> anteChips = new List<GameObject>();
    private readonly List<GameObject> callChips = new List<GameObject>();
    private readonly List<GameObject> toPlayer = new List<GameObject>();

    // State read by the HUD, the commands and her animations.
    public State CurrentState => state;
    public HoldemEngine Engine => engine;
    public override int MinBet => minAnte;
    public override int MaxBet => maxAnte;
    public bool DealerShown => dealerShown;   // her cards are face up (the HUD names her hand)
    // Cards already on the table (the one still in her hand doesn't count).
    public int ShownPlayerCards => CountShown(playerCards);
    public int ShownBoardCards => CountShown(boardCards);

    // Cards of a list that are on the table.
    private int CountShown(List<BlackjackTable.CardVisual> list)
    {
        int n = 0;
        foreach (BlackjackTable.CardVisual c in list) if (c != cardInHand) n++;
        return n;
    }

    // ------------------------------------------------------------------ CardTableGame hooks
    // The shared stage of the current State (every state of a round = InRound).
    protected override TableStage Stage
    {
        get
        {
            switch (state)
            {
                case State.Off: return TableStage.Off;
                case State.Joining: return TableStage.Joining;
                case State.Opening: return TableStage.Opening;
                case State.Betting: return TableStage.Betting;
                case State.AskAgain: return TableStage.AskAgain;
                case State.Leaving: return TableStage.Leaving;
                default: return TableStage.InRound;
            }
        }
    }

    // The State for a shared stage the base routine moves to.
    protected override void EnterStage(TableStage stage)
    {
        switch (stage)
        {
            case TableStage.Off: SetState(State.Off); break;
            case TableStage.Joining: SetState(State.Joining); break;
            case TableStage.Opening: SetState(State.Opening); break;
            case TableStage.Betting: SetState(State.Betting); break;
            case TableStage.AskAgain: SetState(State.AskAgain); break;
            case TableStage.Leaving: SetState(State.Leaving); break;
            default: throw new ArgumentOutOfRangeException(nameof(stage));
        }
    }

    // She nags while the operator has to bet, call / fold or say "another round".
    protected override bool WaitingForPlayer => state == State.Betting || state == State.Deciding || state == State.AskAgain;

    // Broke = can't cover the smallest ante plus its call (3x the ante).
    protected override int BettingFloor => minAnte * 3;

    // Dealt cards land a little crooked (up to 4 degrees).
    protected override float CardTilt => 4f;

    // The engine with this table's limits.
    protected override void EnsureEngine()
    {
        if (engine == null) engine = new HoldemEngine(minAnte, maxAnte, bank.Chips, Environment.TickCount);
    }

    // Bets go back; the bank takes the engine's chip count.
    protected override void CancelRound()
    {
        if (engine == null) return;
        engine.CancelRound();
        bank.Set(engine.Chips);
    }

    // The engine starts from the bank's chips.
    protected override void SyncEngineChips() => engine.SetChips(bank.Chips);

    // Forgets every card and chip list of the round.
    protected override void ClearRoundObjects()
    {
        playerCards.Clear();
        dealerCards.Clear();
        boardCards.Clear();
        anteChips.Clear();
        callChips.Clear();
        toPlayer.Clear();
    }

    // ------------------------------------------------------------------ operator commands
    // An ante between rounds: checked by the engine (limits, chips for the call too), then the round is dealt.
    public override void Bet(int ante)
    {
        if (!CanStartRound()) return;
        engine.SetChips(bank.Chips);
        switch (engine.CheckAnte(ante))
        {
            case HoldemEngine.BetResult.BelowMinimum:
            case HoldemEngine.BetResult.AboveMaximum: voice.Say(PartnerLine.BjBetLimits); return;
            case HoldemEngine.BetResult.NotEnoughChips: voice.Say(PartnerLine.BjNotEnough); return;   // the ante needs 2x more for the call
        }
        ClearTable();
        dealerShown = false;
        if (engine.StartRound(ante) != HoldemEngine.BetResult.Ok) return;
        bank.Set(engine.Chips);
        SetState(State.Dealing);
    }

    // "yes" / "deal" / "again": the same ante; while deciding = call
    public override void Continue()
    {
        if (state == State.Deciding) { Call(); return; }
        if (!CanStartRound()) return;
        int ante = engine.LastAnte > 0 ? engine.LastAnte : defaultAnte;
        ante = Mathf.Clamp(Mathf.Min(ante, bank.Chips / 3), minAnte, maxAnte);
        Bet(ante);
    }

    // "no": between rounds = leave; while deciding = fold
    public override void Decline()
    {
        if (state == State.Deciding) { Fold(); return; }
        if (IsBetweenRounds) { Quit(); return; }
        voice.Say(PartnerLine.BjFinishHand);
    }

    // "Call": pays 2x the ante; the turn and river come out.
    public void Call()
    {
        if (state != State.Deciding) { NotNowLine(); return; }
        if (!engine.DoCall()) { voice.Say(PartnerLine.BjNotEnough); return; }
        bank.Set(engine.Chips);
        SetState(State.Dealing);
    }

    // "Fold": the ante is lost.
    public void Fold()
    {
        if (state != State.Deciding) { NotNowLine(); return; }
        if (!engine.Fold()) return;
        voice.Say(PartnerLine.PokerFolded);
        SetState(State.Dealing);
    }

    // What she says when call / fold come at the wrong time.
    private void NotNowLine()
    {
        if (state == State.Betting || state == State.AskAgain) voice.Say(PartnerLine.BjPlaceBet);
        else voice.Say(PartnerLine.Busy);
    }

    // ------------------------------------------------------------------ round flow
    // One engine event at a time, then "call or fold?", then the round end and the sweep.
    protected override void StepRound()
    {
        if (state == State.Dealing)
        {
            if (engine.Events.Count > 0) { Play(engine.Events.Dequeue()); return; }
            if (engine.CurrentPhase == HoldemEngine.Phase.Deciding)
            {
                SetState(State.Deciding);
                voice.Say(PartnerLine.PokerCallOrFold);
            }
            else if (engine.CurrentPhase == HoldemEngine.Phase.RoundOver)
            {
                RoundOverLine();
                SetState(State.RoundEnd);
                waitTimer = pauseAfterRound;
            }
            return;
        }

        if (state == State.RoundEnd)
        {
            stepBusy = true;
            Sweep(() => { stepBusy = false; AskAgain(); });
        }
    }

    // After the round: "another round?" (or betting again when the chips are too low).
    private void AskAgain()
    {
        engine.NextRound();
        bank.Set(engine.Chips);
        if (bank.Chips < BettingFloor) { EnterBetting(false); return; }
        SetState(State.AskAgain);
        voice.Say(PartnerLine.BjAnotherRound);
    }

    // What she says at the end of a round.
    private void RoundOverLine()
    {
        switch (engine.Outcome)
        {
            case HoldemOutcome.Win: voice.Say(PartnerLine.BjPlayerWins); break;
            case HoldemOutcome.Lose: voice.Say(PartnerLine.BjHouseWins); break;
            case HoldemOutcome.Push: voice.Say(PartnerLine.BjPush); break;
            case HoldemOutcome.DealerNoQualify: voice.Say(PartnerLine.PokerNotQualified); break;
        }
    }

    // ------------------------------------------------------------------ events -> table
    // Plays one engine event on the table (cards, chips, her reveal...), then tells the HUD.
    private void Play(HoldemEvent e)
    {
        switch (e.Type)
        {
            case HoldemEventType.AntePlaced:
                anteChips.AddRange(Drop(e.Amount, table.World(anteSpot)));
                break;
            case HoldemEventType.CallPlaced:
                callChips.AddRange(Drop(e.Amount, table.World(callSpot)));
                break;
            case HoldemEventType.CardToPlayer:
                Deal(e.Card, table.World(playerHand + new Vector3((e.Index - 0.5f) * handSpacing, e.Index * 0.0004f, 0f)), true, playerCards);
                break;
            case HoldemEventType.CardToDealer:
                Deal(e.Card, table.DealerCardPosition(e.Index), false, dealerCards);
                break;
            case HoldemEventType.CardToBoard:
                Deal(e.Card, table.World(board + new Vector3((e.Index - 2) * boardSpacing, 0f, 0f)), true, boardCards);
                break;
            case HoldemEventType.Folded:
                // her cards and the board stay; the ante goes to the house
                Begin(1);
                table.MoveChips(anteChips, table.TrayPosition, chipTime, true, Part);
                anteChips.Clear();
                break;
            case HoldemEventType.DealerReveal:
                RevealDealer();
                break;
            case HoldemEventType.Settled:
                Settle(e.Amount);
                break;
        }
        bank.Set(engine.Chips);
        RaiseChanged();
    }

    // Turns her two cards up.
    private void RevealDealer()
    {
        dealerShown = true;
        if (dealerCards.Count == 0) return;
        voice.Say(PartnerLine.BjPeek);
        Begin(dealerCards.Count);
        foreach (BlackjackTable.CardVisual c in dealerCards)
            table.MoveCard(c, c.Transform.position, true, cardSlideTime, 0.05f, Part);
        waitTimer = 0.6f;
    }

    // Pays or takes the bets (winnings come from the tray and go to the player with the sweep).
    private void Settle(int returned)
    {
        int ante = engine.Ante, call = engine.Call;
        int profit = returned - ante - call;   // what the house pays on top of the bets
        switch (engine.Outcome)
        {
            case HoldemOutcome.Fold:
            case HoldemOutcome.Lose:
            {
                Begin(1);
                var lost = new List<GameObject>(anteChips);
                lost.AddRange(callChips);
                table.MoveChips(lost, table.TrayPosition, chipTime, true, Part);
                break;
            }
            default:
            {
                var back = new List<GameObject>(anteChips);
                back.AddRange(callChips);
                if (profit > 0)
                {
                    List<GameObject> won = table.SpawnChips(profit, table.TrayPosition);
                    Begin(1);
                    table.MoveChips(won, table.World(anteSpot) + table.transform.forward * -0.07f, chipTime, false, Part);
                    back.AddRange(won);
                }
                waitTimer = chipTime + 0.4f;
                KeepForPlayer(back);   // they go to the player with the sweep, after the pause
                break;
            }
        }
        anteChips.Clear();
        callChips.Clear();
    }

    // The chips that go to the player at the sweep.
    private void KeepForPlayer(List<GameObject> chips)
    {
        toPlayer.Clear();
        toPlayer.AddRange(chips);
    }

    // the winnings to the player, every card to the discard holder
    private void Sweep(Action done)
    {
        if (toPlayer.Count > 0) { table.MoveChips(new List<GameObject>(toPlayer), table.PlayerSidePosition, chipTime, true, null); toPlayer.Clear(); }
        var all = new List<BlackjackTable.CardVisual>(dealerCards);
        all.AddRange(playerCards);
        all.AddRange(boardCards);
        dealerCards.Clear();
        playerCards.Clear();
        boardCards.Clear();
        SweepCards(all, done);
    }

    // Changes state; resets the idle nag; tells the HUD.
    private void SetState(State s)
    {
        state = s;
        OnStateChanged();
    }
}
