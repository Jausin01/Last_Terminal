using TMPro;
using UnityEngine;

// The panel on the right of the screen while he looks at a photo of an evidence board (EvidenceBoard).
public class EvidencePanel : MonoBehaviour
{
    public const string GroupField = nameof(group);
    public const string TitleField = nameof(title);
    public const string BodyField = nameof(body);

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private float fade = 4f;

    private bool shown;

    private void Awake()
    {
        if (group == null) { enabled = false; return; }
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    public void Show(string heading, string text)
    {
        if (title != null) title.text = heading;
        if (body != null) body.text = text;
        shown = !string.IsNullOrEmpty(heading) || !string.IsNullOrEmpty(text);
    }

    public void Hide() => shown = false;

    private void Update() => group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, Time.unscaledDeltaTime * fade);
}
