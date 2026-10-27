using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// The screen UI of a location scene: canvas + save screen + ammo HUD (BuildUi), the command line, the combat HUD and
// the game over screen. Green phosphor look; the scanline texture is generated once.
internal static class LocationUiBuilder
{
    // Generated UI textures (the scanlines).
    private const string UiDataFolder = "Assets/Data/UI";
    private const string ScanlinesPath = UiDataFolder + "/Scanlines.asset";
    // Green phosphor text colors (bright / dim).
    private static readonly Color Phosphor = new Color(0.45f, 1f, 0.55f);
    private static readonly Color PhosphorDim = new Color(0.2f, 0.55f, 0.28f);

    // combat overlay: red frame, title, target list + her health (left), floating numbers over the hostiles
    internal static void BuildCombatHud(Transform canvas, CombatEncounter encounter, SecurityCameraSystem cameraSystem,
                                       CameraSettings settings, Health partnerHealth)
    {
        Color red = new Color(1f, 0.22f, 0.18f);
        var root = EditorUi.CreateUiObject(canvas, "CombatHud");
        root.transform.SetSiblingIndex(1);   // over the camera HUD, under subtitles / command line / save screen
        EditorUi.Stretch((RectTransform)root.transform);
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // frame: four thin bars on the screen edges
        foreach (var (min, max) in new[] { (new Vector2(0f, 1f), new Vector2(1f, 1f)), (Vector2.zero, new Vector2(1f, 0f)),
                                           (Vector2.zero, new Vector2(0f, 1f)), (new Vector2(1f, 0f), Vector2.one) })
        {
            Image bar = EditorUi.CreateUiObject(root.transform, "Frame").AddComponent<Image>();
            bar.color = new Color(red.r, red.g, red.b, 0.6f);
            bar.raycastTarget = false;
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            bool horizontal = min.y == max.y;
            rt.sizeDelta = horizontal ? new Vector2(0f, 6f) : new Vector2(6f, 0f);
        }

        var labels = EditorUi.CreateUiObject(root.transform, "TargetLabels").GetComponent<RectTransform>();
        EditorUi.Stretch(labels);

        Vector2 tl = new Vector2(0f, 1f);
        TMP_Text title = EditorUi.CreateText(root.transform, "Title", 30, TextAlignmentOptions.Left, red);
        title.fontStyle = FontStyles.Bold;
        EditorUi.Place(title.rectTransform, tl, tl, tl, new Vector2(52f, -150f), new Vector2(300f, 36f));
        TMP_Text list = EditorUi.CreateText(root.transform, "Targets", 22, TextAlignmentOptions.TopLeft, red);
        EditorUi.Place(list.rectTransform, tl, tl, tl, new Vector2(52f, -190f), new Vector2(300f, 200f));

        TMP_Text name = EditorUi.CreateText(root.transform, "PartnerName", 18, TextAlignmentOptions.Left, Phosphor);
        name.text = "HELENA";
        Vector2 bl = Vector2.zero;
        EditorUi.Place(name.rectTransform, bl, bl, bl, new Vector2(52f, 96f), new Vector2(200f, 24f));
        Image back = EditorUi.CreateUiObject(root.transform, "PartnerBarBack").AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;
        EditorUi.Place(back.rectTransform, bl, bl, bl, new Vector2(52f, 80f), new Vector2(220f, 12f));
        Image fill = EditorUi.CreateUiObject(back.transform, "Fill").AddComponent<Image>();
        fill.raycastTarget = false;
        EditorUi.Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 12f));

        var hud = root.AddComponent<CombatHud>();
        EditorSerialized.SetReference(hud, CombatHud.EncounterField, encounter);
        EditorSerialized.SetReference(hud, CombatHud.CameraSystemField, cameraSystem);
        EditorSerialized.SetReference(hud, CombatHud.SettingsField, settings);
        EditorSerialized.SetReference(hud, CombatHud.PartnerHealthField, partnerHealth);
        EditorSerialized.SetReference(hud, CombatHud.GroupField, group);
        EditorSerialized.SetReference(hud, CombatHud.LabelsRootField, labels);
        EditorSerialized.SetReference(hud, CombatHud.TitleField, title);
        EditorSerialized.SetReference(hud, CombatHud.ListField, list);
        EditorSerialized.SetReference(hud, CombatHud.PartnerBarField, fill);
    }

    // game over: a red flash on every hit; on her death "SIGNAL LOST" + LOAD LAST SAVE / RESTART
    internal static void BuildGameOver(Transform canvas, PartnerDamage damage, Health health, SaveSystem saveSystem)
    {
        Color red = new Color(0.9f, 0.12f, 0.1f);
        var root = EditorUi.CreateUiObject(canvas, "GameOver");
        root.transform.SetAsLastSibling();
        EditorUi.Stretch((RectTransform)root.transform);

        Image flash = EditorUi.CreateUiObject(root.transform, "HitFlash").AddComponent<Image>();   // outside the group: shown while playing
        EditorUi.Stretch(flash.rectTransform);
        flash.color = new Color(0.6f, 0f, 0f, 0f);
        flash.raycastTarget = false;

        var screenGo = EditorUi.CreateUiObject(root.transform, "Screen");
        EditorUi.Stretch((RectTransform)screenGo.transform);
        var group = screenGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        Image backdrop = EditorUi.CreateUiObject(screenGo.transform, "Backdrop").AddComponent<Image>();
        EditorUi.Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0.04f, 0f, 0f, 0.9f);

        RawImage scan = EditorUi.CreateUiObject(screenGo.transform, "Scanlines").AddComponent<RawImage>();
        EditorUi.Stretch(scan.rectTransform);
        scan.texture = EnsureScanlines();
        scan.uvRect = new Rect(0f, 0f, 1f, 180f);   // 720 px / 4 px per line
        scan.color = new Color(1f, 1f, 1f, 0.3f);
        scan.raycastTarget = false;

        Vector2 c = new Vector2(0.5f, 0.5f);
        TMP_Text title = EditorUi.CreateText(screenGo.transform, "Title", 64, TextAlignmentOptions.Center, red);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        title.text = "SIGNAL LOST";
        EditorUi.Place(title.rectTransform, c, c, c, new Vector2(0f, 110f), new Vector2(900f, 80f));
        TMP_Text detail = EditorUi.CreateText(screenGo.transform, "Detail", 26, TextAlignmentOptions.Center, new Color(0.85f, 0.75f, 0.72f));
        EditorUi.Place(detail.rectTransform, c, c, c, new Vector2(0f, 30f), new Vector2(900f, 80f));

        Button load = EditorUi.CreateButton(screenGo.transform, "LoadButton", "LOAD LAST SAVE", red, new Vector2(-150f, -80f));
        Button restart = EditorUi.CreateButton(screenGo.transform, "RestartButton", "RESTART", red, new Vector2(150f, -80f));

        var over = root.AddComponent<GameOverScreen>();
        EditorSerialized.SetReference(over, GameOverScreen.DamageField, damage);
        EditorSerialized.SetReference(over, GameOverScreen.HealthField, health);
        EditorSerialized.SetReference(over, GameOverScreen.SaveSystemField, saveSystem);
        EditorSerialized.SetReference(over, GameOverScreen.GroupField, group);
        EditorSerialized.SetReference(over, GameOverScreen.FlashField, flash);
        EditorSerialized.SetReference(over, GameOverScreen.DetailField, detail);
        EditorSerialized.SetReference(over, GameOverScreen.LoadButtonField, load);
        EditorSerialized.SetReference(over, GameOverScreen.RestartButtonField, restart);
    }

    // command line (Enter) above the subtitles, at the bottom of the operator's screen
    internal static CommandConsole BuildCommandUi(Transform canvas, PartnerCommands commands, PartnerVoice voice, SecurityCameraSystem cameraSystem,
                                       SaveScreen saveScreen, CameraSettings settings)
    {
        Vector2 bc = new Vector2(0.5f, 0f);

        var subtitleGo = EditorUi.CreateUiObject(canvas, "Subtitles");
        subtitleGo.transform.SetSiblingIndex(1);   // over the camera HUD, under the save screen and fader
        var subtitleGroup = subtitleGo.AddComponent<CanvasGroup>();
        subtitleGroup.blocksRaycasts = false;
        EditorUi.Place((RectTransform)subtitleGo.transform, bc, bc, bc, new Vector2(0f, 96f), new Vector2(1000f, 90f));
        Image band = EditorUi.CreateUiObject(subtitleGo.transform, "Band").AddComponent<Image>();
        EditorUi.Stretch(band.rectTransform);
        band.color = new Color(0f, 0f, 0f, 0.55f);
        band.raycastTarget = false;
        TMP_Text subtitle = EditorUi.CreateText(subtitleGo.transform, "Text", 28, TextAlignmentOptions.Center, Color.white);
        EditorUi.Stretch(subtitle.rectTransform);
        subtitle.margin = new Vector4(20f, 6f, 20f, 6f);
        var view = subtitleGo.AddComponent<SubtitleView>();
        EditorSerialized.SetReference(view, SubtitleView.VoiceField, voice);
        EditorSerialized.SetReference(view, SubtitleView.SettingsField, settings);
        EditorSerialized.SetReference(view, SubtitleView.TextField, subtitle);
        EditorSerialized.SetReference(view, SubtitleView.GroupField, subtitleGroup);

        var consoleGo = EditorUi.CreateUiObject(canvas, "CommandConsole");
        consoleGo.transform.SetSiblingIndex(2);
        EditorUi.Place((RectTransform)consoleGo.transform, bc, bc, bc, new Vector2(0f, 196f), new Vector2(900f, 44f));
        var panelGo = EditorUi.CreateUiObject(consoleGo.transform, "Line");
        EditorUi.Stretch((RectTransform)panelGo.transform);
        var panel = panelGo.AddComponent<CanvasGroup>();
        panel.blocksRaycasts = false;
        Image back = EditorUi.CreateUiObject(panelGo.transform, "Back").AddComponent<Image>();
        EditorUi.Stretch(back.rectTransform);
        back.color = new Color(0f, 0.06f, 0.02f, 0.8f);
        back.raycastTarget = false;
        var frame = back.gameObject.AddComponent<Outline>();
        frame.effectColor = PhosphorDim;
        frame.effectDistance = new Vector2(2f, -2f);
        TMP_Text input = EditorUi.CreateText(panelGo.transform, "Input", 24, TextAlignmentOptions.Left, Phosphor);
        EditorUi.Stretch(input.rectTransform);
        input.margin = new Vector4(16f, 0f, 16f, 0f);
        TMP_Text echo = EditorUi.CreateText(consoleGo.transform, "Echo", 20, TextAlignmentOptions.Center, PhosphorDim);
        EditorUi.Stretch(echo.rectTransform);

        var console = consoleGo.AddComponent<CommandConsole>();
        EditorSerialized.SetReference(console, CommandConsole.CommandsField, commands);
        EditorSerialized.SetReference(console, CommandConsole.CameraSystemField, cameraSystem);
        EditorSerialized.SetReference(console, CommandConsole.SaveScreenField, saveScreen);
        EditorSerialized.SetReference(console, CommandConsole.SettingsField, settings);
        EditorSerialized.SetReference(console, CommandConsole.PanelField, panel);
        EditorSerialized.SetReference(console, CommandConsole.InputTextField, input);
        EditorSerialized.SetReference(console, CommandConsole.EchoTextField, echo);
        return console;
    }

    // Canvas: ammo HUD (bottom right), save screen (terminal panel + scanlines), black fader on top.
    internal static SaveScreen BuildUi(Scene scene, SaveSystem saveSystem, PartnerShooter shooter)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        var canvasGo = new GameObject("UI", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        canvasGo.layer = uiLayer;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();   // map / settings buttons
        var eventSystem = new GameObject("EventSystem");
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        // ammo HUD
        TMP_Text ammoText = EditorUi.CreateText(canvasGo.transform, "AmmoHud", 44, TextAlignmentOptions.BottomRight, new Color(0.9f, 0.95f, 0.85f));
        EditorUi.Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(420f, 130f));
        var hud = ammoText.gameObject.AddComponent<AmmoHud>();
        EditorSerialized.SetReference(hud, AmmoHud.ShooterField, shooter);
        EditorSerialized.SetReference(hud, AmmoHud.TextField, ammoText);

        // save screen
        var screenGo = EditorUi.CreateUiObject(canvasGo.transform, "SaveScreen");
        EditorUi.Stretch((RectTransform)screenGo.transform);
        var panel = screenGo.AddComponent<CanvasGroup>();
        Image backdrop = EditorUi.CreateUiObject(screenGo.transform, "Backdrop").AddComponent<Image>();
        EditorUi.Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0f, 0f, 0.85f);
        backdrop.raycastTarget = false;

        Image terminal = EditorUi.CreateUiObject(screenGo.transform, "Terminal").AddComponent<Image>();
        EditorUi.Place(terminal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
        terminal.color = new Color(0.015f, 0.06f, 0.03f, 1f);
        terminal.raycastTarget = false;
        var frame = terminal.gameObject.AddComponent<Outline>();
        frame.effectColor = PhosphorDim;
        frame.effectDistance = new Vector2(3f, -3f);

        Vector2 topLeft = new Vector2(0f, 1f);
        TMP_Text header = EditorUi.CreateText(terminal.transform, "Header", 26, TextAlignmentOptions.TopLeft, Phosphor);
        EditorUi.Place(header.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -30f), new Vector2(920f, 110f));
        TMP_Text body = EditorUi.CreateText(terminal.transform, "Body", 26, TextAlignmentOptions.TopLeft, Phosphor);
        EditorUi.Place(body.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -150f), new Vector2(920f, 240f));
        TMP_Text status = EditorUi.CreateText(terminal.transform, "Status", 24, TextAlignmentOptions.TopLeft, Phosphor);
        EditorUi.Place(status.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -400f), new Vector2(920f, 80f));
        Vector2 bottomLeft = Vector2.zero;
        TMP_Text hint = EditorUi.CreateText(terminal.transform, "Hint", 18, TextAlignmentOptions.BottomLeft, PhosphorDim);
        EditorUi.Place(hint.rectTransform, bottomLeft, bottomLeft, bottomLeft, new Vector2(40f, 22f), new Vector2(920f, 30f));

        RawImage scan = EditorUi.CreateUiObject(terminal.transform, "Scanlines").AddComponent<RawImage>();
        EditorUi.Stretch(scan.rectTransform);
        scan.texture = EnsureScanlines();
        scan.uvRect = new Rect(0f, 0f, 1f, 140f);   // 560 px / 4 px per line
        scan.color = new Color(1f, 1f, 1f, 0.35f);
        scan.raycastTarget = false;

        // fader (on top of everything)
        Image fade = EditorUi.CreateUiObject(canvasGo.transform, "Fader").AddComponent<Image>();
        EditorUi.Stretch(fade.rectTransform);
        fade.color = Color.black;
        fade.raycastTarget = false;
        var fader = fade.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f;
        panel.alpha = 0f;

        var screen = screenGo.AddComponent<SaveScreen>();
        EditorSerialized.SetReference(screen, SaveScreen.SaveSystemField, saveSystem);
        EditorSerialized.SetReference(screen, SaveScreen.PanelField, panel);
        EditorSerialized.SetReference(screen, SaveScreen.FaderField, fader);
        EditorSerialized.SetReference(screen, SaveScreen.HeaderTextField, header);
        EditorSerialized.SetReference(screen, SaveScreen.BodyTextField, body);
        EditorSerialized.SetReference(screen, SaveScreen.StatusTextField, status);
        EditorSerialized.SetReference(screen, SaveScreen.HintTextField, hint);
        return screen;
    }

    // 1 x 4 texture: 2 clear rows + 2 dark rows, repeated = CRT scanlines
    internal static Texture2D EnsureScanlines()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ScanlinesPath);
        if (tex != null) return tex;
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "UI");
        tex = new Texture2D(1, 4, TextureFormat.RGBA32, false)
        {
            name = "Scanlines",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
        };
        tex.SetPixels(new[] { new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.8f), new Color(0f, 0f, 0f, 0.8f) });
        tex.Apply();
        AssetDatabase.CreateAsset(tex, ScanlinesPath);
        return tex;
    }
}
