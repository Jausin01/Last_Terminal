using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The operator's microphone: hold V (or the middle mouse button) and talk; on release what was heard goes to Helena
// exactly like a typed command (PartnerCommands.Execute). Optional: always listening (every pause ends a command).
// Recognition: Vosk, offline, on its own thread (VoskWorker), one model per game language in
// StreamingAssets/Vosk/en and StreamingAssets/Vosk/pt; it only listens for the words of the command lexicon, so
// new phrases in the lexicon are heard without touching this. The language follows the settings.
// Off while the command line is open, in the settings, the save screen or a dialog over the cameras (and outside the
// camera program: OperatorStation turns it off). The microphone is the one chosen in the settings (MicrophoneInput);
// while the settings' microphone test is open, what is heard goes to the test, not to Helena.
public class VoiceCommandInput : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string CommandsField = nameof(commands);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string SaveScreenField = nameof(saveScreen);
    public const string SettingsField = nameof(settings);
    public const string EnglishField = nameof(english);
    public const string PortugueseField = nameof(portuguese);
    public const string ConsoleField = nameof(console);
    public const string GroupField = nameof(group);
    public const string TextField = nameof(text);

    // Where commands go, and the screens that turn the mic off (camera system, save screen), the settings (language).
    [SerializeField] private PartnerCommands commands;
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private SaveScreen saveScreen;
    [SerializeField] private CameraSettings settings;
    [Tooltip("The lexicons: their words are what the recognizer listens for.")]
    [SerializeField] private CommandLexicon english;
    // Portuguese lexicon.
    [SerializeField] private CommandLexicon portuguese;
    [Tooltip("Optional: shows what was heard like a typed command.")]
    [SerializeField] private CommandConsole console;
    // The "listening / heard" indicator.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text text;

    // Model folders under StreamingAssets per language.
    [Header("Recognition")]
    [SerializeField] private string englishModel = "Vosk/en";
    [SerializeField] private string portugueseModel = "Vosk/pt";
    [Tooltip("Listen all the time (a pause ends each command) instead of push to talk.")]
    [SerializeField] private bool alwaysListening;
    [Tooltip("Audio kept from just before the key (the first syllable isn't lost), s.")]
    [SerializeField] private float preRoll = 0.3f;
    [Tooltip("Audio still taken after the key is released, s.")]
    [SerializeField] private float tail = 0.35f;
    // How long the heard text stays (s).
    [SerializeField] private float resultTime = 2.5f;

    // Push to talk button.
    [Header("Input (bindings added when empty)")]
    [SerializeField] private InputAction talk = new InputAction("Talk", InputActionType.Button);

    // words that join the commands ("vai PARA O depósito") and are worth hearing
    private static readonly string[] FillersEn = { "the", "a", "an", "to", "into", "and", "please", "now", "that", "this", "it", "on", "in", "at", "of", "my", "your", "her", "me", "you", "helena", "go", "get", "okay" };
    private static readonly string[] FillersPt = { "o", "a", "os", "as", "um", "uma", "para", "pra", "pro", "no", "na", "nos", "nas", "do", "da", "de", "e", "por", "favor", "agora", "isso", "esse", "essa", "aquele", "aquela", "me", "você", "helena", "vai", "ali", "lá", "aqui" };
    // what a lexicon "stem*" word may end with
    private static readonly string[] EndingsEn = { "", "s", "es", "ed", "ing", "e", "er" };
    private static readonly string[] EndingsPt = { "", "a", "e", "o", "as", "es", "am", "em", "ar", "er", "ir", "ando", "endo", "indo", "ou", "eu", "iu", "ado", "ido", "ada", "ida", "ue", "uem", "ra", "re" };

    // Vosk result JSON.
    [Serializable]
    private class VoskJson
    {
        // Final text / partial text.
        public string text;
        public string partial;
    }

    // Model loading state.
    private enum State { Loading, Ready, Failed }

    // The recognizer thread.
    private VoskWorker worker;
    private Action<string> overrideHandler;   // a cutscene waiting for the operator's line: what is heard goes there
    // Model state, language loaded, failure reason, the microphone, its rate, the looping clip, read position and capturing.
    private State state = State.Loading;
    private GameLanguage loadedLanguage;
    private bool languageLoaded;
    private string failReason = "";
    private int rate = 16000;
    private AudioClip clip;
    private bool micAcquired;
    private int readPos;
    private bool capturing;
    private bool awaitingFinish;          // push to talk: the end of the utterance was asked for
    // No microphone found.
    private bool noMicrophone;
    private readonly List<string> segments = new List<string>();   // push to talk: pieces heard before the release
    // Tail audio left, partial poll timer, the text heard, its display timer, and a conversion buffer.
    private float tailLeft;
    private float partialTimer;
    private string heard = "";
    private float resultTimer;
    private float[] floatBuffer = new float[0];
    private bool codeGrammar;             // she is at a keypad: listening mostly for the code

    // Language from the settings.
    private GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;
    private bool Pt => Language == GameLanguage.Portuguese;

    // Starts the worker and loads the model.
    private void Awake()
    {
        if (talk.bindings.Count == 0)
        {
            talk.AddBinding("<Keyboard>/v");
            talk.AddBinding("<Mouse>/middleButton");
        }
        // no PartnerCommands (a story cutscene scene): only the operator's lines of the cutscenes are heard
        if (group != null) group.alpha = 1f;
        worker = new VoskWorker();
    }

    // Stops the mic and the worker.
    private void OnDestroy()
    {
        if (worker != null) worker.Dispose();
        worker = null;
    }

    // Enables the button; listens to the settings.
    private void OnEnable()
    {
        talk.Enable();
        if (settings != null) settings.Changed += OnSettingsChanged;
        MicrophoneInput.Restarted += OnMicrophoneRestarted;
        StartMicrophone();          // first: the recognizer works at the microphone's rate
        LoadLanguageIfNeeded();
    }

    // Disables the button; stops listening.
    private void OnDisable()
    {
        talk.Disable();
        if (settings != null) settings.Changed -= OnSettingsChanged;
        MicrophoneInput.Restarted -= OnMicrophoneRestarted;
        StopCapture(false);
        StopMicrophone();
    }

    // Language changed: load the other model; another microphone chosen: record from it.
    private void OnSettingsChanged()
    {
        MicrophoneInput.Use(settings.MicrophoneDevice);
        LoadLanguageIfNeeded();
    }

    // The recording restarted (another device): read from the new clip; a new rate needs the model loaded again.
    private void OnMicrophoneRestarted()
    {
        StopCapture(false);
        awaitingFinish = false;
        clip = MicrophoneInput.Clip;
        readPos = MicrophoneInput.Position;
        if (clip == null) return;
        if (MicrophoneInput.Rate != rate)
        {
            rate = MicrophoneInput.Rate;
            languageLoaded = false;
            LoadLanguageIfNeeded();
        }
    }

    // ------------------------------------------------------------------ model
    private void LoadLanguageIfNeeded()
    {
        if (worker == null || (languageLoaded && loadedLanguage == Language)) return;
        loadedLanguage = Language;
        languageLoaded = true;
        StopCapture(false);
        awaitingFinish = false;
        CommandLexicon lexicon = Pt ? portuguese : english;
        string path = Path.Combine(Application.streamingAssetsPath, Pt ? portugueseModel : englishModel);
        if (!Directory.Exists(path))
        {
            Fail($"model folder missing: {path}");
            return;
        }
        state = State.Loading;
        codeGrammar = false;
        worker.Load(path, rate, LexiconWords(lexicon, Pt ? FillersPt : FillersEn), Pt ? EndingsPt : EndingsEn);
    }

    // Every word of a lexicon plus the fillers.
    private static string[] LexiconWords(CommandLexicon lexicon, string[] fillers)
    {
        var words = new HashSet<string>(fillers);
        if (lexicon != null)
        {
            foreach (var v in lexicon.Verbs) AddWords(words, v.Phrases);
            foreach (var n in lexicon.Names) AddWords(words, n.Phrases);
            foreach (var m in lexicon.Modifiers) AddWords(words, m.Phrases);
            foreach (var n in lexicon.Numbers) AddWords(words, n.Phrases);
            AddWords(words, lexicon.Pronouns);
        }
        return new List<string>(words).ToArray();
    }

    // Adds the words of phrases to a set.
    private static void AddWords(HashSet<string> words, IEnumerable<string> phrases)
    {
        foreach (string p in phrases)
            if (!string.IsNullOrEmpty(p))
                foreach (string w in p.Split(' ')) if (w.Length > 0) words.Add(w.ToLowerInvariant());
    }

    // Recognition unavailable: shows why.
    private void Fail(string reason)
    {
        state = State.Failed;
        failReason = reason;
        Debug.LogWarning("[Voice] " + reason + ". Typed commands still work (Enter).");
    }

    // ------------------------------------------------------------------ microphone
    // Records from the microphone chosen in the settings (shared with the other scenes: MicrophoneInput).
    private void StartMicrophone()
    {
        if (!MicrophoneInput.HasDevice) { noMicrophone = true; Fail("no microphone"); return; }
        MicrophoneInput.Acquire(settings != null ? settings.MicrophoneDevice : "");
        micAcquired = true;
        clip = MicrophoneInput.Clip;
        rate = MicrophoneInput.Rate;
        readPos = MicrophoneInput.Position;
    }

    // Stops using the microphone.
    private void StopMicrophone()
    {
        if (micAcquired) MicrophoneInput.Release();
        micAcquired = false;
        clip = null;
    }

    // Mic off while typing, in the settings / save screen / a dialog (the settings' microphone test: only typing).
    private bool Blocked => MicrophoneInput.TestOpen ? console != null && console.IsOpen
                          : (cameraSystem != null && (cameraSystem.IsTyping || cameraSystem.IsSettingsOpen || cameraSystem.IsOverlayOpen
                                                      || (cameraSystem.IsLocked && overrideHandler == null)))
                            || (saveScreen != null && saveScreen.IsOpen)
                            || (commands == null && overrideHandler == null)
                            || (console != null && console.IsOpen);

    // Starts capturing (with the pre-roll).
    private void StartCapture()
    {
        if (clip == null || state != State.Ready) return;
        capturing = true;
        tailLeft = -1f;
        heard = "";
        segments.Clear();
        int pos = MicrophoneInput.Position;
        int pre = Mathf.Min((int)(preRoll * rate), clip.samples - 1);
        readPos = ((pos - pre) % clip.samples + clip.samples) % clip.samples;
        worker.Reset();
    }

    // finish: the utterance ends (its text comes back); false = dropped
    private void StopCapture(bool finish)
    {
        if (!capturing) return;
        capturing = false;
        if (finish) { awaitingFinish = true; worker.Finish(); }
        else { segments.Clear(); worker.Reset(); }
    }

    // Each frame: push to talk / always listening, audio to the worker, tail, indicator.
    private void Update()
    {
        ReadOutputs();
        if (clip == null || state != State.Ready) { ShowStatus(); return; }
        UpdateGrammar();

        bool blocked = Blocked;
        if (alwaysListening)
        {
            if (!capturing && !blocked) StartCapture();
            else if (capturing && blocked) StopCapture(false);
        }
        else
        {
            if (!capturing && !blocked && talk.WasPressedThisFrame()) StartCapture();
            if (capturing && tailLeft < 0f && !talk.IsPressed()) tailLeft = tail;
            if (capturing && blocked) StopCapture(false);
        }

        int pos = MicrophoneInput.Position;
        if (capturing) Pump(pos);
        else readPos = pos;

        if (capturing && tailLeft >= 0f)
        {
            tailLeft -= Time.unscaledDeltaTime;
            if (tailLeft <= 0f) StopCapture(true);
        }
        if (resultTimer > 0f) resultTimer -= Time.unscaledDeltaTime;
        ShowStatus();
    }

    // at a keypad the short joining words ("o", "no", "de"...) leave the grammar: they are what a short "um" is
    // mistaken for. The rest of the commands stay (she can still be sent elsewhere).
    private void UpdateGrammar()
    {
        if (overrideHandler != null || capturing || awaitingFinish) return;
        bool code = commands != null && commands.AtKeypad;
        if (code == codeGrammar) return;
        codeGrammar = code;
        if (!code) { worker.UseWords(null); return; }
        CommandLexicon lexicon = Pt ? portuguese : english;
        var words = new HashSet<string>();
        foreach (string w in LexiconWords(lexicon, Pt ? FillersPt : FillersEn))
            if (w.TrimEnd('*').Length >= 3) words.Add(w);
        foreach (string w in KeypadInput.Words(Language)) words.Add(w);
        worker.UseWords(new List<string>(words).ToArray(), Pt ? EndingsPt : EndingsEn);
    }

    // the new samples since the last read, as 16 bit, to the recognizer
    private void Pump(int pos)
    {
        int count = (pos - readPos + clip.samples) % clip.samples;
        if (count <= 0) return;
        if (floatBuffer.Length != count) floatBuffer = new float[count];
        clip.GetData(floatBuffer, readPos);   // wraps around the looping clip
        var samples = new short[count];
        for (int i = 0; i < count; i++) samples[i] = (short)(Mathf.Clamp(floatBuffer[i], -1f, 1f) * 32767f);
        readPos = pos;
        partialTimer -= Time.unscaledDeltaTime;
        bool wantPartial = partialTimer <= 0f;
        if (wantPartial) partialTimer = 0.2f;
        worker.Audio(samples, wantPartial);
    }

    // Reads the worker's outputs (loaded, failed, partial, final).
    private void ReadOutputs()
    {
        if (worker == null) return;
        while (worker.TryGetOutput(out VoskWorker.Output o))
        {
            switch (o.Kind)
            {
                case VoskWorker.OutputKind.Loaded:
                    if (noMicrophone) break;
                    state = State.Ready;
                    Debug.Log($"[Voice] {(Pt ? "pt" : "en")} model ready ({o.Text}). Hold V to talk.");
                    break;
                case VoskWorker.OutputKind.Failed:
                    Fail(o.Text);
                    break;
                case VoskWorker.OutputKind.Partial:
                    if (capturing) heard = Clean(Parse(o.Text).partial);
                    break;
                case VoskWorker.OutputKind.Final:
                    string piece = Clean(Parse(o.Text).text);
                    if (alwaysListening) { if (piece.Length > 0) Heard(piece); break; }
                    // push to talk: a pause mid-sentence gives a piece; the release gives the last one
                    if (piece.Length > 0) segments.Add(piece);
                    if (!awaitingFinish) break;
                    awaitingFinish = false;
                    Heard(string.Join(" ", segments));
                    segments.Clear();
                    break;
            }
        }
    }

    // Parses a Vosk JSON result.
    private static VoskJson Parse(string json)
    {
        try { return JsonUtility.FromJson<VoskJson>(json) ?? new VoskJson(); }
        catch (ArgumentException) { return new VoskJson(); }
    }

    // "[unk]" = something not in the commands
    private static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("[unk]", " ").Trim().Replace("  ", " ");

    // A final sentence: to the settings' test, the override handler or Helena as a command.
    private void Heard(string sentence)
    {
        resultTimer = resultTime;
        if (MicrophoneInput.TestOpen && overrideHandler == null)
        {
            heard = sentence.Length > 0 ? "> " + sentence : (Pt ? "(não entendi)" : "(didn't catch that)");
            MicrophoneInput.ReportTest(sentence);
            return;
        }
        if (sentence.Length == 0)
        {
            heard = Pt ? "(não entendi)" : "(didn't catch that)";
            return;
        }
        if (overrideHandler != null) { heard = "> " + sentence; overrideHandler(sentence); return; }
        if (commands == null) return;
        if (console != null) { console.Echo(sentence); resultTimer = 0f; heard = ""; }   // shown on the command line
        else heard = "> " + sentence;
        commands.Execute(sentence);
    }

    // ------------------------------------------------------------------ a cutscene's line
    // what is said goes to the handler (not to Helena), listening only for the words of that line
    public void BeginOverride(Action<string> handler, string line)
    {
        overrideHandler = handler;
        if (worker != null) worker.UseWords(LineWords(line));
    }

    // Ends a cutscene override: back to the command words.
    public void EndOverride()
    {
        if (overrideHandler == null) return;
        overrideHandler = null;
        StopCapture(false);
        codeGrammar = false;
        if (worker != null) worker.UseWords(null);
    }

    // the words of a line as written (accents kept: the model spells them so), lower case, no punctuation
    private static string[] LineWords(string line)
    {
        var words = new List<string>();
        var sb = new System.Text.StringBuilder();
        foreach (char c in (line ?? "").ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '\'' || c == '-') sb.Append(c);
            else if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) words.Add(sb.ToString());
        return words.ToArray();
    }

    // ------------------------------------------------------------------ indicator
    private void ShowStatus()
    {
        if (text == null) return;
        // a story scene (no Helena): only while the operator has a line to say
        if (commands == null && overrideHandler == null) { text.text = ""; return; }
        switch (state)
        {
            case State.Loading:
                text.text = Pt ? "<color=#8A8A8A>VOZ: carregando...</color>" : "<color=#8A8A8A>VOICE: loading...</color>";
                break;
            case State.Failed:
                text.text = Pt ? "<color=#8A5A5A>VOZ indisponível (veja o Console)</color>" : "<color=#8A5A5A>VOICE unavailable (see the Console)</color>";
                break;
            default:
                if (capturing && (alwaysListening || tailLeft < 0f || heard.Length > 0))
                    text.text = $"<color=#FF5050>●</color> {(Pt ? "OUVINDO" : "LISTENING")}  <color=#E8E8E8>{heard}</color>";
                else if (resultTimer > 0f)
                    text.text = $"<color=#9AD0A0>{heard}</color>";
                else
                    text.text = alwaysListening
                        ? $"<color=#6A9A70>● {(Pt ? "MICROFONE ABERTO" : "MIC OPEN")}</color>"
                        : $"<color=#6A9A70>[V] {(Pt ? "FALAR" : "TALK")}</color>";
                break;
        }
    }
}
