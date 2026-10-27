using UnityEngine;

// One CCTV camera on a wall. It doesn't render anything itself: SecurityCameraSystem puts the single view camera on
// the active camera's Lens and turns the model's Pan (left / right) and Tilt (down) parts with the operator's input.
// Yaw 0 = the direction this object faces (+Z); the operator pans inside [-yawRange, +yawRange].
public class SecurityCamera : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LabelField = nameof(label);
    public const string RoomField = nameof(room);
    public const string PanField = nameof(pan);
    public const string TiltField = nameof(tilt);
    public const string LensField = nameof(lens);
    public const string PitchField = nameof(pitch);
    public const string YawRangeField = nameof(yawRange);

    // Name shown on the HUD and the room it watches.
    [SerializeField] private string label = "CAM 01";
    [SerializeField] private Room room;
    // Model parts: the pan pivot (left / right), the tilt pivot (down) and the lens the view camera sits on.
    [SerializeField] private Transform pan;
    [SerializeField] private Transform tilt;
    [SerializeField] private Transform lens;
    [Tooltip("How far down the camera looks (deg).")]
    [SerializeField, Range(0f, 80f)] private float pitch = 28f;
    [Tooltip("How far the operator can pan each way (deg).")]
    [SerializeField, Range(0f, 170f)] private float yawRange = 55f;
    // Field of view of the view camera on this camera (deg).
    [SerializeField, Range(20f, 100f)] private float fieldOfView = 70f;

    // Rest rotations of the pan / tilt parts (stored once).
    private Quaternion panHome = Quaternion.identity;
    private Quaternion tiltHome = Quaternion.identity;
    private bool homeStored;

    // Read-only data for the system, HUD and map.
    public string Label => label;
    public Room Room => room;
    public string RoomName => room != null ? room.DisplayName : "";
    public float YawRange => yawRange;
    public float FieldOfView => fieldOfView;
    // Current pan angle (deg).
    public float Yaw { get; private set; }
    // Where the view camera goes.
    public Transform Lens => lens;

    // Stores the rest rotations.
    private void Awake()
    {
        StoreHome();
        SetYaw(0f);
    }

    // Home poses relative to this object: the model's own node axes don't matter (Blender empties / nodes
    // can come in with extra rotations), everything turns around this object's up and right.
    private void StoreHome()
    {
        if (homeStored) return;
        Quaternion inv = Quaternion.Inverse(transform.rotation);
        if (pan != null) panHome = inv * pan.rotation;
        if (tilt != null) tiltHome = inv * tilt.rotation;
        homeStored = true;
    }

    // Turns the model (pan around this object's up, tilt around its right) and clamps to the range.
    public void SetYaw(float yaw)
    {
        StoreHome();
        Yaw = Mathf.Clamp(yaw, -yawRange, yawRange);
        Quaternion turn = transform.rotation * Quaternion.AngleAxis(Yaw, Vector3.up);
        if (pan != null) pan.rotation = turn * panHome;
        if (tilt != null) tilt.rotation = turn * Quaternion.AngleAxis(pitch, Vector3.right) * tiltHome;
    }

    // A camera without a model that moves to where a game is played (GameCameraDirector): placed at `from`, turned
    // toward `lookAt` (yaw on this object, pitch down), in that room.
    public void Frame(Vector3 from, Vector3 lookAt, Room inRoom)
    {
        Vector3 d = lookAt - from;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(flat));
        pitch = Mathf.Clamp(Mathf.Atan2(-d.y, flat.magnitude) * Mathf.Rad2Deg, 0f, 80f);
        room = inRoom;
        SetYaw(0f);
    }

    // cutscene shots: the label on the screen and the lens
    public void SetLabel(string value) => label = value;
    public void SetFieldOfView(float value) => fieldOfView = Mathf.Clamp(value, 10f, 100f);

    // Where the view camera goes: at the lens, looking along this object's forward turned by yaw and pitch
    // (not the Lens marker's rotation, which depends on how the model was exported).
    public void GetView(out Vector3 position, out Quaternion rotation)
    {
        rotation = transform.rotation * Quaternion.AngleAxis(Yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
        Vector3 origin = lens != null ? lens.position : transform.position;
        position = origin + rotation * Vector3.forward * 0.03f;
    }

    // scene view: the field of view at both pan limits
    private void OnDrawGizmosSelected()
    {
        Transform t = lens != null ? lens : transform;
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
        foreach (float y in new[] { -yawRange, 0f, yawRange })
        {
            Quaternion r = Quaternion.AngleAxis(y, Vector3.up) * Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up)) * Quaternion.Euler(pitch, 0f, 0f);
            Gizmos.DrawRay(t.position, r * Vector3.forward * 6f);
        }
    }
}
