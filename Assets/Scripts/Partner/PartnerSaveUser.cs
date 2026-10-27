using System;
using UnityEngine;
using UnityEngine.AI;

// Using a save station:
//   walk there (NavMesh) -> keeps walking into the gap between chair and desk on a curve (no stop) ->
//   stops in front of the chair -> turns to the desk with her turn clip -> sits (root motion, hips guided onto the chair)
//   -> chair pushed in -> types -> save screen -> chair pulled out -> stands up -> turns with the clip -> walks out.
// The scripted walk accelerates / brakes like the agent and feeds the real speed to the walk blend, so feet don't slide.
// The same seat routine serves other seated activities (the blackjack table): UseSeat(seat, activity) replaces
// typing + save screen with the activity, which calls LeaveSeat() when it is done.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerSaveUser : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CombatField = nameof(combat);
    public const string SaveScreenField = nameof(saveScreen);
    public const string SaveSystemField = nameof(saveSystem);

    // Her animator (seat / turn clips), interactor and combat (stopped first), and the save screen / save system the station opens.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private SaveScreen saveScreen;
    [SerializeField] private SaveSystem saveSystem;

    [Header("Walking in / out")]
    [Tooltip("Max speed in the gap (her walk clip is 1.8 m/s).")]
    [SerializeField] private float walkSpeed = 1.3f;
    // Scripted walk acceleration / braking (m/s²).
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float deceleration = 3.5f;
    [Tooltip("How fast she steers while walking (deg/s).")]
    [SerializeField] private float steerSpeed = 240f;
    [Tooltip("The NavMesh walk hands over to the scripted walk this close to the walk-in point (m).")]
    [SerializeField] private float handoffDistance = 0.6f;
    [Tooltip("Above this angle she turns in place with the turn clip instead of steering (deg).")]
    [SerializeField] private float clipTurnAngle = 50f;
    // How close counts as arrived on the scripted walk (m).
    [SerializeField] private float arriveTolerance = 0.02f;
    [Tooltip("She heads for the walk-in point until this close, then curves into the gap (m).")]
    [SerializeField] private float entryRadius = 0.35f;

    [Header("Seat (normalized time of the clips, measured from the Mixamo files)")]
    [Tooltip("Hips guided onto the chair while she sits down (her hips reach the seat around 0.8).")]
    [SerializeField] private Vector2 sitGuide = new Vector2(0.3f, 0.8f);
    [Tooltip("Hips released from the chair while she stands up (she leaves the seat around 0.35).")]
    [SerializeField] private Vector2 standRelease = new Vector2(0.25f, 0.45f);
    // Time to push the chair in / pull it out (s).
    [SerializeField] private float chairMoveTime = 0.6f;
    [Tooltip("Walking out, the chair goes back in once she is this far from where she stood up (m).")]
    [SerializeField] private float chairClearDistance = 0.45f;
    [Tooltip("Typing before the save screen appears (s).")]
    [SerializeField] private float typingDelay = 1.2f;

    // Steps of a seat use.
    private enum Step { None, Approach, ToEntry, WalkIn, TurnToDesk, SitDown, ScootIn, Typing, ScreenOpen, Activity, ScootOut, StandUp, TurnOut, WalkOut }

    // Her agent, her hips bone (guided onto the chair), the station, the walk-in point, the step and a timer.
    private NavMeshAgent agent;
    private Transform hips;
    private SaveStation station;
    private Vector3 walkIn;
    private Step step;
    private float timer;
    private float speed;        // scripted walk speed
    private float chairPull;    // 0 = at the desk, 1 = pulled out
    // She already turned with the clip; the seated activity in progress (null = saving).
    private bool clipTurned;
    private ISeatActivity activity;

    // Raised when she starts / ends using a station.
    public event Action<SaveStation> Started;
    public event Action<SaveStation> Finished;

    // She is using a station.
    public bool IsBusy => step != Step.None;

    // Gets the agent and hips; checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || interactor == null || combat == null || saveScreen == null || saveSystem == null)
        {
            Debug.LogError($"{name}: PartnerSaveUser needs PartnerAnimator, PartnerInteractor, PartnerCombat, SaveScreen and SaveSystem.", this);
            enabled = false;
        }
    }

    // Listens to the save screen closing.
    private void Start()
    {
        hips = partnerAnimator.GetBone(HumanBodyBones.Hips);
    }

    // "Save the game" / clicking the computer.
    public bool Use(SaveStation target) => UseSeat(target, null);

    // Sit at a seat for an activity (null = type and open the save screen).
    public bool UseSeat(SaveStation target, ISeatActivity seatActivity)
    {
        if (!enabled || IsBusy || target == null || !target.enabled) return false;
        if (partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp || partnerAnimator.IsScripted) return false;
        Vector3 entry = target.GetWalkInPosition(transform.position);
        if (!NavMesh.SamplePosition(entry, out _, 0.3f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"{target.name}: no walk-in side of this save point is on the NavMesh (see its gizmos).", target);
            return false;
        }
        combat.Holster();
        if (!interactor.MoveTo(entry)) return false;   // also drops any pickup she was walking to
        station = target;
        walkIn = entry;
        activity = seatActivity;
        step = Step.Approach;
        Started?.Invoke(station);
        return true;
    }

    // Runs the current step.
    private void Update()
    {
        switch (step)
        {
            case Step.Approach:
                UpdateApproach();
                break;
            case Step.ToEntry:
                // pass by the walk-in point without stopping, so she enters the gap along it
                MoveChair(1f);
                if (WalkTo(walkIn, entryRadius)) step = Step.WalkIn;
                break;
            case Step.WalkIn:
                MoveChair(1f);
                if (WalkTo(station.StandPosition)) { step = Step.TurnToDesk; clipTurned = false; }
                break;
            case Step.TurnToDesk:
                MoveChair(1f);
                if (TurnTo(station.Facing) && chairPull >= 1f) StartSitting();
                break;
            case Step.SitDown:
                if (partnerAnimator.GetSeatPhase(out _) == PartnerAnimator.SeatPhase.Seated) step = Step.ScootIn;
                break;
            case Step.ScootIn:
                if (MoveChair(0f))
                {
                    if (activity != null) { step = Step.Activity; activity.OnSeated(); }
                    else { step = Step.Typing; timer = typingDelay; }
                }
                break;
            case Step.Typing:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    step = Step.ScreenOpen;
                    saveScreen.Open(station.LocationName, walkIn, station.ResumeYaw(walkIn), OnScreenClosed);
                }
                break;
            case Step.ScootOut:
                if (MoveChair(1f))
                {
                    partnerAnimator.SetSeated(false);
                    step = Step.StandUp;
                }
                break;
            case Step.StandUp:
                if (partnerAnimator.GetSeatPhase(out _) == PartnerAnimator.SeatPhase.None)
                {
                    partnerAnimator.SetScriptedRootMotion(false);
                    step = Step.TurnOut;
                    clipTurned = false;
                }
                break;
            case Step.TurnOut:
                if (TurnTo(Flat(walkIn) - Flat(transform.position))) { step = Step.WalkOut; speed = 0f; }
                break;
            case Step.WalkOut:
                // push the chair back in once she is out of its way
                if ((Flat(transform.position) - Flat(station.StandPosition)).sqrMagnitude > chairClearDistance * chairClearDistance) MoveChair(0f);
                if (WalkTo(walkIn) && chairPull <= 0f) Finish();
                break;
        }
    }

    // NavMesh walk to the station, then the scripted walk-in.
    private void UpdateApproach()
    {
        // another command sent her somewhere else: give up
        if ((Flat(agent.destination) - Flat(walkIn)).sqrMagnitude > 0.25f) { step = Step.None; EndActivity(); Finished?.Invoke(station); return; }
        if (agent.pathPending || partnerAnimator.IsTurning) return;

        float dist = (Flat(transform.position) - Flat(walkIn)).magnitude;
        bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f;
        if (dist > handoffDistance && !arrived) return;

        // hand over without stopping: the scripted walk continues at her current speed
        speed = Mathf.Min(Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude, walkSpeed);
        combat.Holster();   // in case she was told to draw on the way
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        partnerAnimator.BeginScripted();
        partnerAnimator.SetScriptedSpeed(speed);
        clipTurned = false;
        step = Step.ToEntry;
    }

    // Stops in front of the chair and sits down.
    private void StartSitting()
    {
        partnerAnimator.SetScriptedSpeed(0f);
        partnerAnimator.SetScriptedRootMotion(true);
        partnerAnimator.SetSeatAnimation(activity != null ? activity.SeatAnimation : PartnerAnimator.SeatAnimation.Typing);
        partnerAnimator.SetSeated(true);
        step = Step.SitDown;
    }

    // Save screen closed: saves (if asked) and stands up.
    private void OnScreenClosed(SaveScreen.Result result, int slot)
    {
        if (step != Step.ScreenOpen) return;
        if (result == SaveScreen.Result.Loaded)
        {
            // the screen is black now: drop the seat sequence and put her where the save says
            Abort();
            saveSystem.Load(slot);
            return;
        }
        step = Step.ScootOut;
    }

    // The activity is over: chair out, stand up, walk out (as after saving).
    public void LeaveSeat()
    {
        if (step == Step.Activity) step = Step.ScootOut;
    }

    // Tells the seated activity she left the seat.
    private void EndActivity()
    {
        ISeatActivity a = activity;
        activity = null;
        a?.OnSeatLeft();
    }

    // Ends the sequence at once, standing, chair pushed in (loading a save).
    public void Abort()
    {
        if (!IsBusy) return;
        bool scripted = partnerAnimator.IsScripted;
        if (scripted) partnerAnimator.SnapToLocomotion();
        chairPull = 0f;
        station.SetChairPull(0f);
        if (scripted) RestoreAgent(walkIn);
        else agent.ResetPath();
        step = Step.None;
        EndActivity();
        Finished?.Invoke(station);
    }

    // Back to the agent; the sequence is over.
    private void Finish()
    {
        RestoreAgent(transform.position);
        step = Step.None;
        EndActivity();
        Finished?.Invoke(station);
    }

    // Ends the scripted control and gives the body back to the agent at a point.
    private void RestoreAgent(Vector3 at)
    {
        partnerAnimator.EndScripted();
        agent.Warp(at);
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    // after the animation: keep her facing the desk and her hips on the chair while she uses the seat
    private void LateUpdate()
    {
        float weight = SeatWeight();
        if (weight <= 0f || hips == null) return;

        transform.rotation = Quaternion.LookRotation(station.Facing);
        Vector3 error = station.SeatPosition - hips.position;
        error.y = 0f;
        transform.position += error * weight;
    }

    // How strongly her hips are held on the seat in the current step (0..1).
    private float SeatWeight()
    {
        switch (step)
        {
            case Step.SitDown:
            {
                PartnerAnimator.SeatPhase phase = partnerAnimator.GetSeatPhase(out float t);
                if (phase == PartnerAnimator.SeatPhase.Seated) return 1f;
                return phase == PartnerAnimator.SeatPhase.SittingDown ? Mathf.InverseLerp(sitGuide.x, sitGuide.y, t) : 0f;
            }
            case Step.ScootIn:
            case Step.Typing:
            case Step.ScreenOpen:
            case Step.Activity:
            case Step.ScootOut:
                return 1f;
            case Step.StandUp:
            {
                PartnerAnimator.SeatPhase phase = partnerAnimator.GetSeatPhase(out float t);
                if (phase == PartnerAnimator.SeatPhase.Seated) return 1f;
                return phase == PartnerAnimator.SeatPhase.StandingUp ? 1f - Mathf.InverseLerp(standRelease.x, standRelease.y, t) : 0f;
            }
            default:
                return 0f;
        }
    }

    // Scripted walk: steers toward the target while moving, accelerates and brakes to stop on it.
    // Standing still and facing well away, she turns first with the turn clip. True once there (stopped).
    // passRadius > 0: no braking, true as soon as she is that close (she keeps walking into the next leg).
    private bool WalkTo(Vector3 target, float passRadius = 0f)
    {
        if (partnerAnimator.IsTurning) return false;

        Vector3 to = Flat(target) - Flat(transform.position);
        float dist = to.magnitude;
        if (passRadius > 0f && dist <= passRadius) return true;
        if (dist <= arriveTolerance)
        {
            transform.position = new Vector3(target.x, transform.position.y, target.z);
            speed = 0f;
            partnerAnimator.SetScriptedSpeed(0f);
            return true;
        }

        float angle = Vector3.SignedAngle(Flat(transform.forward), to, Vector3.up);
        if (speed < 0.1f && Mathf.Abs(angle) > clipTurnAngle && partnerAnimator.PlayScriptedTurn(angle)) return false;

        // (not in the last few cm: the direction to the target gets noisy there)
        if (dist > 0.1f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), steerSpeed * Time.deltaTime);

        // brake to stop exactly on the target; slow down while the turn is still large
        float stopDistance = speed * speed / (2f * deceleration);
        bool braking = passRadius <= 0f && dist <= stopDistance + arriveTolerance;
        float wanted = braking ? 0f : walkSpeed * (Mathf.Abs(angle) > 45f ? 0.4f : 1f);
        speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? acceleration : deceleration) * Time.deltaTime);
        if (speed < 0.05f && wanted <= 0f) speed = Mathf.Min(0.15f, dist / Mathf.Max(Time.deltaTime, 0.0001f));   // creep the last cm

        float move = Mathf.Min(speed * Time.deltaTime, dist);
        transform.position += to / dist * move;
        partnerAnimator.SetScriptedSpeed(speed);
        return false;
    }

    // Faces a direction: big angles with the turn clip (once), the rest smoothly. True when aligned.
    private bool TurnTo(Vector3 direction)
    {
        partnerAnimator.SetScriptedSpeed(0f);
        if (partnerAnimator.IsTurning) return false;
        if (direction.sqrMagnitude < 0.0001f) return true;

        float angle = Vector3.SignedAngle(Flat(transform.forward), direction, Vector3.up);
        if (!clipTurned && Mathf.Abs(angle) > 30f)
        {
            clipTurned = partnerAnimator.PlayScriptedTurn(angle);
            if (clipTurned) return false;
        }
        Quaternion want = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 180f * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, want) < 1f;
    }

    // Moves the chair toward its target pull; true once there.
    private bool MoveChair(float target)
    {
        float rate = chairMoveTime > 0f ? Time.deltaTime / chairMoveTime : 1f;
        chairPull = Mathf.MoveTowards(chairPull, target, rate);
        station.SetChairPull(Mathf.SmoothStep(0f, 1f, chairPull));
        return Mathf.Approximately(chairPull, target);
    }

    // A vector flattened onto the floor.
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}

// Something she does seated at a SaveStation-style seat (instead of saving): see PartnerSaveUser.UseSeat.
public interface ISeatActivity
{
    // seated clips to use (Animator SeatActivity)
    PartnerAnimator.SeatAnimation SeatAnimation { get; }
    // seated, chair pushed in: the activity runs until it calls PartnerSaveUser.LeaveSeat()
    void OnSeated();
    // she left the seat (walked out, or the routine was cancelled / aborted before or while seated)
    void OnSeatLeft();
}
