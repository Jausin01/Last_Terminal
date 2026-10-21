using System;
using System.Collections.Generic;

// Blackjack rules, no Unity code (the table / Helena only animate what this produces):
// shoe of N decks with a cut card, player vs dealer, hit / stand / double / split (re-split up to 4 hands,
// split aces get one card), late surrender, insurance (dealer shows an ace), dealer peeks for blackjack with an ace
// or a ten up, dealer stands (or hits) on soft 17, blackjack pays 3:2, insurance 2:1.
// Every change is also queued as a TableEvent, in order, for the presentation.

public enum Suit { Spades = 0, Hearts = 1, Diamonds = 2, Clubs = 3 }

[Serializable]
public struct Card
{
    public int Rank;        // 1 = ace .. 13 = king
    public Suit Suit;

    public Card(int rank, Suit suit) { Rank = rank; Suit = suit; }
    public int Points => Rank == 1 ? 11 : Math.Min(Rank, 10);
    public bool IsAce => Rank == 1;
    public bool IsTen => Rank >= 10;
    public override string ToString() => RankName + "SHDC"[(int)Suit];
    public string RankName => Rank == 1 ? "A" : Rank == 11 ? "J" : Rank == 12 ? "Q" : Rank == 13 ? "K" : Rank.ToString();
}

public class BlackjackRules
{
    public int Decks = 6;
    public float Penetration = 0.75f;      // reshuffle when this much of the shoe is used
    public bool DealerHitsSoft17;          // false = "dealer must stand on all 17s"
    public float BlackjackPays = 1.5f;
    public bool DoubleAfterSplit = true;
    public int MaxHands = 4;
    public bool ResplitAces;
    public bool LateSurrender = true;
    public bool Insurance = true;
    public int MinBet = 5;
    public int MaxBet = 500;
}

public enum HandOutcome { None, Win, Blackjack, Push, Lose, Bust, Surrender }

public class BlackjackHand
{
    public readonly List<Card> Cards = new List<Card>();
    public int Bet;
    public bool Doubled;
    public bool FromSplit;
    public bool SplitAces;
    public bool Done;
    public bool Surrendered;
    public HandOutcome Outcome;
    public int Payout;          // returned to the player (bet included) when settled

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

public enum RoundPhase { Betting, Insurance, PlayerTurn, DealerTurn, RoundOver }

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

public struct TableEvent
{
    public TableEventType Type;
    public int Hand;
    public int Source;          // HandSplit: the hand the card came from
    public Card Card;
    public bool FaceDown;
    public bool Flag;
    public int Amount;
    public HandOutcome Outcome;
}

public class BlackjackEngine
{
    public readonly BlackjackRules Rules;
    public readonly List<BlackjackHand> Hands = new List<BlackjackHand>();
    public readonly List<Card> Dealer = new List<Card>();
    public readonly Queue<TableEvent> Events = new Queue<TableEvent>();

    private readonly List<Card> shoe = new List<Card>();
    private readonly Random random;
    private int next;
    private int cutIndex;

    public RoundPhase Phase { get; private set; } = RoundPhase.Betting;
    public int Chips { get; private set; }
    public int Active { get; private set; } = -1;          // hand being played
    public int InsuranceBet { get; private set; }
    public bool HoleRevealed { get; private set; }
    public int LastBet { get; private set; }

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

    public void SetChips(int chips) { if (Phase == RoundPhase.Betting || Phase == RoundPhase.RoundOver) Chips = Math.Max(0, chips); }
    public void AddChips(int amount) => Chips += amount;

    // ------------------------------------------------------------------ round start
    public enum BetResult { Ok, BelowMinimum, AboveMaximum, NotEnoughChips, NotNow }

    public BetResult CheckBet(int bet)
    {
        if (Phase != RoundPhase.Betting && Phase != RoundPhase.RoundOver) return BetResult.NotNow;
        if (bet < Rules.MinBet) return BetResult.BelowMinimum;
        if (bet > Rules.MaxBet) return BetResult.AboveMaximum;
        if (bet > Chips) return BetResult.NotEnoughChips;
        return BetResult.Ok;
    }

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

    public bool CanSurrender => Rules.LateSurrender && Phase == RoundPhase.PlayerTurn && Hands.Count == 1 &&
                                ActiveHand != null && ActiveHand.Cards.Count == 2 && !ActiveHand.Done;

    public bool Hit()
    {
        if (!CanHit) return false;
        BlackjackHand h = ActiveHand;
        DealTo(Active);
        if (h.Total >= 21) { h.Done = true; NextHand(); }
        return true;
    }

    public bool Stand()
    {
        if (!CanStand) return false;
        ActiveHand.Done = true;
        NextHand();
        return true;
    }

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

    public bool Surrender()
    {
        if (!CanSurrender) return false;
        BlackjackHand h = ActiveHand;
        h.Surrendered = true;
        h.Done = true;
        NextHand();
        return true;
    }

    private void Activate(int index)
    {
        Active = index;
        BlackjackHand h = Hands[index];
        // a split hand waiting with one card gets its second card now
        if (h.Cards.Count == 1) DealTo(index);
        Emit(new TableEvent { Type = TableEventType.HandActive, Hand = index });
        if (h.Total == 21 || h.SplitAces) { h.Done = true; NextHand(); }
    }

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

    private void RevealHole()
    {
        if (HoleRevealed) return;
        HoleRevealed = true;
        Emit(new TableEvent { Type = TableEventType.HoleRevealed, Card = Dealer[1] });
    }

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

    private Card Draw()
    {
        if (next >= shoe.Count) Shuffle();   // never runs dry mid-round
        return shoe[next++];
    }

    private void DealTo(int hand)
    {
        Card c = Draw();
        Hands[hand].Cards.Add(c);
        Emit(new TableEvent { Type = TableEventType.CardToPlayer, Hand = hand, Card = c });
    }

    private void DealDealer(bool faceDown)
    {
        Card c = Draw();
        Dealer.Add(c);
        Emit(new TableEvent { Type = TableEventType.CardToDealer, Card = c, FaceDown = faceDown });
    }

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
