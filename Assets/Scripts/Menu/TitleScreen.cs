using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title menu (Title.unity, the first scene of the build): Continue (the newest save) / New game / Load (the slots)
// / Settings (the PC's Settings window) / Quit. It reads the saves once and hands the newest to TitleVariants (the
// desk changes with the story); ScreenFade covers the scene changes.
// A save is loaded the way the game loads one from another scene: the GameSession carries it, its scene opens over
// his room (GameScenes), the operator at the cameras.
// Built by Last Terminal/22 (TitleMenuUiBuilder); every reference is wired there.
public class TitleScreen : MonoBehaviour
{
    // Names of the serialized fields below: the editor builder sets them by name (SerializedObject).
    public const string SettingsField = nameof(settings);
    public const string SessionField = nameof(session);
    public const string SettingsWindowField = nameof(settingsWindow);
    public const string AudioField = nameof(audioSource);
    public const string NewGameSceneField = nameof(newGameScene);
    public const string SceneVariantsField = nameof(sceneVariants);
    public const string FadeField = nameof(fade);
    public const string RefsField = nameof(refs);

    // The menu's controls (wired by Last Terminal/22).
    [Serializable]
    public class Refs
    {
        // The main list: its group and buttons.
        public CanvasGroup main;
        public Button continueButton;
        public Button newGame;
        public Button load;
        public Button settings;
        public Button quit;
        // The slot list: its group, one button per slot (label = what is saved there) and Back.
        public CanvasGroup slots;
        public Button[] slotButtons = new Button[0];
        public Button back;
        // The line under the title (what Continue would load).
        public TMP_Text status;
    }

    // Language and options, the carrier of a loaded save to its scene, the Settings window, the click source.
    [SerializeField] private CameraSettings settings;
    [SerializeField] private GameSession session;
    [SerializeField] private SettingsWindow settingsWindow;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Scene of a new game (the first cutscene).")]
    [SerializeField] private string newGameScene = "";
    [Tooltip("Save files: number of slots and file name prefix (the same as the SaveSystem's).")]
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string filePrefix = "save_";
    [Tooltip("The desk for the newest save (its variants).")]
    [SerializeField] private TitleVariants sceneVariants;
    [Tooltip("The black overlay: fades in at the start, out before a scene loads.")]
    [SerializeField] private ScreenFade fade;
    [SerializeField] private Refs refs = new Refs();

    // The newest save (-1 = none) and its data; a scene is loading.
    private int latest = -1;
    private SaveData latestData;
    private bool leaving;

    // Language helpers.
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private string T(string en, string pt) => Pt ? pt : en;

    // Reads the saves, sets the scene for them, wires the buttons.
    private void Awake()
    {
        if (refs.main == null || refs.slots == null)
        {
            Debug.LogError($"{name}: TitleScreen is missing its menu (run Last Terminal/22).", this);
            enabled = false;
            return;
        }
        latest = SaveFiles.Latest(filePrefix, slotCount);
        if (latest >= 0) SaveFiles.TryRead(filePrefix, latest, out latestData);
        if (sceneVariants != null) sceneVariants.Apply(latestData);

        Wire(refs.continueButton, () => LoadSlot(latest));
        Wire(refs.newGame, NewGame);
        Wire(refs.load, () => ShowSlots(true));
        Wire(refs.settings, OpenSettings);
        Wire(refs.quit, Quit);
        Wire(refs.back, () => ShowSlots(false));
        for (int i = 0; i < refs.slotButtons.Length; i++)
        {
            int slot = i;
            Wire(refs.slotButtons[i], () => LoadSlot(slot));
        }
        Show(refs.slots, false);
        Show(refs.main, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Texts follow the language; the Settings window gives the menu back when it closes.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        if (settingsWindow != null) settingsWindow.Closed += OnSettingsClosed;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
        if (settingsWindow != null) settingsWindow.Closed -= OnSettingsClosed;
    }

    // First texts.
    private void Start() => Refresh();

    // ------------------------------------------------------------------ menu
    // Button texts, the slots, what Continue would load.
    private void Refresh()
    {
        SetText(refs.continueButton, T("Continue", "Continuar"));
        SetText(refs.newGame, T("New game", "Novo jogo"));
        SetText(refs.load, T("Load", "Carregar"));
        SetText(refs.settings, T("Settings", "Configurações"));
        SetText(refs.quit, T("Quit", "Sair"));
        SetText(refs.back, T("Back", "Voltar"));
        bool any = latest >= 0;
        if (refs.continueButton != null) refs.continueButton.gameObject.SetActive(any);
        if (refs.load != null) refs.load.gameObject.SetActive(any);
        if (refs.status != null)
            refs.status.text = any && latestData != null
                ? $"{Where(latestData)}  ·  {SaveSystem.FormatPlayTime(latestData.playTime)}"
                : "";
        for (int i = 0; i < refs.slotButtons.Length; i++)
        {
            Button b = refs.slotButtons[i];
            if (b == null) continue;
            bool has = SaveFiles.TryRead(filePrefix, i, out SaveData d);
            b.interactable = has;
            SetText(b, has ? $"{i + 1}.  {Where(d)}   {SaveSystem.FormatPlayTime(d.playTime)}   {When(d)}"
                           : $"{i + 1}.  {T("Empty", "Vazio")}");
        }
    }

    // Where a save was made (the save station's name, else the scene).
    private static string Where(SaveData d) => !string.IsNullOrEmpty(d.location) ? d.location : d.scene;

    // When it was saved (local date and time).
    private static string When(SaveData d) => DateTime.TryParse(d.savedAt, out DateTime t) ? t.ToString("dd/MM/yyyy HH:mm") : "";

    // Shows the slot list (or the main list).
    private void ShowSlots(bool on)
    {
        Show(refs.slots, on);
        Show(refs.main, !on);
        Refresh();
    }

    // The Settings window over the menu.
    private void OpenSettings()
    {
        if (settingsWindow == null) return;
        Show(refs.main, false);
        settingsWindow.Open();
    }

    // The window closed: the menu again.
    private void OnSettingsClosed()
    {
        if (!leaving) Show(refs.main, true);
    }

    // A new game: the first cutscene.
    private void NewGame()
    {
        if (string.IsNullOrEmpty(newGameScene) || !Application.CanStreamedLevelBeLoaded(newGameScene))
        {
            Debug.LogError($"[Title] The new game scene '{newGameScene}' is not in the Build Settings (Last Terminal/14 adds it).", this);
            return;
        }
        StartCoroutine(Leave(() => SceneManager.LoadScene(newGameScene)));
    }

    // Loads a slot: its scene opens and the GameSession hands it the save (like a save of another scene in the game).
    private void LoadSlot(int slot)
    {
        if (slot < 0 || !SaveFiles.TryRead(filePrefix, slot, out SaveData data)) return;
        if (session == null || string.IsNullOrEmpty(data.scene) || !Application.CanStreamedLevelBeLoaded(data.scene))
        {
            Debug.LogError($"[Title] Slot {slot + 1}: scene '{data.scene}' is not in the Build Settings (or no GameSession).", this);
            return;
        }
        StartCoroutine(Leave(() =>
        {
            session.SetTransfer(data, null, null, true);
            GameScenes.OpenLocation(session, data.scene, false);   // in his room; Helena waits where the save left her
        }));
    }

    // Quits (stops play mode in the editor).
    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Hides the menu, fades to black, then runs the load.
    private IEnumerator Leave(Action load)
    {
        if (leaving) yield break;
        leaving = true;
        Show(refs.main, false);
        Show(refs.slots, false);
        if (fade != null) yield return fade.Out();
        load();
    }

    // ------------------------------------------------------------------ helpers
    // A button that clicks and runs an action.
    private void Wire(Button b, Action action)
    {
        if (b == null) return;
        b.onClick.AddListener(() =>
        {
            if (leaving) return;
            OperatorSounds.Play(audioSource, OperatorSounds.Kind.Click, 0.6f);
            action();
        });
    }

    // A button's label.
    private static void SetText(Button b, string text)
    {
        if (b == null) return;
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = text;
    }

    // Shows / hides a group and its clicks.
    private static void Show(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}
