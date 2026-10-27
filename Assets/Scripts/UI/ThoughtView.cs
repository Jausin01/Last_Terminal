using TMPro;
using UnityEngine;

// The protagonist's thoughts: a small block in the top-left corner of the screen (under the camera name / room of the
// CCTV), with a "THOUGHTS" / "PENSAMENTOS" tag over the text. Fades in, stays for a while (by the length of the text),
// fades out. Used by the cutscene director (ThoughtStep) and by the operator's room (OperatorRoomHud.ShowThought).
// The block grows with the text (the layout on its root); move / resize its RectTransform freely.
public class ThoughtView : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GroupField = nameof(group);
    public const string TagField = nameof(tagText);
    public const string BodyField = nameof(body);
    public const string SettingsField = nameof(settings);

    // The block's CanvasGroup (fades).
    [SerializeField] private CanvasGroup group;
    [Tooltip("The small tag over the text (\"THOUGHTS\").")]
    [SerializeField] private TMP_Text tagText;
    // The thought text.
    [SerializeField] private TMP_Text body;
    [Tooltip("Optional: the tag follows the game language.")]
    [SerializeField] private CameraSettings settings;
    // Fade in / out (s).
    [SerializeField] private float fadeIn = 0.25f;
    [SerializeField] private float fadeOut = 0.6f;
    [Tooltip("Seconds per character (the shortest stays Min Seconds).")]
    [SerializeField] private float secondsPerChar = 0.06f;
    // Shortest time on screen (s).
    [SerializeField] private float minSeconds = 2.2f;

    // Time left on screen.
    private float timer;

    // A thought is showing.
    public bool IsShowing => timer > 0f;

    // Hidden at start; checks references.
    private void Awake()
    {
        if (group == null || body == null) { enabled = false; return; }
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // how long a text stays on screen
    public float Duration(string text) => Mathf.Max(minSeconds, (text != null ? text.Length : 0) * secondsPerChar);

    // shows a thought; seconds <= 0 = by its length. Returns how long it stays.
    public float Show(string text, float seconds = 0f)
    {
        if (!enabled) return 0f;
        if (string.IsNullOrEmpty(text)) { Hide(); return 0f; }
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        if (tagText != null) tagText.text = pt ? "PENSAMENTOS" : "THOUGHTS";
        body.text = $"<i>{text}</i>";
        timer = seconds > 0f ? seconds : Duration(text);
        return timer;
    }

    // Hides it at once.
    public void Hide() => timer = 0f;

    // Fades in, holds, fades out.
    private void Update()
    {
        if (timer > 0f) timer -= Time.unscaledDeltaTime;
        float target = timer > 0f ? 1f : 0f;
        float speed = 1f / Mathf.Max(0.01f, target > group.alpha ? fadeIn : fadeOut);
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * speed);
    }
}
