using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The facility map of the operator's screen: a blueprint drawn from the scene's Rooms, Doors and cameras (grid, rooms,
// numbered doors - the remote ones clickable -, camera views, her blip). One drawing that moves between two windows:
// the corner map (first Tab: follows her until dragged) and the large map (second Tab, with the side column,
// MapSidePanel). Mouse: wheel = zoom toward the cursor, right / middle drag = move. Built once by SecurityCameraHud.
internal sealed class FacilityMapView
{
    // Fill opacity of rooms; generated map objects (camera buttons, doors, rooms, names, camera views) kept to update them.
    private const float RoomAlpha = 0.07f;
    private readonly List<Image> cameraButtons = new List<Image>();
    private readonly List<(Door door, Image image)> doorIcons = new List<(Door, Image)>();
    private readonly List<(Door door, Image[] edges)> doorBadges = new List<(Door, Image[])>();
    private readonly List<(Room room, Image fill, Image[] border)> roomViews = new List<(Room, Image, Image[])>();
    private readonly List<(Room room, TMP_Text text)> roomNames = new List<(Room, TMP_Text)>();
    private readonly List<(SecurityCamera cam, Image wedge, Image dot)> cameraViews = new List<(SecurityCamera, Image, Image)>();
    // Her blip on the map and her name, the world bounds of the map and its scale.
    private Image partnerBlip, partnerRing, partnerHeading;
    private TMP_Text partnerName;
    private Rect worldBounds;
    private float mapScale;
    private RectTransform mapContent;          // everything drawn on the map: scaled / moved by the zoom
    // Current zoom of the map shown.
    private float mapZoom = 1f;
    // each window keeps its own zoom and position; the corner map follows her until dragged
    private SecurityCameraSystem.MapView shownView;
    private float largeZoom = 1f, miniZoom = -1f;
    private Vector2 largePos, miniPos;
    private bool follow = true;
    private bool followShown;
    private Vector2 lastMouse;
    private Canvas canvas;

    // The hud (widgets, colors, language, scene data) and the side column of the large map.
    private readonly SecurityCameraHud hud;
    private readonly MapSidePanel side;

    public FacilityMapView(SecurityCameraHud hud)
    {
        this.hud = hud;
        side = new MapSidePanel(hud);
    }

    // Each frame while the map is open: the drawing, the mouse, the corner window following her.
    internal void Tick()
    {
        UpdateMap();
        MapInput();
        if (shownView == SecurityCameraSystem.MapView.Corner) UpdateCorner();
    }

    // The "follow Helena" button of the corner map.
    internal void FollowAgain()
    {
        follow = true;
        RefreshFollow();
    }

    // Highlights the camera on screen among the camera tags.
    internal void ShowActiveCamera(int activeIndex)
    {
        for (int i = 0; i < cameraButtons.Count; i++)
        {
            bool on = i == activeIndex;
            Color c = on ? hud.ActiveCameraColor : hud.Phosphor;
            cameraButtons[i].color = new Color(c.r, c.g, c.b, on ? 0.45f : 0.15f);
            cameraButtons[i].GetComponentInChildren<TMP_Text>().color = on ? Color.white : hud.Phosphor;
        }
    }

    // Colors every door as it is now (the first frame).
    internal void RefreshAllDoors()
    {
        foreach (var (door, _) in doorIcons) OnDoorChanged(door);
    }

    // Texts that depend on the language: room names, the window titles, the follow button, the side column.
    internal void RefreshLanguage()
    {
        foreach (var (room, t) in roomNames) t.text = "<b>" + hud.NameOf(room) + "</b>";
        if (hud.MiniTitle != null) hud.MiniTitle.text = "<mspace=0.55em>" + hud.L("MAP  //  TAB: LARGE", "MAPA  //  TAB: AMPLIAR") + "</mspace>";
        followShown = !follow;
        RefreshFollow();
        string place = hud.L(hud.PlaceName, string.IsNullOrEmpty(hud.PlaceNamePt) ? hud.PlaceName : hud.PlaceNamePt).ToUpperInvariant();
        if (hud.MapTitle != null) hud.MapTitle.text = "<mspace=0.6em>" + hud.L(place + " MAP  //  SECURITY NETWORK", "MAPA: " + place + "  //  REDE DE SEGURANÇA") + "</mspace>";
        side.RefreshLanguage();
    }

    // the corner map: the cursor on it is not for the world (no pan / zoom / clicks there); it follows her
    private void UpdateCorner()
    {
        Mouse mouse = Mouse.current;
        if (hud.MiniPanel != null && mouse != null)
        {
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            hud.CameraSystem.SetPointerOverPanel(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)hud.MiniPanel.transform, mouse.position.ReadValue(), eventCamera));
        }
        if (follow && hud.Partner != null && mapContent != null)
        {
            Vector2 want = -MapPoint(hud.Partner.position) * mapZoom;
            float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            SetMapView(Vector2.Lerp(mapContent.anchoredPosition, want, k));
        }
        RefreshFollow();
    }

    // Shows / hides the "follow" button of the corner map.
    private void RefreshFollow()
    {
        if (hud.MiniFollowLabel == null || followShown == follow) return;
        followShown = follow;
        hud.MiniFollowLabel.text = follow ? hud.L("FOLLOWING HELENA", "SEGUINDO HELENA") : hud.L("> FOLLOW HELENA", "> SEGUIR HELENA");
        hud.MiniFollowLabel.color = follow ? hud.Dim : hud.SpotColor;
    }

    // The window the map is drawn in now.
    private RectTransform Viewport => shownView == SecurityCameraSystem.MapView.Corner ? hud.MiniArea : hud.MapArea;

    // the corner map can zoom out until the whole drawing fits in it; the large one starts there
    private float MinZoom
    {
        get
        {
            RectTransform v = Viewport;
            if (v == null || mapContent == null || shownView != SecurityCameraSystem.MapView.Corner) return 1f;
            Vector2 c = mapContent.sizeDelta;
            return Mathf.Min(1f, Mathf.Min(v.rect.width / Mathf.Max(c.x, 1f), v.rect.height / Mathf.Max(c.y, 1f)));
        }
    }

    // Mouse over the map: wheel = zoom toward the cursor, right / middle drag = move (stops following her); over the side column: scroll.
    private void MapInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (canvas == null) canvas = hud.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screen = mouse.position.ReadValue();
        float wheel = mouse.scroll.ReadValue().y;
        int notch = Mathf.Abs(wheel) > 0.01f ? (int)Mathf.Sign(wheel) : 0;

        RectTransform view = Viewport;
        if (view != null && mapContent != null && RectTransformUtility.RectangleContainsScreenPoint(view, screen, eventCamera))
        {
            if (notch != 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, eventCamera, out Vector2 local))
            {
                // the point under the cursor stays under it
                float next = Mathf.Clamp(mapZoom * Mathf.Pow(hud.MapZoomStep, notch), MinZoom, hud.MaxMapZoom);
                Vector2 onContent = (local - mapContent.anchoredPosition) / mapZoom;
                mapZoom = next;
                SetMapView(local - onContent * mapZoom);
            }
            bool drag = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (drag && (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)) lastMouse = screen;
            else if (drag)
            {
                float toLocal = canvas != null ? 1f / canvas.scaleFactor : 1f;
                if (screen != lastMouse) follow = false;   // looking around: it stops following her
                SetMapView(mapContent.anchoredPosition + (screen - lastMouse) * toLocal);
                lastMouse = screen;
            }
        }
        else if (shownView == SecurityCameraSystem.MapView.Large) side.Scroll(notch, screen, eventCamera);
    }

    // the zoomed map never leaves its window (the large one: at zoom 1 it fits it exactly; smaller than the window =
    // centered)
    private void SetMapView(Vector2 position)
    {
        mapContent.localScale = new Vector3(mapZoom, mapZoom, 1f);
        RectTransform view = Viewport;
        Vector2 limit = Vector2.Max(Vector2.zero, (mapContent.sizeDelta * mapZoom - view.rect.size) * 0.5f);
        mapContent.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
    }

    // Blueprint of the facility: grid, rooms (hers lit up, the watched one framed in amber), doors with their number
    // badge (color = state, click = operate the remote ones), cameras with their field of view, her blip and heading.
    // Side panel: where she is, the camera on screen, every door (number, rooms, state), legend, controls.
    internal void BuildMap()
    {
        if (hud.MapArea == null || hud.Rooms.Length == 0) return;
        bool first = true;
        foreach (Room r in hud.Rooms)
        {
            if (r == null) continue;
            if (first) { worldBounds = r.FloorRect; first = false; }
            else worldBounds = Rect.MinMaxRect(Mathf.Min(worldBounds.xMin, r.FloorRect.xMin), Mathf.Min(worldBounds.yMin, r.FloorRect.yMin),
                                               Mathf.Max(worldBounds.xMax, r.FloorRect.xMax), Mathf.Max(worldBounds.yMax, r.FloorRect.yMax));
        }
        Vector2 area = hud.MapArea.rect.size * 0.92f;
        mapScale = Mathf.Min(area.x / Mathf.Max(worldBounds.width, 0.01f), area.y / Mathf.Max(worldBounds.height, 0.01f));
        mapContent = (RectTransform)new GameObject("MapContent", typeof(RectTransform)).transform;
        mapContent.gameObject.layer = hud.MapArea.gameObject.layer;
        mapContent.SetParent(hud.MapArea, false);
        mapContent.sizeDelta = hud.MapArea.rect.size;

        BuildGrid();

        // rooms: fill + crisp border + name
        foreach (Room r in hud.Rooms)
        {
            if (r == null) continue;
            Rect f = r.FloorRect;
            Vector2 size = f.size * mapScale;
            Image fill = HudUi.NewImage(mapContent, "Room_" + r.DisplayName, new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, RoomAlpha));
            fill.raycastTarget = false;
            fill.rectTransform.anchoredPosition = MapPoint(new Vector3(f.center.x, 0f, f.center.y));
            fill.rectTransform.sizeDelta = size;
            Image[] border = HudUi.Frame(fill.rectTransform, 2f, new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, 0.75f));
            roomViews.Add((r, fill, border));
            if (r.Spot != null) continue;   // named at its spot (below)
            TMP_Text t = HudUi.NewText(fill.rectTransform, "", 18f, hud.Phosphor);
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
        foreach (Room r in hud.Rooms)
        {
            if (r == null || r.Spot == null) continue;
            Vector2 at = MapPoint(r.Spot.position);
            Image mark = HudUi.NewImage(mapContent, "Spot_" + r.DisplayName, hud.SpotColor);
            mark.raycastTarget = false;
            mark.rectTransform.sizeDelta = new Vector2(11f, 11f);
            mark.rectTransform.anchoredPosition = at;
            mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image hole = HudUi.NewImage(mark.rectTransform, "Hole", new Color(0.02f, 0.06f, 0.035f, 1f));
            hole.raycastTarget = false;
            hole.rectTransform.sizeDelta = new Vector2(5f, 5f);
            TMP_Text t = HudUi.NewText(mapContent, "", 13, hud.SpotColor);
            t.rectTransform.sizeDelta = new Vector2(180f, 18f);
            t.rectTransform.anchoredPosition = at + new Vector2(0f, 14f);
            roomNames.Add((r, t));
        }

        // cameras: field of view wedge + a clickable tag
        IReadOnlyList<SecurityCamera> cams = hud.CameraSystem.Cameras;
        for (int i = 0; i < cams.Count; i++)
        {
            int index = i;
            SecurityCamera cam = cams[i];
            Image wedge = HudUi.NewImage(mapContent, "View_" + cam.Label, new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, 0.12f));
            wedge.sprite = HudUi.Wedge;
            wedge.raycastTarget = false;
            wedge.rectTransform.pivot = new Vector2(0.5f, 0f);   // apex = the camera
            wedge.rectTransform.sizeDelta = new Vector2(84f, 64f);
            wedge.rectTransform.anchoredPosition = MapPoint(cam.transform.position);
            Image dot = HudUi.NewImage(mapContent, "Cam_" + cam.Label, hud.Phosphor);
            dot.sprite = HudUi.Circle;
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(8f, 8f);
            dot.rectTransform.anchoredPosition = MapPoint(cam.transform.position);
            string number = cam.Label.Replace("CAM", "").Trim();
            HudUi.NewButton(mapContent, number, 12, () => hud.CameraSystem.Switch(index), out Image tag, hud.Phosphor);
            tag.rectTransform.sizeDelta = new Vector2(30f, 18f);
            tag.rectTransform.anchoredPosition = MapPoint(cam.transform.position) + InwardOffset(cam.transform.position, 24f);
            cameraButtons.Add(tag);
            cameraViews.Add((cam, wedge, dot));
        }

        // doors: a bar along the wall (state color) and the number badge on it
        foreach (Door d in hud.Doors)
        {
            if (d == null) continue;
            Door door = d;
            Vector3 along = door.transform.right;
            bool horizontal = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
            Image bar = HudUi.NewImage(mapContent, "Door_" + door.DisplayName, hud.DoorClosedColor);
            bar.raycastTarget = false;
            bar.rectTransform.sizeDelta = horizontal ? new Vector2(1.6f * mapScale, 6f) : new Vector2(6f, 1.6f * mapScale);
            bar.rectTransform.anchoredPosition = MapPoint(door.transform.position);

            Image badge;
            if (door.IsRemoteControlled)
            {
                Button b = HudUi.NewButton(mapContent, "", 14, () => door.Toggle(), out badge, hud.Phosphor);
                b.transition = Selectable.Transition.ColorTint;
            }
            else badge = HudUi.NewImage(mapContent, "Badge_" + door.DisplayName, Color.white);
            badge.color = new Color(0.02f, 0.06f, 0.035f, 0.95f);
            badge.rectTransform.sizeDelta = new Vector2(24f, 24f);
            badge.rectTransform.anchoredPosition = MapPoint(door.transform.position);
            Image[] edges = HudUi.Frame(badge.rectTransform, 2f, hud.DoorClosedColor);
            TMP_Text n = HudUi.NewText(badge.rectTransform, door.Number > 0 ? "<b>" + door.Number + "</b>" : "-", 15, Color.white);
            HudUi.Stretch(n.rectTransform, 0f);
            doorIcons.Add((door, bar));
            doorBadges.Add((door, edges));
        }

        // her: pulse ring, heading tick, dot, name
        partnerRing = HudUi.NewImage(mapContent, "PartnerRing", new Color(1f, 0.25f, 0.2f, 0.6f));
        partnerRing.sprite = HudUi.Ring;
        partnerRing.raycastTarget = false;
        partnerHeading = HudUi.NewImage(mapContent, "PartnerHeading", new Color(1f, 0.35f, 0.3f));
        partnerHeading.raycastTarget = false;
        partnerHeading.rectTransform.pivot = new Vector2(0.5f, 0f);
        partnerHeading.rectTransform.sizeDelta = new Vector2(3f, 15f);
        partnerBlip = HudUi.NewImage(mapContent, "PartnerBlip", new Color(1f, 0.25f, 0.2f));
        partnerBlip.sprite = HudUi.Circle;
        partnerBlip.raycastTarget = false;
        partnerBlip.rectTransform.sizeDelta = new Vector2(12f, 12f);
        partnerName = HudUi.NewText(mapContent, "<b>HELENA</b>", 11, new Color(1f, 0.45f, 0.4f));
        partnerName.rectTransform.sizeDelta = new Vector2(70f, 14f);

        side.Build();
    }

    // faint lines every meter, stronger every 4
    private void BuildGrid()
    {
        Color minor = new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, 0.045f);
        Color major = new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, 0.1f);
        Vector2 size = mapContent.sizeDelta;
        int x0 = Mathf.FloorToInt(worldBounds.xMin - 2f), x1 = Mathf.CeilToInt(worldBounds.xMax + 2f);
        int z0 = Mathf.FloorToInt(worldBounds.yMin - 2f), z1 = Mathf.CeilToInt(worldBounds.yMax + 2f);
        for (int x = x0; x <= x1; x++)
        {
            float px = MapPoint(new Vector3(x, 0f, 0f)).x;
            if (Mathf.Abs(px) > size.x * 0.5f) continue;
            Image l = HudUi.NewImage(mapContent, "GridX", x % 4 == 0 ? major : minor);
            l.raycastTarget = false;
            l.rectTransform.sizeDelta = new Vector2(1f, size.y);
            l.rectTransform.anchoredPosition = new Vector2(px, 0f);
        }
        for (int z = z0; z <= z1; z++)
        {
            float py = MapPoint(new Vector3(0f, 0f, z)).y;
            if (Mathf.Abs(py) > size.y * 0.5f) continue;
            Image l = HudUi.NewImage(mapContent, "GridZ", z % 4 == 0 ? major : minor);
            l.raycastTarget = false;
            l.rectTransform.sizeDelta = new Vector2(size.x, 1f);
            l.rectTransform.anchoredPosition = new Vector2(0f, py);
        }
    }

    // World position -> map position.
    private Vector2 MapPoint(Vector3 world)
    {
        Vector2 c = worldBounds.center;
        return new Vector2((world.x - c.x) * mapScale, (world.z - c.y) * mapScale);
    }

    // from a point on the wall toward the middle of its room (camera tags sit inside the room)
    private Vector2 InwardOffset(Vector3 world, float pixels)
    {
        Room r = hud.RoomOf(world);
        Vector2 to = r != null ? MapPoint(r.transform.position) - MapPoint(world) : -MapPoint(world);
        return to.sqrMagnitude > 0.01f ? to.normalized * pixels : Vector2.zero;
    }

    // A door changed: recolors its icon and row.
    internal void OnDoorChanged(Door door)
    {
        Color c = hud.DoorColor(door);
        foreach (var (d, image) in doorIcons) if (d == door) image.color = c;
        foreach (var (d, edges) in doorBadges)
            if (d == door) foreach (Image e in edges) e.color = c;
        side.RefreshDoorRows();
    }

    // Map closed / corner / large: moves the drawing to that window.
    internal void OnMapChanged(SecurityCameraSystem.MapView view)
    {
        bool open = view != SecurityCameraSystem.MapView.Closed;
        MoveMap(view);
        // cameras that only exist sometimes (blackjack table) are on the map only while active
        foreach (var (cam, wedge, dot) in cameraViews)
        {
            bool on = cam != null && cam.gameObject.activeInHierarchy;
            wedge.gameObject.SetActive(on);
            dot.gameObject.SetActive(on);
        }
        IReadOnlyList<SecurityCamera> cams = hud.CameraSystem.Cameras;
        for (int i = 0; i < cameraButtons.Count && i < cams.Count; i++)
            cameraButtons[i].gameObject.SetActive(cams[i] != null && cams[i].gameObject.activeInHierarchy);
        if (open) { side.RefreshDoorRows(); side.ForceRefresh(); UpdateMap(); }
        HudUi.SetPanel(hud.MapPanel, view == SecurityCameraSystem.MapView.Large);
        HudUi.SetPanel(hud.MiniPanel, view == SecurityCameraSystem.MapView.Corner);
    }

    // the drawing goes to the window of this view, with that window's zoom / position (the corner map opens on her)
    private void MoveMap(SecurityCameraSystem.MapView view)
    {
        if (mapContent == null) return;
        if (shownView == SecurityCameraSystem.MapView.Large) { largeZoom = mapZoom; largePos = mapContent.anchoredPosition; }
        else if (shownView == SecurityCameraSystem.MapView.Corner) { miniZoom = mapZoom; miniPos = mapContent.anchoredPosition; }
        if (view == SecurityCameraSystem.MapView.Closed) { shownView = view; return; }
        bool corner = view == SecurityCameraSystem.MapView.Corner && hud.MiniArea != null;
        shownView = corner ? SecurityCameraSystem.MapView.Corner : SecurityCameraSystem.MapView.Large;
        mapContent.SetParent(corner ? hud.MiniArea : hud.MapArea, false);
        if (corner)
        {
            if (miniZoom < 0f) miniZoom = hud.MiniStartZoom;
            follow = true;
            mapZoom = Mathf.Clamp(miniZoom, MinZoom, hud.MaxMapZoom);
            SetMapView(hud.Partner != null ? -MapPoint(hud.Partner.position) * mapZoom : miniPos);
        }
        else
        {
            mapZoom = largeZoom;
            SetMapView(largePos);
        }
    }

    // while the map is open: her blip, rooms lit, camera views, the side panel
    private void UpdateMap()
    {
        if (hud.MapArea == null) return;
        SecurityCamera active = hud.CameraSystem.Active;
        Room here = hud.Partner != null ? hud.RoomOf(hud.Partner.position) : null;
        float pulse = Mathf.Repeat(Time.unscaledTime * 0.8f, 1f);

        foreach (var (room, fill, border) in roomViews)
        {
            bool hers = room == here;
            bool watched = active != null && active.Room == room;
            fill.color = new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, hers ? RoomAlpha + 0.08f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f) : RoomAlpha);
            Color edge = watched ? hud.ActiveCameraColor : new Color(hud.Phosphor.r, hud.Phosphor.g, hud.Phosphor.b, 0.75f);
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
            Color c = on ? hud.ActiveCameraColor : hud.Phosphor;
            wedge.color = new Color(c.r, c.g, c.b, on ? 0.32f : 0.1f);
            dot.color = c;
        }

        if (partnerBlip != null && hud.Partner != null)
        {
            Vector2 p = MapPoint(hud.Partner.position);
            partnerBlip.rectTransform.anchoredPosition = p;
            partnerRing.rectTransform.anchoredPosition = p;
            float s = Mathf.Lerp(12f, 34f, pulse);
            partnerRing.rectTransform.sizeDelta = new Vector2(s, s);
            partnerRing.color = new Color(1f, 0.25f, 0.2f, 0.7f * (1f - pulse));
            Vector3 f = hud.Partner.forward;
            partnerHeading.rectTransform.anchoredPosition = p;
            partnerHeading.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            partnerName.rectTransform.anchoredPosition = p + new Vector2(0f, -15f);
        }

        side.Tick(active, here);
    }
}
