using UnityEngine;

// Melee data of a weapon (same prefab as its Weapon). The hit volume is a capsule from the
// Weapon's GripPoint to its Muzzle marker (the tip / head).
// Feel: each attack clip is split in phases (wind-up, strike, recovery); every weapon plays them at its own speed,
// so the same swing feels heavy with the axe and snappy with the hammer. Hit-stop freezes the swing on impact.
[RequireComponent(typeof(Weapon))]
public class MeleeWeapon : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string DamageField = nameof(damage);
    public const string HitRadiusField = nameof(hitRadius);
    public const string WindupSpeedField = nameof(windupSpeed);
    public const string StrikeSpeedField = nameof(strikeSpeed);
    public const string RecoverySpeedField = nameof(recoverySpeed);
    public const string HitStopField = nameof(hitStop);

    // Damage of one hit.
    [SerializeField, Min(0)] private int damage = 20;
    [Tooltip("Radius of the capsule from the grip to the tip (m).")]
    [SerializeField, Min(0.01f)] private float hitRadius = 0.06f;

    // Playback speed of each phase of the swing (wind-up, strike, recovery).
    [Header("Feel (playback speed of each phase)")]
    [SerializeField, Range(0.3f, 2f)] private float windupSpeed = 1f;
    [SerializeField, Range(0.3f, 2f)] private float strikeSpeed = 1f;
    [SerializeField, Range(0.3f, 2f)] private float recoverySpeed = 1f;
    [Tooltip("Freeze on impact (s). Longer = heavier.")]
    [SerializeField, Range(0f, 0.3f)] private float hitStop = 0.06f;

    // The Weapon on the same object (its markers give the hit capsule).
    private Weapon weapon;

    // Values read by PartnerMelee.
    public int Damage => damage;
    public float HitRadius => hitRadius;
    public float WindupSpeed => windupSpeed;
    public float StrikeSpeed => strikeSpeed;
    public float RecoverySpeed => recoverySpeed;
    public float HitStop => hitStop;

    // The hit capsule: from the grip to the tip, in world space.
    public Vector3 HitStart => Weapon.GripPoint.position;
    public Vector3 HitEnd => Weapon.Muzzle.position;

    private Weapon Weapon
    {
        get
        {
            if (weapon == null) weapon = GetComponent<Weapon>();
            return weapon;
        }
    }
}
