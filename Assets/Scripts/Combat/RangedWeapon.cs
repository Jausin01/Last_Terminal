using UnityEngine;

// Firearm data of a weapon (same prefab as its Weapon). Shots leave from the Weapon's Muzzle marker.
// Ammo comes from the partner's Inventory (the AmmoItem below) when she reloads; the magazine count lives in PartnerShooter.
//
// Reload styles:
//   Timer    - no animation, just a delay (whole magazine, or round by round).
//   Magazine - her reload clip: the magazine drops out, a new one comes from her belt in the left hand and goes in.
//   Shells   - her shell loop clip: one shell per loop, from the belt into the loading port.
// The moments are normalized times of the clip (measured on the Mixamo / authored files). The prop carried by the
// left hand keeps the pose it has at the insert moment, stored in the left-hand frame
// (x = toward the fingers, y = toward the index, z = out of the palm), like the hand holds in Weapon.
[RequireComponent(typeof(Weapon))]
public class RangedWeapon : MonoBehaviour
{
    public enum ReloadMode { Timer, Magazine, Shells }

    public const string AmmoField = nameof(ammo);
    public const string MagazineSizeField = nameof(magazineSize);
    public const string DamageField = nameof(damage);
    public const string PelletsField = nameof(pellets);
    public const string SpreadField = nameof(spread);
    public const string RangeField = nameof(range);
    public const string FireIntervalField = nameof(fireInterval);
    public const string ReloadTimeField = nameof(reloadTime);
    public const string ReloadPerRoundField = nameof(reloadPerRound);
    public const string RecoilField = nameof(recoil);
    public const string ReloadStyleField = nameof(reloadStyle);
    public const string ReloadPropField = nameof(reloadProp);
    public const string CarryPositionField = nameof(carryPosition);
    public const string CarryForwardField = nameof(carryForward);
    public const string CarryUpField = nameof(carryUp);
    public const string PumpField = nameof(pump);

    [SerializeField] private AmmoItem ammo;
    [SerializeField, Min(1)] private int magazineSize = 7;

    [Header("Shot")]
    [Tooltip("Damage of each pellet / bullet.")]
    [SerializeField, Min(0)] private int damage = 25;
    [Tooltip("1 = bullet, more = shotgun pellets.")]
    [SerializeField, Min(1)] private int pellets = 1;
    [Tooltip("Random cone around the aim direction (deg).")]
    [SerializeField, Range(0f, 20f)] private float spread = 1f;
    [SerializeField, Min(1f)] private float range = 40f;
    [Tooltip("Minimum time between shots (s): trigger / pump.")]
    [SerializeField, Min(0.05f)] private float fireInterval = 0.3f;

    [Header("Reload")]
    [SerializeField] private ReloadMode reloadStyle = ReloadMode.Timer;
    [Tooltip("Timer style: whole magazine (s), or one round when Reload Per Round is on.")]
    [SerializeField, Min(0.1f)] private float reloadTime = 1.6f;
    [Tooltip("Loads one round at a time and can be interrupted by a shot (Timer and Shells styles).")]
    [SerializeField] private bool reloadPerRound;
    [Tooltip("Magazine / shell mesh of the model (child of the gun, at its place in the gun).")]
    [SerializeField] private Transform reloadProp;

    [Header("Magazine style (normalized time of the reload clip)")]
    [SerializeField, Range(0f, 1f)] private float magOut = 0.18f;
    [SerializeField, Range(0f, 1f)] private float magGrab = 0.31f;
    [SerializeField, Range(0f, 1f)] private float magInsertStart = 0.77f;
    [SerializeField, Range(0f, 1f)] private float magInsert = 0.82f;

    [Header("Shells style (one loop of the clip = one shell)")]
    [SerializeField, Range(0f, 1f)] private float shellGrab = 0.30f;
    [SerializeField, Range(0f, 1f)] private float shellInsert = 0.79f;

    [Header("Prop in the left hand (left-hand frame)")]
    [SerializeField] private Vector3 carryPosition;
    [SerializeField] private Vector3 carryForward = Vector3.forward;
    [SerializeField] private Vector3 carryUp = Vector3.up;

    [Header("Pump (shotgun)")]
    [Tooltip("Forend mesh: slides back and forward after every shot (the left hand follows it).")]
    [SerializeField] private Transform pump;
    [SerializeField] private float pumpTravel = 0.07f;
    [SerializeField] private float pumpDelay = 0.2f;
    [SerializeField] private float pumpTime = 0.35f;

    [Header("Feel")]
    [Tooltip("Upper-body kick per shot (deg).")]
    [SerializeField, Range(0f, 20f)] private float recoil = 4f;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptySound;

    private Weapon weapon;
    private Vector3 propHomePosition;
    private Quaternion propHomeRotation;
    private Vector3 pumpHome;
    private float pumpTimer = -1f;

    public AmmoItem Ammo => ammo;
    public int MagazineSize => magazineSize;
    public int Damage => damage;
    public int Pellets => pellets;
    public float Spread => spread;
    public float Range => range;
    public float FireInterval => fireInterval;
    public float ReloadTime => reloadTime;
    public bool ReloadPerRound => reloadPerRound || reloadStyle == ReloadMode.Shells;
    public float Recoil => recoil;
    public AudioClip FireSound => fireSound;
    public AudioClip ReloadSound => reloadSound;
    public AudioClip EmptySound => emptySound;

    public ReloadMode Style => reloadStyle == ReloadMode.Timer || reloadProp != null ? reloadStyle : ReloadMode.Timer;
    public Transform ReloadProp => reloadProp;
    public float MagOut => magOut;
    public float MagGrab => magGrab;
    public float MagInsertStart => magInsertStart;
    public float MagInsert => magInsert;
    public float ShellGrab => shellGrab;
    public float ShellInsert => shellInsert;
    public Vector3 CarryPosition => carryPosition;
    public Vector3 CarryForward => carryForward;
    public Vector3 CarryUp => carryUp;

    public Transform Muzzle => Weapon.Muzzle;
    public Weapon Weapon
    {
        get
        {
            if (weapon == null) weapon = GetComponent<Weapon>();
            return weapon;
        }
    }

    private void Awake()
    {
        if (reloadProp != null)
        {
            propHomePosition = reloadProp.localPosition;
            propHomeRotation = reloadProp.localRotation;
        }
        if (pump != null) pumpHome = pump.localPosition;
    }

    // ------------------------------------------------------------------ reload prop (driven by ReloadVisuals)
    public void SetPropHome()
    {
        if (reloadProp == null) return;
        reloadProp.localPosition = propHomePosition;
        reloadProp.localRotation = propHomeRotation;
    }

    // World pose of the prop when it sits in the gun.
    public void GetPropHome(out Vector3 position, out Quaternion rotation)
    {
        Transform parent = reloadProp.parent;
        position = parent.TransformPoint(propHomePosition);
        rotation = parent.rotation * propHomeRotation;
    }

    // ------------------------------------------------------------------ pump
    public bool HasPump => pump != null;

    // Rack the pump after a shot (starts after pumpDelay).
    public void CyclePump()
    {
        if (pump != null) pumpTimer = 0f;
    }

    private void Update()
    {
        if (pump == null || pumpTimer < 0f) return;
        pumpTimer += Time.deltaTime;
        float t = Mathf.InverseLerp(pumpDelay, pumpDelay + pumpTime, pumpTimer);
        float slide = Mathf.Sin(t * Mathf.PI) * pumpTravel;   // back, then forward
        if (t >= 1f) { slide = 0f; pumpTimer = -1f; }
        Vector3 back = pump.parent.InverseTransformDirection(Weapon.GripPoint.position - Weapon.Muzzle.position).normalized;
        pump.localPosition = pumpHome + back * slide;
        Weapon.SetLeftSlide(slide);
    }

    // true while the pump is moving (the left hand follows it)
    public bool IsPumping => pumpTimer >= 0f;
}
