using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The ride: the screen goes dark with the floor indicator, her state goes to the GameSession (SaveSystem) and the
// destination scene loads in the background (a short load). In the new scene she starts in the car of the elevator
// of that stop (SaveSystem.TransferArrived) and the picture comes back as the doors open.
public class ElevatorTravel : MonoBehaviour
{
    public const string SaveSystemField = nameof(saveSystem);
    public const string SessionField = nameof(session);
    public const string UserField = nameof(user);
    public const string ElevatorsField = nameof(elevators);
    public const string FaderField = nameof(fader);
    public const string LabelField = nameof(label);
    public const string SettingsField = nameof(settings);
    public const string CameraSystemField = nameof(cameraSystem);

    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private GameSession session;
    [SerializeField] private PartnerElevatorUser user;
    [Tooltip("The elevators of this scene (arrivals are matched by their stop).")]
    [SerializeField] private Elevator[] elevators = new Elevator[0];
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private TMP_Text label;
    [SerializeField] private CameraSettings settings;
    [Tooltip("Arriving: the view goes to the camera that sees her.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private float fadeTime = 0.8f;
    [Tooltip("At least this long in the dark (the ride), even when the scene loads faster (s).")]
    [SerializeField] private float minimumRide = 1.2f;

    private bool going;

    public bool IsTraveling => going;

    private void Awake()
    {
        if (saveSystem == null || session == null || user == null || fader == null)
        {
            Debug.LogError($"{name}: ElevatorTravel is missing references (run the scene menu).", this);
            enabled = false;
            return;
        }
        // arriving from another scene: dark from the first frame
        fader.alpha = session.HasTransfer ? 1f : 0f;
        fader.blocksRaycasts = false;
        if (label != null) label.text = "";
    }

    private void OnEnable()
    {
        if (saveSystem != null) saveSystem.TransferArrived += OnArrived;
    }

    private void OnDisable()
    {
        if (saveSystem != null) saveSystem.TransferArrived -= OnArrived;
    }

    public bool CanGo(ElevatorStop stop) =>
        stop != null && !string.IsNullOrEmpty(stop.SceneName) && Application.CanStreamedLevelBeLoaded(stop.SceneName);

    public bool Go(Elevator from)
    {
        if (going || from == null || !CanGo(from.Destination)) { user.Cancel(); return false; }
        StartCoroutine(Ride(from.Destination, from.Here));
        return true;
    }

    private IEnumerator Ride(ElevatorStop to, ElevatorStop from)
    {
        going = true;
        GameLanguage lang = settings != null ? settings.Language : GameLanguage.English;
        string title = lang == GameLanguage.Portuguese ? "ELEVADOR" : "ELEVATOR";
        if (label != null) label.text = $"<size=55%>{title}</size>\n{to.NameIn(lang).ToUpperInvariant()}  <size=70%>{to.Floor}</size>";
        yield return Fade(1f, fadeTime);
        saveSystem.PrepareTransfer(to.Id, from != null ? from.Id : null);
        float started = Time.unscaledTime;
        AsyncOperation load = SceneManager.LoadSceneAsync(to.SceneName);
        load.allowSceneActivation = false;
        while (load.progress < 0.9f || Time.unscaledTime - started < minimumRide) yield return null;
        load.allowSceneActivation = true;
    }

    // she comes out of an elevator of this stop: the one that goes back where she came from, else the first one
    private void OnArrived(string stopId, string originId)
    {
        if (stopId == null) { WatchHer(); StartCoroutine(Fade(0f, 0.3f)); return; }   // a save of this scene loaded from another one
        Elevator at = null;
        foreach (Elevator e in elevators)
        {
            if (e == null || e.Here == null || e.Here.Id != stopId) continue;
            if (at == null) at = e;
            if (e.Destination != null && e.Destination.Id == originId) { at = e; break; }
        }
        if (at != null)
        {
            user.Arrive(at);
            if (cameraSystem != null && at.WatchCamera != null) cameraSystem.Switch(at.WatchCamera);
            else WatchHer();
            if (label != null) label.text = "";
            StartCoroutine(Fade(0f, fadeTime));
            return;
        }
        Debug.LogWarning($"[Elevator] No elevator for stop '{stopId}' in this scene: she starts where she is.");
        WatchHer();
        StartCoroutine(Fade(0f, fadeTime));
    }

    // the first camera of the room she is in
    private void WatchHer()
    {
        if (cameraSystem == null) return;
        Vector3 at = user.transform.position;
        foreach (SecurityCamera cam in cameraSystem.Cameras)
        {
            if (cam == null || cam.Room == null || !cam.Room.Contains(at)) continue;
            cameraSystem.Switch(cam);
            return;
        }
    }

    private IEnumerator Fade(float to, float time)
    {
        float from = fader.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            fader.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        fader.alpha = to;
    }
}
