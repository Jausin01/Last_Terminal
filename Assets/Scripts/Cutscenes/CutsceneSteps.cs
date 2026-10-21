using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// The steps a Cutscene is made of (added in its inspector: "+ Adicionar passo"). Each one runs (a coroutine, the
// next step starts when it ends) and knows how to jump to its end (Finish: when the cutscene is skipped, the world
// still ends up as if it had played: she is where she walked to, the door is open, the flag is set).
// A new kind of step: a [Serializable] class deriving from CutsceneStep with a [CutsceneStepInfo] menu path.
[Serializable]
public abstract class CutsceneStep
{
    // text of the step in the list (the inspector)
    public abstract string Summary { get; }

    public abstract IEnumerator Run(CutsceneDirector director);

    // skipped: the end state right away (nothing to do by default)
    public virtual void Finish(CutsceneDirector director) { }

    protected static string N(Object o) => o != null ? o.name : "(nada)";
    protected static string Short(string s, int max = 42) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max) + "...";
}

// where the step shows in the "+ Adicionar passo" menu, and its order there
[AttributeUsage(AttributeTargets.Class)]
public class CutsceneStepInfoAttribute : Attribute
{
    public readonly string Menu;
    public readonly int Order;
    public CutsceneStepInfoAttribute(string menu, int order = 0) { Menu = menu; Order = order; }
}

// ====================================================================== movement
[Serializable, CutsceneStepInfo("Movimento/Andar até", 0)]
public class MoveStep : CutsceneStep
{
    public CutsceneActor actor;
    [Tooltip("Where to (an empty object in the scene; its blue arrow = where she faces at the end).")]
    public Transform target;
    [Tooltip("1 = walk, 2 = hurry.")]
    public float speed = 1f;
    [Tooltip("Wait until arrived before the next step.")]
    public bool waitArrival = true;
    [Tooltip("At the end turn to the target's forward (blue arrow).")]
    public bool faceTargetForward = true;

    public override string Summary => $"{N(actor)} anda até {N(target)}{(speed > 1.3f ? " (rápido)" : "")}{(waitArrival ? "" : " (sem esperar)")}";

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

    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || target == null) return;
        d.Use(actor);
        actor.Teleport(target.position, faceTargetForward ? target.rotation : actor.transform.rotation);
    }
}

[Serializable, CutsceneStepInfo("Movimento/Esperar chegar", 1)]
public class WaitArrivalStep : CutsceneStep
{
    public CutsceneActor actor;
    public Transform target;

    public override string Summary => $"espera {N(actor)} chegar em {N(target)}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || target == null) yield break;
        float t = 0f;
        while (!actor.HasArrived(target.position) && t < 30f) { t += Time.deltaTime; yield return null; }
    }

    public override void Finish(CutsceneDirector d)
    {
        if (actor != null && target != null) actor.Teleport(target.position, actor.transform.rotation);
    }
}

[Serializable, CutsceneStepInfo("Movimento/Virar para", 2)]
public class FaceStep : CutsceneStep
{
    public CutsceneActor actor;
    [Tooltip("Turn toward this object (or along its blue arrow, below).")]
    public Transform lookAt;
    public bool useTargetForward;
    [Tooltip("Degrees per second.")]
    public float turnSpeed = 200f;

    public override string Summary => $"{N(actor)} vira para {N(lookAt)}";

    private Vector3 Direction => useTargetForward ? lookAt.forward : lookAt.position - actor.transform.position;

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || lookAt == null) yield break;
        d.Use(actor);
        float t = 0f;
        while (!actor.TurnToward(Direction, turnSpeed) && t < 5f) { t += Time.deltaTime; yield return null; }
    }

    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || lookAt == null) return;
        Vector3 dir = Direction;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f) actor.transform.rotation = Quaternion.LookRotation(dir);
    }
}

[Serializable, CutsceneStepInfo("Movimento/Teleportar", 3)]
public class TeleportStep : CutsceneStep
{
    public CutsceneActor actor;
    public Transform target;

    public override string Summary => $"{N(actor)} aparece em {N(target)}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d)
    {
        if (actor == null || target == null) return;
        d.Use(actor);
        actor.Teleport(target.position, target.rotation);
    }
}

// ====================================================================== animation
[Serializable, CutsceneStepInfo("Animação/Tocar animação (clip)", 10)]
public class ClipStep : CutsceneStep
{
    public CutsceneActor actor;
    [Tooltip("Any animation clip (for Helena: a Humanoid clip, e.g. from a Mixamo FBX).")]
    public AnimationClip clip;
    [Tooltip("Crossfade time (s).")]
    public float blend = 0.25f;
    [Tooltip("Wait for the clip to end (a looping clip: use a Wait step after it instead).")]
    public bool waitEnd = true;
    [Tooltip("When it ends, back to standing. Off = keeps the last pose (until another animation / walk).")]
    public bool backToNormal = true;

    public override string Summary => $"{N(actor)} faz a animação {N(clip)}{(waitEnd ? "" : " (sem esperar)")}";

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

    public override void Finish(CutsceneDirector d)
    {
        if (actor != null && backToNormal) actor.StopAnimation(0.1f);
    }
}

[Serializable, CutsceneStepInfo("Animação/Estado do Animator", 11)]
public class StateStep : CutsceneStep
{
    public CutsceneActor actor;
    [Tooltip("A state of its Animator by name (Helena: e.g. FloorSit).")]
    public string state = "";
    public float blend = 0.3f;
    [Tooltip("Seconds before the next step.")]
    public float wait = 0f;

    public override string Summary => $"{N(actor)} entra no estado \"{state}\"";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null || string.IsNullOrEmpty(state)) yield break;
        d.Use(actor);
        actor.PlayState(state, blend);
        float t = 0f;
        while (t < wait) { t += Time.deltaTime; yield return null; }
    }
}

[Serializable, CutsceneStepInfo("Animação/Voltar ao normal", 12)]
public class StopAnimationStep : CutsceneStep
{
    public CutsceneActor actor;
    public float blend = 0.3f;

    public override string Summary => $"{N(actor)} volta ao normal";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor != null) actor.StopAnimation(blend);
        yield break;
    }

    public override void Finish(CutsceneDirector d)
    {
        if (actor != null) actor.StopAnimation(0.1f);
    }
}

// ====================================================================== speech
[Serializable, CutsceneStepInfo("Fala/Falar", 20)]
public class SayStep : CutsceneStep
{
    [Tooltip("Who speaks (name and color on the subtitle). Empty = the name below.")]
    public CutsceneActor actor;
    [Tooltip("Name on the subtitle when there is no actor (or to override it).")]
    public string speaker = "";
    [TextArea(2, 5)] public string textPt = "";
    [TextArea(2, 5)] public string textEn = "";
    [Tooltip("Optional recordings.")]
    public AudioClip audioPt;
    public AudioClip audioEn;
    [Tooltip("Wait until the line ends before the next step.")]
    public bool wait = true;
    [Tooltip("Extra pause after the line (s).")]
    public float pauseAfter = 0.2f;

    private string Who => !string.IsNullOrEmpty(speaker) ? speaker : actor != null ? actor.DisplayName : "";

    public override string Summary => $"{(Who.Length > 0 ? Who : "?")}: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

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

    public override void Finish(CutsceneDirector d)
    {
        d.StopVoice();
        d.HideSubtitle();
    }
}

[Serializable, CutsceneStepInfo("Fala/Operador fala (o jogador diz)", 22)]
public class OperatorLineStep : CutsceneStep
{
    [Tooltip("What the operator has to say: shown on the screen; he says it (hold V) or types it (Enter).")]
    [TextArea(2, 4)] public string textPt = "";
    [TextArea(2, 4)] public string textEn = "";
    [Tooltip("Share of the line's words that must be heard (0.6 = most of it). Short words (a, o, de...) don't count.")]
    [Range(0.1f, 1f)] public float match = 0.6f;
    [Tooltip("Anything he says is accepted (only the moment matters).")]
    public bool acceptAnything;
    [Tooltip("After he says it, the line shows as his subtitle.")]
    public bool showSubtitle = true;
    public string speakerPt = "VOCÊ";
    public string speakerEn = "YOU";

    public override string Summary => $"OPERADOR diz: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

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

[Serializable, CutsceneStepInfo("Fala/Fala da Helena (do banco)", 21)]
public class PartnerLineStep : CutsceneStep
{
    [Tooltip("One of her lines (VoiceLineBank: text, recordings, subtitles).")]
    public PartnerLine line = PartnerLine.Acknowledge;
    public bool wait = true;

    public override string Summary => $"Helena diz a fala {line}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        float duration = d.SayPartnerLine(line);
        if (!wait) yield break;
        float t = 0f;
        while (t < duration) { t += Time.deltaTime; yield return null; }
    }
}

// ====================================================================== cameras / screen
[Serializable, CutsceneStepInfo("Câmera/Cortar para câmera", 30)]
public class CameraCutStep : CutsceneStep
{
    [Tooltip("One of the scene's CCTV cameras.")]
    public SecurityCamera camera;

    public override string Summary => $"câmera: {(camera != null ? camera.Label : "(nada)")}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d) => d.CutTo(camera);
}

[Serializable, CutsceneStepInfo("Câmera/Plano livre (de um ponto)", 31)]
public class CameraShotStep : CutsceneStep
{
    [Tooltip("An empty object: the shot is taken from it, looking along its blue arrow. Tip: align it with the " +
             "Scene view (GameObject > Align With View).")]
    public Transform shot;
    [Tooltip("The room shown on the map (optional).")]
    public Room room;
    public string label = "CAM ??";
    [Range(15f, 100f)] public float fieldOfView = 60f;

    public override string Summary => $"plano de {N(shot)} ({label})";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d) => d.Shot(shot, room, label, fieldOfView);
}

[Serializable, CutsceneStepInfo("Tela/Escurecer ou clarear", 32)]
public class FadeStep : CutsceneStep
{
    [Tooltip("On = to black, off = back from black.")]
    public bool toBlack = true;
    public float duration = 1f;

    public override string Summary => toBlack ? $"escurece ({duration:0.#} s)" : $"clareia ({duration:0.#} s)";

    public override IEnumerator Run(CutsceneDirector d)
    {
        yield return d.FadeTo(toBlack ? 1f : 0f, duration);
    }

    public override void Finish(CutsceneDirector d) => d.SetFade(toBlack ? 1f : 0f);
}

// ====================================================================== world
[Serializable, CutsceneStepInfo("Mundo/Porta", 40)]
public class DoorStep : CutsceneStep
{
    public enum DoorAction { Open, Close, Lock, Unlock, UnlockAndOpen }

    public Door door;
    public DoorAction action = DoorAction.Open;

    public override string Summary => $"porta {N(door)}: {action}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

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

[Serializable, CutsceneStepInfo("Mundo/Ativar ou desativar objeto", 41)]
public class SetActiveStep : CutsceneStep
{
    public GameObject target;
    public bool active = true;

    public override string Summary => $"{(active ? "ativa" : "desativa")} {N(target)}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d)
    {
        if (target != null) target.SetActive(active);
    }
}

[Serializable, CutsceneStepInfo("Mundo/Tocar som", 42)]
public class SoundStep : CutsceneStep
{
    public AudioClip clip;
    [Tooltip("Where it sounds (empty = no position, heard the same everywhere).")]
    public Transform at;
    [Range(0f, 1f)] public float volume = 1f;
    public bool wait;

    public override string Summary => $"som {N(clip)}{(at != null ? " em " + at.name : "")}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (clip == null) yield break;
        d.PlaySound(clip, at, volume);
        if (!wait) yield break;
        float t = 0f;
        while (t < clip.length) { t += Time.deltaTime; yield return null; }
    }
}

[Serializable, CutsceneStepInfo("Mundo/Chamar evento (qualquer coisa)", 43)]
public class EventStep : CutsceneStep
{
    [Tooltip("Calls methods of objects in the scene (like a button's On Click).")]
    public UnityEvent onRun = new UnityEvent();

    public override string Summary => $"evento ({onRun.GetPersistentEventCount()} chamada(s))";

    public override IEnumerator Run(CutsceneDirector d)
    {
        onRun.Invoke();
        yield break;
    }

    public override void Finish(CutsceneDirector d) => onRun.Invoke();
}

[Serializable, CutsceneStepInfo("História/Marcar flag", 50)]
public class SetFlagStep : CutsceneStep
{
    [Tooltip("StoryProgress flag (saved; files on the PC and other cutscenes use them).")]
    public string flag = "";

    public override string Summary => $"flag \"{flag}\"";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d)
    {
        if (d.Story != null && !string.IsNullOrEmpty(flag)) d.Story.Set(flag);
    }
}

// ====================================================================== flow
[Serializable, CutsceneStepInfo("Fluxo/Esperar", 60)]
public class WaitStep : CutsceneStep
{
    public float seconds = 1f;

    public override string Summary => $"espera {seconds:0.##} s";

    public override IEnumerator Run(CutsceneDirector d)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
    }
}

[Serializable, CutsceneStepInfo("Fluxo/Ao mesmo tempo (grupo)", 61)]
public class ParallelStep : CutsceneStep
{
    [Tooltip("These run together; the group ends when all have ended.")]
    [SerializeReference] public List<CutsceneStep> steps = new List<CutsceneStep>();

    public override string Summary => $"ao mesmo tempo: {steps.Count} passo(s)";

    [NonSerialized] private bool[] done;

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

    public override void Finish(CutsceneDirector d)
    {
        for (int i = 0; i < steps.Count; i++)
            if (steps[i] != null && (done == null || i >= done.Length || !done[i])) steps[i].Finish(d);
    }
}
