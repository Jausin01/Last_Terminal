using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the scene's cutscenes (one at a time), seen through the operator's cameras: the operator only watches
// (camera system locked: no keys, map, clicks, commands), black bars, subtitles with the speaker's name, Esc skips
// (every remaining step jumps to its end). At the end: the camera from before, the actors back to normal, the
// cutscene's flag set (StoryProgress, saved: a cutscene played once isn't played again).
// A cutscene only starts with the operator at the camera program, Helena out of a fight and free.
// Cinema mode (a scene without the camera program, e.g. the story cutscenes before the facility): no camera system,
// a plain Camera instead (cinemaCamera) moved by the shots, with a light handheld sway; thoughts, loading screens
// and the scare effect work in both modes.
public class CutsceneDirector : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string CameraSystemField = nameof(cameraSystem);
    public const string ShotCameraField = nameof(shotCamera);
    public const string StationField = nameof(station);
    public const string EncounterField = nameof(encounter);
    public const string HelenaField = nameof(helena);
    public const string HelenaAnimatorField = nameof(helenaAnimator);
    public const string HelenaVoiceField = nameof(helenaVoice);
    public const string StoryField = nameof(story);
    public const string SettingsField = nameof(settings);
    public const string AudioField = nameof(audioSource);
    public const string BarsField = nameof(bars);
    public const string FadeField = nameof(fade);
    public const string SubtitleGroupField = nameof(subtitleGroup);
    public const string SubtitleTextField = nameof(subtitleText);
    public const string SkipHintField = nameof(skipHint);
    public const string VoiceInputField = nameof(voiceInput);
    public const string ConsoleField = nameof(console);
    public const string PromptGroupField = nameof(promptGroup);
    public const string PromptTextField = nameof(promptText);
    public const string CinemaCameraField = nameof(cinemaCamera);
    public const string ThoughtViewField = nameof(thoughtView);
    public const string ThoughtGroupField = nameof(thoughtGroup);
    public const string ThoughtTextField = nameof(thoughtText);
    public const string LoadingGroupField = nameof(loadingGroup);
    public const string LoadingTextField = nameof(loadingText);
    public const string ScareGroupField = nameof(scareGroup);
    public const string ScareTintField = nameof(scareTint);
    public const string ScareNoiseField = nameof(scareNoise);

    // Story flag prefix of a played cutscene (cutscene_<id>).
    public const string PlayedPrefix = "cutscene_";

    [Tooltip("The operator's cameras. Empty = cinema mode (the Cinema Camera below).")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [Tooltip("Cinema mode: the camera the shots move (no camera system in the scene).")]
    [SerializeField] private Camera cinemaCamera;
    [Tooltip("Cinema mode: handheld sway (degrees).")]
    [SerializeField, Range(0f, 2f)] private float handheld = 0.35f;
    [Tooltip("A camera without a model, one of the camera system's (off outside cutscenes): free shots.")]
    [SerializeField] private SecurityCamera shotCamera;
    [Tooltip("Optional: cutscenes wait for the operator to be at the camera program.")]
    [SerializeField] private OperatorStation station;
    [Tooltip("Optional: cutscenes wait for the fight to end.")]
    [SerializeField] private CombatEncounter encounter;
    // Helena (as an actor, her animator and her voice), the story flags, the settings and the voice / sound source.
    [SerializeField] private CutsceneActor helena;
    [SerializeField] private PartnerAnimator helenaAnimator;
    [SerializeField] private PartnerVoice helenaVoice;
    [SerializeField] private StoryProgress story;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The operator's microphone and command line: what he says in a cutscene (OperatorLineStep).")]
    [SerializeField] private VoiceCommandInput voiceInput;
    // The typed command line (it takes the operator's line too).
    [SerializeField] private CommandConsole console;

    [Header("Screen")]
    [Tooltip("The black bars (alpha).")]
    [SerializeField] private CanvasGroup bars;
    // Full-screen fade, subtitles and the "Esc: skip" hint.
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text skipHint;
    [Tooltip("\"Say: ...\" while the operator has a line.")]
    [SerializeField] private CanvasGroup promptGroup;
    // The operator's line to say.
    [SerializeField] private TMP_Text promptText;
    [Tooltip("The thoughts block in the top-left corner (ThoughtStep).")]
    [SerializeField] private ThoughtView thoughtView;
    [Tooltip("Old thought balloon: used only when there is no Thought View.")]
    [SerializeField] private CanvasGroup thoughtGroup;
    // Text of the old balloon.
    [SerializeField] private TMP_Text thoughtText;
    [Tooltip("\"Loading...\" over the black (LoadingStep).")]
    [SerializeField] private CanvasGroup loadingGroup;
    // The loading text.
    [SerializeField] private TMP_Text loadingText;
    [Tooltip("Scare effect (ScareStep): a colored flash and static noise.")]
    [SerializeField] private CanvasGroup scareGroup;
    // The scare overlay's tint and noise images.
    [SerializeField] private UnityEngine.UI.Image scareTint;
    [SerializeField] private UnityEngine.UI.RawImage scareNoise;

    // Esc skips the cutscene.
    [Header("Input")]
    [SerializeField] private InputAction skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");

    // Actors used in the current cutscene (put back at the end) and whether a skip was asked.
    private readonly HashSet<CutsceneActor> used = new HashSet<CutsceneActor>();
    private bool skipRequested;
    // Screen state: subtitle time left, black bars target, how long Helena's current line lasts.
    private float subtitleTimer;
    private float barsTarget;
    private float partnerLineDuration;
    // The operator's line: what was heard, and whether we are listening for it.
    private string heardLine;
    private bool listening;
    // Time left on the old thought balloon.
    private float thoughtTimer;
    // cinema camera: the shot's pose, then sway / shake / zoom kick added each frame
    private Vector3 camPos;
    private Quaternion camRot = Quaternion.identity;
    private float camFov = 60f;
    private float shake;
    private float fovKick;
    private float swayScale = 1f;
    private Texture2D noiseTex;

    // State read by the steps, triggers and other systems.
    public bool IsPlaying { get; private set; }
    public Cutscene Current { get; private set; }
    public StoryProgress Story => story;
    public Transform Partner => helena != null ? helena.transform : null;
    public CutsceneActor Helena => helena;
    public bool IsPortuguese => settings != null && settings.Language == GameLanguage.Portuguese;
    public bool IsCinema => cameraSystem == null && cinemaCamera != null;
    public Camera CinemaCamera => cinemaCamera;

    // Raised when a cutscene starts / ends (music, HUD, the station listen).
    public event Action<Cutscene> Started;
    public event Action<Cutscene> Finished;

    // the operator is watching, she is free
    public bool CanStart => !IsPlaying && (IsCinema || CanStartOperator);

    // Operator mode: he is at the camera program, no fight, and Helena is free (not scripted, dead, picking up or attacking).
    private bool CanStartOperator =>
        cameraSystem != null && cameraSystem.isActiveAndEnabled
        && (station == null || station.Current == OperatorStation.Mode.Operator)
        && (encounter == null || !encounter.InCombat)
        && (helenaAnimator == null || ((!helenaAnimator.IsScripted || (helena != null && helena.IsResting)) && !helenaAnimator.IsDead && !helenaAnimator.IsPickingUp && !helenaAnimator.IsAttacking));

    // Checks the references and hides every overlay.
    private void Awake()
    {
        if (cameraSystem == null && cinemaCamera == null)
        {
            Debug.LogError($"{name}: CutsceneDirector needs the camera system or a cinema camera (run the Last Terminal menu).", this);
            enabled = false;
            return;
        }
        if (cinemaCamera != null)
        {
            camPos = cinemaCamera.transform.position;
            camRot = cinemaCamera.transform.rotation;
            camFov = cinemaCamera.fieldOfView;
        }
        if (thoughtGroup != null) thoughtGroup.alpha = 0f;
        if (loadingGroup != null) loadingGroup.alpha = 0f;
        if (scareGroup != null) scareGroup.alpha = 0f;
        if (shotCamera != null) shotCamera.gameObject.SetActive(false);
        if (bars != null) bars.alpha = 0f;
        if (fade != null) fade.alpha = 0f;
        if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        if (skipHint != null) skipHint.text = "";
        if (promptGroup != null) promptGroup.alpha = 0f;
    }

    // Listens to Esc and to Helena's lines (their length).
    private void OnEnable()
    {
        skip.Enable();
        skip.performed += OnSkip;
        if (helenaVoice != null) helenaVoice.LineStarted += OnPartnerLine;
    }

    // Stops listening.
    private void OnDisable()
    {
        skip.performed -= OnSkip;
        skip.Disable();
        if (helenaVoice != null) helenaVoice.LineStarted -= OnPartnerLine;
    }

    // Esc: skip the rest (if the cutscene allows it).
    private void OnSkip(InputAction.CallbackContext ctx)
    {
        if (IsPlaying && Current != null && Current.Skippable) skipRequested = true;
    }

    // Helena started a line: remember how long it lasts.
    private void OnPartnerLine(string text, float seconds) => partnerLineDuration = seconds;

    // Starts a cutscene if none is playing and it can start now.
    public bool Play(Cutscene cutscene)
    {
        if (cutscene == null || !CanStart) return false;
        StartCoroutine(Run(cutscene));
        return true;
    }

    // ------------------------------------------------------------------ the run
    private IEnumerator Run(Cutscene cutscene)
    {
        IsPlaying = true;
        Current = cutscene;
        skipRequested = false;
        used.Clear();
        SecurityCamera before = cameraSystem != null ? cameraSystem.Active : null;
        if (cameraSystem != null) cameraSystem.SetLocked(true);
        if (helena != null) Use(helena);
        barsTarget = cutscene.Letterbox ? 1f : 0f;
        if (skipHint != null) skipHint.text = cutscene.Skippable ? (IsPortuguese ? "Esc: pular" : "Esc: skip") : "";
        Started?.Invoke(cutscene);

        foreach (CutsceneStep step in cutscene.Steps)
        {
            if (step == null) continue;
            if (skipRequested) { SafeFinish(step); continue; }
            var stack = new Stack<IEnumerator>();
            stack.Push(step.Run(this));
            while (SafeAdvance(stack, step))
            {
                if (skipRequested) { SafeFinish(step); break; }
                yield return null;
            }
        }

        // the end: everything back to the operator
        EndListening();
        StopVoice();
        HideSubtitle();
        HideThought();
        HideLoading();
        SetScare(0f, Color.clear, false);
        shake = 0f;
        fovKick = 0f;
        if (skipHint != null) skipHint.text = "";
        if (cameraSystem != null)
        {
            bool onShot = shotCamera != null && cameraSystem.Active == shotCamera;
            if ((cutscene.RestoreCamera || onShot) && before != null && before != shotCamera) cameraSystem.Switch(before);
            if (shotCamera != null && cameraSystem.Active != shotCamera) shotCamera.gameObject.SetActive(false);
        }
        foreach (CutsceneActor actor in used) if (actor != null) actor.EndCutscene();
        used.Clear();
        barsTarget = 0f;
        if (fade != null && fade.alpha > 0f && cutscene.ClearScreenAtEnd) yield return FadeTo(0f, 0.5f);
        if (cameraSystem != null) cameraSystem.SetLocked(false);
        if (story != null) story.Set(cutscene.PlayedFlag);
        IsPlaying = false;
        Current = null;
        Finished?.Invoke(cutscene);
    }

    // one step of nested coroutines (a step may yield another IEnumerator: it runs inside); false = finished
    public static bool Advance(Stack<IEnumerator> stack)
    {
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (top.MoveNext())
            {
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return true;
            }
            stack.Pop();
        }
        return false;
    }

    // a broken step (missing reference, an exception) doesn't stop the cutscene
    private bool SafeAdvance(Stack<IEnumerator> stack, CutsceneStep step)
    {
        try { return Advance(stack); }
        catch (Exception e) { Debug.LogException(e, this); Debug.LogWarning($"[Cutscene] step failed: {step.Summary}", this); return false; }
    }

    // Ends a step at once, logging (not throwing) a failure.
    private void SafeFinish(CutsceneStep step)
    {
        try { step.Finish(this); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    // an actor of this cutscene (made ready once, put back at the end)
    public void Use(CutsceneActor actor)
    {
        if (actor == null || !used.Add(actor)) return;
        actor.BeginCutscene();
    }

    // ------------------------------------------------------------------ what the steps use
    // a subtitle (and its recording): seconds it lasts
    public float Say(string speaker, Color color, string text, AudioClip clip)
    {
        float duration = clip != null ? clip.length + 0.3f : Mathf.Max(1.6f, (text != null ? text.Length : 0) * 0.055f);
        if (audioSource != null)
        {
            audioSource.Stop();
            if (clip != null) { audioSource.clip = clip; audioSource.Play(); }
        }
        if (subtitleText != null && (settings == null || settings.SubtitlesEnabled))
        {
            string name = string.IsNullOrEmpty(speaker) ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{speaker}:</color> ";
            subtitleText.text = name + text;
            subtitleTimer = duration;
        }
        return duration;
    }

    // Helena says one of her voice lines; returns how long it lasts.
    public float SayPartnerLine(PartnerLine line)
    {
        if (helenaVoice == null) return 0f;
        partnerLineDuration = 2f;
        helenaVoice.Say(line);
        return partnerLineDuration;
    }

    // ------------------------------------------------------------------ the operator's line (OperatorLineStep)
    // shows what to say; what he says (microphone) or types (Enter) comes back through TryTakeHeard
    public void BeginListening(string line)
    {
        listening = true;
        heardLine = null;
        ShowPrompt(line, null);
        if (voiceInput != null) voiceInput.BeginOverride(OnOperatorSaid, line);
        if (console != null) console.BeginOverride(OnOperatorSaid);
    }

    // What the operator said / typed while a line was expected.
    private void OnOperatorSaid(string text)
    {
        if (listening) heardLine = text;
    }

    // Takes the line the operator said (once).
    public bool TryTakeHeard(out string text)
    {
        text = heardLine;
        heardLine = null;
        return text != null;
    }

    // not what the line says: try again (shows what was heard)
    public void ShowPrompt(string line, string heardWrong)
    {
        if (promptText == null) return;
        bool pt = IsPortuguese;
        string say = pt ? "DIGA" : "SAY";
        string how = pt ? "segure V e fale · ou Enter para digitar" : "hold V and talk · or Enter to type";
        string wrong = string.IsNullOrEmpty(heardWrong) ? "" : $"\n<size=70%><color=#FF8080>{(pt ? "ouvi" : "heard")}: \"{heardWrong}\"</color></size>";
        promptText.text = $"<color=#9AD0FF>{say}:</color> \"{line}\"\n<size=60%><color=#9A9A9A>{how}</color></size>{wrong}";
        if (promptGroup != null) promptGroup.alpha = 1f;
    }

    // Stops listening for the operator's line.
    public void EndListening()
    {
        if (!listening) return;
        listening = false;
        heardLine = null;
        if (promptGroup != null) promptGroup.alpha = 0f;
        if (voiceInput != null) voiceInput.EndOverride();
        if (console != null) console.EndOverride();
    }

    // Stops the voice recording.
    public void StopVoice()
    {
        if (audioSource != null) audioSource.Stop();
    }

    // Hides the subtitle.
    public void HideSubtitle() => subtitleTimer = 0f;

    // Cuts the operator's view to a security camera.
    public void CutTo(SecurityCamera cam)
    {
        if (cam == null || cameraSystem == null) return;
        cam.gameObject.SetActive(true);
        cameraSystem.Switch(cam);
        if (shotCamera != null && cam != shotCamera) shotCamera.gameObject.SetActive(false);
    }

    // A free shot: the cinema camera (cinema mode) or the model-less shot camera framed from a transform.
    public void Shot(Transform from, Room room, string label, float fieldOfView)
    {
        if (from == null) return;
        if (IsCinema) { SetCinemaPose(from.position, from.rotation, fieldOfView); return; }
        if (shotCamera == null || cameraSystem == null) return;
        bool wasActive = cameraSystem.Active == shotCamera;
        shotCamera.gameObject.SetActive(true);
        shotCamera.SetLabel(string.IsNullOrEmpty(label) ? "CAM ??" : label);
        shotCamera.SetFieldOfView(fieldOfView);
        shotCamera.Frame(from.position, from.position + from.forward * 5f, room);
        if (wasActive)
        {
            // already on it: switch away and back so the view and the HUD take the new shot
            SecurityCamera other = null;
            foreach (SecurityCamera c in cameraSystem.Cameras) if (c != null && c != shotCamera && c.gameObject.activeInHierarchy) { other = c; break; }
            if (other != null) cameraSystem.Switch(other);
        }
        cameraSystem.Switch(shotCamera);
    }

    // Fades the black screen to an alpha over time.
    public IEnumerator FadeTo(float alpha, float duration)
    {
        if (fade == null) yield break;
        float from = fade.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            fade.alpha = Mathf.Lerp(from, alpha, duration > 0f ? t / duration : 1f);
            yield return null;
        }
        fade.alpha = alpha;
    }

    // Sets the black screen alpha at once.
    public void SetFade(float alpha)
    {
        if (fade != null) fade.alpha = alpha;
    }

    // Plays a sound (at a point in the world, or on the director's source).
    public void PlaySound(AudioClip clip, Transform at, float volume)
    {
        if (clip == null) return;
        if (at != null) AudioSource.PlayClipAtPoint(clip, at.position, volume);
        else if (audioSource != null) audioSource.PlayOneShot(clip, volume);
    }

    // ------------------------------------------------------------------ thoughts, loading, scare
    // a thought (the protagonist thinks it): seconds it lasts
    public float Think(string text)
    {
        if (thoughtView != null) return thoughtView.Show(text);
        float duration = Mathf.Max(1.8f, (text != null ? text.Length : 0) * 0.06f);
        if (thoughtText != null)
        {
            thoughtText.text = $"<i>{text}</i>";
            thoughtTimer = duration;
        }
        return duration;
    }

    // Hides the thought.
    public void HideThought()
    {
        thoughtTimer = 0f;
        if (thoughtView != null) thoughtView.Hide();
    }

    // Shows / hides the loading text.
    public void ShowLoading(string text)
    {
        if (loadingText != null) loadingText.text = text;
        if (loadingGroup != null) loadingGroup.alpha = 1f;
    }

    // Hides the loading screen.
    public void HideLoading()
    {
        if (loadingGroup != null) loadingGroup.alpha = 0f;
    }

    // the scare overlay: tint (alpha = strength), static noise on top
    public void SetScare(float alpha, Color tint, bool noise)
    {
        if (scareGroup == null) return;
        scareGroup.alpha = alpha;
        if (scareTint != null) scareTint.color = tint;
        if (scareNoise == null) return;
        scareNoise.enabled = noise && alpha > 0f;
        if (!scareNoise.enabled) return;
        if (noiseTex == null) noiseTex = MakeNoise();
        scareNoise.texture = noiseTex;
        scareNoise.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 1.6f, 0.9f);
    }

    // A 128x128 grey static noise texture (made once).
    private static Texture2D MakeNoise()
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[128 * 128];
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)UnityEngine.Random.Range(0, 256);
            px[i] = new Color32(v, v, v, (byte)UnityEngine.Random.Range(60, 200));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // camera shake (degrees) and zoom kick (field of view added), cinema mode
    public void Shake(float degrees) => shake = Mathf.Max(0f, degrees);
    public void KickFov(float degrees) => fovKick = degrees;
    public void SetHandheld(float scale) => swayScale = Mathf.Max(0f, scale);

    // ------------------------------------------------------------------ the cinema camera
    public void SetCinemaPose(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        camPos = position;
        camRot = rotation;
        if (fieldOfView > 0f) camFov = fieldOfView;
    }

    // The cinema camera's base pose.
    public void GetCinemaPose(out Vector3 position, out Quaternion rotation, out float fieldOfView)
    {
        position = camPos;
        rotation = camRot;
        fieldOfView = camFov;
    }

    // Cinema mode: the camera pose with the handheld sway, shake and zoom kick.
    private void LateUpdate()
    {
        if (!IsCinema) return;
        float t = Time.time;
        float sway = handheld * swayScale;
        var swayRot = Quaternion.Euler(sway * (Mathf.PerlinNoise(t * 0.35f, 1.3f) - 0.5f) * 2f,
                                       sway * (Mathf.PerlinNoise(2.1f, t * 0.3f) - 0.5f) * 2f, 0f);
        Quaternion shakeRot = Quaternion.identity;
        if (shake > 0f)
            shakeRot = Quaternion.Euler(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake) * 0.5f);
        cinemaCamera.transform.SetPositionAndRotation(camPos, camRot * swayRot * shakeRot);
        cinemaCamera.fieldOfView = Mathf.Clamp(camFov + fovKick, 5f, 120f);
    }

    // Fades the bars, the thought balloon and the subtitles.
    private void Update()
    {
        if (bars != null) bars.alpha = Mathf.MoveTowards(bars.alpha, barsTarget, Time.unscaledDeltaTime * 3f);
        if (thoughtGroup != null)
        {
            thoughtTimer -= Time.deltaTime;
            thoughtGroup.alpha = Mathf.MoveTowards(thoughtGroup.alpha, thoughtTimer > 0f ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }
        if (subtitleGroup == null) return;
        subtitleTimer -= Time.deltaTime;
        subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, subtitleTimer > 0f ? 1f : 0f, Time.unscaledDeltaTime * 5f);
    }
}
