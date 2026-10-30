using System;
using System.Collections.Generic;
using UnityEngine;

// One version of the title screen's scene, picked by the newest save (TitleVariants): when its conditions hold, its
// objects are shown and others hidden (a case file on the desk, a candle by the photo, the rain stopped...).
// Conditions: whether there must be a save at all, flags the save must have, flags it must not have.
// Edited in the Inspector of TitleVariants (Title.unity): add the objects to the scene, then a variant that shows them.
[Serializable]
public class TitleVariant
{
    // Whether a save is needed: any state, only without a save (first time), only with one.
    public enum SaveCondition { Any, NoSave, HasSave }

    [Tooltip("Shown in the Inspector only.")]
    public string name = "";
    public SaveCondition save = SaveCondition.Any;
    [Tooltip("Story flags the newest save must have (all of them).")]
    public string[] requiredFlags = new string[0];
    [Tooltip("Story flags that rule this variant out (any of them).")]
    public string[] blockingFlags = new string[0];
    [Tooltip("Turned on / off when the variant applies.")]
    public GameObject[] show = new GameObject[0];
    public GameObject[] hide = new GameObject[0];

    // Do the conditions hold for this save (null = no save)?
    public bool Matches(SaveData newest)
    {
        bool hasSave = newest != null;
        if (save == SaveCondition.NoSave && hasSave) return false;
        if (save == SaveCondition.HasSave && !hasSave) return false;
        List<string> flags = newest != null && newest.storyFlags != null ? newest.storyFlags : new List<string>();
        foreach (string f in requiredFlags)
            if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) return false;
        foreach (string f in blockingFlags)
            if (!string.IsNullOrEmpty(f) && flags.Contains(f)) return false;
        return true;
    }

    // Shows / hides its objects.
    public void Apply()
    {
        foreach (GameObject g in show) if (g != null) g.SetActive(true);
        foreach (GameObject g in hide) if (g != null) g.SetActive(false);
    }
}
