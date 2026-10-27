using UnityEngine;
using UnityEngine.AI;

// Base of the command handlers (one per kind of order: moving, doors and items, combat, the casino games...).
// Plain C# objects owned by PartnerCommands, not components: nothing to wire in the scene. This base only gives each
// handler short names for what they share (CommandContext): her components, the tuning, the other handlers, "it".
internal abstract class CommandHandler
{
    // Everything the handlers share.
    protected readonly CommandContext Ctx;

    protected CommandHandler(CommandContext ctx) { Ctx = ctx; }

    // Her components and the scene systems.
    protected PartnerAnimator PartnerAnimator => Ctx.PartnerAnimator;
    protected PartnerInteractor Interactor => Ctx.Interactor;
    protected PartnerCombat Combat => Ctx.Combat;
    protected PartnerShooter Shooter => Ctx.Shooter;
    protected WeaponHolder WeaponHolder => Ctx.WeaponHolder;
    protected PartnerDoorUser DoorUser => Ctx.DoorUser;
    protected PartnerSaveUser SaveUser => Ctx.SaveUser;
    protected Health Health => Ctx.Health;
    protected CommandTargets Targets => Ctx.Targets;
    protected PartnerVoice Voice => Ctx.Voice;
    protected CameraSettings Settings => Ctx.Settings;
    protected CombatEncounter Encounter => Ctx.Encounter;
    protected PartnerEvasion Evasion => Ctx.Evasion;
    protected PartnerOutfit Outfit => Ctx.Outfit;
    protected PartnerCameraPose CameraPose => Ctx.CameraPose;
    protected PartnerCombatStance Stance => Ctx.Stance;
    protected PartnerKeypadUser KeypadUser => Ctx.KeypadUser;
    protected PartnerElevatorUser ElevatorUser => Ctx.ElevatorUser;
    protected PartnerExaminer Examiner => Ctx.Examiner;
    protected Transform Transform => Ctx.Transform;
    protected NavMeshAgent Agent => Ctx.Agent;

    // The last thing she was sent to ("it", "the other one").
    protected Component LastTarget { get => Ctx.LastTarget; set => Ctx.LastTarget = value; }

    // Tuning (the inspector values of PartnerCommands).
    protected float AimHeight => Ctx.Owner.AimHeight;
    protected float MeleeRange => Ctx.Owner.MeleeRange;
    protected float MeleeApproachTimeout => Ctx.Owner.MeleeApproachTimeout;
    protected float DoorStandOff => Ctx.Owner.DoorStandOff;
    protected float BackOffDistance => Ctx.Owner.BackOffDistance;
    protected float AdvanceDistance => Ctx.Owner.AdvanceDistance;
    protected float AdvanceStopDistance => Ctx.Owner.AdvanceStopDistance;
    protected float WallMargin => Ctx.Owner.WallMargin;
    protected float CoverGrid => Ctx.Owner.CoverGrid;
    protected float FireAtWillInterval => Ctx.Owner.FireAtWillInterval;

    // The handlers that others call into.
    protected CombatCommands CombatCommands => Ctx.CombatCommands;
    protected InteractionCommands InteractionCommands => Ctx.InteractionCommands;
    protected RouletteCommands RouletteCommands => Ctx.RouletteCommands;

    // Shared checks (CommandContext).
    protected bool IsActive => Ctx.IsActive;
    protected bool InCombat => Ctx.InCombat;
    protected bool IsBusy => Ctx.IsBusy;
    protected bool PathExists(Vector3 destination) => Ctx.PathExists(destination);
    protected Door DoorTo(Room here, Room room) => Ctx.DoorTo(here, room);
    protected void SayStatus() => Ctx.SayStatus();
    protected void Rerun(ParsedCommand cmd) => Ctx.Rerun(cmd);

    // A vector flattened onto the floor.
    protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
