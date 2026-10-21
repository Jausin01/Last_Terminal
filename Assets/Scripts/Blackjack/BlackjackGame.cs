using System;
using System.Collections.Generic;
using UnityEngine;

// A game of blackjack at one table: the operator plays (by command), Helena deals.
//   "let's play" -> she sits as the dealer (save point seat routine) -> takes the deck -> "place your bet"
//   bet -> she deals (one deal clip per card, the card slides from her hand to its place) -> insurance? -> peek
//   -> hit / stand / double / split / surrender -> her hand -> chips paid / taken -> "another round?"
//   between rounds: "yes" / a new bet = next round, "no" / "that's enough" = she puts the deck back and gets up.
// The rules live in BlackjackEngine; this plays its events one by one with the animations, cards and chips.
public class BlackjackGame : MonoBehaviour, ISeatActivity
{
    public const string TableField = nameof(table);
    public const string BankField = nameof(bank);
    public const string SeatUserField = nameof(seatUser);
    public const string DealerField = nameof(dealer);
    public const string VoiceField = nameof(voice);
    public const string EncounterField = nameof(encounter);
    public const string CameraSystemField = nameof(cameraSystem);

    public enum State { Off, Joining, Opening, Betting, Playing, Insurance, Deciding, RoundEnd, AskAgain, Leaving }
    public enum Decision { Hit, Stand, Double, Split, Surrender }

    [SerializeField] private BlackjackTable table;
    [SerializeField] private ChipBank bank;
    [SerializeField] private PartnerSaveUser seatUser;
    [SerializeField] private PartnerBlackjackDealer dealer;
    [SerializeField] private PartnerVoice voice;
    [Tooltip("Optional: the game stops when a fight starts.")]
    [SerializeField] private CombatEncounter encounter;
    [Tooltip("Optional: the operator's view jumps to the table camera while the game is on.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;

    [Header("Rules")]
    [SerializeField, Range(1, 8)] private int decks = 6;
    [SerializeField] private bool dealerHitsSoft17;
    [SerializeField] private int minBet = 5;
    [SerializeField] private int maxBet = 500;
    [SerializeField] private bool lateSurrender = true;
    [SerializeField, Range(2, 4)] private int maxHands = 4;

    [Header("Pacing")]
    [SerializeField] private float cardSlideTime = 0.35f;
    [SerializeField] private float chipTime = 0.4f;
    [SerializeField] private float pauseAfterRound = 1.4f;
    [Tooltip("She says something if the operator takes this long to decide (s).")]
    [SerializeField] private float idleNag = 25f;
    [SerializeField] private int defaultBet = 10;
    [Tooltip("Chips she gives when the operator is broke.")]
    [SerializeField] private int pityChips = 100;

    private BlackjackEngine engine;
    private State state;
    private bool stepBusy;
    private int stepParts;
    private float waitTimer;
    private float idleTimer;
    private bool nagged;
    private bool firstDecision;
    private bool shuffledOnce;
    private int winStreak;
    private int roundNet;
    private Decision? pending;

    private readonly List<List<BlackjackTable.CardVisual>> handCards = new List<List<BlackjackTable.CardVisual>>();
    private readonly List<BlackjackTable.CardVisual> dealerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<List<GameObject>> betChips = new List<List<GameObject>>();
    private readonly List<GameObject> insuranceChips = new List<GameObject>();
    private readonly List<GameObject> returnChips = new List<GameObject>();
    private BlackjackTable.CardVisual cardInHand;
    private bool deckInHand;
    private SecurityCamera cameraBefore;
    private Vector3 deckHomeLocal;
    private Quaternion deckHomeRot;

    public event Action Changed;

    public bool IsActive => state != State.Off;
    public bool IsBetweenRounds => state == State.Betting || state == State.AskAgain || state == State.Opening || state == State.Joining;
    public State CurrentState => state;
    public BlackjackEngine Engine => engine;
    public BlackjackTable Table => table;
    public int Chips => bank.Chips;
    public int MinBet => minBet;
    public int MaxBet => maxBet;
    public PartnerAnimator.SeatAnimation SeatAnimation => PartnerAnimator.SeatAnimation.Dealer;

    // what is on the table now (the HUD shows the cards once they are dealt, not before)
    public int ShownHandCount => handCards.Count;
    public int ActiveHandIndex => state == State.Deciding && engine != null ? engine.Active : -1;

    public void GetShownDealer(List<Card> cards, List<bool> faceUp)
    {
        cards.Clear(); faceUp.Clear();
        foreach (BlackjackTable.CardVisual c in dealerCards)
        {
            if (c == cardInHand) continue;
            cards.Add(c.Card); faceUp.Add(c.FaceUp);
        }
    }

    public void GetShownHand(int hand, List<Card> cards)
    {
        cards.Clear();
        if (hand < 0 || hand >= handCards.Count) return;
        foreach (BlackjackTable.CardVisual c in handCards[hand])
            if (c != cardInHand) cards.Add(c.Card);
    }

    private void Awake()
    {
        if (table == null || bank == null || seatUser == null || dealer == null || voice == null)
        {
            Debug.LogError($"{name}: BlackjackGame needs the table, ChipBank, PartnerSaveUser, PartnerBlackjackDealer and PartnerVoice.", this);
            enabled = false;
            return;
        }
        if (table.Deck != null) { deckHomeLocal = table.Deck.localPosition; deckHomeRot = table.Deck.localRotation; }
    }

    private void OnEnable() { if (encounter != null) encounter.CombatStarted += OnCombatStarted; }
    private void OnDisable() { if (encounter != null) encounter.CombatStarted -= OnCombatStarted; }

    private void OnCombatStarted() { if (IsActive) ForceLeave(); }

    // ------------------------------------------------------------------ joining / leaving
    public bool Join()
    {
        if (!enabled) return false;
        if (IsActive) return true;
        if (engine == null)
            engine = new BlackjackEngine(new BlackjackRules
            {
                Decks = decks, DealerHitsSoft17 = dealerHitsSoft17, MinBet = minBet, MaxBet = maxBet,
                LateSurrender = lateSurrender, MaxHands = maxHands,
            }, bank.Chips, Environment.TickCount);
        if (!seatUser.UseSeat(table.Seat, this)) return false;
        SetState(State.Joining);
        return true;
    }

    // seated, chair pushed in
    public void OnSeated()
    {
        TableCameraOn();
        dealer.PlantFeet(table.transform);
        SetState(State.Opening);
        stepBusy = true;
        dealer.PickUpDeck(() => deckInHand = true, () =>
        {
            stepBusy = false;
            dealer.Hold();
            EnterBetting(true);
        });
    }

    // she walked away from the table (or never got there)
    public void OnSeatLeft()
    {
        if (engine != null) engine.CancelRound();
        if (engine != null) bank.Set(engine.Chips);
        dealer.Stop();
        dealer.ReleaseFeet();
        ClearTable();
        ReturnDeck();
        TableCameraOff();
        SetState(State.Off);
    }

    // the game "knows": a camera at the player's seat, facing her, appears and the view cuts to it
    private void TableCameraOn()
    {
        SecurityCamera cam = table.TableCamera;
        if (cam == null || cameraSystem == null) return;
        cameraBefore = cameraSystem.Active;
        cam.gameObject.SetActive(true);
        cameraSystem.Switch(cam);
    }

    private void TableCameraOff()
    {
        SecurityCamera cam = table.TableCamera;
        if (cam == null || cameraSystem == null || !cam.gameObject.activeSelf) return;
        if (cameraSystem.Active == cam)
        {
            if (cameraBefore != null && cameraBefore != cam) cameraSystem.Switch(cameraBefore);
            else cameraSystem.Switch(0);
        }
        cam.gameObject.SetActive(false);
    }

    // "that's enough": only between rounds
    public bool Quit()
    {
        if (!IsActive) return false;
        if (state == State.Joining) { seatUser.Abort(); return true; }
        if (!IsBetweenRounds) { voice.Say(PartnerLine.BjFinishHand); return false; }
        Leave(true);
        return true;
    }

    // a fight, a load...: the round is called off (bets back) and she gets up at once
    public void ForceLeave()
    {
        if (!IsActive) return;
        if (state == State.Joining || state == State.Leaving) { if (state == State.Joining) seatUser.Abort(); return; }
        engine.CancelRound();
        bank.Set(engine.Chips);
        ClearTable();
        Leave(false);
    }

    private void Leave(bool sayGoodbye)
    {
        SetState(State.Leaving);
        if (sayGoodbye) voice.Say(PartnerLine.BjGameOver);
        stepBusy = true;
        dealer.PutBackDeck(ReturnDeck, () => { stepBusy = false; dealer.Idle(); dealer.ReleaseFeet(); seatUser.LeaveSeat(); });
    }

    // ------------------------------------------------------------------ operator commands
    public void Bet(int amount)
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
    public void Continue()
    {
        if (state == State.Insurance) { Insurance(true); return; }
        if (state == State.Deciding || state == State.Playing) { Decide(Decision.Hit); return; }
        if (!CanStartRound()) return;
        int bet = engine.LastBet > 0 ? engine.LastBet : defaultBet;
        bet = Mathf.Clamp(Mathf.Min(bet, bank.Chips), minBet, maxBet);
        Bet(bet);
    }

    // "no" between rounds = leave; to insurance = decline
    public void Decline()
    {
        if (state == State.Insurance) { Insurance(false); return; }
        if (IsBetweenRounds) { Quit(); return; }
        voice.Say(PartnerLine.BjFinishHand);
    }

    public void Insurance(bool take)
    {
        if (state != State.Insurance) { if (take) voice.Say(PartnerLine.DontHaveThat); return; }
        if (take && !engine.CanInsure) { voice.Say(PartnerLine.BjNotEnough); take = false; }
        engine.AnswerInsurance(take);
        SetState(State.Playing);
    }

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
    private bool CanStartRound()
    {
        if (state == State.Betting || state == State.AskAgain) return true;
        if (state == State.Opening || state == State.Joining) voice.Say(PartnerLine.Busy);
        else if (state != State.Off && state != State.Leaving) voice.Say(PartnerLine.BjFinishHand);
        return false;
    }

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

    private void EnterBetting(bool sayIt)
    {
        if (bank.Chips < minBet)
        {
            // broke: she spots the operator some chips
            bank.Add(pityChips);
            voice.Say(PartnerLine.BjBroke);
        }
        else if (sayIt) voice.Say(PartnerLine.BjPlaceBet);
        engine.SetChips(bank.Chips);
        SetState(State.Betting);
    }

    private void Update()
    {
        if (state == State.Off || state == State.Joining || state == State.Leaving) return;

        if (state == State.Betting || state == State.Deciding || state == State.Insurance || state == State.AskAgain)
        {
            idleTimer += Time.deltaTime;
            if (!nagged && idleTimer > idleNag) { nagged = true; voice.Say(PartnerLine.BjIdle); }
        }

        if (stepBusy) return;
        if (waitTimer > 0f) { waitTimer -= Time.deltaTime; return; }

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

    private void AskAgain()
    {
        bank.Set(engine.Chips);
        if (bank.Chips < minBet) { EnterBetting(false); return; }
        SetState(State.AskAgain);
        voice.Say(PartnerLine.BjAnotherRound);
    }

    // ------------------------------------------------------------------ events -> table
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
        Changed?.Invoke();
    }

    // one card: out of the shoe into her hand, dealt with the clip, slid to its place at the release point
    private void Deal(Card card, Vector3 place, bool faceUp, List<BlackjackTable.CardVisual> into)
    {
        // her hole card has no face until it is revealed (it can't flash its value while in her hand)
        BlackjackTable.CardVisual v = table.SpawnCard(card, table.ShoePosition, table.CardRotation(false), false, !faceUp);
        into.Add(v);
        cardInHand = v;
        Begin(2);
        dealer.DealCard(place,
            () => { cardInHand = null; table.MoveCard(v, place, faceUp, cardSlideTime, 0.04f, Part, UnityEngine.Random.Range(-6f, 6f)); },
            () => { dealer.Hold(); Part(); });
    }

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

    private void EnsureHand(int hand)
    {
        while (handCards.Count <= hand) handCards.Add(new List<BlackjackTable.CardVisual>());
        while (betChips.Count <= hand) betChips.Add(new List<GameObject>());
    }

    // chips dropped onto a spot (from a little above it)
    private List<GameObject> Drop(int amount, Vector3 at)
    {
        List<GameObject> pile = table.SpawnChips(amount, at + Vector3.up * 0.25f);
        Begin(1);
        table.MoveChips(pile, at, chipTime, false, Part);
        return pile;
    }

    // every card to the discard holder
    private void Sweep(Action done)
    {
        var all = new List<BlackjackTable.CardVisual>(dealerCards);
        foreach (List<BlackjackTable.CardVisual> h in handCards) all.AddRange(h);
        dealerCards.Clear();
        handCards.Clear();
        if (all.Count == 0) { done(); return; }
        int left = all.Count;
        foreach (BlackjackTable.CardVisual c in all)
        {
            BlackjackTable.CardVisual card = c;
            table.MoveCard(card, table.DiscardPosition, false, 0.45f, 0.08f, () =>
            {
                table.HideCard(card);
                if (--left == 0) done();
            });
        }
    }

    private void ClearTable()
    {
        table.ClearAll();
        dealerCards.Clear();
        handCards.Clear();
        betChips.Clear();
        insuranceChips.Clear();
        returnChips.Clear();
        cardInHand = null;
        stepBusy = false;
        stepParts = 0;
        waitTimer = 0f;
    }

    private void ReturnDeck()
    {
        deckInHand = false;
        if (table.Deck == null) return;
        table.Deck.SetParent(table.transform, false);
        table.Deck.localPosition = deckHomeLocal;
        table.Deck.localRotation = deckHomeRot;
    }

    // a step made of several parts (clip + card slide...): the next event waits for all of them
    private void Begin(int parts) { stepBusy = true; stepParts += parts; }
    private void Part() { if (--stepParts <= 0) { stepParts = 0; stepBusy = false; } Changed?.Invoke(); }

    private void SetState(State s)
    {
        state = s;
        idleTimer = 0f;
        nagged = false;
        Changed?.Invoke();
    }

    // after the Animator: the card and the deck follow her hands
    private void LateUpdate()
    {
        if (cardInHand != null && dealer.TryGetCardInHand(out Vector3 p, out Quaternion r))
            cardInHand.Transform.SetPositionAndRotation(p, r);
        if (deckInHand && table.Deck != null && dealer.TryGetDeckInHand(out Vector3 dp, out Quaternion dr))
            table.Deck.SetPositionAndRotation(dp, dr);
    }
}
