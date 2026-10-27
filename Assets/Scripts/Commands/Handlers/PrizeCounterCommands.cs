using System;
using UnityEngine;
using UnityEngine.AI;

// The cashier / prize counter: "exchange my chips" (or an amount), "buy the bunny suit" - at the counter now, or
// once she gets there.
internal sealed class PrizeCounterCommands : CommandHandler
{
    public PrizeCounterCommands(CommandContext ctx) : base(ctx) { }

    // "exchange my chips" / "exchange 500", "buy the bunny suit": at the counter now, or when she gets there
    internal void Counter(ParsedCommand cmd)
    {
        PrizeCounter counter = Targets.Counter;
        if (counter == null) { Voice.Say(PartnerLine.NoCounter); return; }
        Action deal;
        OutfitDefinition buying = null;
        if (cmd.Verb == CommandVerb.Exchange)
        {
            int amount = cmd.HasKey(CommandTargets.AllInKey) ? 0 : cmd.Number;
            deal = () => Voice.Say(counter.Exchange(amount, out _) == PrizeCounter.Result.Ok ? PartnerLine.CounterExchanged : PartnerLine.CounterNoChips);
        }
        else
        {
            string key = cmd.Keys.Find(k => k.StartsWith(PartnerCommands.OutfitKeyPrefix, StringComparison.Ordinal));
            if (key == null || Outfit == null || !Outfit.TryFind(key, out OutfitDefinition prize) || prize == null)
            {
                Voice.Say(PartnerLine.NotUnderstood);
                return;
            }
            buying = prize;
            deal = () =>
            {
                switch (counter.Buy(prize))
                {
                    case PrizeCounter.Result.Ok: Voice.Say(PartnerLine.PrizeBought); break;
                    case PrizeCounter.Result.TooExpensive: Voice.Say(PartnerLine.PrizeTooExpensive); break;
                    case PrizeCounter.Result.AlreadyOwned: Voice.Say(PartnerLine.PrizeOwned); break;
                    default: Voice.Say(PartnerLine.DontHaveThat); break;
                }
            };
        }
        // exchanging: at the cashier; buying: at the prize's locker
        Vector3 place = cmd.Verb == CommandVerb.Buy && buying != null ? counter.PlaceFor(buying).position : counter.Spot.position;
        if (counter.IsNear(place, Transform.position)) { deal(); return; }
        if (IsBusy || Evasion.IsCrouched) { Voice.Say(PartnerLine.Busy); return; }
        if (!Targets.InReach(Transform.position, place)) { Voice.Say(PartnerLine.TooFar); return; }
        if (!NavMesh.SamplePosition(place, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || !Interactor.MoveTo(hit.position))
        {
            Voice.Say(PartnerLine.CantGetThere);
            return;
        }
        counter.WhenThere(place, deal);
        LastTarget = counter;
        Voice.Say(PartnerLine.OnMyWay);
    }
}
