using System;
using System.Collections.Generic;

// Casino Hold'em, no Unity code (HoldemGame animates what this produces). One deck, shuffled every round.
//   ante -> two cards each (hers face down) + the flop -> CALL (2x the ante) or FOLD (the ante is lost)
//   call -> turn + river, she shows her cards. She qualifies with a pair of fours or better:
//     she doesn't: the ante is paid by the ante table, the call comes back;
//     she does: the best five cards win; the player wins = ante by the ante table + call 1:1; a tie = both back.
//   Ante table: royal flush 100, straight flush 20, four of a kind 10, full house 3, flush 2, anything else 1.
// Every change is queued as a HoldemEvent, in order, for the presentation.
public enum HandCategory { HighCard, OnePair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush }

public enum HoldemOutcome { None, Win, Lose, Push, Fold, DealerNoQualify }

public enum HoldemEventType { AntePlaced, CardToPlayer, CardToDealer, CardToBoard, CallPlaced, Folded, DealerReveal, Settled }

public struct HoldemEvent
{
    public HoldemEventType Type;
    public Card Card;
    public int Index;     // card index (player / dealer / board)
    public int Amount;
}

// a 5-card hand's strength: category, then the ranks that break ties (ace = 14), compared in order
public struct HandValue : IComparable<HandValue>
{
    public HandCategory Category;
    public int[] Ranks;

    public int CompareTo(HandValue other)
    {
        if (Category != other.Category) return Category.CompareTo(other.Category);
        for (int i = 0; i < Ranks.Length && i < other.Ranks.Length; i++)
            if (Ranks[i] != other.Ranks[i]) return Ranks[i].CompareTo(other.Ranks[i]);
        return 0;
    }
}

public class HoldemEngine
{
    public enum BetResult { Ok, BelowMinimum, AboveMaximum, NotEnoughChips }
    public enum Phase { Betting, Deciding, RoundOver }

    private readonly Random random;
    private readonly List<Card> deck = new List<Card>();
    private int next;

    public int MinAnte { get; }
    public int MaxAnte { get; }
    public int Chips { get; private set; }
    public Phase CurrentPhase { get; private set; } = Phase.Betting;
    public int Ante { get; private set; }
    public int Call { get; private set; }
    public int LastAnte { get; private set; }
    public readonly List<Card> Player = new List<Card>();
    public readonly List<Card> Dealer = new List<Card>();
    public readonly List<Card> Board = new List<Card>();
    public HoldemOutcome Outcome { get; private set; }
    public HandValue PlayerValue { get; private set; }
    public HandValue DealerValue { get; private set; }
    public int Returned { get; private set; }   // chips back to the player at the end (bets included)
    public readonly Queue<HoldemEvent> Events = new Queue<HoldemEvent>();

    public HoldemEngine(int minAnte, int maxAnte, int chips, int seed)
    {
        MinAnte = minAnte;
        MaxAnte = maxAnte;
        Chips = chips;
        random = new Random(seed);
    }

    public void SetChips(int chips) => Chips = chips;

    // the ante has to leave enough for the call (2x)
    public BetResult CheckAnte(int ante)
    {
        if (ante < MinAnte) return BetResult.BelowMinimum;
        if (ante > MaxAnte) return BetResult.AboveMaximum;
        if (ante * 3 > Chips) return BetResult.NotEnoughChips;
        return BetResult.Ok;
    }

    public BetResult StartRound(int ante)
    {
        BetResult r = CheckAnte(ante);
        if (r != BetResult.Ok) return r;
        Shuffle();
        Player.Clear(); Dealer.Clear(); Board.Clear();
        Ante = ante; Call = 0; LastAnte = ante; Returned = 0;
        Outcome = HoldemOutcome.None;
        Chips -= ante;
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.AntePlaced, Amount = ante });
        for (int i = 0; i < 2; i++)
        {
            Give(Player, HoldemEventType.CardToPlayer);
            Give(Dealer, HoldemEventType.CardToDealer);
        }
        for (int i = 0; i < 3; i++) Give(Board, HoldemEventType.CardToBoard);
        CurrentPhase = Phase.Deciding;
        return BetResult.Ok;
    }

    public bool CanCall => CurrentPhase == Phase.Deciding && Chips >= Ante * 2;

    public bool DoCall()
    {
        if (!CanCall) return false;
        Call = Ante * 2;
        Chips -= Call;
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.CallPlaced, Amount = Call });
        Give(Board, HoldemEventType.CardToBoard);
        Give(Board, HoldemEventType.CardToBoard);
        Showdown();
        return true;
    }

    public bool Fold()
    {
        if (CurrentPhase != Phase.Deciding) return false;
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.Folded });
        Outcome = HoldemOutcome.Fold;
        Returned = 0;
        PlayerValue = Best(Player, Board);
        DealerValue = Best(Dealer, Board);
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.DealerReveal });
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.Settled });
        CurrentPhase = Phase.RoundOver;
        return true;
    }

    // a round called off (she leaves, a fight): everything back
    public void CancelRound()
    {
        if (CurrentPhase == Phase.Deciding) Chips += Ante + Call;
        CurrentPhase = Phase.Betting;
        Events.Clear();
    }

    public void NextRound() => CurrentPhase = Phase.Betting;

    private void Showdown()
    {
        PlayerValue = Best(Player, Board);
        DealerValue = Best(Dealer, Board);
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.DealerReveal });
        int anteWin = Ante * AntePays(PlayerValue.Category);
        if (!Qualifies(DealerValue))
        {
            Outcome = HoldemOutcome.DealerNoQualify;
            Returned = Ante + anteWin + Call;
        }
        else
        {
            int cmp = PlayerValue.CompareTo(DealerValue);
            if (cmp > 0) { Outcome = HoldemOutcome.Win; Returned = Ante + anteWin + Call * 2; }
            else if (cmp < 0) { Outcome = HoldemOutcome.Lose; Returned = 0; }
            else { Outcome = HoldemOutcome.Push; Returned = Ante + Call; }
        }
        Chips += Returned;
        Events.Enqueue(new HoldemEvent { Type = HoldemEventType.Settled, Amount = Returned });
        CurrentPhase = Phase.RoundOver;
    }

    public static int AntePays(HandCategory c)
    {
        switch (c)
        {
            case HandCategory.RoyalFlush: return 100;
            case HandCategory.StraightFlush: return 20;
            case HandCategory.FourOfAKind: return 10;
            case HandCategory.FullHouse: return 3;
            case HandCategory.Flush: return 2;
            default: return 1;
        }
    }

    // a pair of fours or better
    public static bool Qualifies(HandValue v) => v.Category > HandCategory.OnePair || (v.Category == HandCategory.OnePair && v.Ranks[0] >= 4);

    private void Give(List<Card> to, HoldemEventType type)
    {
        Card c = deck[next++];
        to.Add(c);
        Events.Enqueue(new HoldemEvent { Type = type, Card = c, Index = to.Count - 1 });
    }

    private void Shuffle()
    {
        deck.Clear();
        for (int s = 0; s < 4; s++)
            for (int r = 1; r <= 13; r++) deck.Add(new Card(r, (Suit)s));
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        next = 0;
    }

    // ------------------------------------------------------------------ hand values
    // the best 5 of the 2 hole cards + the board (any 5 of up to 7)
    public static HandValue Best(List<Card> hole, List<Card> board)
    {
        var all = new List<Card>(hole);
        all.AddRange(board);
        if (all.Count < 5) return Evaluate(all);
        HandValue best = default;
        bool any = false;
        var five = new List<Card>(5);
        int n = all.Count;
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                // leave out (n - 5) cards: with 7 cards = 2 (a, b); with 6 = only a; with 5 = none
                five.Clear();
                for (int i = 0; i < n; i++)
                {
                    bool skip = n == 7 ? (i == a || i == b) : n == 6 ? i == a : false;
                    if (!skip) five.Add(all[i]);
                }
                HandValue v = Evaluate(five);
                if (!any || v.CompareTo(best) > 0) { best = v; any = true; }
                if (n == 5) return best;
                if (n == 6) break;
            }
        return best;
    }

    // up to 5 cards
    public static HandValue Evaluate(List<Card> cards)
    {
        var counts = new int[15];
        var suits = new int[4];
        foreach (Card c in cards) { counts[High(c)]++; suits[(int)c.Suit]++; }
        bool flush = cards.Count == 5 && Array.IndexOf(suits, 5) >= 0;
        int straightHigh = 0;
        if (cards.Count == 5)
        {
            for (int h = 14; h >= 5; h--)
            {
                bool run = true;
                for (int k = 0; k < 5; k++) { int r = h - k == 1 ? 14 : h - k; if (counts[r] != 1) { run = false; break; } }
                if (run) { straightHigh = h; break; }
            }
        }
        // ranks grouped: by count, then rank (high first)
        var groups = new List<(int count, int rank)>();
        for (int r = 14; r >= 2; r--) if (counts[r] > 0) groups.Add((counts[r], r));
        groups.Sort((x, y) => x.count != y.count ? y.count.CompareTo(x.count) : y.rank.CompareTo(x.rank));
        var ranks = new List<int>();
        foreach (var g in groups) ranks.Add(g.rank);

        if (straightHigh > 0 && flush)
            return new HandValue { Category = straightHigh == 14 ? HandCategory.RoyalFlush : HandCategory.StraightFlush, Ranks = new[] { straightHigh } };
        if (groups[0].count == 4) return new HandValue { Category = HandCategory.FourOfAKind, Ranks = ranks.ToArray() };
        if (groups[0].count == 3 && groups.Count > 1 && groups[1].count >= 2) return new HandValue { Category = HandCategory.FullHouse, Ranks = ranks.ToArray() };
        if (flush) return new HandValue { Category = HandCategory.Flush, Ranks = ranks.ToArray() };
        if (straightHigh > 0) return new HandValue { Category = HandCategory.Straight, Ranks = new[] { straightHigh } };
        if (groups[0].count == 3) return new HandValue { Category = HandCategory.ThreeOfAKind, Ranks = ranks.ToArray() };
        if (groups[0].count == 2 && groups.Count > 1 && groups[1].count == 2) return new HandValue { Category = HandCategory.TwoPair, Ranks = ranks.ToArray() };
        if (groups[0].count == 2) return new HandValue { Category = HandCategory.OnePair, Ranks = ranks.ToArray() };
        return new HandValue { Category = HandCategory.HighCard, Ranks = ranks.ToArray() };
    }

    private static int High(Card c) => c.Rank == 1 ? 14 : c.Rank;
}
