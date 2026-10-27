using System;
using UnityEngine;
using UnityEngine.AI;

// What an enemy does (one per enemy, on its root with the NavMeshAgent, Health and the body collider):
// - unaware: stands, wanders around its room or is crouched feeding (its start mode)
// - notices her: sees her (range, field of view, nothing in between), hears her close by, is hurt, or hears a shot
//   in its room; a feeding one only hears her very close and gets up first; some scream before coming
// - chases her and attacks when she is in reach (attack chosen at random among the ones its type / weapon can do);
//   the hit lands at the clip's hit time if she is still in front and in reach (her dodge / duck counts: Health)
// - she leaves its room for a while (or dies): it gives up and walks back to where it started
// - hurt: flinches (EnemyAnimator); a heavy hit staggers it (attack interrupted, stops for a moment)
// - dead: falls (backward when hit from the front), drops its weapon; saves keep who is dead (SaveSystem)
// Every reference is assigned by the builders (Last Terminal/7 prefabs, Last Terminal/4 scene); nothing is searched.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyBrain : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string TypeField = nameof(type);
    public const string EnemyAnimatorField = nameof(enemyAnimator);
    public const string WeaponField = nameof(weapon);
    public const string BodyColliderField = nameof(bodyCollider);
    public const string StartModeField = nameof(startMode);
    public const string SaveIdField = nameof(saveId);
    public const string PartnerField = nameof(partner);
    public const string PartnerHealthField = nameof(partnerHealth);
    public const string ShooterField = nameof(shooter);
    public const string TargetsField = nameof(targets);

    // What the enemy is doing (a small state machine, see the top of the file).
    public enum State { Unaware, Rising, Screaming, Chasing, Attacking, Returning, Dead }

    // Its type (stats, attacks) and its animator wrapper.
    [SerializeField] private EnemyType type;
    [SerializeField] private EnemyAnimator enemyAnimator;
    [Tooltip("Optional: the melee weapon in its hand.")]
    [SerializeField] private EnemyWeapon weapon;
    // Its body collider (off once dead) and how it starts.
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private EnemyStartMode startMode = EnemyStartMode.Idle;
    [Tooltip("Unique in the scene: its entry in the save files.")]
    [SerializeField] private string saveId = "enemy";

    // Helena (position, health), her shooter (shots alert it) and the rooms.
    [Header("Scene")]
    [SerializeField] private Transform partner;
    [SerializeField] private Health partnerHealth;
    [Tooltip("Optional: her shots alert the enemies of the room.")]
    [SerializeField] private PartnerShooter shooter;
    [SerializeField] private CommandTargets targets;

    // Eye height for the line of sight (m).
    [Header("Tuning")]
    [SerializeField] private float eyeHeight = 1.6f;
    [Tooltip("Her chest, for the line of sight (m above her feet).")]
    [SerializeField] private float partnerChest = 1.3f;
    [Tooltip("A shot this close is heard even from another room (m).")]
    [SerializeField] private float shotHearRange = 14f;
    [Tooltip("The hit lands if she is within this angle in front of it (deg, each side).")]
    [SerializeField] private float hitAngle = 70f;
    [Tooltip("The hit lands up to this far beyond the attack range (she stepped back a little).")]
    [SerializeField] private float hitSlack = 0.35f;
    [Tooltip("Path to her refreshed every this many seconds.")]
    [SerializeField] private float repathInterval = 0.2f;

    // Its agent and health.
    private NavMeshAgent agent;
    private Health health;
    // Current state, and where / how it started (it goes back there).
    private State state;
    private Vector3 home;
    private Quaternion homeRotation;
    private bool feeding;          // in the feeding pose right now
    private float timer;           // wander pause / lost-her time
    // Timers: repath, attack cooldown, stagger.
    private float repathTimer;
    private float cooldown;
    private float staggerTimer;
    // Current walk speed shown by the legs (ramps up, no sliding).
    private float stepSpeed;
    // The attack chosen, its damage, whether it already hit, and the fall direction when killed.
    private EnemyType.Attack attack;
    private int attackDamage;
    private bool attackHit;
    private bool deathBackward;
    // Scratch list of the attacks usable right now.
    private readonly EnemyType.Attack[] usable = new EnemyType.Attack[8];

    // Raised when it notices her (other systems can react).
    public event Action<EnemyBrain> Alerted;

    // State read by the combat HUD, the commands and the save system.
    public State Current => state;
    public bool IsDead => state == State.Dead;
    public string SaveId => saveId;
    public EnemyType Type => type;

    // Gets its components and remembers its start pose; the agent doesn't rotate it (done here).
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        home = transform.position;
        homeRotation = transform.rotation;
        if (type == null || enemyAnimator == null || bodyCollider == null || partner == null || partnerHealth == null || targets == null)
        {
            Debug.LogError($"{name}: EnemyBrain is missing references (run Last Terminal/7 then Last Terminal/4).", this);
            enabled = false;
            return;
        }
        agent.speed = type.WalkSpeed;
        agent.updateRotation = false;   // turned here (toward the path or toward her)
    }

    // Listens to its health and to her shots.
    private void OnEnable()
    {
        if (health == null) return;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        if (shooter != null) shooter.Fired += OnShot;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        if (shooter != null) shooter.Fired -= OnShot;
    }

    // Attack clip speed, then its start pose.
    private void Start()
    {
        enemyAnimator.SetAttackSpeed(type.AttackSpeed);
        EnterStart(true);
    }

    // ------------------------------------------------------------------ states
    private void EnterStart(bool snap)
    {
        state = State.Unaware;
        timer = type.WanderPause * UnityEngine.Random.Range(0.3f, 1f);
        Stop();
        feeding = startMode == EnemyStartMode.Feeding;
        if (feeding)
        {
            if (snap) enemyAnimator.SnapFeeding();
            else enemyAnimator.PlayFeed();
        }
        else if (snap) enemyAnimator.SnapLocomotion();
    }

    // It noticed her: get up (feeding), scream, or chase.
    private void Alert()
    {
        if (state != State.Unaware && state != State.Returning) return;
        Stop();
        Alerted?.Invoke(this);
        if (feeding)
        {
            feeding = false;
            state = State.Rising;
            enemyAnimator.PlayRise();
        }
        else if (type.ScreamsOnAlert && state == State.Unaware) StartScream();
        else StartChase();
    }

    // Plays the scream; the chase starts at its end.
    private void StartScream()
    {
        state = State.Screaming;
        enemyAnimator.PlayScream();
    }

    // Chases her (back to locomotion).
    private void StartChase()
    {
        state = State.Chasing;
        timer = 0f;
        repathTimer = 0f;
        if (enemyAnimator.IsPlayingState) enemyAnimator.ToLocomotion();
    }

    // Gives up: walks back to where it started.
    private void GiveUp()
    {
        state = State.Returning;
        enemyAnimator.ToLocomotion();
        if (agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(home); }
    }

    // The state machine, then the locomotion.
    private void Update()
    {
        if (state == State.Dead) return;
        float dt = Time.deltaTime;
        cooldown -= dt;
        staggerTimer -= dt;
        bool herAlive = !partnerHealth.IsDead;

        switch (state)
        {
            case State.Unaware:
                if (herAlive && Senses()) { Alert(); break; }
                if (startMode == EnemyStartMode.Wander) UpdateWander(dt);
                break;
            case State.Returning:
                if (herAlive && Senses()) { Alert(); break; }
                if (Arrived()) { transform.rotation = homeRotation; EnterStart(false); }
                break;
            case State.Rising:
                FaceHer(dt, 0.5f);
                if (enemyAnimator.PlayingTime >= 0.95f)
                {
                    if (type.ScreamsOnAlert) StartScream();
                    else StartChase();
                }
                break;
            case State.Screaming:
                FaceHer(dt, 1f);
                if (enemyAnimator.PlayingTime >= 0.9f) StartChase();
                break;
            case State.Chasing:
                UpdateChase(dt, herAlive);
                break;
            case State.Attacking:
                UpdateAttack(dt);
                break;
        }
        UpdateLocomotion(dt);
    }

    // Wander: walks to a random point of its room after each pause.
    private void UpdateWander(float dt)
    {
        if (agent.hasPath && !Arrived()) return;
        timer -= dt;
        if (timer > 0f) return;
        timer = type.WanderPause * UnityEngine.Random.Range(0.6f, 1.4f);
        if (TryWanderPoint(out Vector3 p) && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(p);
        }
    }

    // A random point of its room on the NavMesh (6 tries).
    private bool TryWanderPoint(out Vector3 point)
    {
        point = home;
        Room room = targets.RoomOf(home);
        for (int i = 0; i < 6; i++)
        {
            Vector3 p;
            if (room != null)
            {
                Rect r = room.FloorRect;
                p = new Vector3(UnityEngine.Random.Range(r.xMin + 1f, r.xMax - 1f), home.y, UnityEngine.Random.Range(r.yMin + 1f, r.yMax - 1f));
            }
            else p = home + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) { point = hit.position; return true; }
        }
        return false;
    }

    // Chase: gives up after she left its room long enough; attacks when one reaches (facing her), else follows her path.
    private void UpdateChase(float dt, bool herAlive)
    {
        if (!herAlive) { GiveUp(); return; }

        // she left its room: it keeps after her for a while, then gives up
        Room mine = targets.RoomOf(transform.position);
        Room hers = targets.RoomOf(partner.position);
        timer = mine != null && hers != mine ? timer + dt : 0f;
        if (timer > type.LoseTime) { GiveUp(); return; }

        if (staggerTimer > 0f) { Stop(); return; }

        float distance = FlatDistance(partner.position);
        if (cooldown <= 0f && ChooseAttack(distance))
        {
            if (Facing(partner.position, 35f)) { StartAttack(); return; }
            Stop();
            FaceHer(dt, 1.5f);
            return;
        }
        if (distance < MinReach() * 0.8f) { Stop(); FaceHer(dt, 1.5f); return; }   // close, waiting for the cooldown

        repathTimer -= dt;
        if (repathTimer <= 0f && agent.isOnNavMesh)
        {
            repathTimer = repathInterval;
            agent.isStopped = false;
            agent.SetDestination(partner.position);
        }
    }

    // ------------------------------------------------------------------ attacks
    private float Reach => weapon != null ? weapon.Reach : 0f;

    // Shortest reach of its usable attacks (it stops closer than that while waiting).
    private float MinReach()
    {
        float min = float.MaxValue;
        foreach (EnemyType.Attack a in type.Attacks)
            if (a != null && DamageOf(a) > 0) min = Mathf.Min(min, a.range + Reach);
        return min < float.MaxValue ? min : 1f;
    }

    // Damage of an attack with its weapon (0 = this weapon can't do it).
    private int DamageOf(EnemyType.Attack a)
    {
        if (weapon == null) return a.damage;
        int extra = weapon.DamageFor(a.slot);
        return extra > 0 ? a.damage + extra : 0;   // this weapon can't do it
    }

    // picks one of the attacks that reach her (weighted); false = none reaches
    private bool ChooseAttack(float distance)
    {
        int count = 0;
        float total = 0f;
        foreach (EnemyType.Attack a in type.Attacks)
        {
            if (a == null || a.weight <= 0f || DamageOf(a) <= 0 || distance > a.range + Reach || count >= usable.Length) continue;
            usable[count++] = a;
            total += a.weight;
        }
        if (count == 0) return false;
        float pick = UnityEngine.Random.value * total;
        attack = usable[count - 1];
        for (int i = 0; i < count; i++)
        {
            pick -= usable[i].weight;
            if (pick <= 0f) { attack = usable[i]; break; }
        }
        return true;
    }

    // Starts the chosen attack.
    private void StartAttack()
    {
        state = State.Attacking;
        attackDamage = DamageOf(attack);
        attackHit = false;
        Stop();
        enemyAnimator.PlayAttack(attack.slot);
    }

    // Attack: faces her during the wind-up, deals the hit at the hit time if she is still in the zone, then back to the chase.
    private void UpdateAttack(float dt)
    {
        float t = enemyAnimator.PlayingTime;
        if (t < 0f) { FaceHer(dt, 1f); return; }          // blending in
        if (t < attack.hitTime) FaceHer(dt, 0.6f);        // tracks her a little during the wind-up
        if (!attackHit && t >= attack.hitTime)
        {
            attackHit = true;
            if (InHitZone()) partnerHealth.TakeDamage(attackDamage);
        }
        if (t >= attack.exitTime) EndAttack();
    }

    // Is she in reach, in front of it and with nothing in between?
    private bool InHitZone()
    {
        if (partnerHealth.IsDead) return false;
        float distance = FlatDistance(partner.position);
        if (distance > attack.range + Reach + hitSlack || !Facing(partner.position, hitAngle)) return false;
        return CommandTargets.Clear(transform.position + Vector3.up * 1.2f, partner.position + Vector3.up * 1.2f);
    }

    // Attack over: cooldown, chase again.
    private void EndAttack()
    {
        cooldown = attack.cooldown;
        StartChase();
    }

    // ------------------------------------------------------------------ senses
    private bool Senses()
    {
        Vector3 to = partner.position - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (feeding) return distance <= type.FeedingHearRange;
        if (distance <= type.HearRange) return true;
        if (distance > type.SightRange) return false;
        if (distance > 0.01f && Vector3.Angle(transform.forward, to) > type.SightAngle * 0.5f) return false;
        return CommandTargets.Clear(transform.position + Vector3.up * eyeHeight, partner.position + Vector3.up * partnerChest);
    }

    // Her shot: heard in its room or close enough.
    private void OnShot(RangedWeapon w)
    {
        if (state != State.Unaware && state != State.Returning) return;
        Room mine = targets.RoomOf(transform.position);
        bool sameRoom = mine != null && mine == targets.RoomOf(partner.position);
        if (sameRoom || FlatDistance(partner.position) <= shotHearRange) Alert();
    }

    // ------------------------------------------------------------------ damage
    private void OnDamaged(int amount)
    {
        if (amount <= 0 || health.IsDead) return;
        bool heavy = amount >= type.StaggerDamage;
        enemyAnimator.Flinch(transform.position - partner.position, heavy);
        if (state == State.Unaware || state == State.Returning) { Alert(); return; }
        if (!heavy) return;
        staggerTimer = type.StaggerTime;
        if (state == State.Attacking && !attackHit)
        {
            cooldown = Mathf.Max(cooldown, type.StaggerTime);
            StartChase();
        }
    }

    // Killed: falls backward if hit from the front.
    private void OnDied()
    {
        // hit from the front (she is in front of it): it falls backward
        Vector3 toHer = partner.position - transform.position;
        toHer.y = 0f;
        deathBackward = Vector3.Dot(transform.forward, toHer) >= 0f;
        EnterDead(false);
    }

    // Dead state: agent and collider off, weapon dropped, death pose (snapped on load).
    private void EnterDead(bool snap)
    {
        state = State.Dead;
        feeding = false;
        Stop();
        agent.enabled = false;
        bodyCollider.enabled = false;
        if (weapon != null) weapon.Drop();
        if (snap) enemyAnimator.SnapDead(deathBackward);
        else enemyAnimator.PlayDeath(deathBackward);
    }

    // ------------------------------------------------------------------ save
    public SavedEnemy Capture() => new SavedEnemy
    {
        id = saveId,
        dead = state == State.Dead,
        health = health.Current,
        position = transform.position,
        yaw = transform.eulerAngles.y,
        fellBackward = deathBackward,
    };

    // a save was loaded: back as saved (null = not in that save: as at the start of the scene)
    public void Restore(SavedEnemy saved)
    {
        if (saved != null && saved.dead)
        {
            deathBackward = saved.fellBackward;
            transform.SetPositionAndRotation(saved.position, Quaternion.Euler(0f, saved.yaw, 0f));
            if (!health.IsDead) health.SetDefeated();
            EnterDead(true);
            return;
        }
        if (health.IsDead) health.Restore();
        if (saved != null) health.SetCurrent(saved.health);
        else health.Restore();
        bodyCollider.enabled = true;
        agent.enabled = true;
        if (weapon != null) weapon.Reattach();
        Vector3 at = saved != null ? saved.position : home;
        Quaternion rot = saved != null ? Quaternion.Euler(0f, saved.yaw, 0f) : homeRotation;
        if (agent.isOnNavMesh) agent.Warp(at);
        else transform.position = at;
        transform.rotation = rot;
        cooldown = 0f;
        staggerTimer = 0f;
        stepSpeed = 0f;
        EnterStart(true);
    }

    // ------------------------------------------------------------------ movement
    // The legs lead (as hers, PartnerAnimator): the walk ramps up over stepInTime and the agent is held to the
    // speed being shown, so it steps off instead of sliding. Walk or run picked by state.
    private void UpdateLocomotion(float dt)
    {
        if (state == State.Dead || !agent.enabled) return;
        bool run = state == State.Chasing && type.Runs;
        float top = run ? type.RunSpeed : type.WalkSpeed;
        agent.speed = top;

        float actual = agent.velocity.magnitude;
        bool moving = agent.isOnNavMesh && !agent.isStopped && !agent.pathPending && agent.hasPath;
        float wanted = moving ? agent.desiredVelocity.magnitude : 0f;
        float target = Mathf.Max(wanted, actual);
        if (target > stepSpeed) stepSpeed = Mathf.MoveTowards(stepSpeed, target, top / Mathf.Max(type.StepInTime, 0.01f) * dt);
        else stepSpeed = target;
        if (actual > stepSpeed + 0.02f) agent.velocity *= stepSpeed / actual;

        // gait 0 idle .. 1 walk .. 2 run; the clips are played faster / slower so the feet match the speed
        float gait, rate;
        if (stepSpeed <= type.WalkSpeed || !type.Runs)
        {
            gait = Mathf.Clamp01(stepSpeed / Mathf.Max(type.WalkSpeed, 0.01f));
            rate = Mathf.Lerp(1f, type.WalkSpeed / Mathf.Max(type.WalkClipSpeed, 0.01f), gait);
        }
        else
        {
            float k = Mathf.Clamp01((stepSpeed - type.WalkSpeed) / Mathf.Max(type.RunSpeed - type.WalkSpeed, 0.01f));
            gait = 1f + k;
            rate = Mathf.Lerp(type.WalkSpeed / Mathf.Max(type.WalkClipSpeed, 0.01f), type.RunSpeed / Mathf.Max(type.RunClipSpeed, 0.01f), k);
        }
        enemyAnimator.SetGait(gait, rate);

        // facing the way it walks (attacks and screams turn it toward her themselves)
        if (stepSpeed > 0.05f && state != State.Attacking)
        {
            Vector3 dir = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
            Turn(dir, type.TurnSpeed * 1.5f, dt);
        }
    }

    // Stops on the spot.
    private void Stop()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    // Reached the agent's destination.
    private bool Arrived() =>
        agent.enabled && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

    // Turns toward her.
    private void FaceHer(float dt, float speedScale) => Turn(partner.position - transform.position, type.TurnSpeed * speedScale, dt);

    // Turns toward a direction at a max speed.
    private void Turn(Vector3 direction, float degreesPerSecond, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * dt);
    }

    // Is the point within an angle of its forward?
    private bool Facing(Vector3 point, float maxAngle)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        return to.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, to) <= maxAngle;
    }

    // Floor distance to a point.
    private float FlatDistance(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.magnitude;
    }
}
