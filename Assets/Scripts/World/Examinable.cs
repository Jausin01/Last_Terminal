using UnityEngine;

// Something she can look at and comment on ("look at the bed", "olha o corpo"): she walks up to it, turns to it and
// says the next line (the last one repeats). Key: the lexicon name it answers to (several things can share one:
// "body" = the closest body in reach). Lines in English and Portuguese, subtitles only unless clips are given.
// Optional: where she stands (Stand Point; empty = in front of it, on her side) and a story flag set the first time.
public class Examinable : MonoBehaviour
{
    public const string KeyField = nameof(key);
    public const string StandPointField = nameof(standPoint);

    [Tooltip("Lexicon key (e.g. x_bed, x_body): the words that name it are in the lexicons.")]
    [SerializeField] private string key = "";
    [SerializeField, TextArea(1, 3)] private string[] linesEn = new string[0];
    [SerializeField, TextArea(1, 3)] private string[] linesPt = new string[0];
    [Tooltip("Optional recordings, same order as the lines.")]
    [SerializeField] private AudioClip[] clipsEn = new AudioClip[0];
    [SerializeField] private AudioClip[] clipsPt = new AudioClip[0];
    [Tooltip("Optional: where she stands to look at it (empty = in front of it, on her side).")]
    [SerializeField] private Transform standPoint;
    [Tooltip("How far from it she stops (m), without a Stand Point.")]
    [SerializeField] private float standDistance = 1.1f;
    [Tooltip("Optional StoryProgress flag set the first time she looks at it.")]
    [SerializeField] private string seenFlag = "";

    private int next;

    public string Key => key;
    public Transform StandPoint => standPoint;
    public float StandDistance => standDistance;
    public string SeenFlag => seenFlag;
    public Vector3 LookPoint => transform.position;
    // she has looked at it at least once (its sparkle goes out)
    public bool Seen { get; private set; }

    // the next line (cycles until the last, which repeats)
    public string NextLine(GameLanguage language, out AudioClip clip)
    {
        bool pt = language == GameLanguage.Portuguese && linesPt.Length > 0;
        string[] lines = pt ? linesPt : linesEn;
        AudioClip[] clips = pt ? clipsPt : clipsEn;
        clip = null;
        Seen = true;
        if (lines.Length == 0) return "";
        int i = Mathf.Min(next, lines.Length - 1);
        next++;
        if (i < clips.Length) clip = clips[i];
        return lines[i];
    }

#if UNITY_EDITOR
    public void EditorSet(string lexiconKey, string[] en, string[] pt, string flag = "")
    {
        key = lexiconKey; linesEn = en; linesPt = pt; seenFlag = flag;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
