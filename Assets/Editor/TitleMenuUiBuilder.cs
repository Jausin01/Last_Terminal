using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The title screen's UI, for Last Terminal/22 (TitleSceneBuilder): the canvas, a soft dark backdrop on the right, the
// logo, the menu (TitleScreen) and its slot list, the PC's Settings window, the fade (ScreenFade). Wires TitleScreen.
public static class TitleMenuUiBuilder
{
    // Menu colors: warm paper white, a dimmer line.
    private static readonly Color Ink = new Color(0.93f, 0.88f, 0.78f);
    private static readonly Color InkDim = new Color(0.74f, 0.69f, 0.6f);
    // The backdrop: width (from the right edge, wider than the title) and darkness at its right side.
    private const float BackdropWidth = 1180f;
    private const float BackdropAlpha = 0.62f;
    private const string BackdropTexture = TitleSceneBuilder.ArtFolder + "/T_MenuBand.png";
    // The game's logo (title + cursor + waveform, transparent): top right, this wide (its height follows the image).
    private const string LogoTexture = "Assets/Art/UI/Logo/T_Logo_LastTerminal.png";
    private const float LogoWidth = 900f;

    // The UI over the scene; returns the menu (wired).
    public static TitleScreen Build(Transform root, CameraSettings settings, AudioSource uiAudio, TitleVariants variants)
    {
        Transform canvas = BuildCanvas();
        BuildBackdrop(canvas);
        TMP_Text status = BuildTitle(canvas);

        var refs = new TitleScreen.Refs { status = status };
        refs.main = Group(canvas, "Main");
        refs.continueButton = MenuButton(refs.main.transform, "Continue");
        refs.newGame = MenuButton(refs.main.transform, "NewGame");
        refs.load = MenuButton(refs.main.transform, "Load");
        refs.settings = MenuButton(refs.main.transform, "Settings");
        refs.quit = MenuButton(refs.main.transform, "Quit");
        refs.slots = Group(canvas, "Slots");
        refs.slotButtons = new[] { MenuButton(refs.slots.transform, "Slot1", 30f), MenuButton(refs.slots.transform, "Slot2", 30f), MenuButton(refs.slots.transform, "Slot3", 30f) };
        refs.back = MenuButton(refs.slots.transform, "Back");

        SettingsWindow window = OperatorRoomUiBuilder.BuildSettingsWindow(canvas, settings, uiAudio);
        ScreenFade fade = BuildFade(canvas);

        var title = new GameObject("TitleScreen").AddComponent<TitleScreen>();
        title.transform.SetParent(root, false);
        Wire(title, refs, settings, window, uiAudio, variants, fade);
        return title;
    }

    // ------------------------------------------------------------------ canvas
    // A 1920x1080 overlay canvas and the event system (new Input System).
    private static Transform BuildCanvas()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
        return canvasGo.transform;
    }

    // A black gradient on the right (clear at its left side, no hard edge), wider than the title, so the text reads
    // over the window and the rain; the photo on the left stays clear.
    private static void BuildBackdrop(Transform canvas)
    {
        UiTexture(BackdropTexture);
        RawImage band = EditorUi.CreateUiObject(canvas, "Backdrop").AddComponent<RawImage>();
        band.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropTexture);
        band.color = new Color(1f, 1f, 1f, BackdropAlpha);
        band.raycastTarget = false;
        if (band.texture == null) Debug.LogWarning($"[Title] {BackdropTexture} not found: the backdrop is a flat tint.");
        EditorUi.Place(band.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(BackdropWidth, 0f));
    }

    // The logo top right (the text title if the image is missing) and the line under it (what Continue would load);
    // returns that line.
    private static TMP_Text BuildTitle(Transform canvas)
    {
        Vector2 tr = new Vector2(1f, 1f);
        UiTexture(LogoTexture);
        var logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoTexture);
        if (logoTex != null)
        {
            RawImage logo = EditorUi.CreateUiObject(canvas, "Logo").AddComponent<RawImage>();
            logo.texture = logoTex;
            logo.raycastTarget = false;
            EditorUi.Place(logo.rectTransform, tr, tr, tr, new Vector2(-100f, -110f), new Vector2(LogoWidth, LogoWidth * logoTex.height / logoTex.width));
        }
        else
        {
            Debug.LogWarning($"[Title] {LogoTexture} not found: the title is plain text.");
            TMP_Text titleText = EditorUi.CreateText(canvas, "Title", 92, TextAlignmentOptions.Right, Ink);
            EditorUi.Place(titleText.rectTransform, tr, tr, tr, new Vector2(-110f, -150f), new Vector2(900f, 120f));
            titleText.text = "LAST TERMINAL";
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 8f;
        }
        TMP_Text status = EditorUi.CreateText(canvas, "Status", 26, TextAlignmentOptions.Right, InkDim);
        EditorUi.Place(status.rectTransform, tr, tr, tr, new Vector2(-116f, -305f), new Vector2(900f, 40f));
        return status;
    }

    // A texture shown as is on the UI: full size, no mipmaps, clamped edges (reimported only when it changes).
    private static void UiTexture(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        if (ti.wrapMode == TextureWrapMode.Clamp && !ti.mipmapEnabled && ti.npotScale == TextureImporterNPOTScale.None && ti.alphaIsTransparency) return;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }

    // The black overlay over everything (fades the scene in, out before a load).
    private static ScreenFade BuildFade(Transform canvas)
    {
        var fadeGo = EditorUi.CreateUiObject(canvas, "Fade");
        EditorUi.Stretch((RectTransform)fadeGo.transform);
        Image black = fadeGo.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        fadeGo.AddComponent<CanvasGroup>();
        return fadeGo.AddComponent<ScreenFade>();
    }

    // ------------------------------------------------------------------ lists
    // One list of buttons under the title: a group (shown / hidden by TitleScreen) stacking its buttons from the top,
    // so a hidden button (Continue without a save) leaves no gap.
    private static CanvasGroup Group(Transform canvas, string objectName)
    {
        var go = EditorUi.CreateUiObject(canvas, objectName);
        Vector2 tr = new Vector2(1f, 1f);
        EditorUi.Place((RectTransform)go.transform, tr, tr, tr, new Vector2(-96f, -400f), new Vector2(820f, 520f));
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return go.AddComponent<CanvasGroup>();
    }

    // A text button of a list (stacked by the group's layout), a faint highlight on hover / selection.
    private static Button MenuButton(Transform parent, string objectName, float fontSize = 40f)
    {
        var go = EditorUi.CreateUiObject(parent, objectName);
        var size = go.AddComponent<LayoutElement>();
        size.preferredWidth = fontSize > 35f ? 520f : 820f;
        size.preferredHeight = 62f;
        Image img = go.AddComponent<Image>();
        img.color = Color.white;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 0.9f, 0.75f, 0.12f);
        cb.selectedColor = new Color(1f, 0.9f, 0.75f, 0.12f);
        cb.pressedColor = new Color(1f, 0.9f, 0.75f, 0.22f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        b.colors = cb;
        TMP_Text label = EditorUi.CreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Right, Ink);
        EditorUi.Stretch(label.rectTransform);
        label.margin = new Vector4(0f, 0f, 20f, 0f);
        label.text = objectName;
        return b;
    }

    // ------------------------------------------------------------------ wiring
    // TitleScreen's references, by field name.
    private static void Wire(TitleScreen title, TitleScreen.Refs refs, CameraSettings settings, SettingsWindow window,
                             AudioSource uiAudio, TitleVariants variants, ScreenFade fade)
    {
        var so = new SerializedObject(title);
        so.FindProperty(TitleScreen.SettingsField).objectReferenceValue = settings;
        so.FindProperty(TitleScreen.SessionField).objectReferenceValue = ElevatorBuilder.EnsureSession();
        so.FindProperty(TitleScreen.SettingsWindowField).objectReferenceValue = window;
        so.FindProperty(TitleScreen.AudioField).objectReferenceValue = uiAudio;
        so.FindProperty(TitleScreen.NewGameSceneField).stringValue = System.IO.Path.GetFileNameWithoutExtension(InitiationBuilder.ScenePath);
        so.FindProperty(TitleScreen.SceneVariantsField).objectReferenceValue = variants;
        so.FindProperty(TitleScreen.FadeField).objectReferenceValue = fade;
        SerializedProperty r = so.FindProperty(TitleScreen.RefsField);
        void R(string field, Object value) => r.FindPropertyRelative(field).objectReferenceValue = value;
        R(nameof(TitleScreen.Refs.main), refs.main);
        R(nameof(TitleScreen.Refs.continueButton), refs.continueButton);
        R(nameof(TitleScreen.Refs.newGame), refs.newGame);
        R(nameof(TitleScreen.Refs.load), refs.load);
        R(nameof(TitleScreen.Refs.settings), refs.settings);
        R(nameof(TitleScreen.Refs.quit), refs.quit);
        R(nameof(TitleScreen.Refs.slots), refs.slots);
        R(nameof(TitleScreen.Refs.back), refs.back);
        R(nameof(TitleScreen.Refs.status), refs.status);
        SerializedProperty slots = r.FindPropertyRelative(nameof(TitleScreen.Refs.slotButtons));
        slots.arraySize = refs.slotButtons.Length;
        for (int i = 0; i < refs.slotButtons.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = refs.slotButtons[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
