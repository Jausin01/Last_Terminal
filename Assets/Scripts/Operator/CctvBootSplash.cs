using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The camera program starting (CCTV.EXE clicked on his PC): a company splash over the screen while the cameras
// connect - the emblem draws itself (ring, eye, pupil), the name and the tagline come in, a bar fills while the
// location loads, then "connected" and the splash fades off the camera view. Its sound (A_Cctv_Boot) is timed to it.
// Played by the OperatorStation; built by Last Terminal/16 (every reference wired there).
public class CctvBootSplash : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string RefsField = nameof(refs);
    public const string SettingsField = nameof(settings);
    public const string AudioField = nameof(audioSource);
    public const string ClipField = nameof(clip);

    // The splash's parts (wired by Last Terminal/16).
    [Serializable]
    public class Refs
    {
        // The whole splash, the soft light behind the emblem.
        public CanvasGroup group;
        public Image glow;
        // The emblem: ring (radial fill), the eye's two lids (scaled open), the pupil.
        public Image ring;
        public RectTransform eyeTop;
        public RectTransform eyeBottom;
        public RectTransform pupil;
        // The name, the light sweeping over it, the tagline.
        public TMP_Text title;
        public RectTransform sweep;
        public TMP_Text tagline;
        // The loading bar (horizontal fill), the status line, the small print.
        public CanvasGroup loader;
        public Image barFill;
        public TMP_Text status;
        public TMP_Text footer;
        // White flash when the view cuts to the cameras.
        public Image flash;
    }

    [SerializeField] private Refs refs = new Refs();
    [Tooltip("The game's language.")]
    [SerializeField] private CameraSettings settings;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The startup sound (its beats: the eye opens at 1.0 s, the name at 1.3 s, connected at 3.95 s).")]
    [SerializeField] private AudioClip clip;

    // Timings (seconds from the start), matched to the sound.
    [SerializeField] private float barStart = 1.9f;
    [SerializeField] private float barEnd = 3.8f;
    [SerializeField] private float connectedHold = 0.45f;
    [SerializeField] private float revealTime = 0.5f;

    // Title spacing while it settles, the emblem's color.
    private const float TitleSpacingFrom = 40f, TitleSpacingTo = 14f;
    private static readonly Color Teal = new Color(0.47f, 0.78f, 0.8f);

    // Is it on screen?
    public bool IsPlaying { get; private set; }

    // Language helper.
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private string T(string en, string pt) => Pt ? pt : en;

    // Hidden until played.
    private void Awake()
    {
        if (refs.group == null) { Debug.LogError($"{name}: CctvBootSplash is missing its parts (run Last Terminal/16).", this); enabled = false; return; }
        Hide();
    }

    // ------------------------------------------------------------------ playing
    // The splash up to "connected": waits for ready() (the cameras are there, or the load gave up); progress() is the
    // location's load (0..1). The caller then switches to the cameras and runs Reveal().
    public IEnumerator Play(Func<bool> ready, Func<float> progress)
    {
        if (!enabled) yield break;
        IsPlaying = true;
        Reset();
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        refs.tagline.text = T("SECURE SURVEILLANCE NETWORK", "REDE DE VIGILÂNCIA SEGURA");
        refs.footer.text = T("© Sentinel Systems  ·  Licensed to the State Government - Operations Terminal 07",
                             "© Sentinel Systems  ·  Licenciado ao Governo do Estado - Terminal de Operações 07");
        float start = Time.unscaledTime;
        float shown = 0f;
        while (true)
        {
            float t = Time.unscaledTime - start;
            Intro(t);
            if (t >= barStart)
            {
                float timeFill = Mathf.Clamp01((t - barStart) / (barEnd - barStart)) * 0.9f;
                float loadFill = Mathf.Clamp01(progress != null ? progress() : 1f) * 0.9f;
                shown = Mathf.MoveTowards(shown, Mathf.Min(timeFill, Mathf.Max(loadFill, timeFill * 0.6f)), Time.unscaledDeltaTime * 0.8f);
                refs.barFill.fillAmount = shown;
                refs.status.text = Status(shown);
                if (t >= barEnd && ready()) break;
            }
            yield return null;
        }
        // connected: the bar completes, the emblem brightens
        float done = Time.unscaledTime;
        refs.status.text = T("Connected.", "Conectado.");
        while (Time.unscaledTime - done < connectedHold)
        {
            float k = (Time.unscaledTime - done) / connectedHold;
            refs.barFill.fillAmount = Mathf.Lerp(shown, 1f, Ease(Mathf.Clamp01(k * 3f)));
            SetAlpha(refs.glow, 0.35f + 0.35f * Mathf.Sin(Mathf.PI * k));
            refs.ring.color = Color.Lerp(Teal, Color.white, Mathf.Sin(Mathf.PI * k) * 0.6f);
            yield return null;
        }
    }

    // Off the camera view: a short white flash, then the splash fades away.
    public IEnumerator Reveal()
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < revealTime)
        {
            float k = (Time.unscaledTime - start) / revealTime;
            SetAlpha(refs.flash, k < 0.15f ? k / 0.15f * 0.45f : Mathf.Lerp(0.45f, 0f, (k - 0.15f) / 0.85f));
            refs.group.alpha = 1f - Ease(k);
            yield return null;
        }
        Hide();
    }

    // Hides the splash at once (a load that failed, a scene leaving).
    public void Hide()
    {
        IsPlaying = false;
        if (refs.group == null) return;
        refs.group.alpha = 0f;
        refs.group.blocksRaycasts = false;
        refs.group.interactable = false;
    }

    // ------------------------------------------------------------------ the frames
    // Everything at its starting point, the splash over the screen (it blocks clicks).
    private void Reset()
    {
        refs.group.alpha = 0f;
        refs.group.blocksRaycasts = true;
        refs.ring.fillAmount = 0f;
        refs.ring.color = Teal;
        SetAlpha(refs.glow, 0f);
        refs.eyeTop.localScale = new Vector3(0f, 1f, 1f);
        refs.eyeBottom.localScale = new Vector3(0f, 1f, 1f);
        refs.pupil.localScale = Vector3.zero;
        SetAlpha(refs.title, 0f);
        refs.title.characterSpacing = TitleSpacingFrom;
        SetAlpha(refs.tagline, 0f);
        refs.sweep.gameObject.SetActive(false);
        refs.loader.alpha = 0f;
        refs.barFill.fillAmount = 0f;
        refs.status.text = "";
        SetAlpha(refs.flash, 0f);
    }

    // The intro at time t: fade in, ring, lids, pupil, name, sweep, tagline, loader.
    private void Intro(float t)
    {
        refs.group.alpha = Mathf.Clamp01(t / 0.15f);
        refs.ring.fillAmount = Ease(Mathf.InverseLerp(0.05f, 0.85f, t));
        float lids = EaseOut(Mathf.InverseLerp(0.75f, 1.05f, t));
        refs.eyeTop.localScale = new Vector3(lids, 1f, 1f);
        refs.eyeBottom.localScale = new Vector3(lids, 1f, 1f);
        refs.pupil.localScale = Vector3.one * BackOut(Mathf.InverseLerp(1.0f, 1.25f, t));
        float glowIn = Mathf.InverseLerp(0.6f, 1.2f, t);
        SetAlpha(refs.glow, glowIn * (0.3f + 0.05f * Mathf.Sin(t * 2.4f)));
        float nameIn = Ease(Mathf.InverseLerp(1.25f, 1.9f, t));
        SetAlpha(refs.title, nameIn);
        refs.title.characterSpacing = Mathf.Lerp(TitleSpacingFrom, TitleSpacingTo, nameIn);
        float sw = Mathf.InverseLerp(1.35f, 2.05f, t);
        refs.sweep.gameObject.SetActive(sw > 0f && sw < 1f);
        refs.sweep.anchoredPosition = new Vector2(Mathf.Lerp(-380f, 380f, Ease(sw)), refs.sweep.anchoredPosition.y);
        SetAlpha(refs.tagline, Ease(Mathf.InverseLerp(1.6f, 2.0f, t)));
        refs.loader.alpha = Ease(Mathf.InverseLerp(barStart - 0.1f, barStart + 0.2f, t));
    }

    // The status line for the bar's fill.
    private string Status(float fill)
    {
        if (fill < 0.22f) return T("Starting video subsystem...", "Iniciando subsistema de vídeo...");
        if (fill < 0.45f) return T("Authenticating operator...", "Autenticando operador...");
        if (fill < 0.7f) return T("Opening the camera network...", "Abrindo a rede de câmeras...");
        return T("Synchronizing feeds...", "Sincronizando transmissões...");
    }

    // ------------------------------------------------------------------ helpers
    private static float Ease(float x) => x * x * (3f - 2f * x);
    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    private static float BackOut(float x)
    {
        if (x <= 0f) return 0f;
        const float c = 1.9f;
        float y = x - 1f;
        return 1f + (c + 1f) * y * y * y + c * y * y;
    }
    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color; c.a = a; g.color = c;
    }
}
