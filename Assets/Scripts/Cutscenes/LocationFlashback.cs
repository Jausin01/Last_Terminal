using UnityEngine;

// A location leaves for a story scene in between (a flashback) and comes back to the same moment: her state is
// captured where she stands (like a save: position, story, the world) and handed to the GameSession, the place to
// come back to is remembered, and the flashback scene loads behind the CCTV signal screen (warm: a memory).
// Called by a cutscene (step "Call event") - e.g. the asylum's stairwell (Last Terminal/17 wires it).
public class LocationFlashback : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SaveSystemField = nameof(saveSystem);
    public const string SessionField = nameof(session);
    public const string PartnerField = nameof(partner);
    public const string SettingsField = nameof(settings);
    public const string LoaderField = nameof(loaderPrefab);
    public const string SceneField = nameof(flashbackScene);
    public const string PlaceEnField = nameof(placeEn);
    public const string PlacePtField = nameof(placePt);

    // Her state (captured), the session that carries it, her body (where she stands), the language.
    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private GameSession session;
    [SerializeField] private Transform partner;
    [SerializeField] private CameraSettings settings;
    [Tooltip("The CCTV signal loading screen (prefab).")]
    [SerializeField] private SignalLoader loaderPrefab;
    [Tooltip("The flashback scene (in the build).")]
    [SerializeField] private string flashbackScene = "Cutscene02_Bar";
    [Tooltip("Where the camera is \"reconnecting\" to (the loading screen).")]
    [SerializeField] private string placeEn = "WARD B — VIOLENT PATIENTS";
    [SerializeField] private string placePt = "ALA B — VIOLENTOS";

    // Leaves for the flashback (once).
    private bool gone;

    // Captures her, remembers this place and loads the flashback.
    public void Go()
    {
        if (gone) return;
        if (!Application.CanStreamedLevelBeLoaded(flashbackScene))
        {
            Debug.LogWarning($"{name}: the flashback scene \"{flashbackScene}\" is not in the build (Last Terminal/25 adds it).", this);
            return;
        }
        gone = true;
        if (saveSystem != null && session != null && partner != null)
        {
            session.SetTransfer(saveSystem.Capture("", partner.position, partner.eulerAngles.y), null, null, true);
            session.SetReturnLocation(gameObject.scene.name);
            session.SetOperating(true);
        }
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        SignalLoader loader = Instantiate(loaderPrefab);
        loader.Play(flashbackScene, true, pt ? placePt : placeEn, pt);
    }
}
