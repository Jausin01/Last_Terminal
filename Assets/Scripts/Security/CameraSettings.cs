using System;
using UnityEngine;

// Operator options (settings menu), saved in PlayerPrefs (per machine, not in the save slots).
// Language: commands are understood and Helena speaks (voice + subtitles) in this language only.
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

    [SerializeField] private PanControl panControl = PanControl.Mouse;
    [Tooltip("Pan speed when the cursor touches the screen edge (deg/s).")]
    [SerializeField, Range(10f, 150f)] private float mousePanSpeed = 60f;
    [SerializeField, Range(10f, 150f)] private float keyboardPanSpeed = 50f;
    [SerializeField] private bool filterEnabled = true;
    [SerializeField, Range(0f, 1f)] private float filterStrength = 0.8f;
    [SerializeField] private GameLanguage language = GameLanguage.English;
    [SerializeField] private bool subtitles = true;

    public event Action Changed;

    public PanControl Pan => panControl;
    public float MousePanSpeed => mousePanSpeed;
    public float KeyboardPanSpeed => keyboardPanSpeed;
    public bool FilterEnabled => filterEnabled;
    public float FilterStrength => filterStrength;
    public GameLanguage Language => language;
    public bool SubtitlesEnabled => subtitles;

    private void Awake()
    {
        panControl = (PanControl)PlayerPrefs.GetInt(PanControlKey, (int)panControl);
        mousePanSpeed = PlayerPrefs.GetFloat(MouseSpeedKey, mousePanSpeed);
        keyboardPanSpeed = PlayerPrefs.GetFloat(KeyboardSpeedKey, keyboardPanSpeed);
        filterEnabled = PlayerPrefs.GetInt(FilterKey, filterEnabled ? 1 : 0) == 1;
        filterStrength = PlayerPrefs.GetFloat(FilterStrengthKey, filterStrength);
        language = (GameLanguage)PlayerPrefs.GetInt(LanguageKey, (int)language);
        subtitles = PlayerPrefs.GetInt(SubtitlesKey, subtitles ? 1 : 0) == 1;
    }

    public void SetPanControl(PanControl value) { panControl = value; Save(); }
    public void SetMousePanSpeed(float value) { mousePanSpeed = Mathf.Clamp(value, 10f, 150f); Save(); }
    public void SetKeyboardPanSpeed(float value) { keyboardPanSpeed = Mathf.Clamp(value, 10f, 150f); Save(); }
    public void SetFilterEnabled(bool value) { filterEnabled = value; Save(); }
    public void SetFilterStrength(float value) { filterStrength = Mathf.Clamp01(value); Save(); }
    public void SetLanguage(GameLanguage value) { language = value; Save(); }
    public void SetSubtitles(bool value) { subtitles = value; Save(); }

    private void Save()
    {
        PlayerPrefs.SetInt(PanControlKey, (int)panControl);
        PlayerPrefs.SetFloat(MouseSpeedKey, mousePanSpeed);
        PlayerPrefs.SetFloat(KeyboardSpeedKey, keyboardPanSpeed);
        PlayerPrefs.SetInt(FilterKey, filterEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(FilterStrengthKey, filterStrength);
        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.SetInt(SubtitlesKey, subtitles ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
