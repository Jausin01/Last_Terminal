using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The fake OS of his PC (his own room): what the screen shows once he sits down (OperatorStation, Desktop mode).
// - Power (PcPower): off = black; pressed = BIOS lines, the logo (the jingle plays on the case), then the login.
// - Login: user OPERADOR, the password set here (empty = just Enter); a hint after a few wrong tries.
// - Desktop: Cameras (CCTV.EXE: the camera program, or "no signal" when no location is linked), My Computer
//   (file explorer: folders of OperatorFiles, text and picture viewers), E-mail (OperatorEmails, unread until opened),
//   Recycle Bin; Start menu (the same + Run..., Stand up, Shut down). Run "cmd" opens the hidden terminal.
// Files and e-mails appear with their story flags (StoryProgress). Windows drag by their title bar; Esc closes the
// top one, then gets him up. Every text follows the game language (CameraSettings).
public class OperatorOS : DesktopScreen
{
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

    // a fixed text of the UI in both languages (icon names, menu entries, buttons)
    [Serializable]
    public class LabelRef
    {
        public TMP_Text text;
        public string en;
        public string pt;
    }

    // a hidden command of the terminal ("cmd" from Start > Run). Several names: "door|porta".
    [Serializable]
    public class TerminalEntry
    {
        public string command = "";
        [TextArea(2, 10)] public string responseEn = "";
        [TextArea(2, 10)] public string responsePt = "";
        [Tooltip("Optional StoryProgress flag set when typed.")]
        public string setFlag = "";
    }

    [Serializable]
    public class ScreenRefs
    {
        public CanvasGroup off;
        public CanvasGroup bios;
        public TMP_Text biosText;
        public CanvasGroup logo;
        public CanvasGroup loginScreen;
        public CanvasGroup desktopScreen;
    }

    [Serializable]
    public class LoginRefs
    {
        public TMP_Text user;
        public TMP_InputField passwordInput;
        public Button ok;
        public Button stand;
        public TMP_Text message;
    }

    [Serializable]
    public class DesktopRefs
    {
        public Button background;
        public Button cameras;
        public Button computer;
        public Button mail;
        public TMP_Text mailLabel;
        public Button recycle;
        public Button start;
        public CanvasGroup startMenu;
        public Button startCameras;
        public Button startFiles;
        public Button startMail;
        public Button startRecycle;
        public Button startRun;
        public Button startStand;
        public Button startShutdown;
        public TMP_Text clock;
        public TMP_Text status;
    }

    [Serializable]
    public class ExplorerRefs
    {
        public OsWindow window;
        public RectTransform folders;
        public RectTransform list;
        public OperatorFileRow rowTemplate;
        public TMP_Text path;
        public TMP_Text empty;
    }

    [Serializable]
    public class ViewerRefs
    {
        public OsWindow text;
        public TMP_Text textBody;
        public ScrollRect textScroll;
        public OsWindow image;
        public RawImage picture;
        public AspectRatioFitter pictureFitter;
        public TMP_Text caption;
    }

    [Serializable]
    public class MailRefs
    {
        public OsWindow window;
        public RectTransform list;
        public TMP_Text header;
        public TMP_Text body;
        public ScrollRect bodyScroll;
        public Button attachment;
        public TMP_Text attachmentLabel;
    }

    [Serializable]
    public class TerminalRefs
    {
        public OsWindow window;
        public TMP_Text output;
        public ScrollRect scroll;
        public TMP_InputField input;
    }

    [Serializable]
    public class DialogRefs
    {
        public OsWindow run;
        public TMP_InputField runInput;
        public Button runOk;
        public Button runCancel;
        public OsWindow message;
        public TMP_Text messageText;
        public Button messageOk;
    }

    private enum Stage { Off, Bios, Logo, Login, Desktop }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private StoryProgress story;
    [Tooltip("Clicks and beeps of the screen (2D).")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Optional: the PC's power button (none = always on, starts at the login).")]
    [SerializeField] private PcPower power;

    [Header("Login")]
    [SerializeField] private string userName = "OPERADOR";
    [Tooltip("Empty = just Enter.")]
    [SerializeField] private string password = "operador";
    [SerializeField] private string passwordHintEn = "Hint: same as the user name.";
    [SerializeField] private string passwordHintPt = "Dica: igual ao nome de usuário.";
    [SerializeField] private int triesBeforeHint = 2;

    [Header("Boot (seconds)")]
    [SerializeField] private float biosSeconds = 3f;
    [SerializeField] private float logoSeconds = 4.5f;

    [Header("Content")]
    [SerializeField] private OperatorFile[] files = new OperatorFile[0];
    [SerializeField] private OperatorEmail[] emails = new OperatorEmail[0];
    [SerializeField] private TerminalEntry[] terminalEntries = new TerminalEntry[0];

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

    private const string RecycleKey = "\u0001recycle";
    private const int TerminalMaxChars = 6000;

    private readonly List<OsWindow> windows = new List<OsWindow>();
    private readonly List<GameObject> folderRows = new List<GameObject>();
    private readonly List<GameObject> fileRows = new List<GameObject>();
    private readonly List<GameObject> mailRows = new List<GameObject>();
    private readonly List<OperatorFile> unlockedFiles = new List<OperatorFile>();
    private readonly List<OperatorEmail> unlockedMail = new List<OperatorEmail>();
    private readonly HashSet<string> readLocal = new HashSet<string>();
    private readonly StringBuilder terminalText = new StringBuilder();

    private Stage stage = Stage.Off;
    private float stageTimer;
    private int biosShown;
    private bool open;
    private bool programAvailable;
    private bool startOpen;
    private int wrongTries;
    private float statusTimer;
    private string folder;
    private OperatorEmail mailOpen;

    public override bool IsOpen => open;

    private GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;
    private bool Pt => Language == GameLanguage.Portuguese;
    private string T(string en, string pt) => Pt ? pt : en;
    private string Prompt => $"C:\\{userName.ToUpperInvariant()}>";

    // ------------------------------------------------------------------ setup
    private void Awake()
    {
        if (group == null || screens.desktopScreen == null || screens.loginScreen == null)
        {
            Debug.LogError($"{name}: OperatorOS is missing references (run Last Terminal/16).", this);
            enabled = false;
            return;
        }
        Wire(login.ok, TryLogin);
        Wire(login.stand, OnStand);
        if (login.passwordInput != null) login.passwordInput.onSubmit.AddListener(_ => TryLogin());

        Wire(desktop.background, CloseStart);
        Wire(desktop.cameras, OnCameras);
        Wire(desktop.computer, () => OpenExplorer(null));
        Wire(desktop.mail, OpenMail);
        Wire(desktop.recycle, () => OpenExplorer(RecycleKey));
        Wire(desktop.start, ToggleStart);
        Wire(desktop.startCameras, OnCameras);
        Wire(desktop.startFiles, () => OpenExplorer(null));
        Wire(desktop.startMail, OpenMail);
        Wire(desktop.startRecycle, () => OpenExplorer(RecycleKey));
        Wire(desktop.startRun, OpenRun);
        Wire(desktop.startStand, OnStand);
        Wire(desktop.startShutdown, OnShutdown);

        Wire(mail.attachment, OpenAttachment);
        Wire(dialogs.runOk, SubmitRun);
        Wire(dialogs.runCancel, () => dialogs.run.Close());
        Wire(dialogs.messageOk, () => dialogs.message.Close());
        if (dialogs.runInput != null) dialogs.runInput.onSubmit.AddListener(_ => SubmitRun());
        if (terminal.input != null) terminal.input.onSubmit.AddListener(SubmitTerminal);

        foreach (OsWindow w in new[] { explorer.window, viewers.text, viewers.image, mail.window, terminal.window, dialogs.run, dialogs.message })
            if (w != null) windows.Add(w);
        if (explorer.rowTemplate != null) explorer.rowTemplate.gameObject.SetActive(false);

        Show(group, false);
        SetStartMenu(false);
        SetStage(power == null ? Stage.Login : Stage.Off);
    }

    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        if (story != null) story.FlagSet += OnFlag;
        if (power != null) { power.PoweredOn += OnPoweredOn; power.PoweredOff += OnPoweredOff; }
    }

    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
        if (story != null) story.FlagSet -= OnFlag;
        if (power != null) { power.PoweredOn -= OnPoweredOn; power.PoweredOff -= OnPoweredOff; }
    }

    private void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(Click);
        b.onClick.AddListener(action);
    }

    // ------------------------------------------------------------------ DesktopScreen
    public override void SetOpen(bool value)
    {
        open = value;
        Show(group, value);
        if (!value) { SetStartMenu(false); return; }
        Refresh();
        if (stage == Stage.Login) FocusPassword();
    }

    public override bool CloseTopWindow()
    {
        if (stage != Stage.Desktop) return false;
        if (startOpen) { SetStartMenu(false); return true; }
        OsWindow top = null;
        foreach (OsWindow w in windows)
            if (w.IsOpen && (top == null || w.transform.GetSiblingIndex() > top.transform.GetSiblingIndex())) top = w;
        if (top == null) return false;
        top.Close();
        Click();
        return true;
    }

    public override void SetStatus(string text, float seconds = 4f)
    {
        if (desktop.status == null) return;
        desktop.status.text = text;
        statusTimer = seconds;
    }

    public override void SetProgramAvailable(bool available) => programAvailable = available;

    // ------------------------------------------------------------------ power / boot / login
    private void OnPoweredOn()
    {
        wrongTries = 0;
        SetStage(Stage.Bios);
    }

    private void OnPoweredOff()
    {
        foreach (OsWindow w in windows) w.Hide();
        SetStartMenu(false);
        SetStage(Stage.Off);
    }

    private void SetStage(Stage next)
    {
        stage = next;
        stageTimer = 0f;
        biosShown = 0;
        Show(screens.off, next == Stage.Off);
        Show(screens.bios, next == Stage.Bios);
        Show(screens.logo, next == Stage.Logo);
        Show(screens.loginScreen, next == Stage.Login);
        Show(screens.desktopScreen, next == Stage.Desktop);
        if (screens.logo != null && next == Stage.Logo) screens.logo.alpha = 0f;
        if (screens.biosText != null && next == Stage.Bios) screens.biosText.text = "";
        if (next == Stage.Login)
        {
            if (login.user != null) login.user.text = userName;
            if (login.message != null) login.message.text = "";
            if (login.passwordInput != null) login.passwordInput.text = "";
            if (open) FocusPassword();
        }
        if (next == Stage.Desktop) Refresh();
    }

    private string[] BiosLines => new[]
    {
        "TERMINAL BIOS v2.07   (C) 1998-2004",
        "CPU: x86 733 MHz",
        T("Memory test: 262144K OK", "Teste de memória: 262144K OK"),
        T("Detecting drives... C: 40 GB", "Detectando discos... C: 40 GB"),
        T("Network: LINKED (1 remote node)", "Rede: CONECTADA (1 nó remoto)"),
        T("Camera bus: waiting for signal...", "Barramento de câmeras: aguardando sinal..."),
        "",
        T("Starting TERMINAL OS...", "Iniciando TERMINAL OS..."),
    };

    private void UpdateBoot()
    {
        stageTimer += Time.unscaledDeltaTime;
        if (stage == Stage.Bios)
        {
            string[] lines = BiosLines;
            int want = Mathf.Min(lines.Length, Mathf.FloorToInt(stageTimer / Mathf.Max(0.1f, biosSeconds) * (lines.Length + 1)));
            if (want != biosShown && screens.biosText != null)
            {
                biosShown = want;
                screens.biosText.text = string.Join("\n", lines, 0, want) + (want < lines.Length ? "\n_" : "");
            }
            if (stageTimer >= biosSeconds) SetStage(Stage.Logo);
        }
        else if (stage == Stage.Logo)
        {
            if (screens.logo != null)
            {
                float a = Mathf.Clamp01(stageTimer / 0.8f) * Mathf.Clamp01((logoSeconds - stageTimer) / 0.6f);
                screens.logo.alpha = a;
            }
            if (stageTimer >= logoSeconds) SetStage(Stage.Login);
        }
    }

    private void FocusPassword()
    {
        if (login.passwordInput == null) return;
        login.passwordInput.Select();
        login.passwordInput.ActivateInputField();
    }

    private void TryLogin()
    {
        if (stage != Stage.Login) return;
        string typed = login.passwordInput != null ? login.passwordInput.text : "";
        if (string.IsNullOrEmpty(password) || string.Equals(typed.Trim(), password, StringComparison.OrdinalIgnoreCase))
        {
            OperatorSounds.Play(audioSource, OperatorSounds.Kind.Accepted, 0.5f);
            if (story != null) story.Set("pc_login");
            SetStage(Stage.Desktop);
            return;
        }
        wrongTries++;
        OperatorSounds.Play(audioSource, OperatorSounds.Kind.Denied, 0.5f);
        if (login.message != null)
        {
            string msg = T("Wrong password.", "Senha incorreta.");
            if (wrongTries >= triesBeforeHint) msg += "\n<size=80%>" + T(passwordHintEn, passwordHintPt) + "</size>";
            login.message.text = msg;
        }
        if (login.passwordInput != null) login.passwordInput.text = "";
        FocusPassword();
    }

    // ------------------------------------------------------------------ desktop
    private void OnCameras()
    {
        CloseStart();
        if (programAvailable) { RaiseProgram(); return; }
        OperatorSounds.Play(audioSource, OperatorSounds.Kind.Denied, 0.4f);
        ShowMessage("CCTV.EXE", T("No signal from the cameras.\nNo location is linked to this terminal yet.",
                                      "Sem sinal das câmeras.\nNenhum local está conectado a este terminal ainda."));
    }

    private void OnStand()
    {
        CloseStart();
        RaiseStand();
    }

    private void OnShutdown()
    {
        CloseStart();
        RaiseShutdown();
    }

    private void ToggleStart() => SetStartMenu(!startOpen);

    private void CloseStart() => SetStartMenu(false);

    private void SetStartMenu(bool on)
    {
        startOpen = on;
        Show(desktop.startMenu, on);
        if (on && desktop.startMenu != null) desktop.startMenu.transform.SetAsLastSibling();
    }

    private void ShowMessage(string title, string text)
    {
        if (dialogs.message == null) return;
        if (dialogs.messageText != null) dialogs.messageText.text = text;
        dialogs.message.Open(title);
    }

    private void OnFlag(string flag)
    {
        if (open) Refresh();
    }

    private void Refresh()
    {
        foreach (LabelRef l in labels)
            if (l != null && l.text != null) l.text.text = Pt && !string.IsNullOrEmpty(l.pt) ? l.pt : l.en;
        CollectFiles();
        int unread = CollectMail();
        if (desktop.mailLabel != null)
            desktop.mailLabel.text = "E-mail" + (unread > 0 ? $"\n<color=#FFE066>({unread} {T("NEW", unread > 1 ? "NOVOS" : "NOVO")})</color>" : "");
        if (explorer.window != null && explorer.window.IsOpen) BuildExplorer();
        if (mail.window != null && mail.window.IsOpen) BuildMailList();
    }

    // ------------------------------------------------------------------ files
    private bool Unlocked(string flag) => story != null ? story.Has(flag) : string.IsNullOrEmpty(flag);

    private bool IsRead(string key) => story != null ? story.Has(key) : readLocal.Contains(key);

    private void MarkRead(string key)
    {
        if (IsRead(key)) return;
        if (story != null) story.Set(key);   // refreshes (FlagSet)
        else { readLocal.Add(key); Refresh(); }
    }

    private static string FileKey(OperatorFile f) => StoryProgress.ReadPrefix + f.Id;

    private void CollectFiles()
    {
        unlockedFiles.Clear();
        foreach (OperatorFile f in files)
            if (f != null && Unlocked(f.RequiredFlag)) unlockedFiles.Add(f);
        unlockedFiles.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    // the folders in order of their first file, then the Recycle Bin
    private List<string> Folders()
    {
        var list = new List<string>();
        foreach (OperatorFile f in unlockedFiles)
        {
            if (f.InRecycleBin) continue;
            string name = f.Folder(Language);
            if (!list.Contains(name)) list.Add(name);
        }
        list.Add(RecycleKey);
        return list;
    }

    private string FolderTitle(string key) => key == RecycleKey ? T("Recycle Bin", "Lixeira") : key;

    private void OpenExplorer(string startFolder)
    {
        CloseStart();
        CollectFiles();
        List<string> folders = Folders();
        folder = startFolder ?? (folders.Count > 0 ? folders[0] : RecycleKey);
        if (explorer.window != null) explorer.window.Open(T("My Computer", "Meu Computador"));
        BuildExplorer();
    }

    private void BuildExplorer()
    {
        if (explorer.rowTemplate == null) return;
        List<string> folders = Folders();
        if (folder == null || !folders.Contains(folder)) folder = folders[0];
        Clear(folderRows);
        foreach (string key in folders)
        {
            string k = key;
            bool current = key == folder;
            string label = (current ? "<b>> " : "  ") + FolderTitle(key) + (current ? "</b>" : "");
            folderRows.Add(Row(explorer.folders, label, () => { Click(); folder = k; BuildExplorer(); }));
        }
        Clear(fileRows);
        int count = 0;
        foreach (OperatorFile f in unlockedFiles)
        {
            bool inHere = folder == RecycleKey ? f.InRecycleBin : !f.InRecycleBin && f.Folder(Language) == folder;
            if (!inHere) continue;
            OperatorFile file = f;
            string tag = f.FileKind == OperatorFile.Kind.Image ? "[IMG] " : "[TXT] ";
            string label = $"<color=#555555>{tag}</color>" + f.FileName(Language) + (IsRead(FileKey(f)) ? "" : $"   <color=#C08A00>{T("NEW", "NOVO")}</color>");
            fileRows.Add(Row(explorer.list, label, () => { Click(); OpenFile(file); }));
            count++;
        }
        if (explorer.path != null)
            explorer.path.text = folder == RecycleKey ? "C:\\" + T("RECYCLED", "LIXEIRA") : $"C:\\{userName.ToUpperInvariant()}\\{folder.ToUpperInvariant()}";
        if (explorer.empty != null)
            explorer.empty.text = count == 0 ? T("(empty)", "(vazia)") : "";
    }

    private GameObject Row(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
    {
        OperatorFileRow row = Instantiate(explorer.rowTemplate, parent);
        row.gameObject.SetActive(true);
        if (row.Label != null) row.Label.text = label;
        if (row.Button != null) row.Button.onClick.AddListener(action);
        return row.gameObject;
    }

    private static void Clear(List<GameObject> rows)
    {
        foreach (GameObject r in rows) if (r != null) Destroy(r);
        rows.Clear();
    }

    private void OpenFile(OperatorFile f)
    {
        if (f.FileKind == OperatorFile.Kind.Image && viewers.image != null)
        {
            if (viewers.picture != null)
            {
                viewers.picture.texture = f.Image;
                viewers.picture.color = f.Image != null ? Color.white : Color.black;
            }
            if (viewers.pictureFitter != null && f.Image != null)
                viewers.pictureFitter.aspectRatio = f.Image.width / (float)Mathf.Max(1, f.Image.height);
            if (viewers.caption != null) viewers.caption.text = f.Body(Language);
            viewers.image.Open(f.FileName(Language));
        }
        else if (viewers.text != null)
        {
            string title = f.Title(Language);
            if (viewers.textBody != null) viewers.textBody.text = (string.IsNullOrEmpty(title) ? "" : $"<b>{title}</b>\n\n") + f.Body(Language);
            viewers.text.Open(f.FileName(Language) + " - " + T("Notepad", "Bloco de Notas"));
            if (viewers.textScroll != null) viewers.textScroll.verticalNormalizedPosition = 1f;
        }
        MarkRead(FileKey(f));
    }

    // ------------------------------------------------------------------ e-mail
    private int CollectMail()
    {
        unlockedMail.Clear();
        int unread = 0;
        foreach (OperatorEmail m in emails)
        {
            if (m == null || !Unlocked(m.RequiredFlag)) continue;
            unlockedMail.Add(m);
            if (!IsRead(OperatorEmail.ReadPrefix + m.Id)) unread++;
        }
        unlockedMail.Sort((a, b) => b.Order.CompareTo(a.Order));   // newest first
        return unread;
    }

    private void OpenMail()
    {
        CloseStart();
        CollectMail();
        if (mail.window != null) mail.window.Open(T("E-mail - Inbox", "E-mail - Caixa de entrada"));
        mailOpen = null;
        ShowMailBody(null);
        BuildMailList();
    }

    private void BuildMailList()
    {
        if (explorer.rowTemplate == null || mail.list == null) return;
        Clear(mailRows);
        foreach (OperatorEmail m in unlockedMail)
        {
            OperatorEmail e = m;
            bool unread = !IsRead(OperatorEmail.ReadPrefix + m.Id);
            string label = (unread ? $"<b><color=#C08A00>{T("NEW", "NOVO")}</color> " : "") + m.FromName + "  |  " + m.Subject(Language) + (unread ? "</b>" : "");
            if (m == mailOpen) label = "<u>" + label + "</u>";
            mailRows.Add(Row(mail.list, label, () => { Click(); ReadMail(e); }));
        }
        if (unlockedMail.Count == 0) ShowMailBody(null);
    }

    private void ReadMail(OperatorEmail m)
    {
        mailOpen = m;
        ShowMailBody(m);
        MarkRead(OperatorEmail.ReadPrefix + m.Id);   // rebuilds the list (FlagSet)
        BuildMailList();
    }

    private void ShowMailBody(OperatorEmail m)
    {
        if (mail.header != null)
            mail.header.text = m == null ? (unlockedMail.Count == 0 ? T("No messages.", "Nenhuma mensagem.") : T("Select a message.", "Selecione uma mensagem."))
                : $"<b>{T("From", "De")}:</b> {m.FromName} <noparse><{m.FromAddress}></noparse>\n<b>{T("Subject", "Assunto")}:</b> {m.Subject(Language)}\n<b>{T("Date", "Data")}:</b> {m.Date}";
        if (mail.body != null) mail.body.text = m != null ? m.Body(Language) : "";
        if (mail.bodyScroll != null) mail.bodyScroll.verticalNormalizedPosition = 1f;
        bool attached = m != null && m.Attachment != null;
        if (mail.attachment != null) mail.attachment.gameObject.SetActive(attached);
        if (attached && mail.attachmentLabel != null) mail.attachmentLabel.text = T("Attachment: ", "Anexo: ") + m.Attachment.FileName(Language);
    }

    private void OpenAttachment()
    {
        if (mailOpen != null && mailOpen.Attachment != null) OpenFile(mailOpen.Attachment);
    }

    // ------------------------------------------------------------------ run + terminal
    private void OpenRun()
    {
        CloseStart();
        if (dialogs.run == null) return;
        dialogs.run.Open(T("Run", "Executar"));
        if (dialogs.runInput != null)
        {
            dialogs.runInput.text = "";
            dialogs.runInput.Select();
            dialogs.runInput.ActivateInputField();
        }
    }

    private void SubmitRun()
    {
        if (dialogs.run == null || !dialogs.run.IsOpen) return;
        string what = dialogs.runInput != null ? dialogs.runInput.text.Trim().ToLowerInvariant() : "";
        if (what.EndsWith(".exe")) what = what.Substring(0, what.Length - 4);
        dialogs.run.Close();
        switch (what)
        {
            case "": return;
            case "cmd": case "terminal": case "command": case "prompt":
                OpenTerminal();
                break;
            case "explorer": case "explorador":
                OpenExplorer(null);
                break;
            case "mail": case "email": case "e-mail":
                OpenMail();
                break;
            case "cctv":
                OnCameras();
                break;
            default:
                OperatorSounds.Play(audioSource, OperatorSounds.Kind.Denied, 0.4f);
                ShowMessage(T("Run", "Executar"), T($"Cannot find '{what}'.", $"Não foi possível encontrar '{what}'."));
                break;
        }
    }

    private void OpenTerminal()
    {
        if (terminal.window == null) return;
        if (terminalText.Length == 0)
        {
            terminalText.Append("TERMINAL OS [4.1.0207]\n");
            terminalText.Append(T("(C) State Government. Authorized use only.\n", "(C) Governo do Estado. Uso autorizado apenas.\n"));
            terminalText.Append(T("Type HELP for the commands.\n\n", "Digite AJUDA para os comandos.\n\n"));
        }
        terminal.window.Open("C:\\WINDOWS\\SYSTEM32\\CMD.EXE");
        ShowTerminal();
        if (terminal.input != null)
        {
            terminal.input.text = "";
            terminal.input.Select();
            terminal.input.ActivateInputField();
        }
    }

    private void SubmitTerminal(string line)
    {
        if (terminal.window == null || !terminal.window.IsOpen) return;
        string cmd = (line ?? "").Trim();
        terminalText.Append(Prompt).Append(' ').Append(cmd).Append('\n');
        if (cmd.Length > 0) RunCommand(cmd);
        if (terminal.input != null)
        {
            terminal.input.text = "";
            terminal.input.ActivateInputField();
        }
        ShowTerminal();
    }

    private void RunCommand(string cmd)
    {
        string verb = cmd;
        string arg = "";
        int space = cmd.IndexOf(' ');
        if (space > 0) { verb = cmd.Substring(0, space); arg = cmd.Substring(space + 1).Trim(); }
        verb = verb.ToLowerInvariant();
        switch (verb)
        {
            case "help": case "ajuda": case "?":
                Print(T("HELP      this list\nDIR       the files\nTYPE x    shows a text file\nWHOAMI    who is logged in\nDATE      date and time\nCLS       clears the screen\nEXIT      closes the terminal",
                        "AJUDA     esta lista\nDIR       os arquivos\nTYPE x    mostra um arquivo de texto\nWHOAMI    quem está conectado\nDATA      data e hora\nCLS       limpa a tela\nSAIR      fecha o terminal"));
                return;
            case "dir": case "ls":
                CollectFiles();
                var sb = new StringBuilder();
                foreach (string f in Folders())
                {
                    sb.Append(' ').Append(f == RecycleKey ? T("RECYCLED", "LIXEIRA") : f.ToUpperInvariant()).Append("\\\n");
                    foreach (OperatorFile file in unlockedFiles)
                    {
                        bool inHere = f == RecycleKey ? file.InRecycleBin : !file.InRecycleBin && file.Folder(Language) == f;
                        if (inHere) sb.Append("    ").Append(file.FileName(Language)).Append('\n');
                    }
                }
                Print(sb.ToString().TrimEnd('\n'));
                return;
            case "type": case "cat": case "more":
                OperatorFile found = FindFile(arg);
                if (found == null) Print(T("File not found.", "Arquivo não encontrado."));
                else if (found.FileKind == OperatorFile.Kind.Image) Print(T("Binary file. Open it in My Computer.", "Arquivo binário. Abra pelo Meu Computador."));
                else { Print(found.Body(Language)); MarkRead(FileKey(found)); }
                return;
            case "whoami":
                Print(userName.ToLowerInvariant() + "@terminal07");
                return;
            case "date": case "data": case "time": case "hora":
                Print(DateTime.Now.ToString(Pt ? "dd/MM/yyyy HH:mm:ss" : "MM/dd/yyyy HH:mm:ss"));
                return;
            case "cls": case "clear":
                terminalText.Length = 0;
                return;
            case "exit": case "sair":
                terminal.window.Close();
                return;
        }
        string whole = cmd.ToLowerInvariant();
        foreach (TerminalEntry e in terminalEntries)
        {
            if (e == null || string.IsNullOrEmpty(e.command)) continue;
            foreach (string name in e.command.Split('|'))
            {
                if (name.Trim().ToLowerInvariant() != whole) continue;
                Print(Pt && !string.IsNullOrEmpty(e.responsePt) ? e.responsePt : e.responseEn);
                if (story != null && !string.IsNullOrEmpty(e.setFlag)) story.Set(e.setFlag);
                return;
            }
        }
        Print(T($"'{verb}' is not recognized as an internal command.", $"'{verb}' não é reconhecido como um comando interno."));
    }

    private OperatorFile FindFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        CollectFiles();
        foreach (OperatorFile f in unlockedFiles)
            if (string.Equals(f.FileName(GameLanguage.English), fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.FileName(GameLanguage.Portuguese), fileName, StringComparison.OrdinalIgnoreCase))
                return f;
        return null;
    }

    private void Print(string text)
    {
        terminalText.Append(text).Append("\n\n");
        if (terminalText.Length > TerminalMaxChars) terminalText.Remove(0, terminalText.Length - TerminalMaxChars);
    }

    private void ShowTerminal()
    {
        if (terminal.output != null) terminal.output.text = "<noparse>" + terminalText.ToString().Replace("</noparse>", "") + "</noparse>";
        if (terminal.scroll == null) return;
        Canvas.ForceUpdateCanvases();
        terminal.scroll.verticalNormalizedPosition = 0f;
    }

    // ------------------------------------------------------------------ misc
    private void Click() => OperatorSounds.Play(audioSource, OperatorSounds.Kind.Click, 0.6f);

    private static void Show(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private void Update()
    {
        if (stage == Stage.Bios || stage == Stage.Logo) UpdateBoot();
        if (!open) return;
        if (desktop.clock != null) desktop.clock.text = DateTime.Now.ToString("HH:mm");
        if (desktop.status != null && statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f) desktop.status.text = "";
        }
    }
}
