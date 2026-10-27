using UnityEngine;
using UnityEngine.AI;

// Keypads: the code said ("type 1 7", or just "seven" / "confirm" while she is at it); a late code takes her back
// to the keypad she just left; anything else first takes her hand off it.
internal sealed class KeypadCommands : CommandHandler
{
    public KeypadCommands(CommandContext ctx) : base(ctx) { }

    // a bare code ("dezessete", "confirma") after she left the keypad: she goes back to it this close (m)
    private const float KeypadReturnDistance = 6f;

    // true = the command was for the keypad (handled)
    internal bool HandleKeypad(ParsedCommand cmd)
    {
        string keys = KeypadInput.Keys(cmd.Text, Settings.Language, out bool onlyKeys);
        bool named = cmd.Verb == CommandVerb.TypeCode || cmd.HasKey(CommandTargets.KeypadKey);
        // only keys, she already left the keypad she was just at ("dezessete" said late): back to that one
        Keypad back = !named && onlyKeys && !KeypadUser.IsBusy && LastTarget is Keypad last && !last.IsOpen &&
                      (Flat(last.transform.position) - Flat(Transform.position)).sqrMagnitude <= KeypadReturnDistance * KeypadReturnDistance
                      ? last : null;
        // at the keypad, a sentence with no order in it: the keys heard in it ("sete" + noise), or "what?" - she stays
        if (!named && Ctx.Owner.AtKeypad && KeypadUser.IsBusy && cmd.Verb == CommandVerb.None && cmd.Keys.Count == 0 && !cmd.Pronoun)
        {
            if (keys.Length == 0) { Voice.Say(PartnerLine.NotUnderstood); return true; }
            named = true;
        }
        if (!named && back == null && !(KeypadUser.IsBusy && onlyKeys))
        {
            // at the keypad, something else: hand down first, then that (asking how she is doesn't need it)
            if (!KeypadUser.IsBusy || cmd.Verb == CommandVerb.Status) return false;
            KeypadUser.Leave(() => Rerun(cmd));
            return true;
        }

        Keypad keypad = KeypadUser.Current as Keypad;
        if (KeypadUser.IsBusy && keypad == null) { KeypadUser.Leave(() => Rerun(cmd)); return true; }   // at another panel (a slot)
        if (keypad == null) keypad = back;
        if (keypad == null) keypad = Targets.FindKeypad(Transform.position);
        if (keypad == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoKeypad); return true; }
        if (keypad.IsOpen && keys.Length > 0) { Voice.Say(PartnerLine.DoorAlreadyOpen); return true; }
        bool already = KeypadUser.IsBusy;
        if (!already && (IsBusy || Evasion.IsCrouched)) { Voice.Say(PartnerLine.Busy); return true; }
        Vector3 front = keypad.transform.position + Flat(keypad.transform.forward).normalized * 0.45f;
        bool reachable = NavMesh.SamplePosition(new Vector3(front.x, Transform.position.y, front.z), out NavMeshHit spot, 0.5f, NavMesh.AllAreas) &&
                         PathExists(spot.position);
        if (!already && !reachable)
        {
            Voice.Say(PartnerLine.CantGetThere);
            return true;
        }
        CombatCommands.LowerGuard();
        if (!KeypadUser.Use(keypad, keys)) { Voice.Say(PartnerLine.Busy); return true; }
        LastTarget = keypad;
        if (keys.Length == 0) Voice.Say(PartnerLine.KeypadWhich);       // "use the keypad": she waits for the numbers
        else if (!already) Voice.Say(PartnerLine.Acknowledge);          // at it already: she just types
        return true;
    }

    // After "confirm" on a keypad: right / wrong code.
    internal void OnKeyPressed(ButtonPanel panel, char key)
    {
        if (!(panel is Keypad keypad) || key != Keypad.Enter) return;
        if (keypad.LastResult == Keypad.Result.Accepted) Voice.Say(PartnerLine.KeypadOpen);
        else if (keypad.LastResult == Keypad.Result.Rejected) Voice.Say(PartnerLine.KeypadWrong);
    }
}
