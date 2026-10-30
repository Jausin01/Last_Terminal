using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The screen UI of his own room (Last Terminal/16): the fake OS (OperatorOS), the evidence panel, the keypad overlay.
// Retro look: gray windows, blue title bars (like the desktop of the location scenes).
internal static class OperatorRoomUiBuilder
{
    // OS art folder; the retro window colors; anchor shorthands.
    private const string OsArt = "Assets/Art/UI/OperatorOS";
    private static readonly Color WinGray = new Color(0.76f, 0.76f, 0.74f);
    private static readonly Color WinLight = new Color(0.86f, 0.86f, 0.84f);
    private static readonly Color WinTitle = new Color(0.05f, 0.12f, 0.42f);
    private static readonly Color WinBody = new Color(0.97f, 0.97f, 0.95f);
    private static readonly Color Ink = new Color(0.08f, 0.08f, 0.1f);
    private static readonly Color Phosphor = new Color(0.75f, 0.78f, 0.75f);
    private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    private static readonly List<(TMP_Text text, string en, string pt)> labels = new List<(TMP_Text, string, string)>();

    // ================================================================== the fake OS
    internal static OperatorOS BuildOS(Transform canvas, CameraSettings settings, StoryProgress story, AudioSource audio, PcPower power,
                                       List<OperatorFile> files, List<OperatorEmail> mails, SettingsWindow settingsWindow)
    {
        labels.Clear();
        var go = EditorUi.CreateUiObject(canvas, "OperatorOS");
        EditorUi.Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();
        Texture2D wallTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_Wallpaper.png");
        Texture2D wallPt = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_Wallpaper_PT.png");   // (its words in Portuguese)

        // power off / BIOS / logo
        CanvasGroup off = Screen(go.transform, "Off", Color.black);
        CanvasGroup bios = Screen(go.transform, "Bios", Color.black);
        TMP_Text biosText = EditorUi.CreateText(bios.transform, "Text", 22, TextAlignmentOptions.TopLeft, Phosphor);
        EditorUi.Place(biosText.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -36f), new Vector2(1000f, 600f));
        biosText.lineSpacing = 10f;
        CanvasGroup logo = Screen(go.transform, "Logo", Color.black);
        RawImage logoArt = EditorUi.CreateUiObject(logo.transform, "Art").AddComponent<RawImage>();
        EditorUi.Stretch(logoArt.rectTransform);
        logoArt.texture = wallTex;
        logoArt.color = wallTex != null ? Color.white : Color.black;
        logoArt.raycastTarget = false;
        Localize(logoArt, wallTex, wallPt, settings);

        // login
        CanvasGroup loginScreen = Screen(go.transform, "Login", new Color(0.02f, 0.08f, 0.1f));
        RawImage loginWall = EditorUi.CreateUiObject(loginScreen.transform, "Wallpaper").AddComponent<RawImage>();
        EditorUi.Stretch(loginWall.rectTransform);
        loginWall.texture = wallTex;
        loginWall.color = new Color(0.45f, 0.45f, 0.45f);
        loginWall.raycastTarget = false;
        Localize(loginWall, wallTex, wallPt, settings);
        var loginBox = EditorUi.CreateUiObject(loginScreen.transform, "Box");
        EditorUi.Place((RectTransform)loginBox.transform, Mid, Mid, Mid, new Vector2(0f, -40f), new Vector2(460f, 250f));
        loginBox.AddComponent<Image>().color = WinGray;
        Outline(loginBox, 2f);
        Image loginBar = Bar(loginBox.transform, out TMP_Text loginTitle);
        loginTitle.text = "TERMINAL OS";
        RawImage avatar = EditorUi.CreateUiObject(loginBox.transform, "Avatar").AddComponent<RawImage>();
        avatar.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_IconComputer.png");
        avatar.raycastTarget = false;
        EditorUi.Place(avatar.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(20f, -52f), new Vector2(72f, 72f));
        Label(loginBox.transform, "UserLabel", new Vector2(110f, -54f), new Vector2(110f, 30f), 18, "User:", "Usuário:");
        TMP_Text user = EditorUi.CreateText(loginBox.transform, "User", 20, TextAlignmentOptions.Left, Ink);
        EditorUi.Place(user.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(220f, -54f), new Vector2(220f, 30f));
        user.fontStyle = FontStyles.Bold;
        Label(loginBox.transform, "PassLabel", new Vector2(110f, -96f), new Vector2(110f, 30f), 18, "Password:", "Senha:");
        TMP_InputField pass = Input(loginBox.transform, "Password", 20, Color.white, Ink, true);
        EditorUi.Place((RectTransform)pass.transform, TopLeft, TopLeft, TopLeft, new Vector2(220f, -94f), new Vector2(215f, 32f));
        Button ok = UiButton(loginBox.transform, "OK", TopLeft, new Vector2(330f, -142f), new Vector2(105f, 34f), "OK", "OK", 18);
        ((RectTransform)ok.transform).pivot = TopLeft;
        TMP_Text loginMsg = EditorUi.CreateText(loginBox.transform, "Message", 17, TextAlignmentOptions.TopLeft, new Color(0.6f, 0.05f, 0.05f));
        EditorUi.Place(loginMsg.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(20f, -184f), new Vector2(420f, 56f));
        Button loginStand = UiButton(loginScreen.transform, "Stand", new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(160f, 36f), "STAND UP", "LEVANTAR", 17);
        ((RectTransform)loginStand.transform).pivot = Vector2.zero;
        Label(loginScreen.transform, "Hint", new Vector2(0f, 0f), new Vector2(1280f, 26f), 15, "Type the password and press Enter", "Digite a senha e aperte Enter",
              new Vector2(0.5f, 0f), new Vector2(0f, 26f), TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.85f));

        // desktop
        CanvasGroup desk = Screen(go.transform, "Desktop", new Color(0.02f, 0.18f, 0.2f));
        RawImage wall = EditorUi.CreateUiObject(desk.transform, "Wallpaper").AddComponent<RawImage>();
        EditorUi.Stretch(wall.rectTransform);
        wall.texture = wallTex;
        wall.color = wallTex != null ? Color.white : new Color(0.02f, 0.18f, 0.2f);
        Localize(wall, wallTex, wallPt, settings);
        var background = wall.gameObject.AddComponent<Button>();
        background.transition = Selectable.Transition.None;

        Button cameras = Icon(desk.transform, 0, "T_OpOS_IconCameras", "CCTV.EXE", "CCTV.EXE", out _);
        Button computer = Icon(desk.transform, 1, "T_OpOS_IconComputer", "My Computer", "Meu Computador", out _);
        Button mail = Icon(desk.transform, 2, "T_OpOS_IconMail", null, null, out TMP_Text mailLabel);
        Button recycle = Icon(desk.transform, 3, "T_OpOS_IconRecycle", "Recycle Bin", "Lixeira", out _);
        Button settingsIcon = Icon(desk.transform, 4, "T_OpOS_IconSettings", "Settings", "Configurações", out _);

        var windowsGo = EditorUi.CreateUiObject(desk.transform, "Windows");
        var windows = (RectTransform)windowsGo.transform;
        windows.anchorMin = Vector2.zero; windows.anchorMax = Vector2.one; windows.pivot = Mid;
        windows.offsetMin = new Vector2(0f, 40f); windows.offsetMax = Vector2.zero;

        // explorer: folders on the left, files on the right, path on top
        RectTransform exBody = Window(windows, "Explorer", new Vector2(-70f, 20f), new Vector2(780f, 450f), out OsWindow explorer);
        Image pathBack = EditorUi.CreateUiObject(exBody, "Path").AddComponent<Image>();
        pathBack.color = Color.white;
        EditorUi.Place(pathBack.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 28f));
        TMP_Text path = EditorUi.CreateText(pathBack.transform, "Text", 16, TextAlignmentOptions.Left, Ink);
        EditorUi.Stretch(path.rectTransform);
        path.margin = new Vector4(8f, 0f, 8f, 0f);
        RectTransform folderArea = Area(exBody, "Folders", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(210f, -32f));
        RectTransform folders = ScrollList(folderArea, new Color(0.9f, 0.9f, 0.88f), out _);
        RectTransform listArea = Area(exBody, "Files", Vector2.zero, Vector2.one, new Vector2(214f, 0f), new Vector2(0f, -32f));
        RectTransform list = ScrollList(listArea, WinBody, out _);
        TMP_Text empty = EditorUi.CreateText(listArea, "Empty", 17, TextAlignmentOptions.Center, new Color(0.4f, 0.4f, 0.4f));
        EditorUi.Stretch(empty.rectTransform);
        OperatorFileRow row = RowTemplate(list);

        // viewers
        RectTransform tvBody = Window(windows, "TextViewer", new Vector2(150f, 0f), new Vector2(640f, 470f), out OsWindow textViewer);
        RectTransform textContent = ScrollText(tvBody, WinBody, 19, Ink, out TMP_Text textBody, out ScrollRect textScroll);
        RectTransform ivBody = Window(windows, "ImageViewer", new Vector2(120f, -10f), new Vector2(660f, 520f), out OsWindow imageViewer);
        Image ivBack = EditorUi.CreateUiObject(ivBody, "Back").AddComponent<Image>();
        ivBack.color = new Color(0.12f, 0.12f, 0.12f);
        ivBack.rectTransform.anchorMin = Vector2.zero; ivBack.rectTransform.anchorMax = Vector2.one;
        ivBack.rectTransform.offsetMin = new Vector2(0f, 44f); ivBack.rectTransform.offsetMax = Vector2.zero;
        RawImage picture = EditorUi.CreateUiObject(ivBack.transform, "Picture").AddComponent<RawImage>();
        EditorUi.Stretch(picture.rectTransform);
        picture.raycastTarget = false;
        var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 4f / 3f;
        TMP_Text caption = EditorUi.CreateText(ivBody, "Caption", 17, TextAlignmentOptions.Left, Ink);
        EditorUi.Place(caption.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(-8f, 42f));
        caption.margin = new Vector4(6f, 2f, 6f, 2f);

        // e-mail: list on the left, the message on the right
        RectTransform mBody = Window(windows, "Mail", new Vector2(0f, 20f), new Vector2(860f, 480f), out OsWindow mailWindow);
        RectTransform mListArea = Area(mBody, "List", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(330f, 0f));
        RectTransform mList = ScrollList(mListArea, WinBody, out _);
        RectTransform readArea = Area(mBody, "Message", Vector2.zero, Vector2.one, new Vector2(334f, 0f), Vector2.zero);
        Image headerBack = EditorUi.CreateUiObject(readArea, "Header").AddComponent<Image>();
        headerBack.color = new Color(0.9f, 0.9f, 0.88f);
        EditorUi.Place(headerBack.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 84f));
        TMP_Text mHeader = EditorUi.CreateText(headerBack.transform, "Text", 16, TextAlignmentOptions.TopLeft, Ink);
        EditorUi.Stretch(mHeader.rectTransform);
        mHeader.margin = new Vector4(10f, 8f, 10f, 4f);
        // the voice of the e-mail: play / pause, who is speaking, how much was played, the time
        Image voiceBack = EditorUi.CreateUiObject(readArea, "Voice").AddComponent<Image>();
        voiceBack.color = new Color(0.82f, 0.84f, 0.86f);
        EditorUi.Place(voiceBack.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(0f, 44f));
        Button voicePlay = UiButton(voiceBack.transform, "Play", new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(36f, 36f), "", "", 16, false);
        ((RectTransform)voicePlay.transform).pivot = new Vector2(0f, 0.5f);
        RawImage voiceIcon = EditorUi.CreateUiObject(voicePlay.transform, "Icon").AddComponent<RawImage>();
        EditorUi.Place(voiceIcon.rectTransform, Mid, Mid, Mid, Vector2.zero, new Vector2(28f, 28f));
        voiceIcon.raycastTarget = false;
        Texture2D playTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_Play.png");
        voiceIcon.texture = playTex;
        TMP_Text voiceLabel = EditorUi.CreateText(voiceBack.transform, "Label", 15, TextAlignmentOptions.Left, Ink);
        EditorUi.Place(voiceLabel.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(52f, -2f), new Vector2(330f, 22f));
        Image track = EditorUi.CreateUiObject(voiceBack.transform, "Track").AddComponent<Image>();
        track.color = new Color(0.55f, 0.56f, 0.58f);
        track.raycastTarget = false;
        EditorUi.Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-22f, 10f), new Vector2(-148f, 6f));
        Image fill = EditorUi.CreateUiObject(track.transform, "Fill").AddComponent<Image>();
        fill.color = WinTitle;
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
        TMP_Text voiceTime = EditorUi.CreateText(voiceBack.transform, "Time", 15, TextAlignmentOptions.Right, Ink);
        EditorUi.Place(voiceTime.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, -6f), new Vector2(90f, 22f));
        var voiceSource = voiceBack.gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        RectTransform mBodyArea = Area(readArea, "Body", Vector2.zero, Vector2.one, new Vector2(0f, 40f), new Vector2(0f, -136f));
        ScrollText(mBodyArea, WinBody, 18, Ink, out TMP_Text mText, out ScrollRect mScroll);
        Button attach = UiButton(readArea, "Attachment", Vector2.zero, new Vector2(4f, 4f), new Vector2(380f, 32f), "", "", 16);
        ((RectTransform)attach.transform).pivot = Vector2.zero;
        TMP_Text attachLabel = attach.GetComponentInChildren<TMP_Text>();
        attachLabel.alignment = TextAlignmentOptions.Left;
        attachLabel.margin = new Vector4(10f, 0f, 10f, 0f);

        // terminal (black, Start > Run > cmd)
        RectTransform tBody = Window(windows, "Terminal", new Vector2(60f, -20f), new Vector2(720f, 420f), out OsWindow terminal);
        RectTransform outArea = Area(tBody, "Output", Vector2.zero, Vector2.one, new Vector2(0f, 34f), Vector2.zero);
        ScrollText(outArea, Color.black, 17, Phosphor, out TMP_Text tOut, out ScrollRect tScroll);
        Image lineBack = EditorUi.CreateUiObject(tBody, "Line").AddComponent<Image>();
        lineBack.color = Color.black;
        EditorUi.Place(lineBack.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 32f));
        TMP_Text promptLabel = EditorUi.CreateText(lineBack.transform, "Prompt", 17, TextAlignmentOptions.Left, Phosphor);
        EditorUi.Place(promptLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(140f, 30f));
        promptLabel.text = "C:\\OPERADOR>";
        TMP_InputField tIn = Input(lineBack.transform, "Input", 17, Color.black, Phosphor, false);
        var tInRt = (RectTransform)tIn.transform;
        tInRt.anchorMin = Vector2.zero; tInRt.anchorMax = Vector2.one; tInRt.offsetMin = new Vector2(150f, 2f); tInRt.offsetMax = new Vector2(-4f, -2f);
        tIn.caretColor = Phosphor;
        tIn.customCaretColor = true;

        // run + message boxes
        RectTransform rBody = Window(windows, "Run", new Vector2(-380f, -180f), new Vector2(440f, 170f), out OsWindow run);
        Label(rBody, "Text", new Vector2(10f, -6f), new Vector2(410f, 44f), 16, "Type the name of a program and Windows will open it.", "Digite o nome de um programa e ele será aberto.");
        TMP_InputField rIn = Input(rBody, "Input", 18, Color.white, Ink, false);
        EditorUi.Place((RectTransform)rIn.transform, TopLeft, TopLeft, TopLeft, new Vector2(10f, -54f), new Vector2(410f, 30f));
        Button rOk = UiButton(rBody, "OK", new Vector2(1f, 0f), new Vector2(-120f, 8f), new Vector2(100f, 30f), "OK", "OK", 16);
        Button rCancel = UiButton(rBody, "Cancel", new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(100f, 30f), "Cancel", "Cancelar", 16);
        ((RectTransform)rOk.transform).pivot = new Vector2(1f, 0f);
        ((RectTransform)rCancel.transform).pivot = new Vector2(1f, 0f);
        RectTransform msgBody = Window(windows, "Message", Vector2.zero, new Vector2(460f, 200f), out OsWindow message);
        TMP_Text msgText = EditorUi.CreateText(msgBody, "Text", 18, TextAlignmentOptions.Center, Ink);
        msgText.rectTransform.anchorMin = Vector2.zero; msgText.rectTransform.anchorMax = Vector2.one;
        msgText.rectTransform.offsetMin = new Vector2(10f, 44f); msgText.rectTransform.offsetMax = new Vector2(-10f, -6f);
        Button msgOk = UiButton(msgBody, "OK", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(110f, 30f), "OK", "OK", 16);
        ((RectTransform)msgOk.transform).pivot = new Vector2(0.5f, 0f);

        // taskbar + start menu
        Image bar = EditorUi.CreateUiObject(desk.transform, "Taskbar").AddComponent<Image>();
        bar.color = WinGray;
        EditorUi.Place(bar.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 40f));
        Button start = UiButton(bar.transform, "Start", new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(120f, 30f), "<b>Start</b>", "<b>Iniciar</b>", 18);
        ((RectTransform)start.transform).pivot = new Vector2(0f, 0.5f);
        TMP_Text status = EditorUi.CreateText(bar.transform, "Status", 16, TextAlignmentOptions.Left, new Color(0.5f, 0.05f, 0.05f));
        EditorUi.Place(status.rectTransform, Vector2.zero, Vector2.one, Mid, new Vector2(30f, 0f), new Vector2(-340f, 0f));
        Image tray = EditorUi.CreateUiObject(bar.transform, "Tray").AddComponent<Image>();
        tray.color = new Color(0.7f, 0.7f, 0.68f);
        EditorUi.Place(tray.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(96f, 30f));
        TMP_Text clock = EditorUi.CreateText(tray.transform, "Clock", 17, TextAlignmentOptions.Center, Ink);
        EditorUi.Stretch(clock.rectTransform);

        var menuGo = EditorUi.CreateUiObject(desk.transform, "StartMenu");
        EditorUi.Place((RectTransform)menuGo.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(4f, 40f), new Vector2(270f, 8 * 40f + 16f));
        var startMenu = menuGo.AddComponent<CanvasGroup>();
        menuGo.AddComponent<Image>().color = WinGray;
        Outline(menuGo, 2f);
        Image side = EditorUi.CreateUiObject(menuGo.transform, "Side").AddComponent<Image>();
        side.color = WinTitle;
        EditorUi.Place(side.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(30f, -8f));
        TMP_Text sideText = EditorUi.CreateText(side.transform, "Text", 18, TextAlignmentOptions.Center, Color.white);
        EditorUi.Place(sideText.rectTransform, Mid, Mid, Mid, Vector2.zero, new Vector2(270f, 30f));
        sideText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        sideText.text = "<b>TERMINAL</b> OS";
        var entries = new (string icon, string en, string pt)[]
        {
            ("Cameras", "Cameras (CCTV.EXE)", "Câmeras (CCTV.EXE)"), ("Computer", "My Computer", "Meu Computador"), ("Mail", "E-mail", "E-mail"),
            ("Recycle", "Recycle Bin", "Lixeira"), ("Settings", "Settings", "Configurações"), ("Run", "Run...", "Executar..."),
            ("Stand", "Stand up", "Levantar"), ("Power", "Shut down", "Desligar"),
        };
        var startButtons = new Button[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            startButtons[i] = MenuEntry(menuGo.transform, i, entries[i].icon, entries[i].en, entries[i].pt);

        // ---------------------------------------------------------------- the component
        var os = go.AddComponent<OperatorOS>();
        var so = new SerializedObject(os);
        Set(so, OperatorOS.GroupField, group);
        Set(so, OperatorOS.SettingsField, settings);
        Set(so, OperatorOS.StoryField, story);
        Set(so, OperatorOS.AudioField, audio);
        Set(so, OperatorOS.PowerField, power);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.off)), off);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.bios)), bios);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.biosText)), biosText);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.logo)), logo);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.loginScreen)), loginScreen);
        Set(so, Sub(OperatorOS.ScreensField, nameof(OperatorOS.ScreenRefs.desktopScreen)), desk);
        Set(so, Sub(OperatorOS.LoginField, nameof(OperatorOS.LoginRefs.user)), user);
        Set(so, Sub(OperatorOS.LoginField, nameof(OperatorOS.LoginRefs.passwordInput)), pass);
        Set(so, Sub(OperatorOS.LoginField, nameof(OperatorOS.LoginRefs.ok)), ok);
        Set(so, Sub(OperatorOS.LoginField, nameof(OperatorOS.LoginRefs.stand)), loginStand);
        Set(so, Sub(OperatorOS.LoginField, nameof(OperatorOS.LoginRefs.message)), loginMsg);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.background)), background);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.cameras)), cameras);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.computer)), computer);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.mail)), mail);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.mailLabel)), mailLabel);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.recycle)), recycle);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.start)), start);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startMenu)), startMenu);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startCameras)), startButtons[0]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startFiles)), startButtons[1]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startMail)), startButtons[2]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startRecycle)), startButtons[3]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startSettings)), startButtons[4]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startRun)), startButtons[5]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startStand)), startButtons[6]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.startShutdown)), startButtons[7]);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.settings)), settingsIcon);
        Set(so, OperatorOS.SettingsWindowField, settingsWindow);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.clock)), clock);
        Set(so, Sub(OperatorOS.DesktopField, nameof(OperatorOS.DesktopRefs.status)), status);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.window)), explorer);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.folders)), folders);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.list)), list);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.rowTemplate)), row);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.path)), path);
        Set(so, Sub(OperatorOS.ExplorerField, nameof(OperatorOS.ExplorerRefs.empty)), empty);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.text)), textViewer);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.textBody)), textBody);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.textScroll)), textScroll);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.image)), imageViewer);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.picture)), picture);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.pictureFitter)), fitter);
        Set(so, Sub(OperatorOS.ViewersField, nameof(OperatorOS.ViewerRefs.caption)), caption);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.window)), mailWindow);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.list)), mList);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.header)), mHeader);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.body)), mText);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.bodyScroll)), mScroll);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.attachment)), attach);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.attachmentLabel)), attachLabel);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceBar)), voiceBack.gameObject);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voicePlay)), voicePlay);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceIcon)), voiceIcon);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.playTexture)), playTex);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.pauseTexture)), AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_Pause.png"));
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceFill)), fill.rectTransform);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceLabel)), voiceLabel);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceTime)), voiceTime);
        Set(so, Sub(OperatorOS.MailField, nameof(OperatorOS.MailRefs.voiceSource)), voiceSource);
        Set(so, Sub(OperatorOS.TerminalField, nameof(OperatorOS.TerminalRefs.window)), terminal);
        Set(so, Sub(OperatorOS.TerminalField, nameof(OperatorOS.TerminalRefs.output)), tOut);
        Set(so, Sub(OperatorOS.TerminalField, nameof(OperatorOS.TerminalRefs.scroll)), tScroll);
        Set(so, Sub(OperatorOS.TerminalField, nameof(OperatorOS.TerminalRefs.input)), tIn);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.run)), run);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.runInput)), rIn);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.runOk)), rOk);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.runCancel)), rCancel);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.message)), message);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.messageText)), msgText);
        Set(so, Sub(OperatorOS.DialogsField, nameof(OperatorOS.DialogRefs.messageOk)), msgOk);

        SerializedProperty fp = so.FindProperty(OperatorOS.FilesField);
        fp.arraySize = files.Count;
        for (int i = 0; i < files.Count; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = files[i];
        SerializedProperty mp = so.FindProperty(OperatorOS.EmailsField);
        mp.arraySize = mails.Count;
        for (int i = 0; i < mails.Count; i++) mp.GetArrayElementAtIndex(i).objectReferenceValue = mails[i];
        OperatorOS.TerminalEntry[] cmds = OperatorRoomContent.TerminalEntries;
        SerializedProperty tp = so.FindProperty(OperatorOS.TerminalEntriesField);
        tp.arraySize = cmds.Length;
        for (int i = 0; i < cmds.Length; i++)
        {
            SerializedProperty e = tp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(nameof(OperatorOS.TerminalEntry.command)).stringValue = cmds[i].command;
            e.FindPropertyRelative(nameof(OperatorOS.TerminalEntry.responseEn)).stringValue = cmds[i].responseEn;
            e.FindPropertyRelative(nameof(OperatorOS.TerminalEntry.responsePt)).stringValue = cmds[i].responsePt;
            e.FindPropertyRelative(nameof(OperatorOS.TerminalEntry.setFlag)).stringValue = cmds[i].setFlag ?? "";
        }
        SerializedProperty lp = so.FindProperty(OperatorOS.LabelsField);
        lp.arraySize = labels.Count;
        for (int i = 0; i < labels.Count; i++)
        {
            SerializedProperty e = lp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(nameof(OperatorOS.LabelRef.text)).objectReferenceValue = labels[i].text;
            e.FindPropertyRelative(nameof(OperatorOS.LabelRef.en)).stringValue = labels[i].en;
            e.FindPropertyRelative(nameof(OperatorOS.LabelRef.pt)).stringValue = labels[i].pt;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return os;
    }

    // ================================================================== the settings window (gear on the desktop, Esc over the cameras)
    // A shade over everything (clicks stay in the window), the window in the middle: four sections of rows, each row a
    // name on the left and its control on the right (two choices, a slider with its value, an on / off button).
    internal static SettingsWindow BuildSettingsWindow(Transform canvas, CameraSettings settings, AudioSource audio)
    {
        var texts = new List<(TMP_Text text, string en, string pt)>();
        var go = EditorUi.CreateUiObject(canvas, "SettingsWindow");
        EditorUi.Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();
        Image shade = go.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.45f);
        var win = EditorUi.CreateUiObject(go.transform, "Window");
        EditorUi.Place((RectTransform)win.transform, Mid, Mid, Mid, Vector2.zero, new Vector2(660f, 770f));
        win.AddComponent<Image>().color = WinGray;
        Outline(win, 2f);
        Bar(win.transform, out TMP_Text title);
        Button close = UiButton(win.transform.Find("TitleBar"), "Close", new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(28f, 22f), "X", "X", 16, false);
        Image body = EditorUi.CreateUiObject(win.transform, "Body").AddComponent<Image>();
        body.color = WinBody;
        body.rectTransform.anchorMin = Vector2.zero; body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.offsetMin = new Vector2(8f, 58f); body.rectTransform.offsetMax = new Vector2(-8f, -40f);
        Transform b = body.transform;

        float y = -10f;
        void Section(string en, string pt)
        {
            TMP_Text h = EditorUi.CreateText(b, "Section", 17, TextAlignmentOptions.Left, WinTitle);
            EditorUi.Place(h.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(14f, y), new Vector2(600f, 24f));
            h.fontStyle = FontStyles.Bold;
            h.characterSpacing = 3f;
            texts.Add((h, en.ToUpperInvariant(), pt.ToUpperInvariant()));
            Image rule = EditorUi.CreateUiObject(b, "Rule").AddComponent<Image>();
            rule.color = new Color(0.6f, 0.6f, 0.62f);
            rule.raycastTarget = false;
            EditorUi.Place(rule.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(14f, y - 25f), new Vector2(616f, 1f));
            y -= 32f;
        }
        void RowName(string en, string pt)
        {
            TMP_Text t = EditorUi.CreateText(b, "Name", 17, TextAlignmentOptions.Left, Ink);
            EditorUi.Place(t.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(26f, y), new Vector2(250f, 32f));
            texts.Add((t, en, pt));
        }
        Button Choice(string name, float x, float width, string en, string pt)
        {
            Button c = UiButton(b, name, TopLeft, new Vector2(x, y - 2f), new Vector2(width, 28f), en, pt, 16, false);
            ((RectTransform)c.transform).pivot = TopLeft;
            texts.Add((c.GetComponentInChildren<TMP_Text>(), en, pt));
            return c;
        }
        SettingsWindow.Level Level(string name)
        {
            var level = new SettingsWindow.Level();
            GameObject sg = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sg.name = name;
            sg.transform.SetParent(b, false);
            foreach (Transform t in sg.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = b.gameObject.layer;
            var rt = (RectTransform)sg.transform;
            EditorUi.Place(rt, TopLeft, TopLeft, TopLeft, new Vector2(290f, y - 8f), new Vector2(250f, 16f));
            rt.pivot = TopLeft;
            level.slider = sg.GetComponent<Slider>();
            sg.transform.Find("Background").GetComponent<Image>().color = new Color(0.62f, 0.62f, 0.6f);
            sg.transform.Find("Fill Area/Fill").GetComponent<Image>().color = WinTitle;
            Image handle = sg.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
            handle.color = WinLight;
            var ho = handle.gameObject.AddComponent<Outline>();
            ho.effectColor = new Color(0.2f, 0.2f, 0.2f);
            ho.effectDistance = new Vector2(1f, -1f);
            level.value = EditorUi.CreateText(b, name + "Value", 17, TextAlignmentOptions.Right, Ink);
            EditorUi.Place(level.value.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(548f, y), new Vector2(80f, 32f));
            return level;
        }

        var refs = new SettingsWindow.Refs();
        Section("Cameras", "Câmeras");
        RowName("Camera control", "Controle da câmera");
        refs.pan.a = Choice("PanMouse", 290f, 166f, "Mouse (screen edges)", "Mouse (bordas)");
        refs.pan.b = Choice("PanKeys", 462f, 166f, "Keyboard (A / D)", "Teclado (A / D)");
        y -= 38f;
        RowName("Speed (mouse)", "Velocidade (mouse)");
        refs.mouseSpeed = Level("MouseSpeed");
        y -= 38f;
        RowName("Speed (keyboard)", "Velocidade (teclado)");
        refs.keyboardSpeed = Level("KeyboardSpeed");
        y -= 46f;
        Section("Picture", "Imagem");
        RowName("Camera filter (CRT)", "Filtro da câmera (CRT)");
        refs.filter = Choice("Filter", 290f, 166f, "On", "Ligado");
        y -= 38f;
        RowName("Filter strength", "Força do filtro");
        refs.filterStrength = Level("FilterStrength");
        y -= 46f;
        Section("Sound", "Som");
        RowName("Volume", "Volume geral");
        refs.volume = Level("Volume");
        y -= 38f;
        // the microphone: its name cycles the devices (the label is set by the window, not translated here)
        RowName("Microphone", "Microfone");
        refs.microphone = UiButton(b, "Microphone", TopLeft, new Vector2(290f, y - 2f), new Vector2(338f, 28f), "", "", 15, false);
        ((RectTransform)refs.microphone.transform).pivot = TopLeft;
        y -= 38f;
        RowName("Test microphone", "Testar microfone");
        refs.micRecord = UiButton(b, "MicRecord", TopLeft, new Vector2(290f, y - 2f), new Vector2(210f, 28f), "", "", 15, false);
        ((RectTransform)refs.micRecord.transform).pivot = TopLeft;
        Image meterBack = EditorUi.CreateUiObject(b, "MicMeter").AddComponent<Image>();
        meterBack.color = new Color(0.62f, 0.62f, 0.6f);
        EditorUi.Place(meterBack.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(510f, y - 8f), new Vector2(118f, 16f));
        meterBack.rectTransform.pivot = TopLeft;
        Image meter = EditorUi.CreateUiObject(meterBack.transform, "Level").AddComponent<Image>();
        meter.color = new Color(0.15f, 0.6f, 0.2f);
        meter.type = Image.Type.Filled;
        meter.fillMethod = Image.FillMethod.Horizontal;
        meter.fillAmount = 0f;
        meter.raycastTarget = false;
        EditorUi.Stretch(meter.rectTransform);
        refs.micLevel = meter;
        y -= 34f;
        refs.micHeard = EditorUi.CreateText(b, "MicHeard", 14, TextAlignmentOptions.Left, Ink);
        EditorUi.Place(refs.micHeard.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(26f, y), new Vector2(602f, 24f));
        refs.micHeard.fontStyle = FontStyles.Italic;
        y -= 34f;
        RowName("Sound output", "Saída de som");
        refs.outputTest = Choice("OutputTest", 290f, 166f, "Test sound", "Testar som");
        y -= 34f;
        refs.outputNote = EditorUi.CreateText(b, "OutputNote", 13, TextAlignmentOptions.TopLeft, Ink);
        EditorUi.Place(refs.outputNote.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(26f, y), new Vector2(602f, 36f));
        refs.outputNote.textWrappingMode = TextWrappingModes.Normal;
        y -= 54f;
        Section("Language", "Idioma");
        RowName("Language (voice and text)", "Idioma (voz e texto)");
        refs.language.a = Choice("LangPt", 290f, 166f, "Português", "Português");
        refs.language.b = Choice("LangEn", 462f, 166f, "English", "English");
        y -= 38f;
        RowName("Subtitles", "Legendas");
        refs.subtitles = Choice("Subtitles", 290f, 166f, "On", "Ligadas");

        refs.defaults = UiButton(win.transform, "Defaults", Vector2.zero, new Vector2(12f, 12f), new Vector2(210f, 34f), "Restore defaults", "Restaurar padrão", 17, false);
        ((RectTransform)refs.defaults.transform).pivot = Vector2.zero;
        texts.Add((refs.defaults.GetComponentInChildren<TMP_Text>(), "Restore defaults", "Restaurar padrão"));
        refs.ok = UiButton(win.transform, "OK", new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(120f, 34f), "OK", "OK", 17, false);
        ((RectTransform)refs.ok.transform).pivot = new Vector2(1f, 0f);
        refs.title = title;
        refs.close = close;

        var window = go.AddComponent<SettingsWindow>();
        var so = new SerializedObject(window);
        Set(so, SettingsWindow.SettingsField, settings);
        Set(so, SettingsWindow.AudioField, audio);
        Set(so, SettingsWindow.GroupField, group);
        void R(string path, Object value) => Set(so, Sub(SettingsWindow.RefsField, path), value);
        R(nameof(SettingsWindow.Refs.title), refs.title);
        R(nameof(SettingsWindow.Refs.close), refs.close);
        R(nameof(SettingsWindow.Refs.ok), refs.ok);
        R(nameof(SettingsWindow.Refs.defaults), refs.defaults);
        R("pan.a", refs.pan.a);
        R("pan.b", refs.pan.b);
        R("mouseSpeed.slider", refs.mouseSpeed.slider);
        R("mouseSpeed.value", refs.mouseSpeed.value);
        R("keyboardSpeed.slider", refs.keyboardSpeed.slider);
        R("keyboardSpeed.value", refs.keyboardSpeed.value);
        R(nameof(SettingsWindow.Refs.filter), refs.filter);
        R("filterStrength.slider", refs.filterStrength.slider);
        R("filterStrength.value", refs.filterStrength.value);
        R("volume.slider", refs.volume.slider);
        R("volume.value", refs.volume.value);
        R(nameof(SettingsWindow.Refs.microphone), refs.microphone);
        R(nameof(SettingsWindow.Refs.micLevel), refs.micLevel);
        R(nameof(SettingsWindow.Refs.micRecord), refs.micRecord);
        R(nameof(SettingsWindow.Refs.micHeard), refs.micHeard);
        R(nameof(SettingsWindow.Refs.outputTest), refs.outputTest);
        R(nameof(SettingsWindow.Refs.outputNote), refs.outputNote);
        R("language.a", refs.language.a);
        R("language.b", refs.language.b);
        R(nameof(SettingsWindow.Refs.subtitles), refs.subtitles);
        SerializedProperty lp = so.FindProperty(SettingsWindow.LabelsField);
        lp.arraySize = texts.Count;
        for (int i = 0; i < texts.Count; i++)
        {
            SerializedProperty e = lp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(nameof(SettingsWindow.Label.text)).objectReferenceValue = texts[i].text;
            e.FindPropertyRelative(nameof(SettingsWindow.Label.en)).stringValue = texts[i].en;
            e.FindPropertyRelative(nameof(SettingsWindow.Label.pt)).stringValue = texts[i].pt;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return window;
    }

    // ================================================================== the evidence panel (right side)
    internal static EvidencePanel BuildEvidencePanel(Transform canvas)
    {
        var go = EditorUi.CreateUiObject(canvas, "EvidencePanel");
        Vector2 right = new Vector2(1f, 0.5f);
        EditorUi.Place((RectTransform)go.transform, right, right, right, new Vector2(-36f, 30f), new Vector2(400f, 100f));
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        Image back = go.AddComponent<Image>();
        back.color = new Color(0.03f, 0.03f, 0.035f, 0.82f);
        back.raycastTarget = false;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 18, 14, 16);
        layout.spacing = 8f;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = EditorUi.CreateText(go.transform, "Title", 22, TextAlignmentOptions.Left, new Color(1f, 0.78f, 0.38f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
        TMP_Text body = EditorUi.CreateText(go.transform, "Body", 19, TextAlignmentOptions.TopLeft, new Color(0.92f, 0.92f, 0.9f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 6f;
        var panel = go.AddComponent<EvidencePanel>();
        EditorSerialized.SetReference(panel, EvidencePanel.GroupField, group);
        EditorSerialized.SetReference(panel, EvidencePanel.TitleField, title);
        EditorSerialized.SetReference(panel, EvidencePanel.BodyField, body);
        return panel;
    }

    // ================================================================== the keypad overlay (right side): display + 12 keys
    internal static void BuildKeypadOverlay(Transform canvas, out CanvasGroup group, out TMP_Text display, out Button[] digits, out Button clear, out Button enter)
    {
        var go = EditorUi.CreateUiObject(canvas, "KeypadOverlay");
        Vector2 right = new Vector2(1f, 0.5f);
        EditorUi.Place((RectTransform)go.transform, right, right, right, new Vector2(-60f, 0f), new Vector2(276f, 392f));
        group = go.AddComponent<CanvasGroup>();
        go.AddComponent<Image>().color = new Color(0.16f, 0.17f, 0.18f, 0.95f);
        Outline(go, 2f);
        Image screen = EditorUi.CreateUiObject(go.transform, "Display").AddComponent<Image>();
        screen.color = new Color(0.02f, 0.05f, 0.03f);
        EditorUi.Place(screen.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(240f, 56f));
        display = EditorUi.CreateText(screen.transform, "Text", 32, TextAlignmentOptions.Center, new Color(0.55f, 1f, 0.6f));
        EditorUi.Stretch(display.rectTransform);
        display.fontStyle = FontStyles.Bold;
        digits = new Button[10];
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        clear = enter = null;
        for (int i = 0; i < keys.Length; i++)
        {
            int col = i % 3, rowIndex = i / 3;
            var pos = new Vector2(18f + col * 82f, -88f - rowIndex * 74f);
            Color c = keys[i] == "OK" ? new Color(0.35f, 0.62f, 0.38f) : keys[i] == "C" ? new Color(0.7f, 0.36f, 0.3f) : new Color(0.72f, 0.73f, 0.72f);
            Button b = UiButton(go.transform, "Key" + keys[i], TopLeft, pos, new Vector2(74f, 64f), keys[i], keys[i], 28, false, c);
            ((RectTransform)b.transform).pivot = TopLeft;
            if (keys[i] == "C") clear = b;
            else if (keys[i] == "OK") enter = b;
            else digits[int.Parse(keys[i])] = b;
        }
    }

    // ================================================================== pieces
    private static string Sub(string field, string sub) => field + "." + sub;

    // Sets an object reference on a serialized property by path.
    private static void Set(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p == null) { Debug.LogError($"[Operator] Field '{path}' not found on {so.targetObject.GetType().Name}."); return; }
        p.objectReferenceValue = value;
    }

    // A full-screen panel with a CanvasGroup.
    // A picture with words in it: its English / Portuguese version follows the language.
    private static void Localize(RawImage image, Texture2D english, Texture2D portuguese, CameraSettings settings)
    {
        if (english == null) return;
        var l = image.gameObject.AddComponent<LocalizedRawImage>();
        EditorSerialized.SetReference(l, LocalizedRawImage.TargetField, image);
        EditorSerialized.SetReference(l, LocalizedRawImage.EnglishField, english);
        EditorSerialized.SetReference(l, LocalizedRawImage.PortugueseField, portuguese);
        EditorSerialized.SetReference(l, LocalizedRawImage.SettingsField, settings);
    }

    // ================================================================== CCTV.EXE starting
    // The startup splash (CctvBootSplash): its own canvas over everything (the camera program's UI too).
    internal static CctvBootSplash BuildBootSplash(Transform canvas, CameraSettings settings)
    {
        Color teal = new Color(0.47f, 0.78f, 0.8f);
        var go = EditorUi.CreateUiObject(canvas, "CctvBootSplash");
        EditorUi.Stretch((RectTransform)go.transform);
        var own = go.AddComponent<Canvas>();
        own.overrideSorting = true;
        own.sortingOrder = 200;
        go.AddComponent<GraphicRaycaster>();
        var group = go.AddComponent<CanvasGroup>();
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.01f, 0.03f, 0.04f);

        Image glow = SplashImage(go.transform, "Glow", "T_Cctv_Glow", new Vector2(0f, 90f), new Vector2(700f, 700f), teal);
        Image ring = SplashImage(go.transform, "Ring", "T_Cctv_Ring", new Vector2(0f, 90f), new Vector2(200f, 200f), teal);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        Image eyeTop = SplashImage(go.transform, "EyeTop", "T_Cctv_EyeTop", new Vector2(0f, 90f), new Vector2(200f, 200f), teal);
        Image eyeBottom = SplashImage(go.transform, "EyeBottom", "T_Cctv_EyeBottom", new Vector2(0f, 90f), new Vector2(200f, 200f), teal);
        Image pupil = SplashImage(go.transform, "Pupil", "T_Cctv_Pupil", new Vector2(0f, 90f), new Vector2(40f, 40f), teal);

        TMP_Text title = EditorUi.CreateText(go.transform, "Title", 46, TextAlignmentOptions.Center, new Color(0.6f, 0.86f, 0.88f));
        EditorUi.Place(title.rectTransform, Mid, Mid, Mid, new Vector2(0f, -55f), new Vector2(900f, 70f));
        title.text = "SENTINEL  CCTV";
        title.fontStyle = FontStyles.Bold;
        Image sweep = SplashImage(go.transform, "Sweep", "T_Cctv_Sweep", new Vector2(0f, -55f), new Vector2(160f, 80f), new Color(1f, 1f, 1f, 0.55f));
        TMP_Text tagline = EditorUi.CreateText(go.transform, "Tagline", 17, TextAlignmentOptions.Center, new Color(0.38f, 0.6f, 0.64f));
        EditorUi.Place(tagline.rectTransform, Mid, Mid, Mid, new Vector2(0f, -102f), new Vector2(900f, 30f));
        tagline.characterSpacing = 8f;

        var loaderGo = EditorUi.CreateUiObject(go.transform, "Loader");
        EditorUi.Place((RectTransform)loaderGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 60f));
        var loader = loaderGo.AddComponent<CanvasGroup>();
        var track = EditorUi.CreateUiObject(loaderGo.transform, "Track").AddComponent<Image>();
        EditorUi.Place(track.rectTransform, Mid, Mid, Mid, new Vector2(0f, 14f), new Vector2(520f, 4f));
        track.color = new Color(0.47f, 0.78f, 0.8f, 0.18f);
        track.raycastTarget = false;
        var fill = EditorUi.CreateUiObject(loaderGo.transform, "Fill").AddComponent<Image>();
        EditorUi.Place(fill.rectTransform, Mid, Mid, Mid, new Vector2(0f, 14f), new Vector2(520f, 4f));
        fill.color = teal;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;
        TMP_Text status = EditorUi.CreateText(loaderGo.transform, "Status", 14, TextAlignmentOptions.Center, new Color(0.45f, 0.65f, 0.68f));
        EditorUi.Place(status.rectTransform, Mid, Mid, Mid, new Vector2(0f, -10f), new Vector2(520f, 24f));
        TMP_Text footer = EditorUi.CreateText(go.transform, "Footer", 11, TextAlignmentOptions.Center, new Color(0.3f, 0.42f, 0.45f));
        EditorUi.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(1100f, 20f));

        var scan = EditorUi.CreateUiObject(go.transform, "Scanlines").AddComponent<RawImage>();
        EditorUi.Stretch(scan.rectTransform);
        Texture2D scanTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_Cctv_Scan.png");
        scan.texture = scanTex;
        scan.uvRect = new Rect(0f, 0f, 1280f / 4f, 720f / 4f);
        scan.color = scanTex != null ? new Color(1f, 1f, 1f, 0.5f) : Color.clear;
        scan.raycastTarget = false;
        var flash = EditorUi.CreateUiObject(go.transform, "Flash").AddComponent<Image>();
        EditorUi.Stretch(flash.rectTransform);
        flash.color = new Color(0.85f, 0.97f, 1f, 0f);
        flash.raycastTarget = false;

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.8f;

        var splash = go.AddComponent<CctvBootSplash>();
        var so = new SerializedObject(splash);
        SplashRef(so, "group", group); SplashRef(so, "glow", glow); SplashRef(so, "ring", ring);
        SplashRef(so, "eyeTop", eyeTop.rectTransform); SplashRef(so, "eyeBottom", eyeBottom.rectTransform); SplashRef(so, "pupil", pupil.rectTransform);
        SplashRef(so, "title", title); SplashRef(so, "sweep", sweep.rectTransform); SplashRef(so, "tagline", tagline);
        SplashRef(so, "loader", loader); SplashRef(so, "barFill", fill); SplashRef(so, "status", status);
        SplashRef(so, "footer", footer); SplashRef(so, "flash", flash);
        so.FindProperty(CctvBootSplash.SettingsField).objectReferenceValue = settings;
        so.FindProperty(CctvBootSplash.AudioField).objectReferenceValue = audio;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OperatorRoom/A_Cctv_Boot.wav");
        if (clip == null) Debug.LogWarning("[Operator] Assets/Audio/OperatorRoom/A_Cctv_Boot.wav missing: the splash plays silent.");
        so.FindProperty(CctvBootSplash.ClipField).objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return splash;
    }

    private static void SplashRef(SerializedObject so, string field, Object value) =>
        so.FindProperty($"{CctvBootSplash.RefsField}.{field}").objectReferenceValue = value;

    // A splash part: a white sprite (imported as a sprite, no mipmaps) tinted, centered.
    private static Image SplashImage(Transform parent, string name, string art, Vector2 position, Vector2 size, Color color)
    {
        string path = $"{OsArt}/{art}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
            (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        var img = EditorUi.CreateUiObject(parent, name).AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (img.sprite == null) Debug.LogWarning($"[Operator] {path} missing: the splash draws a plain shape there.");
        img.color = color;
        img.raycastTarget = false;
        EditorUi.Place(img.rectTransform, Mid, Mid, Mid, position, size);
        return img;
    }

    private static CanvasGroup Screen(Transform parent, string name, Color color)
    {
        var go = EditorUi.CreateUiObject(parent, name);
        EditorUi.Stretch((RectTransform)go.transform);
        go.AddComponent<Image>().color = color;
        return go.AddComponent<CanvasGroup>();
    }

    // Adds a UI outline.
    private static void Outline(GameObject go, float d)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0.2f, 0.2f, 0.2f);
        o.effectDistance = new Vector2(d, -d);
    }

    // A blue title bar on a window.
    private static Image Bar(Transform window, out TMP_Text title)
    {
        Image bar = EditorUi.CreateUiObject(window, "TitleBar").AddComponent<Image>();
        bar.color = WinTitle;
        EditorUi.Place(bar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-8f, 30f));
        title = EditorUi.CreateText(bar.transform, "Title", 18, TextAlignmentOptions.Left, Color.white);
        EditorUi.Stretch(title.rectTransform);
        title.margin = new Vector4(10f, 0f, 40f, 0f);
        return bar;
    }

    // a fixed text (both languages) at a top-left position (or the given anchor)
    private static TMP_Text Label(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, string en, string pt,
                                  Vector2? anchor = null, Vector2? at = null, TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
    {
        TMP_Text t = EditorUi.CreateText(parent, name, fontSize, align, color ?? Ink);
        Vector2 a = anchor ?? TopLeft;
        EditorUi.Place(t.rectTransform, a, a, a, at ?? pos, size);
        t.text = en;
        labels.Add((t, en, pt));
        return t;
    }

    // A button with an EN / PT label.
    private static Button UiButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string en, string pt, float fontSize,
                                   bool translated = true, Color? color = null)
    {
        var go = EditorUi.CreateUiObject(parent, name);
        EditorUi.Place((RectTransform)go.transform, anchor, anchor, anchor, pos, size);
        Image img = go.AddComponent<Image>();
        img.color = color ?? WinLight;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        Outline(go, 1f);
        TMP_Text label = EditorUi.CreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Center, Ink);
        EditorUi.Stretch(label.rectTransform);
        label.text = en;
        if (translated && (en != pt)) labels.Add((label, en, pt));
        return button;
    }

    // A text input field.
    private static TMP_InputField Input(Transform parent, string name, float fontSize, Color back, Color ink, bool password)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
        var field = go.GetComponent<TMP_InputField>();
        go.GetComponent<Image>().color = back;
        field.textComponent.fontSize = fontSize;
        field.textComponent.color = ink;
        field.pointSize = fontSize;
        if (field.placeholder is TMP_Text ph) ph.text = "";
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.restoreOriginalTextOnEscape = false;
        field.onFocusSelectAll = false;
        field.richText = false;
        if (password)
        {
            field.contentType = TMP_InputField.ContentType.Password;
            field.asteriskChar = '*';
        }
        return field;
    }

    // A desktop icon with its label.
    private static Button Icon(Transform desk, int index, string tex, string en, string pt, out TMP_Text label)
    {
        var b = EditorUi.CreateUiObject(desk, "Icon" + index);
        EditorUi.Place((RectTransform)b.transform, TopLeft, TopLeft, TopLeft, new Vector2(20f, -18f - index * 118f), new Vector2(120f, 110f));
        Image hit = b.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        var button = b.AddComponent<Button>();
        button.targetGraphic = hit;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(0.4f, 0.6f, 1f, 0.35f);
        cb.pressedColor = new Color(0.3f, 0.5f, 1f, 0.55f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        button.colors = cb;
        RawImage pic = EditorUi.CreateUiObject(b.transform, "Icon").AddComponent<RawImage>();
        pic.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/{tex}.png");
        pic.raycastTarget = false;
        Vector2 top = new Vector2(0.5f, 1f);
        EditorUi.Place(pic.rectTransform, top, top, top, new Vector2(0f, -4f), new Vector2(56f, 56f));
        label = EditorUi.CreateText(b.transform, "Label", 16, TextAlignmentOptions.Top, Color.white);
        EditorUi.Place(label.rectTransform, top, top, top, new Vector2(0f, -62f), new Vector2(120f, 46f));
        if (en != null) { label.text = en; labels.Add((label, en, pt)); }
        return button;
    }

    // A start menu entry.
    private static Button MenuEntry(Transform menu, int index, string icon, string en, string pt)
    {
        var go = EditorUi.CreateUiObject(menu, "Entry" + index);
        EditorUi.Place((RectTransform)go.transform, TopLeft, TopLeft, TopLeft, new Vector2(38f, -8f - index * 40f), new Vector2(226f, 38f));
        Image img = go.AddComponent<Image>();
        img.color = WinGray;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock cb = button.colors;
        cb.highlightedColor = new Color(0.55f, 0.65f, 0.95f);
        cb.pressedColor = new Color(0.4f, 0.5f, 0.9f);
        button.colors = cb;
        RawImage pic = EditorUi.CreateUiObject(go.transform, "Icon").AddComponent<RawImage>();
        pic.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OsArt}/T_OpOS_Icon{icon}.png");
        pic.raycastTarget = false;
        Vector2 l = new Vector2(0f, 0.5f);
        EditorUi.Place(pic.rectTransform, l, l, l, new Vector2(4f, 0f), new Vector2(30f, 30f));
        TMP_Text t = EditorUi.CreateText(go.transform, "Label", 17, TextAlignmentOptions.Left, Ink);
        EditorUi.Place(t.rectTransform, Vector2.zero, Vector2.one, Mid, new Vector2(20f, 0f), new Vector2(-40f, 0f));
        t.text = en;
        labels.Add((t, en, pt));
        return button;
    }

    // a window of the OS (OsWindow + drag on its bar); returns the body
    private static RectTransform Window(RectTransform parent, string name, Vector2 pos, Vector2 size, out OsWindow window)
    {
        var go = EditorUi.CreateUiObject(parent, name);
        EditorUi.Place((RectTransform)go.transform, Mid, Mid, Mid, pos, size);
        var group = go.AddComponent<CanvasGroup>();
        go.AddComponent<Image>().color = WinGray;
        Outline(go, 2f);
        Image bar = Bar(go.transform, out TMP_Text title);
        var drag = bar.gameObject.AddComponent<OsWindowDrag>();
        EditorSerialized.SetReference(drag, OsWindowDrag.WindowField, go.transform);
        Button close = UiButton(bar.transform, "Close", new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(28f, 22f), "X", "X", 16, false);
        var body = EditorUi.CreateUiObject(go.transform, "Body");
        var rt = (RectTransform)body.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = Mid;
        rt.offsetMin = new Vector2(6f, 6f); rt.offsetMax = new Vector2(-6f, -40f);
        window = go.AddComponent<OsWindow>();
        EditorSerialized.SetReference(window, OsWindow.GroupField, group);
        EditorSerialized.SetReference(window, OsWindow.TitleField, title);
        EditorSerialized.SetReference(window, OsWindow.CloseButtonField, close);
        return rt;
    }

    // a part of a body: anchors + offsets from them (min = bottom-left, max = top-right)
    private static RectTransform Area(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = (RectTransform)EditorUi.CreateUiObject(parent, name).transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = Mid;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    // a vertical list that scrolls; returns its content (rows go there)
    private static RectTransform ScrollList(RectTransform area, Color back, out ScrollRect scroll)
    {
        RectTransform content = Scroll(area, back, out scroll);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(3, 3, 3, 3);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    // a text that scrolls
    private static RectTransform ScrollText(RectTransform area, Color back, float size, Color ink, out TMP_Text text, out ScrollRect scroll)
    {
        RectTransform content = Scroll(area, back, out scroll);
        text = EditorUi.CreateText(content, "Text", size, TextAlignmentOptions.TopLeft, ink);
        text.margin = new Vector4(12f, 10f, 12f, 10f);
        text.textWrappingMode = TextWrappingModes.Normal;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    // A scroll view inside an area.
    private static RectTransform Scroll(RectTransform area, Color back, out ScrollRect scroll)
    {
        var go = EditorUi.CreateUiObject(area, "Scroll");
        EditorUi.Stretch((RectTransform)go.transform);
        go.AddComponent<Image>().color = back;
        scroll = go.AddComponent<ScrollRect>();
        var viewport = EditorUi.CreateUiObject(go.transform, "Viewport");
        EditorUi.Stretch((RectTransform)viewport.transform);
        viewport.AddComponent<RectMask2D>();
        var content = (RectTransform)EditorUi.CreateUiObject(viewport.transform, "Content").transform;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    // the row the explorer / mail lists copy (inactive)
    private static OperatorFileRow RowTemplate(RectTransform list)
    {
        var rowGo = EditorUi.CreateUiObject(list, "RowTemplate");
        Image img = rowGo.AddComponent<Image>();
        img.color = Color.white;
        var button = rowGo.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(0.75f, 0.85f, 1f);
        cb.pressedColor = new Color(0.55f, 0.7f, 1f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        button.colors = cb;
        rowGo.AddComponent<LayoutElement>().preferredHeight = 30f;
        TMP_Text label = EditorUi.CreateText(rowGo.transform, "Label", 17, TextAlignmentOptions.Left, Ink);
        EditorUi.Stretch(label.rectTransform);
        label.margin = new Vector4(10f, 0f, 6f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        var row = rowGo.AddComponent<OperatorFileRow>();
        EditorSerialized.SetReference(row, OperatorFileRow.ButtonField, button);
        EditorSerialized.SetReference(row, OperatorFileRow.LabelField, label);
        rowGo.SetActive(false);
        return row;
    }
}
