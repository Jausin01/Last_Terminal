using TMPro;
using UnityEngine;

// Helena's lines at the bottom of the screen (can be turned off in the settings).
public class SubtitleView : MonoBehaviour
{
    public const string VoiceField = nameof(voice);
    public const string SettingsField = nameof(settings);
    public const string TextField = nameof(text);
    public const string GroupField = nameof(group);

    [SerializeField] private PartnerVoice voice;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private string speakerName = "HELENA";
    [SerializeField] private Color speakerColor = new Color(0.85f, 0.25f, 0.3f);
    [SerializeField] private float fadeTime = 0.25f;

    private float timer;

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

    private void OnEnable()
    {
        if (voice != null) voice.LineStarted += Show;
    }

    private void OnDisable()
    {
        if (voice != null) voice.LineStarted -= Show;
    }

    private void Show(string line, float seconds)
    {
        if (settings != null && !settings.SubtitlesEnabled) return;
        string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
        text.text = $"<color=#{hex}>{speakerName}:</color> {line}";
        timer = seconds;
    }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        float target = timer > 0f ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
    }
}
