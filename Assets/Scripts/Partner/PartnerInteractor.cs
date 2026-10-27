using System;
using UnityEngine;
using UnityEngine.AI;

// Outcome of a pick up attempt.
public enum PickupResult { Collected, Partial, InventoryFull, OutOfReach }

// What the partner does with the world: walk somewhere, walk to a pickup and take it.
// Pick up = approach to arm's reach -> face the item -> low/medium animation -> item goes to her hand -> inventory.
// Commands (voice / text / click) call MoveTo and PickUp; feedback listens to PickupAttempted.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Inventory))]
public class PartnerInteractor : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);

    // Her animator (pickup clips, right hand / palm).
    [SerializeField] private PartnerAnimator partnerAnimator;

    [Header("Pick up")]
    [Tooltip("Items at or above this height (from her feet) use the medium animation.")]
    [SerializeField] private float mediumHeight = 0.5f;
    [Tooltip("Palm position at the grab frame, relative to her feet: x = forward, y = right (m).")]
    [SerializeField] private Vector2 lowReach = new Vector2(0.255f, 0.053f);
    // Palm position for the medium animation, arrival tolerance, turn speed, and whether the item shows in her hand.
    [SerializeField] private Vector2 mediumReach = new Vector2(0.646f, 0.077f);
    [SerializeField] private float arriveTolerance = 0.05f;
    [Tooltip("If she can't get closer, still try when the item is at most this much beyond her reach (IK covers it).")]
    [SerializeField] private float extraReach = 0.3f;
    [SerializeField] private float faceSpeed = 360f;
    [SerializeField] private bool showItemInHand = true;
    [Tooltip("Gap between the palm and the item surface while she holds it (m).")]
    [SerializeField] private float palmGap = 0.01f;

    // Pick up steps.
    private enum Step { None, Approaching, Facing, Picking }

    // Her agent and inventory, the step, the item, its height class and the reach used.
    private NavMeshAgent agent;
    private Inventory inventory;
    private Step step;
    private Pickup target;
    private PickupHeight height;
    private Vector2 reach;

    // model borrowed from the pickup while it is in her hand
    private Transform heldVisual;
    private Transform heldParent;
    private Vector3 heldLocalPos;
    private Quaternion heldLocalRot;
    private Vector3 heldLocalScale;

    public event Action<ItemDefinition, int, PickupResult> PickupAttempted;   // item, units taken, result
    public event Action PickupRequested;   // a pick up was accepted (she starts walking to the item)

    // Read-only state for commands and combat.
    public Inventory Inventory => inventory;
    public bool IsBusy => step == Step.Facing || step == Step.Picking;
    public bool HasPickupTarget => target != null;

    // Gets the agent and inventory; checks the animator.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        inventory = GetComponent<Inventory>();
        if (partnerAnimator == null)
        {
            Debug.LogError($"{name}: PartnerInteractor has no PartnerAnimator assigned.", this);
            enabled = false;
        }
    }

    // Listens to the pickup animation (grab frame / end).
    private void OnEnable()
    {
        if (partnerAnimator == null) return;
        partnerAnimator.PickupGrabbed += OnGrabbed;
        partnerAnimator.PickupFinished += OnFinished;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (partnerAnimator == null) return;
        partnerAnimator.PickupGrabbed -= OnGrabbed;
        partnerAnimator.PickupFinished -= OnFinished;
    }

    // Returns false while she is busy picking something up, swinging a weapon, under scripted control (save terminal) or dead.
    public bool MoveTo(Vector3 position)
    {
        if (IsBusy || partnerAnimator.IsAttacking || partnerAnimator.IsScripted || partnerAnimator.IsDead) return false;
        target = null;
        step = Step.None;
        agent.SetDestination(position);
        return true;
    }

    // Walks to an item and takes it (false while busy / dead / scripted).
    public bool PickUp(Pickup pickup)
    {
        if (IsBusy || partnerAnimator.IsAttacking || partnerAnimator.IsScripted || partnerAnimator.IsDead || pickup == null || !pickup.gameObject.activeInHierarchy) return false;

        target = pickup;
        float itemHeight = pickup.GrabPosition.y - transform.position.y;
        height = itemHeight >= mediumHeight ? PickupHeight.Medium : PickupHeight.Low;
        reach = height == PickupHeight.Medium ? mediumReach : lowReach;

        // the item may be on a table: walk to a floor point she can reach (FindStand)
        if (!FindStand(pickup, out Vector3 goal)) goal = pickup.InteractionPosition;
        agent.SetDestination(goal);
        step = Step.Approaching;
        PickupRequested?.Invoke();
        return true;
    }

    // Where she stands to take it: the closest floor point when she can walk there; otherwise (furniture around it
    // leaves the closest point cut off: behind a gurney, between a cart and a wall) a point around the item, within
    // arm's reach, that she can walk to. False = none.
    public bool FindStand(Pickup pickup, out Vector3 stand)
    {
        stand = Vector3.zero;
        if (pickup == null || !agent.isOnNavMesh) return false;
        Vector3 item = pickup.InteractionPosition;
        float grab = FlatDistanceLimit(pickup);
        if (NavMesh.SamplePosition(item, out NavMeshHit hit, 2f, NavMesh.AllAreas) && Reachable(hit.position))
        {
            stand = hit.position;
            return true;
        }
        float best = float.MaxValue;
        bool found = false;
        foreach (float r in StandRings)
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                Vector3 p = new Vector3(item.x + Mathf.Cos(a) * r, transform.position.y, item.z + Mathf.Sin(a) * r);
                if (!NavMesh.SamplePosition(p, out NavMeshHit h, 0.35f, NavMesh.AllAreas)) continue;
                float d = FlatDistance(h.position, item);
                if (d > grab || d >= best || !Reachable(h.position)) continue;
                best = d;
                stand = h.position;
                found = true;
            }
        return found;
    }

    // Distances around the item tried as standing spots (m).
    private static readonly float[] StandRings = { 0.45f, 0.65f, 0.85f };

    // how far from the item she can stand and still take it (her reach + what the IK covers)
    private float FlatDistanceLimit(Pickup pickup)
    {
        float itemHeight = pickup.GrabPosition.y - transform.position.y;
        Vector2 r = itemHeight >= mediumHeight ? mediumReach : lowReach;
        return r.magnitude + extraReach;
    }

    // Reused path for the reachability test.
    private NavMeshPath pathBuffer;

    // Can she walk all the way to this point?
    private bool Reachable(Vector3 point)
    {
        if (pathBuffer == null) pathBuffer = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, point, NavMesh.AllAreas, pathBuffer) && pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    // Runs the current step.
    private void Update()
    {
        switch (step)
        {
            case Step.Approaching: UpdateApproach(); break;
            case Step.Facing: UpdateFacing(); break;
        }
    }

    // Walking to the item: in reach -> face it; arrived but too far -> out of reach.
    private void UpdateApproach()
    {
        if (target == null || !target.gameObject.activeInHierarchy) { Cancel(); return; }
        if (agent.pathPending || partnerAnimator.IsTurning) return;

        float dist = FlatDistance(transform.position, target.GrabPosition);
        float reachDist = reach.magnitude;
        if (dist <= reachDist + arriveTolerance) { StartFacing(); return; }

        bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.05f && agent.velocity.sqrMagnitude < 0.01f;
        if (!arrived) return;

        if (dist <= reachDist + extraReach) StartFacing();
        else Fail(PickupResult.OutOfReach);
    }

    // Stops and starts turning to the item.
    private void StartFacing()
    {
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
        step = Step.Facing;
    }

    // Turns until the item sits where her right palm goes, then plays the pickup.
    private void UpdateFacing()
    {
        if (target == null || !target.gameObject.activeInHierarchy) { Cancel(); return; }

        Vector3 toItem = target.GrabPosition - transform.position;
        toItem.y = 0f;
        if (toItem.sqrMagnitude > 0.0001f)
        {
            // the item must end up where her right palm goes: a bit to the right of straight ahead
            float sideAngle = Mathf.Atan2(reach.y, reach.x) * Mathf.Rad2Deg;
            Quaternion want = Quaternion.LookRotation(toItem) * Quaternion.Euler(0f, -sideAngle, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, faceSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, want) > 2f) return;
        }

        step = Step.Picking;
        partnerAnimator.PlayPickup(height, target.GrabPosition);
    }

    // Grab frame: item model to her hand, units to the inventory, result raised.
    private void OnGrabbed()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        ItemDefinition item = target.Item;
        int offered = target.Amount;
        if (showItemInHand) BorrowVisual(target.Visual);

        int taken = target.Collect(inventory);
        if (target.gameObject.activeInHierarchy) ReturnVisual();   // not all of it fit: it stays on the floor

        PickupResult result = taken == 0 ? PickupResult.InventoryFull
                            : taken < offered ? PickupResult.Partial
                            : PickupResult.Collected;
        PickupAttempted?.Invoke(item, taken, result);
    }

    // Animation over: model back, ready again.
    private void OnFinished()
    {
        ReturnVisual();
        target = null;
        step = Step.None;
        agent.updateRotation = true;
    }

    // Gives up and reports why.
    private void Fail(PickupResult result)
    {
        ItemDefinition item = target != null ? target.Item : null;
        Cancel();
        if (item != null) PickupAttempted?.Invoke(item, 0, result);
    }

    // Clears the pickup and its path.
    private void Cancel()
    {
        agent.ResetPath();
        agent.updateRotation = true;
        target = null;
        step = Step.None;
    }

    // Moves the item model to her hand, resting against the palm.
    private void BorrowVisual(Transform visual)
    {
        Transform hand = partnerAnimator.RightHand;
        if (visual == null || hand == null) return;

        heldVisual = visual;
        heldParent = visual.parent;
        heldLocalPos = visual.localPosition;
        heldLocalRot = visual.localRotation;
        heldLocalScale = visual.localScale;

        // rest the item against the palm: its bounds touch the palm surface instead of crossing the hand
        if (TryGetVisualBounds(visual, out Bounds bounds) && partnerAnimator.TryGetRightPalm(out Vector3 palm, out Vector3 normal))
        {
            Vector3 e = bounds.extents;
            float halfAlongNormal = Mathf.Abs(normal.x) * e.x + Mathf.Abs(normal.y) * e.y + Mathf.Abs(normal.z) * e.z;
            Vector3 wantedCenter = palm + normal * (halfAlongNormal + palmGap);
            visual.position += wantedCenter - bounds.center;
        }
        visual.SetParent(hand, true);
    }

    // Puts the model back under its pickup (inactive once collected), so nothing is destroyed.
    private void ReturnVisual()
    {
        if (heldVisual == null) return;
        heldVisual.SetParent(heldParent, false);
        heldVisual.localPosition = heldLocalPos;
        heldVisual.localRotation = heldLocalRot;
        heldVisual.localScale = heldLocalScale;
        heldVisual = null;
    }

    // World bounds of every renderer of the model.
    private static bool TryGetVisualBounds(Transform visual, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    // Distance on the floor plane.
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
