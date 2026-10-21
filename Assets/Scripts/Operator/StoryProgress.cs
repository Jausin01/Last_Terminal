using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// What the operator has seen happen: story flags ("visited_Casino", "keypad_solved", "first_save", "first_kill"...)
// and counters ("coffee"). The files on his PC unlock with them (OperatorFile.RequiredFlag). Saved with the game
// (SaveSystem), carried between scenes like the rest of her state.
// Set by: entering a scene, the save system, the combat encounter, the keypads (wired by the Last Terminal menus), and
// by anything that calls Set / Add (the coffee machine, the pinball).
public class StoryProgress : MonoBehaviour
{
    public const string SaveSystemField = nameof(saveSystem);
    public const string EncounterField = nameof(encounter);
    public const string KeypadsField = nameof(keypads);

    public const string VisitedPrefix = "visited_";
    public const string ReadPrefix = "read_";
    public const string FirstSave = "first_save";
    public const string FirstKill = "first_kill";
    public const string KeypadSolved = "keypad_solved";

    [Tooltip("Optional: a save = \"first_save\".")]
    [SerializeField] private SaveSystem saveSystem;
    [Tooltip("Optional: a hostile down = \"first_kill\".")]
    [SerializeField] private CombatEncounter encounter;
    [Tooltip("Optional: a code accepted = \"keypad_solved\".")]
    [SerializeField] private Keypad[] keypads = new Keypad[0];

    private readonly HashSet<string> flags = new HashSet<string>();
    private readonly Dictionary<string, int> counters = new Dictionary<string, int>();

    public event Action<string> FlagSet;

    private static string SceneFlag => VisitedPrefix + SceneManager.GetActiveScene().name;

    private void Awake() => flags.Add(SceneFlag);

    private void OnEnable()
    {
        if (saveSystem != null) saveSystem.Saved += OnSaved;
        if (encounter != null) encounter.TargetDown += OnTargetDown;
        foreach (Keypad k in keypads) if (k != null) k.Used += OnKeypadUsed;
    }

    private void OnDisable()
    {
        if (saveSystem != null) saveSystem.Saved -= OnSaved;
        if (encounter != null) encounter.TargetDown -= OnTargetDown;
        foreach (Keypad k in keypads) if (k != null) k.Used -= OnKeypadUsed;
    }

    private void OnSaved(int slot) => Set(FirstSave);
    private void OnTargetDown(Health target) => Set(FirstKill);

    private void OnKeypadUsed(Keypad keypad, Keypad.Result result)
    {
        if (result == Keypad.Result.Accepted) Set(KeypadSolved);
    }

    // empty = always true (a file with no requirement)
    public bool Has(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);

    public void Set(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
        FlagSet?.Invoke(flag);
    }

    public int Count(string key) => counters.TryGetValue(key, out int n) ? n : 0;

    // a counter; flags "<key>_<n>" are set as it passes 1, 5, 10, 25 (e.g. "coffee_5")
    public int Add(string key, int amount = 1)
    {
        int before = Count(key);
        int now = before + amount;
        counters[key] = now;
        foreach (int step in new[] { 1, 5, 10, 25 })
            if (before < step && now >= step) Set($"{key}_{step}");
        return now;
    }

    public void CaptureInto(SaveData data)
    {
        data.storyFlags = new List<string>(flags);
        data.storyCounters = new List<SavedStack>();
        foreach (KeyValuePair<string, int> c in counters) data.storyCounters.Add(new SavedStack(c.Key, c.Value));
    }

    // a save loaded / her state carried in: what it says replaces what is here (this scene stays visited)
    public void Restore(SaveData data)
    {
        if (data.storyFlags != null)
        {
            flags.Clear();
            foreach (string f in data.storyFlags) if (!string.IsNullOrEmpty(f)) flags.Add(f);
            flags.Add(SceneFlag);
        }
        if (data.storyCounters != null)
        {
            counters.Clear();
            foreach (SavedStack c in data.storyCounters) if (c != null && !string.IsNullOrEmpty(c.id)) counters[c.id] = c.amount;
        }
        FlagSet?.Invoke(null);   // listeners refresh (null = many changed)
    }
}
