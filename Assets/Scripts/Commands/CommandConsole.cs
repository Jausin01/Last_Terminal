using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The operator's microphone, typed for now: Enter opens the line, type, Enter sends it to Helena, Esc cancels.
// Characters come from the keyboard's text input (accents included); while typing, the camera keys are off
// (SecurityCameraSystem.SetTyping). The last command stays on screen for a moment. Voice recognition will call
// PartnerCommands.Execute the same way.
public class CommandConsole : MonoBehaviour
{
    public const string CommandsField = nameof(commands);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string SaveScreenField = nameof(saveScreen);
    public const string SettingsField = nameof(settings);
    public const string PanelField = nameof(panel);
    public const string InputTextField = nameof(inputText);
    public const string EchoTextField = nameof(echoText);

    [SerializeField] private PartnerCommands commands;
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private SaveScreen saveScreen;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text inputText;
    [SerializeField] private TMP_Text echoText;
    [SerializeField] private int maxLength = 80;
    [SerializeField] private float echoTime = 3f;
    [Tooltip("Backspace held: first repeat after, then every (s).")]
    [SerializeField] private Vector2 backspaceRepeat = new Vector2(0.4f, 0.05f);

    [Header("Input")]
    [SerializeField] private InputAction submit = new InputAction("Submit", InputActionType.Button);
    [SerializeField] private InputAction cancel = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
    [SerializeField] private InputAction backspace = new InputAction("Backspace", InputActionType.Button, "<Keyboard>/backspace");

    private bool open;
    private string text = "";
    private float echoTimer;
    private float backspaceTimer;
    private Keyboard keyboard;

    public bool IsOpen => open;

    private void Awake()
    {
        if (submit.bindings.Count == 0)
        {
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
        }
        // no PartnerCommands / camera system (a story cutscene scene): it only opens for the operator's lines
        if (panel == null || inputText == null || (commands != null && cameraSystem == null))
        {
            Debug.LogError($"{name}: CommandConsole needs a panel and a text (and the camera system with Helena).", this);
            enabled = false;
            return;
        }
        panel.alpha = 0f;
        if (echoText != null) echoText.text = "";
    }

    private void OnEnable()
    {
        submit.Enable(); cancel.Enable(); backspace.Enable();
        submit.performed += OnSubmit;
        cancel.performed += OnCancel;
        backspace.performed += OnBackspace;
    }

    private void OnDisable()
    {
        SetOpen(false);
        submit.performed -= OnSubmit;
        cancel.performed -= OnCancel;
        backspace.performed -= OnBackspace;
        submit.Disable(); cancel.Disable(); backspace.Disable();
    }

    private bool CanOpen => (cameraSystem == null || (!cameraSystem.IsSettingsOpen && !cameraSystem.IsOverlayOpen && (!cameraSystem.IsLocked || overrideHandler != null)))
                            && (commands != null || overrideHandler != null)
                            && (saveScreen == null || !saveScreen.IsOpen);

    private System.Action<string> overrideHandler;   // a cutscene waiting for the operator's line

    // what is typed goes to the handler (not to Helena) until EndOverride
    public void BeginOverride(System.Action<string> handler) => overrideHandler = handler;

    public void EndOverride()
    {
        overrideHandler = null;
        if (open && (cameraSystem == null || cameraSystem.IsLocked)) SetOpen(false);
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (!open)
        {
            if (CanOpen) SetOpen(true);
            return;
        }
        string sent = text.Trim();
        SetOpen(false);
        if (sent.Length == 0) return;
        if (echoText != null)
        {
            echoText.text = "> " + sent;
            echoTimer = echoTime;
        }
        if (overrideHandler != null) { overrideHandler(sent); return; }
        if (commands != null) commands.Execute(sent);
    }

    // a command that came another way (the microphone): shown like a typed one
    public void Echo(string sent)
    {
        if (echoText == null || string.IsNullOrEmpty(sent)) return;
        echoText.text = "> " + sent;
        echoTimer = echoTime;
    }

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        if (open) SetOpen(false);
    }

    private void OnBackspace(InputAction.CallbackContext ctx)
    {
        if (!open) return;
        DeleteLast();
        backspaceTimer = backspaceRepeat.x;
    }

    private void SetOpen(bool value)
    {
        if (open == value) return;
        open = value;
        text = "";
        if (open)
        {
            keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onTextInput += OnTextInput;
        }
        else if (keyboard != null)
        {
            keyboard.onTextInput -= OnTextInput;
            keyboard = null;
        }
        if (cameraSystem != null) cameraSystem.SetTyping(open);
        if (panel != null) panel.alpha = open ? 1f : 0f;
        Refresh();
    }

    private void OnTextInput(char c)
    {
        if (!open || char.IsControl(c) || text.Length >= maxLength) return;
        text += c;
        Refresh();
    }

    private void DeleteLast()
    {
        if (text.Length == 0) return;
        text = text.Substring(0, text.Length - 1);
        Refresh();
    }

    private void Update()
    {
        if (open)
        {
            // something else took the screen (save screen, settings): drop the line
            if (!CanOpen) { SetOpen(false); return; }
            if (backspace.IsPressed())
            {
                backspaceTimer -= Time.unscaledDeltaTime;
                if (backspaceTimer <= 0f) { DeleteLast(); backspaceTimer = backspaceRepeat.y; }
            }
            Refresh();
        }
        if (echoText != null && echoTimer > 0f)
        {
            echoTimer -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(echoTimer / 0.5f);
            echoText.alpha = a;
            if (echoTimer <= 0f) echoText.text = "";
        }
    }

    private void Refresh()
    {
        if (inputText == null) return;
        bool caret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        string prompt = commands == null ? (pt ? "DIZER" : "SAY") : pt ? "FALAR COM HELENA" : "TALK TO HELENA";
        // escape TMP rich text typed by the player
        string shown = text.Replace("<", "<noparse><</noparse>");
        inputText.text = $"<color=#5A9A66>{prompt} ></color> {shown}{(caret ? "_" : " ")}";
    }
}
