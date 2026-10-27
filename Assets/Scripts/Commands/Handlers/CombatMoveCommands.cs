using UnityEngine;
using UnityEngine.AI;

// The combat moves: dodge, duck / get up, back off (away from the threat, in her room), advance (toward it, not
// too close), take cover (the farthest spot of the room) and run (out the door farthest from the threats).
internal sealed class CombatMoveCommands : CommandHandler
{
    public CombatMoveCommands(CommandContext ctx) : base(ctx) { }

    // "Dodge": a quick step back out of the way (from down: she gets up first).
    internal void Dodge()
    {
        if (Evasion.IsCrouched) { Evasion.GetUp(Dodge); return; }   // from down: up, then the dodge
        if (!Evasion.Dodge()) { Voice.Say(PartnerLine.Busy); return; }
        CombatCommands.CancelMelee();
        Voice.Say(PartnerLine.Dodging);
    }

    // "Duck": she crouches (smaller target, less damage).
    internal void Duck()
    {
        if (Evasion.IsCrouched) { Voice.Say(PartnerLine.Acknowledge); return; }
        CombatCommands.CancelMelee();
        Voice.Say(Evasion.Duck() ? PartnerLine.Ducking : PartnerLine.Busy);
    }

    // "Get up": she stands up.
    internal void StandUp()
    {
        if (!Evasion.IsCrouched) { Voice.Say(PartnerLine.Acknowledge); return; }
        Voice.Say(Evasion.GetUp() ? PartnerLine.GettingUp : PartnerLine.Busy);
    }

    // away from the threat, staying in her room (straight away first, then turning a bit, then closer)
    internal void BackOff()
    {
        Health threat = CombatCommands.Threat();
        // away from the threat; with none, backward
        Vector3 away = threat != null ? Flat(Transform.position - threat.transform.position) : -Flat(Transform.forward);
        away = away.sqrMagnitude > 0.01f ? away.normalized : -Transform.forward;
        Room room = Encounter != null && Encounter.InCombat ? Encounter.Area : Targets.CurrentRoom(Transform.position);
        foreach (float scale in new[] { 1f, 0.6f })
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                Vector3 wanted = Transform.position + Quaternion.AngleAxis(angle, Vector3.up) * away * (BackOffDistance * scale);
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (room != null && !room.Contains(hit.position, -WallMargin)) continue;
                if (!PathExists(hit.position) || !Interactor.MoveTo(hit.position)) continue;
                CombatCommands.CoverWhileMoving(threat);
                Voice.Say(PartnerLine.BackingOff);
                return;
            }
        Voice.Say(PartnerLine.CantGetThere);
    }

    // "advance" / "avança": a few meters forward (in a fight: toward the threat, not closer than advanceStopDistance),
    // so she doesn't stay behind; armed with a gun, she keeps it on the threat while she walks
    internal void Advance()
    {
        Health threat = CombatCommands.Threat();
        Vector3 ahead = threat != null ? Flat(threat.transform.position - Transform.position) : Flat(Transform.forward);
        float distance = ahead.magnitude;
        ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Flat(Transform.forward).normalized;
        float step = threat != null ? Mathf.Min(AdvanceDistance, distance - AdvanceStopDistance) : AdvanceDistance;
        if (step < 0.4f) { Voice.Say(PartnerLine.CantGetThere); return; }   // already right on it
        Room room = Encounter != null && Encounter.InCombat ? Encounter.Area : Targets.CurrentRoom(Transform.position);
        foreach (float scale in new[] { 1f, 0.6f, 0.35f })
            foreach (float angle in new[] { 0f, 20f, -20f, 40f, -40f })
            {
                Vector3 wanted = Transform.position + Quaternion.AngleAxis(angle, Vector3.up) * ahead * (step * scale);
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (room != null && !room.Contains(hit.position, -WallMargin * 0.5f)) continue;
                if (!PathExists(hit.position) || !Interactor.MoveTo(hit.position)) continue;
                CombatCommands.CoverWhileMoving(threat);
                Voice.Say(PartnerLine.Acknowledge);
                return;
            }
        Voice.Say(PartnerLine.CantGetThere);
    }

    // the spot of her room farthest from every threat (that she can walk to)
    internal void TakeCover()
    {
        Room room = Encounter != null ? Encounter.Area : null;
        if (CombatCommands.Threat() == null || room == null) { Voice.Say(PartnerLine.NoThreat); return; }
        Rect r = room.FloorRect;
        float y = Transform.position.y;
        float best = Encounter.DistanceToThreat(Transform.position) + 0.5f;   // must be worth the walk
        Vector3 bestSpot = Transform.position;
        bool found = false;
        for (float x = r.xMin + WallMargin; x <= r.xMax - WallMargin; x += CoverGrid)
            for (float z = r.yMin + WallMargin; z <= r.yMax - WallMargin; z += CoverGrid)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, y, z), out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
                float d = Encounter.DistanceToThreat(hit.position);
                if (d <= best || !room.Contains(hit.position, -WallMargin * 0.5f) || !PathExists(hit.position)) continue;
                best = d;
                bestSpot = hit.position;
                found = true;
            }
        if (!found) { Voice.Say(PartnerLine.NoWayOut); return; }
        if (!Interactor.MoveTo(bestSpot)) { Voice.Say(PartnerLine.Busy); return; }
        CombatCommands.CoverWhileMoving(CombatCommands.Threat());
        Voice.Say(PartnerLine.TakingCover);
    }

    // out of the room through the door farthest from the threats (combat ends once she is out)
    internal void Flee()
    {
        Room room = Encounter != null ? Encounter.Area : null;
        if (CombatCommands.Threat() == null || room == null) { Voice.Say(PartnerLine.NoThreat); return; }
        Door best = null;
        float bestScore = float.MinValue;
        foreach (Door d in Targets.Doors)
        {
            if (d == null || !room.Contains(d.transform.position, 1f) || (d.IsLocked && !d.IsOpen)) continue;
            if (!d.IsOpen && d.IsRemoteControlled) continue;   // she can't open it herself
            float score = Encounter.DistanceToThreat(d.transform.position) - Flat(d.transform.position - Transform.position).magnitude * 0.3f;
            if (score > bestScore) { bestScore = score; best = d; }
        }
        if (best == null) { Voice.Say(PartnerLine.NoWayOut); return; }
        bool going = best.IsOpen ? InteractionCommands.WalkThrough(best) : DoorUser.Use(best, DoorAction.PassThrough);
        if (!going) { Voice.Say(PartnerLine.NoWayOut); return; }
        CombatCommands.CancelMelee();
        CombatCommands.LowerGuard();
        Combat.StopAiming();
        LastTarget = best;
        Voice.Say(PartnerLine.Retreating);
    }
}
