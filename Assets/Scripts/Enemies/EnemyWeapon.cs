using UnityEngine;

// A melee weapon held by an enemy (child of its right hand, placed by Last Terminal/7).
// Damage per attack slot (0 = this weapon can't do that attack, e.g. no swipe with a sledgehammer) and extra reach.
// When the enemy dies the weapon drops: its collider and rigidbody wake up; Reattach puts it back (a save is loaded).
[RequireComponent(typeof(Rigidbody))]
public class EnemyWeapon : MonoBehaviour
{
    public const string SwipeDamageField = nameof(swipeDamage);
    public const string SmashDamageField = nameof(smashDamage);
    public const string BiteDamageField = nameof(biteDamage);
    public const string ReachField = nameof(reach);
    public const string DropColliderField = nameof(dropCollider);

    [SerializeField, Min(0)] private int swipeDamage = 20;
    [SerializeField, Min(0)] private int smashDamage = 25;
    [SerializeField, Min(0)] private int biteDamage;
    [Tooltip("Extra attack range over the bare hands (m).")]
    [SerializeField, Min(0f)] private float reach = 0.2f;
    [Tooltip("Off while held (shots and her melee hit the enemy, not the weapon).")]
    [SerializeField] private Collider dropCollider;

    private Rigidbody body;
    private Transform holder;
    private Vector3 heldPosition;
    private Quaternion heldRotation;
    private bool dropped;

    public float Reach => reach;

    public int DamageFor(EnemyAttackSlot slot) => slot switch
    {
        EnemyAttackSlot.Swipe => swipeDamage,
        EnemyAttackSlot.Smash => smashDamage,
        _ => biteDamage,
    };

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        holder = transform.parent;
        heldPosition = transform.localPosition;
        heldRotation = transform.localRotation;
        Hold();
    }

    public void Drop()
    {
        if (dropped) return;
        dropped = true;
        transform.SetParent(null, true);
        if (dropCollider != null) dropCollider.enabled = true;
        body.isKinematic = false;
    }

    public void Reattach()
    {
        if (!dropped) return;
        dropped = false;
        transform.SetParent(holder, false);
        transform.localPosition = heldPosition;
        transform.localRotation = heldRotation;
        Hold();
    }

    private void Hold()
    {
        body.isKinematic = true;
        if (dropCollider != null) dropCollider.enabled = false;
    }
}
