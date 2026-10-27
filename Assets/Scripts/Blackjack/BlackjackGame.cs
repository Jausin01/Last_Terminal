using System;
using System.Collections.Generic;
using UnityEngine;

// A game of blackjack at one table: the operator plays (by command), Helena deals.
//   "let's play" -> she sits as the dealer (save point seat routine) -> takes the deck -> "place your bet"
//   bet -> she deals (one deal clip per card, the card slides from her hand to its place) -> insurance? -> peek
//   -> hit / stand / double / split / surrender -> her hand -> chips paid / taken -> "another round?"
//   between rounds: "yes" / a new bet = next round, "no" / "that's enough" = she puts the deck back and gets up.
// The rules live in BlackjackEngine; this plays its events one by one with the animations, cards and chips.
// The seat / camera / deck routine, the animation steps and dealing are shared with Hold'em (CardTableGame).
public class BlackjackGame : CardTableGame
{
    // The game's own flow (on top of the engine's round phases): seating, opening, betting, dealing, deciding, ending, leaving.
    public enum State { Off, Joining, Opening, Betting, Playing, Insurance, Deciding, RoundEnd, AskAgain, Leaving }
    // The player's choices on a hand (spoken commands).
    public enum Decision { Hit, Stand, Double, Split, Surrender }

    // House rules handed to the engine (see BlackjackRules).
    [Header("Rules")]
    [SerializeField, Range(1, 8)] private int decks = 6;
    [SerializeField] private bool dealerHitsSoft17;
    [SerializeField] private int minBet = 5;
    [SerializeField] private int maxBet = 500;
    [SerializeField] private bool lateSurrender = true;
    [SerializeField, Range(2, 4)] private int maxHands = 4;

    // Pause before the next round, and the bet used by "deal" when no bet was ever placed.
    [Header("Round")]
    [SerializeField] private float pauseAfterRound = 1.4f;
    [SerializeField] private int defaultBet = 10;

    // The rules engine (made on the first Join) and where this game is.
    private BlackjackEngine engine;
    private State state;
    // Round bookkeeping: first decision of the round (she says "your turn"), first shuffle (no line), wins in a row, chips won/lost this round.
    private bool firstDecision;
    private bool shuffledOnce;
    private int winStreak;
    private int roundNet;
    // A decision said while cards were still being dealt: done as soon as the player's turn starts.
    private Decision? pending;

    // What is on the table: card objects per player hand, dealer cards, chip piles (bets, insurance, winnings to hand back).
    private readonly List<List<BlackjackTable.CardVisual>> handCards = new List<List<BlackjackTable.CardVisual>>();
    private readonly List<BlackjackTable.CardVisual> dealerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<List<GameObject>> betChips = new List<List<GameObject>>();
    private readonly List<GameObject> insuranceChips = new List<GameObject>();
    private readonly List<GameObject> returnChips = new List<GameObject>();

    // State read by the HUD, the commands and her animations.
    public State CurrentState => state;
    public BlackjackEngine Engine => engine;
    public override int MinBet => minBet;
    public override int MaxBet => maxBet;

    // what is on the table now (the HUD shows the cards once they are dealt, not before)
    public int ShownHandCount => handCards.Count;
    public int ActiveHandIndex => state == State.Deciding && engine != null ? engine.Active : -1;

    // The dealer's cards on the table now (not the one still in her hand), with face up / down.
    public void GetShownDealer(List<Card> cards, List<bool> faceUp)
    {
        cards.Clear(); faceUp.Clear();
        foreach (BlackjackTable.CardVisual c in dealerCards)
        {
            if (c == cardInHand) continue;
            cards.Add(c.Card); faceUp.Add(c.FaceUp);
        }
    }

    // One player hand's cards on the table now.
    public void GetShownHand(int hand, List<Card> cards)
    {
        cards.Clear();
        if (hand < 0 || hand >= handCards.Count) return;
        foreach (BlackjackTable.CardVisual c in handCards[hand])
            if (c != cardInHand) cards.Add(c.Card);
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

    // She nags while the operator has to bet, decide, answer insurance or say "another round".
    protected override bool WaitingForPlayer =>
        state == State.Betting || state == State.Deciding || state == State.Insurance || state == State.AskAgain;

    // Broke = less than the minimum bet.
    protected override int BettingFloor => minBet;

    // Dealt cards land a little crooked (up to 6 degrees).
    protected override float CardTilt => 6f;

    // The engine with this table's house rules.
    protected override void EnsureEngine()
    {
        if (engine == null)
            engine = new BlackjackEngine(new BlackjackRules
            {
                Decks = decks, DealerHitsSoft17 = dealerHitsSoft17, MinBet = minBet, MaxBet = maxBet,
                LateSurrender = lateSurrender, MaxHands = maxHands,
            }, bank.Chips, Environment.TickCount);
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
        dealerCards.Clear();
        handCards.Clear();
        betChips.Clear();
        insuranceChips.Clear();
        returnChips.Clear();
    }

    // ------------------------------------------------------------------ operator commands
    // A bet between rounds: checked by the engine (limits, chips), then the round starts.
    public override void Bet(int amount)
    {
        if (!CanStartRound()) return;
        engine.SetChips(bank.Chips);
        switch (engine.CheckBet(amount))
        {
            case BlackjackEngine.BetResult.BelowMinimum:
            case BlackjackEngine.BetResult.AboveMaximum: voice.Say(PartnerLine.BjBetLimits); return;
            case BlackjackEngine.BetResult.NotEnoughChips: voice.Say(PartnerLine.BjNotEnough); return;
        }
        StartRound(amount);
    }

    // "yes" / "deal" / "one more" between rounds: same bet as last time
    public override void Continue()
    {
        if (state == State.Insurance) { Insurance(true); return; }
        if (state == State.Deciding || state == State.Playing) { Decide(Decision.Hit); return; }
        if (!CanStartRound()) return;
        int bet = engine.LastBet > 0 ? engine.LastBet : defaultBet;
        bet = Mathf.Clamp(Mathf.Min(bet, bank.Chips), minBet, maxBet);
        Bet(bet);
    }

    // "no" between rounds = leave; to insurance = decline
    public override void Decline()
    {
        if (state == State.Insurance) { Insurance(false); return; }
        if (IsBetweenRounds) { Quit(); return; }
        voice.Say(PartnerLine.BjFinishHand);
    }

    // "Insurance" / "no insurance": answers the engine's question.
    public void Insurance(bool take)
    {
        if (state != State.Insurance) { if (take) voice.Say(PartnerLine.DontHaveThat); return; }
        if (take && !engine.CanInsure) { voice.Say(PartnerLine.BjNotEnough); take = false; }
        engine.AnswerInsurance(take);
        SetState(State.Playing);
    }

    // Hit / stand / double / split / surrender: passed to the engine (or queued while dealing); she says why when it isn't allowed.
    public void Decide(Decision d)
    {
        if (state == State.Playing && engine.Phase == RoundPhase.PlayerTurn) { pending = d; return; }   // still dealing: do it after
        if (state != State.Deciding)
        {
            if (state == State.Betting || state == State.AskAgain) voice.Say(PartnerLine.BjPlaceBet);
            else if (state == State.Insurance) voice.Say(PartnerLine.BjInsurance);
            return;
        }
        bool ok;
        switch (d)
        {
            case Decision.Hit: ok = engine.Hit(); break;
            case Decision.Stand: ok = engine.Stand(); break;
            case Decision.Double: ok = engine.Double(); if (!ok) voice.Say(engine.Chips < engine.ActiveHand.Bet ? PartnerLine.BjNotEnough : PartnerLine.BjCantDouble); break;
            case Decision.Split: ok = engine.Split(); if (!ok) voice.Say(PartnerLine.BjCantSplit); break;
            default: ok = engine.Surrender(); if (!ok) voice.Say(PartnerLine.BjCantSurrender); break;
        }
        if (!ok) return;
        bank.Set(engine.Chips);
        SetState(State.Playing);
    }

    // ------------------------------------------------------------------ round flow
    // A new round with this bet (the engine deals; StepRound plays the events).
    private void StartRound(int bet)
    {
        ClearTable();
        roundNet = 0;
        firstDecision = true;
        pending = null;
        if (engine.StartRound(bet) != BlackjackEngine.BetResult.Ok) return;
        bank.Set(engine.Chips);
        SetState(State.Playing);
    }

    // One engine event at a time (each one waits for its animation), then the round end.
    protected override void StepRound()
    {
        if (state == State.Playing)
        {
            if (engine.Events.Count > 0) { Play(engine.Events.Dequeue()); return; }
            switch (engine.Phase)
            {
                case RoundPhase.Insurance:
                    SetState(State.Insurance);
                    voice.Say(PartnerLine.BjInsurance);
                    break;
                case RoundPhase.PlayerTurn:
                    SetState(State.Deciding);
                    if (firstDecision) { firstDecision = false; voice.Say(PartnerLine.BjYourTurn); }
                    if (pending.HasValue) { Decision d = pending.Value; pending = null; Decide(d); }
                    break;
                case RoundPhase.RoundOver:
                    RoundOverLine();
                    SetState(State.RoundEnd);
                    waitTimer = pauseAfterRound;
                    break;
            }
            return;
        }

        if (state == State.RoundEnd)
        {
            // chips to the player, cards to the discard holder
            stepBusy = true;
            table.MoveChips(returnChips, table.PlayerSidePosition, chipTime, true, null);
            returnChips.Clear();
            foreach (List<GameObject> pile in betChips) table.MoveChips(pile, table.PlayerSidePosition, chipTime, true, null);
            betChips.Clear();
            Sweep(() => { stepBusy = false; AskAgain(); });
        }
    }

    // After the round: "another round?" (or betting again when the chips are too low).
    private void AskAgain()
    {
        bank.Set(engine.Chips);
        if (bank.Chips < BettingFloor) { EnterBetting(false); return; }
        SetState(State.AskAgain);
        voice.Say(PartnerLine.BjAnotherRound);
    }

    // ------------------------------------------------------------------ events -> table
    // Plays one engine event on the table (cards, chips, her peek...), then tells the HUD.
    private void Play(TableEvent e)
    {
        switch (e.Type)
        {
            case TableEventType.Shuffle:
                if (shuffledOnce) { voice.Say(PartnerLine.BjShuffle); waitTimer = 1.2f; }
                shuffledOnce = true;
                break;

            case TableEventType.BetPlaced:
                EnsureHand(e.Hand);
                betChips[e.Hand] = Drop(e.Amount, table.BetPosition(e.Hand, VisualHands, 0));
                break;

            case TableEventType.CardToPlayer:
                EnsureHand(e.Hand);
                Deal(e.Card, table.PlayerCardPosition(e.Hand, VisualHands, handCards[e.Hand].Count), true, handCards[e.Hand]);
                break;

            case TableEventType.CardToDealer:
                Deal(e.Card, table.DealerCardPosition(dealerCards.Count), !e.FaceDown, dealerCards);
                break;

            case TableEventType.InsuranceTaken:
                insuranceChips.AddRange(Drop(e.Amount, table.InsurancePosition));
                break;

            case TableEventType.DealerPeek:
                voice.Say(PartnerLine.BjPeek);
                Begin(1);
                dealer.Peek(null, () => { dealer.Hold(); Part(); });
                break;

            case TableEventType.HoleRevealed:
                if (dealerCards.Count > 1)
                {
                    Begin(1);
                    table.MoveCard(dealerCards[1], dealerCards[1].Transform.position, true, cardSlideTime, 0.05f, Part);
                }
                break;

            case TableEventType.HandSplit:
                SplitVisuals(e.Source, e.Hand, e.Amount);
                break;

            case TableEventType.BetDoubled:
                betChips[e.Hand].AddRange(Drop(e.Amount, table.BetPosition(e.Hand, VisualHands, 1)));
                break;

            case TableEventType.HandActive:
                waitTimer = 0.15f;
                break;

            case TableEventType.HandSettled:
                Settle(e.Hand, e.Outcome, e.Amount);
                break;

            case TableEventType.InsuranceSettled:
                if (e.Flag)
                {
                    List<GameObject> paid = table.SpawnChips(e.Amount - engine.InsuranceBet, table.TrayPosition);   // 2:1
                    Begin(1);
                    table.MoveChips(paid, table.InsurancePosition + table.transform.right * 0.07f, chipTime, false, Part);
                    returnChips.AddRange(paid);
                    returnChips.AddRange(insuranceChips);
                    insuranceChips.Clear();
                    roundNet += e.Amount - engine.InsuranceBet;
                }
                else
                {
                    Begin(1);
                    table.MoveChips(insuranceChips, table.TrayPosition, chipTime, true, Part);
                    roundNet -= engine.InsuranceBet;
                    insuranceChips.Clear();
                }
                break;

            case TableEventType.RoundOver:
                break;
        }
        bank.Set(engine.Chips);
        RaiseChanged();
    }

    // A split: the second card moves to the new hand, the new bet appears, everything slides to the new layout.
    private void SplitVisuals(int source, int index, int bet)
    {
        handCards.Insert(index, new List<BlackjackTable.CardVisual>());
        betChips.Insert(index, new List<GameObject>());
        List<BlackjackTable.CardVisual> from = handCards[source];
        if (from.Count > 1)
        {
            handCards[index].Add(from[from.Count - 1]);
            from.RemoveAt(from.Count - 1);
        }
        betChips[index] = table.SpawnChips(bet, table.TrayPosition + Vector3.up * 0.2f);
        // everything to the new layout
        int n = VisualHands;
        Begin(1);
        for (int h = 0; h < handCards.Count; h++)
        {
            for (int i = 0; i < handCards[h].Count; i++)
                table.MoveCard(handCards[h][i], table.PlayerCardPosition(h, n, i), true, cardSlideTime, 0.03f);
            table.MoveChips(betChips[h], table.BetPosition(h, n, 0), cardSlideTime, false);
        }
        waitTimer = cardSlideTime + 0.05f;
        Part();
    }

    // Pays / takes the chips of one hand (winnings come from the tray).
    private void Settle(int hand, HandOutcome outcome, int paidBack)
    {
        BlackjackHand h = engine.Hands[hand];
        roundNet += paidBack - h.Bet;
        List<GameObject> pile = hand < betChips.Count ? betChips[hand] : new List<GameObject>();
        Begin(1);
        switch (outcome)
        {
            case HandOutcome.Win:
            case HandOutcome.Blackjack:
            {
                List<GameObject> won = table.SpawnChips(paidBack - h.Bet, table.TrayPosition);
                table.MoveChips(won, table.BetPosition(hand, VisualHands, 2), chipTime, false, Part);
                returnChips.AddRange(won);
                break;
            }
            case HandOutcome.Push:
                waitTimer = 0.3f;
                Part();
                break;
            case HandOutcome.Surrender:
            {
                table.MoveChips(pile, table.TrayPosition, chipTime, true, Part);
                pile.Clear();
                returnChips.AddRange(table.SpawnChips(paidBack, table.BetPosition(hand, VisualHands, 0)));
                break;
            }
            default:   // lose / bust: the house takes the bet
                table.MoveChips(pile, table.TrayPosition, chipTime, true, Part);
                pile.Clear();
                break;
        }
    }

    // What she says at the end of a round (blackjack, bust, a winning streak...).
    private void RoundOverLine()
    {
        bool dealerBj = engine.Dealer.Count == 2 && engine.DealerTotal == 21;
        bool playerBj = engine.Hands.Count == 1 && engine.Hands[0].Outcome == HandOutcome.Blackjack;
        bool allBust = engine.Hands.TrueForAll(x => x.Outcome == HandOutcome.Bust);
        winStreak = roundNet > 0 ? winStreak + 1 : roundNet < 0 ? 0 : winStreak;

        if (playerBj) voice.Say(PartnerLine.BjPlayerBlackjack);
        else if (dealerBj) voice.Say(PartnerLine.BjDealerBlackjack);
        else if (allBust) voice.Say(PartnerLine.BjPlayerBust);
        else if (roundNet > 0 && winStreak >= 3) voice.Say(PartnerLine.BjCountingCards);
        else if (roundNet > 0 && engine.DealerTotal > 21) voice.Say(PartnerLine.BjDealerBust);
        else if (roundNet > 0) voice.Say(PartnerLine.BjPlayerWins);
        else if (roundNet < 0) voice.Say(PartnerLine.BjHouseWins);
        else voice.Say(PartnerLine.BjPush);
    }

    // ------------------------------------------------------------------ helpers
    // hands on the table now (the engine may already be further: it runs ahead of the animations)
    private int VisualHands => Mathf.Max(1, handCards.Count);

    // Makes room for a hand index in the visual lists.
    private void EnsureHand(int hand)
    {
        while (handCards.Count <= hand) handCards.Add(new List<BlackjackTable.CardVisual>());
        while (betChips.Count <= hand) betChips.Add(new List<GameObject>());
    }

    // every card to the discard holder
    private void Sweep(Action done)
    {
        var all = new List<BlackjackTable.CardVisual>(dealerCards);
        foreach (List<BlackjackTable.CardVisual> h in handCards) all.AddRange(h);
        dealerCards.Clear();
        handCards.Clear();
        SweepCards(all, done);
    }

    // Changes state; resets the idle nag; tells the HUD.
    private void SetState(State s)
    {
        state = s;
        OnStateChanged();
    }
}
