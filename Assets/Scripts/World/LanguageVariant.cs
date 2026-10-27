using UnityEngine;

// Shows the English or the Portuguese version of a thing written in the world (the blood on the wall, a sign),
// following the game's language, also when it changes in the options.
public class LanguageVariant : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SettingsField = nameof(settings);
    public const string EnglishField = nameof(english);
    public const string PortugueseField = nameof(portuguese);

    // The settings (language) and the two versions.
    [SerializeField] private CameraSettings settings;
    [SerializeField] private GameObject english;
    [SerializeField] private GameObject portuguese;

    // Follows language changes.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Apply;
        Apply();
    }

    // Stops following.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Apply;
    }

    // Shows the version of the current language.
    private void Apply()
    {
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        if (english != null) english.SetActive(!pt);
        if (portuguese != null) portuguese.SetActive(pt);
    }
}
