using System;
using System.Collections.Generic;
using UnityEngine;

// What every card game at a BlackjackTable shares (BlackjackGame, HoldemGame): Helena sits as the dealer,
// the table camera comes on, she picks up the deck, the operator plays by command, she puts the deck back and gets up.
// This class runs that seat / camera / deck routine, the animation steps (Begin / Part), the idle nag,
// card dealing and chip drops. Each game keeps its own State, rules engine and round logic, and maps its
// State onto the shared TableStage so the routine here knows where the game is.
public abstract class CardTableGame : MonoBehaviour, ISeatActivity
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    // Reached through the game classes too (BlackjackGame.TableField, HoldemGame.TableField...).
    public const string TableField = nameof(table);
    public const string BankField = nameof(bank);
    public const string SeatUserField = nameof(seatUser);
    public const string DealerField = nameof(dealer);
    public const string VoiceField = nameof(voice);
    public const string EncounterField = nameof(encounter);
    public const string CameraSystemField = nameof(cameraSystem);

    // The part of the flow every table game has: seating, opening, betting, a round being played, "another round?", leaving.
    protected enum TableStage { Off, Joining, Opening, Betting, InRound, AskAgain, Leaving }

    // The table (card/chip spots, deck, seat), the operator's chips, Helena's seat routine, her dealer animations and her voice.
    [SerializeField] protected BlackjackTable table;
    [SerializeField] protected ChipBank bank;
    [SerializeField] protected PartnerSaveUser seatUser;
    [SerializeField] protected PartnerBlackjackDealer dealer;
    [SerializeField] protected PartnerVoice voice;
    [Tooltip("Optional: the game stops when a fight starts.")]
    [SerializeField] protected CombatEncounter encounter;
    [Tooltip("Optional: the operator's view jumps to the table camera while the game is on.")]
    [SerializeField] protected SecurityCameraSystem cameraSystem;

    // Animation timing (card slide, chip move), how long the operator can wait before she nags, and her pity chips.
    [Header("Pacing")]
    [SerializeField] protected float cardSlideTime = 0.35f;
    [SerializeField] protected float chipTime = 0.4f;
    [Tooltip("She says something if the operator takes this long to decide (s).")]
    [SerializeField] private float idleNag = 25f;
    [Tooltip("Chips she gives when the operator is broke.")]
    [SerializeField] private int pityChips = 100;

    // A table step is running (an animation in parts: see Begin / Part) and how many parts are left.
    protected bool stepBusy;
    private int stepParts;
    // Pause before the next event, and the idle timer for her "your turn" nag (once per wait).
    protected float waitTimer;
    private float idleTimer;
    private bool nagged;
    // The card in her hand right now (follows her hand in LateUpdate) and whether she holds the deck.
    protected BlackjackTable.CardVisual cardInHand;
    private bool deckInHand;
    // The security camera that was on before the table camera took over (restored after).
    private SecurityCamera cameraBefore;
    // Where the deck rests on the table (put back there when the game ends).
    private Vector3 deckHomeLocal;
    private Quaternion deckHomeRot;

    // Raised when anything shown changes (the HUD refreshes).
    public event Action Changed;

    // State read by the HUD, the commands and her animations.
    public bool IsActive => Stage != TableStage.Off;
    public bool IsBetweenRounds
    {
        get
        {
            TableStage s = Stage;
            return s == TableStage.Betting || s == TableStage.AskAgain || s == TableStage.Opening || s == TableStage.Joining;
        }
    }
    // The table this game is played at (seat, card / chip spots).
    public BlackjackTable Table => table;
    // The operator's chips right now.
    public int Chips => bank.Chips;
    // She sits with the dealer clips (ISeatActivity).
    public PartnerAnimator.SeatAnimation SeatAnimation => PartnerAnimator.SeatAnimation.Dealer;
    // The table limits for one bet (blackjack: the bet; Hold'em: the ante).
    public abstract int MinBet { get; }
    public abstract int MaxBet { get; }

    // Operator commands every table game answers: a bet, "yes / deal / again", "no".
    public abstract void Bet(int amount);
    public abstract void Continue();
    public abstract void Decline();

    // ------------------------------------------------------------------ what each game provides
    // Where the game's own State is, as a TableStage (every round state = InRound).
    protected abstract TableStage Stage { get; }
    // Sets the game's State for a shared stage (only Off, Joining, Opening, Betting and Leaving are asked for).
    protected abstract void EnterStage(TableStage stage);
    // States where the game waits for the operator (the idle nag runs there).
    protected abstract bool WaitingForPlayer { get; }
    // Below this many chips she spots the operator some (blackjack: the minimum bet; Hold'em: an ante plus its call).
    protected abstract int BettingFloor { get; }
    // Random tilt of a dealt card on the felt (degrees, both ways).
    protected abstract float CardTilt { get; }

    // Makes the rules engine the first time the game is joined.
    protected abstract void EnsureEngine();
    // Calls off the round in the engine (bets back) and syncs the operator's chips; nothing when there is no engine yet.
    protected abstract void CancelRound();
    // Gives the engine the operator's chip count (before betting).
    protected abstract void SyncEngineChips();
    // Forgets the game's card and chip lists (the table objects are already cleared).
    protected abstract void ClearRoundObjects();
    // One frame of the round once no step is running and no pause is left (plays the next engine event, ends the round...).
    protected abstract void StepRound();

    // Checks the references and remembers where the deck rests.
    protected void Awake()
    {
        if (table == null || bank == null || seatUser == null || dealer == null || voice == null)
        {
            Debug.LogError($"{name}: {GetType().Name} needs the table, ChipBank, PartnerSaveUser, PartnerBlackjackDealer and PartnerVoice.", this);
            enabled = false;
            return;
        }
        if (table.Deck != null) { deckHomeLocal = table.Deck.localPosition; deckHomeRot = table.Deck.localRotation; }
    }

    // A fight ends the game (bets go back).
    protected void OnEnable() { if (encounter != null) encounter.CombatStarted += OnCombatStarted; }
    protected void OnDisable() { if (encounter != null) encounter.CombatStarted -= OnCombatStarted; }

    // Combat started near the table: she leaves at once.
    private void OnCombatStarted() { if (IsActive) ForceLeave(); }

    // ------------------------------------------------------------------ joining / leaving
    // "Let's play": she walks to the dealer seat (false = the game is off or the seat is taken).
    public bool Join()
    {
        if (!enabled) return false;
        if (IsActive) return true;
        EnsureEngine();
        if (!seatUser.UseSeat(table.Seat, this)) return false;
        EnterStage(TableStage.Joining);
        return true;
    }

    // Seated, chair pushed in: table camera on, feet under the table, she picks up the deck, then "place your bet".
    public void OnSeated()
    {
        TableCameraOn();
        dealer.PlantFeet(table.transform);
        EnterStage(TableStage.Opening);
        stepBusy = true;
        dealer.PickUpDeck(() => deckInHand = true, () =>
        {
            stepBusy = false;
            dealer.Hold();
            EnterBetting(true);
        });
    }

    // She walked away from the table (or never got there): bets back, table cleared, deck home, camera back.
    public void OnSeatLeft()
    {
        CancelRound();
        dealer.Stop();
        dealer.ReleaseFeet();
        ClearTable();
        ReturnDeck();
        TableCameraOff();
        EnterStage(TableStage.Off);
    }

    // The game "knows": a camera at the player's seat, facing her, appears and the view cuts to it.
    private void TableCameraOn()
    {
        SecurityCamera cam = table.TableCamera;
        if (cam == null || cameraSystem == null) return;
        cameraBefore = cameraSystem.Active;
        cam.gameObject.SetActive(true);
        cameraSystem.Switch(cam);
    }

    // The previous camera view comes back; the table camera hides again.
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

    // "That's enough": only between rounds.
    public bool Quit()
    {
        if (!IsActive) return false;
        if (Stage == TableStage.Joining) { seatUser.Abort(); return true; }
        if (!IsBetweenRounds) { voice.Say(PartnerLine.BjFinishHand); return false; }
        Leave(true);
        return true;
    }

    // A fight, a load...: the round is called off (bets back) and she gets up at once.
    public void ForceLeave()
    {
        if (!IsActive) return;
        TableStage s = Stage;
        if (s == TableStage.Joining || s == TableStage.Leaving) { if (s == TableStage.Joining) seatUser.Abort(); return; }
        CancelRound();
        ClearTable();
        Leave(false);
    }

    // She puts the deck back and gets up (optionally saying goodbye).
    private void Leave(bool sayGoodbye)
    {
        EnterStage(TableStage.Leaving);
        if (sayGoodbye) voice.Say(PartnerLine.BjGameOver);
        stepBusy = true;
        dealer.PutBackDeck(ReturnDeck, () => { stepBusy = false; dealer.Idle(); dealer.ReleaseFeet(); seatUser.LeaveSeat(); });
    }

    // ------------------------------------------------------------------ round flow
    // A new round can start now (betting or "another round?"); otherwise she says why not.
    protected bool CanStartRound()
    {
        TableStage s = Stage;
        if (s == TableStage.Betting || s == TableStage.AskAgain) return true;
        if (s == TableStage.Opening || s == TableStage.Joining) voice.Say(PartnerLine.Busy);
        else if (s != TableStage.Off && s != TableStage.Leaving) voice.Say(PartnerLine.BjFinishHand);
        return false;
    }

    // Waiting for a bet ("place your bet"); a broke player gets some chips from her.
    protected void EnterBetting(bool sayIt)
    {
        if (bank.Chips < BettingFloor)
        {
            // broke: she spots the operator some chips
            bank.Add(pityChips);
            voice.Say(PartnerLine.BjBroke);
        }
        else if (sayIt) voice.Say(PartnerLine.BjPlaceBet);
        SyncEngineChips();
        EnterStage(TableStage.Betting);
    }

    // The game loop: idle nag, then (once no step is running and the pause is over) the game's round step.
    protected void Update()
    {
        TableStage s = Stage;
        if (s == TableStage.Off || s == TableStage.Joining || s == TableStage.Leaving) return;

        if (WaitingForPlayer)
        {
            idleTimer += Time.deltaTime;
            if (!nagged && idleTimer > idleNag) { nagged = true; voice.Say(PartnerLine.BjIdle); }
        }

        if (stepBusy) return;
        if (waitTimer > 0f) { waitTimer -= Time.deltaTime; return; }

        StepRound();
    }

    // ------------------------------------------------------------------ table helpers
    // One card: out of the shoe into her hand, dealt with the clip, slid to its place at the release point.
    protected void Deal(Card card, Vector3 place, bool faceUp, List<BlackjackTable.CardVisual> into)
    {
        // a face-down card has no face until it is revealed (it can't flash its value while in her hand)
        BlackjackTable.CardVisual v = table.SpawnCard(card, table.ShoePosition, table.CardRotation(false), false, !faceUp);
        into.Add(v);
        cardInHand = v;
        Begin(2);
        dealer.DealCard(place,
            () => { cardInHand = null; table.MoveCard(v, place, faceUp, cardSlideTime, 0.04f, Part, UnityEngine.Random.Range(-CardTilt, CardTilt)); },
            () => { dealer.Hold(); Part(); });
    }

    // Chips dropped onto a spot (from a little above it).
    protected List<GameObject> Drop(int amount, Vector3 at)
    {
        List<GameObject> pile = table.SpawnChips(amount, at + Vector3.up * 0.25f);
        Begin(1);
        table.MoveChips(pile, at, chipTime, false, Part);
        return pile;
    }

    // These cards to the discard holder (hidden there); done runs when the last one arrives (at once if none).
    protected void SweepCards(List<BlackjackTable.CardVisual> all, Action done)
    {
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

    // Removes every card and chip from the table and resets the step state.
    protected void ClearTable()
    {
        table.ClearAll();
        ClearRoundObjects();
        cardInHand = null;
        stepBusy = false;
        stepParts = 0;
        waitTimer = 0f;
    }

    // Puts the deck back on its spot on the table.
    private void ReturnDeck()
    {
        deckInHand = false;
        if (table.Deck == null) return;
        table.Deck.SetParent(table.transform, false);
        table.Deck.localPosition = deckHomeLocal;
        table.Deck.localRotation = deckHomeRot;
    }

    // A step made of several parts (clip + card slide...): the next event waits for all of them.
    protected void Begin(int parts) { stepBusy = true; stepParts += parts; }
    protected void Part() { if (--stepParts <= 0) { stepParts = 0; stepBusy = false; } Changed?.Invoke(); }

    // The game's State changed: resets the idle nag and tells the HUD.
    protected void OnStateChanged()
    {
        idleTimer = 0f;
        nagged = false;
        Changed?.Invoke();
    }

    // Something shown changed (the HUD refreshes).
    protected void RaiseChanged() => Changed?.Invoke();

    // After the Animator: the card and the deck follow her hands.
    protected void LateUpdate()
    {
        if (cardInHand != null && dealer.TryGetCardInHand(out Vector3 p, out Quaternion r))
            cardInHand.Transform.SetPositionAndRotation(p, r);
        if (deckInHand && table.Deck != null && dealer.TryGetDeckInHand(out Vector3 dp, out Quaternion dr))
            table.Deck.SetPositionAndRotation(dp, dr);
    }
}
