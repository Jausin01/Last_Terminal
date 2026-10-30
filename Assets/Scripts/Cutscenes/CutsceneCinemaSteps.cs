using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Steps for the story cutscenes (cinema mode: a scene of its own, a plain camera) - they work in the CCTV cutscenes
// too where it makes sense (thought, scare, loading, sounds, moving objects, lighting).

// ====================================================================== speech
[Serializable, CutsceneStepInfo("Speech/Thought (bubble)", 23)]
public class ThoughtStep : CutsceneStep
{
    [Tooltip("What the protagonist thinks (shown in the thoughts block, top-left, not said).")]
    [TextArea(2, 4)] public string textPt = "";
    // The same thought in English.
    [TextArea(2, 4)] public string textEn = "";
    [Tooltip("Wait until it fades before the next step.")]
    public bool wait = true;
    [Tooltip("Extra pause after it (s).")]
    public float pauseAfter = 0.2f;

    // One-line description shown in the cutscene's step list.
    public override string Summary => $"THOUGHT: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

    // Shows the thought (in the game's language) and waits for it to fade.
    public override IEnumerator Run(CutsceneDirector d)
    {
        bool pt = d.IsPortuguese;
        string text = pt && !string.IsNullOrEmpty(textPt) ? textPt : !string.IsNullOrEmpty(textEn) ? textEn : textPt;
        float duration = d.Think(text);
        if (!wait) yield break;
        float t = 0f;
        while (t < duration + pauseAfter) { t += Time.deltaTime; yield return null; }
    }

    // Skipped: the thought disappears.
    public override void Finish(CutsceneDirector d) => d.HideThought();
}

// ====================================================================== animation
[Serializable, CutsceneStepInfo("Animation/Hands on targets (IK)", 13)]
public class HandTargetStep : CutsceneStep
{
    // Who moves the hands.
    public CutsceneActor actor;
    [Tooltip("Where the left / right hand goes (an empty object: on a steering wheel, a door...). Both empty = hands free again.")]
    public Transform left;
    // Right hand target.
    public Transform right;
    // IK weight at full blend.
    [Range(0f, 1f)] public float weight = 1f;
    [Tooltip("Seconds to get there.")]
    public float blend = 0.4f;
    [Tooltip("The hand also turns to the target's rotation (blue arrow = where the fingers point, green = the back of the hand).")]
    public bool orient;

    // Step list description.
    public override string Summary => left == null && right == null ? $"{N(actor)} lets go with the hands" : $"{N(actor)} puts the hands on {N(left)} / {N(right)}";

    // Puts the hands on the targets (blending).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null) yield break;
        d.Use(actor);
        actor.SetHandTargets(left, right, weight, blend, orient);
    }

    // Skipped: hands on the targets at once.
    public override void Finish(CutsceneDirector d)
    {
        if (actor == null) return;
        d.Use(actor);
        actor.SetHandTargets(left, right, weight, 0f, orient);
    }
}

[Serializable, CutsceneStepInfo("Animation/Look at (head IK)", 14)]
public class LookStep : CutsceneStep
{
    // Who looks.
    public CutsceneActor actor;
    [Tooltip("What it looks at (an empty object, a person's head...). Empty = back to the animation.")]
    public Transform target;
    // IK weight at full blend.
    [Range(0f, 1f)] public float weight = 0.9f;
    [Tooltip("Seconds to turn the head.")]
    public float blend = 0.6f;

    // Step list description.
    public override string Summary => target == null ? $"{N(actor)} stops looking" : $"{N(actor)} looks at {N(target)}";

    // Turns the head (blending; the step does not wait).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null) yield break;
        d.Use(actor);
        actor.SetLookTarget(target, weight, blend);
    }

    // Skipped: looking there at once.
    public override void Finish(CutsceneDirector d)
    {
        if (actor == null) return;
        d.Use(actor);
        actor.SetLookTarget(target, weight, 0f);
    }
}

// ====================================================================== camera (cinema mode)
[Serializable, CutsceneStepInfo("Camera/Camera move (cinema)", 33)]
public class CameraMoveStep : CutsceneStep
{
    [Tooltip("Where the camera goes (an empty object: position + blue arrow).")]
    public Transform to;
    // Duration of the move (s).
    public float seconds = 2f;
    [Tooltip("Field of view at the end (0 = keep).")]
    [Range(0f, 100f)] public float fieldOfView = 0f;
    [Tooltip("Slow start and end.")]
    public bool smooth = true;
    [Tooltip("Wait for the move to end before the next step.")]
    public bool wait = true;

    // Step list description.
    public override string Summary => $"camera moves to {N(to)} ({seconds:0.#} s){(wait ? "" : " (no wait)")}";

    // Moves the cinema camera to the target pose (optionally without waiting).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (to == null) yield break;
        if (!wait) { d.StartCoroutine(Move(d)); yield break; }
        yield return Move(d);
    }

    // The move itself: position, rotation and field of view eased over the duration.
    private IEnumerator Move(CutsceneDirector d)
    {
        d.GetCinemaPose(out Vector3 p0, out Quaternion r0, out float f0);
        float f1 = fieldOfView > 0f ? fieldOfView : f0;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(seconds > 0f ? t / seconds : 1f);
            if (smooth) k = k * k * (3f - 2f * k);
            d.SetCinemaPose(Vector3.Lerp(p0, to.position, k), Quaternion.Slerp(r0, to.rotation, k), Mathf.Lerp(f0, f1, k));
            yield return null;
        }
        d.SetCinemaPose(to.position, to.rotation, f1);
    }

    // Skipped: the camera jumps to the end pose.
    public override void Finish(CutsceneDirector d)
    {
        if (to == null) return;
        d.GetCinemaPose(out _, out _, out float f0);
        d.SetCinemaPose(to.position, to.rotation, fieldOfView > 0f ? fieldOfView : f0);
    }
}

// Changes the handheld shake of the cinema camera.
[Serializable, CutsceneStepInfo("Camera/Handheld shake (cinema)", 34)]
public class HandheldStep : CutsceneStep
{
    [Tooltip("0 = still (a tripod), 1 = normal, 3 = nervous.")]
    [Range(0f, 4f)] public float amount = 1f;

    // Step list description.
    public override string Summary => $"handheld shake x{amount:0.#}";

    // Applies the shake amount (instant).
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Sets the shake.
    public override void Finish(CutsceneDirector d) => d.SetHandheld(amount);
}

// ====================================================================== screen
[Serializable, CutsceneStepInfo("Screen/Scare (VFX)", 35)]
public class ScareStep : CutsceneStep
{
    [Tooltip("The scream (or any sound), played at the start.")]
    public AudioClip sound;
    // Sound volume, duration of the effect and flash color.
    [Range(0f, 1f)] public float volume = 1f;
    public float seconds = 1.2f;
    public Color flashColor = new Color(1f, 0.12f, 0.08f);
    [Tooltip("Strongest flash (alpha).")]
    [Range(0f, 1f)] public float flash = 0.85f;
    [Tooltip("Static noise over the image.")]
    public bool noise = true;
    [Tooltip("Camera shake (degrees, cinema mode).")]
    [Range(0f, 10f)] public float shake = 3f;
    [Tooltip("Zoom kick (field of view added; negative = closer).")]
    [Range(-40f, 20f)] public float zoomKick = -14f;
    [Tooltip("At the end: hard cut to black.")]
    public bool endOnBlack = true;

    // Step list description.
    public override string Summary => $"SCARE {N(sound)} ({seconds:0.#} s){(endOnBlack ? " -> black" : "")}";

    // The scare: a spike then a flickering decay of flash, noise, shake and zoom.
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (sound != null) d.PlaySound(sound, null, volume);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(seconds, 0.01f));
            // a spike, then a flickering decay
            float strength = t < 0.08f ? 1f : (1f - k) * (0.55f + 0.45f * Mathf.PerlinNoise(t * 22f, 0.5f));
            d.SetScare(flash * strength, flashColor, noise);
            d.Shake(shake * strength);
            d.KickFov(zoomKick * Mathf.Min(1f, t / 0.12f) * (1f - k * 0.5f));
            yield return null;
        }
        Finish(d);
    }

    // End of the scare: effects off (optionally on black).
    public override void Finish(CutsceneDirector d)
    {
        d.SetScare(0f, Color.clear, false);
        d.Shake(0f);
        d.KickFov(0f);
        if (endOnBlack) d.SetFade(1f);
    }
}

// A "Loading..." text on the screen for a while.
[Serializable, CutsceneStepInfo("Screen/Loading", 36)]
public class LoadingStep : CutsceneStep
{
    // The text in each language.
    public string textPt = "Carregando...";
    public string textEn = "Loading...";
    [Tooltip("Seconds on screen (over the black: put a fade to black before it).")]
    public float seconds = 2f;

    // Step list description.
    public override string Summary => $"loading ({seconds:0.#} s)";

    // Shows the text for the duration.
    public override IEnumerator Run(CutsceneDirector d)
    {
        d.ShowLoading(d.IsPortuguese ? textPt : textEn);
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
        d.HideLoading();
    }

    // Skipped: hides the text.
    public override void Finish(CutsceneDirector d) => d.HideLoading();
}

// ====================================================================== world
[Serializable, CutsceneStepInfo("World/Move object (door, glass...)", 44)]
public class TransformStep : CutsceneStep
{
    [Tooltip("What moves (a door leaf, a sliding glass...).")]
    public Transform target;
    [Tooltip("Where it ends (an empty object: its position and rotation).")]
    public Transform to;
    // Duration, easing and whether the next step waits for it.
    public float seconds = 1f;
    public bool smooth = true;
    public bool wait = true;

    // Step list description.
    public override string Summary => $"move {N(target)} to {N(to)} ({seconds:0.#} s)";

    // Moves the object (optionally without waiting).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (target == null || to == null) yield break;
        if (!wait) { d.StartCoroutine(Move()); yield break; }
        yield return Move();
    }

    // The move itself, eased.
    private IEnumerator Move()
    {
        Vector3 p0 = target.position;
        Quaternion r0 = target.rotation;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(seconds > 0f ? t / seconds : 1f);
            if (smooth) k = k * k * (3f - 2f * k);
            target.SetPositionAndRotation(Vector3.Lerp(p0, to.position, k), Quaternion.Slerp(r0, to.rotation, k));
            yield return null;
        }
        target.SetPositionAndRotation(to.position, to.rotation);
    }

    // Skipped: the object jumps to the end pose.
    public override void Finish(CutsceneDirector d)
    {
        if (target != null && to != null) target.SetPositionAndRotation(to.position, to.rotation);
    }
}

// Starts / stops a looping sound of the scene with a fade.
[Serializable, CutsceneStepInfo("World/Looping sound (on or off)", 45)]
public class SoundLoopStep : CutsceneStep
{
    [Tooltip("An AudioSource of the scene (its clip, loop on).")]
    public AudioSource source;
    // On or off, and the target volume.
    public bool play = true;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Fade in / out (s).")]
    public float fade = 0.8f;

    // Step list description.
    public override string Summary => $"sound {N(source)} {(play ? "on" : "off")}";

    // Starts the fade (the step does not wait for it).
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (source == null) yield break;
        float from = source.isPlaying ? source.volume : 0f;
        float to = play ? volume : 0f;
        if (play && !source.isPlaying) { source.volume = 0f; source.Play(); }
        d.StartCoroutine(Ramp(from, to));
    }

    // Fades the volume; stops the source at the end when turning off.
    private IEnumerator Ramp(float from, float to)
    {
        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            if (source == null) yield break;
            source.volume = Mathf.Lerp(from, to, fade > 0f ? t / fade : 1f);
            yield return null;
        }
        source.volume = to;
        if (!play) source.Stop();
    }

    // Skipped: the final volume at once.
    public override void Finish(CutsceneDirector d)
    {
        if (source == null) return;
        source.volume = play ? volume : 0f;
        if (play && !source.isPlaying) source.Play();
        if (!play) source.Stop();
    }
}

// Changes the environment: sun, ambient light, sky color and fog (blended or a cut).
[Serializable, CutsceneStepInfo("World/Environment (light, sky, fog)", 46)]
public class LightingStep : CutsceneStep
{
    [Tooltip("The sun / moon (a directional light).")]
    public Light sun;
    // The sun's rotation, color and intensity, and the ambient light color.
    public Vector3 sunAngles = new Vector3(40f, -30f, 0f);
    public Color sunColor = new Color(1f, 0.92f, 0.8f);
    public float sunIntensity = 1f;
    public Color ambient = new Color(0.45f, 0.47f, 0.5f);
    [Tooltip("The sky (the camera's background color, cinema mode).")]
    public Color sky = new Color(0.62f, 0.68f, 0.74f);
    // Fog on / off, color and range.
    public bool fog = true;
    public Color fogColor = new Color(0.62f, 0.68f, 0.74f);
    public float fogStart = 15f;
    public float fogEnd = 90f;
    [Tooltip("Blend time (0 = at once: a cut).")]
    public float seconds = 0f;

    // Step list description.
    public override string Summary => $"environment: sun {sunIntensity:0.##}, fog {(fog ? fogEnd.ToString("0") + " m" : "off")}";

    // Blends from the current environment to this one over the duration.
    public override IEnumerator Run(CutsceneDirector d)
    {
        if (seconds <= 0f) { Finish(d); yield break; }
        Quaternion r0 = sun != null ? sun.transform.rotation : Quaternion.identity;
        Color c0 = sun != null ? sun.color : Color.white;
        float i0 = sun != null ? sun.intensity : 0f;
        Color a0 = RenderSettings.ambientLight, f0 = RenderSettings.fogColor;
        float fs0 = RenderSettings.fogStartDistance, fe0 = RenderSettings.fogEndDistance;
        Camera cam = d.CinemaCamera;
        Color s0 = cam != null ? cam.backgroundColor : sky;
        RenderSettings.fog = fog || RenderSettings.fog;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            Apply(d, Quaternion.Slerp(r0, Quaternion.Euler(sunAngles), k), Color.Lerp(c0, sunColor, k), Mathf.Lerp(i0, sunIntensity, k),
                  Color.Lerp(a0, ambient, k), Color.Lerp(s0, sky, k), Color.Lerp(f0, fogColor, k), Mathf.Lerp(fs0, fogStart, k), Mathf.Lerp(fe0, fogEnd, k));
            yield return null;
        }
        Finish(d);
    }

    // Skipped / end: the final environment at once.
    public override void Finish(CutsceneDirector d)
    {
        Apply(d, Quaternion.Euler(sunAngles), sunColor, sunIntensity, ambient, sky, fogColor, fogStart, fogEnd);
        RenderSettings.fog = fog;
    }

    // Applies one environment state to the sun, RenderSettings and the cinema camera.
    private void Apply(CutsceneDirector d, Quaternion sunRot, Color sunCol, float sunInt, Color amb, Color skyCol, Color fogCol, float fs, float fe)
    {
        if (sun != null)
        {
            sun.transform.rotation = sunRot;
            sun.color = sunCol;
            sun.intensity = sunInt;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = amb;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogCol;
        RenderSettings.fogStartDistance = fs;
        RenderSettings.fogEndDistance = fe;
        Camera cam = d.CinemaCamera;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyCol;
        }
    }
}

// ====================================================================== flow
[Serializable, CutsceneStepInfo("Flow/Load scene", 62)]
public class LoadSceneStep : CutsceneStep
{
    [Tooltip("The scene to open (by name, it must be in File > Build Profiles). Empty = nothing (stays here).")]
    public string sceneName = "";

    // Step list description.
    public override string Summary => string.IsNullOrEmpty(sceneName) ? "load scene: (none)" : $"load scene {sceneName}";

    // Loads the scene (immediately).
    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    // Loads the scene if it is in the build (else a warning).
    public override void Finish(CutsceneDirector d)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[Cutscene] Scene \"{sceneName}\" is not in the build profile: add it to load it.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
