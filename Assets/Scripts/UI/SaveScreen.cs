using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Terminal-style save screen (green CRT, like the classic save points): boots up line by line, lists the slots,
// SAVE / LOAD modes, overwrite confirmation, "writing" progress bar.
// Opened by PartnerSaveUser; reports back with the result (and fades to black for a load).
// Keyboard / gamepad: up/down = slot, left/right = SAVE/LOAD, Enter = confirm, Esc = exit.
public class SaveScreen : MonoBehaviour
{
    public const string SaveSystemField = nameof(saveSystem);
    public const string PanelField = nameof(panel);
    public const string FaderField = nameof(fader);
    public const string HeaderTextField = nameof(headerText);
    public const string BodyTextField = nameof(bodyText);
    public const string StatusTextField = nameof(statusText);
    public const string HintTextField = nameof(hintText);

    public enum Result { Cancelled, Saved, Loaded }

    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private CanvasGroup panel;
    [Tooltip("Full-screen black, used to hide the jump when a save is loaded.")]
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text hintText;

    [Header("Timing (s, unscaled)")]
    [SerializeField] private float fadeTime = 0.3f;
    [SerializeField] private float bootCharsPerSecond = 160f;
    [SerializeField] private float writeTime = 1.4f;
    [SerializeField] private float doneTime = 1.0f;
    [SerializeField] private float blackTime = 0.4f;

    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.45f, 1f, 0.55f);
    [SerializeField] private Color dim = new Color(0.2f, 0.55f, 0.28f);
    [SerializeField] private string terminalName = "SECURITY NET  v2.11";

    [Header("Input (bindings added when empty)")]
    [SerializeField] private InputAction up = new InputAction("Up", InputActionType.Button);
    [SerializeField] private InputAction down = new InputAction("Down", InputActionType.Button);
    [SerializeField] private InputAction left = new InputAction("Left", InputActionType.Button);
    [SerializeField] private InputAction right = new InputAction("Right", InputActionType.Button);
    [SerializeField] private InputAction submit = new InputAction("Submit", InputActionType.Button);
    [SerializeField] private InputAction cancel = new InputAction("Cancel", InputActionType.Button);

    private enum State { Closed, Opening, Browsing, ConfirmOverwrite, Writing, Done, LoadingOut, LoadingIn, Closing }
    private enum Mode { Save, Load }

    private State state;
    private Mode mode;
    private int selected;
    private float timer;
    private string location;
    private Vector3 resumePosition;
    private float resumeYaw;
    private Action<Result, int> onClosed;
    private Result result;
    private string[] slotLines;
    private readonly StringBuilder sb = new StringBuilder(512);

    public bool IsOpen => state != State.Closed;

    private void Awake()
    {
        AddDefaultBindings(up, "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up");
        AddDefaultBindings(down, "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down");
        AddDefaultBindings(left, "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
        AddDefaultBindings(right, "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
        AddDefaultBindings(submit, "<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        AddDefaultBindings(cancel, "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
        SetVisible(panel, 0f);
        SetVisible(fader, 0f);
        if (saveSystem == null || panel == null || fader == null || headerText == null || bodyText == null || statusText == null || hintText == null)
        {
            Debug.LogError($"{name}: SaveScreen is missing references (built by Last Terminal/4).", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        up.performed += OnUp;
        down.performed += OnDown;
        left.performed += OnLeft;
        right.performed += OnRight;
        submit.performed += OnSubmit;
        cancel.performed += OnCancel;
    }

    private void OnDisable()
    {
        up.performed -= OnUp;
        down.performed -= OnDown;
        left.performed -= OnLeft;
        right.performed -= OnRight;
        submit.performed -= OnSubmit;
        cancel.performed -= OnCancel;
        EnableInput(false);
    }

    // resume position / yaw: where she will stand when this save is loaded.
    public bool Open(string locationName, Vector3 resumeAt, float resumeFacing, Action<Result, int> closed)
    {
        if (!enabled || IsOpen) return false;
        location = locationName;
        resumePosition = resumeAt;
        resumeYaw = resumeFacing;
        onClosed = closed;
        mode = Mode.Save;
        selected = 0;
        RefreshSlots();
        headerText.text = Header();
        headerText.maxVisibleCharacters = 0;
        bodyText.maxVisibleCharacters = 0;
        RedrawBody();
        statusText.text = "";
        hintText.text = "";
        headerText.ForceMeshUpdate();   // character counts for the boot effect
        bodyText.ForceMeshUpdate();
        timer = 0f;
        state = State.Opening;
        EnableInput(true);
        return true;
    }

    private void Update()
    {
        if (state == State.Closed) return;
        float dt = Time.unscaledDeltaTime;
        timer += dt;

        switch (state)
        {
            case State.Opening:
                panel.alpha = Mathf.Clamp01(timer / fadeTime);
                // boot: header then body typed out like a terminal
                int chars = Mathf.FloorToInt(timer * bootCharsPerSecond);
                headerText.maxVisibleCharacters = chars;
                bodyText.maxVisibleCharacters = Mathf.Max(0, chars - headerText.textInfo.characterCount);
                if (chars >= headerText.textInfo.characterCount + bodyText.textInfo.characterCount && timer >= fadeTime)
                {
                    headerText.maxVisibleCharacters = int.MaxValue;
                    bodyText.maxVisibleCharacters = int.MaxValue;
                    EnterBrowsing();
                }
                break;
            case State.Writing:
                statusText.text = $"<mspace=0.6em>WRITING DATA  {Bar(timer / writeTime)}</mspace>";
                if (timer >= writeTime) FinishWriting();
                break;
            case State.Done:
                if (timer >= doneTime) StartClosing(Result.Saved);
                break;
            case State.LoadingOut:
                fader.alpha = Mathf.Clamp01(timer / fadeTime);
                if (timer >= fadeTime + blackTime * 0.5f)
                {
                    SetVisible(panel, 0f);
                    Close(Result.Loaded);   // she is moved while the screen is black
                    state = State.LoadingIn;
                    timer = 0f;
                }
                break;
            case State.LoadingIn:
                fader.alpha = 1f - Mathf.Clamp01((timer - blackTime * 0.5f) / fadeTime);
                if (fader.alpha <= 0f) state = State.Closed;
                break;
            case State.Closing:
                panel.alpha = 1f - Mathf.Clamp01(timer / fadeTime);
                if (panel.alpha <= 0f)
                {
                    state = State.Closed;
                    Close(result);
                }
                break;
        }
    }

    private void EnterBrowsing()
    {
        state = State.Browsing;
        timer = 0f;
        statusText.text = mode == Mode.Save ? "SELECT A SLOT TO WRITE." : "SELECT A SLOT TO READ.";
        hintText.text = "<mspace=0.6em>UP/DOWN SLOT   LEFT/RIGHT SAVE-LOAD   ENTER OK   ESC EXIT</mspace>";
        RedrawBody();
    }

    // ------------------------------------------------------------------ input
    private void OnUp(InputAction.CallbackContext ctx) => MoveSelection(-1);
    private void OnDown(InputAction.CallbackContext ctx) => MoveSelection(1);
    private void OnLeft(InputAction.CallbackContext ctx) => SetMode(Mode.Save);
    private void OnRight(InputAction.CallbackContext ctx) => SetMode(Mode.Load);

    private void MoveSelection(int delta)
    {
        if (state != State.Browsing) return;
        int n = saveSystem.SlotCount;
        selected = (selected + delta + n) % n;
        RedrawBody();
    }

    private void SetMode(Mode m)
    {
        if (state != State.Browsing || mode == m) return;
        mode = m;
        EnterBrowsing();
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (state == State.ConfirmOverwrite) { StartWriting(); return; }
        if (state != State.Browsing) return;

        bool hasData = saveSystem.HasSave(selected);
        if (mode == Mode.Save)
        {
            if (hasData)
            {
                state = State.ConfirmOverwrite;
                statusText.text = $"OVERWRITE SLOT {selected + 1:00}?  ENTER = YES   ESC = NO";
            }
            else StartWriting();
            return;
        }
        if (!hasData) { statusText.text = "NO DATA IN THIS SLOT."; return; }
        statusText.text = "READING DATA...";
        state = State.LoadingOut;
        timer = 0f;
        EnableInput(false);
    }

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        if (state == State.ConfirmOverwrite) { EnterBrowsing(); return; }
        if (state == State.Browsing || state == State.Opening) StartClosing(Result.Cancelled);
    }

    // ------------------------------------------------------------------ save
    private void StartWriting()
    {
        state = State.Writing;
        timer = 0f;
        EnableInput(false);
    }

    private void FinishWriting()
    {
        bool ok = saveSystem.Save(selected, location, resumePosition, resumeYaw);
        RefreshSlots();
        RedrawBody();
        statusText.text = ok ? $"<mspace=0.6em>WRITING DATA  {Bar(1f)}</mspace>\nDATA SAVED. SLOT {selected + 1:00}." : "WRITE ERROR. CHECK STORAGE.";
        state = State.Done;
        timer = 0f;
    }

    private void StartClosing(Result r)
    {
        result = r;
        state = State.Closing;
        timer = 0f;
        EnableInput(false);
    }

    private void Close(Result r)
    {
        Action<Result, int> callback = onClosed;
        onClosed = null;
        callback?.Invoke(r, selected);
    }

    // ------------------------------------------------------------------ text
    private string Header()
    {
        sb.Clear();
        sb.Append("<mspace=0.6em>").Append(terminalName).Append('\n');
        sb.Append("TERMINAL: ").Append(location.ToUpperInvariant()).Append('\n');
        sb.Append("------------------------------------------------</mspace>");
        return sb.ToString();
    }

    private void RefreshSlots()
    {
        int n = saveSystem.SlotCount;
        if (slotLines == null || slotLines.Length != n) slotLines = new string[n];
        for (int i = 0; i < n; i++)
        {
            if (saveSystem.TryRead(i, out SaveData d))
            {
                string date = DateTime.TryParse(d.savedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t)
                    ? t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "----------";
                string loc = (d.location ?? "").ToUpperInvariant();
                if (loc.Length > 16) loc = loc.Substring(0, 16);
                slotLines[i] = $"SLOT {i + 1:00}  {loc,-16}  {SaveSystem.FormatPlayTime(d.playTime)}  {date}";
            }
            else slotLines[i] = $"SLOT {i + 1:00}  ------------  NO DATA  ------------";
        }
    }

    private void RedrawBody()
    {
        string on = ColorUtility.ToHtmlStringRGB(phosphor);
        string off = ColorUtility.ToHtmlStringRGB(dim);
        sb.Clear();
        sb.Append("<mspace=0.6em>");
        sb.Append(mode == Mode.Save ? $"<color=#{on}>[ SAVE ]</color>   <color=#{off}>  LOAD  </color>" : $"<color=#{off}>  SAVE  </color>   <color=#{on}>[ LOAD ]</color>");
        sb.Append("\n\n");
        for (int i = 0; i < slotLines.Length; i++)
        {
            bool sel = i == selected;
            sb.Append(sel ? $"<color=#{on}>> " : $"<color=#{off}>  ").Append(slotLines[i]).Append("</color>\n");
        }
        sb.Append("</mspace>");
        bodyText.text = sb.ToString();
    }

    private static string Bar(float t)
    {
        const int width = 20;
        int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "] " + Mathf.RoundToInt(Mathf.Clamp01(t) * 100f) + "%";
    }

    // ------------------------------------------------------------------ helpers
    private void EnableInput(bool on)
    {
        foreach (InputAction a in new[] { up, down, left, right, submit, cancel })
        {
            if (on) a.Enable();
            else a.Disable();
        }
    }

    private static void AddDefaultBindings(InputAction action, params string[] paths)
    {
        if (action.bindings.Count > 0) return;
        foreach (string p in paths) action.AddBinding(p);
    }

    private static void SetVisible(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
