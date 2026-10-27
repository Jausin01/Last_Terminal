using System;
using UnityEngine;
using UnityEngine.AI;

// Her fight: drawing / holstering, attacking (a gun fires, a melee weapon walks up and swings), aiming and keeping
// the aim on a target, reloading, fire at will, combat position / relax, stop. Owns the combat state (the aim, the
// melee approach, fire at will, facing a threat while she moves) and updates it every frame (Tick). Also the
// loadout questions the other handlers ask (which weapon was named, is it a firearm).
internal sealed class CombatCommands : CommandHandler
{
    // Melee approach: the target she is walking to and when she gives up.
    private Health meleeTarget;
    private float meleeUntil;
    // "Aim at it": the target her gun stays on.
    private Health aimTarget;
    // "Fire at will": she shoots on her own, and when the next shot is allowed.
    private bool fireAtWill;
    private float nextAutoShot;
    private Health coverFacing;      // backing off / taking cover with a melee weapon: turned toward it while she moves

    public CombatCommands(CommandContext ctx) : base(ctx) { }

    // "Stop": she stays where she is and lowers her guard.
    internal void Stop()
    {
        meleeTarget = null;
        fireAtWill = false;
        EndStance();
        Combat.StopAiming();
        if (!Evasion.IsCrouched) Interactor.MoveTo(Transform.position);   // down: she just stays down
        Voice.Say(PartnerLine.Stopping);
    }

    // "Draw (the pistol)": takes a weapon out.
    internal void Draw(ParsedCommand cmd)
    {
        if (TryWeapon(cmd, out int weapon)) { DrawWeapon(weapon); return; }
        if (HasUnknownWeapon(cmd)) { Voice.Say(PartnerLine.DontHaveThat); return; }
        if (!Combat.HasAnyWeapon) { Voice.Say(PartnerLine.DontHaveThat); return; }   // nothing picked up yet
        Voice.Say(Combat.Draw() ? PartnerLine.WeaponReady : PartnerLine.Busy);
    }

    // Selects and draws a weapon of the loadout.
    internal void DrawWeapon(int index)
    {
        if (!Combat.SelectWeapon(index) || !Combat.Draw()) { Voice.Say(PartnerLine.Busy); return; }
        Voice.Say(PartnerLine.WeaponReady);
    }

    // "Attack / shoot (X)": fires at the target with a gun, or walks up to it and swings with a melee weapon.
    internal void Attack(ParsedCommand cmd, bool shoot)
    {
        if (!Combat.HasAnyWeapon) { Voice.Say(PartnerLine.DontHaveThat); return; }   // nothing picked up yet
        if (TryWeapon(cmd, out int weapon) && !Combat.SelectWeapon(weapon)) { Voice.Say(PartnerLine.Busy); return; }

        if (!TryCombatTarget(cmd, out Health target)) return;
        if (target != null && Encounter != null) Encounter.Provoke(target);   // attacking it starts the fight

        // "shoot" with a melee weapon in hand: the first firearm of the loadout
        if (shoot && !Combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { Voice.Say(PartnerLine.NoGun); return; }
            if (!Combat.SelectWeapon(ranged)) { Voice.Say(PartnerLine.Busy); return; }
        }

        if (Combat.SelectedIsRanged)
        {
            if (target == null) { Voice.Say(PartnerLine.NoTarget); return; }
            SetTarget(target);
            if (aimTarget != null) aimTarget = target;   // aiming: the aim follows the new target
            // an empty gun answers through PartnerShooter.OutOfAmmo
            Combat.Fire(AimPoint(target));
            return;
        }

        // melee
        if (target == null) { Combat.Attack(); return; }
        SetTarget(target);
        Vector3 to = Flat(target.transform.position - Transform.position);
        if (to.magnitude <= MeleeRange) { Combat.Attack(target.transform.position + Vector3.up * AimHeight); return; }
        Vector3 stand = target.transform.position - to.normalized * (MeleeRange * 0.8f);
        if (!NavMesh.SamplePosition(stand, out NavMeshHit hit, 0.8f, NavMesh.AllAreas) || !PathExists(hit.position) || !Interactor.MoveTo(hit.position))
        {
            Voice.Say(PartnerLine.CantGetThere);
            return;
        }
        Combat.Draw();
        meleeTarget = target;
        meleeUntil = Time.time + MeleeApproachTimeout;
        Voice.Say(PartnerLine.Acknowledge);
    }

    // "Hold fire": she stops shooting on her own and drops a queued shot.
    internal void HoldFire()
    {
        fireAtWill = false;
        Shooter.CancelPending();
        Voice.Say(PartnerLine.HoldingFire);
    }

    // she picks her targets herself (the one she is on, then the closest) and keeps shooting until told to stop
    internal void FireAtWill()
    {
        if (!Combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { Voice.Say(PartnerLine.NoGun); return; }
            if (!Combat.SelectWeapon(ranged)) { Voice.Say(PartnerLine.Busy); return; }
        }
        if (!Combat.Draw()) { Voice.Say(PartnerLine.Busy); return; }
        if (Threat() == null) { Voice.Say(PartnerLine.NoTarget); return; }
        fireAtWill = true;
        nextAutoShot = 0f;
        Voice.Say(PartnerLine.FireAtWill);
    }

    // with a weapon in hand she keeps facing the threat while she moves (she backpedals / strafes):
    // a gun stays up on it, a melee weapon stays on guard toward it until she gets there
    internal void CoverWhileMoving(Health threat)
    {
        if (threat == null || !Combat.IsArmed) return;
        if (Combat.SelectedIsRanged)
        {
            if (!Alive(aimTarget)) { aimTarget = threat; SetTarget(threat); }
            return;
        }
        coverFacing = threat;
        Combat.FaceToward(threat.transform.position);
    }

    // "combat position": she keeps her distance with a gun, stays on guard with a melee weapon (PartnerCombatStance)
    internal void CombatStance()
    {
        if (Stance == null) { Voice.Say(PartnerLine.NotUnderstood); return; }
        if (!Stance.Begin()) { Voice.Say(PartnerLine.Busy); return; }
        Voice.Say(Combat.SelectedIsRanged ? PartnerLine.StanceRanged : PartnerLine.StanceMelee);
    }

    // "relax": out of the stance, weapon still in hand (lowered)
    internal void StandDown()
    {
        fireAtWill = false;
        aimTarget = null;
        EndStance();
        Combat.StopAiming();
        Voice.Say(PartnerLine.StandingDown);
    }

    // Ends the combat stance and any facing toward a threat.
    private void EndStance()
    {
        coverFacing = null;
        if (Stance != null) Stance.End();
        Combat.StopFacing();
    }

    // The stance found no room to back away.
    internal void OnCornered() => Voice.Say(PartnerLine.Cornered);

    // the hostile she is dealing with: her aim, the focus, else the closest one
    internal Health Threat()
    {
        if (Alive(aimTarget)) return aimTarget;
        if (Encounter == null || !Encounter.InCombat) return null;
        if (Alive(Encounter.Focus)) return Encounter.Focus;
        return Encounter.Nearest(Transform.position);
    }

    // Fire at will: keeps a gun on the threat, reloads, fires at a steady pace.
    private void UpdateFireAtWill()
    {
        if (!InCombat) { fireAtWill = false; return; }
        if (!Combat.IsArmed || !Combat.SelectedIsRanged || PartnerAnimator.IsScripted || Evasion.IsBusy) return;
        Health target = Threat();
        if (target == null) return;
        if (aimTarget != target) { aimTarget = target; SetTarget(target); }
        if (Shooter.IsBusy || Time.time < nextAutoShot) return;
        if (Shooter.CurrentRounds <= 0)
        {
            if (Shooter.CurrentReserve > 0) { Combat.Reload(); Voice.Say(PartnerLine.Reloading); }
            else { fireAtWill = false; Voice.Say(PartnerLine.OutOfAmmo); }
            nextAutoShot = Time.time + FireAtWillInterval;
            return;
        }
        Combat.Fire(AimPoint(target));
        nextAutoShot = Time.time + FireAtWillInterval;
    }

    // keeps the gun on a target (switching targets: "aim at 2")
    internal void Aim(ParsedCommand cmd)
    {
        if (TryWeapon(cmd, out int weapon) && !Combat.SelectWeapon(weapon)) { Voice.Say(PartnerLine.Busy); return; }
        if (!TryCombatTarget(cmd, out Health target)) return;
        if (target == null) { Voice.Say(PartnerLine.NoTarget); return; }
        if (!Combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { Voice.Say(PartnerLine.NoGun); return; }
            if (!Combat.SelectWeapon(ranged)) { Voice.Say(PartnerLine.Busy); return; }
        }
        SetTarget(target);
        aimTarget = target;
        Combat.AimAt(AimPoint(target));
        Voice.Say(PartnerLine.Aiming);
    }

    // The target of a combat command: "enemy 2" (its number), a name / "the other one" / left / right, "it" = the last one;
    // a plain "shoot" stays on the target she is aiming at / fighting. False = that number isn't in the fight (answered).
    private bool TryCombatTarget(ParsedCommand cmd, out Health target)
    {
        target = null;
        if (cmd.Number > 0 && Encounter != null)
        {
            target = Encounter.Get(cmd.Number);
            if (target == null) { Voice.Say(PartnerLine.NoSuchTarget); return false; }
            return true;
        }
        bool plain = cmd.Modifier == CommandModifier.None && !cmd.Pronoun &&
                     !cmd.Keys.Exists(k => !k.StartsWith(PartnerCommands.WeaponKeyPrefix, StringComparison.Ordinal));
        if (plain && Alive(aimTarget)) target = aimTarget;
        else if (plain && Encounter != null && Alive(Encounter.Focus)) target = Encounter.Focus;
        else if (plain && Encounter != null && Encounter.InCombat) target = Encounter.Nearest(Transform.position);   // "shoot": the closest one
        if (target == null) target = Targets.FindShootable(cmd, Transform.position, false, LastTarget);
        if (target == null && cmd.Pronoun && LastTarget is Health last && Alive(last)) target = last;
        return true;
    }

    // Remembers the target and makes it the fight's focus (the HUD highlights it).
    private void SetTarget(Health target)
    {
        LastTarget = target;
        if (Encounter != null) Encounter.SetFocus(target);
    }

    // Where shots aim on a target (above its feet).
    private Vector3 AimPoint(Health target) => target.transform.position + Vector3.up * AimHeight;

    // A target that is still there and alive.
    private static bool Alive(Health h) => h != null && !h.IsDead && h.gameObject.activeInHierarchy;

    // "Reload": only with a gun that isn't full and with ammo left.
    internal void Reload()
    {
        RangedWeapon gun = Combat.IsArmed ? Shooter.Current : null;
        if (gun == null) { Voice.Say(PartnerLine.NoGun); return; }
        if (Shooter.CurrentRounds >= gun.MagazineSize) { Voice.Say(PartnerLine.AlreadyLoaded); return; }
        if (Shooter.CurrentReserve <= 0) { Voice.Say(PartnerLine.OutOfAmmo); return; }
        Voice.Say(Combat.Reload() ? PartnerLine.Reloading : PartnerLine.Busy);
    }

    // Answers to system events: out of ammo, a fight starts / ends, a target went down.
    internal void OnOutOfAmmo(RangedWeapon weapon) => Voice.Say(PartnerLine.OutOfAmmo);

    // A fight started: she calls it.
    internal void OnCombatStarted() => Voice.Say(PartnerLine.CombatStart);

    // The fight ended: no more shooting on her own.
    internal void OnCombatEnded() => fireAtWill = false;

    // The target she was on went down.
    internal void OnTargetDown(Health target)
    {
        if (target == LastTarget) Voice.Say(PartnerLine.TargetDown);
    }

    // a weapon of her loadout named in the command (lexicon key = weapon object name)
    internal bool TryWeapon(ParsedCommand cmd, out int index)
    {
        for (int i = 0; i < WeaponHolder.LoadoutCount; i++)
        {
            Weapon w = WeaponHolder.GetWeapon(i);
            if (w != null && WeaponHolder.Owns(i) && cmd.HasKey(w.name)) { index = i; return true; }
        }
        index = -1;
        return false;
    }

    // a weapon name she doesn't carry ("draw the axe" without an axe)
    private bool HasUnknownWeapon(ParsedCommand cmd)
    {
        foreach (string key in cmd.Keys)
            if (key.StartsWith(PartnerCommands.WeaponKeyPrefix, StringComparison.Ordinal)) return true;
        return false;
    }

    // Is the loadout weapon at this index a firearm?
    internal bool IsRanged(int index)
    {
        Weapon w = WeaponHolder.GetWeapon(index);
        return w != null && w.TryGetComponent(out RangedWeapon _);
    }

    // First firearm she owns (-1 = none).
    private int FirstRangedWeapon()
    {
        for (int i = 0; i < WeaponHolder.LoadoutCount; i++)
        {
            Weapon w = WeaponHolder.GetWeapon(i);
            if (w != null && WeaponHolder.Owns(i) && w.TryGetComponent(out RangedWeapon _)) return i;
        }
        return -1;
    }

    // Drops the stance, any facing, the held aim and fire at will (another kind of order takes over).
    internal void LowerGuard()
    {
        EndStance();
        aimTarget = null;
        fireAtWill = false;
    }

    // Forgets the melee approach (a new command, a dodge...).
    internal void CancelMelee() => meleeTarget = null;

    // Forgets the target her aim was held on.
    internal void DropAim() => aimTarget = null;

    // "Put it away": no more fire at will, out of the stance, weapon holstered.
    internal void Holster()
    {
        fireAtWill = false;
        EndStance();
        Combat.Holster();
        Voice.Say(PartnerLine.WeaponAway);
    }

    // Each frame: fire at will, when it is on.
    internal void TickFireAtWill()
    {
        if (fireAtWill) UpdateFireAtWill();
    }

    // Each frame: the aim held on a target, facing the threat while she moves, the melee approach.
    internal void Tick()
    {
        // aim held on a target until it goes down (or she is put away / busy)
        if (aimTarget != null)
        {
            if (!Alive(aimTarget) || !Combat.IsArmed || PartnerAnimator.IsScripted)
            {
                aimTarget = null;
                Combat.StopAiming();
            }
            else Combat.AimAt(AimPoint(aimTarget));
        }

        // melee weapon, backing off / taking cover: facing the threat until she gets there
        if (coverFacing != null)
        {
            bool moving = Agent.pathPending || (Agent.hasPath && Agent.remainingDistance > Agent.stoppingDistance + 0.1f);
            if (!Alive(coverFacing) || !moving || !Combat.IsArmed)
            {
                coverFacing = null;
                if (Stance == null || !Stance.IsActive) Combat.StopFacing();
            }
            else Combat.FaceToward(coverFacing.transform.position);
        }

        if (meleeTarget == null) return;
        if (meleeTarget.IsDead || Time.time > meleeUntil || PartnerAnimator.IsScripted) { meleeTarget = null; return; }
        if (Flat(meleeTarget.transform.position - Transform.position).magnitude > MeleeRange) return;
        Interactor.MoveTo(Transform.position);
        Combat.Attack(meleeTarget.transform.position + Vector3.up * AimHeight);
        meleeTarget = null;
    }
}
