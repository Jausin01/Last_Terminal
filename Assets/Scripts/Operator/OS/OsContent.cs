using System;
using System.Collections.Generic;
using UnityEngine;

// Which files and e-mails the OS shows (their story flag is set) and which were read (story flags, or a local
// set without StoryProgress); the folders of the files, the Recycle Bin, finding a file by name.
internal sealed class OsContent
{
    // Folder key of the Recycle Bin (cannot clash with a real folder name).
    internal const string RecycleKey = "\u0001recycle";
    // The unlocked files and e-mails, and the read marks kept without StoryProgress.
    private readonly List<OperatorFile> unlockedFiles = new List<OperatorFile>();
    private readonly List<OperatorEmail> unlockedMail = new List<OperatorEmail>();
    private readonly HashSet<string> readLocal = new HashSet<string>();

    // The unlocked files (in order) and e-mails (newest first), as of the last Collect.
    internal IReadOnlyList<OperatorFile> UnlockedFiles => unlockedFiles;
    internal IReadOnlyList<OperatorEmail> UnlockedMail => unlockedMail;

    // The OS whose screen this part drives.
    private readonly OperatorOS os;

    public OsContent(OperatorOS os) { this.os = os; }

    // Is a story flag set (empty = always)?
    private bool Unlocked(string flag) => os.Story != null ? os.Story.Has(flag) : string.IsNullOrEmpty(flag);

    // Read marks: story flags (or a local set when there is no StoryProgress).
    internal bool IsRead(string key) => os.Story != null ? os.Story.Has(key) : readLocal.Contains(key);

    // Marks a file / e-mail as read.
    internal void MarkRead(string key)
    {
        if (IsRead(key)) return;
        if (os.Story != null) os.Story.Set(key);   // refreshes (FlagSet)
        else { readLocal.Add(key); os.Refresh(); }
    }

    // The read flag of a file.
    internal static string FileKey(OperatorFile f) => StoryProgress.ReadPrefix + f.Id;

    // The files whose flag is set, in order.
    internal void CollectFiles()
    {
        unlockedFiles.Clear();
        foreach (OperatorFile f in os.Files)
            if (f != null && Unlocked(f.RequiredFlag)) unlockedFiles.Add(f);
        unlockedFiles.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    // the folders in order of their first file, then the Recycle Bin
    internal List<string> Folders()
    {
        var list = new List<string>();
        foreach (OperatorFile f in unlockedFiles)
        {
            if (f.InRecycleBin) continue;
            string name = f.Folder(os.Language);
            if (!list.Contains(name)) list.Add(name);
        }
        list.Add(RecycleKey);
        return list;
    }

    // Display name of a folder (the Recycle Bin is translated).
    internal string FolderTitle(string key) => key == RecycleKey ? os.T("Recycle Bin", "Lixeira") : key;

    // The e-mails whose flag is set (newest first); returns how many are unread.
    internal int CollectMail()
    {
        unlockedMail.Clear();
        int unread = 0;
        foreach (OperatorEmail m in os.Emails)
        {
            if (m == null || !Unlocked(m.RequiredFlag)) continue;
            unlockedMail.Add(m);
            if (!IsRead(OperatorEmail.ReadPrefix + m.Id)) unread++;
        }
        unlockedMail.Sort((a, b) => b.Order.CompareTo(a.Order));   // newest first
        return unread;
    }

    // A file by its name in either language.
    internal OperatorFile FindFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        CollectFiles();
        foreach (OperatorFile f in unlockedFiles)
            if (string.Equals(f.FileName(GameLanguage.English), fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.FileName(GameLanguage.Portuguese), fileName, StringComparison.OrdinalIgnoreCase))
                return f;
        return null;
    }
}
