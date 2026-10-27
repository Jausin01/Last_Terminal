using TMPro;
using UnityEngine;

// The panel on the right of the screen while he looks at a photo of an evidence board (EvidenceBoard).
public class EvidencePanel : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GroupField = nameof(group);
    public const string TitleField = nameof(title);
    public const string BodyField = nameof(body);

    // The panel, its title and body, and the fade speed.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private float fade = 4f;

    // Wanted visibility.
    private bool shown;

    // Starts hidden.
    private void Awake()
    {
        if (group == null) { enabled = false; return; }
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    // Shows a text (hidden when both are empty).
    public void Show(string heading, string text)
    {
        if (title != null) title.text = heading;
        if (body != null) body.text = text;
        shown = !string.IsNullOrEmpty(heading) || !string.IsNullOrEmpty(text);
    }

    // Fades out.
    public void Hide() => shown = false;

    // Fades toward the wanted visibility.
    private void Update() => group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, Time.unscaledDeltaTime * fade);
}
