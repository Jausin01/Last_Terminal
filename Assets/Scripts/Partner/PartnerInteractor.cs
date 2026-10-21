using System;
using UnityEngine;
using UnityEngine.AI;

public enum PickupResult { Collected, Partial, InventoryFull, OutOfReach }

// What the partner does with the world: walk somewhere, walk to a pickup and take it.
// Pick up = approach to arm's reach -> face the item -> low/medium animation -> item goes to her hand -> inventory.
// Commands (voice / text / click) call MoveTo and PickUp; feedback listens to PickupAttempted.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Inventory))]
public class PartnerInteractor : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);

    [SerializeField] private PartnerAnimator partnerAnimator;

    [Header("Pick up")]
    [Tooltip("Items at or above this height (from her feet) use the medium animation.")]
    [SerializeField] private float mediumHeight = 0.5f;
    [Tooltip("Palm position at the grab frame, relative to her feet: x = forward, y = right (m).")]
    [SerializeField] private Vector2 lowReach = new Vector2(0.255f, 0.053f);
    [SerializeField] private Vector2 mediumReach = new Vector2(0.646f, 0.077f);
    [SerializeField] private float arriveTolerance = 0.05f;
    [Tooltip("If she can't get closer, still try when the item is at most this much beyond her reach (IK covers it).")]
    [SerializeField] private float extraReach = 0.3f;
    [SerializeField] private float faceSpeed = 360f;
    [SerializeField] private bool showItemInHand = true;
    [Tooltip("Gap between the palm and the item surface while she holds it (m).")]
    [SerializeField] private float palmGap = 0.01f;

    private enum Step { None, Approaching, Facing, Picking }

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

    public Inventory Inventory => inventory;
    public bool IsBusy => step == Step.Facing || step == Step.Picking;
    public bool HasPickupTarget => target != null;

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

    private void OnEnable()
    {
        if (partnerAnimator == null) return;
        partnerAnimator.PickupGrabbed += OnGrabbed;
        partnerAnimator.PickupFinished += OnFinished;
    }

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

    public bool PickUp(Pickup pickup)
    {
        if (IsBusy || partnerAnimator.IsAttacking || partnerAnimator.IsScripted || partnerAnimator.IsDead || pickup == null || !pickup.gameObject.activeInHierarchy) return false;

        target = pickup;
        float itemHeight = pickup.GrabPosition.y - transform.position.y;
        height = itemHeight >= mediumHeight ? PickupHeight.Medium : PickupHeight.Low;
        reach = height == PickupHeight.Medium ? mediumReach : lowReach;

        // the item may be on a table: walk to the closest floor point
        Vector3 goal = pickup.InteractionPosition;
        if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 2f, NavMesh.AllAreas)) goal = hit.position;
        agent.SetDestination(goal);
        step = Step.Approaching;
        PickupRequested?.Invoke();
        return true;
    }

    private void Update()
    {
        switch (step)
        {
            case Step.Approaching: UpdateApproach(); break;
            case Step.Facing: UpdateFacing(); break;
        }
    }

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

    private void StartFacing()
    {
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
        step = Step.Facing;
    }

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

    private void OnFinished()
    {
        ReturnVisual();
        target = null;
        step = Step.None;
        agent.updateRotation = true;
    }

    private void Fail(PickupResult result)
    {
        ItemDefinition item = target != null ? target.Item : null;
        Cancel();
        if (item != null) PickupAttempted?.Invoke(item, 0, result);
    }

    private void Cancel()
    {
        agent.ResetPath();
        agent.updateRotation = true;
        target = null;
        step = Step.None;
    }

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

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
