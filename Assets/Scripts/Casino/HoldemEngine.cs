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

// How a round ended for the player.
public enum HoldemOutcome { None, Win, Lose, Push, Fold, DealerNoQualify }

// Everything that happens at the table, in order, for the presentation (HoldemGame plays them one by one).
public enum HoldemEventType { AntePlaced, CardToPlayer, CardToDealer, CardToBoard, CallPlaced, Folded, DealerReveal, Settled }

// One table event (Card / Index / Amount used depending on the type).
public struct HoldemEvent
{
    // What happened.
    public HoldemEventType Type;
    // The card dealt (card events).
    public Card Card;
    public int Index;     // card index (player / dealer / board)
    // Chips involved (ante, call, chips returned).
    public int Amount;
}

// a 5-card hand's strength: category, then the ranks that break ties (ace = 14), compared in order
public struct HandValue : IComparable<HandValue>
{
    // Hand category (pair, flush...).
    public HandCategory Category;
    // Ranks that break ties within the category, most important first.
    public int[] Ranks;

    // Compares two hands: category first, then the tie-break ranks in order.
    public int CompareTo(HandValue other)
    {
        if (Category != other.Category) return Category.CompareTo(other.Category);
        for (int i = 0; i < Ranks.Length && i < other.Ranks.Length; i++)
            if (Ranks[i] != other.Ranks[i]) return Ranks[i].CompareTo(other.Ranks[i]);
        return 0;
    }
}

// The game itself: one deck, the player against the dealer, the ante / call bets and the payouts. Pure C#.
public class HoldemEngine
{
    // Why an ante can't be placed, and where the round is.
    public enum BetResult { Ok, BelowMinimum, AboveMaximum, NotEnoughChips }
    public enum Phase { Betting, Deciding, RoundOver }

    // Random source, the shuffled deck and the next card to deal.
    private readonly Random random;
    private readonly List<Card> deck = new List<Card>();
    private int next;

    // Table limits and the player's chips.
    public int MinAnte { get; }
    public int MaxAnte { get; }
    public int Chips { get; private set; }
    // Round state: phase, the bets on the table this round, the last ante ("same bet").
    public Phase CurrentPhase { get; private set; } = Phase.Betting;
    public int Ante { get; private set; }
    public int Call { get; private set; }
    public int LastAnte { get; private set; }
    // The cards: player's two, dealer's two, the board (up to five).
    public readonly List<Card> Player = new List<Card>();
    public readonly List<Card> Dealer = new List<Card>();
    public readonly List<Card> Board = new List<Card>();
    // Result of the round and both best hands (shown by the HUD and spoken by Helena).
    public HoldemOutcome Outcome { get; private set; }
    public HandValue PlayerValue { get; private set; }
    public HandValue DealerValue { get; private set; }
    public int Returned { get; private set; }   // chips back to the player at the end (bets included)
    // Events waiting to be shown, in order.
    public readonly Queue<HoldemEvent> Events = new Queue<HoldemEvent>();

    public HoldemEngine(int minAnte, int maxAnte, int chips, int seed)
    {
        MinAnte = minAnte;
        MaxAnte = maxAnte;
        Chips = chips;
        random = new Random(seed);
    }

    // Chip changes from outside (save loading, cashier).
    public void SetChips(int chips) => Chips = chips;

    // the ante has to leave enough for the call (2x)
    public BetResult CheckAnte(int ante)
    {
        if (ante < MinAnte) return BetResult.BelowMinimum;
        if (ante > MaxAnte) return BetResult.AboveMaximum;
        if (ante * 3 > Chips) return BetResult.NotEnoughChips;
        return BetResult.Ok;
    }

    // Takes the ante, shuffles and deals two cards each plus the flop; then the player decides.
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

    // Can the player pay the call (2x the ante)?
    public bool CanCall => CurrentPhase == Phase.Deciding && Chips >= Ante * 2;

    // Call: pays 2x the ante, the turn and river come out, showdown.
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

    // Fold: the ante is lost; her cards are still shown.
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

    // Back to betting after the round was shown.
    public void NextRound() => CurrentPhase = Phase.Betting;

    // Compares the hands and pays according to the house rules (see the top of the file).
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

    // Ante payout multiplier per hand category (the ante pay table).
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

    // Deals the next card to a hand / the board and queues the event.
    private void Give(List<Card> to, HoldemEventType type)
    {
        Card c = deck[next++];
        to.Add(c);
        Events.Enqueue(new HoldemEvent { Type = type, Card = c, Index = to.Count - 1 });
    }

    // New deck, Fisher-Yates shuffle.
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

    // Card rank for poker comparisons (ace high = 14).
    private static int High(Card c) => c.Rank == 1 ? 14 : c.Rank;
}
