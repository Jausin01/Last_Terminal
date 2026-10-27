using UnityEngine;

// Hand frames, weapon placement, the left hand IK and finger grips, shared by WeaponHolder (the game), ModelCompare
// (the test scene) and HelenaClipBaker (her clips adjusted to Helena 2).
// Frames are built from the hand bones (independent of the bone axes):
//   right: origin between wrist and middle knuckle, fingers, across (toward the index), normal (out of the palm);
//   left:  wrist, fingers, across = Cross(fingers, palm), palm.
// Finger grips (Weapon.RightFingers / LeftFingers) are the direction of each finger segment in that frame, applied
// after the animation: every segment is turned onto its direction, proximal to distal.
public static class WeaponGrip
{
    public const int FingerSegments = 15;   // thumb, index, middle, ring, little x proximal / intermediate / distal

    // Finger bones of each hand, in segment order (same order as Weapon.RightFingers / LeftFingers).
    private static readonly HumanBodyBones[] RightBones =
    {
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
    };
    // Finger bones of the left hand (same order as the right).
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

    // Right-hand frame from the hand bones: origin (mid palm), fingers, across (toward the index), normal (out of the palm).
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

    // left hand: wrist, fingers, palm (out of the palm) and wrist -> middle knuckle length (same as PartnerAnimator)
    public static bool TryLeftFrame(System.Func<HumanBodyBones, Transform> bone, out Vector3 wrist, out Vector3 fingers, out Vector3 palm, out float handLength)
    {
        Transform hand = bone(HumanBodyBones.LeftHand);
        Transform index = bone(HumanBodyBones.LeftIndexProximal);
        Transform middle = bone(HumanBodyBones.LeftMiddleProximal);
        Transform little = bone(HumanBodyBones.LeftLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            wrist = fingers = palm = Vector3.zero;
            handLength = 0f;
            return false;
        }
        wrist = hand.position;
        Vector3 toMiddle = middle.position - hand.position;
        handLength = toMiddle.magnitude;
        fingers = toMiddle.normalized;
        Vector3 a = index.position - little.position;
        Vector3 across = (a - Vector3.Dot(a, fingers) * fingers).normalized;
        palm = -Vector3.Cross(fingers, across).normalized;   // left hand: mirror of the right
        return true;
    }

    // where the left hand holds a two-handed weapon (its left hold, in the gun frame): wrist, fingers, palm
    public static void LeftHold(Weapon w, float handLength, out Vector3 wrist, out Vector3 fingers, out Vector3 palm)
    {
        // gun frame from the markers: right, up, forward (toward the muzzle)
        Vector3 grip = w.GripPoint.position;
        Vector3 fwd = (w.Muzzle.position - grip).normalized;
        Vector3 up = Vector3.ProjectOnPlane(w.UpHint.position - grip, fwd).normalized;
        Vector3 right = Vector3.Cross(up, fwd);
        Vector3 FromGun(Vector3 c) => right * c.x + up * c.y + fwd * c.z;

        palm = FromGun(w.LeftPalmDirection).normalized;
        fingers = Vector3.ProjectOnPlane(FromGun(w.LeftFingersDirection), palm).normalized;
        Vector3 palmCenter = grip + FromGun(w.LeftAxisPoint) - palm * w.LeftPalmDepth;
        wrist = palmCenter - fingers * (handLength * 0.5f);
    }

    // the left arm brought to a wrist position (two-bone IK) and the hand turned onto fingers / palm, weight 0 = the clip
    public static void ReachLeftHand(System.Func<HumanBodyBones, Transform> bone, Transform upper, Transform lower, Transform hand,
                                     Vector3 wantWrist, Vector3 wantFingers, Vector3 wantPalm, float weight)
    {
        if (upper == null || lower == null || hand == null || weight <= 0f) return;
        if (!TryLeftFrame(bone, out Vector3 wrist, out _, out _, out _)) return;
        SolveTwoBone(upper, lower, hand, Vector3.Lerp(wrist, wantWrist, weight));

        // hand orientation: current (fingers, palm) -> wanted, measured again after the arm moved
        if (!TryLeftFrame(bone, out _, out Vector3 fingers, out Vector3 palm, out _)) return;
        Quaternion fix = Quaternion.LookRotation(wantFingers, wantPalm) * Quaternion.Inverse(Quaternion.LookRotation(fingers, palm));
        hand.rotation = Quaternion.Slerp(Quaternion.identity, fix, weight) * hand.rotation;
    }

    // Two-bone IK in world space: keeps the elbow in the bend plane of the animation.
    public static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target)
    {
        // Smallest distance used (avoids degenerate angles).
        const float eps = 0.0001f;
        Vector3 a = upper.position, b = lower.position, c = end.position;
        float lab = (b - a).magnitude, lcb = (c - b).magnitude;
        float lat = Mathf.Clamp((target - a).magnitude, eps, lab + lcb - eps);

        float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
        float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
        float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axis = Vector3.Cross(c - a, b - a);
        if (axis.sqrMagnitude < 1e-10f) axis = Vector3.Cross(c - a, Vector3.up);
        axis.Normalize();

        upper.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis) * upper.rotation;
        lower.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis) * lower.rotation;
        upper.rotation = Quaternion.FromToRotation(end.position - a, target - a) * upper.rotation;
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
