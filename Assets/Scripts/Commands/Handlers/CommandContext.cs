using System;
using UnityEngine;
using UnityEngine.AI;

// What every command handler shares: her components (the serialized fields of PartnerCommands), the tuning, the last
// thing she was sent to ("it"), the handlers themselves (they call each other through here) and the checks several of
// them need. Made once by PartnerCommands (Awake); handlers reach it through CommandHandler.
internal sealed class CommandContext
{
    // The component that owns the commands: its tuning is read live (inspector edits apply in Play mode) and it is enabled.
    public readonly PartnerCommands Owner;

    // Her components and the scene systems the commands drive.
    public readonly PartnerAnimator PartnerAnimator;
    public readonly PartnerInteractor Interactor;
    public readonly PartnerCombat Combat;
    public readonly PartnerShooter Shooter;
    public readonly WeaponHolder WeaponHolder;
    public readonly PartnerDoorUser DoorUser;
    public readonly PartnerSaveUser SaveUser;
    public readonly Health Health;
    public readonly CommandTargets Targets;
    public readonly PartnerVoice Voice;
    public readonly CameraSettings Settings;
    public readonly CombatEncounter Encounter;           // optional
    public readonly PartnerEvasion Evasion;
    public readonly PartnerOutfit Outfit;                // optional
    public readonly PartnerCameraPose CameraPose;        // optional
    public readonly PartnerCombatStance Stance;          // optional
    public readonly PartnerKeypadUser KeypadUser;        // optional
    public readonly PartnerElevatorUser ElevatorUser;    // optional
    public readonly PartnerExaminer Examiner;            // optional

    // Her transform, her agent and a reusable path (reachability checks).
    public readonly Transform Transform;
    public readonly NavMeshAgent Agent;
    private readonly NavMeshPath path = new NavMeshPath();

    // The last thing she was sent to ("it", "the other one").
    public Component LastTarget;

    // Runs a command again from the top (after she got up / left a keypad / left a game).
    public readonly Action<ParsedCommand> Rerun;

    // The handlers (set right after this context is made).
    public NavigationCommands NavigationCommands;
    public InteractionCommands InteractionCommands;
    public CombatCommands CombatCommands;
    public CombatMoveCommands CombatMoveCommands;
    public KeypadCommands KeypadCommands;
    public ElevatorCommands ElevatorCommands;
    public SlotCommands SlotCommands;
    public CardTableCommands CardTableCommands;
    public RouletteCommands RouletteCommands;
    public PrizeCounterCommands PrizeCounterCommands;
    public OutfitCommands OutfitCommands;

    public CommandContext(PartnerCommands owner, Action<ParsedCommand> rerun,
                          PartnerAnimator partnerAnimator, PartnerInteractor interactor, PartnerCombat combat, PartnerShooter shooter,
                          WeaponHolder weaponHolder, PartnerDoorUser doorUser, PartnerSaveUser saveUser, Health health,
                          CommandTargets targets, PartnerVoice voice, CameraSettings settings, CombatEncounter encounter,
                          PartnerEvasion evasion, PartnerOutfit outfit, PartnerCameraPose cameraPose, PartnerCombatStance stance,
                          PartnerKeypadUser keypadUser, PartnerElevatorUser elevatorUser, PartnerExaminer examiner)
    {
        Owner = owner;
        Rerun = rerun;
        Transform = owner.transform;
        Agent = owner.GetComponent<NavMeshAgent>();
        PartnerAnimator = partnerAnimator;
        Interactor = interactor;
        Combat = combat;
        Shooter = shooter;
        WeaponHolder = weaponHolder;
        DoorUser = doorUser;
        SaveUser = saveUser;
        Health = health;
        Targets = targets;
        Voice = voice;
        Settings = settings;
        Encounter = encounter;
        Evasion = evasion;
        Outfit = outfit;
        CameraPose = cameraPose;
        Stance = stance;
        KeypadUser = keypadUser;
        ElevatorUser = elevatorUser;
        Examiner = examiner;
    }

    // The commands are on and she is alive (checked by the entry points that can be called from outside, e.g. a click).
    public bool IsActive => Owner.enabled && !Health.IsDead;

    // A fight is going on.
    public bool InCombat => Encounter != null && Encounter.InCombat;

    // in the middle of something she can't drop (seated, door clip, picking up)
    public bool IsBusy => SaveUser.IsBusy || !DoorUser.IsInterruptible || Interactor.IsBusy || PartnerAnimator.IsScripted || Evasion.IsBusy ||
                          (ElevatorUser != null && ElevatorUser.IsBusy);

    // "How are you?": fine / hurt / badly hurt from her health.
    public void SayStatus()
    {
        float f = Health.Max > 0 ? (float)Health.Current / Health.Max : 0f;
        Voice.Say(f > Owner.StatusThresholds.x ? PartnerLine.StatusFine : f > Owner.StatusThresholds.y ? PartnerLine.StatusHurt : PartnerLine.StatusBadlyHurt);
    }

    // Can she walk there (a complete NavMesh path)?
    public bool PathExists(Vector3 destination)
    {
        if (!Agent.isOnNavMesh) return false;
        return NavMesh.CalculatePath(Transform.position, destination, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    // the door from her room to that one (else the one named after it)
    public Door DoorTo(Room here, Room room)
    {
        Door d = Targets.DoorBetween(here, room);
        return d != null ? d : FindDoorNamed(room.DisplayName);
    }

    // The door named after a room (its display name).
    private Door FindDoorNamed(string displayName)
    {
        foreach (Door d in Targets.Doors)
            if (d != null && d.DisplayName == displayName) return d;
        return null;
    }
}
