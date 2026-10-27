using UnityEngine;

// A melee weapon held by an enemy (child of its right hand, placed by Last Terminal/7).
// Damage per attack slot (0 = this weapon can't do that attack, e.g. no swipe with a sledgehammer) and extra reach.
// When the enemy dies the weapon drops: its collider and rigidbody wake up; Reattach puts it back (a save is loaded).
[RequireComponent(typeof(Rigidbody))]
public class EnemyWeapon : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SwipeDamageField = nameof(swipeDamage);
    public const string SmashDamageField = nameof(smashDamage);
    public const string BiteDamageField = nameof(biteDamage);
    public const string ReachField = nameof(reach);
    public const string DropColliderField = nameof(dropCollider);

    // Damage added to each attack slot (0 = this weapon can't do that attack).
    [SerializeField, Min(0)] private int swipeDamage = 20;
    [SerializeField, Min(0)] private int smashDamage = 25;
    [SerializeField, Min(0)] private int biteDamage;
    [Tooltip("Extra attack range over the bare hands (m).")]
    [SerializeField, Min(0f)] private float reach = 0.2f;
    [Tooltip("Off while held (shots and her melee hit the enemy, not the weapon).")]
    [SerializeField] private Collider dropCollider;

    // Its rigidbody, the hand it hangs from and its pose there, and whether it was dropped.
    private Rigidbody body;
    private Transform holder;
    private Vector3 heldPosition;
    private Quaternion heldRotation;
    private bool dropped;

    // Extra reach (read by EnemyBrain).
    public float Reach => reach;

    // Damage added to an attack slot.
    public int DamageFor(EnemyAttackSlot slot) => slot switch
    {
        EnemyAttackSlot.Swipe => swipeDamage,
        EnemyAttackSlot.Smash => smashDamage,
        _ => biteDamage,
    };

    // Remembers its place in the hand; held (kinematic, no collider).
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        holder = transform.parent;
        heldPosition = transform.localPosition;
        heldRotation = transform.localRotation;
        Hold();
    }

    // The enemy died: the weapon falls with physics.
    public void Drop()
    {
        if (dropped) return;
        dropped = true;
        transform.SetParent(null, true);
        if (dropCollider != null) dropCollider.enabled = true;
        body.isKinematic = false;
    }

    // A save was loaded: back in the hand.
    public void Reattach()
    {
        if (!dropped) return;
        dropped = false;
        transform.SetParent(holder, false);
        transform.localPosition = heldPosition;
        transform.localRotation = heldRotation;
        Hold();
    }

    // Kinematic and without collider while held.
    private void Hold()
    {
        body.isKinematic = true;
        if (dropCollider != null) dropCollider.enabled = false;
    }
}
