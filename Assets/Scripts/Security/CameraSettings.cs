using System;
using UnityEngine;

// Operator options (settings menu), saved in PlayerPrefs (per machine, not in the save slots).
// Language: commands are understood and Helena speaks (voice + subtitles) in this language only.
// Several scenes open at once (his room + a location) each have one: a change in one reaches the others.
public class CameraSettings : MonoBehaviour
{
    public enum PanControl { Mouse, Keyboard }

    private const string PanControlKey = "cam_pan_control";
    private const string MouseSpeedKey = "cam_mouse_speed";
    private const string KeyboardSpeedKey = "cam_keyboard_speed";
    private const string FilterKey = "cam_filter";
    private const string FilterStrengthKey = "cam_filter_strength";
    private const string LanguageKey = "game_language";
    private const string SubtitlesKey = "subtitles";
    private const string VolumeKey = "master_volume";

    // the values "Restore defaults" puts back (the language stays)
    public const PanControl DefaultPan = PanControl.Mouse;
    public const float DefaultMouseSpeed = 60f, DefaultKeyboardSpeed = 50f, DefaultFilterStrength = 0.8f, DefaultVolume = 1f;

    [SerializeField] private PanControl panControl = PanControl.Mouse;
    [Tooltip("Pan speed when the cursor touches the screen edge (deg/s).")]
    [SerializeField, Range(10f, 150f)] private float mousePanSpeed = 60f;
    [SerializeField, Range(10f, 150f)] private float keyboardPanSpeed = 50f;
    [SerializeField] private bool filterEnabled = true;
    [SerializeField, Range(0f, 1f)] private float filterStrength = 0.8f;
    [SerializeField] private GameLanguage language = GameLanguage.English;
    [SerializeField] private bool subtitles = true;
    [Tooltip("Every sound of the game (AudioListener.volume).")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    public event Action Changed;
    private static event Action<CameraSettings> AnySaved;

    public PanControl Pan => panControl;
    public float MousePanSpeed => mousePanSpeed;
    public float KeyboardPanSpeed => keyboardPanSpeed;
    public bool FilterEnabled => filterEnabled;
    public float FilterStrength => filterStrength;
    public GameLanguage Language => language;
    public bool SubtitlesEnabled => subtitles;
    public float MasterVolume => masterVolume;

    private void Awake() => Load();

    private void OnEnable() => AnySaved += OnOtherSaved;
    private void OnDisable() => AnySaved -= OnOtherSaved;

    // another scene's settings changed: the same values here
    private void OnOtherSaved(CameraSettings from)
    {
        if (from == this) return;
        Load();
        Changed?.Invoke();
    }

    private void Load()
    {
        panControl = (PanControl)PlayerPrefs.GetInt(PanControlKey, (int)panControl);
        mousePanSpeed = PlayerPrefs.GetFloat(MouseSpeedKey, mousePanSpeed);
        keyboardPanSpeed = PlayerPrefs.GetFloat(KeyboardSpeedKey, keyboardPanSpeed);
        filterEnabled = PlayerPrefs.GetInt(FilterKey, filterEnabled ? 1 : 0) == 1;
        filterStrength = PlayerPrefs.GetFloat(FilterStrengthKey, filterStrength);
        language = (GameLanguage)PlayerPrefs.GetInt(LanguageKey, (int)language);
        subtitles = PlayerPrefs.GetInt(SubtitlesKey, subtitles ? 1 : 0) == 1;
        masterVolume = PlayerPrefs.GetFloat(VolumeKey, masterVolume);
        AudioListener.volume = masterVolume;
    }

    public void SetPanControl(PanControl value) { panControl = value; Save(); }
    public void SetMousePanSpeed(float value) { mousePanSpeed = Mathf.Clamp(value, 10f, 150f); Save(); }
    public void SetKeyboardPanSpeed(float value) { keyboardPanSpeed = Mathf.Clamp(value, 10f, 150f); Save(); }
    public void SetFilterEnabled(bool value) { filterEnabled = value; Save(); }
    public void SetFilterStrength(float value) { filterStrength = Mathf.Clamp01(value); Save(); }
    public void SetLanguage(GameLanguage value) { language = value; Save(); }
    public void SetSubtitles(bool value) { subtitles = value; Save(); }
    public void SetMasterVolume(float value) { masterVolume = Mathf.Clamp01(value); Save(); }

    public void RestoreDefaults()
    {
        panControl = DefaultPan;
        mousePanSpeed = DefaultMouseSpeed;
        keyboardPanSpeed = DefaultKeyboardSpeed;
        filterEnabled = true;
        filterStrength = DefaultFilterStrength;
        subtitles = true;
        masterVolume = DefaultVolume;
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetInt(PanControlKey, (int)panControl);
        PlayerPrefs.SetFloat(MouseSpeedKey, mousePanSpeed);
        PlayerPrefs.SetFloat(KeyboardSpeedKey, keyboardPanSpeed);
        PlayerPrefs.SetInt(FilterKey, filterEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(FilterStrengthKey, filterStrength);
        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.SetInt(SubtitlesKey, subtitles ? 1 : 0);
        PlayerPrefs.SetFloat(VolumeKey, masterVolume);
        PlayerPrefs.Save();
        AudioListener.volume = masterVolume;
        Changed?.Invoke();
        AnySaved?.Invoke(this);
    }
}
