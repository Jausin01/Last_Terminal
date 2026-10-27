using UnityEngine;

// One thing pinned on an evidence board (EvidenceBoard): a photo, a clipping, a note. Its box collider is the area
// that can be clicked (only while he looks at the board); clicked, the view zooms on it and the board's panel shows
// its title and text, and he thinks its thought. Its blue arrow (forward) looks INTO the board: the view comes from
// the front, Zoom Distance away. Move / resize it over the picture of the board you want.
[RequireComponent(typeof(BoxCollider))]
public class EvidenceItem : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string HighlightField = nameof(highlight);
    public const string ZoomDistanceField = nameof(zoomDistance);

    // Its title and text (shown on the panel), per language.
    [SerializeField] private string titleEn = "";
    [SerializeField] private string titlePt = "";
    [SerializeField, TextArea(3, 12)] private string textEn = "";
    [SerializeField, TextArea(3, 12)] private string textPt = "";
    [Tooltip("What he thinks looking at it (top-left block). Empty = nothing.")]
    [SerializeField, TextArea(1, 4)] private string thoughtEn = "";
    // His thought in Portuguese.
    [SerializeField, TextArea(1, 4)] private string thoughtPt = "";
    [Tooltip("StoryProgress flag needed for it to be there (empty = always).")]
    [SerializeField] private string requiredFlag = "";
    [Tooltip("StoryProgress flag set the first time it is seen (empty = none).")]
    [SerializeField] private string seenFlag = "";
    [Tooltip("How far the view stops in front of it (m).")]
    [SerializeField] private float zoomDistance = 0.55f;
    [Tooltip("The frame drawn around it while the mouse is over it.")]
    [SerializeField] private LineRenderer highlight;

    // Its click area.
    private BoxCollider box;

    // Read by the board.
    public string RequiredFlag => requiredFlag;
    public string SeenFlag => seenFlag;
    public BoxCollider Box => box != null ? box : box = GetComponent<BoxCollider>();

    // Title / text / thought in a language (English when the Portuguese one is empty).
    public string Title(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(titlePt) ? titlePt : titleEn;
    public string Text(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(textPt) ? textPt : textEn;
    public string Thought(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(thoughtPt) ? thoughtPt : thoughtEn;

    // Not clickable until he looks at the board.
    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        SetClickable(false);
    }

    // Clickable only while he looks at the board.
    public void SetClickable(bool on)
    {
        Box.enabled = on;
        if (!on) SetHighlight(false);
    }

    // The frame around it while the mouse is over it.
    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.enabled = on;
    }

    // where the view goes to look at it
    public void ZoomPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 center = transform.TransformPoint(Box.center);
        position = center - transform.forward * zoomDistance;
        rotation = Quaternion.LookRotation(transform.forward, transform.up);
    }

#if UNITY_EDITOR
    // Editor: sets the content (room builder).
    public void EditorSet(string tEn, string tPt, string bEn, string bPt, string thEn, string thPt, string flag, string seen)
    {
        titleEn = tEn; titlePt = tPt; textEn = bEn; textPt = bPt; thoughtEn = thEn; thoughtPt = thPt;
        requiredFlag = flag; seenFlag = seen;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // Draws the click box and the zoom distance in the Scene view.
    private void OnDrawGizmosSelected()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.DrawLine(b.center - Vector3.forward * zoomDistance, b.center);
    }
}
