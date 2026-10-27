using System;
using System.Collections.Generic;

// Blackjack rules, no Unity code (the table / Helena only animate what this produces):
// shoe of N decks with a cut card, player vs dealer, hit / stand / double / split (re-split up to 4 hands,
// split aces get one card), late surrender, insurance (dealer shows an ace), dealer peeks for blackjack with an ace
// or a ten up, dealer stands (or hits) on soft 17, blackjack pays 3:2, insurance 2:1.
// Every change is also queued as a TableEvent, in order, for the presentation.

// Card suits (the order matches the card sprite sheet rows).
public enum Suit { Spades = 0, Hearts = 1, Diamonds = 2, Clubs = 3 }

// One playing card: rank 1 (ace) to 13 (king) and suit.
[Serializable]
public struct Card
{
    public int Rank;        // 1 = ace .. 13 = king
    // Which suit (only shown; blackjack ignores suits).
    public Suit Suit;

    public Card(int rank, Suit suit) { Rank = rank; Suit = suit; }
    // Blackjack value: ace 11 (Value() lowers it to 1 when needed), face cards 10.
    public int Points => Rank == 1 ? 11 : Math.Min(Rank, 10);
    public bool IsAce => Rank == 1;
    public bool IsTen => Rank >= 10;
    public override string ToString() => RankName + "SHDC"[(int)Suit];
    public string RankName => Rank == 1 ? "A" : Rank == 11 ? "J" : Rank == 12 ? "Q" : Rank == 13 ? "K" : Rank.ToString();
}

// House rules of the table (one instance per table, set by BlackjackTable).
public class BlackjackRules
{
    // Number of 52-card decks in the shoe.
    public int Decks = 6;
    public float Penetration = 0.75f;      // reshuffle when this much of the shoe is used
    public bool DealerHitsSoft17;          // false = "dealer must stand on all 17s"
    // Payout multiplier for a natural blackjack (1.5 = 3:2).
    public float BlackjackPays = 1.5f;
    public bool DoubleAfterSplit = true;
    public int MaxHands = 4;
    public bool ResplitAces;
    public bool LateSurrender = true;
    public bool Insurance = true;
    public int MinBet = 5;
    public int MaxBet = 500;
}

// How a hand ended (shown by the HUD and spoken by Helena).
public enum HandOutcome { None, Win, Blackjack, Push, Lose, Bust, Surrender }

// One player hand: its cards, its bet and how it ended. A split makes more of these.
public class BlackjackHand
{
    // The cards in this hand, in the order they were dealt.
    public readonly List<Card> Cards = new List<Card>();
    public int Bet;
    public bool Doubled;
    public bool FromSplit;
    public bool SplitAces;
    public bool Done;
    public bool Surrendered;
    public HandOutcome Outcome;
    public int Payout;          // returned to the player (bet included) when settled

    // Totals and states derived from the cards.
    public int Total => Value(Cards, out _);
    public bool IsSoft { get { Value(Cards, out bool soft); return soft; } }
    public bool IsBust => Total > 21;
    public bool IsBlackjack => Cards.Count == 2 && Total == 21 && !FromSplit;

    // best total (aces 11 while it doesn't bust); soft = an ace still counts 11
    public static int Value(IReadOnlyList<Card> cards, out bool soft)
    {
        int total = 0, aces = 0;
        foreach (Card c in cards) { total += c.Points; if (c.IsAce) aces++; }
        while (total > 21 && aces > 0) { total -= 10; aces--; }
        soft = aces > 0;
        return total;
    }
}

// Where the round is: betting, insurance question, player's hands, dealer's hand, settled.
public enum RoundPhase { Betting, Insurance, PlayerTurn, DealerTurn, RoundOver }

// Everything that happens at the table, in order, for the presentation (BlackjackTable animates them one by one).
public enum TableEventType
{
    Shuffle,            // new shoe
    BetPlaced,          // Hand = hand index, Amount = chips put down
    CardToPlayer,       // Hand, Card
    CardToDealer,       // Card, FaceDown
    InsuranceTaken,     // Amount
    DealerPeek,         // Flag = dealer has blackjack
    HoleRevealed,       // Card
    HandSplit,          // Hand = the new hand index (its first card moves there from Hand - 1... see Source)
    BetDoubled,         // Hand, Amount added
    HandActive,         // Hand (the one the player is playing now)
    HandSettled,        // Hand, Outcome, Amount = paid back (0 = chips taken)
    InsuranceSettled,   // Flag = paid, Amount = paid back
    RoundOver,
}

// One table event; which fields are used depends on Type (see TableEventType).
public struct TableEvent
{
    // What happened.
    public TableEventType Type;
    // The player hand it concerns (index into Hands).
    public int Hand;
    public int Source;          // HandSplit: the hand the card came from
    // The card dealt / revealed.
    public Card Card;
    // Dealt face down (the dealer's hole card).
    public bool FaceDown;
    // Yes/no detail (DealerPeek: dealer has blackjack; InsuranceSettled: insurance paid).
    public bool Flag;
    // Chips involved (bet placed, doubled, paid back).
    public int Amount;
    // HandSettled: how the hand ended.
    public HandOutcome Outcome;
}

// The game itself: one shoe, one player (up to MaxHands after splits) against the dealer. Pure C#: easy to test.
public class BlackjackEngine
{
    // Public state, read by the table / HUD (they never change it directly).
    public readonly BlackjackRules Rules;
    public readonly List<BlackjackHand> Hands = new List<BlackjackHand>();
    public readonly List<Card> Dealer = new List<Card>();
    public readonly Queue<TableEvent> Events = new Queue<TableEvent>();

    // The shuffled shoe, the next card to deal and where the cut card sits.
    private readonly List<Card> shoe = new List<Card>();
    private readonly Random random;
    private int next;
    private int cutIndex;

    // Round state and the player's chips.
    public RoundPhase Phase { get; private set; } = RoundPhase.Betting;
    public int Chips { get; private set; }
    public int Active { get; private set; } = -1;          // hand being played
    // Insurance put down this round (0 = none).
    public int InsuranceBet { get; private set; }
    // The dealer's second card has been turned up.
    public bool HoleRevealed { get; private set; }
    // The last bet placed ("same bet again").
    public int LastBet { get; private set; }

    // Convenience views of the state.
    public BlackjackHand ActiveHand => Active >= 0 && Active < Hands.Count ? Hands[Active] : null;
    public int DealerTotal => BlackjackHand.Value(Dealer, out _);
    public int DealerVisibleTotal => Dealer.Count == 0 ? 0 : HoleRevealed ? DealerTotal : Dealer[0].Points;
    public Card DealerUpCard => Dealer[0];
    public int CardsLeft => shoe.Count - next;

    public BlackjackEngine(BlackjackRules rules, int chips, int seed)
    {
        Rules = rules;
        Chips = chips;
        random = new Random(seed);
        Shuffle();
    }

    // Chip changes from outside (save loading, cashier): SetChips only between rounds.
    public void SetChips(int chips) { if (Phase == RoundPhase.Betting || Phase == RoundPhase.RoundOver) Chips = Math.Max(0, chips); }
    public void AddChips(int amount) => Chips += amount;

    // ------------------------------------------------------------------ round start
    public enum BetResult { Ok, BelowMinimum, AboveMaximum, NotEnoughChips, NotNow }

    // Can this bet start a round now? (limits, chips, phase)
    public BetResult CheckBet(int bet)
    {
        if (Phase != RoundPhase.Betting && Phase != RoundPhase.RoundOver) return BetResult.NotNow;
        if (bet < Rules.MinBet) return BetResult.BelowMinimum;
        if (bet > Rules.MaxBet) return BetResult.AboveMaximum;
        if (bet > Chips) return BetResult.NotEnoughChips;
        return BetResult.Ok;
    }

    // Places the bet and deals two cards to the player and the dealer (the second dealer card face down).
    public BetResult StartRound(int bet)
    {
        BetResult check = CheckBet(bet);
        if (check != BetResult.Ok) return check;

        Hands.Clear();
        Dealer.Clear();
        InsuranceBet = 0;
        HoleRevealed = false;
        Active = -1;
        if (next >= cutIndex) Shuffle();

        LastBet = bet;
        Chips -= bet;
        Hands.Add(new BlackjackHand { Bet = bet });
        Emit(new TableEvent { Type = TableEventType.BetPlaced, Hand = 0, Amount = bet });

        DealTo(0);
        DealDealer(false);
        DealTo(0);
        DealDealer(true);

        if (Rules.Insurance && DealerUpCard.IsAce) { Phase = RoundPhase.Insurance; return BetResult.Ok; }
        AfterInsurance();
        return BetResult.Ok;
    }

    // insurance costs half the bet; pays 2:1 when the dealer has blackjack
    public bool CanInsure => Phase == RoundPhase.Insurance && Chips >= Hands[0].Bet / 2 && Hands[0].Bet / 2 > 0;

    // The player's answer to the insurance question.
    public bool AnswerInsurance(bool take)
    {
        if (Phase != RoundPhase.Insurance) return false;
        if (take)
        {
            if (!CanInsure) return false;
            InsuranceBet = Hands[0].Bet / 2;
            Chips -= InsuranceBet;
            Emit(new TableEvent { Type = TableEventType.InsuranceTaken, Amount = InsuranceBet });
        }
        AfterInsurance();
        return true;
    }

    // After the insurance question: dealer peeks for blackjack, settles insurance, then the player's turn (or an early end).
    private void AfterInsurance()
    {
        // the dealer peeks with an ace or a ten up
        bool dealerBj = BlackjackHand.Value(Dealer, out _) == 21;
        if (DealerUpCard.IsAce || DealerUpCard.IsTen)
            Emit(new TableEvent { Type = TableEventType.DealerPeek, Flag = dealerBj });

        if (InsuranceBet > 0)
        {
            int paid = dealerBj ? InsuranceBet * 3 : 0;
            Chips += paid;
            Emit(new TableEvent { Type = TableEventType.InsuranceSettled, Flag = dealerBj, Amount = paid });
        }

        if (dealerBj || Hands[0].IsBlackjack)
        {
            RevealHole();
            SettleAll();
            return;
        }
        Phase = RoundPhase.PlayerTurn;
        Activate(0);
    }

    // ------------------------------------------------------------------ player decisions
    public bool CanHit => Phase == RoundPhase.PlayerTurn && ActiveHand != null && !ActiveHand.Done && !ActiveHand.SplitAces;
    public bool CanStand => Phase == RoundPhase.PlayerTurn && ActiveHand != null && !ActiveHand.Done;

    // Double down: only on the first two cards of a hand (after a split only if the rules allow it).
    public bool CanDouble => Phase == RoundPhase.PlayerTurn && ActiveHand != null && !ActiveHand.Done && ActiveHand.Cards.Count == 2 &&
                             !ActiveHand.SplitAces && (!ActiveHand.FromSplit || Rules.DoubleAfterSplit) && Chips >= ActiveHand.Bet;

    public bool CanSplit
    {
        get
        {
            BlackjackHand h = ActiveHand;
            if (Phase != RoundPhase.PlayerTurn || h == null || h.Done || h.Cards.Count != 2) return false;
            if (h.Cards[0].Points != h.Cards[1].Points || Hands.Count >= Rules.MaxHands || Chips < h.Bet) return false;
            return !h.SplitAces || Rules.ResplitAces;
        }
    }

    // Late surrender: only as the first decision of an unsplit hand.
    public bool CanSurrender => Rules.LateSurrender && Phase == RoundPhase.PlayerTurn && Hands.Count == 1 &&
                                ActiveHand != null && ActiveHand.Cards.Count == 2 && !ActiveHand.Done;

    // Player decisions: each one checks it is allowed, changes the state and moves to the next hand when this one is done.
    public bool Hit()
    {
        if (!CanHit) return false;
        BlackjackHand h = ActiveHand;
        DealTo(Active);
        if (h.Total >= 21) { h.Done = true; NextHand(); }
        return true;
    }

    // Stand: this hand is done.
    public bool Stand()
    {
        if (!CanStand) return false;
        ActiveHand.Done = true;
        NextHand();
        return true;
    }

    // Double: the bet doubles, exactly one more card, the hand is done.
    public bool Double()
    {
        if (!CanDouble) return false;
        BlackjackHand h = ActiveHand;
        Chips -= h.Bet;
        Emit(new TableEvent { Type = TableEventType.BetDoubled, Hand = Active, Amount = h.Bet });
        h.Bet *= 2;
        h.Doubled = true;
        DealTo(Active);
        h.Done = true;
        NextHand();
        return true;
    }

    // Split: a pair becomes two hands (same bet each); split aces get one card each.
    public bool Split()
    {
        if (!CanSplit) return false;
        BlackjackHand h = ActiveHand;
        bool aces = h.Cards[0].IsAce;
        var second = new BlackjackHand { Bet = h.Bet, FromSplit = true, SplitAces = aces };
        second.Cards.Add(h.Cards[1]);
        h.Cards.RemoveAt(1);
        h.FromSplit = true;
        h.SplitAces = aces;
        Chips -= h.Bet;
        int index = Active + 1;
        Hands.Insert(index, second);
        Emit(new TableEvent { Type = TableEventType.HandSplit, Hand = index, Source = Active, Amount = second.Bet });

        // second card to the hand being played; the new hand gets its own when its turn comes
        DealTo(Active);
        if (aces || h.Total == 21)
        {
            h.Done = true;
            if (aces) { DealTo(index); second.Done = true; }
            NextHand();
        }
        return true;
    }

    // Surrender: half the bet back, the hand is done.
    public bool Surrender()
    {
        if (!CanSurrender) return false;
        BlackjackHand h = ActiveHand;
        h.Surrendered = true;
        h.Done = true;
        NextHand();
        return true;
    }

    // Makes a hand the one being played (deals its second card if it came from a split).
    private void Activate(int index)
    {
        Active = index;
        BlackjackHand h = Hands[index];
        // a split hand waiting with one card gets its second card now
        if (h.Cards.Count == 1) DealTo(index);
        Emit(new TableEvent { Type = TableEventType.HandActive, Hand = index });
        if (h.Total == 21 || h.SplitAces) { h.Done = true; NextHand(); }
    }

    // Moves to the next hand still to play, or to the dealer when there is none.
    private void NextHand()
    {
        for (int i = Active + 1; i < Hands.Count; i++)
            if (!Hands[i].Done) { Activate(i); return; }
        Active = -1;
        DealerPlay();
    }

    // ------------------------------------------------------------------ dealer + settlement
    private void DealerPlay()
    {
        Phase = RoundPhase.DealerTurn;
        RevealHole();
        bool anyLive = Hands.Exists(h => !h.IsBust && !h.Surrendered);
        if (anyLive)
        {
            while (true)
            {
                int total = BlackjackHand.Value(Dealer, out bool soft);
                if (total > 17 || (total == 17 && !(soft && Rules.DealerHitsSoft17))) break;
                DealDealer(false);
            }
        }
        SettleAll();
    }

    // Turns the dealer's hole card up (once).
    private void RevealHole()
    {
        if (HoleRevealed) return;
        HoleRevealed = true;
        Emit(new TableEvent { Type = TableEventType.HoleRevealed, Card = Dealer[1] });
    }

    // Pays every hand against the dealer's total and ends the round.
    private void SettleAll()
    {
        int dealer = DealerTotal;
        bool dealerBj = Dealer.Count == 2 && dealer == 21;
        for (int i = 0; i < Hands.Count; i++)
        {
            BlackjackHand h = Hands[i];
            if (h.Surrendered) { h.Outcome = HandOutcome.Surrender; h.Payout = h.Bet / 2; }
            else if (h.IsBust) { h.Outcome = HandOutcome.Bust; h.Payout = 0; }
            else if (h.IsBlackjack && !dealerBj)
            {
                h.Outcome = HandOutcome.Blackjack;
                h.Payout = h.Bet + (int)Math.Ceiling(h.Bet * Rules.BlackjackPays);   // the house rounds up
            }
            else if (dealerBj && !h.IsBlackjack) { h.Outcome = HandOutcome.Lose; h.Payout = 0; }
            else if (dealer > 21 || h.Total > dealer) { h.Outcome = HandOutcome.Win; h.Payout = h.Bet * 2; }
            else if (h.Total == dealer) { h.Outcome = HandOutcome.Push; h.Payout = h.Bet; }
            else { h.Outcome = HandOutcome.Lose; h.Payout = 0; }
            Chips += h.Payout;
            Emit(new TableEvent { Type = TableEventType.HandSettled, Hand = i, Outcome = h.Outcome, Amount = h.Payout });
        }
        Phase = RoundPhase.RoundOver;
        Active = -1;
        Emit(new TableEvent { Type = TableEventType.RoundOver });
    }

    // The round is called off (she has to leave): every bet on the table goes back to the player.
    public void CancelRound()
    {
        if (Phase == RoundPhase.Betting || Phase == RoundPhase.RoundOver) return;
        foreach (BlackjackHand h in Hands) Chips += h.Bet;
        Chips += InsuranceBet;
        InsuranceBet = 0;
        Phase = RoundPhase.RoundOver;
        Active = -1;
        Events.Clear();
    }

    // ------------------------------------------------------------------ shoe
    private void Shuffle()
    {
        shoe.Clear();
        for (int d = 0; d < Rules.Decks; d++)
            for (int s = 0; s < 4; s++)
                for (int r = 1; r <= 13; r++) shoe.Add(new Card(r, (Suit)s));
        for (int i = shoe.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Card t = shoe[i]; shoe[i] = shoe[j]; shoe[j] = t;
        }
        next = 0;
        cutIndex = (int)(shoe.Count * Rules.Penetration);
        Emit(new TableEvent { Type = TableEventType.Shuffle });
    }

    // Next card from the shoe (reshuffles if it ever runs out).
    private Card Draw()
    {
        if (next >= shoe.Count) Shuffle();   // never runs dry mid-round
        return shoe[next++];
    }

    // Deals one card to a player hand / to the dealer and queues the event.
    private void DealTo(int hand)
    {
        Card c = Draw();
        Hands[hand].Cards.Add(c);
        Emit(new TableEvent { Type = TableEventType.CardToPlayer, Hand = hand, Card = c });
    }

    // The dealer's card (the second one of the round goes face down).
    private void DealDealer(bool faceDown)
    {
        Card c = Draw();
        Dealer.Add(c);
        Emit(new TableEvent { Type = TableEventType.CardToDealer, Card = c, FaceDown = faceDown });
    }

    // Queues an event for the presentation.
    private void Emit(TableEvent e) => Events.Enqueue(e);

    // test hook: next cards out of the shoe (rigged deals for tests / demos)
    public void StackShoe(params Card[] cards)
    {
        int at = next;
        foreach (Card c in cards)
        {
            if (at >= shoe.Count) break;
            int idx = shoe.FindIndex(at, x => x.Rank == c.Rank && x.Suit == c.Suit);
            if (idx < 0) idx = at;
            Card t = shoe[at]; shoe[at] = shoe[idx]; shoe[idx] = t;
            at++;
        }
    }
}
