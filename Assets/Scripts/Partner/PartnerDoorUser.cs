using System;
using UnityEngine;
using UnityEngine.AI;

// What she was asked to do with a door.
public enum DoorAction { PassThrough, Open, Close }
// How it went.
public enum DoorResult { Done, Locked, NeedsOperator, Unlocked, Cancelled }

// Her side of a door, with the three Mixamo door clips:
//   push side (the leaf opens away from her)  -> HelenaOpenDoorInwards: pushes it open, walks through, closes it behind her
//   pull side (the leaf opens toward her)     -> HelenaOpenDoorOutwards: pulls it open, slips through, it closes behind her
//   locked / operator-only door                -> HelenaTryToOpenLockedDoor (the leaf rattles)
// PassThrough plays the whole clip. Open plays it up to where the door is open and she is still on her side.
// Close plays that same part BACKWARDS (negative state speed): the opening becomes a closing.
// The leaf follows her hand: a curve (leaf angle by clip time, measured from her hands in the files) drives the Door.
// Walks to the clip's start pose (NavMesh, then a short scripted walk), faces the door, then the clip moves her (root motion).
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerDoorUser : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CombatField = nameof(combat);

    // Her animator (door clips), interactor and combat (stopped first), and the short scripted walk to the start pose.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private ScriptedWalker walker = new ScriptedWalker();
    [Tooltip("The NavMesh walk hands over to the scripted walk this close to the start pose (m).")]
    [SerializeField] private float handoffDistance = 0.6f;

    [Header("Clip times (normalized, measured on the Mixamo files)")]
    [Tooltip("Inwards: door open (~76 deg), she is in the doorway. Open stops here; Close starts here backwards.")]
    [SerializeField, Range(0f, 1f)] private float pushOpenTime = 0.628f;
    [Tooltip("Inwards backwards: her hand has left the handle.")]
    [SerializeField, Range(0f, 1f)] private float pushClosedTime = 0.188f;
    [Tooltip("Outwards: door pulled open (~45 deg), she stepped aside. Open stops here; Close starts here backwards.")]
    [SerializeField, Range(0f, 1f)] private float pullOpenTime = 0.463f;
    // Outwards backwards: her hand has left the handle.
    [SerializeField, Range(0f, 1f)] private float pullClosedTime = 0.02f;
    [Tooltip("Pass through: she is standing on the other side.")]
    [SerializeField, Range(0f, 1f)] private float passEndTime = 0.985f;
    // Locked: the try ends here.
    [SerializeField, Range(0f, 1f)] private float lockedEndTime = 0.95f;
    [Tooltip("Closing: part of the clip during which the leaf comes from where it is to her hand.")]
    [SerializeField, Range(0.01f, 0.5f)] private float closeCatchSpan = 0.12f;
    [Tooltip("Blend back to standing after the clip (s).")]
    [SerializeField] private float exitTime = 0.3f;
    // Max time for the scripted walk-in (s).
    [SerializeField] private float enterTimeout = 1.5f;

    // Leaf angle (deg) by frame of the inwards (push) clip.
    [Header("Leaf angle (deg) by clip time")]
    [SerializeField] private AnimationCurve inwardsLeaf = FramesCurve(192,
        1, 0, 79, 0, 82, 5, 85, 11, 88, 18, 91, 22, 94, 26, 97, 28, 100, 31, 103, 34, 106, 38, 109, 42, 112, 50, 115, 60,
        118, 69, 121, 76, 124, 78, 127, 79, 130, 76, 133, 73, 136, 70, 139, 64, 142, 60, 145, 58, 148, 55, 151, 49, 154, 42,
        157, 32, 160, 23, 163, 18, 166, 15, 169, 14, 172, 9, 175, 3, 177, 0, 192, 0);
    // Leaf angle (deg) by frame of the outwards (pull) clip.
    [SerializeField] private AnimationCurve outwardsLeaf = FramesCurve(124,
        1, 0, 20, 0, 22, 1, 25, 5, 28, 11, 31, 17, 34, 23, 37, 28, 40, 33, 43, 37, 46, 40, 49, 42, 52, 44, 55, 45, 64, 45,
        67, 43, 70, 40, 73, 37, 76, 34, 79, 31, 82, 26, 85, 18, 88, 10, 91, 3, 93, 0, 124, 0);

    // Part of the clip the leaf rattles in, how far (deg) and how fast (Hz).
    [Header("Locked: the leaf rattles while she pulls the handle")]
    [SerializeField] private Vector2 rattleWindow = new Vector2(0.115f, 0.69f);
    [SerializeField] private float rattleAngle = 0.8f;
    [SerializeField] private float rattleFrequency = 9f;

    // Where she stands for each clip (pass / close / locked, push and pull side).
    [Header("Start poses (door space from the hinge: x toward the handle, z toward the side the leaf opens to)")]
    [SerializeField] private Vector2 pushStart = new Vector2(0.50f, -0.72f);
    [SerializeField] private Vector2 pullStart = new Vector2(0.37f, 0.88f);
    [SerializeField] private Vector2 pushCloseStart = new Vector2(0.74f, 0.47f);
    [SerializeField] private Vector2 pullCloseStart = new Vector2(0.95f, 0.72f);
    [SerializeField] private Vector2 lockedPush = new Vector2(0.92f, -0.60f);
    [SerializeField] private Vector2 lockedPull = new Vector2(0.76f, 0.60f);
    [Tooltip("Sliding (security) doors: distance from the door center where she tries it.")]
    [SerializeField] private float slidingTryDistance = 0.6f;

    // Steps of a door use.
    private enum Step { None, Approach, WalkIn, Face, Play, Exit }

    // Her agent, the step, the door, the action and its result, the clip with its time range and direction, the start pose, a key to use, walk-in done, leaf driven by her, clip time, the leaf angle at the start and a timer.
    private NavMeshAgent agent;
    private Step step;
    private Door door;
    private DoorAction action;
    private DoorResult result;
    private PartnerAnimator.DoorClip clip;
    private bool backward;
    private float startTime, endTime;
    private Vector3 posePosition;
    private Vector3 poseForward;
    private KeyItem keyToUse;
    private bool entered;
    private bool driving;
    private float clipTime;
    private float leafStart;
    private float timer;

    // Raised when she starts / ends a door action.
    public event Action<Door, DoorAction> Started;
    public event Action<Door, DoorAction, DoorResult> Finished;

    // She is using a door.
    public bool IsBusy => step != Step.None;
    // still walking there on the NavMesh: another command may send her elsewhere (cancels the door)
    public bool IsInterruptible => step == Step.None || step == Step.Approach;
    public Door CurrentDoor => door;

    // Gets the agent; checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || interactor == null || combat == null)
        {
            Debug.LogError($"{name}: PartnerDoorUser needs PartnerAnimator, PartnerInteractor and PartnerCombat.", this);
            enabled = false;
            return;
        }
        walker.Bind(transform, partnerAnimator);
    }

    // PassThrough / Open a closed door, Close an open one. False when there is nothing she can do with it from here
    // (open door asked to open, remote door asked to close...): the caller can just walk her there.
    public bool Use(Door target, DoorAction wanted)
    {
        if (!enabled || IsBusy || target == null) return false;
        if (partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp || partnerAnimator.IsScripted) return false;
        if (!Plan(target, wanted)) return false;
        if (!NavMesh.SamplePosition(posePosition, out NavMeshHit hit, 0.3f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"{target.name}: where she stands to use this door is not on the NavMesh.", target);
            return false;
        }
        combat.Holster();
        if (!interactor.MoveTo(hit.position)) return false;   // also drops any pickup she was walking to
        door = target;
        action = wanted;
        step = Step.Approach;
        Started?.Invoke(door, action);
        return true;
    }

    // Picks the clip, its time range, the start pose and the outcome for her side of the door.
    private bool Plan(Door target, DoorAction wanted)
    {
        Transform t = target.transform;
        bool pullSide = t.InverseTransformPoint(transform.position).z > 0f;   // on the side the leaf opens toward
        keyToUse = null;
        result = DoorResult.Done;

        if (!target.IsHinged || target.IsRemoteControlled)
        {
            // operator's door: she can only try it (and report it)
            if (target.IsOpen || wanted == DoorAction.Close) return false;
            float z = pullSide ? slidingTryDistance : -slidingTryDistance;
            SetPose(t, t.TransformPoint(new Vector3(0f, 0f, z)), pullSide);
            SetClip(PartnerAnimator.DoorClip.Locked, 0f, lockedEndTime, false);
            result = DoorResult.NeedsOperator;
            return true;
        }

        if (wanted == DoorAction.Close)
        {
            if (!target.IsOpen) return false;
            SetPose(t, HingePoint(target, pullSide ? pullCloseStart : pushCloseStart), pullSide);
            if (pullSide) SetClip(PartnerAnimator.DoorClip.Outwards, pullOpenTime, pullClosedTime, true);
            else SetClip(PartnerAnimator.DoorClip.Inwards, pushOpenTime, pushClosedTime, true);
            return true;
        }

        if (target.IsOpen) return false;
        if (target.IsLocked)
        {
            KeyItem key = target.RequiredKey;
            if (key == null || interactor.Inventory == null || !interactor.Inventory.Has(key))
            {
                SetPose(t, HingePoint(target, pullSide ? lockedPull : lockedPush), pullSide);
                SetClip(PartnerAnimator.DoorClip.Locked, 0f, lockedEndTime, false);
                result = DoorResult.Locked;
                return true;
            }
            keyToUse = key;
            result = DoorResult.Unlocked;
        }

        SetPose(t, HingePoint(target, pullSide ? pullStart : pushStart), pullSide);
        bool pass = wanted == DoorAction.PassThrough;
        if (pullSide) SetClip(PartnerAnimator.DoorClip.Outwards, 0f, pass ? passEndTime : pullOpenTime, false);
        else SetClip(PartnerAnimator.DoorClip.Inwards, 0f, pass ? passEndTime : pushOpenTime, false);
        return true;
    }

    // A start pose point in world space from a hinge offset.
    private static Vector3 HingePoint(Door target, Vector2 offset)
    {
        Vector3 hinge = target.HingeLocal;
        return target.transform.TransformPoint(new Vector3(hinge.x + offset.x, 0f, offset.y));
    }

    // she faces the door: +Z of the door from the push side, -Z from the pull side
    private void SetPose(Transform doorTransform, Vector3 position, bool pullSide)
    {
        posePosition = position;
        Vector3 f = doorTransform.forward * (pullSide ? -1f : 1f);
        poseForward = new Vector3(f.x, 0f, f.z).normalized;
    }

    // Chooses the clip, the part of it played and the direction.
    private void SetClip(PartnerAnimator.DoorClip c, float from, float to, bool reverse)
    {
        clip = c;
        startTime = from;
        endTime = to;
        backward = reverse;
    }

    // Runs the current step.
    private void Update()
    {
        switch (step)
        {
            case Step.Approach:
                UpdateApproach();
                break;
            case Step.WalkIn:
                if (walker.WalkTo(posePosition)) { step = Step.Face; walker.ResetTurn(); }
                break;
            case Step.Face:
                if (walker.TurnTo(poseForward)) StartClip();
                break;
            case Step.Play:
                UpdatePlay();
                break;
            case Step.Exit:
                timer -= Time.deltaTime;
                if (timer <= 0f) Finish();
                break;
        }
    }

    // NavMesh walk to the start pose, then the scripted walk-in.
    private void UpdateApproach()
    {
        // another command sent her somewhere else: give up
        if ((Flat(agent.destination) - Flat(posePosition)).sqrMagnitude > 0.25f) { Cancel(); return; }
        if (agent.pathPending || partnerAnimator.IsTurning) return;

        float dist = (Flat(transform.position) - Flat(posePosition)).magnitude;
        bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f;
        if (dist > handoffDistance && !arrived) return;

        // hand over without stopping: the scripted walk continues at her current speed
        float speed = Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude;
        combat.Holster();   // in case she was told to draw on the way
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        partnerAnimator.BeginScripted();
        walker.Begin(speed);
        step = Step.WalkIn;
    }

    // Faces the door and plays the clip; the leaf is driven by her hand.
    private void StartClip()
    {
        // the door may have changed while she walked (operator, another command...)
        bool stillValid = action == DoorAction.Close ? door.IsOpen : !door.IsOpen;
        if (!stillValid) { EndScriptedHere(); Done(DoorResult.Cancelled); return; }

        if (keyToUse != null)
        {
            if (door.TryUnlock(keyToUse) && keyToUse.ConsumeOnUse) interactor.Inventory.TryRemove(keyToUse, 1);
            keyToUse = null;
        }

        walker.Stop();
        driving = door.BeginManual();
        leafStart = driving ? door.LeafAngle : 0f;
        clipTime = startTime;
        entered = false;
        timer = 0f;
        partnerAnimator.SetScriptedRootMotion(true);
        partnerAnimator.PlayDoorClip(clip, startTime, backward);
        step = Step.Play;
    }

    // Advances the clip; the leaf follows the curve (or rattles when locked).
    private void UpdatePlay()
    {
        timer += Time.deltaTime;
        if (!partnerAnimator.TryGetDoorTime(out float t))
        {
            // not in the door state yet (first frame) or kicked out of it
            if (entered || timer > enterTimeout) StopClip();
            return;
        }
        entered = true;
        clipTime = t;
        if (backward ? t <= endTime : t >= endTime) StopClip();
    }

    // after the Animator: the leaf follows her hand
    private void LateUpdate()
    {
        if (step != Step.Play || !driving || !entered) return;
        if (clip == PartnerAnimator.DoorClip.Locked)
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(Mathf.InverseLerp(rattleWindow.x, rattleWindow.y, clipTime)));
            float shake = Mathf.Abs(Mathf.Sin(Time.time * rattleFrequency * Mathf.PI));
            door.SetManualAngle(rattleAngle * env * shake);
            return;
        }
        door.SetManualAngle(LeafAngle(clipTime));
    }

    // Leaf angle at a clip time (closing: catches the leaf from where it is).
    private float LeafAngle(float t)
    {
        float angle = Mathf.Max(0f, (clip == PartnerAnimator.DoorClip.Inwards ? inwardsLeaf : outwardsLeaf).Evaluate(t));
        if (!backward) return angle;
        // closing: the leaf comes from where it is to her hand during the first part
        float played = Mathf.InverseLerp(startTime, endTime, t);
        return Mathf.Lerp(leafStart, angle, Mathf.SmoothStep(0f, 1f, played / closeCatchSpan));
    }

    // Releases the leaf and blends back to standing.
    private void StopClip()
    {
        if (driving)
        {
            // Open lets the leaf carry on to fully open; PassThrough / Close / Locked leave it closed
            door.EndManual(action == DoorAction.Open && clip != PartnerAnimator.DoorClip.Locked);
            driving = false;
        }
        partnerAnimator.EndDoorClip();   // root motion stays on during the blend: she settles instead of stopping dead
        timer = exitTime;
        step = Step.Exit;
    }

    // Back to the agent with the result.
    private void Finish()
    {
        EndScriptedHere();
        Done(result);
    }

    // Stops at once and reports Cancelled.
    private void Cancel()
    {
        if (!IsBusy) return;
        if (driving) { door.EndManual(door.IsOpen); driving = false; }
        if (partnerAnimator.IsScripted)
        {
            if (step == Step.Play || step == Step.Exit) partnerAnimator.SnapToLocomotion();
            EndScriptedHere();
        }
        Done(DoorResult.Cancelled);
    }

    // Ends at once, standing where she is (e.g. a save is loaded).
    public void Abort() => Cancel();

    // Ends the scripted control: snaps her to the NavMesh and gives the body back to the agent.
    private void EndScriptedHere()
    {
        partnerAnimator.SetScriptedRootMotion(false);
        partnerAnimator.EndScripted();
        Vector3 at = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) ? hit.position : transform.position;
        agent.Warp(at);
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    // Clears the state and raises Finished.
    private void Done(DoorResult outcome)
    {
        Door used = door;
        step = Step.None;
        door = null;
        Finished?.Invoke(used, action, outcome);
    }

    // Leaf curve from (Blender frame, degrees) pairs of a clip with lastFrame frames (frame 1 = time 0), linear between keys.
    private static AnimationCurve FramesCurve(int lastFrame, params float[] frameDegrees)
    {
        int n = frameDegrees.Length / 2;
        var keys = new Keyframe[n];
        for (int i = 0; i < n; i++)
            keys[i] = new Keyframe((frameDegrees[i * 2] - 1f) / (lastFrame - 1f), frameDegrees[i * 2 + 1]);
        for (int i = 0; i < n; i++)
        {
            float inSlope = i > 0 ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time) : 0f;
            float outSlope = i < n - 1 ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time) : 0f;
            keys[i].inTangent = inSlope;
            keys[i].outTangent = outSlope;
        }
        return new AnimationCurve(keys);
    }

    // A vector flattened onto the floor.
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
