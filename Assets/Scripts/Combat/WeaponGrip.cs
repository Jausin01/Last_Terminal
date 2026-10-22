using UnityEngine;

// Hand frames, weapon placement and finger grips, shared by WeaponHolder (the game) and ModelCompare (the test scene).
// Frames are built from the hand bones (independent of the bone axes):
//   right: origin between wrist and middle knuckle, fingers, across (toward the index), normal (out of the palm);
//   left:  wrist, fingers, across = Cross(fingers, palm), palm.
// Finger grips (Weapon.RightFingers / LeftFingers) are the direction of each finger segment in that frame, applied
// after the animation: every segment is turned onto its direction, proximal to distal.
public static class WeaponGrip
{
    public const int FingerSegments = 15;   // thumb, index, middle, ring, little x proximal / intermediate / distal

    private static readonly HumanBodyBones[] RightBones =
    {
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
    };
    private static readonly HumanBodyBones[] LeftBones =
    {
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
    };

    // the finger joints of a hand + where each segment ends (the next joint, or the tip bone after the distal)
    public static bool GetFingers(System.Func<HumanBodyBones, Transform> bone, bool right, out Transform[] joints, out Transform[] ends)
    {
        HumanBodyBones[] ids = right ? RightBones : LeftBones;
        joints = new Transform[FingerSegments];
        ends = new Transform[FingerSegments];
        for (int i = 0; i < FingerSegments; i++)
        {
            joints[i] = bone(ids[i]);
            if (joints[i] == null) return false;
        }
        for (int i = 0; i < FingerSegments; i++)
            ends[i] = i % 3 < 2 ? joints[i + 1] : (joints[i].childCount > 0 ? joints[i].GetChild(0) : null);
        return true;
    }

    public static bool TryRightFrame(Animator animator, out Vector3 origin, out Vector3 fingers, out Vector3 across, out Vector3 normal)
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            origin = fingers = across = normal = Vector3.zero;
            return false;
        }
        fingers = (middle.position - hand.position).normalized;
        Vector3 a = index.position - little.position;
        across = (a - Vector3.Dot(a, fingers) * fingers).normalized;
        normal = Vector3.Cross(fingers, across).normalized;
        origin = Vector3.Lerp(hand.position, middle.position, 0.5f);
        return true;
    }

    // the weapon in the right hand: its GripPoint on the hold point, barrel / up along the hold (aim 0 = idle, 1 = aim)
    public static void PlaceInRightHand(Weapon w, Vector3 origin, Vector3 fingers, Vector3 across, Vector3 normal, float aim)
    {
        w.GetHold(aim, out Vector3 grip, out Vector3 barrel, out Vector3 up);
        Vector3 FromFrame(Vector3 c) => fingers * c.x + across * c.y + normal * c.z;
        Vector3 targetGrip = origin + FromFrame(grip);
        Vector3 targetBarrel = FromFrame(barrel).normalized;
        Vector3 targetUp = FromFrame(up).normalized;

        Transform t = w.transform;
        Vector3 modelBarrel = (w.Muzzle.position - w.GripPoint.position).normalized;
        Vector3 modelUp = (w.UpHint.position - w.GripPoint.position).normalized;
        Quaternion delta = Quaternion.LookRotation(targetBarrel, targetUp) * Quaternion.Inverse(Quaternion.LookRotation(modelBarrel, modelUp));
        t.rotation = delta * t.rotation;
        t.position += targetGrip - w.GripPoint.position;
    }

    // every finger segment turned onto its direction (in the frame x / y / z), weight 0 = the clip, 1 = the grip
    public static void ApplyFingers(Transform[] joints, Transform[] ends, Vector3[] directions, Vector3 x, Vector3 y, Vector3 z, float weight)
    {
        if (joints == null || directions == null || directions.Length < FingerSegments || weight <= 0f) return;
        for (int i = 0; i < FingerSegments; i++)
        {
            if (ends[i] == null) continue;
            Vector3 current = ends[i].position - joints[i].position;
            Vector3 d = directions[i];
            Vector3 want = x * d.x + y * d.y + z * d.z;
            if (current.sqrMagnitude < 1e-10f || want.sqrMagnitude < 1e-10f) continue;
            Quaternion turn = Quaternion.FromToRotation(current, want);
            joints[i].rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * joints[i].rotation;
        }
    }
}
