using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The operator's screen over the camera feed:
//   feed  - camera label + room, blinking REC, clock, where she is ("SUBJECT IN VIEW" / room name), pan indicator,
//           a burst of static when the camera changes;
//   map   - (Tab) blueprint of the place drawn from the Rooms: numbered doors (click the remote ones), cameras and
//           their view, her blip, the marked spots of big halls (named: say it to send her there); side panel with
//           where she is, the camera on screen, the places, the door list and a legend;
//           mouse wheel on the map = zoom (toward the cursor), right / middle drag = move it; wheel on the side = scroll;
// Every text follows the game's language (CameraSettings), names of rooms included (Room.NameIn).
//   settings - (Esc) pan control, pan speeds, camera filter on / off + strength, language, subtitles.
// The fixed layout is built by Last Terminal/4; the map and the settings rows are generated here from the scene data.
public class SecurityCameraHud : MonoBehaviour
{
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
    public const string HintField = nameof(hint);
    public const string MapTitleField = nameof(mapTitle);
    public const string SettingsTitleField = nameof(settingsTitle);
    public const string PlaceNameField = nameof(placeName);
    public const string PlaceNamePtField = nameof(placeNamePt);

    [SerializeField] private SecurityCameraSystem system;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private Transform partner;
    [SerializeField] private Room[] rooms = new Room[0];
    [SerializeField] private Door[] doors = new Door[0];

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

    [Header("Panels")]
    [SerializeField] private CanvasGroup mapPanel;
    [SerializeField] private RectTransform mapArea;
    [Tooltip("Optional: the column next to the map (subject, camera, doors, legend).")]
    [SerializeField] private RectTransform mapSide;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private RectTransform settingsRows;
    [SerializeField] private TMP_Text mapTitle;
    [SerializeField] private TMP_Text settingsTitle;
    [Tooltip("The place on the map title (\"FACILITY\", \"CASINO\").")]
    [SerializeField] private string placeName = "FACILITY";
    [SerializeField] private string placeNamePt = "INSTALAÇÃO";

    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.45f, 1f, 0.55f);
    [SerializeField] private Color dim = new Color(0.2f, 0.5f, 0.27f);
    [SerializeField] private Color activeCamera = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color doorOpen = new Color(0.25f, 1f, 0.35f);
    [SerializeField] private Color doorClosed = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private Color doorLocked = new Color(1f, 0.65f, 0.1f);
    [SerializeField] private Color spotColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private float staticTime = 0.35f;

    [Header("Map zoom / side scroll")]
    [SerializeField, Min(1f)] private float maxMapZoom = 5f;
    [Tooltip("Zoom per wheel notch.")]
    [SerializeField] private float mapZoomStep = 1.2f;
    [Tooltip("Side panel: pixels per wheel notch.")]
    [SerializeField] private float sideScrollStep = 48f;

    private const float RoomAlpha = 0.07f;
    private readonly List<Image> cameraButtons = new List<Image>();
    private readonly List<(Door door, Image image)> doorIcons = new List<(Door, Image)>();
    private readonly List<(Door door, Image[] edges)> doorBadges = new List<(Door, Image[])>();
    private readonly List<(Door door, TMP_Text row)> doorRows = new List<(Door, TMP_Text)>();
    private readonly List<(Room room, Image fill, Image[] border)> roomViews = new List<(Room, Image, Image[])>();
    private readonly List<(Room room, TMP_Text text)> roomNames = new List<(Room, TMP_Text)>();
    private readonly List<(Room room, TMP_Text text)> placeRows = new List<(Room, TMP_Text)>();
    private readonly List<(TMP_Text text, string en, string pt)> fixedTexts = new List<(TMP_Text, string, string)>();
    private readonly string[] settingsLabelsEn = new string[SettingsRowCount], settingsLabelsPt = new string[SettingsRowCount];
    private readonly TMP_Text[] settingsLabels = new TMP_Text[SettingsRowCount];
    private TMP_Text closeLabel;
    private GameLanguage shownLanguage = (GameLanguage)(-1);
    private readonly List<(SecurityCamera cam, Image wedge, Image dot)> cameraViews = new List<(SecurityCamera, Image, Image)>();
    private Image partnerBlip, partnerRing, partnerHeading;
    private TMP_Text partnerName;
    private TMP_Text sideSubject, sideSubjectState, sideCamera, sideCameraRoom;
    private float sideTimer;
    private Rect worldBounds;
    private float mapScale;
    private RectTransform mapContent;          // everything drawn on the map: scaled / moved by the zoom
    private float mapZoom = 1f;
    private RectTransform sideContent;         // the side rows: scrolled inside sideViewport
    private RectTransform sideViewport;
    private float sideHeight;
    private float sideScroll;
    private Vector2 lastMouse;
    private Canvas canvas;
    private float staticTimer;
    private int lastSecond = -1;
    private const int SettingsRowCount = 7;
    private readonly Action[] settingsRefresh = new Action[SettingsRowCount];
    private TMP_Text[] settingsValues;

    private void Awake()
    {
        if (system == null || settings == null)
        {
            Debug.LogError($"{name}: SecurityCameraHud needs the SecurityCameraSystem and CameraSettings.", this);
            enabled = false;
            return;
        }
        SetPanel(mapPanel, false);
        SetPanel(settingsPanel, false);
        BuildMap();
        BuildSettings();
    }

    private void OnEnable()
    {
        if (system == null) return;
        system.ActiveCameraChanged += OnCameraChanged;
        system.MapToggled += OnMapToggled;
        system.SettingsToggled += OnSettingsToggled;
        if (settings != null) { settings.Changed += RefreshSettings; settings.Changed += RefreshLanguage; }
        foreach (Door d in doors) if (d != null) d.Changed += OnDoorChanged;
    }

    private void OnDisable()
    {
        if (system == null) return;
        system.ActiveCameraChanged -= OnCameraChanged;
        system.MapToggled -= OnMapToggled;
        system.SettingsToggled -= OnSettingsToggled;
        if (settings != null) { settings.Changed -= RefreshSettings; settings.Changed -= RefreshLanguage; }
        foreach (Door d in doors) if (d != null) d.Changed -= OnDoorChanged;
    }

    private void Start()
    {
        RefreshSettings();
        RefreshLanguage();
        foreach (var (door, _) in doorIcons) OnDoorChanged(door);
    }

    // ------------------------------------------------------------------ language
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private string L(string en, string pt) => Pt ? pt : en;
    private string NameOf(Room r) => r == null ? "" : r.NameIn(Pt ? GameLanguage.Portuguese : GameLanguage.English).ToUpperInvariant();

    // a text that never changes except with the language
    private TMP_Text Fixed(TMP_Text t, string en, string pt)
    {
        fixedTexts.Add((t, en, pt));
        t.text = L(en, pt);
        return t;
    }

    // every text that depends on the language, rebuilt when it changes
    private void RefreshLanguage()
    {
        GameLanguage now = Pt ? GameLanguage.Portuguese : GameLanguage.English;
        if (now == shownLanguage) return;
        shownLanguage = now;
        foreach (var (t, en, pt) in fixedTexts) if (t != null) t.text = L(en, pt);
        foreach (var (room, t) in roomNames) t.text = "<b>" + NameOf(room) + "</b>";
        foreach (var (room, t) in placeRows) t.text = "<color=#" + ColorUtility.ToHtmlStringRGB(spotColor) + "><b>></b></color>  " + NameOf(room);
        for (int i = 0; i < SettingsRowCount; i++)
            if (settingsLabels[i] != null) settingsLabels[i].text = L(settingsLabelsEn[i], settingsLabelsPt[i]);
        if (closeLabel != null) closeLabel.text = L("CLOSE", "FECHAR");
        if (hint != null) hint.text = "<mspace=0.6em>" + L("ENTER TALK   TAB MAP   Q/E CAMERA   ESC SETTINGS", "ENTER FALAR   TAB MAPA   Q/E CÂMERA   ESC OPÇÕES") + "</mspace>";
        string place = L(placeName, string.IsNullOrEmpty(placeNamePt) ? placeName : placeNamePt).ToUpperInvariant();
        if (mapTitle != null) mapTitle.text = "<mspace=0.6em>" + L(place + " MAP  //  SECURITY NETWORK", "MAPA: " + place + "  //  REDE DE SEGURANÇA") + "</mspace>";
        if (settingsTitle != null) settingsTitle.text = "<mspace=0.6em>" + L("SETTINGS", "CONFIGURAÇÕES") + "</mspace>";
        if (system.Active != null && roomLabel != null) roomLabel.text = NameOf(system.Active.Room);
        RefreshDoorRows();
        RefreshSettings();
        sideTimer = 0f;
    }

    // ------------------------------------------------------------------ feed
    private void OnCameraChanged(SecurityCamera cam)
    {
        staticTimer = staticTime;
        if (cameraLabel != null) cameraLabel.text = cam.Label;
        if (roomLabel != null) roomLabel.text = NameOf(cam.Room);
        for (int i = 0; i < cameraButtons.Count; i++)
        {
            bool on = i == system.ActiveIndex;
            Color c = on ? activeCamera : phosphor;
            cameraButtons[i].color = new Color(c.r, c.g, c.b, on ? 0.45f : 0.15f);
            cameraButtons[i].GetComponentInChildren<TMP_Text>().color = on ? Color.white : phosphor;
        }
    }

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

        if (system.IsMapOpen)
        {
            UpdateMap();
            MapInput();
        }
    }

    // ------------------------------------------------------------------ map zoom / side scroll (mouse)
    private void MapInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screen = mouse.position.ReadValue();
        float wheel = mouse.scroll.ReadValue().y;
        int notch = Mathf.Abs(wheel) > 0.01f ? (int)Mathf.Sign(wheel) : 0;

        if (mapArea != null && mapContent != null && RectTransformUtility.RectangleContainsScreenPoint(mapArea, screen, eventCamera))
        {
            if (notch != 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, screen, eventCamera, out Vector2 local))
            {
                // the point under the cursor stays under it
                float next = Mathf.Clamp(mapZoom * Mathf.Pow(mapZoomStep, notch), 1f, maxMapZoom);
                Vector2 onContent = (local - mapContent.anchoredPosition) / mapZoom;
                mapZoom = next;
                SetMapView(local - onContent * mapZoom);
            }
            bool drag = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (drag && (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)) lastMouse = screen;
            else if (drag)
            {
                float toLocal = canvas != null ? 1f / canvas.scaleFactor : 1f;
                SetMapView(mapContent.anchoredPosition + (screen - lastMouse) * toLocal);
                lastMouse = screen;
            }
        }
        else if (sideViewport != null && notch != 0 && RectTransformUtility.RectangleContainsScreenPoint(mapSide, screen, eventCamera))
        {
            sideScroll = Mathf.Clamp(sideScroll - notch * sideScrollStep, 0f, Mathf.Max(0f, sideHeight - sideViewport.rect.height));
            sideContent.anchoredPosition = new Vector2(0f, sideScroll);
        }
    }

    // the zoomed map never leaves the well (at zoom 1 it fits it exactly)
    private void SetMapView(Vector2 position)
    {
        mapContent.localScale = new Vector3(mapZoom, mapZoom, 1f);
        Vector2 limit = mapArea.rect.size * (mapZoom - 1f) * 0.5f;
        mapContent.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
    }

    private string SubjectText()
    {
        if (partner == null) return "";
        SecurityCamera cam = system.Active;
        Room here = RoomOf(partner.position);
        if (cam != null && here != null && cam.Room == here) return L("SUBJECT: IN VIEW", "HELENA: NA CÂMERA");
        return here != null ? L("SUBJECT: ", "HELENA: ") + NameOf(here) : L("SUBJECT: NO SIGNAL", "HELENA: SEM SINAL");
    }

    private Room RoomOf(Vector3 p)
    {
        foreach (Room r in rooms)
            if (r != null && r.Contains(p)) return r;
        return null;
    }

    // ------------------------------------------------------------------ map
    // Blueprint of the facility: grid, rooms (hers lit up, the watched one framed in amber), doors with their number
    // badge (color = state, click = operate the remote ones), cameras with their field of view, her blip and heading.
    // Side panel: where she is, the camera on screen, every door (number, rooms, state), legend, controls.
    private void BuildMap()
    {
        if (mapArea == null || rooms.Length == 0) return;
        bool first = true;
        foreach (Room r in rooms)
        {
            if (r == null) continue;
            if (first) { worldBounds = r.FloorRect; first = false; }
            else worldBounds = Rect.MinMaxRect(Mathf.Min(worldBounds.xMin, r.FloorRect.xMin), Mathf.Min(worldBounds.yMin, r.FloorRect.yMin),
                                               Mathf.Max(worldBounds.xMax, r.FloorRect.xMax), Mathf.Max(worldBounds.yMax, r.FloorRect.yMax));
        }
        Vector2 area = mapArea.rect.size * 0.92f;
        mapScale = Mathf.Min(area.x / Mathf.Max(worldBounds.width, 0.01f), area.y / Mathf.Max(worldBounds.height, 0.01f));
        mapContent = (RectTransform)new GameObject("MapContent", typeof(RectTransform)).transform;
        mapContent.gameObject.layer = mapArea.gameObject.layer;
        mapContent.SetParent(mapArea, false);
        mapContent.sizeDelta = mapArea.rect.size;

        BuildGrid();

        // rooms: fill + crisp border + name
        foreach (Room r in rooms)
        {
            if (r == null) continue;
            Rect f = r.FloorRect;
            Vector2 size = f.size * mapScale;
            Image fill = NewImage(mapContent, "Room_" + r.DisplayName, new Color(phosphor.r, phosphor.g, phosphor.b, RoomAlpha));
            fill.raycastTarget = false;
            fill.rectTransform.anchoredPosition = MapPoint(new Vector3(f.center.x, 0f, f.center.y));
            fill.rectTransform.sizeDelta = size;
            Image[] border = Frame(fill.rectTransform, 2f, new Color(phosphor.r, phosphor.g, phosphor.b, 0.75f));
            roomViews.Add((r, fill, border));
            if (r.Spot != null) continue;   // named at its spot (below)
            TMP_Text t = NewText(fill.rectTransform, "", 18f, phosphor);
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = Mathf.Clamp(Mathf.Min(size.x, size.y) / 3f, 11f, 18f);
            bool sideways = size.y > size.x * 1.5f;   // a long narrow hall: the name along it
            RectTransform tr = t.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = sideways ? new Vector2(size.y - 8f, size.x - 8f) : new Vector2(size.x - 8f, size.y - 8f);
            tr.localRotation = sideways ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            roomNames.Add((r, t));
        }

        // the spots of big halls: a diamond where she goes, its name beside it (what to say to send her)
        foreach (Room r in rooms)
        {
            if (r == null || r.Spot == null) continue;
            Vector2 at = MapPoint(r.Spot.position);
            Image mark = NewImage(mapContent, "Spot_" + r.DisplayName, spotColor);
            mark.raycastTarget = false;
            mark.rectTransform.sizeDelta = new Vector2(11f, 11f);
            mark.rectTransform.anchoredPosition = at;
            mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image hole = NewImage(mark.rectTransform, "Hole", new Color(0.02f, 0.06f, 0.035f, 1f));
            hole.raycastTarget = false;
            hole.rectTransform.sizeDelta = new Vector2(5f, 5f);
            TMP_Text t = NewText(mapContent, "", 13, spotColor);
            t.rectTransform.sizeDelta = new Vector2(180f, 18f);
            t.rectTransform.anchoredPosition = at + new Vector2(0f, 14f);
            roomNames.Add((r, t));
        }

        // cameras: field of view wedge + a clickable tag
        IReadOnlyList<SecurityCamera> cams = system.Cameras;
        for (int i = 0; i < cams.Count; i++)
        {
            int index = i;
            SecurityCamera cam = cams[i];
            Image wedge = NewImage(mapContent, "View_" + cam.Label, new Color(phosphor.r, phosphor.g, phosphor.b, 0.12f));
            wedge.sprite = Wedge;
            wedge.raycastTarget = false;
            wedge.rectTransform.pivot = new Vector2(0.5f, 0f);   // apex = the camera
            wedge.rectTransform.sizeDelta = new Vector2(84f, 64f);
            wedge.rectTransform.anchoredPosition = MapPoint(cam.transform.position);
            Image dot = NewImage(mapContent, "Cam_" + cam.Label, phosphor);
            dot.sprite = Circle;
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(8f, 8f);
            dot.rectTransform.anchoredPosition = MapPoint(cam.transform.position);
            string number = cam.Label.Replace("CAM", "").Trim();
            NewButton(mapContent, number, 12, () => system.Switch(index), out Image tag);
            tag.rectTransform.sizeDelta = new Vector2(30f, 18f);
            tag.rectTransform.anchoredPosition = MapPoint(cam.transform.position) + InwardOffset(cam.transform.position, 24f);
            cameraButtons.Add(tag);
            cameraViews.Add((cam, wedge, dot));
        }

        // doors: a bar along the wall (state color) and the number badge on it
        foreach (Door d in doors)
        {
            if (d == null) continue;
            Door door = d;
            Vector3 along = door.transform.right;
            bool horizontal = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
            Image bar = NewImage(mapContent, "Door_" + door.DisplayName, doorClosed);
            bar.raycastTarget = false;
            bar.rectTransform.sizeDelta = horizontal ? new Vector2(1.6f * mapScale, 6f) : new Vector2(6f, 1.6f * mapScale);
            bar.rectTransform.anchoredPosition = MapPoint(door.transform.position);

            Image badge;
            if (door.IsRemoteControlled)
            {
                Button b = NewButton(mapContent, "", 14, () => door.Toggle(), out badge);
                b.transition = Selectable.Transition.ColorTint;
            }
            else badge = NewImage(mapContent, "Badge_" + door.DisplayName, Color.white);
            badge.color = new Color(0.02f, 0.06f, 0.035f, 0.95f);
            badge.rectTransform.sizeDelta = new Vector2(24f, 24f);
            badge.rectTransform.anchoredPosition = MapPoint(door.transform.position);
            Image[] edges = Frame(badge.rectTransform, 2f, doorClosed);
            TMP_Text n = NewText(badge.rectTransform, door.Number > 0 ? "<b>" + door.Number + "</b>" : "-", 15, Color.white);
            Stretch(n.rectTransform, 0f);
            doorIcons.Add((door, bar));
            doorBadges.Add((door, edges));
        }

        // her: pulse ring, heading tick, dot, name
        partnerRing = NewImage(mapContent, "PartnerRing", new Color(1f, 0.25f, 0.2f, 0.6f));
        partnerRing.sprite = Ring;
        partnerRing.raycastTarget = false;
        partnerHeading = NewImage(mapContent, "PartnerHeading", new Color(1f, 0.35f, 0.3f));
        partnerHeading.raycastTarget = false;
        partnerHeading.rectTransform.pivot = new Vector2(0.5f, 0f);
        partnerHeading.rectTransform.sizeDelta = new Vector2(3f, 15f);
        partnerBlip = NewImage(mapContent, "PartnerBlip", new Color(1f, 0.25f, 0.2f));
        partnerBlip.sprite = Circle;
        partnerBlip.raycastTarget = false;
        partnerBlip.rectTransform.sizeDelta = new Vector2(12f, 12f);
        partnerName = NewText(mapContent, "<b>HELENA</b>", 11, new Color(1f, 0.45f, 0.4f));
        partnerName.rectTransform.sizeDelta = new Vector2(70f, 14f);

        BuildMapSide();
    }

    // faint lines every meter, stronger every 4
    private void BuildGrid()
    {
        Color minor = new Color(phosphor.r, phosphor.g, phosphor.b, 0.045f);
        Color major = new Color(phosphor.r, phosphor.g, phosphor.b, 0.1f);
        Vector2 size = mapContent.sizeDelta;
        int x0 = Mathf.FloorToInt(worldBounds.xMin - 2f), x1 = Mathf.CeilToInt(worldBounds.xMax + 2f);
        int z0 = Mathf.FloorToInt(worldBounds.yMin - 2f), z1 = Mathf.CeilToInt(worldBounds.yMax + 2f);
        for (int x = x0; x <= x1; x++)
        {
            float px = MapPoint(new Vector3(x, 0f, 0f)).x;
            if (Mathf.Abs(px) > size.x * 0.5f) continue;
            Image l = NewImage(mapContent, "GridX", x % 4 == 0 ? major : minor);
            l.raycastTarget = false;
            l.rectTransform.sizeDelta = new Vector2(1f, size.y);
            l.rectTransform.anchoredPosition = new Vector2(px, 0f);
        }
        for (int z = z0; z <= z1; z++)
        {
            float py = MapPoint(new Vector3(0f, 0f, z)).y;
            if (Mathf.Abs(py) > size.y * 0.5f) continue;
            Image l = NewImage(mapContent, "GridZ", z % 4 == 0 ? major : minor);
            l.raycastTarget = false;
            l.rectTransform.sizeDelta = new Vector2(size.x, 1f);
            l.rectTransform.anchoredPosition = new Vector2(0f, py);
        }
    }

    private void BuildMapSide()
    {
        if (mapSide == null) return;
        // the rows scroll (mouse wheel) inside a viewport; the controls stay at the bottom
        sideViewport = (RectTransform)new GameObject("SideViewport", typeof(RectTransform)).transform;
        sideViewport.gameObject.layer = mapSide.gameObject.layer;
        sideViewport.SetParent(mapSide, false);
        Stretch(sideViewport, 0f);
        sideViewport.offsetMin = new Vector2(0f, KeysHeight + 8f);
        sideViewport.gameObject.AddComponent<RectMask2D>();
        sideContent = (RectTransform)new GameObject("SideContent", typeof(RectTransform)).transform;
        sideContent.gameObject.layer = mapSide.gameObject.layer;
        sideContent.SetParent(sideViewport, false);
        sideContent.anchorMin = new Vector2(0f, 1f);
        sideContent.anchorMax = new Vector2(1f, 1f);
        sideContent.pivot = new Vector2(0.5f, 1f);
        sideContent.anchoredPosition = Vector2.zero;
        float y = 0f;
        TMP_Text Header(string en, string pt)
        {
            TMP_Text h = Fixed(NewText(sideContent, "", 16, dim), "<b>" + en + "</b>", "<b>" + pt + "</b>");
            Top(h.rectTransform, y, 22f);
            Image rule = NewImage(sideContent, "Rule", new Color(dim.r, dim.g, dim.b, 0.8f));
            rule.raycastTarget = false;
            Top(rule.rectTransform, y + 24f, 1f);
            y += 34f;
            return h;
        }
        TMP_Text Line(float size, float height)
        {
            TMP_Text t = NewText(sideContent, "", size, phosphor);
            Top(t.rectTransform, y, height);
            y += height;
            return t;
        }

        Header("SUBJECT", "HELENA");
        sideSubject = Line(20, 28f);
        sideSubjectState = Line(16, 26f);
        y += 14f;
        Header("CAMERA ON SCREEN", "CÂMERA NA TELA");
        sideCamera = Line(20, 28f);
        sideCameraRoom = Line(16, 26f);
        y += 14f;
        // the places of big halls (their spots): what to say to send her
        bool anySpot = false;
        foreach (Room r in rooms) anySpot |= r != null && r.Spot != null;
        if (anySpot)
        {
            Header("PLACES  (\"GO TO ...\")", "LUGARES  (\"VAI PARA ...\")");
            foreach (Room r in rooms)
            {
                if (r == null || r.Spot == null) continue;
                TMP_Text row = NewText(sideContent, "", 15, phosphor);
                Top(row.rectTransform, y, 24f);
                row.margin = new Vector4(6f, 0f, 0f, 0f);
                placeRows.Add((r, row));
                y += 24f;
            }
            y += 14f;
        }
        bool anyDoor = false;
        foreach (Door d in doors) anyDoor |= d != null;
        if (anyDoor) Header("DOORS", "PORTAS");
        foreach (Door d in doors)
        {
            if (d == null) continue;
            Door door = d;
            TMP_Text row;
            if (door.IsRemoteControlled)
            {
                NewButton(sideContent, "", 15, () => door.Toggle(), out Image img);
                Top(img.rectTransform, y, 44f);
                row = img.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                row = NewText(sideContent, "", 15, phosphor);
                Top(row.rectTransform, y, 44f);
            }
            row.alignment = TextAlignmentOptions.Left;
            row.margin = new Vector4(6f, 0f, 6f, 0f);
            row.richText = true;
            doorRows.Add((door, row));
            y += 50f;   // two lines: the number + the rooms, then the state
        }
        y += 14f;
        Header("LEGEND", "LEGENDA");
        if (anyDoor)
        {
            LegendRow(ref y, doorOpen, "OPEN", "ABERTA");
            LegendRow(ref y, doorClosed, "CLOSED", "FECHADA");
            LegendRow(ref y, doorLocked, "LOCKED", "TRANCADA");
        }
        if (anySpot) LegendRow(ref y, spotColor, "PLACE (SAY ITS NAME)", "LUGAR (FALE O NOME)");
        LegendRow(ref y, activeCamera, "CAMERA ON SCREEN", "CÂMERA NA TELA");
        LegendRow(ref y, new Color(1f, 0.3f, 0.25f), "HELENA", "HELENA");
        sideHeight = y + 8f;
        sideContent.sizeDelta = new Vector2(0f, sideHeight);
        // the controls: fixed under the scrolled rows (with a dark band so the rows don't show through)
        Image band = NewImage(mapSide, "KeysBand", new Color(0.01f, 0.05f, 0.025f, 1f));
        band.raycastTarget = false;
        band.rectTransform.anchorMin = Vector2.zero;
        band.rectTransform.anchorMax = new Vector2(1f, 0f);
        band.rectTransform.pivot = new Vector2(0.5f, 0f);
        band.rectTransform.anchoredPosition = Vector2.zero;
        band.rectTransform.sizeDelta = new Vector2(0f, KeysHeight);
        Image bandRule = NewImage(band.rectTransform, "Rule", new Color(dim.r, dim.g, dim.b, 0.8f));
        bandRule.raycastTarget = false;
        Top(bandRule.rectTransform, 0f, 1f);
        TMP_Text keys = Fixed(NewText(mapSide, "", 13, dim),
                              "<b>WHEEL</b> ON MAP - ZOOM   <b>RIGHT DRAG</b> - MOVE\n<b>WHEEL</b> HERE - SCROLL\n<b>CLICK</b> CAMERA - SWITCH\n<b>CLICK</b> REMOTE DOOR [R] - OPEN / CLOSE   <b>TAB</b> - CLOSE",
                              "<b>RODA</b> NO MAPA - ZOOM   <b>BOTÃO DIREITO</b> - MOVER\n<b>RODA</b> AQUI - ROLAR\n<b>CLIQUE</b> CÂMERA - TROCAR\n<b>CLIQUE</b> PORTA REMOTA [R] - ABRIR / FECHAR   <b>TAB</b> - FECHAR");
        keys.alignment = TextAlignmentOptions.BottomLeft;
        keys.rectTransform.anchorMin = new Vector2(0f, 0f);
        keys.rectTransform.anchorMax = new Vector2(1f, 0f);
        keys.rectTransform.pivot = new Vector2(0.5f, 0f);
        keys.rectTransform.anchoredPosition = Vector2.zero;
        keys.rectTransform.sizeDelta = new Vector2(0f, KeysHeight - 6f);
    }

    private const float KeysHeight = 84f;

    private void LegendRow(ref float y, Color color, string en, string pt)
    {
        Image sw = NewImage(sideContent, "Swatch", color);
        sw.raycastTarget = false;
        sw.rectTransform.anchorMin = sw.rectTransform.anchorMax = new Vector2(0f, 1f);
        sw.rectTransform.pivot = new Vector2(0f, 1f);
        sw.rectTransform.sizeDelta = new Vector2(16f, 16f);
        sw.rectTransform.anchoredPosition = new Vector2(2f, -y - 4f);
        TMP_Text t = Fixed(NewText(sideContent, "", 15, phosphor), en, pt);
        t.alignment = TextAlignmentOptions.Left;
        Top(t.rectTransform, y, 24f);
        t.margin = new Vector4(26f, 0f, 0f, 0f);
        y += 26f;
    }

    // the rooms on both sides of a door
    private string DoorRooms(Door door)
    {
        string a = null, b = null;
        foreach (Room r in rooms)
        {
            if (r == null || !r.Contains(door.transform.position, 0.6f)) continue;
            if (a == null) a = NameOf(r); else if (b == null) b = NameOf(r);
        }
        return a == null ? door.DisplayName.ToUpperInvariant() : b == null ? a : a + " / " + b;
    }

    private Vector2 MapPoint(Vector3 world)
    {
        Vector2 c = worldBounds.center;
        return new Vector2((world.x - c.x) * mapScale, (world.z - c.y) * mapScale);
    }

    // from a point on the wall toward the middle of its room (camera tags sit inside the room)
    private Vector2 InwardOffset(Vector3 world, float pixels)
    {
        Room r = RoomOf(world);
        Vector2 to = r != null ? MapPoint(r.transform.position) - MapPoint(world) : -MapPoint(world);
        return to.sqrMagnitude > 0.01f ? to.normalized * pixels : Vector2.zero;
    }

    private void OnDoorChanged(Door door)
    {
        Color c = DoorColor(door);
        foreach (var (d, image) in doorIcons) if (d == door) image.color = c;
        foreach (var (d, edges) in doorBadges)
            if (d == door) foreach (Image e in edges) e.color = c;
        RefreshDoorRows();
    }

    private Color DoorColor(Door door) => door.IsLocked ? doorLocked : door.IsOpen ? doorOpen : doorClosed;

    private void RefreshDoorRows()
    {
        foreach (var (door, row) in doorRows)
        {
            string between = DoorRooms(door);
            string state = door.IsLocked ? L("LOCKED", "TRANCADA") : door.IsOpen ? L("OPEN", "ABERTA") : L("CLOSED", "FECHADA");
            string hex = ColorUtility.ToHtmlStringRGB(DoorColor(door));
            row.text = $"<b>{door.Number:00}</b>  {between}\n      <color=#{hex}>{state}</color>{(door.IsRemoteControlled ? "  <size=80%>[R]</size>" : "")}";
        }
    }

    private void OnMapToggled(bool open)
    {
        // cameras that only exist sometimes (blackjack table) are on the map only while active
        foreach (var (cam, wedge, dot) in cameraViews)
        {
            bool on = cam != null && cam.gameObject.activeInHierarchy;
            wedge.gameObject.SetActive(on);
            dot.gameObject.SetActive(on);
        }
        IReadOnlyList<SecurityCamera> cams = system.Cameras;
        for (int i = 0; i < cameraButtons.Count && i < cams.Count; i++)
            cameraButtons[i].gameObject.SetActive(cams[i] != null && cams[i].gameObject.activeInHierarchy);
        if (open) { RefreshDoorRows(); sideTimer = 0f; UpdateMap(); }
        SetPanel(mapPanel, open);
    }

    // while the map is open: her blip, rooms lit, camera views, the side panel
    private void UpdateMap()
    {
        if (mapArea == null) return;
        SecurityCamera active = system.Active;
        Room here = partner != null ? RoomOf(partner.position) : null;
        float pulse = Mathf.Repeat(Time.unscaledTime * 0.8f, 1f);

        foreach (var (room, fill, border) in roomViews)
        {
            bool hers = room == here;
            bool watched = active != null && active.Room == room;
            fill.color = new Color(phosphor.r, phosphor.g, phosphor.b, hers ? RoomAlpha + 0.08f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f) : RoomAlpha);
            Color edge = watched ? activeCamera : new Color(phosphor.r, phosphor.g, phosphor.b, 0.75f);
            foreach (Image e in border) e.color = edge;
        }

        foreach (var (cam, wedge, dot) in cameraViews)
        {
            if (cam == null) continue;
            bool on = cam == active;
            Vector2 at = MapPoint(cam.transform.position);   // the game camera moves to the game being played
            wedge.rectTransform.anchoredPosition = at;
            dot.rectTransform.anchoredPosition = at;
            Vector3 fwd = cam.transform.rotation * Quaternion.AngleAxis(cam.Yaw, Vector3.up) * Vector3.forward;
            wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            Color c = on ? activeCamera : phosphor;
            wedge.color = new Color(c.r, c.g, c.b, on ? 0.32f : 0.1f);
            dot.color = c;
        }

        if (partnerBlip != null && partner != null)
        {
            Vector2 p = MapPoint(partner.position);
            partnerBlip.rectTransform.anchoredPosition = p;
            partnerRing.rectTransform.anchoredPosition = p;
            float s = Mathf.Lerp(12f, 34f, pulse);
            partnerRing.rectTransform.sizeDelta = new Vector2(s, s);
            partnerRing.color = new Color(1f, 0.25f, 0.2f, 0.7f * (1f - pulse));
            Vector3 f = partner.forward;
            partnerHeading.rectTransform.anchoredPosition = p;
            partnerHeading.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            partnerName.rectTransform.anchoredPosition = p + new Vector2(0f, -15f);
        }

        sideTimer -= Time.unscaledDeltaTime;
        if (sideSubject != null && sideTimer <= 0f)
        {
            sideTimer = 0.2f;
            bool inView = active != null && here != null && active.Room == here;
            sideSubject.text = "HELENA  -  " + (here != null ? NameOf(here) : L("UNKNOWN", "DESCONHECIDO"));
            sideSubjectState.text = inView ? "<color=#" + ColorUtility.ToHtmlStringRGB(doorOpen) + ">" + L("ON CAMERA", "NA CÂMERA") + "</color>"
                                           : "<color=#" + ColorUtility.ToHtmlStringRGB(doorClosed) + ">" + L("NOT ON THIS CAMERA", "FORA DESTA CÂMERA") + "</color>";
            sideCamera.text = active != null ? "<color=#" + ColorUtility.ToHtmlStringRGB(activeCamera) + ">" + active.Label + "</color>" : "-";
            sideCameraRoom.text = active != null ? NameOf(active.Room) : "";
        }
    }

    // ------------------------------------------------------------------ map shapes
    // four thin edges inside a rect (a crisp border; Outline draws a blurry offset copy)
    private Image[] Frame(RectTransform parent, float thickness, Color color)
    {
        var edges = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image e = NewImage(parent, "Edge", color);
            e.raycastTarget = false;
            RectTransform rt = e.rectTransform;
            bool vertical = i >= 2;
            float side = i % 2 == 0 ? 0f : 1f;
            rt.anchorMin = vertical ? new Vector2(side, 0f) : new Vector2(0f, side);
            rt.anchorMax = vertical ? new Vector2(side, 1f) : new Vector2(1f, side);
            rt.pivot = vertical ? new Vector2(side, 0.5f) : new Vector2(0.5f, side);
            rt.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
            rt.anchoredPosition = Vector2.zero;
            edges[i] = e;
        }
        return edges;
    }

    // a rect under the previous one in the side panel
    private static void Top(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(0f, height);
        if (rt.TryGetComponent(out TMP_Text t)) t.alignment = TextAlignmentOptions.Left;
    }

    private static Sprite circleSprite, ringSprite, wedgeSprite;
    private static Sprite Circle => circleSprite != null ? circleSprite : circleSprite = MakeSprite(64, (x, y) => Disc(x, y, 0.5f));
    private static Sprite Ring => ringSprite != null ? ringSprite : ringSprite = MakeSprite(64, (x, y) => Mathf.Clamp01(Disc(x, y, 0.5f) - Disc(x, y, 0.4f)));
    // field of view: apex at the bottom middle, 70 deg wide, fading out with distance
    private static Sprite Wedge => wedgeSprite != null ? wedgeSprite : wedgeSprite = MakeSprite(64, (x, y) =>
    {
        float dx = x - 0.5f, dy = y;
        if (dy <= 0f) return 0f;
        float angle = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg;
        float inside = Mathf.Clamp01((35f - angle) / 2f);
        return inside * Mathf.Clamp01(1.05f - Mathf.Sqrt(dx * dx + dy * dy));
    });

    private static float Disc(float x, float y, float radius)
    {
        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
        return Mathf.Clamp01((radius - d) * 64f);
    }

    private static Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha((x + 0.5f) / size, (y + 0.5f) / size) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------ settings
    private void BuildSettings()
    {
        if (settingsRows == null) return;
        settingsValues = new TMP_Text[SettingsRowCount];
        AddRow(0, "PAN CONTROL", "CONTROLE DA CÂMERA", () => settings.SetPanControl(settings.Pan == CameraSettings.PanControl.Mouse ? CameraSettings.PanControl.Keyboard : CameraSettings.PanControl.Mouse), null,
               () => settings.Pan == CameraSettings.PanControl.Mouse ? L("MOUSE (SCREEN EDGES)", "MOUSE (BORDAS DA TELA)") : L("KEYBOARD (A / D)", "TECLADO (A / D)"));
        AddRow(1, "MOUSE PAN SPEED", "VELOCIDADE (MOUSE)", () => settings.SetMousePanSpeed(settings.MousePanSpeed + 10f), () => settings.SetMousePanSpeed(settings.MousePanSpeed - 10f),
               () => Mathf.RoundToInt(settings.MousePanSpeed).ToString());
        AddRow(2, "KEYBOARD PAN SPEED", "VELOCIDADE (TECLADO)", () => settings.SetKeyboardPanSpeed(settings.KeyboardPanSpeed + 10f), () => settings.SetKeyboardPanSpeed(settings.KeyboardPanSpeed - 10f),
               () => Mathf.RoundToInt(settings.KeyboardPanSpeed).ToString());
        AddRow(3, "CAMERA FILTER", "FILTRO DA CÂMERA", () => settings.SetFilterEnabled(!settings.FilterEnabled), null, () => settings.FilterEnabled ? L("ON", "LIGADO") : L("OFF", "DESLIGADO"));
        AddRow(4, "FILTER STRENGTH", "FORÇA DO FILTRO", () => settings.SetFilterStrength(settings.FilterStrength + 0.1f), () => settings.SetFilterStrength(settings.FilterStrength - 0.1f),
               () => Mathf.RoundToInt(settings.FilterStrength * 100f) + "%");
        AddRow(5, "LANGUAGE / IDIOMA", "IDIOMA / LANGUAGE", () => settings.SetLanguage(settings.Language == GameLanguage.English ? GameLanguage.Portuguese : GameLanguage.English), null,
               () => settings.Language == GameLanguage.English ? "ENGLISH" : "PORTUGUÊS");
        AddRow(6, "SUBTITLES", "LEGENDAS", () => settings.SetSubtitles(!settings.SubtitlesEnabled), null, () => settings.SubtitlesEnabled ? L("ON", "LIGADAS") : L("OFF", "DESLIGADAS"));
        Button close = NewButton(settingsRows, "CLOSE", 18, () => system.SetSettingsOpen(false), out Image closeImg);
        closeLabel = close.GetComponentInChildren<TMP_Text>();
        closeImg.rectTransform.anchoredPosition = new Vector2(0f, -SettingsRowCount * 52f - 20f);
        closeImg.rectTransform.sizeDelta = new Vector2(160f, 36f);
    }

    // a row: label on the left, value in the middle, [-] [+] (or one button that cycles)
    private void AddRow(int index, string label, string labelPt, Action plus, Action minus, Func<string> value)
    {
        float y = -index * 52f;
        TMP_Text l = NewText(settingsRows, label, 18, phosphor);
        settingsLabels[index] = l;
        settingsLabelsEn[index] = label;
        settingsLabelsPt[index] = labelPt;
        l.alignment = TextAlignmentOptions.Left;
        l.rectTransform.sizeDelta = new Vector2(260f, 40f);
        l.rectTransform.anchoredPosition = new Vector2(-230f, y);

        Button main = NewButton(settingsRows, "", 18, minus == null ? plus : null, out Image mainImg);
        mainImg.rectTransform.sizeDelta = new Vector2(260f, 36f);
        mainImg.rectTransform.anchoredPosition = new Vector2(70f, y);
        if (minus != null) main.transition = Selectable.Transition.None;   // just shows the value
        settingsValues[index] = main.GetComponentInChildren<TMP_Text>();

        if (minus != null)
        {
            NewButton(settingsRows, "-", 20, minus, out Image m);
            m.rectTransform.sizeDelta = new Vector2(36f, 36f);
            m.rectTransform.anchoredPosition = new Vector2(230f, y);
            NewButton(settingsRows, "+", 20, plus, out Image p);
            p.rectTransform.sizeDelta = new Vector2(36f, 36f);
            p.rectTransform.anchoredPosition = new Vector2(272f, y);
        }
        settingsRefresh[index] = () => settingsValues[index].text = value();
    }

    private void RefreshSettings()
    {
        foreach (Action a in settingsRefresh) a?.Invoke();
    }

    private void OnSettingsToggled(bool open) => SetPanel(settingsPanel, open);

    // ------------------------------------------------------------------ helpers
    private static void SetPanel(CanvasGroup panel, bool open)
    {
        if (panel == null) return;
        panel.alpha = open ? 1f : 0f;
        panel.interactable = open;
        panel.blocksRaycasts = open;
    }

    private Image NewImage(RectTransform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private TMP_Text NewText(RectTransform parent, string text, float size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private Button NewButton(RectTransform parent, string text, float size, Action onClick, out Image image)
    {
        image = NewImage(parent, "Button_" + text, new Color(phosphor.r, phosphor.g, phosphor.b, 0.15f));
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = phosphor;
        outline.effectDistance = new Vector2(1f, -1f);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        TMP_Text t = NewText(image.rectTransform, text, size, phosphor);
        Stretch(t.rectTransform, 0f);
        return button;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}
