using UnityEngine;
using UnityEngine.AI;

// A save point you can drop anywhere: desk + computer + chair (a retro terminal).
// PartnerSaveUser walks her in from one side of the desk, she sits (the chair is pulled out, then pushed in),
// types, and the save screen opens.
//
// Prefab layout (Last Terminal/3 builds SavePoint_Base and its variants):
//   SavePoint (this, BoxCollider, NavMeshObstacle)
//     Desk / Computer / Chair   <- model slots: swap the models in a Prefab Variant
//     StandPoint, WalkInRight, WalkInLeft   <- where she stands to sit, where she steps in from
// The geometry is fixed by her sit / typing clips (gizmos show it when selected):
// desk top 0.75 m, desk edge 0.48 m in front of the seat, keyboard 0.52..0.68 m, seat top 0.46 m.
// Forward of this object = toward the desk. Chair slot pivot = where her hips go when seated at the desk.
public class SaveStation : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LocationNameField = nameof(locationName);
    public const string ChairField = nameof(chair);
    public const string StandPointField = nameof(standPoint);
    public const string WalkInRightField = nameof(walkInRight);
    public const string WalkInLeftField = nameof(walkInLeft);
    public const string ChairPullDistanceField = nameof(chairPullDistance);

    // Which side she walks in from (Auto = the one she can reach, nearest first).
    public enum Side { Auto, Right, Left }

    // measured on the clips (station local space, z = toward the desk)
    public const float StandDistance = 0.30f;        // she stands this far in front of the seat to sit (default pull)
    public const float SitTravel = 0.55f;            // her sit clip moves her hips this far back (stand point -> pulled chair)
    public const float DeskTop = 0.75f;
    public const float DeskEdge = 0.48f;
    public const float SeatTop = 0.46f;
    public static readonly Vector3 KeyboardCenter = new Vector3(0f, 0.76f, 0.60f);
    public static readonly Vector3 KeyboardSize = new Vector3(0.44f, 0.04f, 0.16f);

    // Name shown in the save slots.
    [SerializeField] private string locationName = "Office";
    [Tooltip("Which side of the desk she walks in from. Auto = the side closest to her that is on the NavMesh.")]
    [SerializeField] private Side walkInSide = Side.Auto;
    // The chair (pulled out / pushed in), where she stands to sit, and the walk-in points on each side.
    [SerializeField] private Transform chair;
    [SerializeField] private Transform standPoint;
    [SerializeField] private Transform walkInRight;
    [SerializeField] private Transform walkInLeft;
    [Tooltip("How far the chair is pulled out for her to sit (m). Her sit clip travels 0.55 m backwards, so she stands " +
             "0.55 - this in front of the seat (pull more when the table edge is close to the seat).")]
    // How far the chair is pulled out (m).
    [SerializeField] private float chairPullDistance = 0.25f;

    [Header("Fit To Models (context menu)")]
    [Tooltip("Radius the NavMesh is baked with (Humanoid agent type; Last Terminal/4 sets it to 0.3 = her agent).")]
    [SerializeField] private float bakeAgentRadius = 0.3f;
    [Tooltip("Radius of her NavMeshAgent.")]
    [SerializeField] private float partnerRadius = 0.3f;

    // The chair's pushed-in position.
    private Vector3 chairHome;

    // Read-only data for PartnerSaveUser and the save screen.
    public string LocationName => locationName;
    public Vector3 StandPosition => standPoint.position;
    public Vector3 Facing => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    // where her hips must be (on the floor plane) for the current chair position
    public Vector3 SeatPosition => chair.position;
    // where she stands to sit, in front of the pushed-in seat
    public float StandOffset => SitTravel - chairPullDistance;

    // Records the chair position; checks the slots.
    private void Awake()
    {
        if (chair == null || standPoint == null || walkInRight == null || walkInLeft == null)
        {
            Debug.LogError($"{name}: SaveStation is missing its Chair / StandPoint / WalkIn points.", this);
            enabled = false;
            return;
        }
        chairHome = chair.position;
    }

    // Side to walk in from, seen from where she is now.
    public Vector3 GetWalkInPosition(Vector3 from)
    {
        if (walkInSide == Side.Right) return walkInRight.position;
        if (walkInSide == Side.Left) return walkInLeft.position;
        bool rightOk = IsOnNavMesh(walkInRight.position);
        bool leftOk = IsOnNavMesh(walkInLeft.position);
        if (rightOk != leftOk) return rightOk ? walkInRight.position : walkInLeft.position;
        float r = (walkInRight.position - from).sqrMagnitude;
        float l = (walkInLeft.position - from).sqrMagnitude;
        return r <= l ? walkInRight.position : walkInLeft.position;
    }

    // A loaded save puts her beside the desk (this side), facing out.
    public float ResumeYaw(Vector3 walkIn) =>
        Quaternion.LookRotation(Vector3.ProjectOnPlane(walkIn - standPoint.position, Vector3.up)).eulerAngles.y;

    // 0 = pushed in (at the desk), 1 = pulled out.
    public void SetChairPull(float amount)
    {
        chair.position = chairHome - Facing * (chairPullDistance * Mathf.Clamp01(amount));
    }

    // Is this point on the NavMesh?
    private static bool IsOnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out _, 0.2f, NavMesh.AllAreas);

    // Sizes the click collider and the NavMesh hole to the models in the slots, and puts the walk-in points
    // just outside the hole. Run it after changing the models of a variant (Last Terminal/3 runs it for its variants).
    [ContextMenu("Fit To Models")]
    public void FitToModels()
    {
        Bounds b = new Bounds(new Vector3(0f, 0.5f, 0.4f), new Vector3(1.2f, 1f, 1.8f));
        bool first = true;
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = toLocal.MultiplyPoint3x4(corner);
                if (first) { b = new Bounds(local, Vector3.zero); first = false; }
                else b.Encapsulate(local);
            }
        }
        // the chair slides back when pulled out
        b.Encapsulate(new Vector3(0f, 0f, -0.5f - chairPullDistance * 0.5f));

        if (TryGetComponent(out BoxCollider box))
        {
            box.center = b.center;
            box.size = b.size;
        }
        if (TryGetComponent(out NavMeshObstacle obstacle))
        {
            // carving grows the hole by the bake radius: shrink the box so the hole matches her real radius
            float shrink = Mathf.Max(0f, bakeAgentRadius - partnerRadius) * 2f;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = b.center;
            obstacle.size = new Vector3(Mathf.Max(0.2f, b.size.x - shrink), b.size.y, Mathf.Max(0.2f, b.size.z - shrink));
            obstacle.carving = true;
        }
        float side = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)) + partnerRadius + 0.15f;
        if (standPoint != null) standPoint.localPosition = new Vector3(0f, 0f, StandOffset);
        if (walkInRight != null) walkInRight.localPosition = new Vector3(side, 0f, StandOffset);
        if (walkInLeft != null) walkInLeft.localPosition = new Vector3(-side, 0f, StandOffset);
        MarkChanged(box);
        MarkChanged(obstacle);
        MarkChanged(standPoint);
        MarkChanged(walkInRight);
        MarkChanged(walkInLeft);
    }

    // editor: keep the change (scene / prefab asset / prefab variant override)
    private static void MarkChanged(Object target)
    {
#if UNITY_EDITOR
        if (target == null) return;
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }

    // walk-in points: green = on the NavMesh, red = not reachable (move the save point or pick the other side)
    private void OnDrawGizmos()
    {
        if (standPoint == null) return;
        DrawWalkIn(walkInRight, walkInSide != Side.Left);
        DrawWalkIn(walkInLeft, walkInSide != Side.Right);
    }

    // Gizmo of a walk-in point (bright when it is the one used).
    private void DrawWalkIn(Transform point, bool used)
    {
        if (point == null) return;
        Gizmos.color = !used ? Color.gray : IsOnNavMesh(point.position) ? Color.green : Color.red;
        Gizmos.DrawWireSphere(point.position + Vector3.up * 0.05f, 0.15f);
        Gizmos.DrawLine(point.position + Vector3.up * 0.05f, standPoint.position + Vector3.up * 0.05f);
    }

    // guides for your own models: keyboard zone, desk top / edge, seat, knee room
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 1f, 0.4f);
        Gizmos.DrawWireCube(KeyboardCenter, KeyboardSize);
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireCube(new Vector3(0f, DeskTop, DeskEdge + 0.4f), new Vector3(1.2f, 0.005f, 0.8f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.6f, 0.45f), new Vector3(0.6f, 0.22f, 0.3f));   // knees: keep empty
        Gizmos.color = new Color(0.4f, 0.7f, 1f);
        Gizmos.DrawWireCube(new Vector3(0f, SeatTop, 0.07f), new Vector3(0.44f, 0.005f, 0.42f));
        Gizmos.DrawWireSphere(new Vector3(0f, 0.05f, StandOffset), 0.08f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
