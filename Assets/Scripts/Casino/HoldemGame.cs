using System;
using System.Collections.Generic;
using UnityEngine;

// A game of Casino Hold'em at one table (same table, seat, deck, cards and chips as blackjack: BlackjackTable).
// The operator plays, Helena deals:
//   "let's play poker" -> she sits as the dealer -> takes the deck -> "place your bet" (the ante)
//   ante -> two cards each (hers face down) + the flop -> "call or fold?"
//   call (2x the ante) -> turn + river, she shows her cards -> paid / taken (HoldemEngine has the rules)
//   fold -> the ante is lost.  Between rounds: "yes" / a new ante = next round, "that's enough" = she gets up.
public class HoldemGame : MonoBehaviour, ISeatActivity
{
    public const string TableField = nameof(table);
    public const string BankField = nameof(bank);
    public const string SeatUserField = nameof(seatUser);
    public const string DealerField = nameof(dealer);
    public const string VoiceField = nameof(voice);
    public const string EncounterField = nameof(encounter);
    public const string CameraSystemField = nameof(cameraSystem);

    public enum State { Off, Joining, Opening, Betting, Dealing, Deciding, RoundEnd, AskAgain, Leaving }

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
    [SerializeField] private int minAnte = 5;
    [SerializeField] private int maxAnte = 200;
    [SerializeField] private int defaultAnte = 10;
    [Tooltip("Chips she gives when the operator is broke.")]
    [SerializeField] private int pityChips = 100;

    [Header("Layout (table space, felt at 0.751: the cards side by side, the board between her and the player)")]
    [SerializeField] private Vector3 board = new Vector3(0f, 0.751f, 0.71f);
    [SerializeField] private float boardSpacing = 0.075f;
    [SerializeField] private Vector3 playerHand = new Vector3(0f, 0.751f, 0.92f);
    [SerializeField] private float handSpacing = 0.07f;
    [SerializeField] private Vector3 anteSpot = new Vector3(-0.06f, 0.751f, 1.04f);
    [SerializeField] private Vector3 callSpot = new Vector3(0.06f, 0.751f, 1.04f);

    [Header("Pacing")]
    [SerializeField] private float cardSlideTime = 0.35f;
    [SerializeField] private float chipTime = 0.4f;
    [SerializeField] private float pauseAfterRound = 1.6f;
    [SerializeField] private float idleNag = 25f;

    private HoldemEngine engine;
    private State state;
    private bool stepBusy;
    private int stepParts;
    private float waitTimer;
    private float idleTimer;
    private bool nagged;
    private bool dealerShown;

    private readonly List<BlackjackTable.CardVisual> playerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<BlackjackTable.CardVisual> dealerCards = new List<BlackjackTable.CardVisual>();
    private readonly List<BlackjackTable.CardVisual> boardCards = new List<BlackjackTable.CardVisual>();
    private readonly List<GameObject> anteChips = new List<GameObject>();
    private readonly List<GameObject> callChips = new List<GameObject>();
    private BlackjackTable.CardVisual cardInHand;
    private bool deckInHand;
    private SecurityCamera cameraBefore;
    private Vector3 deckHomeLocal;
    private Quaternion deckHomeRot;

    public event Action Changed;

    public bool IsActive => state != State.Off;
    public bool IsBetweenRounds => state == State.Betting || state == State.AskAgain || state == State.Opening || state == State.Joining;
    public State CurrentState => state;
    public HoldemEngine Engine => engine;
    public BlackjackTable Table => table;
    public int Chips => bank.Chips;
    public int MinBet => minAnte;
    public int MaxBet => maxAnte;
    public bool DealerShown => dealerShown;   // her cards are face up (the HUD names her hand)
    public int ShownPlayerCards => CountShown(playerCards);
    public int ShownBoardCards => CountShown(boardCards);
    public PartnerAnimator.SeatAnimation SeatAnimation => PartnerAnimator.SeatAnimation.Dealer;

    private int CountShown(List<BlackjackTable.CardVisual> list)
    {
        int n = 0;
        foreach (BlackjackTable.CardVisual c in list) if (c != cardInHand) n++;
        return n;
    }

    private void Awake()
    {
        if (table == null || bank == null || seatUser == null || dealer == null || voice == null)
        {
            Debug.LogError($"{name}: HoldemGame needs the table, ChipBank, PartnerSaveUser, PartnerBlackjackDealer and PartnerVoice.", this);
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
        if (engine == null) engine = new HoldemEngine(minAnte, maxAnte, bank.Chips, Environment.TickCount);
        if (!seatUser.UseSeat(table.Seat, this)) return false;
        SetState(State.Joining);
        return true;
    }

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

    public void OnSeatLeft()
    {
        if (engine != null) { engine.CancelRound(); bank.Set(engine.Chips); }
        dealer.Stop();
        dealer.ReleaseFeet();
        ClearTable();
        ReturnDeck();
        TableCameraOff();
        SetState(State.Off);
    }

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

    public bool Quit()
    {
        if (!IsActive) return false;
        if (state == State.Joining) { seatUser.Abort(); return true; }
        if (!IsBetweenRounds) { voice.Say(PartnerLine.BjFinishHand); return false; }
        Leave(true);
        return true;
    }

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
    public void Bet(int ante)
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
    public void Continue()
    {
        if (state == State.Deciding) { Call(); return; }
        if (!CanStartRound()) return;
        int ante = engine.LastAnte > 0 ? engine.LastAnte : defaultAnte;
        ante = Mathf.Clamp(Mathf.Min(ante, bank.Chips / 3), minAnte, maxAnte);
        Bet(ante);
    }

    // "no": between rounds = leave; while deciding = fold
    public void Decline()
    {
        if (state == State.Deciding) { Fold(); return; }
        if (IsBetweenRounds) { Quit(); return; }
        voice.Say(PartnerLine.BjFinishHand);
    }

    public void Call()
    {
        if (state != State.Deciding) { NotNowLine(); return; }
        if (!engine.DoCall()) { voice.Say(PartnerLine.BjNotEnough); return; }
        bank.Set(engine.Chips);
        SetState(State.Dealing);
    }

    public void Fold()
    {
        if (state != State.Deciding) { NotNowLine(); return; }
        if (!engine.Fold()) return;
        voice.Say(PartnerLine.PokerFolded);
        SetState(State.Dealing);
    }

    private void NotNowLine()
    {
        if (state == State.Betting || state == State.AskAgain) voice.Say(PartnerLine.BjPlaceBet);
        else voice.Say(PartnerLine.Busy);
    }

    // ------------------------------------------------------------------ round flow
    private bool CanStartRound()
    {
        if (state == State.Betting || state == State.AskAgain) return true;
        if (state == State.Opening || state == State.Joining) voice.Say(PartnerLine.Busy);
        else if (state != State.Off && state != State.Leaving) voice.Say(PartnerLine.BjFinishHand);
        return false;
    }

    private void EnterBetting(bool sayIt)
    {
        if (bank.Chips < minAnte * 3)
        {
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

        if (state == State.Betting || state == State.Deciding || state == State.AskAgain)
        {
            idleTimer += Time.deltaTime;
            if (!nagged && idleTimer > idleNag) { nagged = true; voice.Say(PartnerLine.BjIdle); }
        }

        if (stepBusy) return;
        if (waitTimer > 0f) { waitTimer -= Time.deltaTime; return; }

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

    private void AskAgain()
    {
        engine.NextRound();
        bank.Set(engine.Chips);
        if (bank.Chips < minAnte * 3) { EnterBetting(false); return; }
        SetState(State.AskAgain);
        voice.Say(PartnerLine.BjAnotherRound);
    }

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
        Changed?.Invoke();
    }

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

    private readonly List<GameObject> toPlayer = new List<GameObject>();

    private void KeepForPlayer(List<GameObject> chips)
    {
        toPlayer.Clear();
        toPlayer.AddRange(chips);
    }

    // one card: out of the shoe into her hand, dealt with the clip, slid to its place at the release point
    private void Deal(Card card, Vector3 place, bool faceUp, List<BlackjackTable.CardVisual> into)
    {
        BlackjackTable.CardVisual v = table.SpawnCard(card, table.ShoePosition, table.CardRotation(false), false, !faceUp);
        into.Add(v);
        cardInHand = v;
        Begin(2);
        dealer.DealCard(place,
            () => { cardInHand = null; table.MoveCard(v, place, faceUp, cardSlideTime, 0.04f, Part, UnityEngine.Random.Range(-4f, 4f)); },
            () => { dealer.Hold(); Part(); });
    }

    private List<GameObject> Drop(int amount, Vector3 at)
    {
        List<GameObject> pile = table.SpawnChips(amount, at + Vector3.up * 0.25f);
        Begin(1);
        table.MoveChips(pile, at, chipTime, false, Part);
        return pile;
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
        playerCards.Clear();
        dealerCards.Clear();
        boardCards.Clear();
        anteChips.Clear();
        callChips.Clear();
        toPlayer.Clear();
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

    private void Begin(int parts) { stepBusy = true; stepParts += parts; }
    private void Part() { if (--stepParts <= 0) { stepParts = 0; stepBusy = false; } Changed?.Invoke(); }

    private void SetState(State s)
    {
        state = s;
        idleTimer = 0f;
        nagged = false;
        Changed?.Invoke();
    }

    private void LateUpdate()
    {
        if (cardInHand != null && dealer.TryGetCardInHand(out Vector3 p, out Quaternion r))
            cardInHand.Transform.SetPositionAndRotation(p, r);
        if (deckInHand && table.Deck != null && dealer.TryGetDeckInHand(out Vector3 dp, out Quaternion dr))
            table.Deck.SetPositionAndRotation(dp, dr);
    }
}
