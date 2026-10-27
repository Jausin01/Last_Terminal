using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The game's settings as a window of his PC: the gear on the desktop (and Start > Settings), and over the cameras from
// the "close the program?" dialog (Esc). Sections: cameras (pan control, pan speeds), picture (camera filter and its
// strength), sound (volume), language (language, subtitles). Every change applies at once (CameraSettings saves it);
// Restore defaults keeps the language. Esc / OK / X close it. Built by Last Terminal/16.
public class SettingsWindow : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SettingsField = nameof(settings);
    public const string AudioField = nameof(audioSource);
    public const string GroupField = nameof(group);
    public const string RefsField = nameof(refs);
    public const string LabelsField = nameof(labels);

    // a text fixed except for the language
    [Serializable]
    public class Label
    {
        // The text, and what it says in each language.
        public TMP_Text text;
        public string en;
        public string pt;
    }

    // two buttons, one of them chosen (pan control, language)
    [Serializable]
    public class Choice
    {
        // The two buttons.
        public Button a;
        public Button b;
    }

    // a slider with its value shown beside it
    [Serializable]
    public class Level
    {
        // The slider and the text showing its value.
        public Slider slider;
        public TMP_Text value;
    }

    // Every control of the window (wired by Last Terminal/16).
    [Serializable]
    public class Refs
    {
        // Title, close (X), OK and "Restore defaults".
        public TMP_Text title;
        public Button close;
        public Button ok;
        public Button defaults;
        // Cameras: pan control (mouse / keyboard) and the pan speed of each.
        public Choice pan = new Choice();
        public Level mouseSpeed = new Level();
        public Level keyboardSpeed = new Level();
        // Picture: the CCTV filter toggle and its strength.
        public Button filter;
        public Level filterStrength = new Level();
        // Sound: master volume.
        public Level volume = new Level();
        // Language: Portuguese / English, and the subtitles toggle.
        public Choice language = new Choice();
        public Button subtitles;
    }

    // The settings this window edits (they save themselves).
    [SerializeField] private CameraSettings settings;
    [Tooltip("Clicks (2D).")]
    [SerializeField] private AudioSource audioSource;
    // The window's CanvasGroup, its controls and its fixed texts.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Refs refs = new Refs();
    [SerializeField] private Label[] labels = new Label[0];

    // Colors of a chosen / normal button and their texts.
    [Header("Look")]
    [SerializeField] private Color chosen = new Color(0.05f, 0.12f, 0.42f);
    [SerializeField] private Color chosenText = Color.white;
    [SerializeField] private Color normal = new Color(0.86f, 0.86f, 0.84f);
    [SerializeField] private Color normalText = new Color(0.08f, 0.08f, 0.1f);

    // Shown now; a refresh is setting the controls (their callbacks are ignored then).
    private bool open;
    private bool refreshing;

    // Shown now, and raised when it closes (the station / dialog continue).
    public bool IsOpen => open;
    public event Action Closed;

    // Language helpers.
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private string T(string en, string pt) => Pt ? pt : en;

    // Wires every control to its setting; starts hidden.
    private void Awake()
    {
        if (settings == null || group == null)
        {
            Debug.LogError($"{name}: SettingsWindow is missing references (run Last Terminal/16).", this);
            enabled = false;
            return;
        }
        Wire(refs.close, Close);
        Wire(refs.ok, Close);
        Wire(refs.defaults, () => settings.RestoreDefaults());
        Wire(refs.pan.a, () => settings.SetPanControl(CameraSettings.PanControl.Mouse));
        Wire(refs.pan.b, () => settings.SetPanControl(CameraSettings.PanControl.Keyboard));
        Wire(refs.filter, () => settings.SetFilterEnabled(!settings.FilterEnabled));
        Wire(refs.language.a, () => settings.SetLanguage(GameLanguage.Portuguese));
        Wire(refs.language.b, () => settings.SetLanguage(GameLanguage.English));
        Wire(refs.subtitles, () => settings.SetSubtitles(!settings.SubtitlesEnabled));
        Slide(refs.mouseSpeed, 10f, 150f, v => settings.SetMousePanSpeed(v));
        Slide(refs.keyboardSpeed, 10f, 150f, v => settings.SetKeyboardPanSpeed(v));
        Slide(refs.filterStrength, 0f, 1f, v => settings.SetFilterStrength(v));
        Slide(refs.volume, 0f, 1f, v => settings.SetMasterVolume(v));
        Show(false);
    }

    // Refreshes when a setting changes.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
    }

    // Opens on top, showing the current values.
    public void Open()
    {
        if (open) return;
        transform.SetAsLastSibling();
        Show(true);
        Refresh();
    }

    // Hides it and raises Closed.
    public void Close()
    {
        if (!open) return;
        Show(false);
        Closed?.Invoke();
    }

    // Shows / hides it and its clicks.
    private void Show(bool on)
    {
        open = on;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    // A button that clicks and runs an action.
    private void Wire(Button b, Action action)
    {
        if (b == null) return;
        b.onClick.AddListener(() =>
        {
            OperatorSounds.Play(audioSource, OperatorSounds.Kind.Click, 0.6f);
            action();
        });
    }

    // A slider's range and the setting it changes.
    private void Slide(Level level, float min, float max, Action<float> set)
    {
        if (level.slider == null) return;
        level.slider.minValue = min;
        level.slider.maxValue = max;
        level.slider.onValueChanged.AddListener(v => { if (!refreshing) set(v); });
    }

    // every value and text as the settings are now
    private void Refresh()
    {
        if (!open) return;
        refreshing = true;
        foreach (Label l in labels)
            if (l != null && l.text != null) l.text.text = Pt && !string.IsNullOrEmpty(l.pt) ? l.pt : l.en;
        if (refs.title != null) refs.title.text = T("Settings", "Configurações");
        Pick(refs.pan, settings.Pan == CameraSettings.PanControl.Mouse);
        Set(refs.mouseSpeed, settings.MousePanSpeed, Mathf.RoundToInt(settings.MousePanSpeed).ToString());
        Set(refs.keyboardSpeed, settings.KeyboardPanSpeed, Mathf.RoundToInt(settings.KeyboardPanSpeed).ToString());
        Toggle(refs.filter, settings.FilterEnabled, T("On", "Ligado"), T("Off", "Desligado"));
        Set(refs.filterStrength, settings.FilterStrength, Mathf.RoundToInt(settings.FilterStrength * 100f) + "%");
        if (refs.filterStrength.slider != null) refs.filterStrength.slider.interactable = settings.FilterEnabled;
        Set(refs.volume, settings.MasterVolume, Mathf.RoundToInt(settings.MasterVolume * 100f) + "%");
        Pick(refs.language, settings.Language == GameLanguage.Portuguese);
        Toggle(refs.subtitles, settings.SubtitlesEnabled, T("On", "Ligadas"), T("Off", "Desligadas"));
        refreshing = false;
    }

    // Paints the chosen one of two buttons.
    private void Pick(Choice c, bool first)
    {
        Paint(c.a, first);
        Paint(c.b, !first);
    }

    // An on / off button: painted and labeled "[X] On" / "[  ] Off".
    private void Toggle(Button b, bool on, string onText, string offText)
    {
        if (b == null) return;
        Paint(b, on);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = on ? "[X]  " + onText : "[  ]  " + offText;
    }

    // Colors a button as chosen or normal.
    private void Paint(Button b, bool on)
    {
        if (b == null) return;
        if (b.targetGraphic != null) b.targetGraphic.color = on ? chosen : normal;
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.color = on ? chosenText : normalText;
    }

    // Sets a slider without raising its callback, and its value text.
    private static void Set(Level level, float value, string shown)
    {
        if (level.slider != null) level.slider.SetValueWithoutNotify(value);
        if (level.value != null) level.value.text = shown;
    }
}
