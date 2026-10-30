using System;
using System.IO;
using UnityEngine;

// The save slot files (JSON in Application.persistentDataPath: <prefix>1.json, <prefix>2.json...): their path, reading
// one, finding the newest. Shared by SaveSystem (in the game) and TitleScreen (the menu, where no SaveSystem runs).
public static class SaveFiles
{
    // File path of a slot (0-based).
    public static string PathFor(string prefix, int slot) => Path.Combine(Application.persistentDataPath, $"{prefix}{slot + 1}.json");

    // Does the slot have a file?
    public static bool Exists(string prefix, int slot) => File.Exists(PathFor(prefix, slot));

    // Reads a slot's file (false if missing or broken).
    public static bool TryRead(string prefix, int slot, out SaveData data)
    {
        data = null;
        string path = PathFor(prefix, slot);
        if (!File.Exists(path)) return false;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
            data = null;
        }
        return data != null;
    }

    // The slot saved last (-1 = no save yet).
    public static int Latest(string prefix, int slotCount)
    {
        int best = -1;
        string bestTime = null;
        for (int i = 0; i < slotCount; i++)
        {
            if (!TryRead(prefix, i, out SaveData d)) continue;
            if (best < 0 || string.CompareOrdinal(d.savedAt, bestTime) > 0) { best = i; bestTime = d.savedAt; }
        }
        return best;
    }
}
