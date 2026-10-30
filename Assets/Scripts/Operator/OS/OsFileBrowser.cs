using System.Collections.Generic;
using UnityEngine;

// My Computer: the folder list, the files of the folder (NEW until read), the Recycle Bin; opening a file in the
// text or picture viewer marks it read.
internal sealed class OsFileBrowser
{
    // The rows made for the folder and file lists.
    private readonly List<GameObject> folderRows = new List<GameObject>();
    private readonly List<GameObject> fileRows = new List<GameObject>();
    // The folder shown.
    private string folder;

    // The OS whose screen this part drives.
    private readonly OperatorOS os;

    public OsFileBrowser(OperatorOS os) { this.os = os; }

    // Opens My Computer at a folder (or the first one).
    internal void OpenExplorer(string startFolder)
    {
        os.CloseStart();
        os.Content.CollectFiles();
        List<string> folders = os.Content.Folders();
        folder = startFolder ?? (folders.Count > 0 ? folders[0] : OsContent.RecycleKey);
        if (os.ExplorerUi.window != null) os.ExplorerUi.window.Open(os.T("My Computer", "Meu Computador"));
        BuildExplorer();
    }

    // Rebuilds the folder and file lists of the explorer.
    internal void BuildExplorer()
    {
        if (os.ExplorerUi.rowTemplate == null) return;
        List<string> folders = os.Content.Folders();
        if (folder == null || !folders.Contains(folder)) folder = folders[0];
        OperatorOS.Clear(folderRows);
        foreach (string key in folders)
        {
            string k = key;
            bool current = key == folder;
            string label = (current ? "<b>> " : "  ") + os.Content.FolderTitle(key) + (current ? "</b>" : "");
            folderRows.Add(os.Row(os.ExplorerUi.folders, label, () => { os.Click(); folder = k; BuildExplorer(); }));
        }
        OperatorOS.Clear(fileRows);
        int count = 0;
        foreach (OperatorFile f in os.Content.UnlockedFiles)
        {
            bool inHere = folder == OsContent.RecycleKey ? f.InRecycleBin : !f.InRecycleBin && f.Folder(os.Language) == folder;
            if (!inHere) continue;
            OperatorFile file = f;
            string tag = f.FileKind == OperatorFile.Kind.Image ? "[IMG] " : "[TXT] ";
            string label = $"<color=#555555>{tag}</color>" + f.FileName(os.Language) + (os.Content.IsRead(OsContent.FileKey(f)) ? "" : $"   <color=#C08A00>{os.T("NEW", "NOVO")}</color>");
            fileRows.Add(os.Row(os.ExplorerUi.list, label, () => { os.Click(); OpenFile(file); }));
            count++;
        }
        if (os.ExplorerUi.path != null)
            os.ExplorerUi.path.text = folder == OsContent.RecycleKey ? "C:\\" + os.T("RECYCLED", "LIXEIRA") : $"C:\\{os.UserName.ToUpperInvariant()}\\{folder.ToUpperInvariant()}";
        if (os.ExplorerUi.empty != null)
            os.ExplorerUi.empty.text = count == 0 ? os.T("(empty)", "(vazia)") : "";
    }

    // Opens a file: picture viewer or text viewer; marks it read.
    internal void OpenFile(OperatorFile f)
    {
        if (f.FileKind == OperatorFile.Kind.Image && os.ViewerUi.image != null)
        {
            if (os.ViewerUi.picture != null)
            {
                os.ViewerUi.picture.texture = f.ImageIn(os.Language);
                os.ViewerUi.picture.color = f.ImageIn(os.Language) != null ? Color.white : Color.black;
            }
            Texture2D pic = f.ImageIn(os.Language);
            if (os.ViewerUi.pictureFitter != null && pic != null)
                os.ViewerUi.pictureFitter.aspectRatio = pic.width / (float)Mathf.Max(1, pic.height);
            if (os.ViewerUi.caption != null) os.ViewerUi.caption.text = f.Body(os.Language);
            os.ViewerUi.image.Open(f.FileName(os.Language));
        }
        else if (os.ViewerUi.text != null)
        {
            string title = f.Title(os.Language);
            if (os.ViewerUi.textBody != null) os.ViewerUi.textBody.text = (string.IsNullOrEmpty(title) ? "" : $"<b>{title}</b>\n\n") + f.Body(os.Language);
            os.ViewerUi.text.Open(f.FileName(os.Language) + " - " + os.T("Notepad", "Bloco de Notas"));
            if (os.ViewerUi.textScroll != null) os.ViewerUi.textScroll.verticalNormalizedPosition = 1f;
        }
        os.Content.MarkRead(OsContent.FileKey(f));
    }
}
