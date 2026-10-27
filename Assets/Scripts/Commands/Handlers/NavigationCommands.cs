using UnityEngine;
using UnityEngine.AI;

// Moving her around: "go to <room / door / thing>", "the center", "look at <thing>", and posing for the operator's
// camera ("look at the camera", "turn around", "show your side"). She only goes as far as the next room.
internal sealed class NavigationCommands : CommandHandler
{
    public NavigationCommands(CommandContext ctx) : base(ctx) { }

    // She only goes as far as the next room (or a door of her room): farther = "too far", the operator guides her.
    internal void GoTo(ParsedCommand cmd)
    {
        Vector3 from = Transform.position;
        Room here = Targets.CurrentRoom(from);
        Room room = Targets.FindRoom(cmd);
        if (cmd.HasKey(CommandTargets.CenterKey))
        {
            // "the center" alone: the middle of the hall (when the scene has one), else of her room
            Room center = room != null ? room : Targets.CenterRoom != null && (here == null || Targets.IsNextTo(here, Targets.CenterRoom)) ? Targets.CenterRoom : here;
            GoToCenter(center, here);
            return;
        }

        Door door = null;
        if (cmd.HasKey(CommandTargets.DoorKey))
        {
            // "door 2" / "the storage door": the door, not the room
            door = Targets.FindDoor(cmd, from, false, LastTarget);
            if (door == null && cmd.Number > 0) { Voice.Say(PartnerLine.NothingThere); return; }
            if (door == null && room != null) door = DoorTo(here, room);
            if (door == null) door = Targets.FindDoor(cmd, from, true, LastTarget);
            if (door != null && here != null && !Targets.DoorOf(here, door)) { Voice.Say(PartnerLine.TooFar); return; }
        }
        if (door == null && room != null && here != null && !Targets.IsNextTo(here, room)) { Voice.Say(PartnerLine.TooFar); return; }

        bool outOfReach = false;
        Pickup pickup = Targets.FindPickup(cmd, from, false, LastTarget);
        outOfReach |= Targets.LastSearchOutOfReach;
        SaveStation station = null;
        if (cmd.HasKey(CommandTargets.ComputerKey)) { station = Targets.FindStation(from); outOfReach |= Targets.LastSearchOutOfReach; }
        Health shootable = Targets.FindShootable(cmd, from, true, LastTarget);
        outOfReach |= Targets.LastSearchOutOfReach;
        Component target = null;
        if (door != null) target = door;
        else if (room != null) target = room;
        else if (pickup != null) target = pickup;
        else if (station != null) target = station;
        else if (shootable != null) target = shootable;
        if (target == null && cmd.Pronoun && LastTarget != null && Targets.InReach(from, LastTarget.transform.position)) target = LastTarget;
        if (target == null && outOfReach) { Voice.Say(PartnerLine.TooFar); return; }
        if (target == null || !TryDestination(target, out Vector3 destination))
        {
            Voice.Say(cmd.Keys.Count > 0 ? PartnerLine.NothingThere : PartnerLine.NotUnderstood);
            return;
        }

        if (!PathExists(destination))
        {
            // the next room behind its closed door: go through the door
            Door entrance = target is Room r ? DoorTo(here, r) : null;
            if (entrance != null && !entrance.IsOpen && DoorUser.Use(entrance, DoorAction.PassThrough))
            {
                LastTarget = target;
                Voice.Say(PartnerLine.OnMyWay);
                return;
            }
            Voice.Say(PartnerLine.CantGetThere);
            return;
        }
        if (!Interactor.MoveTo(destination)) { Voice.Say(PartnerLine.Busy); return; }
        LastTarget = target;
        Voice.Say(PartnerLine.OnMyWay);
    }

    // "the middle of the room": a free spot near the center the room's cameras see, one she can walk to
    private void GoToCenter(Room room, Room here)
    {
        if (room == null) { Voice.Say(PartnerLine.NotUnderstood); return; }
        if (here != null && !Targets.IsNextTo(here, room)) { Voice.Say(PartnerLine.TooFar); return; }
        if (Targets.TryCenterSpot(room, out Vector3 spot, PathExists))
        {
            if (!Interactor.MoveTo(spot)) { Voice.Say(PartnerLine.Busy); return; }
            LastTarget = room;
            Voice.Say(PartnerLine.OnMyWay);
            return;
        }
        // the next room behind a closed door: through the door first
        Door entrance = room != here ? Targets.DoorBetween(here, room) : null;
        if (entrance != null && !entrance.IsOpen && DoorUser.Use(entrance, DoorAction.PassThrough))
        {
            LastTarget = room;
            Voice.Say(PartnerLine.OnMyWay);
            return;
        }
        Voice.Say(PartnerLine.CantGetThere);
    }

    // "Look at the camera": she walks to where the operator's camera sees her best and poses.
    internal void LookAtCamera()
    {
        CombatCommands.LowerGuard();
        Combat.StopAiming();
        if (CameraPose == null) { Voice.Say(PartnerLine.NotUnderstood); return; }
        switch (CameraPose.Begin())
        {
            case PartnerCameraPose.Result.Ok: Voice.Say(PartnerLine.Acknowledge); break;
            case PartnerCameraPose.Result.NoCamera: Voice.Say(PartnerLine.NoCamera); break;
            case PartnerCameraPose.Result.TooFar: Voice.Say(PartnerLine.NoCamera); break;
            default: Voice.Say(PartnerLine.CantGetThere); break;
        }
    }

    // "turn around" / "show your left side": back or a side toward the camera being watched, where she stands
    internal void TurnToCamera(PartnerCameraPose.Facing facing)
    {
        CombatCommands.LowerGuard();
        Combat.StopAiming();
        if (CameraPose == null) { Voice.Say(PartnerLine.NotUnderstood); return; }
        if (CameraPose.Turn(facing) != PartnerCameraPose.Result.Ok) { Voice.Say(PartnerLine.NoCamera); return; }
        Voice.Say(facing == PartnerCameraPose.Facing.Back ? PartnerLine.TurnBack :
                  facing == PartnerCameraPose.Facing.Front ? PartnerLine.Acknowledge : PartnerLine.TurnSide);
    }

    // "look at the bed": she goes up to it and says what she thinks; "look at the door" (not a thing to look at): she goes there
    internal void Examine(ParsedCommand cmd)
    {
        if (Examiner == null) { GoTo(cmd); return; }
        Examinable thing = null;
        if (cmd.Keys.Count == 0 && cmd.Pronoun && LastTarget is Examinable last && last.isActiveAndEnabled) thing = last;
        if (thing == null) thing = Targets.FindExaminable(cmd, Transform.position, LastTarget);
        if (thing == null)
        {
            if (Targets.LastSearchOutOfReach) { Voice.Say(PartnerLine.TooFar); return; }
            if (cmd.Keys.Count > 0) { GoTo(cmd); return; }   // a room / door / item: go and see
            Voice.Say(PartnerLine.NothingThere);
            return;
        }
        if (!Examiner.Begin(thing)) { Voice.Say(PartnerLine.CantGetThere); return; }
        LastTarget = thing;
    }

    // Where she walks for a target (room spot, in front of a door, a pickup, a computer, near a hostile); false = no NavMesh there.
    private bool TryDestination(Component target, out Vector3 destination)
    {
        Vector3 wanted;
        switch (target)
        {
            case Room room: wanted = room.Spot != null ? room.Spot.position : room.transform.position; break;
            case Door door:
                Transform t = door.transform;
                float side = t.InverseTransformPoint(Transform.position).z > 0f ? 1f : -1f;
                wanted = t.position + t.forward * (side * DoorStandOff);
                break;
            case Pickup pickup: wanted = pickup.InteractionPosition; break;
            case SaveStation station: wanted = station.GetWalkInPosition(Transform.position); break;
            case Health h:
                Vector3 back = Flat(Transform.position - h.transform.position);
                wanted = h.transform.position + (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward) * 1.5f;
                break;
            default: destination = Transform.position; return false;
        }
        bool found = NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2.5f, NavMesh.AllAreas);
        destination = found ? hit.position : wanted;
        return found;
    }
}
