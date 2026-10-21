using UnityEngine;

public enum WeaponHoldType { Pistol = 0, Shotgun = 1, Blade = 2, Blunt = 3 }

// A weapon model + how she holds it.
// Markers are exported with the model (Blender empties): GripPoint = where the right palm holds it,
// Muzzle = barrel tip, UpHint = above the grip (the gun's "up").
// Holds are stored in the right-hand frame (x = toward the fingers, y = toward the index, z = out of the palm),
// measured on her animations: one for the idle pose, one for the aim pose (blended by how much she is aiming).
// Two-handed weapons also define where the left hand goes, in the gun frame (x = right, y = up, z = toward the muzzle).
public class Weapon : MonoBehaviour
{
    public const string GripPointField = nameof(gripPoint);
    public const string MuzzleField = nameof(muzzle);
    public const string UpHintField = nameof(upHint);
    public const string HoldTypeField = nameof(holdType);
    public const string AimGripField = nameof(aimGrip);
    public const string AimBarrelField = nameof(aimBarrel);
    public const string AimUpField = nameof(aimUp);
    public const string IdleGripField = nameof(idleGrip);
    public const string IdleBarrelField = nameof(idleBarrel);
    public const string IdleUpField = nameof(idleUp);
    public const string TwoHandedField = nameof(twoHanded);

    [SerializeField] private Transform gripPoint;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform upHint;

    [Header("Right-hand hold (hand frame)")]
    [SerializeField] private WeaponHoldType holdType = WeaponHoldType.Pistol;
    [SerializeField] private Vector3 aimGrip = new Vector3(0f, 0f, 0.025f);
    [SerializeField] private Vector3 aimBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f);
    [SerializeField] private Vector3 aimUp = new Vector3(0.1564f, 0.9877f, 0f);
    [SerializeField] private Vector3 idleGrip = new Vector3(0f, 0f, 0.025f);
    [SerializeField] private Vector3 idleBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f);
    [SerializeField] private Vector3 idleUp = new Vector3(0.1564f, 0.9877f, 0f);

    [Header("Left hand (two-handed weapons, gun frame)")]
    [SerializeField] private bool twoHanded;
    [Tooltip("Point on the pump / forend axis the left palm wraps (from the GripPoint).")]
    [SerializeField] private Vector3 leftAxisPoint = new Vector3(0f, 0.006f, 0.31f);
    [Tooltip("Direction from the left palm toward that axis (palm up, a bit to the right).")]
    [SerializeField] private Vector3 leftPalmDirection = new Vector3(0.45f, 1f, 0f);
    [Tooltip("Where the left fingers point (wrapping the right side of the forend).")]
    [SerializeField] private Vector3 leftFingersDirection = new Vector3(0.8f, 0.3f, 0.6f);
    [Tooltip("Forend radius + palm thickness (m).")]
    [SerializeField] private float leftPalmDepth = 0.04f;

    private float leftSlide;   // pump racked back (m): the left hand follows it

    public Transform GripPoint => gripPoint;
    public Transform Muzzle => muzzle;
    public Transform UpHint => upHint;
    public WeaponHoldType HoldType => holdType;
    public bool HasMarkers => gripPoint != null && muzzle != null && upHint != null;
    public bool IsTwoHanded => twoHanded;
    public Vector3 LeftAxisPoint => leftAxisPoint - Vector3.forward * leftSlide;
    public Vector3 LeftPalmDirection => leftPalmDirection;
    public Vector3 LeftFingersDirection => leftFingersDirection;
    public float LeftPalmDepth => leftPalmDepth;

    // How far the forend is racked back right now (RangedWeapon pump).
    public void SetLeftSlide(float meters) => leftSlide = meters;

    // Hold for a given aim amount (0 = idle pose, 1 = aim pose), in the right-hand frame.
    public void GetHold(float aim, out Vector3 grip, out Vector3 barrel, out Vector3 up)
    {
        grip = Vector3.Lerp(idleGrip, aimGrip, aim);
        barrel = Vector3.Slerp(idleBarrel.normalized, aimBarrel.normalized, aim);
        up = Vector3.Slerp(idleUp.normalized, aimUp.normalized, aim);
    }
}
