using TMPro;
using UnityEngine;
using UnityEngine.UI;

// What the operator sees on his own screen while walking around his room: a dot in the middle (bigger over
// something he can use, with its name), a line of text for what happens ("Locked."), a note being read (cork
// boards), his thoughts (the block in the top-left corner) and the controls hint of the pinball.
public class OperatorRoomHud : MonoBehaviour
{
    public const string GroupField = nameof(group);
    public const string CrosshairField = nameof(crosshair);
    public const string HoverField = nameof(hover);
    public const string MessageField = nameof(message);
    public const string NoteGroupField = nameof(noteGroup);
    public const string NoteTitleField = nameof(noteTitle);
    public const string NoteBodyField = nameof(noteBody);
    public const string NoteHintField = nameof(noteHint);
    public const string HintField = nameof(hint);
    public const string ThoughtsField = nameof(thoughts);

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image crosshair;
    [SerializeField] private TMP_Text hover;
    [SerializeField] private TMP_Text message;
    [SerializeField] private CanvasGroup noteGroup;
    [SerializeField] private TMP_Text noteTitle;
    [SerializeField] private TMP_Text noteBody;
    [SerializeField] private TMP_Text noteHint;
    [Tooltip("Controls hint at the bottom (pinball).")]
    [SerializeField] private TMP_Text hint;
    [Tooltip("What he thinks (the block in the top-left corner).")]
    [SerializeField] private ThoughtView thoughts;
    [SerializeField] private float crosshairSize = 6f;
    [SerializeField] private float crosshairHoverSize = 14f;

    private float messageTimer;
    private bool visible = true;

    public bool IsNoteOpen => noteGroup != null && noteGroup.alpha > 0.5f;

    private void Awake()
    {
        if (group == null || crosshair == null || hover == null || message == null) { enabled = false; return; }
        hover.text = "";
        message.text = "";
        if (noteGroup != null) { noteGroup.alpha = 0f; noteGroup.blocksRaycasts = false; }
        if (hint != null) hint.text = "";
    }

    // the room view (dot, labels, messages); the hint and the note have their own switches
    public void SetVisible(bool value)
    {
        visible = value;
        group.alpha = value ? 1f : 0f;
        if (!value) SetHover(null);
    }

    public void SetHover(string label)
    {
        bool on = !string.IsNullOrEmpty(label);
        hover.text = on ? label : "";
        float s = on ? crosshairHoverSize : crosshairSize;
        crosshair.rectTransform.sizeDelta = new Vector2(s, s);
        crosshair.color = on ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(1f, 1f, 1f, 0.6f);
    }

    public void ShowMessage(string text, float seconds = 3f)
    {
        message.text = text;
        message.alpha = 1f;
        messageTimer = seconds;
    }

    // a thought in the top-left block (falls back to the message line without one); returns how long it stays
    public float ShowThought(string text, float seconds = 0f)
    {
        if (thoughts != null) return thoughts.Show(text, seconds);
        float t = seconds > 0f ? seconds : 3f;
        ShowMessage(text, t);
        return t;
    }

    public void HideThought()
    {
        if (thoughts != null) thoughts.Hide();
    }

    public void ShowNote(string title, string body, string closeHint)
    {
        if (noteGroup == null) return;
        noteTitle.text = title;
        noteBody.text = body;
        if (noteHint != null) noteHint.text = closeHint;
        noteGroup.alpha = 1f;
    }

    public void HideNote()
    {
        if (noteGroup != null) noteGroup.alpha = 0f;
    }

    public void SetHint(string text)
    {
        if (hint != null) hint.text = text ?? "";
    }

    private void Update()
    {
        if (messageTimer <= 0f) return;
        messageTimer -= Time.unscaledDeltaTime;
        message.alpha = Mathf.Clamp01(messageTimer / 0.6f);
        if (messageTimer <= 0f) message.text = "";
        if (!visible) message.alpha = 0f;
    }
}
