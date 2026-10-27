using System;
using UnityEngine;

// Helena talking to the operator (over the radio: 2D audio). Plays a recorded take of the line in the chosen language
// and announces the subtitle. A new line cuts the current one; the same line isn't repeated within a short time.
public class PartnerVoice : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string BankField = nameof(bank);
    public const string SettingsField = nameof(settings);
    public const string AudioSourceField = nameof(audioSource);

    // The lines (text + takes), the settings (language, subtitles) and the 2D audio source.
    [SerializeField] private VoiceLineBank bank;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The same line isn't said again within this time (s).")]
    [SerializeField] private float repeatCooldown = 1.5f;
    // Subtitle time: at least this long (s), plus this per character (s).
    [Header("Subtitle time without a clip")]
    [SerializeField] private float minSubtitleTime = 1.6f;
    [SerializeField] private float secondsPerCharacter = 0.055f;
    [Tooltip("Extra time on screen after the clip ends (s).")]
    [SerializeField] private float subtitleTail = 0.4f;

    // Last line said, when, and the take used (no repeats within a short time, no same take twice).
    private PartnerLine lastLine;
    private float lastTime = -100f;
    private AudioClip lastClip;

    // text, seconds on screen
    public event Action<string, float> LineStarted;

    // Language she speaks.
    public GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;

    // Checks references.
    private void Awake()
    {
        if (bank == null || audioSource == null)
        {
            Debug.LogError($"{name}: PartnerVoice needs a VoiceLineBank and an AudioSource.", this);
            enabled = false;
        }
    }

    // Says a line: picks a take, plays it, announces the subtitle.
    public void Say(PartnerLine line)
    {
        if (!enabled) return;
        if (line == lastLine && Time.time - lastTime < repeatCooldown) return;
        if (!bank.TryGet(line, out VoiceLineBank.Entry entry))
        {
            Debug.LogWarning($"[Voice] Line {line} is not in the bank (run Last Terminal/3).", this);
            return;
        }
        lastLine = line;
        lastTime = Time.time;

        GameLanguage language = Language;
        string text = entry.Text(language);
        AudioClip clip = PickClip(entry.Clips(language));
        float duration;
        audioSource.Stop();
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
            duration = clip.length + subtitleTail;
        }
        else duration = Mathf.Max(minSubtitleTime, (text != null ? text.Length : 0) * secondsPerCharacter);
        LineStarted?.Invoke(text, duration);
    }

    // a line that isn't in the bank (what she says about a thing she looks at): the text, its recording if any
    public void SayText(string text, AudioClip clip = null)
    {
        if (!enabled || string.IsNullOrEmpty(text)) return;
        float duration;
        audioSource.Stop();
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
            duration = clip.length + subtitleTail;
        }
        else duration = Mathf.Max(minSubtitleTime, text.Length * secondsPerCharacter);
        LineStarted?.Invoke(text, duration);
    }

    // a random take, not the same one twice in a row when there are several
    private AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clips.Length > 1 && clip == lastClip) clip = clips[(Array.IndexOf(clips, clip) + 1) % clips.Length];
        lastClip = clip;
        return clip;
    }
}
