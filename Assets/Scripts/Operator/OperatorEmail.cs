using UnityEngine;

// An e-mail in the fake OS of his room (OperatorOS > E-mail): from someone, a subject and a text in English and
// Portuguese, shown once its story flag is set (StoryProgress; empty = there from the start), unread until opened.
// Optional attachment: a file opened from the e-mail. Optional voice: the sender reading it (one recording per
// language; the game's language picks it). Edit freely: the Last Terminal menu never overwrites them.
[CreateAssetMenu(menuName = "Last Terminal/Operator E-mail", fileName = "OperatorEmail")]
public class OperatorEmail : ScriptableObject
{
    [SerializeField] private string fromName = "";
    [SerializeField] private string fromAddress = "";
    [SerializeField] private string subjectEn = "";
    [SerializeField] private string subjectPt = "";
    [Tooltip("As shown (e.g. 14/03 23:58).")]
    [SerializeField] private string date = "";
    [SerializeField, TextArea(6, 30)] private string bodyEn = "";
    [SerializeField, TextArea(6, 30)] private string bodyPt = "";
    [Tooltip("StoryProgress flag that makes it arrive (empty = from the start).")]
    [SerializeField] private string requiredFlag = "";
    [Tooltip("Order (higher = newer = higher in the inbox).")]
    [SerializeField] private int order;
    [Tooltip("Optional: a file attached to it.")]
    [SerializeField] private OperatorFile attachment;
    [Header("Voice (the sender reading it)")]
    [Tooltip("Played in English. Empty = the player shows that there is no recording yet.")]
    [SerializeField] private AudioClip voiceEn;
    [Tooltip("Played in Portuguese.")]
    [SerializeField] private AudioClip voicePt;

    public const string ReadPrefix = "read_mail_";

    public string FromName => fromName;
    public string FromAddress => fromAddress;
    public string Date => date;
    public string RequiredFlag => requiredFlag;
    public int Order => order;
    public OperatorFile Attachment => attachment;
    public AudioClip Voice(GameLanguage l) => l == GameLanguage.Portuguese ? voicePt : voiceEn;
    public string Id => name;

    public string Subject(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(subjectPt) ? subjectPt : subjectEn;
    public string Body(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(bodyPt) ? bodyPt : bodyEn;

#if UNITY_EDITOR
    public void EditorSet(string from, string address, string sEn, string sPt, string when, string bEn, string bPt, string flag, int sortOrder, OperatorFile attached)
    {
        fromName = from; fromAddress = address; subjectEn = sEn; subjectPt = sPt; date = when; bodyEn = bEn; bodyPt = bPt;
        requiredFlag = flag; order = sortOrder; attachment = attached;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
