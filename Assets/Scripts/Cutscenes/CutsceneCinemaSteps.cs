using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Steps for the story cutscenes (cinema mode: a scene of its own, a plain camera) - they work in the CCTV cutscenes
// too where it makes sense (thought, scare, loading, sounds, moving objects, lighting).

// ====================================================================== speech
[Serializable, CutsceneStepInfo("Fala/Pensamento (balão)", 23)]
public class ThoughtStep : CutsceneStep
{
    [Tooltip("What the protagonist thinks (shown in the thought balloon, not said).")]
    [TextArea(2, 4)] public string textPt = "";
    [TextArea(2, 4)] public string textEn = "";
    [Tooltip("Wait until it fades before the next step.")]
    public bool wait = true;
    [Tooltip("Extra pause after it (s).")]
    public float pauseAfter = 0.2f;

    public override string Summary => $"PENSAMENTO: \"{Short(!string.IsNullOrEmpty(textPt) ? textPt : textEn)}\"";

    public override IEnumerator Run(CutsceneDirector d)
    {
        bool pt = d.IsPortuguese;
        string text = pt && !string.IsNullOrEmpty(textPt) ? textPt : !string.IsNullOrEmpty(textEn) ? textEn : textPt;
        float duration = d.Think(text);
        if (!wait) yield break;
        float t = 0f;
        while (t < duration + pauseAfter) { t += Time.deltaTime; yield return null; }
    }

    public override void Finish(CutsceneDirector d) => d.HideThought();
}

// ====================================================================== animation
[Serializable, CutsceneStepInfo("Animação/Mãos em alvos (IK)", 13)]
public class HandTargetStep : CutsceneStep
{
    public CutsceneActor actor;
    [Tooltip("Where the left / right hand goes (an empty object: on a steering wheel, a door...). Both empty = hands free again.")]
    public Transform left;
    public Transform right;
    [Range(0f, 1f)] public float weight = 1f;
    [Tooltip("Seconds to get there.")]
    public float blend = 0.4f;

    public override string Summary => left == null && right == null ? $"{N(actor)} solta as mãos" : $"{N(actor)} põe as mãos em {N(left)} / {N(right)}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (actor == null) yield break;
        d.Use(actor);
        actor.SetHandTargets(left, right, weight, blend);
    }

    public override void Finish(CutsceneDirector d)
    {
        if (actor == null) return;
        d.Use(actor);
        actor.SetHandTargets(left, right, weight, 0f);
    }
}

// ====================================================================== camera (cinema mode)
[Serializable, CutsceneStepInfo("Câmera/Movimento de câmera (cinema)", 33)]
public class CameraMoveStep : CutsceneStep
{
    [Tooltip("Where the camera goes (an empty object: position + blue arrow).")]
    public Transform to;
    public float seconds = 2f;
    [Tooltip("Field of view at the end (0 = keep).")]
    [Range(0f, 100f)] public float fieldOfView = 0f;
    [Tooltip("Slow start and end.")]
    public bool smooth = true;
    [Tooltip("Wait for the move to end before the next step.")]
    public bool wait = true;

    public override string Summary => $"câmera vai até {N(to)} ({seconds:0.#} s){(wait ? "" : " (sem esperar)")}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (to == null) yield break;
        if (!wait) { d.StartCoroutine(Move(d)); yield break; }
        yield return Move(d);
    }

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

    public override void Finish(CutsceneDirector d)
    {
        if (to == null) return;
        d.GetCinemaPose(out _, out _, out float f0);
        d.SetCinemaPose(to.position, to.rotation, fieldOfView > 0f ? fieldOfView : f0);
    }
}

[Serializable, CutsceneStepInfo("Câmera/Tremor de mão (cinema)", 34)]
public class HandheldStep : CutsceneStep
{
    [Tooltip("0 = still (a tripod), 1 = normal, 3 = nervous.")]
    [Range(0f, 4f)] public float amount = 1f;

    public override string Summary => $"tremor de mão x{amount:0.#}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

    public override void Finish(CutsceneDirector d) => d.SetHandheld(amount);
}

// ====================================================================== screen
[Serializable, CutsceneStepInfo("Tela/Susto (VFX)", 35)]
public class ScareStep : CutsceneStep
{
    [Tooltip("The scream (or any sound), played at the start.")]
    public AudioClip sound;
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

    public override string Summary => $"SUSTO {N(sound)} ({seconds:0.#} s){(endOnBlack ? " -> preto" : "")}";

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

    public override void Finish(CutsceneDirector d)
    {
        d.SetScare(0f, Color.clear, false);
        d.Shake(0f);
        d.KickFov(0f);
        if (endOnBlack) d.SetFade(1f);
    }
}

[Serializable, CutsceneStepInfo("Tela/Carregamento", 36)]
public class LoadingStep : CutsceneStep
{
    public string textPt = "Carregando...";
    public string textEn = "Loading...";
    [Tooltip("Seconds on screen (over the black: put a fade to black before it).")]
    public float seconds = 2f;

    public override string Summary => $"carregamento ({seconds:0.#} s)";

    public override IEnumerator Run(CutsceneDirector d)
    {
        d.ShowLoading(d.IsPortuguese ? textPt : textEn);
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
        d.HideLoading();
    }

    public override void Finish(CutsceneDirector d) => d.HideLoading();
}

// ====================================================================== world
[Serializable, CutsceneStepInfo("Mundo/Mover objeto (porta, vidro...)", 44)]
public class TransformStep : CutsceneStep
{
    [Tooltip("What moves (a door leaf, a sliding glass...).")]
    public Transform target;
    [Tooltip("Where it ends (an empty object: its position and rotation).")]
    public Transform to;
    public float seconds = 1f;
    public bool smooth = true;
    public bool wait = true;

    public override string Summary => $"move {N(target)} até {N(to)} ({seconds:0.#} s)";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (target == null || to == null) yield break;
        if (!wait) { d.StartCoroutine(Move()); yield break; }
        yield return Move();
    }

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

    public override void Finish(CutsceneDirector d)
    {
        if (target != null && to != null) target.SetPositionAndRotation(to.position, to.rotation);
    }
}

[Serializable, CutsceneStepInfo("Mundo/Som contínuo (ligar ou desligar)", 45)]
public class SoundLoopStep : CutsceneStep
{
    [Tooltip("An AudioSource of the scene (its clip, loop on).")]
    public AudioSource source;
    public bool play = true;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Fade in / out (s).")]
    public float fade = 0.8f;

    public override string Summary => $"{(play ? "liga" : "desliga")} som {N(source)}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        if (source == null) yield break;
        float from = source.isPlaying ? source.volume : 0f;
        float to = play ? volume : 0f;
        if (play && !source.isPlaying) { source.volume = 0f; source.Play(); }
        d.StartCoroutine(Ramp(from, to));
    }

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

    public override void Finish(CutsceneDirector d)
    {
        if (source == null) return;
        source.volume = play ? volume : 0f;
        if (play && !source.isPlaying) source.Play();
        if (!play) source.Stop();
    }
}

[Serializable, CutsceneStepInfo("Mundo/Ambiente (luz, céu, névoa)", 46)]
public class LightingStep : CutsceneStep
{
    [Tooltip("The sun / moon (a directional light).")]
    public Light sun;
    public Vector3 sunAngles = new Vector3(40f, -30f, 0f);
    public Color sunColor = new Color(1f, 0.92f, 0.8f);
    public float sunIntensity = 1f;
    public Color ambient = new Color(0.45f, 0.47f, 0.5f);
    [Tooltip("The sky (the camera's background color, cinema mode).")]
    public Color sky = new Color(0.62f, 0.68f, 0.74f);
    public bool fog = true;
    public Color fogColor = new Color(0.62f, 0.68f, 0.74f);
    public float fogStart = 15f;
    public float fogEnd = 90f;
    [Tooltip("Blend time (0 = at once: a cut).")]
    public float seconds = 0f;

    public override string Summary => $"ambiente: sol {sunIntensity:0.##}, névoa {(fog ? fogEnd.ToString("0") + " m" : "não")}";

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

    public override void Finish(CutsceneDirector d)
    {
        Apply(d, Quaternion.Euler(sunAngles), sunColor, sunIntensity, ambient, sky, fogColor, fogStart, fogEnd);
        RenderSettings.fog = fog;
    }

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
[Serializable, CutsceneStepInfo("Fluxo/Carregar cena", 62)]
public class LoadSceneStep : CutsceneStep
{
    [Tooltip("The scene to open (by name, it must be in File > Build Profiles). Empty = nothing (stays here).")]
    public string sceneName = "";

    public override string Summary => string.IsNullOrEmpty(sceneName) ? "carregar cena: (nenhuma)" : $"carregar cena {sceneName}";

    public override IEnumerator Run(CutsceneDirector d)
    {
        Finish(d);
        yield break;
    }

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
