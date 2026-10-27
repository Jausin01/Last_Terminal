using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Test scene report (ModelCompare, key P): what Unity really did on this frame, for Claude to reproduce outside Unity.
// For each model: every Humanoid bone (world / local position and rotation) as the clip posed it ("anim") and after the
// weapon grip ran ("final"), the finger tips, the hand frame, the weapon (transform, markers, hold, grip data).
// Written to <project>/PoseReports/<clip>_<time>.json.
public static class PoseReport
{
    // One bone: world position / rotation and local position / rotation.
    [Serializable] public class Bone { public string name; public Vector3 p; public Quaternion r; public Vector3 lp; public Quaternion lr; }

    // Everything about one model on that frame.
    [Serializable] public class Model
    {
        // Label, Humanoid or not, avatar name, root pose.
        public string label;
        public bool isHuman;
        public string avatar;
        public Vector3 rootPos; public Quaternion rootRot;
        // Bones as the clip posed them and after the grip ran.
        public Bone[] anim;
        public Bone[] final;
        // Hand frame (from the fingers).
        public bool fingersFound;
        public Vector3 frameOrigin, frameFingers, frameAcross, frameNormal;
        // Weapon: name, active, pose, its markers, aim, hold data and fingers closed.
        public string weapon;
        public bool weaponActive;
        public Vector3 weaponPos; public Quaternion weaponRot;
        public Vector3 gripPoint, muzzle, upHint;
        public float aim;
        public Vector3 holdGrip, holdBarrel, holdUp;
        public int rightFingers, leftFingers;
    }

    // The report file: clip, time and the models.
    [Serializable] public class Report { public string clip; public float time; public Model[] models; }

    // Every Humanoid bone of an Animator.
    public static Bone[] Snapshot(Animator a)
    {
        var list = new List<Bone>();
        foreach (HumanBodyBones b in Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (b == HumanBodyBones.LastBone) continue;
            Transform t = a.GetBoneTransform(b);
            if (t == null) continue;
            list.Add(Make(b.ToString(), t));
            string n = b.ToString();
            if (n.EndsWith("Distal") && t.childCount > 0) list.Add(Make(n + "_tip", t.GetChild(0)));
        }
        return list.ToArray();
    }

    // One bone entry.
    private static Bone Make(string name, Transform t) =>
        new Bone { name = name, p = t.position, r = t.rotation, lp = t.localPosition, lr = t.localRotation };

    // Describes a model and its weapon.
    public static Model Describe(string label, Animator a, Bone[] anim, bool fingersFound, Weapon w, float aim)
    {
        var m = new Model
        {
            label = label, isHuman = a.isHuman, avatar = a.avatar != null ? a.avatar.name : "",
            rootPos = a.transform.position, rootRot = a.transform.rotation,
            anim = anim, final = Snapshot(a), fingersFound = fingersFound, aim = aim,
        };
        if (WeaponGrip.TryRightFrame(a, out Vector3 o, out Vector3 f, out Vector3 ac, out Vector3 n))
        {
            m.frameOrigin = o; m.frameFingers = f; m.frameAcross = ac; m.frameNormal = n;
        }
        if (w != null)
        {
            m.weapon = w.name; m.weaponActive = w.gameObject.activeInHierarchy;
            m.weaponPos = w.transform.position; m.weaponRot = w.transform.rotation;
            if (w.HasMarkers) { m.gripPoint = w.GripPoint.position; m.muzzle = w.Muzzle.position; m.upHint = w.UpHint.position; }
            w.GetHold(aim, out m.holdGrip, out m.holdBarrel, out m.holdUp);
            m.rightFingers = w.RightFingers != null ? w.RightFingers.Length : -1;
            m.leftFingers = w.LeftFingers != null ? w.LeftFingers.Length : -1;
        }
        return m;
    }

    // Writes the report JSON; returns its path.
    public static string Write(Report report)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PoseReports"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"{report.clip}_{report.time:0.00}.json".Replace(',', '.'));
        File.WriteAllText(file, JsonUtility.ToJson(report, true));
        return file;
    }
}
