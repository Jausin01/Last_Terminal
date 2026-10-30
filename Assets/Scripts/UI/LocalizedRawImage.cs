using UnityEngine;
using UnityEngine.UI;

// A picture with words in it (the PC's wallpaper...): the English or the Portuguese version, following the game's
// language (CameraSettings). Built by the Last Terminal menus; every reference is wired there.
[RequireComponent(typeof(RawImage))]
public class LocalizedRawImage : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string TargetField = nameof(target);
    public const string EnglishField = nameof(english);
    public const string PortugueseField = nameof(portuguese);
    public const string SettingsField = nameof(settings);

    // The image shown, its two versions, the language.
    [SerializeField] private RawImage target;
    [SerializeField] private Texture english;
    [Tooltip("Empty = the English one.")]
    [SerializeField] private Texture portuguese;
    [SerializeField] private CameraSettings settings;

    // Follows the language.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        Refresh();
    }

    // Stops following.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
    }

    // The version for the current language.
    private void Refresh()
    {
        if (target == null) return;
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese && portuguese != null;
        target.texture = pt ? portuguese : english;
    }
}
