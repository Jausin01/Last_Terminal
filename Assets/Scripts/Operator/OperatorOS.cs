using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The fake OS of his PC (his own room): what the screen shows once he sits down (OperatorStation, Desktop mode).
// - Power (PcPower): off = black; pressed = BIOS lines, the logo (the jingle plays on the case), then the login.
// - Login: user OPERADOR, the password set here (empty = just Enter); a hint after a few wrong tries.
// - Desktop: Cameras (CCTV.EXE: the camera program, or "no signal" when no location is linked), My Computer
//   (file explorer: folders of OperatorFiles, text and picture viewers), E-mail (OperatorEmails, unread until opened;
//   a voice player when the sender recorded it), Recycle Bin, Settings (SettingsWindow); Start menu (the same + Run...,
//   Stand up, Shut down). Run "cmd" opens the hidden terminal.
// Files and e-mails appear with their story flags (StoryProgress). Windows drag by their title bar; Esc closes the
// top one, then gets him up. Every text follows the game language (CameraSettings).
// This component keeps the desktop (icons, Start menu, message box, status line) and coordinates the parts, plain C#
// objects made in Awake: OsBoot (power, BIOS, logo, login), OsContent (what is unlocked / read), OsFileBrowser (My
// Computer and the viewers), OsMailbox (e-mail and its voice player), OsShell (Run and the hidden terminal). The UI
// reference classes it serializes (LoginRefs, MailRefs...) are declared in OperatorOSRefs.cs.
public partial class OperatorOS : DesktopScreen
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GroupField = nameof(group);
    public const string SettingsField = nameof(settings);
    public const string StoryField = nameof(story);
    public const string AudioField = nameof(audioSource);
    public const string PowerField = nameof(power);
    public const string UserNameField = nameof(userName);
    public const string PasswordField = nameof(password);
    public const string FilesField = nameof(files);
    public const string EmailsField = nameof(emails);
    public const string TerminalEntriesField = nameof(terminalEntries);
    public const string LabelsField = nameof(labels);
    public const string ScreensField = nameof(screens);
    public const string LoginField = nameof(login);
    public const string DesktopField = nameof(desktop);
    public const string ExplorerField = nameof(explorer);
    public const string ViewersField = nameof(viewers);
    public const string MailField = nameof(mail);
    public const string TerminalField = nameof(terminal);
    public const string DialogsField = nameof(dialogs);
    public const string SettingsWindowField = nameof(settingsWindow);

    // The screen group, the settings (language), the story flags.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private StoryProgress story;
    [Tooltip("Clicks and beeps of the screen (2D).")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Optional: the PC's power button (none = always on, starts at the login).")]
    [SerializeField] private PcPower power;

    // The user name shown on the login.
    [Header("Login")]
    [SerializeField] private string userName = "OPERADOR";
    [Tooltip("Empty = just Enter.")]
    [SerializeField] private string password = "operador";
    // The password hint per language, shown after this many wrong tries.
    [SerializeField] private string passwordHintEn = "Hint: same as the user name.";
    [SerializeField] private string passwordHintPt = "Dica: igual ao nome de usuário.";
    [SerializeField] private int triesBeforeHint = 2;

    // How long the BIOS and logo stages last (s).
    [Header("Boot (seconds)")]
    [SerializeField] private float biosSeconds = 3f;
    [SerializeField] private float logoSeconds = 4.5f;

    // The content: files, e-mails and terminal commands (assets, edited freely).
    [Header("Content")]
    [SerializeField] private OperatorFile[] files = new OperatorFile[0];
    [SerializeField] private OperatorEmail[] emails = new OperatorEmail[0];
    [SerializeField] private TerminalEntry[] terminalEntries = new TerminalEntry[0];

    // The UI references (built by Last Terminal/16).
    [Header("UI")]
    [SerializeField] private LabelRef[] labels = new LabelRef[0];
    [SerializeField] private ScreenRefs screens = new ScreenRefs();
    [SerializeField] private LoginRefs login = new LoginRefs();
    [SerializeField] private DesktopRefs desktop = new DesktopRefs();
    [SerializeField] private ExplorerRefs explorer = new ExplorerRefs();
    [SerializeField] private ViewerRefs viewers = new ViewerRefs();
    [SerializeField] private MailRefs mail = new MailRefs();
    [SerializeField] private TerminalRefs terminal = new TerminalRefs();
    [SerializeField] private DialogRefs dialogs = new DialogRefs();
    [Tooltip("The game settings (the gear on the desktop).")]
    [SerializeField] private SettingsWindow settingsWindow;

    // The windows (to find the top one).
    private readonly List<OsWindow> windows = new List<OsWindow>();
    // Desktop shown, camera program available, Start menu open, status line time left.
    private bool open;
    private bool programAvailable;
    private bool startOpen;
    private float statusTimer;

    // What the parts read: settings, story flags, sounds, login / boot settings, content and the UI references (live).
    internal CameraSettings Settings => settings;
    internal StoryProgress Story => story;
    internal AudioSource Audio => audioSource;
    internal string UserName => userName;
    internal string Password => password;
    internal string PasswordHintEn => passwordHintEn;
    internal string PasswordHintPt => passwordHintPt;
    internal int TriesBeforeHint => triesBeforeHint;
    internal float BiosSeconds => biosSeconds;
    internal float LogoSeconds => logoSeconds;
    internal OperatorFile[] Files => files;
    internal OperatorEmail[] Emails => emails;
    internal TerminalEntry[] TerminalEntries => terminalEntries;
    internal ScreenRefs ScreenUi => screens;
    internal LoginRefs LoginUi => login;
    internal DesktopRefs DesktopUi => desktop;
    internal ExplorerRefs ExplorerUi => explorer;
    internal ViewerRefs ViewerUi => viewers;
    internal MailRefs MailUi => mail;
    internal TerminalRefs TerminalUi => terminal;
    internal DialogRefs DialogUi => dialogs;
    internal List<OsWindow> Windows => windows;

    // The parts (made in Awake): unlocked content and read marks, boot / login, My Computer, e-mail, Run + terminal.
    internal OsContent Content { get; private set; }
    internal OsBoot Boot { get; private set; }
    internal OsFileBrowser Browser { get; private set; }
    internal OsMailbox Mailbox { get; private set; }
    internal OsShell Shell { get; private set; }

    // The OS is showing.
    public override bool IsOpen => open;

    // Language helpers.
    internal GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;
    internal bool Pt => Language == GameLanguage.Portuguese;
    internal string T(string en, string pt) => Pt ? pt : en;

    // Makes the parts, checks the references, wires every button and starts at the first stage.
    private void Awake()
    {
        Content = new OsContent(this);
        Boot = new OsBoot(this);
        Browser = new OsFileBrowser(this);
        Mailbox = new OsMailbox(this);
        Shell = new OsShell(this);
        if (group == null || screens.desktopScreen == null || screens.loginScreen == null)
        {
            Debug.LogError($"{name}: OperatorOS is missing references (run Last Terminal/16).", this);
            enabled = false;
            return;
        }
        Wire(login.ok, Boot.TryLogin);
        Wire(login.stand, OnStand);
        if (login.passwordInput != null) login.passwordInput.onSubmit.AddListener(_ => Boot.TryLogin());

        Wire(desktop.background, CloseStart);
        Wire(desktop.cameras, OnCameras);
        Wire(desktop.computer, () => Browser.OpenExplorer(null));
        Wire(desktop.mail, Mailbox.OpenMail);
        Wire(desktop.recycle, () => Browser.OpenExplorer(OsContent.RecycleKey));
        Wire(desktop.settings, OpenSettings);
        Wire(desktop.start, ToggleStart);
        Wire(desktop.startCameras, OnCameras);
        Wire(desktop.startFiles, () => Browser.OpenExplorer(null));
        Wire(desktop.startMail, Mailbox.OpenMail);
        Wire(desktop.startRecycle, () => Browser.OpenExplorer(OsContent.RecycleKey));
        Wire(desktop.startRun, Shell.OpenRun);
        Wire(desktop.startSettings, OpenSettings);
        Wire(desktop.startStand, OnStand);
        Wire(desktop.startShutdown, OnShutdown);

        Wire(mail.attachment, Mailbox.OpenAttachment);
        if (mail.voicePlay != null) mail.voicePlay.onClick.AddListener(Mailbox.ToggleVoice);
        Wire(dialogs.runOk, Shell.SubmitRun);
        Wire(dialogs.runCancel, () => dialogs.run.Close());
        Wire(dialogs.messageOk, () => dialogs.message.Close());
        if (dialogs.runInput != null) dialogs.runInput.onSubmit.AddListener(_ => Shell.SubmitRun());
        if (terminal.input != null) terminal.input.onSubmit.AddListener(Shell.SubmitTerminal);

        foreach (OsWindow w in new[] { explorer.window, viewers.text, viewers.image, mail.window, terminal.window, dialogs.run, dialogs.message })
            if (w != null) windows.Add(w);
        if (explorer.rowTemplate != null) explorer.rowTemplate.gameObject.SetActive(false);

        Show(group, false);
        SetStartMenu(false);
        Boot.Start(power == null);
    }

    // Language / story / power events.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        if (story != null) story.FlagSet += OnFlag;
        if (power != null) { power.PoweredOn += Boot.OnPoweredOn; power.PoweredOff += Boot.OnPoweredOff; }
    }

    // Stops listening.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
        if (story != null) story.FlagSet -= OnFlag;
        if (power != null) { power.PoweredOn -= Boot.OnPoweredOn; power.PoweredOff -= Boot.OnPoweredOff; }
    }

    // A button that clicks and runs an action.
    private void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(Click);
        b.onClick.AddListener(action);
    }

    // Shows / hides the OS (he sat down / got up): hiding closes the Start menu, the voice and the settings.
    public override void SetOpen(bool value)
    {
        open = value;
        Show(group, value);
        if (!value) { SetStartMenu(false); Mailbox.StopVoice(); if (settingsWindow != null) settingsWindow.Close(); return; }
        Refresh();
        if (Boot.AtLogin) Boot.FocusPassword();
    }

    // Esc: settings window, Start menu, then the top window; false = nothing open.
    public override bool CloseTopWindow()
    {
        if (!Boot.AtDesktop) return false;
        if (settingsWindow != null && settingsWindow.IsOpen) { settingsWindow.Close(); Click(); return true; }
        if (startOpen) { SetStartMenu(false); return true; }
        OsWindow top = null;
        foreach (OsWindow w in windows)
            if (w.IsOpen && (top == null || w.transform.GetSiblingIndex() > top.transform.GetSiblingIndex())) top = w;
        if (top == null) return false;
        top.Close();
        Click();
        return true;
    }

    // A line on the taskbar for a while.
    public override void SetStatus(string text, float seconds = 4f)
    {
        if (desktop.status == null) return;
        desktop.status.text = text;
        statusTimer = seconds;
    }

    // Whether CCTV.EXE can open the camera program.
    public override void SetProgramAvailable(bool available) => programAvailable = available;

    // CCTV.EXE: the camera program, or a "no signal" box when no location is linked.
    internal void OnCameras()
    {
        CloseStart();
        if (programAvailable) { RaiseProgram(); return; }
        OperatorSounds.Play(audioSource, OperatorSounds.Kind.Denied, 0.4f);
        ShowMessage("CCTV.EXE", T("No signal from the cameras.\nNo location is linked to this terminal yet.",
                                      "Sem sinal das câmeras.\nNenhum local está conectado a este terminal ainda."));
    }

    // Stand up (desktop or Start menu).
    private void OnStand()
    {
        CloseStart();
        RaiseStand();
    }

    // Opens the settings window.
    private void OpenSettings()
    {
        CloseStart();
        if (settingsWindow != null) settingsWindow.Open();
    }

    // Shut down: the station turns the PC off and gets him up.
    private void OnShutdown()
    {
        CloseStart();
        RaiseShutdown();
    }

    // Start menu open / close.
    private void ToggleStart() => SetStartMenu(!startOpen);

    // Closes the Start menu.
    internal void CloseStart() => SetStartMenu(false);

    // Shows / hides the Start menu.
    internal void SetStartMenu(bool on)
    {
        startOpen = on;
        Show(desktop.startMenu, on);
        if (on && desktop.startMenu != null) desktop.startMenu.transform.SetAsLastSibling();
    }

    // A message box.
    internal void ShowMessage(string title, string text)
    {
        if (dialogs.message == null) return;
        if (dialogs.messageText != null) dialogs.messageText.text = text;
        dialogs.message.Open(title);
    }

    // A story flag changed: files / e-mails may have unlocked.
    private void OnFlag(string flag)
    {
        if (open) Refresh();
    }

    // Texts in the current language, unlocked content and unread counts; rebuilds open windows.
    internal void Refresh()
    {
        if (mail.voiceSource != null && mail.voiceSource.clip != null && mail.voiceSource.clip != Mailbox.MailVoice) Mailbox.StopVoice();   // the language changed
        foreach (LabelRef l in labels)
            if (l != null && l.text != null) l.text.text = Pt && !string.IsNullOrEmpty(l.pt) ? l.pt : l.en;
        Content.CollectFiles();
        int unread = Content.CollectMail();
        if (desktop.mailLabel != null)
            desktop.mailLabel.text = "E-mail" + (unread > 0 ? $"\n<color=#FFE066>({unread} {T("NEW", unread > 1 ? "NOVOS" : "NOVO")})</color>" : "");
        if (explorer.window != null && explorer.window.IsOpen) Browser.BuildExplorer();
        if (mail.window != null && mail.window.IsOpen) Mailbox.BuildMailList();
    }

    // A list row (from the template) with a label and a click action.
    internal GameObject Row(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
    {
        OperatorFileRow row = Instantiate(explorer.rowTemplate, parent);
        row.gameObject.SetActive(true);
        if (row.Label != null) row.Label.text = label;
        if (row.Button != null) row.Button.onClick.AddListener(action);
        return row.gameObject;
    }

    // Destroys the rows of a list.
    internal static void Clear(List<GameObject> rows)
    {
        foreach (GameObject r in rows) if (r != null) Destroy(r);
        rows.Clear();
    }

    // Click sound of the screen.
    internal void Click() => OperatorSounds.Play(audioSource, OperatorSounds.Kind.Click, 0.6f);

    // Shows / hides a CanvasGroup and its clicks.
    internal static void Show(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    // Boot animation, clock, voice bar progress, status line timeout.
    private void Update()
    {
        if (Boot.IsBooting) Boot.UpdateBoot();
        if (!open) return;
        if (desktop.clock != null) desktop.clock.text = DateTime.Now.ToString("HH:mm");
        if (mail.voiceSource != null && mail.voiceSource.clip != null)
        {
            if (mail.window != null && !mail.window.IsOpen) Mailbox.StopVoice();   // the mail closed: the voice stops
            else Mailbox.ShowVoice();
        }
        if (desktop.status != null && statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f) desktop.status.text = "";
        }
    }
}
