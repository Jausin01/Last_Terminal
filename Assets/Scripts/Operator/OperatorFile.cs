using UnityEngine;

// A file on the operator's PC: a text (English and Portuguese) or a picture, shown once its story flag is set
// (StoryProgress; empty = there from the start). On the fake OS of his room (OperatorOS) it sits in a folder of the
// file explorer, or in the Recycle Bin. Edit the texts freely: the Last Terminal menus only create the files that are
// missing, they never overwrite them.
[CreateAssetMenu(menuName = "Last Terminal/Operator File", fileName = "OperatorFile")]
public class OperatorFile : ScriptableObject
{
    public enum Kind { Text, Image }

    [Tooltip("The name in the file list (e.g. readme.txt).")]
    [SerializeField] private string fileNameEn = "notes.txt";
    [SerializeField] private string fileNamePt = "notas.txt";
    [SerializeField] private string titleEn = "";
    [SerializeField] private string titlePt = "";
    [SerializeField, TextArea(6, 30)] private string bodyEn = "";
    [SerializeField, TextArea(6, 30)] private string bodyPt = "";
    [Tooltip("StoryProgress flag that unlocks it (empty = from the start).")]
    [SerializeField] private string requiredFlag = "";
    [Tooltip("Order in the list (lower first).")]
    [SerializeField] private int order;

    [Header("Fake OS (his room)")]
    [SerializeField] private Kind kind = Kind.Text;
    [Tooltip("A picture file: the image (the body is its caption).")]
    [SerializeField] private Texture2D image;
    [Tooltip("The folder it is in (files with the same folder name are together).")]
    [SerializeField] private string folderEn = "Documents";
    [SerializeField] private string folderPt = "Documentos";
    [Tooltip("In the Recycle Bin instead of a folder.")]
    [SerializeField] private bool inRecycleBin;

    public string RequiredFlag => requiredFlag;
    public int Order => order;
    public string Id => name;
    public Kind FileKind => kind;
    public Texture2D Image => image;
    public bool InRecycleBin => inRecycleBin;

    public string Folder(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(folderPt) ? folderPt : folderEn;

    public string FileName(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(fileNamePt) ? fileNamePt : fileNameEn;
    public string Title(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(titlePt) ? titlePt : titleEn;
    public string Body(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(bodyPt) ? bodyPt : bodyEn;

#if UNITY_EDITOR
    public void EditorSet(string fileEn, string filePt, string tEn, string tPt, string bEn, string bPt, string flag, int sortOrder)
    {
        fileNameEn = fileEn; fileNamePt = filePt; titleEn = tEn; titlePt = tPt; bodyEn = bEn; bodyPt = bPt;
        requiredFlag = flag; order = sortOrder;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void EditorSetPlace(Kind fileKind, Texture2D picture, string folderEnglish, string folderPortuguese, bool recycled)
    {
        kind = fileKind; image = picture; folderEn = folderEnglish; folderPt = folderPortuguese; inRecycleBin = recycled;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
