using UnityEngine;
using UnityEngine.AI;

// Combat stance of the partner: draw / holster, aim at a point while moving (strafe, backpedal),
// shoot (PartnerShooter) and melee (PartnerMelee).
// Commands (voice / text / test input) call Draw, Holster, SelectWeapon, AimAt, StopAiming, Attack, Fire and Reload.
// Attack = "use the weapon in hand": shoots with a firearm, swings with a melee weapon.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerCombat : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string WeaponHolderField = nameof(weaponHolder);
    public const string InteractorField = nameof(interactor);
    public const string MeleeField = nameof(melee);
    public const string ShooterField = nameof(shooter);

    // Her animator, the weapon holder, the interactor (pickups) and the melee / firearm handlers.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerMelee melee;
    [SerializeField] private PartnerShooter shooter;
    [Tooltip("Loadout index drawn by Draw().")]
    [SerializeField] private int selectedWeapon;
    [Tooltip("How fast she turns to keep facing the aim point (deg/s).")]
    [SerializeField] private float aimTurnSpeed = 360f;
    [Tooltip("After a Fire command (not a held aim), she lowers the weapon after this long without shooting (s).")]
    [SerializeField] private float autoLowerDelay = 2f;
    [Tooltip("Fire with no target: point straight ahead at this distance / height (m).")]
    [SerializeField] private Vector2 defaultTarget = new Vector2(10f, 1.2f);

    // Her agent, aim state and point, the auto-lower timer, and the facing point (no raised weapon).
    private NavMeshAgent agent;
    private bool aiming;
    private Vector3 aimPoint;
    private float lowerTimer = -1f;   // < 0 = aim held by AimAt
    private bool facing;
    private Vector3 facePoint;

    // Read-only state for commands, the HUD and the stance.
    public bool IsArmed => partnerAnimator.IsArmed;
    public bool IsAiming => aiming;
    public bool IsFacing => facing;
    public int SelectedWeapon => selectedWeapon;
    public bool HasAnyWeapon => weaponHolder.FirstOwned() >= 0;

    // The selected weapon is a firearm (Attack shoots) / a melee weapon (Attack swings).
    public bool SelectedIsRanged => HasComponent<RangedWeapon>(selectedWeapon);
    public bool SelectedIsMelee => HasComponent<MeleeWeapon>(selectedWeapon);

    // She can act: not walking to a pickup, not turning in place, not scripted.
    private bool CanAct => !interactor.HasPickupTarget && !partnerAnimator.IsTurning && !partnerAnimator.IsScripted;

    // Gets the agent; checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || weaponHolder == null || interactor == null || melee == null || shooter == null)
        {
            Debug.LogError($"{name}: PartnerCombat needs a PartnerAnimator, a WeaponHolder, a PartnerInteractor, a PartnerMelee and a PartnerShooter.", this);
            enabled = false;
        }
    }

    // Listens to pickups (weapon hidden / aim dropped).
    private void OnEnable()
    {
        if (partnerAnimator == null || interactor == null) return;
        partnerAnimator.PickupStarted += OnPickupStarted;
        partnerAnimator.PickupFinished += OnPickupFinished;
        interactor.PickupRequested += StopAiming;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (partnerAnimator == null || interactor == null) return;
        partnerAnimator.PickupStarted -= OnPickupStarted;
        partnerAnimator.PickupFinished -= OnPickupFinished;
        interactor.PickupRequested -= StopAiming;
    }

    // Takes the selected weapon out (or the first one she owns).
    public bool Draw()
    {
        if (partnerAnimator.IsArmed) return true;
        if (partnerAnimator.IsScripted) return false;
        if (!weaponHolder.Owns(selectedWeapon))
        {
            // the selected one isn't hers (yet): the first one she has
            int owned = weaponHolder.FirstOwned();
            if (owned < 0) return false;
            selectedWeapon = owned;
        }
        if (!weaponHolder.Equip(selectedWeapon)) return false;
        partnerAnimator.SetHoldType(weaponHolder.Current.HoldType);
        partnerAnimator.SetArmed(true);
        return true;
    }

    // Chooses the weapon; if she is armed, she switches right away ("use the shotgun").
    public bool SelectWeapon(int index)
    {
        if (partnerAnimator.IsAttacking || partnerAnimator.IsScripted || weaponHolder.GetWeapon(index) == null || !weaponHolder.Owns(index)) return false;
        selectedWeapon = index;
        if (!partnerAnimator.IsArmed) return true;
        if (!weaponHolder.Equip(index)) return false;
        partnerAnimator.SetHoldType(weaponHolder.Current.HoldType);
        return true;
    }

    // Puts the weapon away (not mid-swing).
    public void Holster()
    {
        if (partnerAnimator.IsAttacking) return;
        StopAiming();
        StopFacing();
        weaponHolder.Holster();
        partnerAnimator.SetArmed(false);
    }

    // Keeps facing the point while the command / button holds (she can still move: strafe / backpedal).
    public void AimAt(Vector3 point)
    {
        if (!StartAiming(point)) return;
        lowerTimer = -1f;
    }

    // Keeps her turned toward a point without raising the weapon (a melee weapon on guard, a threat while she moves):
    // walking away from it she backpedals, sideways she strafes. Only while armed.
    public void FaceToward(Vector3 point)
    {
        if (!partnerAnimator.IsArmed) return;
        facePoint = point;
        if (facing) return;
        facing = true;
        partnerAnimator.SetHoldFacing(true);
    }

    // Stops facing the point; the agent turns her again.
    public void StopFacing()
    {
        if (!facing) return;
        facing = false;
        partnerAnimator.SetHoldFacing(false);
        if (!aiming) agent.updateRotation = true;
    }

    // Lowers the weapon and drops any pending shot.
    public void StopAiming()
    {
        shooter.CancelPending();
        lowerTimer = -1f;
        if (!aiming) return;
        aiming = false;
        partnerAnimator.SetAiming(false);
        agent.updateRotation = true;
    }

    // "Attack": shoots with a firearm, swings with a melee weapon. Toward a point, or straight ahead.
    public bool Attack(Vector3? toward = null)
    {
        if (SelectedIsRanged) return Fire(toward ?? DefaultTarget());
        if (!CanAct || !Draw()) return false;
        return melee.Attack(toward);
    }

    // Raises the firearm, turns to the point and shoots once she is aimed.
    public bool Fire(Vector3 target)
    {
        if (!CanAct || !SelectedIsRanged) return false;
        bool wasHeld = aiming && lowerTimer < 0f;
        if (!StartAiming(target)) return false;
        if (!wasHeld) lowerTimer = autoLowerDelay;
        return shooter.RequestFire(target);
    }

    // Reloads the firearm in hand.
    public bool Reload()
    {
        if (partnerAnimator.IsScripted || !partnerAnimator.IsArmed) return false;
        return shooter.Reload();
    }

    // Raises the weapon toward a point (draws it first).
    private bool StartAiming(Vector3 point)
    {
        if (!CanAct || !Draw()) return false;
        aimPoint = point;
        if (aiming) return true;
        aiming = true;
        partnerAnimator.SetAiming(!shooter.IsReloading);
        return true;
    }

    // Each frame: aim pose vs reload, the auto-lower timer and turning toward the aim point.
    private void Update()
    {
        if (!aiming)
        {
            UpdateFacing();
            return;
        }

        // weapon lowered while reloading, raised again after
        bool wantAimPose = !shooter.IsReloading;
        if (partnerAnimator.IsAiming != wantAimPose) partnerAnimator.SetAiming(wantAimPose);

        if (lowerTimer >= 0f)
        {
            if (shooter.IsBusy) lowerTimer = autoLowerDelay;
            else
            {
                lowerTimer -= Time.deltaTime;
                if (lowerTimer < 0f) { StopAiming(); return; }
            }
        }

        agent.updateRotation = false;   // the agent would turn her toward the path, not the target
        Vector3 to = aimPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, aimTurnSpeed * Time.deltaTime);
    }

    // Turns toward the facing point while armed (not during moves that turn her themselves).
    private void UpdateFacing()
    {
        if (!facing) return;
        if (!partnerAnimator.IsArmed) { StopFacing(); return; }
        // the moves that turn her themselves (a swing, a pickup, a scripted clip) keep the rotation
        if (partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp || partnerAnimator.IsScripted || partnerAnimator.IsTurning) return;
        agent.updateRotation = false;
        Vector3 to = facePoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), aimTurnSpeed * Time.deltaTime);
    }

    // Fire with no target: a point straight ahead.
    private Vector3 DefaultTarget() =>
        transform.position + transform.forward * defaultTarget.x + Vector3.up * defaultTarget.y;

    private bool HasComponent<T>(int index) where T : Component
    {
        Weapon w = weaponHolder.GetWeapon(index);
        return w != null && w.TryGetComponent(out T _);
    }

    // picking something up: weapon out of the way, back in hand afterwards
    private void OnPickupStarted()
    {
        StopAiming();
        if (partnerAnimator.IsArmed) weaponHolder.SetVisible(false);
    }

    // Weapon visible again after the pickup.
    private void OnPickupFinished()
    {
        if (partnerAnimator.IsArmed) weaponHolder.SetVisible(true);
    }
}
