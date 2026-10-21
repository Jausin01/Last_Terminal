using UnityEngine;

// The props of a clip-driven reload (RangedWeapon Magazine / Shells styles), following her reload clip:
//   magazine - drops out of the gun (a physics copy falls to the floor), the left hand brings a new one from the belt
//              and slides it into the grip;
//   shells   - a shell appears in the left hand at the belt and disappears into the loading port, every loop.
// Runs after WeaponHolder (it places the gun first), so props sit on the final hand / gun pose of the frame.
[DefaultExecutionOrder(50)]
public class ReloadVisuals : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string ShooterField = nameof(shooter);

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerShooter shooter;
    [Tooltip("Seconds the dropped magazine stays on the floor.")]
    [SerializeField] private float dropLifetime = 6f;
    [SerializeField] private float dropSpeed = 0.8f;
    [SerializeField] private float dropMass = 0.3f;

    private RangedWeapon shown;
    private bool dropped;

    private void Awake()
    {
        if (partnerAnimator == null || shooter == null)
        {
            Debug.LogError($"{name}: ReloadVisuals needs a PartnerAnimator and a PartnerShooter.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        RangedWeapon r = shooter.Current;
        if (r != shown)
        {
            if (shown != null) Rest(shown);
            shown = r;
            dropped = false;
        }
        if (r == null || r.Style == RangedWeapon.ReloadMode.Timer || r.ReloadProp == null) return;

        if (!shooter.IsReloading || !partnerAnimator.TryGetReloadTime(out float t))
        {
            Rest(r);
            dropped = false;
            return;
        }

        if (r.Style == RangedWeapon.ReloadMode.Magazine) UpdateMagazine(r, t);
        else UpdateShell(r, t - Mathf.Floor(t));
    }

    private void UpdateMagazine(RangedWeapon r, float t)
    {
        if (t < r.MagOut) { Rest(r); return; }
        if (!dropped)
        {
            DropCopy(r);
            dropped = true;
        }
        if (t < r.MagGrab) { SetVisible(r, false); return; }
        SetVisible(r, true);
        if (t >= r.MagInsert) { r.SetPropHome(); return; }
        if (!TryGetCarryPose(r, out Vector3 pos, out Quaternion rot)) return;
        if (t >= r.MagInsertStart)
        {
            // slide from the hand into the grip
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r.MagInsertStart, r.MagInsert, t));
            r.GetPropHome(out Vector3 homePos, out Quaternion homeRot);
            pos = Vector3.Lerp(pos, homePos, k);
            rot = Quaternion.Slerp(rot, homeRot, k);
        }
        r.ReloadProp.SetPositionAndRotation(pos, rot);
    }

    private void UpdateShell(RangedWeapon r, float loopTime)
    {
        bool inHand = loopTime >= r.ShellGrab && loopTime < r.ShellInsert;
        SetVisible(r, inHand);
        if (!inHand) { r.SetPropHome(); return; }
        if (TryGetCarryPose(r, out Vector3 pos, out Quaternion rot)) r.ReloadProp.SetPositionAndRotation(pos, rot);
    }

    // The prop keeps the pose it has in the left hand at the insert moment (stored in the left-hand frame).
    private bool TryGetCarryPose(RangedWeapon r, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        if (!partnerAnimator.TryGetLeftHandBasis(out Vector3 wrist, out Vector3 fingers, out Vector3 across, out Vector3 palm)) return false;
        Vector3 Hand(Vector3 c) => fingers * c.x + across * c.y + palm * c.z;

        Weapon w = r.Weapon;
        Vector3 gunFwd = (w.Muzzle.position - w.GripPoint.position).normalized;
        Vector3 gunUp = Vector3.ProjectOnPlane(w.UpHint.position - w.GripPoint.position, gunFwd).normalized;
        Quaternion gunRot = Quaternion.LookRotation(gunFwd, gunUp);
        Quaternion carriedGunRot = Quaternion.LookRotation(Hand(r.CarryForward).normalized, Hand(r.CarryUp).normalized);

        r.GetPropHome(out _, out Quaternion homeRot);
        pos = wrist + Hand(r.CarryPosition);
        rot = carriedGunRot * Quaternion.Inverse(gunRot) * homeRot;
        return true;
    }

    // a copy of the empty magazine falls out of the grip and lands on the floor
    private void DropCopy(RangedWeapon r)
    {
        Transform prop = r.ReloadProp;
        GameObject copy = Instantiate(prop.gameObject, prop.position, prop.rotation);
        copy.SetActive(true);
        copy.AddComponent<BoxCollider>();   // fits the mesh
        var body = copy.AddComponent<Rigidbody>();
        body.mass = dropMass;
        Weapon w = r.Weapon;
        Vector3 down = (w.GripPoint.position - w.UpHint.position).normalized;
        body.linearVelocity = down * dropSpeed;
        Destroy(copy, dropLifetime);
    }

    private static void Rest(RangedWeapon r)
    {
        if (r.ReloadProp == null) return;
        r.SetPropHome();
        SetVisible(r, r.Style == RangedWeapon.ReloadMode.Magazine);   // magazine in the gun; shells stay hidden
    }

    private static void SetVisible(RangedWeapon r, bool visible)
    {
        if (r.ReloadProp.gameObject.activeSelf != visible) r.ReloadProp.gameObject.SetActive(visible);
    }
}
