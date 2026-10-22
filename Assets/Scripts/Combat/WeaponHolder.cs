using UnityEngine;

// Puts the selected weapon of the loadout in her hands, every frame after the animation (LateUpdate):
// 1) the weapon is rigid in the right hand, in a frame built from the hand bones (independent of bone axes),
//    blending the idle hold and the aim hold by how much she is aiming;
// 2) two-handed weapons: the left arm is solved with two-bone IK so the left palm wraps the forend;
// 3) the fingers closed on the weapon (its grip, Last Terminal/19), right hand and left hand on the forend.
public class WeaponHolder : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string LoadoutField = nameof(loadout);
    public const string InventoryField = nameof(inventory);
    public const string RequireItemsField = nameof(requireItems);

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private Weapon[] loadout = new Weapon[0];
    [Tooltip("Her inventory: with Require Items, a weapon of the loadout is hers only while she carries its item (WeaponItem, id = the weapon's name).")]
    [SerializeField] private Inventory inventory;
    [Tooltip("Off: she has every weapon of the loadout (test rooms). On: only the ones she picked up.")]
    [SerializeField] private bool requireItems;
    [Tooltip("Seconds to hand the left arm over to the reload clip and back.")]
    [SerializeField] private float leftHandBlendTime = 0.15f;

    private Weapon[] instances;
    private int current = -1;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private float leftWeight = 1f;   // left-hand IK: off while a reload clip moves the left arm
    private Transform[] rightJoints, rightEnds, leftJoints, leftEnds;   // finger segments (WeaponGrip)

    public int LoadoutCount => loadout.Length;
    public int CurrentIndex => current;
    public Weapon Current => current >= 0 ? instances[current] : null;
    public bool IsEquipped => Current != null && Current.gameObject.activeSelf;

    private void Awake()
    {
        if (partnerAnimator == null || loadout.Length == 0 || (requireItems && inventory == null))
        {
            Debug.LogError($"{name}: WeaponHolder needs a PartnerAnimator, at least one weapon in the loadout (and the Inventory with Require Items).", this);
            enabled = false;
            return;
        }
        instances = new Weapon[loadout.Length];
    }

    private void Start()
    {
        leftUpperArm = partnerAnimator.GetBone(HumanBodyBones.LeftUpperArm);
        leftLowerArm = partnerAnimator.GetBone(HumanBodyBones.LeftLowerArm);
        leftHand = partnerAnimator.GetBone(HumanBodyBones.LeftHand);
        if (!WeaponGrip.GetFingers(partnerAnimator.GetBone, true, out rightJoints, out rightEnds)) rightJoints = null;
        if (!WeaponGrip.GetFingers(partnerAnimator.GetBone, false, out leftJoints, out leftEnds)) leftJoints = null;
    }

    public Weapon GetWeapon(int index) => index >= 0 && index < loadout.Length ? loadout[index] : null;

    // she has this weapon (always, without Require Items)
    public bool Owns(int index)
    {
        Weapon w = GetWeapon(index);
        if (w == null) return false;
        if (!requireItems) return true;
        return inventory != null && inventory.FindById(w.name) != null;
    }

    // the first weapon she has, -1 = none
    public int FirstOwned()
    {
        for (int i = 0; i < loadout.Length; i++) if (Owns(i)) return i;
        return -1;
    }

    // Shows the weapon at index in her hand (hides the previous one). False if it can't be used.
    public bool Equip(int index)
    {
        if (!enabled || index < 0 || index >= loadout.Length || loadout[index] == null || !Owns(index)) return false;
        Transform rightHand = partnerAnimator.RightHand;
        if (rightHand == null) return false;

        if (instances[index] == null)
        {
            Weapon w = Instantiate(loadout[index], rightHand);
            if (!w.HasMarkers)
            {
                Debug.LogError($"{name}: {loadout[index].name} is missing its GripPoint / Muzzle / UpHint markers.", this);
                Destroy(w.gameObject);
                return false;
            }
            instances[index] = w;
        }

        if (current >= 0 && current != index && instances[current] != null)
            instances[current].gameObject.SetActive(false);
        current = index;
        instances[current].gameObject.SetActive(true);
        PlaceInRightHand(instances[current]);
        return true;
    }

    // No back/hip socket yet: the weapon is just hidden.
    public void Holster()
    {
        if (Current != null) Current.gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (Current != null) Current.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        Weapon w = Current;
        if (w == null || !w.gameObject.activeSelf) return;
        if (!partnerAnimator.TryGetRightHandFrame(out Vector3 origin, out Vector3 fingers, out Vector3 across, out Vector3 normal)) return;
        WeaponGrip.PlaceInRightHand(w, origin, fingers, across, normal, partnerAnimator.AimBlend);
        WeaponGrip.ApplyFingers(rightJoints, rightEnds, w.RightFingers, fingers, across, normal, 1f);
        float step = leftHandBlendTime > 0f ? Time.deltaTime / leftHandBlendTime : 1f;
        leftWeight = Mathf.MoveTowards(leftWeight, partnerAnimator.IsReloadPlaying ? 0f : 1f, step);
        if (w.IsTwoHanded && leftWeight > 0f)
        {
            PlaceLeftHand(w, leftWeight);
            if (partnerAnimator.TryGetLeftHandBasis(out _, out Vector3 lFingers, out Vector3 lAcross, out Vector3 lPalm))
                WeaponGrip.ApplyFingers(leftJoints, leftEnds, w.LeftFingers, lFingers, lAcross, lPalm, leftWeight);
        }
    }

    private bool PlaceInRightHand(Weapon w)
    {
        if (!partnerAnimator.TryGetRightHandFrame(out Vector3 origin, out Vector3 fingers, out Vector3 across, out Vector3 normal))
            return false;
        WeaponGrip.PlaceInRightHand(w, origin, fingers, across, normal, partnerAnimator.AimBlend);
        return true;
    }

    private void PlaceLeftHand(Weapon w, float weight)
    {
        if (!WeaponGrip.TryLeftFrame(partnerAnimator.GetBone, out _, out _, out _, out float handLength)) return;
        WeaponGrip.LeftHold(w, handLength, out Vector3 wrist, out Vector3 fingers, out Vector3 palm);
        WeaponGrip.ReachLeftHand(partnerAnimator.GetBone, leftUpperArm, leftLowerArm, leftHand, wrist, fingers, palm, weight);
    }
}
