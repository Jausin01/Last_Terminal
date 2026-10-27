using System;
using UnityEngine;
using UnityEngine.AI;

// Doors and items: "pick up X", "open / close / go through the door", "use X" (a weapon, a medkit, a key on its
// door, the computer), "heal", "save". Also answers the results that come later (item taken, door locked...).
internal sealed class InteractionCommands : CommandHandler
{
    public InteractionCommands(CommandContext ctx) : base(ctx) { }

    // "Pick up X": walks to the item and takes it ("pick up the shotgun" = draw it).
    internal void PickUp(ParsedCommand cmd)
    {
        // "pick up the shotgun" = take the weapon out
        if (CombatCommands.TryWeapon(cmd, out int weapon)) { CombatCommands.DrawWeapon(weapon); return; }

        Pickup pickup = null;
        if (cmd.Keys.Count == 0 && cmd.Pronoun && LastTarget is Pickup last && last.gameObject.activeInHierarchy) pickup = last;
        if (pickup == null) pickup = Targets.FindPickup(cmd, Transform.position, cmd.Keys.Count == 0, LastTarget);
        if (pickup == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return; }
        if (!Interactor.FindStand(pickup, out _)) { Voice.Say(PartnerLine.CantGetThere); return; }
        if (!Interactor.PickUp(pickup)) { Voice.Say(PartnerLine.Busy); return; }
        LastTarget = pickup;
        Voice.Say(PartnerLine.Acknowledge);
    }

    // Open / close / go through a door (the named one, the one to a named room, or the nearest of her room).
    internal void UseDoor(ParsedCommand cmd, DoorAction action)
    {
        Door door = Targets.FindDoor(cmd, Transform.position, false, LastTarget);
        if (door == null && cmd.Pronoun && LastTarget is Door last) door = last;
        if (door == null && cmd.Number <= 0)
        {
            // a room was named ("open the storage"): the door to it
            Room room = Targets.FindRoom(cmd);
            if (room != null) door = DoorTo(Targets.CurrentRoom(Transform.position), room);
        }
        if (door == null && cmd.Number <= 0) door = Targets.FindDoor(cmd, Transform.position, true, LastTarget);
        if (door == null) { Voice.Say(PartnerLine.NothingThere); return; }
        // only the doors of the room she is in
        Room here = Targets.CurrentRoom(Transform.position);
        if (here != null && !Targets.DoorOf(here, door)) { Voice.Say(PartnerLine.TooFar); return; }
        LastTarget = door;

        if (DoorUser.Use(door, action)) { Voice.Say(PartnerLine.Acknowledge); return; }

        // nothing to do with the door itself
        if (action == DoorAction.Open && door.IsOpen) Voice.Say(PartnerLine.DoorAlreadyOpen);
        else if (action == DoorAction.Close && !door.IsOpen) Voice.Say(PartnerLine.DoorAlreadyClosed);
        else if (action == DoorAction.Close && door.IsRemoteControlled) Voice.Say(PartnerLine.DoorNeedsOperator);
        else if (action == DoorAction.PassThrough && door.IsOpen && WalkThrough(door)) Voice.Say(PartnerLine.OnMyWay);
        else Voice.Say(PartnerLine.CantGetThere);
    }

    // open door: walk to the other side
    internal bool WalkThrough(Door door)
    {
        Transform t = door.transform;
        float side = t.InverseTransformPoint(Transform.position).z > 0f ? -1f : 1f;
        Vector3 beyond = t.position + t.forward * (side * 1.2f);
        return NavMesh.SamplePosition(beyond, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && PathExists(hit.position) && Interactor.MoveTo(hit.position);
    }

    // "Use X": a weapon, the computer, a medkit, a key (on its door), or a door.
    internal void Use(ParsedCommand cmd)
    {
        if (CombatCommands.TryWeapon(cmd, out int weapon)) { CombatCommands.DrawWeapon(weapon); return; }
        if (cmd.HasKey(CommandTargets.ComputerKey)) { Save(); return; }

        Inventory inventory = Interactor.Inventory;
        foreach (string key in cmd.Keys)
        {
            ItemDefinition item = inventory != null ? inventory.FindById(key) : null;
            if (item is HealingItem) { Heal(cmd); return; }
            if (item is KeyItem keyItem)
            {
                Door locked = Targets.FindDoorFor(keyItem, Transform.position);
                if (locked == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return; }
                LastTarget = locked;
                if (DoorUser.Use(locked, DoorAction.PassThrough)) Voice.Say(PartnerLine.Acknowledge);
                else Voice.Say(PartnerLine.CantGetThere);
                return;
            }
        }
        if (Targets.FindDoor(cmd, Transform.position, false) != null || (cmd.Pronoun && LastTarget is Door))
        {
            UseDoor(cmd, DoorAction.PassThrough);
            return;
        }
        // an item she doesn't carry ("use the keycard" without it)
        foreach (string key in cmd.Keys)
            if (key.StartsWith(PartnerCommands.ItemKeyPrefix, StringComparison.Ordinal)) { Voice.Say(PartnerLine.DontHaveThat); return; }
        Voice.Say(PartnerLine.NotUnderstood);
    }

    // "Heal": uses the named healing item (else the first one she has).
    internal void Heal(ParsedCommand cmd)
    {
        Inventory inventory = Interactor.Inventory;
        HealingItem item = null;
        foreach (string key in cmd.Keys)
            if (inventory.FindById(key) is HealingItem named) { item = named; break; }
        if (item == null) item = inventory.FindFirst<HealingItem>();

        if (item == null) Voice.Say(PartnerLine.NoHealing);
        else if (Health.IsFull) Voice.Say(PartnerLine.NotHurt);
        else Voice.Say(inventory.TryUse(item) ? PartnerLine.Healed : PartnerLine.NotHurt);
    }

    // "Save": walks to the nearest computer in reach and saves there.
    internal void Save()
    {
        SaveStation station = Targets.FindStation(Transform.position);
        if (station == null) { Voice.Say(Targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoSavePoint); return; }
        if (!PathExists(station.GetWalkInPosition(Transform.position))) { Voice.Say(PartnerLine.CantGetThere); return; }
        LastTarget = station;
        Voice.Say(SaveUser.Use(station) ? PartnerLine.Saving : PartnerLine.Busy);
    }

    // Pickup results: taken, part of it, bag full, out of reach.
    internal void OnPickupAttempted(ItemDefinition item, int amount, PickupResult result)
    {
        switch (result)
        {
            case PickupResult.Collected: Voice.Say(PartnerLine.GotIt); break;
            case PickupResult.Partial: Voice.Say(PartnerLine.TookSome); break;
            case PickupResult.InventoryFull: Voice.Say(PartnerLine.BagFull); break;
            case PickupResult.OutOfReach: Voice.Say(PartnerLine.CantReach); break;
        }
    }

    // Door results: locked, needs the operator, unlocked with a key.
    internal void OnDoorFinished(Door door, DoorAction action, DoorResult result)
    {
        switch (result)
        {
            case DoorResult.Locked: Voice.Say(PartnerLine.DoorLocked); break;
            case DoorResult.NeedsOperator: Voice.Say(PartnerLine.DoorNeedsOperator); break;
            case DoorResult.Unlocked: Voice.Say(PartnerLine.DoorUnlocked); break;
        }
    }
}
