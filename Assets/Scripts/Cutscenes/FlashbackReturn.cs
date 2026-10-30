using UnityEngine;

// The end of a flashback scene: back to the location it was entered from (GameSession.ReturnLocation; played on its
// own: the fallback), his room first with him at the cameras (GameScenes), Helena where she was (the transfer waits
// in the session), behind the CCTV signal screen (cold). Called by the flashback's cutscene (step "Call event").
// The session is referenced here so the asset stays loaded with this scene (an unreferenced asset is unloaded).
public class FlashbackReturn : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SessionField = nameof(session);
    public const string SettingsField = nameof(settings);
    public const string LoaderField = nameof(loaderPrefab);
    public const string FallbackField = nameof(fallbackLocation);

    [SerializeField] private GameSession session;
    [SerializeField] private CameraSettings settings;
    [Tooltip("The CCTV signal loading screen (prefab).")]
    [SerializeField] private SignalLoader loaderPrefab;
    [Tooltip("Where to go when the scene was played on its own (no location waiting).")]
    [SerializeField] private string fallbackLocation = "Asylum_IsolationWing";
    [SerializeField] private string placeEn = "ISOLATION WING — STAIRWELL";
    [SerializeField] private string placePt = "ALA DE ISOLAMENTO — ESCADA";

    // Back once.
    private bool gone;

    // Loads the location back (his room at the cameras, the location over it).
    public void Go()
    {
        if (gone) return;
        gone = true;
        string location = session != null && !string.IsNullOrEmpty(session.ReturnLocation) ? session.ReturnLocation : fallbackLocation;
        if (!Application.CanStreamedLevelBeLoaded(location))
        {
            Debug.LogWarning($"{name}: \"{location}\" is not in the build: staying here.", this);
            return;
        }
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        SignalLoader loader = Instantiate(loaderPrefab);
        loader.Play(GameScenes.OpenLocation(session, location, true), false, pt ? placePt : placeEn, pt);
    }
}
