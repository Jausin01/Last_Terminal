using UnityEngine;

// A file on the operator's PC: a text (English and Portuguese) or a picture, shown once its story flag is set
// (StoryProgress; empty = there from the start). On the fake OS of his room (OperatorOS) it sits in a folder of the
// file explorer, or in the Recycle Bin. Edit the texts freely: the Last Terminal menus only create the files that are
// missing, they never overwrite them.
[CreateAssetMenu(menuName = "Last Terminal/Operator File", fileName = "OperatorFile")]
public class OperatorFile : ScriptableObject
{
    // A text file or a picture.
    public enum Kind { Text, Image }

    [Tooltip("The name in the file list (e.g. readme.txt).")]
    [SerializeField] private string fileNameEn = "notes.txt";
    // The file name, title and body in each language.
    [SerializeField] private string fileNamePt = "notas.txt";
    [SerializeField] private string titleEn = "";
    [SerializeField] private string titlePt = "";
    [SerializeField, TextArea(6, 30)] private string bodyEn = "";
    [SerializeField, TextArea(6, 30)] private string bodyPt = "";
    [Tooltip("StoryProgress flag that unlocks it (empty = from the start).")]
    [SerializeField] private string requiredFlag = "";
    [Tooltip("Order in the list (lower first).")]
    [SerializeField] private int order;

    // Text or picture.
    [Header("Fake OS (his room)")]
    [SerializeField] private Kind kind = Kind.Text;
    [Tooltip("A picture file: the image (the body is its caption).")]
    [SerializeField] private Texture2D image;
    [Tooltip("The picture with its words in Portuguese (empty = the same one).")]
    [SerializeField] private Texture2D imagePt;
    [Tooltip("The folder it is in (files with the same folder name are together).")]
    [SerializeField] private string folderEn = "Documents";
    // The folder in Portuguese.
    [SerializeField] private string folderPt = "Documentos";
    [Tooltip("In the Recycle Bin instead of a folder.")]
    [SerializeField] private bool inRecycleBin;

    // Read by the desktop / fake OS.
    public string RequiredFlag => requiredFlag;
    public int Order => order;
    public string Id => name;
    public Kind FileKind => kind;
    public Texture2D Image => image;
    // The picture in a language (the English one when there is no Portuguese).
    public Texture2D ImageIn(GameLanguage l) => l == GameLanguage.Portuguese && imagePt != null ? imagePt : image;
    public bool InRecycleBin => inRecycleBin;

    // The folder / file name / title / body in a language (English when the Portuguese one is empty).
    public string Folder(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(folderPt) ? folderPt : folderEn;

    // File name / title / body in a language (English when no Portuguese).
    public string FileName(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(fileNamePt) ? fileNamePt : fileNameEn;
    public string Title(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(titlePt) ? titlePt : titleEn;
    public string Body(GameLanguage l) => l == GameLanguage.Portuguese && !string.IsNullOrEmpty(bodyPt) ? bodyPt : bodyEn;

#if UNITY_EDITOR
    // Editor: sets the content (only for new files).
    public void EditorSet(string fileEn, string filePt, string tEn, string tPt, string bEn, string bPt, string flag, int sortOrder)
    {
        fileNameEn = fileEn; fileNamePt = filePt; titleEn = tEn; titlePt = tPt; bodyEn = bEn; bodyPt = bPt;
        requiredFlag = flag; order = sortOrder;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Editor: sets the kind, picture and folder.
    public void EditorSetPlace(Kind fileKind, Texture2D picture, string folderEnglish, string folderPortuguese, bool recycled)
    {
        kind = fileKind; image = picture; folderEn = folderEnglish; folderPt = folderPortuguese; inRecycleBin = recycled;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Editor: the pictures in each language (also on files that already exist: the art has words in it).
    public void EditorSetPictures(Texture2D english, Texture2D portuguese)
    {
        if (english != null) image = english;
        imagePt = portuguese;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
