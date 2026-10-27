using TMPro;
using UnityEngine;

// Helena's lines at the bottom of the screen (can be turned off in the settings).
public class SubtitleView : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string VoiceField = nameof(voice);
    public const string SettingsField = nameof(settings);
    public const string TextField = nameof(text);
    public const string GroupField = nameof(group);

    // Her voice (subtitle events), the settings (on / off), the text and its CanvasGroup.
    [SerializeField] private PartnerVoice voice;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup group;
    // Speaker tag and its color, fade time (s).
    [SerializeField] private string speakerName = "HELENA";
    [SerializeField] private Color speakerColor = new Color(0.85f, 0.25f, 0.3f);
    [SerializeField] private float fadeTime = 0.25f;

    // Time left on screen.
    private float timer;

    // Checks references; hidden at start.
    private void Awake()
    {
        if (voice == null || text == null || group == null)
        {
            Debug.LogError($"{name}: SubtitleView needs PartnerVoice, a text and a CanvasGroup.", this);
            enabled = false;
            return;
        }
        group.alpha = 0f;
    }

    // Listens to her lines.
    private void OnEnable()
    {
        if (voice != null) voice.LineStarted += Show;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (voice != null) voice.LineStarted -= Show;
    }

    // Shows a line for a while (if subtitles are on).
    private void Show(string line, float seconds)
    {
        if (settings != null && !settings.SubtitlesEnabled) return;
        string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
        text.text = $"<color=#{hex}>{speakerName}:</color> {line}";
        timer = seconds;
    }

    // Counts down and fades out.
    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        float target = timer > 0f ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
    }
}
