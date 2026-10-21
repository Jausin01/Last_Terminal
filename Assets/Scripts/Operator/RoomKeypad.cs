using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// The keypad by his door, looked at up close (FocusInteractable): the keys on screen (click) or the number keys of
// the keyboard / numpad, Backspace erases, Enter confirms. Every key beeps; a wrong code buzzes and shows ERRO; the
// right one chimes, shows OK and calls On Accepted (wire what it should do there: for now the door stays shut).
// The code is set here (Code). Typing it the first time sets the story flag (Accepted Flag).
public class RoomKeypad : FocusInteractable
{
    public const string CodeField = nameof(code);
    public const string AudioField = nameof(audioSource);
    public const string GroupField = nameof(group);
    public const string DisplayField = nameof(display);
    public const string DigitButtonsField = nameof(digitButtons);
    public const string ClearButtonField = nameof(clearButton);
    public const string EnterButtonField = nameof(enterButton);
    public const string OnAcceptedField = nameof(onAccepted);

    [Tooltip("The code that opens it (digits only).")]
    [SerializeField] private string code = "9999";
    [SerializeField, Range(1, 10)] private int maxDigits = 6;
    [Tooltip("StoryProgress flag set the first time the code is right.")]
    [SerializeField] private string acceptedFlag = "operator_door_code";
    [Tooltip("The keypad's sounds (on the keypad, 3D).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Screen overlay")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text display;
    [Tooltip("The buttons 0 to 9, in that order.")]
    [SerializeField] private Button[] digitButtons = new Button[10];
    [SerializeField] private Button clearButton;
    [SerializeField] private Button enterButton;

    [Header("Result")]
    [SerializeField] private UnityEvent onAccepted = new UnityEvent();
    [SerializeField] private string[] wrongThoughtsEn = { "No.", "Not that one either.", "I'm guessing. That's all I'm doing." };
    [SerializeField] private string[] wrongThoughtsPt = { "Não.", "Também não.", "Tô chutando. É só isso que eu tô fazendo." };
    [SerializeField] private string acceptedThoughtEn = "It took it... but the door didn't move.";
    [SerializeField] private string acceptedThoughtPt = "Aceitou... mas a porta não se mexeu.";

    private static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
    private static readonly Key[] PadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

    private static readonly Color Normal = new Color(0.55f, 1f, 0.6f);
    private static readonly Color Wrong = new Color(1f, 0.25f, 0.2f);

    private FirstPersonPlayer user;
    private string entry = "";
    private float resultTimer;
    private bool accepted;
    private int wrongCount;

    public string Code => code;

    private void Awake()
    {
        for (int i = 0; i < digitButtons.Length; i++)
        {
            int d = i;
            if (digitButtons[i] != null) digitButtons[i].onClick.AddListener(() => Press(d));
        }
        if (clearButton != null) clearButton.onClick.AddListener(Erase);
        if (enterButton != null) enterButton.onClick.AddListener(Confirm);
        SetOverlay(false);
    }

    public override void BeginFocus(FirstPersonPlayer player)
    {
        user = player;
        entry = "";
        resultTimer = 0f;
        SetOverlay(true);
        ShowEntry();
        player.Hud.SetHint(Pick(player.Language, "Type the code (keys or mouse)   |   Enter: confirm   |   Esc: back",
                                                 "Digite o código (teclado ou mouse)   |   Enter: confirmar   |   Esc: voltar"));
    }

    public override void EndFocus(FirstPersonPlayer player)
    {
        SetOverlay(false);
        user = null;
    }

    public override void UpdateFocus(FirstPersonPlayer player)
    {
        if (resultTimer > 0f)
        {
            resultTimer -= Time.deltaTime;
            if (resultTimer <= 0f) { entry = ""; ShowEntry(); }
            return;
        }
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        for (int i = 0; i < 10; i++)
            if (kb[DigitKeys[i]].wasPressedThisFrame || kb[PadKeys[i]].wasPressedThisFrame) Press(i);
        if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) Erase();
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
    }

    private void Press(int digit)
    {
        if (user == null || resultTimer > 0f) return;
        Beep(OperatorSounds.Kind.KeyBeep, 0.7f);
        if (entry.Length >= maxDigits) return;
        entry += digit.ToString();
        ShowEntry();
    }

    private void Erase()
    {
        if (user == null || resultTimer > 0f) return;
        Beep(OperatorSounds.Kind.KeyBeep, 0.5f);
        entry = "";
        ShowEntry();
    }

    private void Confirm()
    {
        if (user == null || resultTimer > 0f || entry.Length == 0) return;
        GameLanguage l = user.Language;
        if (entry == code)
        {
            Beep(OperatorSounds.Kind.Accepted, 0.9f);
            SetDisplay("OK", Normal);
            resultTimer = 1.6f;
            if (!accepted && user.Story != null && !string.IsNullOrEmpty(acceptedFlag)) user.Story.Set(acceptedFlag);
            accepted = true;
            user.Hud.ShowThought(Pick(l, acceptedThoughtEn, acceptedThoughtPt));
            onAccepted.Invoke();
            return;
        }
        Beep(OperatorSounds.Kind.Denied, 0.9f);
        SetDisplay(Pick(l, "ERROR", "ERRO"), Wrong);
        resultTimer = 1.2f;
        string[] lines = l == GameLanguage.Portuguese ? wrongThoughtsPt : wrongThoughtsEn;
        if (lines.Length > 0 && wrongCount++ % 2 == 1) user.Hud.ShowThought(lines[(wrongCount / 2) % lines.Length]);
    }

    private void ShowEntry()
    {
        string shown = entry;
        for (int i = entry.Length; i < Mathf.Max(4, code.Length); i++) shown += "_";
        SetDisplay(string.Join(" ", shown.ToCharArray()), Normal);
    }

    private void SetDisplay(string text, Color color)
    {
        if (display == null) return;
        display.text = text;
        display.color = color;
    }

    private void Beep(OperatorSounds.Kind kind, float volume)
    {
        AudioSource source = audioSource != null ? audioSource : user != null ? user.Audio : null;
        OperatorSounds.Play(source, kind, volume);
    }

    private void SetOverlay(bool on)
    {
        if (group == null) return;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}
