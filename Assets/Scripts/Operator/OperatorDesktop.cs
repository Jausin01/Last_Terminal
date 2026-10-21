using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The desktop of his PC (after closing the camera program): icons for the camera program (CCTV.EXE), his files and
// getting up; a clock. Files: the OperatorFiles whose story flag is set (StoryProgress), newest marked NEW until read;
// one opens in a viewer window. Every text follows the game language (CameraSettings).
public class OperatorDesktop : DesktopScreen
{
    public const string GroupField = nameof(group);
    public const string SettingsField = nameof(settings);
    public const string StoryField = nameof(story);
    public const string FilesField = nameof(files);
    public const string ProgramButtonField = nameof(programButton);
    public const string FilesButtonField = nameof(filesButton);
    public const string StandButtonField = nameof(standButton);
    public const string ProgramLabelField = nameof(programLabel);
    public const string FilesLabelField = nameof(filesLabel);
    public const string StandLabelField = nameof(standLabel);
    public const string ClockField = nameof(clock);
    public const string StatusField = nameof(status);
    public const string FilesWindowField = nameof(filesWindow);
    public const string FilesTitleField = nameof(filesTitle);
    public const string FileListField = nameof(fileList);
    public const string FileTemplateField = nameof(fileTemplate);
    public const string FilesCloseField = nameof(filesClose);
    public const string ViewerField = nameof(viewer);
    public const string ViewerTitleField = nameof(viewerTitle);
    public const string ViewerBodyField = nameof(viewerBody);
    public const string ViewerCloseField = nameof(viewerClose);
    public const string ViewerScrollField = nameof(viewerScroll);
    public const string AudioField = nameof(audioSource);

    [SerializeField] private CanvasGroup group;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private StoryProgress story;
    [SerializeField] private OperatorFile[] files = new OperatorFile[0];
    [SerializeField] private AudioSource audioSource;

    [Header("Desktop")]
    [SerializeField] private Button programButton;
    [SerializeField] private Button filesButton;
    [SerializeField] private Button standButton;
    [SerializeField] private TMP_Text programLabel;
    [SerializeField] private TMP_Text filesLabel;
    [SerializeField] private TMP_Text standLabel;
    [SerializeField] private TMP_Text clock;
    [Tooltip("Line on the taskbar (why he can't get up, etc.).")]
    [SerializeField] private TMP_Text status;

    [Header("Files window")]
    [SerializeField] private CanvasGroup filesWindow;
    [SerializeField] private TMP_Text filesTitle;
    [SerializeField] private RectTransform fileList;
    [Tooltip("One row of the list (inactive; copied per file).")]
    [SerializeField] private OperatorFileRow fileTemplate;
    [SerializeField] private Button filesClose;

    [Header("Viewer")]
    [SerializeField] private CanvasGroup viewer;
    [SerializeField] private TMP_Text viewerTitle;
    [SerializeField] private TMP_Text viewerBody;
    [SerializeField] private Button viewerClose;
    [SerializeField] private ScrollRect viewerScroll;

    private readonly List<GameObject> rows = new List<GameObject>();
    private readonly List<OperatorFile> unlocked = new List<OperatorFile>();
    private bool open;
    private float statusTimer;

    public override bool IsOpen => open;

    private GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;
    private bool Pt => Language == GameLanguage.Portuguese;

    private void Awake()
    {
        if (group == null || programButton == null || filesButton == null || standButton == null)
        {
            Debug.LogError($"{name}: OperatorDesktop is missing references (run the Last Terminal scene menu).", this);
            enabled = false;
            return;
        }
        programButton.onClick.AddListener(OnProgram);
        filesButton.onClick.AddListener(OpenFiles);
        standButton.onClick.AddListener(OnStand);
        if (filesClose != null) filesClose.onClick.AddListener(CloseFiles);
        if (viewerClose != null) viewerClose.onClick.AddListener(CloseViewer);
        if (fileTemplate != null) fileTemplate.gameObject.SetActive(false);
        Show(group, false);
        Show(filesWindow, false);
        Show(viewer, false);
    }

    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        if (story != null) story.FlagSet += OnFlag;
    }

    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
        if (story != null) story.FlagSet -= OnFlag;
    }

    private void OnFlag(string flag)
    {
        if (open) Refresh();
    }

    public override void SetOpen(bool value)
    {
        open = value;
        Show(group, value);
        if (!value) { Show(filesWindow, false); Show(viewer, false); }
        if (value) Refresh();
    }

    // a line on the taskbar for a while
    public override void SetStatus(string text, float seconds = 4f)
    {
        if (status == null) return;
        status.text = text;
        statusTimer = seconds;
    }

    // Esc on the desktop: closes the top window; false = nothing open (the caller gets him up)
    public override bool CloseTopWindow()
    {
        if (viewer != null && viewer.alpha > 0.5f) { CloseViewer(); return true; }
        if (filesWindow != null && filesWindow.alpha > 0.5f) { CloseFiles(); return true; }
        return false;
    }

    private void OnProgram()
    {
        Click();
        RaiseProgram();
    }

    private void OnStand()
    {
        Click();
        RaiseStand();
    }

    private void Click() => OperatorSounds.Play(audioSource, OperatorSounds.Kind.Click, 0.7f);

    private void Refresh()
    {
        programLabel.text = "CCTV.EXE";
        int fresh = CollectFiles();
        filesLabel.text = (Pt ? "ARQUIVOS" : "FILES") + (fresh > 0 ? $"\n<color=#FFE066>({fresh} {(Pt ? (fresh > 1 ? "NOVOS" : "NOVO") : "NEW")})</color>" : "");
        standLabel.text = Pt ? "LEVANTAR" : "STAND UP";
        if (filesTitle != null) filesTitle.text = Pt ? "C:\\OPERADOR\\ARQUIVOS" : "C:\\OPERATOR\\FILES";
        if (filesWindow != null && filesWindow.alpha > 0.5f) BuildList();
    }

    // the unlocked files, in order; returns how many are unread
    private int CollectFiles()
    {
        unlocked.Clear();
        int fresh = 0;
        foreach (OperatorFile f in files)
        {
            if (f == null || (story != null && !story.Has(f.RequiredFlag))) continue;
            if (story == null && !string.IsNullOrEmpty(f.RequiredFlag)) continue;
            unlocked.Add(f);
            if (!IsRead(f)) fresh++;
        }
        unlocked.Sort((a, b) => a.Order.CompareTo(b.Order));
        return fresh;
    }

    private bool IsRead(OperatorFile f) => story != null && story.Has(StoryProgress.ReadPrefix + f.Id);

    private void OpenFiles()
    {
        Click();
        Show(filesWindow, true);
        CollectFiles();
        BuildList();
    }

    private void CloseFiles()
    {
        Click();
        Show(filesWindow, false);
        Show(viewer, false);
    }

    private void BuildList()
    {
        if (fileList == null || fileTemplate == null) return;
        foreach (GameObject r in rows) Destroy(r);
        rows.Clear();
        foreach (OperatorFile f in unlocked)
        {
            OperatorFileRow row = Instantiate(fileTemplate, fileList);
            row.gameObject.SetActive(true);
            if (row.Label != null) row.Label.text = f.FileName(Language) + (IsRead(f) ? "" : $"   <color=#FFE066>{(Pt ? "NOVO" : "NEW")}</color>");
            OperatorFile file = f;
            if (row.Button != null) row.Button.onClick.AddListener(() => OpenFile(file));
            rows.Add(row.gameObject);
        }
    }

    private void OpenFile(OperatorFile f)
    {
        Click();
        viewerTitle.text = f.FileName(Language) + "  -  " + f.Title(Language);
        viewerBody.text = f.Body(Language);
        Show(viewer, true);
        if (viewerScroll != null) viewerScroll.verticalNormalizedPosition = 1f;
        if (story != null && !IsRead(f)) story.Set(StoryProgress.ReadPrefix + f.Id);   // refreshes (FlagSet)
    }

    private void CloseViewer()
    {
        Click();
        Show(viewer, false);
    }

    private static void Show(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private void Update()
    {
        if (!open) return;
        if (clock != null) clock.text = DateTime.Now.ToString("HH:mm");
        if (status != null && statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f) status.text = "";
        }
    }
}
