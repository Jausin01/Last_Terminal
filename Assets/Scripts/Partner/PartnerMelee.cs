using System;
using UnityEngine;
using UnityEngine.AI;

// Melee attacks with the equipped MeleeWeapon (knife, cleaver, bat, axe...).
// Every attack clip is split in phases measured on the Mixamo files (normalized time):
//   wind-up -> strike (hit window inside it) -> recovery.
// Each weapon plays those phases at its own speed (MeleeWeapon), so one clip feels different per weapon.
// The hit volume is the weapon itself (capsule GripPoint -> Muzzle) plus the path of its tip since the last frame,
// so fast swings don't skip a target. Every target is hit once per swing.
// Blades: pressing again after the strike chains the other stab (combo). Blunt: one heavy swing at a time.
// Runs after WeaponHolder (default order), so the hit check uses the weapon already placed in her hand this frame.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerMelee : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string WeaponHolderField = nameof(weaponHolder);

    // AttackId values of the Animator (built by Last Terminal/2)
    public const int KnifeStab1 = 0;
    public const int KnifeStab2 = 1;
    public const int BluntStrike = 2;

    [Serializable]
    private struct AttackTiming
    {
        [Range(0f, 1f)] public float windupEnd;
        [Range(0f, 1f)] public float strikeEnd;
        [Range(0f, 1f)] public float hitStart;
        [Range(0f, 1f)] public float hitEnd;
    }

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private WeaponHolder weaponHolder;

    [Header("Phases of each clip (index = AttackId)")]
    [SerializeField] private AttackTiming[] timings =
    {
        new AttackTiming { windupEnd = 0.24f, strikeEnd = 0.40f, hitStart = 0.24f, hitEnd = 0.38f },   // KnifeStab1
        new AttackTiming { windupEnd = 0.20f, strikeEnd = 0.52f, hitStart = 0.20f, hitEnd = 0.52f },   // KnifeStab2 (mirrored)
        new AttackTiming { windupEnd = 0.50f, strikeEnd = 0.80f, hitStart = 0.58f, hitEnd = 0.80f },   // BluntStrike
    };

    [Header("Targets")]
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("How fast she turns to the target during the wind-up (deg/s).")]
    [SerializeField] private float faceSpeed = 540f;
    [Tooltip("Without a new press within this time after a swing, the blade combo starts over (s).")]
    [SerializeField] private float comboResetTime = 0.8f;

    private readonly Collider[] overlap = new Collider[16];
    private readonly Health[] hitThisSwing = new Health[16];
    private int hitCount;

    private NavMeshAgent agent;
    private Health self;
    private MeleeWeapon weapon;
    private int attackId = -1;
    private int nextBladeAttack = KnifeStab1;
    private float comboTimer;
    private bool queued;
    private bool hasFacePoint;
    private Vector3 facePoint;
    private float hitStopTimer;
    private bool hasLastTip;
    private Vector3 lastTip;

    public event Action<Health, int> Hit;   // target, damage
    public event Action AttackStarted;

    public bool IsAttacking => partnerAnimator.IsAttacking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        self = GetComponent<Health>();
        if (partnerAnimator == null || weaponHolder == null)
        {
            Debug.LogError($"{name}: PartnerMelee needs a PartnerAnimator and a WeaponHolder.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (partnerAnimator != null) partnerAnimator.AttackFinished += OnAttackFinished;
    }

    private void OnDisable()
    {
        if (partnerAnimator != null) partnerAnimator.AttackFinished -= OnAttackFinished;
    }

    // True if the equipped weapon can strike (a melee weapon in her hand).
    public bool CanAttack => weaponHolder.IsEquipped && weaponHolder.Current.TryGetComponent(out MeleeWeapon _);

    // Swing toward a point (or straight ahead). During a swing the press is remembered for the next one.
    public bool Attack(Vector3? toward = null)
    {
        if (!enabled || !CanAttack || partnerAnimator.IsPickingUp) return false;
        hasFacePoint = toward.HasValue;
        if (hasFacePoint) facePoint = toward.Value;

        if (partnerAnimator.IsAttacking)
        {
            queued = true;   // played when the current strike ends (blade combo) or when the swing ends (blunt)
            return true;
        }
        StartAttack();
        return true;
    }

    private void StartAttack()
    {
        weapon = weaponHolder.Current.GetComponent<MeleeWeapon>();
        bool blade = weaponHolder.Current.HoldType == WeaponHoldType.Blade;
        if (blade)
        {
            if (!partnerAnimator.IsAttacking && comboTimer <= 0f) nextBladeAttack = KnifeStab1;
            attackId = nextBladeAttack;
            nextBladeAttack = attackId == KnifeStab1 ? KnifeStab2 : KnifeStab1;
        }
        else attackId = BluntStrike;

        queued = false;
        hitCount = 0;
        hasLastTip = false;
        hitStopTimer = 0f;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updateRotation = false;

        partnerAnimator.PlayAttack(attackId);
        AttackStarted?.Invoke();
    }

    private void Update()
    {
        if (comboTimer > 0f) comboTimer -= Time.deltaTime;
        if (!partnerAnimator.IsAttacking || attackId < 0 || attackId >= timings.Length) return;

        float t = partnerAnimator.AttackProgress;
        if (t < 0f) return;   // blending into the swing
        AttackTiming timing = timings[attackId];

        // blade combo: the next stab starts as soon as this strike is done
        if (queued && t >= timing.strikeEnd && CanAttack && weaponHolder.Current.HoldType == WeaponHoldType.Blade)
        {
            StartAttack();
            return;
        }

        if (t < timing.windupEnd) FaceTarget();
        UpdateSpeed(t, timing);
    }

    // after the animation and after WeaponHolder.LateUpdate (execution order above)
    private void LateUpdate()
    {
        if (!partnerAnimator.IsAttacking || weapon == null || attackId < 0 || attackId >= timings.Length) return;
        float t = partnerAnimator.AttackProgress;
        AttackTiming timing = timings[attackId];
        if (t < timing.hitStart || t > timing.hitEnd)
        {
            hasLastTip = false;
            return;
        }

        Vector3 grip = weapon.HitStart;
        Vector3 tip = weapon.HitEnd;
        CheckHits(grip, tip);
        if (hasLastTip) CheckHits(lastTip, tip);   // path of the tip since the last frame
        lastTip = tip;
        hasLastTip = true;
    }

    private void UpdateSpeed(float t, AttackTiming timing)
    {
        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.deltaTime;
            partnerAnimator.SetAttackSpeed(0f);
            return;
        }
        float speed = t < timing.windupEnd ? weapon.WindupSpeed
                    : t < timing.strikeEnd ? weapon.StrikeSpeed
                    : weapon.RecoverySpeed;
        partnerAnimator.SetAttackSpeed(speed);
    }

    private void CheckHits(Vector3 a, Vector3 b)
    {
        int count = Physics.OverlapCapsuleNonAlloc(a, b, weapon.HitRadius, overlap, hitMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Health target = overlap[i].GetComponentInParent<Health>();
            if (target == null || target == self || target.IsDead || AlreadyHit(target)) continue;
            if (hitCount < hitThisSwing.Length) hitThisSwing[hitCount++] = target;

            target.TakeDamage(weapon.Damage);
            Hit?.Invoke(target, weapon.Damage);
            if (hitStopTimer <= 0f && weapon.HitStop > 0f)
            {
                hitStopTimer = weapon.HitStop;
                partnerAnimator.SetAttackSpeed(0f);
            }
        }
    }

    private bool AlreadyHit(Health target)
    {
        for (int i = 0; i < hitCount; i++)
            if (hitThisSwing[i] == target) return true;
        return false;
    }

    private void FaceTarget()
    {
        if (!hasFacePoint) return;
        Vector3 to = facePoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), faceSpeed * Time.deltaTime);
    }

    private void OnAttackFinished()
    {
        agent.nextPosition = transform.position;
        agent.isStopped = false;
        agent.updateRotation = true;
        attackId = -1;
        weapon = null;
        comboTimer = comboResetTime;

        // pressed during a blunt swing (or too late for the combo): next swing now
        if (queued && CanAttack) StartAttack();
        queued = false;
    }
}
