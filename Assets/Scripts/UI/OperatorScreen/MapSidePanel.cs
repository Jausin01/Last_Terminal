using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The column next to the large map: where she is, the camera on screen, the places of big halls (what to say to send
// her), every door (number, rooms, state; the remote ones are buttons), a legend and the controls. The rows scroll with
// the mouse wheel. Built once from the scene data; refreshed with the language and the door states.
internal sealed class MapSidePanel
{
    // The side rows by kind (refreshed with the language / door states).
    private readonly List<(Door door, TMP_Text row)> doorRows = new List<(Door, TMP_Text)>();
    private readonly List<(Room room, TMP_Text text)> placeRows = new List<(Room, TMP_Text)>();
    private readonly List<(TMP_Text text, string en, string pt)> fixedTexts = new List<(TMP_Text, string, string)>();
    // The subject / camera lines and their refresh timer.
    private TMP_Text sideSubject, sideSubjectState, sideCamera, sideCameraRoom;
    private float sideTimer;
    // The scrolled rows inside their viewport, their height and the scroll offset.
    private RectTransform sideContent;
    private RectTransform sideViewport;
    private float sideHeight;
    private float sideScroll;
    // Height of the key hints at the bottom of the panel (px).
    private const float KeysHeight = 84f;

    // The hud: its widgets, colors and language.
    private readonly SecurityCameraHud hud;

    public MapSidePanel(SecurityCameraHud hud) { this.hud = hud; }

    // Builds the panel (where she is, camera, places, doors, legend, controls).
    internal void Build()
    {
        if (hud.MapSide == null) return;
        // the rows scroll (mouse wheel) inside a viewport; the controls stay at the bottom
        sideViewport = (RectTransform)new GameObject("SideViewport", typeof(RectTransform)).transform;
        sideViewport.gameObject.layer = hud.MapSide.gameObject.layer;
        sideViewport.SetParent(hud.MapSide, false);
        HudUi.Stretch(sideViewport, 0f);
        sideViewport.offsetMin = new Vector2(0f, KeysHeight + 8f);
        sideViewport.gameObject.AddComponent<RectMask2D>();
        sideContent = (RectTransform)new GameObject("SideContent", typeof(RectTransform)).transform;
        sideContent.gameObject.layer = hud.MapSide.gameObject.layer;
        sideContent.SetParent(sideViewport, false);
        sideContent.anchorMin = new Vector2(0f, 1f);
        sideContent.anchorMax = new Vector2(1f, 1f);
        sideContent.pivot = new Vector2(0.5f, 1f);
        sideContent.anchoredPosition = Vector2.zero;
        float y = 0f;
        TMP_Text Header(string en, string pt)
        {
            TMP_Text h = Fixed(HudUi.NewText(sideContent, "", 16, hud.Dim), "<b>" + en + "</b>", "<b>" + pt + "</b>");
            HudUi.Top(h.rectTransform, y, 22f);
            Image rule = HudUi.NewImage(sideContent, "Rule", new Color(hud.Dim.r, hud.Dim.g, hud.Dim.b, 0.8f));
            rule.raycastTarget = false;
            HudUi.Top(rule.rectTransform, y + 24f, 1f);
            y += 34f;
            return h;
        }
        TMP_Text Line(float size, float height)
        {
            TMP_Text t = HudUi.NewText(sideContent, "", size, hud.Phosphor);
            HudUi.Top(t.rectTransform, y, height);
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
        foreach (Room r in hud.Rooms) anySpot |= r != null && r.Spot != null;
        if (anySpot)
        {
            Header("PLACES  (\"GO TO ...\")", "LUGARES  (\"VAI PARA ...\")");
            foreach (Room r in hud.Rooms)
            {
                if (r == null || r.Spot == null) continue;
                TMP_Text row = HudUi.NewText(sideContent, "", 15, hud.Phosphor);
                HudUi.Top(row.rectTransform, y, 24f);
                row.margin = new Vector4(6f, 0f, 0f, 0f);
                placeRows.Add((r, row));
                y += 24f;
            }
            y += 14f;
        }
        bool anyDoor = false;
        foreach (Door d in hud.Doors) anyDoor |= d != null;
        if (anyDoor) Header("DOORS", "PORTAS");
        foreach (Door d in hud.Doors)
        {
            if (d == null) continue;
            Door door = d;
            TMP_Text row;
            if (door.IsRemoteControlled)
            {
                HudUi.NewButton(sideContent, "", 15, () => door.Toggle(), out Image img, hud.Phosphor);
                HudUi.Top(img.rectTransform, y, 44f);
                row = img.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                row = HudUi.NewText(sideContent, "", 15, hud.Phosphor);
                HudUi.Top(row.rectTransform, y, 44f);
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
            LegendRow(ref y, hud.DoorOpenColor, "OPEN", "ABERTA");
            LegendRow(ref y, hud.DoorClosedColor, "CLOSED", "FECHADA");
            LegendRow(ref y, hud.DoorLockedColor, "LOCKED", "TRANCADA");
        }
        if (anySpot) LegendRow(ref y, hud.SpotColor, "PLACE (SAY ITS NAME)", "LUGAR (FALE O NOME)");
        LegendRow(ref y, hud.ActiveCameraColor, "CAMERA ON SCREEN", "CÂMERA NA TELA");
        LegendRow(ref y, new Color(1f, 0.3f, 0.25f), "HELENA", "HELENA");
        sideHeight = y + 8f;
        sideContent.sizeDelta = new Vector2(0f, sideHeight);
        // the controls: fixed under the scrolled rows (with a dark band so the rows don't show through)
        Image band = HudUi.NewImage(hud.MapSide, "KeysBand", new Color(0.01f, 0.05f, 0.025f, 1f));
        band.raycastTarget = false;
        band.rectTransform.anchorMin = Vector2.zero;
        band.rectTransform.anchorMax = new Vector2(1f, 0f);
        band.rectTransform.pivot = new Vector2(0.5f, 0f);
        band.rectTransform.anchoredPosition = Vector2.zero;
        band.rectTransform.sizeDelta = new Vector2(0f, KeysHeight);
        Image bandRule = HudUi.NewImage(band.rectTransform, "Rule", new Color(hud.Dim.r, hud.Dim.g, hud.Dim.b, 0.8f));
        bandRule.raycastTarget = false;
        HudUi.Top(bandRule.rectTransform, 0f, 1f);
        TMP_Text keys = Fixed(HudUi.NewText(hud.MapSide, "", 13, hud.Dim),
                              "<b>WHEEL</b> ON MAP - ZOOM   <b>RIGHT DRAG</b> - MOVE\n<b>WHEEL</b> HERE - SCROLL\n<b>CLICK</b> CAMERA - SWITCH\n<b>CLICK</b> REMOTE DOOR [R] - OPEN / CLOSE   <b>TAB</b> - CLOSE",
                              "<b>RODA</b> NO MAPA - ZOOM   <b>BOTÃO DIREITO</b> - MOVER\n<b>RODA</b> AQUI - ROLAR\n<b>CLIQUE</b> CÂMERA - TROCAR\n<b>CLIQUE</b> PORTA REMOTA [R] - ABRIR / FECHAR   <b>TAB</b> - FECHAR");
        keys.alignment = TextAlignmentOptions.BottomLeft;
        keys.rectTransform.anchorMin = new Vector2(0f, 0f);
        keys.rectTransform.anchorMax = new Vector2(1f, 0f);
        keys.rectTransform.pivot = new Vector2(0.5f, 0f);
        keys.rectTransform.anchoredPosition = Vector2.zero;
        keys.rectTransform.sizeDelta = new Vector2(0f, KeysHeight - 6f);
    }

    // While the map is open: the subject / camera lines, a few times per second.
    internal void Tick(SecurityCamera active, Room here)
    {
        sideTimer -= Time.unscaledDeltaTime;
        if (sideSubject != null && sideTimer <= 0f)
        {
            sideTimer = 0.2f;
            bool inView = active != null && here != null && active.Room == here;
            sideSubject.text = "HELENA  -  " + (here != null ? hud.NameOf(here) : hud.L("UNKNOWN", "DESCONHECIDO"));
            sideSubjectState.text = inView ? "<color=#" + ColorUtility.ToHtmlStringRGB(hud.DoorOpenColor) + ">" + hud.L("ON CAMERA", "NA CÂMERA") + "</color>"
                                           : "<color=#" + ColorUtility.ToHtmlStringRGB(hud.DoorClosedColor) + ">" + hud.L("NOT ON THIS CAMERA", "FORA DESTA CÂMERA") + "</color>";
            sideCamera.text = active != null ? "<color=#" + ColorUtility.ToHtmlStringRGB(hud.ActiveCameraColor) + ">" + active.Label + "</color>" : "-";
            sideCameraRoom.text = active != null ? hud.NameOf(active.Room) : "";
        }
    }

    // The next Tick rewrites the subject / camera lines at once.
    internal void ForceRefresh() => sideTimer = 0f;

    // Mouse wheel over the panel (large map only): scrolls the rows.
    internal void Scroll(int notch, Vector2 screen, Camera eventCamera)
    {
        if (sideViewport == null || notch == 0 || !RectTransformUtility.RectangleContainsScreenPoint(hud.MapSide, screen, eventCamera)) return;
        sideScroll = Mathf.Clamp(sideScroll - notch * hud.SideScrollStep, 0f, Mathf.Max(0f, sideHeight - sideViewport.rect.height));
        sideContent.anchoredPosition = new Vector2(0f, sideScroll);
    }

    // Texts that depend on the language: fixed labels, the place rows, the door rows; the lines on the next Tick.
    internal void RefreshLanguage()
    {
        foreach (var (t, en, pt) in fixedTexts) if (t != null) t.text = hud.L(en, pt);
        foreach (var (room, t) in placeRows) t.text = "<color=#" + ColorUtility.ToHtmlStringRGB(hud.SpotColor) + "><b>></b></color>  " + hud.NameOf(room);
        RefreshDoorRows();
        sideTimer = 0f;
    }

    // Refreshes the door list rows.
    internal void RefreshDoorRows()
    {
        foreach (var (door, row) in doorRows)
        {
            string between = DoorRooms(door);
            string state = door.IsLocked ? hud.L("LOCKED", "TRANCADA") : door.IsOpen ? hud.L("OPEN", "ABERTA") : hud.L("CLOSED", "FECHADA");
            string hex = ColorUtility.ToHtmlStringRGB(hud.DoorColor(door));
            row.text = $"<b>{door.Number:00}</b>  {between}\n      <color=#{hex}>{state}</color>{(door.IsRemoteControlled ? "  <size=80%>[R]</size>" : "")}";
        }
    }

    // One legend row: a color swatch and its text.
    private void LegendRow(ref float y, Color color, string en, string pt)
    {
        Image sw = HudUi.NewImage(sideContent, "Swatch", color);
        sw.raycastTarget = false;
        sw.rectTransform.anchorMin = sw.rectTransform.anchorMax = new Vector2(0f, 1f);
        sw.rectTransform.pivot = new Vector2(0f, 1f);
        sw.rectTransform.sizeDelta = new Vector2(16f, 16f);
        sw.rectTransform.anchoredPosition = new Vector2(2f, -y - 4f);
        TMP_Text t = Fixed(HudUi.NewText(sideContent, "", 15, hud.Phosphor), en, pt);
        t.alignment = TextAlignmentOptions.Left;
        HudUi.Top(t.rectTransform, y, 24f);
        t.margin = new Vector4(26f, 0f, 0f, 0f);
        y += 26f;
    }

    // the rooms on both sides of a door
    private string DoorRooms(Door door)
    {
        string a = null, b = null;
        foreach (Room r in hud.Rooms)
        {
            if (r == null || !r.Contains(door.transform.position, 0.6f)) continue;
            if (a == null) a = hud.NameOf(r); else if (b == null) b = hud.NameOf(r);
        }
        return a == null ? door.DisplayName.ToUpperInvariant() : b == null ? a : a + " / " + b;
    }

    // a text that never changes except with the language
    private TMP_Text Fixed(TMP_Text t, string en, string pt)
    {
        fixedTexts.Add((t, en, pt));
        t.text = hud.L(en, pt);
        return t;
    }
}
