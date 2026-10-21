using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The casino's cashier (at the bar) and its prizes: chips are exchanged for prize coins at the cashier (one way: coins
// are the winnings you keep); the prizes (her outfits with a price) are bought with coins at their locker in the
// gallery (PrizeCase), or here when a prize has no locker.
// She has to be there (the cashier's Spot / the locker's Spot): an order given from farther away waits until she is.
public class PrizeCounter : MonoBehaviour
{
    public const string BankField = nameof(bank);
    public const string OutfitField = nameof(outfit);
    public const string SpotField = nameof(spot);
    public const string PartnerField = nameof(partner);
    public const string RateField = nameof(chipsPerCoin);
    public const string CasesField = nameof(cases);

    public enum Result { Ok, NothingToExchange, TooExpensive, AlreadyOwned, NotAPrize }

    [SerializeField] private ChipBank bank;
    [SerializeField] private PartnerOutfit outfit;
    [Tooltip("Where she stands at the counter.")]
    [SerializeField] private Transform spot;
    [SerializeField] private NavMeshAgent partner;
    [Tooltip("Chips for one prize coin.")]
    [SerializeField, Min(1)] private int chipsPerCoin = 1;
    [SerializeField] private float reach = 1.2f;
    [Tooltip("The prize lockers of the gallery.")]
    [SerializeField] private PrizeCase[] cases = new PrizeCase[0];

    private Action pending;
    private Vector3 pendingDestination;
    private Vector3 pendingSpot;

    public event Action Changed;

    public ChipBank Bank => bank;
    public int ChipsPerCoin => chipsPerCoin;
    public Transform Spot => spot;
    public bool HasPending => pending != null;

    // she is at the counter
    public bool IsHere(Vector3 position) => spot != null && IsNear(spot.position, position);

    public bool IsNear(Vector3 place, Vector3 position) => Flat(position - place).magnitude <= reach;

    // at the counter or in front of a locker (the panel shows)
    public bool IsAtAnyPlace(Vector3 position)
    {
        if (IsHere(position)) return true;
        foreach (PrizeCase c in cases) if (c != null && c.Spot != null && IsNear(c.Spot.position, position)) return true;
        return false;
    }

    // where a prize is bought: its locker, else the counter
    public Transform PlaceFor(OutfitDefinition prize)
    {
        foreach (PrizeCase c in cases) if (c != null && c.Prize == prize && c.Spot != null) return c.Spot;
        return spot;
    }

    // the prizes: her outfits that cost something, cheapest first
    public void GetPrizes(List<OutfitDefinition> into)
    {
        into.Clear();
        if (outfit == null) return;
        for (int i = 0; i < outfit.OutfitCount; i++)
        {
            OutfitDefinition o = outfit.GetOutfit(i);
            if (o != null && o.Price > 0) into.Add(o);
        }
        into.Sort((a, b) => a.Price.CompareTo(b.Price));
    }

    public bool IsOwned(OutfitDefinition o) => outfit != null && outfit.IsOwned(o);

    // amount <= 0 = all the chips
    public Result Exchange(int chips, out int coins)
    {
        coins = 0;
        if (chips <= 0 || chips > bank.Chips) chips = bank.Chips;
        coins = chips / chipsPerCoin;
        if (coins <= 0) return Result.NothingToExchange;
        bank.Add(-coins * chipsPerCoin);
        bank.AddCoins(coins);
        Changed?.Invoke();
        return Result.Ok;
    }

    public Result Buy(OutfitDefinition prize)
    {
        if (prize == null || prize.Price <= 0) return Result.NotAPrize;
        if (outfit.IsOwned(prize)) return Result.AlreadyOwned;
        if (bank.Coins < prize.Price) return Result.TooExpensive;
        bank.AddCoins(-prize.Price);
        outfit.Unlock(prize);
        Changed?.Invoke();
        return Result.Ok;
    }

    // done when she gets to the place (she was sent there); dropped if she is sent somewhere else
    public void WhenThere(Vector3 place, Action action)
    {
        pending = action;
        pendingSpot = place;
        pendingDestination = partner != null ? partner.destination : place;
    }

    public void CancelPending() => pending = null;

    private void Update()
    {
        if (pending == null || partner == null) return;
        if ((Flat(partner.destination) - Flat(pendingDestination)).sqrMagnitude > 0.25f) { pending = null; return; }   // sent elsewhere
        if (!IsNear(pendingSpot, partner.transform.position) || partner.pathPending || partner.velocity.sqrMagnitude > 0.05f) return;
        Action a = pending;
        pending = null;
        a();
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
