using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Her side of a keypad (any ButtonPanel: a keypad, a slot machine's button): walks up to it (NavMesh, then a short scripted walk), faces it, raises her hand
// (HelenaKeypadRaise), presses each key she was given (HelenaKeypadPress, her finger guided to the key by hand IK)
// and keeps the hand up a while for the next ones (the operator says the code one number at a time);
// nothing more for a while (ButtonPanel.HoldTime) -> she lowers it (HelenaKeypadLower) and is free again.
// Keys: '0'-'9', Keypad.Clear ('C'), Keypad.Enter ('E').
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerKeypadUser : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CombatField = nameof(combat);

    // Her animator (keypad clips, hand IK), interactor and combat (stopped first), and the short scripted walk to the spot.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private ScriptedWalker walker = new ScriptedWalker();
    [Tooltip("The NavMesh walk hands over to the scripted walk this close to the spot (m).")]
    [SerializeField] private float handoffDistance = 0.6f;

    [Header("Measured on HelenaButton_Pushing")]
    [Tooltip("Press clip: the finger touches the key (normalized).")]
    [SerializeField, Range(0f, 1f)] private float contactTime = 0.3f;
    [Tooltip("Press clip: hand IK fades in / holds / fades out (normalized).")]
    [SerializeField] private Vector4 ikWindow = new Vector4(0.05f, 0.25f, 0.55f, 0.8f);
    // How much the hand IK pulls the finger onto the key.
    [SerializeField, Range(0f, 1f)] private float ikWeight = 0.9f;
    [Tooltip("Her wrist from her fingertip at the press (her space: x right, y up, z forward).")]
    [SerializeField] private Vector3 wristFromTip = new Vector3(0.04f, 0.03f, -0.129f);

    // Crossfade between the keypad clips (s), and the max time for the scripted walk-in (s).
    [Header("Timing")]
    [SerializeField] private float clipBlend = 0.2f;
    [SerializeField] private float enterTimeout = 1.5f;
    [Tooltip("Sent to a keypad without the code (\"digita o codigo\"): she waits this long for the first key (s) - time to think and speak.")]
    [SerializeField] private float firstKeyWait = 25f;

    // Steps of a keypad use.
    private enum Step { None, Approach, WalkIn, Face, Raise, Hold, Press, Lower, Waiting }

    // Her agent, the step, the panel, the keys still to press, the key being pressed (pressed / Enter seen), a timer, the pose in front of the panel, and what to run once she leaves.
    private NavMeshAgent agent;
    private Step step;
    private ButtonPanel keypad;
    private readonly Queue<char> pending = new Queue<char>();
    private char pressing;
    private bool pressed;
    private bool entered;
    private float timer;
    private Vector3 posePosition;
    private Vector3 poseForward;
    private Action afterLeaving;
    private bool stayAfterLower;   // hand down, still at the panel (ButtonPanel.KeepHandUp false)
    private bool anyKey;           // a key was pressed since she came to this panel

    public event Action<ButtonPanel, char> KeyPressed;   // each key she pressed (after the panel took it)

    // Read-only state for commands and the operator UI.
    public bool IsBusy => step != Step.None;
    public bool IsAtKeypad => step >= Step.Raise;
    public ButtonPanel Current => keypad;

    // Gets the agent; checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || interactor == null || combat == null)
        {
            Debug.LogError($"{name}: PartnerKeypadUser is missing references (run Last Terminal/4).", this);
            enabled = false;
            return;
        }
        walker.Bind(transform, partnerAnimator);
    }

    // Goes to the keypad (if not there yet) and presses these keys in order. False = can't (busy, no spot).
    public bool Use(ButtonPanel target, string keysToPress)
    {
        if (!enabled || target == null) return false;
        if (step != Step.None && target != keypad) return false;
        if (step == Step.None)
        {
            if (partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp || partnerAnimator.IsScripted) return false;
            Vector3 n = Flat(target.transform.forward).normalized;
            Vector3 spot = target.transform.position + n * target.StandDistance;
            if (!NavMesh.SamplePosition(new Vector3(spot.x, transform.position.y, spot.z), out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) return false;
            combat.Holster();
            if (!interactor.MoveTo(hit.position)) return false;
            keypad = target;
            posePosition = new Vector3(spot.x, hit.position.y, spot.z);
            poseForward = -n;
            pending.Clear();
            afterLeaving = null;
            anyKey = false;
            step = Step.Approach;
        }
        foreach (char c in keysToPress) pending.Enqueue(char.ToUpperInvariant(c));
        if ((step == Step.Lower || step == Step.Waiting) && pending.Count > 0) Raise();   // lowering / waiting: back up
        timer = 0f;
        return true;
    }

    // Done with the keypad: the hand comes down (or the walk there is dropped), then `then` runs.
    public void Leave(Action then = null)
    {
        pending.Clear();
        switch (step)
        {
            case Step.None:
                then?.Invoke();
                return;
            case Step.Approach:
                Done();
                then?.Invoke();
                return;
            case Step.WalkIn:
            case Step.Face:
                EndScriptedHere();
                Done();
                then?.Invoke();
                return;
            case Step.Waiting:
                afterLeaving = then;
                Finish();
                return;
            default:
                afterLeaving = then;
                stayAfterLower = false;
                if (step != Step.Lower && step != Step.Press) Lower();   // a press in progress finishes first
                return;
        }
    }

    // ends at once where she is (a save loaded, she was hit...)
    public void Abort()
    {
        if (step == Step.None) return;
        pending.Clear();
        partnerAnimator.SetRightHandTarget(Vector3.zero, 0f);
        if (partnerAnimator.IsScripted)
        {
            if (step >= Step.Raise) partnerAnimator.SnapToLocomotion();
            EndScriptedHere();
        }
        Done();
    }

    // Runs the current step.
    private void Update()
    {
        switch (step)
        {
            case Step.Approach: UpdateApproach(); break;
            case Step.WalkIn:
                if (walker.WalkTo(posePosition)) { step = Step.Face; walker.ResetTurn(); }
                break;
            case Step.Face:
                if (walker.TurnTo(poseForward)) Raise();
                break;
            case Step.Raise:
                if (ClipTime(PartnerAnimator.KeypadClip.Raise, out float raised) && raised >= 0.98f) Hold();
                break;
            case Step.Hold:
                if (pending.Count > 0) { Press(pending.Dequeue()); break; }
                if (afterLeaving != null) { Lower(); break; }
                if (!keypad.KeepHandUp) { stayAfterLower = true; Lower(); break; }
                timer += Time.deltaTime;
                if (timer >= WaitTime) Lower();   // the panel says how long (keypad: a few s, slot machine: longer)
                break;
            case Step.Press: UpdatePress(); break;
            case Step.Lower:
                if (pending.Count > 0 && afterLeaving == null) { stayAfterLower = false; Raise(); break; }
                if (!ClipTime(PartnerAnimator.KeypadClip.Lower, out float lowered) || lowered < 0.95f) break;
                if (stayAfterLower && afterLeaving == null)
                {
                    stayAfterLower = false;
                    partnerAnimator.EndKeypad();   // standing in front of it, still hers
                    step = Step.Waiting;
                    timer = 0f;
                }
                else Finish();
                break;
            case Step.Waiting:
                if (afterLeaving != null) { Finish(); break; }
                if (pending.Count > 0) { Raise(); break; }
                timer += Time.deltaTime;
                if (timer >= WaitTime) Finish();
                break;
        }
    }

    // NavMesh walk to the panel, then the scripted walk-in.
    private void UpdateApproach()
    {
        // another command sent her somewhere else: give up
        if ((Flat(agent.destination) - Flat(posePosition)).sqrMagnitude > 0.25f) { Done(); return; }
        if (agent.pathPending || partnerAnimator.IsTurning) return;
        float dist = (Flat(transform.position) - Flat(posePosition)).magnitude;
        bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f;
        if (dist > handoffDistance && !arrived) return;

        float speed = Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        partnerAnimator.BeginScripted();
        walker.Begin(speed);
        step = Step.WalkIn;
    }

    // Raises her hand to the panel.
    private void Raise()
    {
        walker.Stop();
        partnerAnimator.PlayKeypad(PartnerAnimator.KeypadClip.Raise, clipBlend);
        step = Step.Raise;
        timer = 0f;
    }

    // Hand up, waiting for the next key (lowers it after the wait time).
    private void Hold()
    {
        step = Step.Hold;
        timer = 0f;
    }

    // Plays the press for one key, finger guided onto it by IK.
    private void Press(char key)
    {
        pressing = key;
        anyKey = true;
        pressed = false;
        entered = false;
        timer = 0f;
        partnerAnimator.PlayKeypad(PartnerAnimator.KeypadClip.Press, clipBlend);
        step = Step.Press;
    }

    // Press clip: IK in / out by phase, the key fires at the contact time.
    private void UpdatePress()
    {
        timer += Time.deltaTime;
        if (!ClipTime(PartnerAnimator.KeypadClip.Press, out float t))
        {
            if (entered || timer > enterTimeout) { partnerAnimator.SetRightHandTarget(Vector3.zero, 0f); Hold(); }
            return;
        }
        entered = true;

        // the finger to the key: the wrist where it is when the fingertip is on the key
        if (keypad.TryGetKey(pressing, out Vector3 key))
        {
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ikWindow.x, ikWindow.y, t)) *
                      (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ikWindow.z, ikWindow.w, t)));
            partnerAnimator.SetRightHandTarget(key + transform.rotation * wristFromTip, w * ikWeight);
        }
        if (!pressed && t >= contactTime)
        {
            pressed = true;
            keypad.Press(pressing);
            KeyPressed?.Invoke(keypad, pressing);
        }
        if (t >= 0.98f)
        {
            partnerAnimator.SetRightHandTarget(Vector3.zero, 0f);
            Hold();
        }
    }

    // waiting for the next key: the panel's time; for the first key of a code not said yet, longer
    private float WaitTime => anyKey ? keypad.HoldTime : Mathf.Max(keypad.HoldTime, firstKeyWait);

    // Drops the IK and lowers the hand.
    private void Lower()
    {
        partnerAnimator.SetRightHandTarget(Vector3.zero, 0f);
        partnerAnimator.PlayKeypad(PartnerAnimator.KeypadClip.Lower, clipBlend);
        step = Step.Lower;
    }

    // Hands her back to the agent and runs what was queued after leaving.
    private void Finish()
    {
        partnerAnimator.EndKeypad();
        EndScriptedHere();
        Action then = afterLeaving;
        Done();
        then?.Invoke();
    }

    // Is this keypad clip playing, and at what normalized time?
    private bool ClipTime(PartnerAnimator.KeypadClip wanted, out float t)
    {
        bool playing = partnerAnimator.TryGetKeypadTime(out PartnerAnimator.KeypadClip clip, out t);
        return playing && clip == wanted;
    }

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

    // Clears the state.
    private void Done()
    {
        keypad = null;
        step = Step.None;
        afterLeaving = null;
    }

    // A vector flattened onto the floor.
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
