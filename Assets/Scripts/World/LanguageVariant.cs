using UnityEngine;

// Shows the English or the Portuguese version of a thing written in the world (the blood on the wall, a sign),
// following the game's language, also when it changes in the options.
public class LanguageVariant : MonoBehaviour
{
    public const string SettingsField = nameof(settings);
    public const string EnglishField = nameof(english);
    public const string PortugueseField = nameof(portuguese);

    [SerializeField] private CameraSettings settings;
    [SerializeField] private GameObject english;
    [SerializeField] private GameObject portuguese;

    private void OnEnable()
    {
        if (settings != null) settings.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Apply;
    }

    private void Apply()
    {
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        if (english != null) english.SetActive(!pt);
        if (portuguese != null) portuguese.SetActive(pt);
    }
}
