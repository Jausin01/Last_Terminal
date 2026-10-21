using System;
using UnityEngine;
using UnityEngine.AI;

// What getting hurt does to her:
// - a hit reaction (light hits: stomach / ribs, heavy ones: a stagger), holding her weapon if she has one out;
//   she stops for it and goes on afterwards; not while seated, opening a door, picking something up or down in a duck
// - badly hurt (below injuredBelow): limping walk and hurting idle, slower; healing brings her back
// - a cry of pain, and a warning the first time she gets badly hurt
// - death: she falls (crumples from a duck) and Died is raised (the game over screen listens); Revive after a load.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerDamage : MonoBehaviour
{
    public const string HealthField = nameof(health);
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string EvasionField = nameof(evasion);
    public const string CombatField = nameof(combat);
    public const string InteractorField = nameof(interactor);
    public const string VoiceField = nameof(voice);

    [SerializeField] private Health health;
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerEvasion evasion;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerVoice voice;
    [Tooltip("Damage from this much up staggers her (the heavy reactions).")]
    [SerializeField] private int heavyDamage = 30;
    [Tooltip("Below this fraction of her health she limps.")]
    [SerializeField, Range(0f, 1f)] private float injuredBelow = 0.35f;
    [Tooltip("Her speed when limping, as a fraction of her walk (injured walk clip: ~1.35 m/s for 1.8).")]
    [SerializeField, Range(0.3f, 1f)] private float injuredSpeed = 0.75f;
    [Tooltip("Seconds to go from walking to limping (and back).")]
    [SerializeField] private float injuredBlendTime = 1f;
    [Tooltip("At most one cry of pain per this many seconds.")]
    [SerializeField] private float cryInterval = 1.2f;

    private NavMeshAgent agent;
    private float baseSpeed;
    private float injured;
    private bool reacting;
    private bool warnedBadlyHurt;
    private float lastCry = -10f;

    public event Action Died;

    public bool IsDead => health != null && health.IsDead;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseSpeed = agent.speed;
        if (health == null || partnerAnimator == null || evasion == null || combat == null || interactor == null || voice == null)
        {
            Debug.LogError($"{name}: PartnerDamage is missing references (run Last Terminal/4).", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void OnDamaged(int amount)
    {
        if (amount <= 0 || health.IsDead) return;   // a killing blow: Died does the rest
        float left = (float)health.Current / health.Max;
        if (left < injuredBelow && !warnedBadlyHurt)
        {
            warnedBadlyHurt = true;
            Cry(PartnerLine.HurtBad, true);
        }
        else Cry(PartnerLine.Hurt, false);

        if (!CanReact()) return;
        bool heavy = amount >= heavyDamage;
        PartnerAnimator.HitKind kind = heavy
            ? (UnityEngine.Random.value < 0.5f ? PartnerAnimator.HitKind.Reaction : PartnerAnimator.HitKind.Reaction2)
            : (UnityEngine.Random.value < 0.5f ? PartnerAnimator.HitKind.Stomach : PartnerAnimator.HitKind.Rib);
        partnerAnimator.PlayHit(kind);
        if (agent.isOnNavMesh) agent.isStopped = true;   // the path is kept: she goes on after the reaction
        reacting = true;
    }

    // in the middle of something that can't be cut (seat, door, pick up, a dodge / duck): the damage, no reaction
    private bool CanReact() =>
        !partnerAnimator.IsScripted && !partnerAnimator.IsPickingUp && !interactor.IsBusy && evasion.Current == PartnerEvasion.Move.None;

    private void Cry(PartnerLine line, bool always)
    {
        if (!always && Time.time - lastCry < cryInterval) return;
        lastCry = Time.time;
        voice.Say(line);
    }

    private void OnDied()
    {
        bool crouched = evasion.IsCrouched;
        evasion.Cancel();
        combat.StopAiming();
        reacting = false;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        partnerAnimator.PlayDeath(crouched);
        voice.Say(PartnerLine.Dying);
        Died?.Invoke();
    }

    // a save was loaded (her health is back): standing again
    public void Revive()
    {
        partnerAnimator.Revive();
        reacting = false;
        warnedBadlyHurt = (float)health.Current / health.Max < injuredBelow;
        injured = warnedBadlyHurt ? 1f : 0f;
        if (agent.isOnNavMesh) agent.isStopped = false;
    }

    private void Update()
    {
        if (health.IsDead) return;
        if (reacting && !partnerAnimator.IsHitReacting)
        {
            reacting = false;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        // limping while badly hurt; healed: walking again (and the warning can come back)
        bool hurt = (float)health.Current / health.Max < injuredBelow;
        if (!hurt) warnedBadlyHurt = false;
        injured = Mathf.MoveTowards(injured, hurt ? 1f : 0f, Time.deltaTime / Mathf.Max(injuredBlendTime, 0.01f));
        partnerAnimator.SetInjured(injured);
        float speed = Mathf.Lerp(1f, injuredSpeed, injured);
        partnerAnimator.SetLocomotionScale(1f / speed);
        agent.speed = baseSpeed * speed;
    }
}
