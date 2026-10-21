using System;
using UnityEngine;

// Helena talking to the operator (over the radio: 2D audio). Plays a recorded take of the line in the chosen language
// and announces the subtitle. A new line cuts the current one; the same line isn't repeated within a short time.
public class PartnerVoice : MonoBehaviour
{
    public const string BankField = nameof(bank);
    public const string SettingsField = nameof(settings);
    public const string AudioSourceField = nameof(audioSource);

    [SerializeField] private VoiceLineBank bank;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The same line isn't said again within this time (s).")]
    [SerializeField] private float repeatCooldown = 1.5f;
    [Header("Subtitle time without a clip")]
    [SerializeField] private float minSubtitleTime = 1.6f;
    [SerializeField] private float secondsPerCharacter = 0.055f;
    [Tooltip("Extra time on screen after the clip ends (s).")]
    [SerializeField] private float subtitleTail = 0.4f;

    private PartnerLine lastLine;
    private float lastTime = -100f;
    private AudioClip lastClip;

    // text, seconds on screen
    public event Action<string, float> LineStarted;

    public GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;

    private void Awake()
    {
        if (bank == null || audioSource == null)
        {
            Debug.LogError($"{name}: PartnerVoice needs a VoiceLineBank and an AudioSource.", this);
            enabled = false;
        }
    }

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
