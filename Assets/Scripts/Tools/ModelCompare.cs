using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

// Test scene (Last Terminal/18): the old and the new Helena side by side playing the same clip at the same time,
// every clip of hers one after the other. Right / Left = next / previous clip, Space = pause the automatic advance,
// Q / E = turn both, R = facing the camera, Up / Down = speed, P = report of this frame (PoseReport, for Claude).
// Weapon clips show the weapon in her hand like the game does (WeaponGrip: placement + fingers closed on it).
// Last Terminal/21 uses it to compare a clip with its adjusted copy: the last model plays lastModelClips, nothing is
// changed after the clips (only the weapon placed), so what you see is what the clips do.
public class ModelCompare : MonoBehaviour
{
    [SerializeField] private Animator[] animators;
    [SerializeField] private string[] labels;
    [SerializeField] private AnimationClip[] clips;
    [SerializeField] private Camera view;
    [SerializeField] private float minHold = 2.5f;     // a short clip still shows this long (it loops / holds its end)
    [SerializeField] private float turnSpeed = 120f;   // degrees per second (Q / E)
    [Header("Weapons (display copies, model k uses weapons[k * weaponCount + w])")]
    [SerializeField] private Weapon[] weapons = new Weapon[0];
    [SerializeField] private int weaponCount;
    [SerializeField] private int[] clipWeapon = new int[0];    // per clip: weapon index, -1 = none
    [SerializeField] private bool[] clipAim = new bool[0];     // per clip: aim hold (else idle hold)
    [SerializeField] private AnimationClip[] lastModelClips = new AnimationClip[0];   // per clip: what the last model plays instead (empty = the same)
    [SerializeField] private bool closeFingers = true;         // fingers closed on the weapon after the clip (the game's WeaponHolder)

    private PlayableGraph[] graphs;
    private AnimationClipPlayable[] players;
    private float[] startYaw;
    private int index;
    private float time;
    private float speed = 1f;
    private bool auto = true;
    private Transform[][] fingerJoints;
    private Transform[][] fingerEnds;
    private bool reportPending;

    private void Start()
    {
        if (animators == null || animators.Length == 0 || clips == null || clips.Length == 0) { enabled = false; return; }
        graphs = new PlayableGraph[animators.Length];
        players = new AnimationClipPlayable[animators.Length];
        startYaw = new float[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].applyRootMotion = false;   // they stay on their marks (seat / door clips travel)
            startYaw[i] = animators[i].transform.eulerAngles.y;
        }
        fingerJoints = new Transform[animators.Length][];
        fingerEnds = new Transform[animators.Length][];
        for (int i = 0; i < animators.Length; i++)
            if (WeaponGrip.GetFingers(animators[i].GetBoneTransform, true, out Transform[] j, out Transform[] e)) { fingerJoints[i] = j; fingerEnds[i] = e; }
        Play(0);
    }

    // after the clip pose: the clip's weapon in each one's right hand, fingers closed on it
    private void LateUpdate()
    {
        if (fingerJoints == null) return;
        int wi = index < clipWeapon.Length ? clipWeapon[index] : -1;
        bool aim = index < clipAim.Length && clipAim[index];
        PoseReport.Bone[][] before = null;
        if (reportPending)
        {
            before = new PoseReport.Bone[animators.Length][];
            for (int k = 0; k < animators.Length; k++) before[k] = PoseReport.Snapshot(animators[k]);
        }
        for (int k = 0; k < animators.Length; k++)
        {
            for (int w = 0; w < weaponCount; w++)
            {
                Weapon weapon = Slot(k, w);
                if (weapon != null && weapon.gameObject.activeSelf != (w == wi)) weapon.gameObject.SetActive(w == wi);
            }
            Weapon held = wi >= 0 ? Slot(k, wi) : null;
            if (held == null || !WeaponGrip.TryRightFrame(animators[k], out Vector3 o, out Vector3 f, out Vector3 a, out Vector3 n)) continue;
            WeaponGrip.PlaceInRightHand(held, o, f, a, n, aim ? 1f : 0f);
            if (closeFingers) WeaponGrip.ApplyFingers(fingerJoints[k], fingerEnds[k], held.RightFingers, f, a, n, 1f);
        }
        if (before == null) return;
        reportPending = false;
        var report = new PoseReport.Report { clip = clips[index].name, time = time, models = new PoseReport.Model[animators.Length] };
        for (int k = 0; k < animators.Length; k++)
            report.models[k] = PoseReport.Describe(k < labels.Length ? labels[k] : animators[k].name, animators[k], before[k],
                                                   fingerJoints[k] != null, wi >= 0 ? Slot(k, wi) : null, aim ? 1f : 0f);
        Debug.Log("[Compare] Report: " + PoseReport.Write(report));
    }

    private Weapon Slot(int model, int weapon)
    {
        int i = model * weaponCount + weapon;
        return i >= 0 && i < weapons.Length ? weapons[i] : null;
    }

    private void OnDestroy()
    {
        if (graphs == null) return;
        foreach (PlayableGraph g in graphs)
            if (g.IsValid()) g.Destroy();
    }

    private void Update()
    {
        if (clips == null || clips.Length == 0) return;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame) Play(index + 1);
            if (kb.leftArrowKey.wasPressedThisFrame) Play(index - 1);
            if (kb.spaceKey.wasPressedThisFrame) auto = !auto;
            if (kb.pKey.wasPressedThisFrame) reportPending = true;
            if (kb.upArrowKey.wasPressedThisFrame) speed = Mathf.Min(speed * 2f, 2f);
            if (kb.downArrowKey.wasPressedThisFrame) speed = Mathf.Max(speed * 0.5f, 0.125f);
            float turn = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            for (int i = 0; i < animators.Length; i++)
            {
                Transform t = animators[i].transform;
                if (turn != 0f) t.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f, Space.World);
                if (kb.rKey.wasPressedThisFrame) t.rotation = Quaternion.Euler(0f, startYaw[i], 0f);
            }
        }

        // same time on every model; a clip that doesn't loop holds its last frame
        AnimationClip clip = clips[index];
        time += Time.deltaTime * speed;
        double at = clip.isLooping ? time % Mathf.Max(clip.length, 0.01f) : Mathf.Min(time, clip.length);
        for (int i = 0; i < players.Length; i++)
            if (players[i].IsValid()) players[i].SetTime(at);

        float hold = Mathf.Max(clip.isLooping ? clip.length * 2f : clip.length + 0.75f, minHold);
        if (auto && time >= hold) Play(index + 1);
    }

    private void Play(int i)
    {
        index = (i % clips.Length + clips.Length) % clips.Length;
        time = 0f;
        for (int k = 0; k < animators.Length; k++)
        {
            if (graphs[k].IsValid()) graphs[k].Destroy();
            players[k] = AnimationPlayableUtilities.PlayClip(animators[k], ClipOf(k), out graphs[k]);
            players[k].SetApplyFootIK(true);
            players[k].Pause();   // the time is set by hand (both in step)
        }
    }

    private AnimationClip ClipOf(int model)
    {
        bool last = model == animators.Length - 1 && index < lastModelClips.Length && lastModelClips[index] != null;
        return last ? lastModelClips[index] : clips[index];
    }

    private void OnGUI()
    {
        if (clips == null || clips.Length == 0) return;
        AnimationClip clip = clips[index];
        var big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        var small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        GUI.Label(new Rect(20, 14, 900, 30), $"{index + 1} / {clips.Length}   {clip.name}", big);
        int wi = index < clipWeapon.Length ? clipWeapon[index] : -1;
        Weapon shown = wi >= 0 ? Slot(0, wi) : null;
        GUI.Label(new Rect(20, 44, 900, 24),
                  $"{clip.length:0.00} s{(clip.isLooping ? "  (loop)" : "")}   speed x{speed:0.###}   {(auto ? "auto" : "PAUSED (Space)")}" +
                  (shown != null ? $"   weapon: {shown.name}" : ""), small);
        GUI.Label(new Rect(20, Screen.height - 34, 1200, 24),
                  "Right / Left = clip   Space = auto on/off   Q / E = turn   R = reset   Up / Down = speed   P = report", small);
        if (view == null || labels == null) return;
        for (int k = 0; k < animators.Length && k < labels.Length; k++)
        {
            Vector3 p = view.WorldToScreenPoint(animators[k].transform.position + Vector3.up * 2.05f);
            if (p.z <= 0f) continue;
            GUI.Label(new Rect(p.x - 100, Screen.height - p.y, 200, 30), labels[k], new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });
        }
    }

#if UNITY_EDITOR
    public void EditorSet(Animator[] models, string[] names, AnimationClip[] list, Camera cam,
                          Weapon[] displayWeapons, int perModel, int[] weaponOfClip, bool[] aimOfClip,
                          AnimationClip[] lastClips = null, bool fingers = true)
    {
        animators = models; labels = names; clips = list; view = cam;
        weapons = displayWeapons; weaponCount = perModel; clipWeapon = weaponOfClip; clipAim = aimOfClip;
        lastModelClips = lastClips ?? new AnimationClip[0]; closeFingers = fingers;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
