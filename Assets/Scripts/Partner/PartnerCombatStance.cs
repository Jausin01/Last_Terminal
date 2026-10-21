using System;
using UnityEngine;
using UnityEngine.AI;

// "Combat position": she fights on her own judgment until told to relax (or she puts the weapon away / flees).
// - firearm: the gun stays up on the threat (the focus, else the closest one) and she keeps her distance: one
//   comes closer than tooClose -> she backs off (backpedalling, the gun still on it) to a spot of her room farther
//   from all of them; cornered, she says so and holds. Shooting stays the operator's call (or "fire at will").
// - melee weapon: on guard, turned toward the threat; she doesn't go in alone ("attack", "dodge" are the operator's).
//   After each swing she steps back out of its reach (hit and back off).
// Her own moves never override an order: while she walks somewhere she was sent, she only keeps facing the threat.
// Nothing happens while she is busy (a dodge, down, a door, a pickup, a hit reaction, seated).
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerCombatStance : MonoBehaviour
{
    public const string CombatField = nameof(combat);
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string EvasionField = nameof(evasion);
    public const string EncounterField = nameof(encounter);
    public const string HealthField = nameof(health);

    [SerializeField] private PartnerCombat combat;
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerEvasion evasion;
    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private Health health;

    [Header("Firearm: keeping the distance")]
    [Tooltip("A threat this close makes her back off (m).")]
    [SerializeField] private float tooClose = 4f;
    [Tooltip("She backs off to be at least this far from every threat, when the room allows it (m).")]
    [SerializeField] private float comfortable = 6.5f;
    [Tooltip("Spots this close to the walls are not used (m).")]
    [SerializeField] private float wallMargin = 0.7f;
    [Tooltip("Seconds between two decisions.")]
    [SerializeField] private float thinkInterval = 0.4f;
    [Tooltip("Shots aim this high above the target's feet (m).")]
    [SerializeField] private float aimHeight = 1.2f;

    [Header("Melee: hit and back off")]
    [Tooltip("After a swing she steps back to this distance from the target (m).")]
    [SerializeField] private float guardDistance = 1.9f;

    private NavMeshAgent agent;
    private NavMeshPath path;
    private bool active;
    private bool raisedGun;          // the aim is the stance's (lowered when there is no threat / it ends)
    private bool ownMove;            // the current destination is hers (the stance's), not an order
    private Vector3 ownDestination;
    private float thinkTimer;
    private bool saidCornered;
    private static readonly float[] Angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

    public event Action Cornered;    // first time she can't back off any farther (PartnerCommands answers)

    public bool IsActive => active;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        if (combat == null || partnerAnimator == null || interactor == null || evasion == null || encounter == null || health == null)
        {
            Debug.LogError($"{name}: PartnerCombatStance is missing references (run Last Terminal/4).", this);
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

    // false = no weapon to take out
    public bool Begin()
    {
        if (!enabled || !combat.Draw()) return false;
        active = true;
        saidCornered = false;
        thinkTimer = 0f;
        return true;
    }

    public void End()
    {
        if (!active) return;
        active = false;
        if (raisedGun) combat.StopAiming();
        raisedGun = false;
        combat.StopFacing();
        if (ownMove && agent.hasPath && (agent.destination - ownDestination).sqrMagnitude < 0.04f) interactor.MoveTo(transform.position);
        ownMove = false;
    }

    private void Update()
    {
        if (!active) return;
        if (health.IsDead || !combat.IsArmed) { End(); return; }   // put away / dead: the stance is over

        Health threat = Threat();
        if (threat == null)
        {
            if (raisedGun) { combat.StopAiming(); raisedGun = false; }
            combat.StopFacing();
            saidCornered = false;
            return;
        }

        // the weapon on the threat: a gun raised, a melee weapon on guard
        Vector3 at = threat.transform.position;
        if (combat.SelectedIsRanged)
        {
            combat.StopFacing();
            combat.AimAt(at + Vector3.up * aimHeight);
            raisedGun = true;
        }
        else
        {
            if (raisedGun) { combat.StopAiming(); raisedGun = false; }
            combat.FaceToward(at);
        }

        if (ownMove && !OwnMoveGoing()) ownMove = false;
        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        thinkTimer = thinkInterval;
        if (!combat.SelectedIsRanged || Busy || OrderGoing()) return;
        KeepDistance();
    }

    // she is doing something the stance must not cut
    private bool Busy =>
        evasion.IsBusy || evasion.IsCrouched || interactor.IsBusy || partnerAnimator.IsScripted ||
        partnerAnimator.IsAttacking || partnerAnimator.IsHitReacting || partnerAnimator.IsTurning;

    // walking somewhere the operator sent her (not the stance's own move)
    private bool OrderGoing() => agent.hasPath && !ownMove && agent.remainingDistance > agent.stoppingDistance + 0.1f;

    private bool OwnMoveGoing() =>
        agent.hasPath && (agent.destination - ownDestination).sqrMagnitude < 0.04f && agent.remainingDistance > agent.stoppingDistance + 0.1f;

    private void KeepDistance()
    {
        Vector3 me = transform.position;
        float closest = encounter.DistanceToThreat(me);
        if (closest >= tooClose) { saidCornered = false; return; }
        if (ownMove) return;   // already backing off (re-planned once she gets there, if still too close)

        // away from all of them (the closest count the most)
        Vector3 away = Vector3.zero;
        foreach (Health h in encounter.Engaged)
        {
            if (h == null || h.IsDead) continue;
            Vector3 d = Flat(me - h.transform.position);
            float dist = Mathf.Max(d.magnitude, 0.3f);
            away += d / (dist * dist);
        }
        away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;

        Room room = encounter.Area;
        float step = Mathf.Max(comfortable - closest, 1.5f);
        Vector3 best = me;
        float bestScore = closest + 0.75f;   // must be worth it
        foreach (float scale in new[] { 1f, 0.6f })
            foreach (float angle in Angles)
            {
                Vector3 wanted = me + Quaternion.AngleAxis(angle, Vector3.up) * away * (step * scale);
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (room != null && !room.Contains(hit.position, -wallMargin)) continue;
                float score = encounter.DistanceToThreat(hit.position) - Mathf.Abs(angle) * 0.004f;   // straight back preferred
                if (score <= bestScore || !PathExists(hit.position)) continue;
                bestScore = score;
                best = hit.position;
            }

        if (bestScore <= closest + 0.75f)
        {
            if (!saidCornered) { saidCornered = true; Cornered?.Invoke(); }
            return;
        }
        if (!interactor.MoveTo(best)) return;
        ownMove = true;
        ownDestination = agent.destination;
        saidCornered = false;
    }

    // melee, after a swing: a step back out of its reach (only if it is still close and nothing was ordered)
    private void OnAttackFinished()
    {
        if (!active || health.IsDead || combat.SelectedIsRanged) return;
        Health threat = Threat();
        if (threat == null) return;
        Vector3 me = transform.position;
        Vector3 d = Flat(me - threat.transform.position);
        float dist = d.magnitude;
        if (dist >= guardDistance || dist < 0.01f) return;
        Vector3 wanted = threat.transform.position + d / dist * guardDistance;
        Room room = encounter.Area;
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) return;
        if (room != null && !room.Contains(hit.position, -wallMargin * 0.5f)) return;
        if (!PathExists(hit.position) || !interactor.MoveTo(hit.position)) return;
        ownMove = true;
        ownDestination = agent.destination;
    }

    // the one she is dealing with: the focus (the operator's last target), else the closest
    private Health Threat()
    {
        if (!encounter.InCombat) return null;
        Health focus = encounter.Focus;
        if (focus != null && !focus.IsDead && focus.gameObject.activeInHierarchy) return focus;
        return encounter.Nearest(transform.position);
    }

    private bool PathExists(Vector3 destination) =>
        agent.isOnNavMesh && NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path) &&
        path.status == NavMeshPathStatus.PathComplete;

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
