using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The operator's screen over the camera feed:
//   feed  - camera label + room, blinking REC, clock, where she is ("SUBJECT IN VIEW" / room name), pan indicator,
//           a burst of static when the camera changes;
//   map   - blueprint of the place drawn from the Rooms: numbered doors (click the remote ones), cameras and
//           their view, her blip, the marked spots of big halls (named: say it to send her there).
//           Tab once: the corner map (bottom right, the picture stays): it follows her (drag = look around, the
//           button = follow again), wheel over it = zoom. Tab again: the large map over the picture, with the side
//           panel (where she is, the camera on screen, the places, the door list, a legend). The same drawing moves
//           between the two windows. Wheel on the map = zoom (toward the cursor), right / middle drag = move it;
//           wheel on the side = scroll;
// Every text follows the game's language (CameraSettings), names of rooms included (Room.NameIn).
//   settings - (Esc) pan control, pan speeds, camera filter on / off + strength, language, subtitles.
// The fixed layout is built by Last Terminal/4; the map and the settings rows are generated at runtime from the scene data.
// This component keeps the feed overlay and coordinates the parts: FacilityMapView (the map, with MapSidePanel),
// SettingsMenuView (the settings rows) and HudUi (the UI factory). Its serialized fields stay here (the scene wires
// them); the parts read them through the internal properties below.
public class SecurityCameraHud : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SystemField = nameof(system);
    public const string SettingsField = nameof(settings);
    public const string PartnerField = nameof(partner);
    public const string RoomsField = nameof(rooms);
    public const string DoorsField = nameof(doors);
    public const string CameraLabelField = nameof(cameraLabel);
    public const string RoomLabelField = nameof(roomLabel);
    public const string RecDotField = nameof(recDot);
    public const string ClockField = nameof(clock);
    public const string SubjectField = nameof(subject);
    public const string PanBarField = nameof(panBar);
    public const string PanMarkerField = nameof(panMarker);
    public const string StaticImageField = nameof(staticImage);
    public const string MapPanelField = nameof(mapPanel);
    public const string MapAreaField = nameof(mapArea);
    public const string MapSideField = nameof(mapSide);
    public const string SettingsPanelField = nameof(settingsPanel);
    public const string SettingsRowsField = nameof(settingsRows);
    public const string AudioField = nameof(audioSource);
    public const string HintField = nameof(hint);
    public const string MapTitleField = nameof(mapTitle);
    public const string SettingsTitleField = nameof(settingsTitle);
    public const string PlaceNameField = nameof(placeName);
    public const string PlaceNamePtField = nameof(placeNamePt);
    public const string MiniPanelField = nameof(miniPanel);
    public const string MiniAreaField = nameof(miniArea);
    public const string MiniTitleField = nameof(miniTitle);
    public const string MiniFollowField = nameof(miniFollow);
    public const string MiniFollowLabelField = nameof(miniFollowLabel);

    // Scene data: the camera system, settings, her transform, the rooms and doors drawn on the map.
    [SerializeField] private SecurityCameraSystem system;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private Transform partner;
    [SerializeField] private Room[] rooms = new Room[0];
    [SerializeField] private Door[] doors = new Door[0];

    // Feed overlay parts.
    [Header("Feed")]
    [SerializeField] private TMP_Text cameraLabel;
    [SerializeField] private TMP_Text roomLabel;
    [SerializeField] private Graphic recDot;
    [SerializeField] private TMP_Text clock;
    [SerializeField] private TMP_Text subject;
    [SerializeField] private RectTransform panBar;
    [SerializeField] private RectTransform panMarker;
    [SerializeField] private RawImage staticImage;
    [SerializeField] private TMP_Text hint;

    // Large map window and its drawing area.
    [Header("Panels")]
    [SerializeField] private CanvasGroup mapPanel;
    [SerializeField] private RectTransform mapArea;
    [Tooltip("Optional: the column next to the map (subject, camera, doors, legend).")]
    [SerializeField] private RectTransform mapSide;
    // Settings panel, its rows parent, and the window titles.
    [SerializeField] private CanvasGroup settingsPanel;
    [Tooltip("2D source of the settings' microphone playback and test sound.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private RectTransform settingsRows;
    [SerializeField] private TMP_Text mapTitle;
    [SerializeField] private TMP_Text settingsTitle;
    [Tooltip("The place on the map title (\"FACILITY\", \"CASINO\").")]
    [SerializeField] private string placeName = "FACILITY";
    // Place name in Portuguese.
    [SerializeField] private string placeNamePt = "INSTALAÇÃO";

    // Corner map window.
    [Header("Corner map (first Tab)")]
    [SerializeField] private CanvasGroup miniPanel;
    [Tooltip("The window of the corner map (the drawing moves in here).")]
    [SerializeField] private RectTransform miniArea;
    // Corner map title.
    [SerializeField] private TMP_Text miniTitle;
    [Tooltip("Follow her again (after dragging the corner map).")]
    [SerializeField] private Button miniFollow;
    // Label of the corner map's "follow" button.
    [SerializeField] private TMP_Text miniFollowLabel;
    [Tooltip("Zoom of the corner map when it opens (1 = the scale of the large map).")]
    [SerializeField, Min(0.1f)] private float miniStartZoom = 1.2f;

    // Colors: text, dim, active camera, doors open / closed / locked, hall spots; static burst time (s).
    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.45f, 1f, 0.55f);
    [SerializeField] private Color dim = new Color(0.2f, 0.5f, 0.27f);
    [SerializeField] private Color activeCamera = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color doorOpen = new Color(0.25f, 1f, 0.35f);
    [SerializeField] private Color doorClosed = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private Color doorLocked = new Color(1f, 0.65f, 0.1f);
    [SerializeField] private Color spotColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private float staticTime = 0.35f;

    // Max map zoom factor.
    [Header("Map zoom / side scroll")]
    [SerializeField, Min(1f)] private float maxMapZoom = 5f;
    [Tooltip("Zoom per wheel notch.")]
    [SerializeField] private float mapZoomStep = 1.2f;
    [Tooltip("Side panel: pixels per wheel notch.")]
    [SerializeField] private float sideScrollStep = 48f;

    // What the parts read: scene data, widgets, colors and map tuning (the serialized fields above, live).
    internal SecurityCameraSystem CameraSystem => system;
    internal CameraSettings Settings => settings;
    internal Transform Partner => partner;
    internal Room[] Rooms => rooms;
    internal Door[] Doors => doors;
    internal CanvasGroup MapPanel => mapPanel;
    internal RectTransform MapArea => mapArea;
    internal RectTransform MapSide => mapSide;
    internal RectTransform SettingsRows => settingsRows;
    internal AudioSource Audio => audioSource;
    internal TMP_Text MapTitle => mapTitle;
    internal TMP_Text SettingsTitle => settingsTitle;
    internal string PlaceName => placeName;
    internal string PlaceNamePt => placeNamePt;
    internal CanvasGroup MiniPanel => miniPanel;
    internal RectTransform MiniArea => miniArea;
    internal TMP_Text MiniTitle => miniTitle;
    internal TMP_Text MiniFollowLabel => miniFollowLabel;
    internal float MiniStartZoom => miniStartZoom;
    internal Color Phosphor => phosphor;
    internal Color Dim => dim;
    internal Color ActiveCameraColor => activeCamera;
    internal Color DoorOpenColor => doorOpen;
    internal Color DoorClosedColor => doorClosed;
    internal Color DoorLockedColor => doorLocked;
    internal Color SpotColor => spotColor;
    internal float MaxMapZoom => maxMapZoom;
    internal float MapZoomStep => mapZoomStep;
    internal float SideScrollStep => sideScrollStep;

    // The parts (made in Awake).
    private FacilityMapView map;
    private SettingsMenuView settingsMenu;
    // The language the texts were last written in, the static burst time left, and the clock's last second.
    private GameLanguage shownLanguage = (GameLanguage)(-1);
    private float staticTimer;
    private int lastSecond = -1;

    // Checks references; hides the panels; builds the map and the settings rows from the scene data.
    private void Awake()
    {
        if (system == null || settings == null)
        {
            Debug.LogError($"{name}: SecurityCameraHud needs the SecurityCameraSystem and CameraSettings.", this);
            enabled = false;
            return;
        }
        map = new FacilityMapView(this);
        settingsMenu = new SettingsMenuView(this);
        HudUi.SetPanel(mapPanel, false);
        HudUi.SetPanel(miniPanel, false);
        HudUi.SetPanel(settingsPanel, false);
        if (miniFollow != null) miniFollow.onClick.AddListener(map.FollowAgain);
        map.BuildMap();
        settingsMenu.Build();
    }

    // Listens to the camera, map, settings and doors.
    private void OnEnable()
    {
        if (system == null) return;
        system.ActiveCameraChanged += OnCameraChanged;
        system.MapChanged += map.OnMapChanged;
        system.SettingsToggled += OnSettingsToggled;
        if (settings != null) { settings.Changed += settingsMenu.Refresh; settings.Changed += RefreshLanguage; }
        foreach (Door d in doors) if (d != null) d.Changed += map.OnDoorChanged;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (system == null) return;
        system.ActiveCameraChanged -= OnCameraChanged;
        system.MapChanged -= map.OnMapChanged;
        system.SettingsToggled -= OnSettingsToggled;
        settingsMenu.SetOpen(false);
        if (settings != null) { settings.Changed -= settingsMenu.Refresh; settings.Changed -= RefreshLanguage; }
        foreach (Door d in doors) if (d != null) d.Changed -= map.OnDoorChanged;
    }

    // First texts and door colors.
    private void Start()
    {
        settingsMenu.Refresh();
        RefreshLanguage();
        map.RefreshAllDoors();
    }

    // The game's language (texts of the parts too).
    internal bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    internal string L(string en, string pt) => Pt ? pt : en;
    internal string NameOf(Room r) => r == null ? "" : r.NameIn(Pt ? GameLanguage.Portuguese : GameLanguage.English).ToUpperInvariant();

    // Map color of a door (map and side column).
    internal Color DoorColor(Door door) => door.IsLocked ? doorLocked : door.IsOpen ? doorOpen : doorClosed;

    // every text that depends on the language, rebuilt when it changes
    private void RefreshLanguage()
    {
        GameLanguage now = Pt ? GameLanguage.Portuguese : GameLanguage.English;
        if (now == shownLanguage) return;
        shownLanguage = now;
        if (hint != null) hint.text = "<mspace=0.6em>" + L("ENTER TALK   TAB MAP (AGAIN: LARGE)   Q/E CAMERA   ESC MENU", "ENTER FALAR   TAB MAPA (DE NOVO: GRANDE)   Q/E CÂMERA   ESC MENU") + "</mspace>";
        if (system.Active != null && roomLabel != null) roomLabel.text = NameOf(system.Active.Room);
        map.RefreshLanguage();
        settingsMenu.RefreshLanguage();
    }

    // A new camera on screen: static burst, its label and room, its tag highlighted on the map.
    private void OnCameraChanged(SecurityCamera cam)
    {
        staticTimer = staticTime;
        if (cameraLabel != null) cameraLabel.text = cam.Label;
        if (roomLabel != null) roomLabel.text = NameOf(cam.Room);
        map.ShowActiveCamera(system.ActiveIndex);
    }

    // Each frame: clock, REC blink, pan marker, subject line, static, and the map while it is open.
    private void Update()
    {
        if (recDot != null) recDot.enabled = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;

        // clock: rebuilt once per second
        DateTime now = DateTime.Now;
        if (clock != null && now.Second != lastSecond)
        {
            lastSecond = now.Second;
            clock.text = now.ToString("MM/dd/yyyy  HH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (subject != null) subject.text = SubjectText();

        if (panBar != null && panMarker != null)
        {
            float half = panBar.rect.width * 0.5f;
            panMarker.anchoredPosition = new Vector2(system.PanAmount * half, panMarker.anchoredPosition.y);
        }

        if (staticImage != null)
        {
            staticTimer = Mathf.Max(0f, staticTimer - Time.unscaledDeltaTime);
            float a = staticTime > 0f ? staticTimer / staticTime : 0f;
            staticImage.enabled = a > 0f;
            if (a > 0f)
            {
                staticImage.color = new Color(1f, 1f, 1f, a);
                staticImage.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 2f, 2f);
            }
        }

        if (system.IsMapOpen) map.Tick();
        if (system.IsSettingsOpen) settingsMenu.Tick();
    }

    // "SUBJECT IN VIEW" / her room / signal lost.
    private string SubjectText()
    {
        if (partner == null) return "";
        SecurityCamera cam = system.Active;
        Room here = RoomOf(partner.position);
        if (cam != null && here != null && cam.Room == here) return L("SUBJECT: IN VIEW", "HELENA: NA CÂMERA");
        return here != null ? L("SUBJECT: ", "HELENA: ") + NameOf(here) : L("SUBJECT: NO SIGNAL", "HELENA: SEM SINAL");
    }

    // The room a point is in (null outside).
    internal Room RoomOf(Vector3 p)
    {
        foreach (Room r in rooms)
            if (r != null && r.Contains(p)) return r;
        return null;
    }

    // Settings opened / closed (the microphone test runs while they are open).
    private void OnSettingsToggled(bool open)
    {
        HudUi.SetPanel(settingsPanel, open);
        settingsMenu.SetOpen(open);
    }
}
