using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Last Terminal/21: copies of her weapon clips adjusted to Helena 2 (Model2/Animations/<clip>_Helena2.anim) + a scene
// comparing each clip with its copy, both on Helena 2 (HelenaBaked.unity).
// Every frame of the clip is played on Helena 2 and on the old Helena (the body the clips were made for), then:
// 1) right hand: the fingers closed on the clip's weapon (its grip, WeaponGrips);
// 2) left hand:
//    - two-handed weapons (shotgun): on the forend, like WeaponHolder.PlaceLeftHand (not in reload clips);
//    - the others: where the old Helena has it relative to her right hand, while the clip brings the hands together.
//      Helena 2's shoulders are ~3.5 cm wider, so the same clip leaves her hands ~4 cm further apart.
// 3) the pose is read back as muscles (HumanPoseHandler); only the muscles that changed are written over the copy.
internal static class HelenaClipBaker
{
    private const string OutParent = "Assets/Characters/Helena/Model2";
    internal const string OutFolder = OutParent + "/Animations";
    internal const string Suffix = "_Helena2";
    private const string ScenePath = "Assets/Scenes/HelenaBaked.unity";
    private const float FarApart = 0.35f;    // old Helena's wrists further apart than this (m): the left hand keeps the clip
    private const float Together = 0.20f;    // closer than this: fully where the old Helena has it
    private const int SmoothFrames = 3;      // the left-hand weight averaged over +- this many frames (no pops)
    private const float Changed = 0.0001f;   // a muscle that moved less than this is left as it was

    private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

    [MenuItem("Last Terminal/21 - Helena 2: Bake Adjusted Weapon Clips (+ compare scene)")]
    private static void Bake()
    {
        var newModel = AssetDatabase.LoadAssetAtPath<GameObject>(HelenaCompareBuilder.ModelPath);
        var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(HelenaCompareBuilder.OldModelPath);
        if (newModel == null || oldModel == null) { Debug.LogError("[Bake] Helena2.fbx or HelenaIdle.fbx not found (menu 18 first)."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        WeaponGrips.ApplyAll();   // the grips the fingers close to
        HelenaSetupMenu.EnsureFolder(OutParent, "Animations");

        AnimationClip[] clips = HelenaCompareBuilder.Clips().Where(c => HelenaCompareBuilder.WeaponFor(c.name) >= 0 && !c.name.EndsWith(Suffix)).ToArray();
        var temp = new List<GameObject>();
        var baked = new List<AnimationClip>();
        try
        {
            Animator helena2 = Spawn(newModel, temp);
            Animator helena = Spawn(oldModel, temp);
            if (helena2 == null || helena == null) return;
            var weapons = new Weapon[HelenaCompareBuilder.ShownWeapons.Length];
            for (int w = 0; w < weapons.Length; w++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{HelenaCompareBuilder.WeaponPrefabFolder}/Weapon_{HelenaCompareBuilder.ShownWeapons[w]}.prefab");
                if (prefab == null) continue;
                GameObject go = Object.Instantiate(prefab);
                go.hideFlags = HideFlags.HideAndDontSave;
                temp.Add(go);
                weapons[w] = go.GetComponent<Weapon>();
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Helena 2: adjusting clips", clips[i].name, (float)i / clips.Length)) break;
                Weapon weapon = weapons[HelenaCompareBuilder.WeaponFor(clips[i].name)];
                if (weapon == null || !weapon.HasMarkers) { Debug.LogWarning($"[Bake] {clips[i].name}: weapon prefab missing, skipped."); continue; }
                AnimationClip copy = BakeClip(clips[i], helena2, helena, weapon);
                if (copy != null) baked.Add(copy);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            foreach (GameObject go in temp) Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        if (baked.Count == 0) return;

        AnimationClip[] originals = baked.Select(b => clips.First(c => c.name + Suffix == b.name)).ToArray();
        HelenaCompareBuilder.BuildScene(ScenePath, newModel, "Original", newModel, "Ajustada", originals, baked.ToArray(), false);
        Debug.Log($"[Bake] {baked.Count} clips in {OutFolder}. Compare: {ScenePath} (Play).");
    }

    private static Animator Spawn(GameObject model, List<GameObject> temp)
    {
        GameObject go = Object.Instantiate(model);
        go.hideFlags = HideFlags.HideAndDontSave;
        temp.Add(go);
        Animator a = go.GetComponent<Animator>();
        if (a == null || a.avatar == null || !a.avatar.isHuman) { Debug.LogError($"[Bake] {model.name} has no Humanoid avatar."); return null; }
        a.runtimeAnimatorController = null;
        a.applyRootMotion = false;
        a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return a;
    }

    // ------------------------------------------------------------------ one clip
    private static AnimationClip BakeClip(AnimationClip clip, Animator helena2, Animator helena, Weapon weapon)
    {
        int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
        var times = new float[frames + 1];
        for (int f = 0; f <= frames; f++) times[f] = Mathf.Min(f / clip.frameRate, clip.length);
        bool forend = weapon.IsTwoHanded && !clip.name.Contains("Reload");
        bool aim = HelenaCompareBuilder.IsAim(clip.name);

        // 1) the old Helena: her left hand in her right-hand frame, and how close the hands are
        var relWrist = new Vector3[times.Length];
        var relFingers = new Vector3[times.Length];
        var relPalm = new Vector3[times.Length];
        var oldHand = new float[times.Length];
        var weight = new float[times.Length];
        using (var sampler = new Sampler(helena, clip))
        {
            for (int f = 0; f < times.Length; f++)
            {
                sampler.Pose(times[f]);
                if (!WeaponGrip.TryRightFrame(helena, out Vector3 o, out Vector3 x, out Vector3 y, out Vector3 z)) continue;
                if (!WeaponGrip.TryLeftFrame(helena.GetBoneTransform, out Vector3 wrist, out Vector3 fingers, out Vector3 palm, out float length)) continue;
                relWrist[f] = InFrame(wrist - o, x, y, z);
                relFingers[f] = InFrame(fingers, x, y, z);
                relPalm[f] = InFrame(palm, x, y, z);
                oldHand[f] = length;
                float apart = (wrist - helena.GetBoneTransform(HumanBodyBones.RightHand).position).magnitude;
                weight[f] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FarApart, Together, apart));
            }
        }
        weight = Smooth(weight, SmoothFrames);

        // 2) Helena 2: the corrections, read back as muscle changes
        Transform upper = helena2.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform lower = helena2.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform hand = helena2.GetBoneTransform(HumanBodyBones.LeftHand);
        WeaponGrip.GetFingers(helena2.GetBoneTransform, true, out Transform[] rightJoints, out Transform[] rightEnds);
        WeaponGrip.GetFingers(helena2.GetBoneTransform, false, out Transform[] leftJoints, out Transform[] leftEnds);
        var delta = new float[HumanTrait.MuscleCount][];
        for (int m = 0; m < delta.Length; m++) delta[m] = new float[times.Length];
        float maxMove = 0f, maxWeight = 0f;
        using (var sampler = new Sampler(helena2, clip))
        using (var handler = new HumanPoseHandler(helena2.avatar, helena2.transform))
        {
            var before = new HumanPose();
            var after = new HumanPose();
            for (int f = 0; f < times.Length; f++)
            {
                sampler.Pose(times[f]);
                handler.GetHumanPose(ref before);
                if (!WeaponGrip.TryRightFrame(helena2, out Vector3 o, out Vector3 x, out Vector3 y, out Vector3 z)) continue;
                WeaponGrip.TryLeftFrame(helena2.GetBoneTransform, out Vector3 wrist0, out _, out _, out float length);

                if (forend)
                {
                    WeaponGrip.PlaceInRightHand(weapon, o, x, y, z, aim ? 1f : 0f);
                    WeaponGrip.LeftHold(weapon, length, out Vector3 wrist, out Vector3 fingers, out Vector3 palm);
                    WeaponGrip.ReachLeftHand(helena2.GetBoneTransform, upper, lower, hand, wrist, fingers, palm, 1f);
                    if (WeaponGrip.TryLeftFrame(helena2.GetBoneTransform, out _, out Vector3 lf, out Vector3 lp, out _))
                        WeaponGrip.ApplyFingers(leftJoints, leftEnds, weapon.LeftFingers, lf, Vector3.Cross(lf, lp).normalized, lp, 1f);
                    maxWeight = 1f;
                }
                else if (weight[f] > 0f && oldHand[f] > 0f)
                {
                    float scale = length / oldHand[f];
                    Vector3 wrist = o + FromFrame(relWrist[f], x, y, z) * scale;
                    WeaponGrip.ReachLeftHand(helena2.GetBoneTransform, upper, lower, hand, wrist,
                                             FromFrame(relFingers[f], x, y, z), FromFrame(relPalm[f], x, y, z), weight[f]);
                    maxWeight = Mathf.Max(maxWeight, weight[f]);
                }
                WeaponGrip.ApplyFingers(rightJoints, rightEnds, weapon.RightFingers, x, y, z, 1f);
                if (hand != null) maxMove = Mathf.Max(maxMove, (hand.position - wrist0).magnitude);

                handler.GetHumanPose(ref after);
                for (int m = 0; m < delta.Length; m++) delta[m][f] = after.muscles[m] - before.muscles[m];
            }
        }

        // 3) the copy: the clip with the changed muscles rewritten (clip value + change)
        var byMuscle = new Dictionary<string, EditorCurveBinding>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            if (b.type == typeof(Animator) && string.IsNullOrEmpty(b.path)) byMuscle[MuscleOf(b.propertyName)] = b;

        if (byMuscle.Count == 0) { Debug.LogError($"[Bake] {clip.name}: no muscle curves (is its file Humanoid?), skipped."); return null; }

        AnimationClip copy = Object.Instantiate(clip);
        copy.name = clip.name + Suffix;
        AnimationUtility.SetAnimationClipSettings(copy, AnimationUtility.GetAnimationClipSettings(clip));
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        for (int m = 0; m < delta.Length; m++)
        {
            if (delta[m].Max(v => Mathf.Abs(v)) < Changed) continue;
            string muscle = HumanTrait.MuscleName[m];
            if (!byMuscle.TryGetValue(muscle, out EditorCurveBinding binding))
                binding = EditorCurveBinding.FloatCurve("", typeof(Animator), PropertyOf(muscle));
            AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
            var keys = new Keyframe[times.Length];
            for (int f = 0; f < times.Length; f++)
                keys[f] = new Keyframe(times[f], (source != null ? source.Evaluate(times[f]) : 0f) + delta[m][f]);
            var curve = new AnimationCurve(keys);
            for (int k = 0; k < keys.Length; k++) curve.SmoothTangents(k, 0f);
            bindings.Add(binding);
            curves.Add(curve);
        }
        AnimationUtility.SetEditorCurves(copy, bindings.ToArray(), curves.ToArray());

        string path = $"{OutFolder}/{copy.name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(copy, existing);   // same asset (GUID): references to it stay
            Object.DestroyImmediate(copy);
            copy = existing;
            EditorUtility.SetDirty(copy);
        }
        else AssetDatabase.CreateAsset(copy, path);

        Debug.Log($"[Bake] {copy.name}: {times.Length} frames, {bindings.Count} muscles changed, left hand " +
                  (forend ? "on the forend" : maxWeight > 0f ? $"moved up to {maxMove * 100f:0.0} cm (weight {maxWeight:0.00})" : "as the clip"));
        return copy;
    }

    // ------------------------------------------------------------------ helpers
    private static Vector3 InFrame(Vector3 v, Vector3 x, Vector3 y, Vector3 z) => new Vector3(Vector3.Dot(v, x), Vector3.Dot(v, y), Vector3.Dot(v, z));
    private static Vector3 FromFrame(Vector3 c, Vector3 x, Vector3 y, Vector3 z) => x * c.x + y * c.y + z * c.z;

    private static float[] Smooth(float[] v, int radius)
    {
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            float sum = 0f; int n = 0;
            for (int j = Mathf.Max(0, i - radius); j <= Mathf.Min(v.Length - 1, i + radius); j++) { sum += v[j]; n++; }
            r[i] = Mathf.Max(v[i], sum / n);   // never less than the frame itself: the hands still meet where the clip has them together
        }
        return r;
    }

    // a muscle name (HumanTrait) from a clip property: "LeftHand.Index.1 Stretched" -> "Left Index 1 Stretched"
    private static string MuscleOf(string property) => property.Replace("LeftHand.", "Left ").Replace("RightHand.", "Right ").Replace('.', ' ');

    // and back, for a muscle the clip doesn't animate yet
    private static string PropertyOf(string muscle)
    {
        string[] w = muscle.Split(' ');
        if (w.Length >= 3 && FingerNames.Contains(w[1])) return $"{w[0]}Hand.{w[1]}.{string.Join(" ", w, 2, w.Length - 2)}";
        return muscle;
    }

    // a clip played on a model at any time (edit mode), through a playable graph evaluated by hand
    private sealed class Sampler : System.IDisposable
    {
        private PlayableGraph graph;
        private AnimationClipPlayable player;

        public Sampler(Animator animator, AnimationClip clip)
        {
            graph = PlayableGraph.Create("HelenaClipBaker");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            player = AnimationClipPlayable.Create(graph, clip);
            player.SetApplyFootIK(true);   // like ModelCompare
            output.SetSourcePlayable(player);
            graph.Play();
            player.Pause();
        }

        public void Pose(float time)
        {
            player.SetTime(time);
            graph.Evaluate(0f);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
