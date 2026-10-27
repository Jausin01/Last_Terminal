using UnityEngine;

// Plays any AnimationClip on an Animator without adding a state for it: the controller has two empty states
// (CutsceneA / CutsceneB, made by the Last Terminal menus) whose clips are swapped through an AnimatorOverrideController.
// Each new clip goes into the slot that isn't playing, so one clip can crossfade into the next.
public class CutsceneClipSlots
{
    // The two placeholder state names in the controller, and their hashes.
    public const string StateA = "CutsceneA";
    public const string StateB = "CutsceneB";
    private static readonly int StateAId = Animator.StringToHash(StateA);
    private static readonly int StateBId = Animator.StringToHash(StateB);

    // The animator, its override controller, and the two placeholder clips that get replaced.
    private readonly Animator animator;
    private readonly AnimatorOverrideController overrides;
    private readonly AnimationClip slotA, slotB;
    private int current = -1;   // -1 = none playing, 0 = A, 1 = B

    // false when the controller has no cutscene states (the Last Terminal animator menu wasn't run again)
    public bool Ready { get; }

    // call once, before anything plays (Awake): the controller is wrapped in an override controller
    public CutsceneClipSlots(Animator animator, AnimationClip placeholderA, AnimationClip placeholderB)
    {
        this.animator = animator;
        slotA = placeholderA;
        slotB = placeholderB;
        if (animator == null || animator.runtimeAnimatorController == null || slotA == null || slotB == null) return;
        overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrides;
        Ready = animator.HasState(0, StateAId) && animator.HasState(0, StateBId);
    }

    // A clip is playing in one of the slots.
    public bool IsPlaying => current >= 0;

    // Plays a clip in the free slot (crossfading from the other one).
    public void Play(AnimationClip clip, float blend)
    {
        if (!Ready || clip == null) return;
        int next = current == 0 ? 1 : 0;
        overrides[next == 0 ? slotA : slotB] = clip;
        int state = next == 0 ? StateAId : StateBId;
        if (blend <= 0f) animator.Play(state, 0, 0f);
        else animator.CrossFadeInFixedTime(state, blend, 0, 0f);
        current = next;
    }

    // the clip reached its end (a looping clip never does)
    public bool IsDone
    {
        get
        {
            if (current < 0) return true;
            if (animator.IsInTransition(0)) return false;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            int state = current == 0 ? StateAId : StateBId;
            return info.shortNameHash == state && !info.loop && info.normalizedTime >= 1f;
        }
    }

    // the caller crossfades to its own state; this only forgets the clip
    public void Stop() => current = -1;
}
