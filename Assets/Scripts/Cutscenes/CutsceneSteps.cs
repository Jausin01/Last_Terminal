using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// The steps a Cutscene is made of (added in its inspector: "+ Add step"). Each one runs (a coroutine, the
// next step starts when it ends) and knows how to jump to its end (Finish: when the cutscene is skipped, the world
// still ends up as if it had played: she is where she walked to, the door is open, the flag is set).
// A new kind of step: a [Serializable] class deriving from CutsceneStep with a [CutsceneStepInfo] menu path.
[Serializable]
public abstract class CutsceneStep
{
    // text of the step in the list (the inspector)
    public abstract string Summary { get; }

    // Plays the step (a coroutine; the next step starts when it ends).
    public abstract IEnumerator Run(CutsceneDirector director);

    // skipped: the end state right away (nothing to do by default)
    public virtual void Finish(CutsceneDirector director) { }

    // Helpers for summaries: an object's name, a shortened text.
    protected static string N(Object o) => o != null ? o.name : "(none)";
    protected static string Short(string s, int max = 42) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max) + "...";
}

// where the step shows in the "+ Add step" menu, and its order there
[AttributeUsage(AttributeTargets.Class)]
public class CutsceneStepInfoAttribute : Attribute
{
    // Menu path ("Category/Name") and order in the menu.
    public readonly string Menu;
    public readonly int Order;
    public CutsceneStepInfoAttribute(string menu, int order = 0) { Menu = menu; Order = order; }
}

// ====================================================================== movement
[Serializable, CutsceneStepInfo("Movement/Walk to", 0)]
public class MoveStep : CutsceneStep
{
    // Who walks.
    public CutsceneActor actor;
    [Tooltip("Where to (an empty object in the scene; its blue arrow = where she faces at the end).")]
    public Transform target;
    [Tooltip("1 = walk, 2 = hurry.")]
    public float speed = 1f;
    [Tooltip("Wait until arrived before the next step.")]
    public bool waitArrival = true;
    [Tooltip("At the end turn to the target's forward (blue arrow).")]
    public bool faceTargetForward = true;

    // Step list description.
    public override string Summary => $"{N(actor)} walks to {N(target)}{(speed > 1.3f ? " (fast)" : "")}{(waitArrival ? "" : " (no wait)")}";

    // Walks to the target (optionally waiting and turning to its arrow at the end).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || target == null) yield break;
        d.Use(actor);
        actor.MoveTo(target.position, speed);
        if (!waitArrival) yield break;
        float t = 0f;
        while (!actor.HasArrived(target.position) && t < 30f) { t += Time.deltaTime; yield return null; }
        if (!faceTargetForward) yield break;
        while (!actor.TurnToward(target.forward, 240f)) yield return null;
    }

    // Skipped: already there, facing the arrow.
    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || target == null) return;
        d.Use(actor);
        actor.Teleport(target.position, faceTargetForward ? target.rotation : actor.transform.rotation);
    }
}

// Waits until an actor that was sent walking (without waiting) arrives.
[Serializable, CutsceneStepInfo("Movement/Wait for arrival", 1)]
public class WaitArrivalStep : CutsceneStep
{
    // Who, and where to.
    public CutsceneActor actor;
    public Transform target;

    // Step list description.
    public override string Summary => $"wait for {N(actor)} to reach {N(target)}";

    // Waits for the arrival (30 s at most).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || target == null) yield break;
        float t = 0f;
        while (!actor.HasArrived(target.position) && t < 30f) { t += Time.deltaTime; yield return null; }
    }

    // Skipped: already there.
    public override void Finish(CutsceneDirector d)
    {
        if (actor != null && target != null) actor.Teleport(target.position, actor.transform.rotation);
    }
}

// Turns an actor toward an object (or along its arrow).
[Serializable, CutsceneStepInfo("Movement/Turn toward", 2)]
public class FaceStep : CutsceneStep
{
    // Who turns.
    public CutsceneActor actor;
    [Tooltip("Turn toward this object (or along its blue arrow, below).")]
    public Transform lookAt;
    // Turn along the target's blue arrow instead of toward it.
    public bool useTargetForward;
    [Tooltip("Degrees per second.")]
    public float turnSpeed = 200f;

    // Step list description.
    public override string Summary => $"{N(actor)} turns toward {N(lookAt)}";

    // The direction to face.
    private Vector3 Direction => useTargetForward ? lookAt.forward : lookAt.position - actor.transform.position;

    // Turns until facing it (5 s at most).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || lookAt == null) yield break;
        d.Use(actor);
        float t = 0f;
        while (!actor.TurnToward(Direction, turnSpeed) && t < 5f) { t += Time.deltaTime; yield return null; }
    }

    // Skipped: facing it at once.
    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || lookAt == null) return;
        Vector3 dir = Direction;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f) actor.transform.rotation = Quaternion.LookRotation(dir);
    }
}

// Puts an actor at a place at once.
[Serializable, CutsceneStepInfo("Movement/Teleport", 3)]
public class TeleportStep : CutsceneStep
{
    // Who, and where.
    public CutsceneActor actor;
    public Transform target;

    // Step list description.
    public override string Summary => $"{N(actor)} appears at {N(target)}";

    // Teleports now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Teleports (also when skipped).
    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || target == null) return;
        d.Use(actor);
        actor.Teleport(target.position, target.rotation);
    }
}

// ====================================================================== animation
[Serializable, CutsceneStepInfo("Animation/Play animation (clip)", 10)]
public class ClipStep : CutsceneStep
{
    // Who plays the clip.
    public CutsceneActor actor;
    [Tooltip("Any animation clip (for Helena: a Humanoid clip, e.g. from a Mixamo FBX).")]
    public AnimationClip clip;
    [Tooltip("Crossfade time (s).")]
    public float blend = 0.25f;
    [Tooltip("Wait for the clip to end (a looping clip: use a Wait step after it instead).")]
    public bool waitEnd = true;
    [Tooltip("When it ends, back to standing. Off = keeps the last pose (until another animation / walk).")]
    public bool backToNormal = true;

    // Step list description.
    public override string Summary => $"{N(actor)} plays {N(clip)}{(waitEnd ? "" : " (no wait)")}";

    // Plays the clip (optionally waiting for its end and going back to normal).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || clip == null) yield break;
        d.Use(actor);
        actor.PlayClip(clip, blend);
        if (!waitEnd) yield break;
        yield return null;
        float t = 0f, limit = clip.length + blend + 1f;
        while (!actor.IsClipDone && t < limit) { t += Time.deltaTime; yield return null; }
        if (backToNormal) actor.StopAnimation(blend);
    }

    // Skipped: back to normal.
    public override void Finish(CutsceneDirector d)
    {
        if (actor != null && backToNormal) actor.StopAnimation(0.1f);
    }
}

// Puts an actor in a state of its Animator by name.
[Serializable, CutsceneStepInfo("Animation/Animator state", 11)]
public class StateStep : CutsceneStep
{
    // Who.
    public CutsceneActor actor;
    [Tooltip("A state of its Animator by name (Helena: e.g. FloorSit).")]
    public string state = "";
    // Crossfade time (s).
    public float blend = 0.3f;
    [Tooltip("Seconds before the next step.")]
    public float wait = 0f;

    // Step list description.
    public override string Summary => $"{N(actor)} enters state \"{state}\"";

    // Plays the state, then waits.
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || string.IsNullOrEmpty(state)) yield break;
        d.Use(actor);
        actor.PlayState(state, blend);
        float t = 0f;
        while (t < wait) { t += Time.deltaTime; yield return null; }
    }
}

// Ends a clip / state: back to walking / idle.
[Serializable, CutsceneStepInfo("Animation/Back to normal", 12)]
public class StopAnimationStep : CutsceneStep
{
    // Who, and the crossfade time.
    public CutsceneActor actor;
    public float blend = 0.3f;

    // Step list description.
    public override string Summary => $"{N(actor)} back to normal";

    // Back to normal now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor != null) actor.StopAnimation(blend);
        yield break;
    }

    // Skipped: back to normal quickly.
    public override void Finish(CutsceneDirector d)
    {
        if (actor != null) actor.StopAnimation(0.1f);
    }
}

// ====================================================================== speech
[Serializable, CutsceneStepInfo("Speech/Say", 20)]
public class SayStep : CutsceneStep
{
    [Tooltip("Who speaks (name and color on the subtitle). Empty = the name below.")]
    public CutsceneActor actor;
    [Tooltip("Name on the subtitle when there is no actor (or to override it).")]
    public string speaker = "";
    // The line in each language.
    [TextArea(2, 5)] public string textPt = "";
    [TextArea(2, 5)] public string textEn = "";
    [Tooltip("Optional recordings.")]
    public AudioClip audioPt;
    // English recording.
    public AudioClip audioEn;
    [Tooltip("Wait until the line ends before the next step.")]
    public bool wait = true;
    [Tooltip("Extra pause after the line (s).")]
    public float pauseAfter = 0.2f;

    // The name shown on the subtitle.
    private string Who => !string.IsNullOrEmpty(speaker) ? speaker : actor != null ? actor.DisplayName : "";

    // Step list description.
    public override string Summary => $"{(Who.Length > 0 ? Who : "?")}: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

    // Shows the subtitle (and plays the recording) in the game's language; waits for its end.
    public override IEnumerator Run(CutsceneDirector d)
    {
        bool pt = d.IsPortuguese;
        string text = pt && !string.IsNullOrEmpty(textPt) ? textPt : !string.IsNullOrEmpty(textEn) ? textEn : textPt;
        AudioClip clip = pt && audioPt != null ? audioPt : audioEn != null ? audioEn : audioPt;
        Color color = actor != null ? actor.NameColor : new Color(0.7f, 0.8f, 1f);
        float duration = d.Say(Who, color, text, clip);
        if (!wait) yield break;
        float t = 0f;
        while (t < duration + pauseAfter) { t += Time.deltaTime; yield return null; }
    }

    // Skipped: voice and subtitle off.
    public override void Finish(CutsceneDirector d)
    {
        d.StopVoice();
        d.HideSubtitle();
    }
}

// A line the operator (the player) must say or type; the cutscene waits until he does.
[Serializable, CutsceneStepInfo("Speech/Operator line (the player says it)", 22)]
public class OperatorLineStep : CutsceneStep
{
    [Tooltip("What the operator has to say: shown on the screen; he says it (hold V) or types it (Enter).")]
    [TextArea(2, 4)] public string textPt = "";
    // The line in English.
    [TextArea(2, 4)] public string textEn = "";
    [Tooltip("Share of the line's words that must be heard (0.6 = most of it). Short words (a, o, de...) don't count.")]
    [Range(0.1f, 1f)] public float match = 0.6f;
    [Tooltip("Anything he says is accepted (only the moment matters).")]
    public bool acceptAnything;
    [Tooltip("After he says it, the line shows as his subtitle.")]
    public bool showSubtitle = true;
    // Name on the subtitle after he says it, per language.
    public string speakerPt = "VOCÊ";
    public string speakerEn = "YOU";

    // Step list description.
    public override string Summary => $"OPERATOR says: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

    // Shows the line, waits until what he says matches it (or anything, if allowed), then shows it as his subtitle.
    public override IEnumerator Run(CutsceneDirector d)
    {
        bool pt = d.IsPortuguese;
        string line = pt && !string.IsNullOrEmpty(textPt) ? textPt : !string.IsNullOrEmpty(textEn) ? textEn : textPt;
        if (string.IsNullOrEmpty(line)) yield break;
        d.BeginListening(line);
        while (true)
        {
            if (d.TryTakeHeard(out string heard))
            {
                if (acceptAnything || Matches(heard, line, match)) break;
                d.ShowPrompt(line, heard);   // not that: again
            }
            yield return null;
        }
        d.EndListening();
        if (!showSubtitle) yield break;
        float duration = d.Say(pt ? speakerPt : speakerEn, new Color(0.6f, 0.85f, 1f), line, null);
        float t = 0f;
        while (t < duration * 0.7f) { t += Time.deltaTime; yield return null; }
    }

    // Skipped: stops listening.
    public override void Finish(CutsceneDirector d) => d.EndListening();

    // enough of the line's words are in what was heard (no accents, no punctuation)
    public static bool Matches(string heard, string line, float share = 0.6f)
    {
        var heardWords = new HashSet<string>(CommandParser.Normalize(heard).Split(' '));
        string[] all = CommandParser.Normalize(line).Split(' ');
        var words = new List<string>();
        foreach (string w in all) if (w.Length >= 3) words.Add(w);
        if (words.Count == 0) words.AddRange(all);
        int found = 0;
        foreach (string w in words) if (heardWords.Contains(w)) found++;
        return words.Count == 0 || found >= Mathf.CeilToInt(words.Count * share - 0.001f);
    }
}

// Helena says one of her voice-bank lines.
[Serializable, CutsceneStepInfo("Speech/Helena line (from the bank)", 21)]
public class PartnerLineStep : CutsceneStep
{
    [Tooltip("One of her lines (VoiceLineBank: text, recordings, subtitles).")]
    public PartnerLine line = PartnerLine.Acknowledge;
    // Wait until the line ends.
    public bool wait = true;

    // Step list description.
    public override string Summary => $"Helena says line {line}";

    // Says the line; optionally waits for its length.
    public override IEnumerator Run(CutsceneDirector d)
    {
        float duration = d.SayPartnerLine(line);
        if (!wait) yield break;
        float t = 0f;
        while (t < duration) { t += Time.deltaTime; yield return null; }
    }
}

// ====================================================================== cameras / screen
[Serializable, CutsceneStepInfo("Camera/Cut to camera", 30)]
public class CameraCutStep : CutsceneStep
{
    [Tooltip("One of the scene's CCTV cameras.")]
    public SecurityCamera camera;

    // Step list description.
    public override string Summary => $"camera: {(camera != null ? camera.Label : "(none)")}";

    // Cuts now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Cuts the operator's view to the camera.
    public override void Finish(CutsceneDirector d) => d.CutTo(camera);
}

// A free camera shot taken from an empty object (CCTV shot camera, or the cinema camera).
[Serializable, CutsceneStepInfo("Camera/Free shot (from a point)", 31)]
public class CameraShotStep : CutsceneStep
{
    [Tooltip("An empty object: the shot is taken from it, looking along its blue arrow. Tip: align it with the " +
             "Scene view (GameObject > Align With View).")]
    // The empty object the shot is taken from.
    public Transform shot;
    [Tooltip("The room shown on the map (optional).")]
    public Room room;
    // Camera label shown on the HUD, and the field of view.
    public string label = "CAM ??";
    [Range(15f, 100f)] public float fieldOfView = 60f;

    // Step list description.
    public override string Summary => $"shot from {N(shot)} ({label})";

    // Takes the shot now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Takes the shot.
    public override void Finish(CutsceneDirector d) => d.Shot(shot, room, label, fieldOfView);
}

// Fades the screen to black or back.
[Serializable, CutsceneStepInfo("Screen/Fade to or from black", 32)]
public class FadeStep : CutsceneStep
{
    [Tooltip("On = to black, off = back from black.")]
    public bool toBlack = true;
    // Fade time (s).
    public float duration = 1f;

    // Step list description.
    public override string Summary => toBlack ? $"fade to black ({duration:0.#} s)" : $"fade from black ({duration:0.#} s)";

    // Fades over the duration.
    public override IEnumerator Run(CutsceneDirector d)
    {
        yield return d.FadeTo(toBlack ? 1f : 0f, duration);
    }

    // Skipped: the end alpha at once.
    public override void Finish(CutsceneDirector d) => d.SetFade(toBlack ? 1f : 0f);
}

// ====================================================================== world
[Serializable, CutsceneStepInfo("World/Door", 40)]
public class DoorStep : CutsceneStep
{
    // What to do with the door.
    public enum DoorAction { Open, Close, Lock, Unlock, UnlockAndOpen }

    // The door and the action.
    public Door door;
    public DoorAction action = DoorAction.Open;

    // Step list description.
    public override string Summary => $"door {N(door)}: {action}";

    // Acts now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Opens / closes / locks / unlocks the door.
    public override void Finish(CutsceneDirector d)
    {
        if (door == null) return;
        switch (action)
        {
            case DoorAction.Open: door.Open(); break;
            case DoorAction.Close: door.Close(); break;
            case DoorAction.Lock: door.SetLocked(true); break;
            case DoorAction.Unlock: door.SetLocked(false); break;
            case DoorAction.UnlockAndOpen: door.SetLocked(false); door.Open(); break;
        }
    }
}

// Enables / disables a GameObject.
[Serializable, CutsceneStepInfo("World/Enable or disable object", 41)]
public class SetActiveStep : CutsceneStep
{
    // The object and the wanted state.
    public GameObject target;
    public bool active = true;

    // Step list description.
    public override string Summary => $"{(active ? "enable" : "disable")} {N(target)}";

    // Acts now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Sets the state.
    public override void Finish(CutsceneDirector d)
    {
        if (target != null) target.SetActive(active);
    }
}

// Plays a sound once.
[Serializable, CutsceneStepInfo("World/Play sound", 42)]
public class SoundStep : CutsceneStep
{
    // The clip.
    public AudioClip clip;
    [Tooltip("Where it sounds (empty = no position, heard the same everywhere).")]
    public Transform at;
    // Volume, and whether the next step waits for the sound's end.
    [Range(0f, 1f)] public float volume = 1f;
    public bool wait;

    // Step list description.
    public override string Summary => $"sound {N(clip)}{(at != null ? " at " + at.name : "")}";

    // Plays it (optionally waiting for its length).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (clip == null) yield break;
        d.PlaySound(clip, at, volume);
        if (!wait) yield break;
        float t = 0f;
        while (t < clip.length) { t += Time.deltaTime; yield return null; }
    }
}

// Calls any methods set in the Inspector (UnityEvent).
[Serializable, CutsceneStepInfo("World/Call event (anything)", 43)]
public class EventStep : CutsceneStep
{
    [Tooltip("Calls methods of objects in the scene (like a button's On Click).")]
    public UnityEvent onRun = new UnityEvent();

    // Step list description.
    public override string Summary => $"event ({onRun.GetPersistentEventCount()} call(s))";

    // Invokes the event.
    public override IEnumerator Run(CutsceneDirector d)
    {
        onRun.Invoke();
        yield break;
    }

    // Skipped: invokes it too (its effects must happen).
    public override void Finish(CutsceneDirector d) => onRun.Invoke();
}

// Sets a story flag.
[Serializable, CutsceneStepInfo("Story/Set flag", 50)]
public class SetFlagStep : CutsceneStep
{
    [Tooltip("StoryProgress flag (saved; files on the PC and other cutscenes use them).")]
    public string flag = "";

    // Step list description.
    public override string Summary => $"flag \"{flag}\"";

    // Sets it now.
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Sets the flag in StoryProgress.
    public override void Finish(CutsceneDirector d)
    {
        if (d.Story != null && !string.IsNullOrEmpty(flag)) d.Story.Set(flag);
    }
}

// ====================================================================== flow
[Serializable, CutsceneStepInfo("Flow/Wait", 60)]
public class WaitStep : CutsceneStep
{
    // How long to wait (s).
    public float seconds = 1f;

    // Step list description.
    public override string Summary => $"wait {seconds:0.##} s";

    // Waits.
    public override IEnumerator Run(CutsceneDirector d)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
    }
}

// Runs several steps at the same time; ends when all of them have ended.
[Serializable, CutsceneStepInfo("Flow/At the same time (group)", 61)]
public class ParallelStep : CutsceneStep
{
    [Tooltip("These run together; the group ends when all have ended.")]
    [SerializeReference] public List<CutsceneStep> steps = new List<CutsceneStep>();

    // Step list description.
    public override string Summary => $"at the same time: {steps.Count} step(s)";

    // Which inner steps finished (the others are finished when skipped).
    [NonSerialized] private bool[] done;

    // Advances every inner step each frame until all are done.
    public override IEnumerator Run(CutsceneDirector d)
    {
        var running = new List<Stack<IEnumerator>>();
        done = new bool[steps.Count];
        for (int i = 0; i < steps.Count; i++)
        {
            var stack = new Stack<IEnumerator>();
            if (steps[i] != null) stack.Push(steps[i].Run(d));
            running.Add(stack);
        }
        while (true)
        {
            bool any = false;
            for (int i = 0; i < running.Count; i++)
            {
                if (done[i]) continue;
                if (CutsceneDirector.Advance(running[i])) any = true;
                else done[i] = true;
            }
            if (!any) yield break;
            yield return null;
        }
    }

    // Skipped: finishes the inner steps that hadn't ended.
    public override void Finish(CutsceneDirector d)
    {
        for (int i = 0; i < steps.Count; i++)
            if (steps[i] != null && (done == null || i >= done.Length || !done[i])) steps[i].Finish(d);
    }
}
