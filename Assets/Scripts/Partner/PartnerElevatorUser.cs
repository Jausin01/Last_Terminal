using System;
using UnityEngine;
using UnityEngine.AI;

// Her side of an elevator: walks up to it, the doors open, she steps in, turns around, the doors close and the ride
// starts (ElevatorTravel loads the destination scene). On arrival (another scene) she is in the car of the elevator
// of that stop: the doors open and she steps out. She refuses a ride without the key its destination needs.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerElevatorUser : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CombatField = nameof(combat);
    public const string TravelField = nameof(travel);

    public enum Result { Ok, NoKey, OutOfService, Busy }

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private ElevatorTravel travel;
    [SerializeField] private ScriptedWalker walker = new ScriptedWalker();
    [SerializeField] private float handoffDistance = 0.6f;
    [Tooltip("Give up waiting for doors after this long (s).")]
    [SerializeField] private float doorTimeout = 4f;

    private enum Step { None, Approach, WaitOpen, WalkIn, TurnOut, WaitClosed, Riding, ArriveWait, WalkOut }

    private NavMeshAgent agent;
    private Step step;
    private Elevator elevator;
    private float timer;

    public event Action<Elevator> Departing;   // doors shut with her inside
    public event Action<Elevator> Arrived;     // she stepped out at the destination

    public bool IsBusy => step != Step.None;
    public Elevator Current => elevator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || interactor == null || combat == null || travel == null)
        {
            Debug.LogError($"{name}: PartnerElevatorUser is missing references (run the scene menu).", this);
            enabled = false;
            return;
        }
        walker.Bind(transform, partnerAnimator);
    }

    public Result Ride(Elevator target)
    {
        if (!enabled || target == null) return Result.Busy;
        if (step != Step.None || partnerAnimator.IsScripted || partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp) return Result.Busy;
        ElevatorKey key = target.RequiredKey;
        if (key != null && !interactor.Inventory.Has(key)) return Result.NoKey;
        if (!travel.CanGo(target.Destination)) return Result.OutOfService;
        if (!NavMesh.SamplePosition(target.Outside.position, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) return Result.Busy;
        combat.Holster();
        if (!interactor.MoveTo(hit.position)) return Result.Busy;
        elevator = target;
        step = Step.Approach;
        return Result.Ok;
    }

    // a new scene starts with her in this elevator's car: out she comes
    public void Arrive(Elevator at)
    {
        elevator = at;
        at.SnapClosed();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.isStopped = true;
        transform.SetPositionAndRotation(at.Inside.position, Quaternion.LookRotation(at.transform.forward));
        partnerAnimator.BeginScripted();
        walker.Begin(0f);
        at.Open();
        timer = 0f;
        step = Step.ArriveWait;
    }

    private void Update()
    {
        if (step == Step.None) return;
        timer += Time.deltaTime;
        switch (step)
        {
            case Step.Approach: UpdateApproach(); break;
            case Step.WaitOpen:
                if (elevator.IsOpen) { step = Step.WalkIn; walker.ResetTurn(); }
                else if (timer > doorTimeout) Cancel();
                break;
            case Step.WalkIn:
                if (walker.WalkTo(elevator.Inside.position)) { step = Step.TurnOut; walker.ResetTurn(); }
                break;
            case Step.TurnOut:
                if (walker.TurnTo(elevator.transform.forward)) { elevator.Close(); step = Step.WaitClosed; timer = 0f; }
                break;
            case Step.WaitClosed:
                if (!elevator.IsClosed) break;
                step = Step.Riding;
                Departing?.Invoke(elevator);
                travel.Go(elevator);
                break;
            case Step.ArriveWait:
                if (elevator.IsOpen || timer > doorTimeout) step = Step.WalkOut;
                break;
            case Step.WalkOut:
                if (!walker.WalkTo(elevator.Outside.position)) break;
                EndScriptedHere();
                elevator.Close();
                Elevator done = elevator;
                Done();
                Arrived?.Invoke(done);
                break;
        }
    }

    private void UpdateApproach()
    {
        if ((Flat(agent.destination) - Flat(elevator.Outside.position)).sqrMagnitude > 0.36f) { Done(); return; }   // sent elsewhere
        if (agent.pathPending || partnerAnimator.IsTurning) return;
        float dist = (Flat(transform.position) - Flat(elevator.Outside.position)).magnitude;
        bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f;
        if (dist > handoffDistance && !arrived) return;
        float speed = Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        partnerAnimator.BeginScripted();
        walker.Begin(speed);
        elevator.Open();
        timer = 0f;
        step = Step.WaitOpen;
    }

    // the ride didn't happen (doors stuck, the scene can't load): she steps back out
    public void Cancel()
    {
        if (step == Step.None) return;
        if (elevator != null) elevator.Open();
        if (partnerAnimator.IsScripted)
        {
            step = Step.WalkOut;   // out of the car, then free
            timer = 0f;
            return;
        }
        Done();
    }

    private void EndScriptedHere()
    {
        partnerAnimator.EndScripted();
        Vector3 at = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.8f, NavMesh.AllAreas) ? hit.position : transform.position;
        agent.Warp(at);
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    private void Done()
    {
        step = Step.None;
        elevator = null;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
