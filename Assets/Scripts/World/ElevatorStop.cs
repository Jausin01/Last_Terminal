using UnityEngine;

// A place an elevator goes to (one asset per place: "Casino", "Facility - Office"...). Edit it here and every sign
// that points to it changes. An elevator in a scene says where it is (its stop) and where it goes (another stop).
[CreateAssetMenu(menuName = "Last Terminal/Elevator Stop", fileName = "Stop_Place")]
public class ElevatorStop : ScriptableObject
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string IdField = nameof(id);
    public const string DisplayNameField = nameof(displayName);
    public const string DisplayNamePtField = nameof(displayNamePt);
    public const string SceneNameField = nameof(sceneName);
    public const string RequiredKeyField = nameof(requiredKey);
    public const string FloorField = nameof(floor);

    [Tooltip("Stable id (saves, arrivals). Don't change it once used.")]
    [SerializeField] private string id = "stop";
    // Name on the signs, per language.
    [SerializeField] private string displayName = "Casino";
    [SerializeField] private string displayNamePt = "Cassino";
    [Tooltip("The scene the elevator loads (must be in the Build Settings). Empty / missing = out of service.")]
    [SerializeField] private string sceneName;
    [Tooltip("The key she needs to ride there (none = free).")]
    [SerializeField] private ElevatorKey requiredKey;
    [Tooltip("On the sign and the floor indicator (\"B2\", \"5\"...).")]
    [SerializeField] private string floor = "1";

    // Read-only data.
    public string Id => id;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public ElevatorKey RequiredKey => requiredKey;
    public string Floor => floor;

    // Name in a language.
    public string NameIn(GameLanguage language) =>
        language == GameLanguage.Portuguese && !string.IsNullOrEmpty(displayNamePt) ? displayNamePt : displayName;

    // the lexicon key of its name ("go to the casino")
    public string LexiconKey => "stop_" + id;
}
