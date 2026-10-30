using System;
using System.Collections.Generic;
using UnityEngine;

// What the operator has seen happen: story flags ("visited_Casino", "keypad_solved", "first_save", "first_kill"...)
// and counters ("coffee"). The files on his PC unlock with them (OperatorFile.RequiredFlag). Saved with the game
// (SaveSystem), carried between scenes like the rest of her state.
// Set by: entering a scene, the save system, the combat encounter, the keypads (wired by the Last Terminal menus), and
// by anything that calls Set / Add (the coffee machine, the pinball).
// His room and the location loaded over it each have one: the station joins them (Join) so both hold the same flags
// and counters - what the location's save writes includes the room's (the wake-up seen, the coffees), and the PC's
// files see what happened to Helena (first save, first kill...).
public class StoryProgress : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SaveSystemField = nameof(saveSystem);
    public const string EncounterField = nameof(encounter);
    public const string KeypadsField = nameof(keypads);

    // Flag names set by the game itself.
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

    // The flags set and the counters.
    private readonly HashSet<string> flags = new HashSet<string>();
    private readonly Dictionary<string, int> counters = new Dictionary<string, int>();

    // Raised when a flag is set (null = many changed: a load).
    public event Action<string> FlagSet;

    // The other scene's story joined to this one (room <-> location), and a change being passed to it.
    private StoryProgress mirror;
    private bool syncing;

    // Flag of this component's scene ("visited_<scene>"; a location loaded over the room is not the active scene).
    private string SceneFlag => VisitedPrefix + gameObject.scene.name;

    // This scene counts as visited.
    private void Awake() => flags.Add(SceneFlag);

    // Listens to saves, kills and keypads.
    private void OnEnable()
    {
        if (saveSystem != null) saveSystem.Saved += OnSaved;
        if (encounter != null) encounter.TargetDown += OnTargetDown;
        foreach (Keypad k in keypads) if (k != null) k.Used += OnKeypadUsed;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (saveSystem != null) saveSystem.Saved -= OnSaved;
        if (encounter != null) encounter.TargetDown -= OnTargetDown;
        foreach (Keypad k in keypads) if (k != null) k.Used -= OnKeypadUsed;
    }

    // The joined story goes on alone.
    private void OnDestroy()
    {
        if (mirror != null && mirror.mirror == this) mirror.mirror = null;
        mirror = null;
    }

    // Joins the other scene's story (the room's and the location's): both get every flag and the higher of each
    // counter; from now on a change in one is made in the other.
    public void Join(StoryProgress other)
    {
        if (other == null || other == this || mirror == other) return;
        mirror = other;
        other.mirror = this;
        flags.UnionWith(other.flags);
        other.flags.UnionWith(flags);
        foreach (KeyValuePair<string, int> c in other.counters) if (Count(c.Key) < c.Value) counters[c.Key] = c.Value;
        foreach (KeyValuePair<string, int> c in counters) other.counters[c.Key] = c.Value;
        FlagSet?.Invoke(null);
        other.FlagSet?.Invoke(null);
    }

    // A save / a kill / a code accepted set their flags.
    private void OnSaved(int slot) => Set(FirstSave);
    private void OnTargetDown(Health target) => Set(FirstKill);

    // A keypad was used: sets its flag when it opened.
    private void OnKeypadUsed(Keypad keypad, Keypad.Result result)
    {
        if (result == Keypad.Result.Accepted) Set(KeypadSolved);
    }

    // empty = always true (a file with no requirement)
    public bool Has(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);

    // Sets a flag (raises FlagSet the first time).
    public void Set(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
        FlagSet?.Invoke(flag);
        if (mirror != null && !syncing) { syncing = true; mirror.Set(flag); syncing = false; }
    }

    // Value of a counter.
    public int Count(string key) => counters.TryGetValue(key, out int n) ? n : 0;

    // a counter; flags "<key>_<n>" are set as it passes 1, 5, 10, 25 (e.g. "coffee_5")
    public int Add(string key, int amount = 1)
    {
        int before = Count(key);
        int now = before + amount;
        counters[key] = now;
        if (mirror != null && !syncing) mirror.counters[key] = now;
        foreach (int step in new[] { 1, 5, 10, 25 })
            if (before < step && now >= step) Set($"{key}_{step}");
        return now;
    }

    // Writes the flags and counters into a save.
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
        if (mirror != null && !syncing) { syncing = true; mirror.Restore(data); syncing = false; }
    }
}
