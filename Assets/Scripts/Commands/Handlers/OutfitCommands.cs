using System;
using UnityEngine;

// Her outfits: "put on the pajamas" / "change clothes", and "what does your tag say" (the line of the outfit she wears).
internal sealed class OutfitCommands : CommandHandler
{
    public OutfitCommands(CommandContext ctx) : base(ctx) { }

    // the outfit named, or the next one ("change clothes")
    internal void ChangeClothes(ParsedCommand cmd)
    {
        if (Outfit == null) { Voice.Say(PartnerLine.NotUnderstood); return; }
        string key = cmd.Keys.Find(k => k.StartsWith(PartnerCommands.OutfitKeyPrefix, StringComparison.Ordinal));
        OutfitDefinition next;
        if (key == null) next = Outfit.Next();
        else if (!Outfit.TryFind(key, out next)) { Voice.Say(PartnerLine.DontHaveThat); return; }
        if (!Outfit.IsOwned(next)) { Voice.Say(PartnerLine.OutfitLocked); return; }   // a casino prize not bought yet
        if (next == Outfit.Current) { Voice.Say(PartnerLine.OutfitSame); return; }
        Outfit.Wear(next);
        Voice.Say(next != null ? next.Line : PartnerLine.OutfitChanged);
    }

    // Does the command name one of her outfits?
    internal static bool HasOutfitKey(ParsedCommand cmd) => cmd.Keys.Exists(k => k.StartsWith(PartnerCommands.OutfitKeyPrefix, StringComparison.Ordinal));

    // what is written on the tag of the outfit she has on (no outfit / no tag: she says so)
    internal void SayTag()
    {
        OutfitDefinition current = Outfit != null ? Outfit.Current : null;
        Voice.Say(current != null ? current.TagLine : PartnerLine.NoTag);
    }
}
