using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Save slots as JSON files in Application.persistentDataPath (save_1.json, save_2.json...).
// Saves the partner (position, health, inventory, magazines, weapon), chips / coins, her outfit, the play time and
// the world: what is left of each pickup and the enemies, of this scene and of the scenes she left (GameSession).
// Between scenes (an elevator ride, a save of another scene loaded) her state goes through the GameSession:
// Start picks it up (TransferArrived tells the elevators where she arrives).
// Every reference is assigned in the scene (the Last Terminal menus build it); nothing is searched at runtime.
public class SaveSystem : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string AgentField = nameof(agent);
    public const string HealthField = nameof(health);
    public const string InventoryField = nameof(inventory);
    public const string CombatField = nameof(combat);
    public const string ShooterField = nameof(shooter);
    public const string WeaponHolderField = nameof(weaponHolder);
    public const string ItemDatabaseField = nameof(itemDatabase);
    public const string PickupsField = nameof(pickups);
    public const string ChipBankField = nameof(chipBank);
    public const string OutfitField = nameof(outfit);
    public const string EnemiesField = nameof(enemies);
    public const string SessionField = nameof(session);
    public const string StoryField = nameof(story);

    // Her components saved / restored: agent (position), health, inventory, combat (weapon drawn), shooter (magazines), weapon holder.
    [Header("Partner")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;
    [SerializeField] private Inventory inventory;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private PartnerShooter shooter;
    [SerializeField] private WeaponHolder weaponHolder;

    // Item ids -> assets, and the pickups of this scene.
    [Header("World")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private Pickup[] pickups = new Pickup[0];
    [Tooltip("Optional: the operator's chips and prize coins.")]
    [SerializeField] private ChipBank chipBank;
    [Tooltip("Optional: what she is wearing.")]
    [SerializeField] private PartnerOutfit outfit;
    [Tooltip("Enemies of the scene: who is dead, health and position.")]
    [SerializeField] private EnemyBrain[] enemies = new EnemyBrain[0];
    [Tooltip("Shared by every scene: her state between scenes, the scenes she left.")]
    [SerializeField] private GameSession session;
    [Tooltip("Optional: the story flags (the operator's files).")]
    [SerializeField] private StoryProgress story;

    // Number of slots and the file name prefix.
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string filePrefix = "save_";

    // Play time carried over from the loaded save.
    private float playTimeOffset;

    // Raised after a slot is written / loaded.
    public event Action<int> Saved;
    public event Action<int> Loaded;
    public event Action<string, string> TransferArrived;   // she came from another scene: arrival stop id, the stop she left (null = a save loaded)

    // Slots, total play time, and the active scene.
    public int SlotCount => slotCount;
    public float PlayTime => playTimeOffset + Time.timeSinceLevelLoad;
    private static string SceneName => SceneManager.GetActiveScene().name;

    // File path of a slot.
    private string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"{filePrefix}{slot + 1}.json");

    // Does the slot have a file?
    public bool HasSave(int slot) => File.Exists(PathFor(slot));

    // Takes the state carried from another scene (elevator / save), if any.
    private void Start()
    {
        if (session == null || !session.TakeTransfer(out SaveData data, out string stop, out string origin, out bool fromSave)) return;
        Apply(data, fromSave);
        if (fromSave) Debug.Log($"[Save] Loaded into {SceneName} ({data.location}).");
        TransferArrived?.Invoke(fromSave ? null : stop, fromSave ? null : origin);
    }

    // the slot saved last (-1 = no save yet)
    public int LatestSlot()
    {
        int best = -1;
        string bestTime = null;
        for (int i = 0; i < slotCount; i++)
        {
            if (!TryRead(i, out SaveData d)) continue;
            if (best < 0 || string.CompareOrdinal(d.savedAt, bestTime) > 0) { best = i; bestTime = d.savedAt; }
        }
        return best;
    }

    // Reads a slot's file (false if missing or broken).
    public bool TryRead(int slot, out SaveData data)
    {
        data = null;
        string path = PathFor(slot);
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

    // position / yaw: where she resumes when this save is loaded (a save station passes the spot beside it).
    public bool Save(int slot, string location, Vector3 position, float yaw)
    {
        SaveData data = Capture(location, position, yaw);
        string path = PathFor(slot);
        string temp = path + ".tmp";
        try
        {
            // write a temp file first: a crash mid-write never destroys the old save
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Could not write {path}: {e.Message}");
            return false;
        }
        Debug.Log($"[Save] Slot {slot + 1} saved: {path}");
        Saved?.Invoke(slot);
        return true;
    }

    // Her state and the world, as they are now (also what an elevator ride carries).
    public SaveData Capture(string location, Vector3 position, float yaw)
    {
        var data = new SaveData
        {
            savedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
            playTime = PlayTime,
            location = location,
            scene = SceneName,
            position = position,
            yaw = yaw,
            health = health.Current,
            selectedWeapon = combat.SelectedWeapon,
            armed = combat.IsArmed,
            chips = chipBank != null ? chipBank.Chips : -1,
            coins = chipBank != null ? chipBank.Coins : -1,
            outfit = outfit != null ? outfit.CurrentKey : null,
            ownedOutfits = outfit != null ? new System.Collections.Generic.List<string>(outfit.OwnedKeys) : null,
        };
        if (story != null) story.CaptureInto(data);
        foreach (ItemStack s in inventory.Slots) data.inventory.Add(new SavedStack(s.Item.Id, s.Amount));
        for (int i = 0; i < weaponHolder.LoadoutCount; i++) data.magazines.Add(shooter.GetRounds(i));

        SceneWorld here = CaptureWorld();
        data.pickups = here.pickups;   // this scene (also in worlds: older builds read these two)
        data.enemies = here.enemies;
        data.worlds.Add(here);
        if (session != null)
            foreach (SceneWorld w in session.AllWorlds)
                if (w.scene != here.scene) data.worlds.Add(w);
        return data;
    }

    // Captures this scene's pickups and enemies.
    private SceneWorld CaptureWorld()
    {
        var world = new SceneWorld { scene = SceneName };
        foreach (Pickup p in pickups)
            if (p != null) world.pickups.Add(new SavedStack(p.SaveId, p.Remaining));
        foreach (EnemyBrain e in enemies)
            if (e != null) world.enemies.Add(e.Capture());
        return world;
    }

    // Leaving the scene (elevator): this scene's world is remembered, her state goes along to the next one.
    public void PrepareTransfer(string arrivalStopId, string originStopId)
    {
        if (session == null) { Debug.LogError($"{name}: SaveSystem has no GameSession: nothing carried to the next scene.", this); return; }
        session.RememberWorld(CaptureWorld());
        session.SetTransfer(Capture("", Vector3.zero, 0f), arrivalStopId, originStopId, false);
    }

    // Applies a save to this scene. The caller makes sure she is free (not seated / scripted).
    // A save of another scene: that scene is loaded and the save applied there.
    public bool Load(int slot)
    {
        if (!TryRead(slot, out SaveData data)) return false;
        if (!string.IsNullOrEmpty(data.scene) && data.scene != SceneName)
        {
            if (session != null && Application.CanStreamedLevelBeLoaded(data.scene))
            {
                session.SetTransfer(data, null, null, true);
                Debug.Log($"[Save] Slot {slot + 1} is in scene '{data.scene}': loading it.");
                Loaded?.Invoke(slot);
                SceneManager.LoadScene(data.scene);
                return true;
            }
            Debug.LogWarning($"[Save] Slot {slot + 1} is from scene '{data.scene}' (not in the Build Settings): loading it here.");
        }
        Apply(data, true);
        Debug.Log($"[Save] Slot {slot + 1} loaded ({data.location}, {FormatPlayTime(data.playTime)}).");
        Loaded?.Invoke(slot);
        return true;
    }

    // placeAtSave: she goes where the save says (a save loaded); false = she arrives by elevator (placed by ElevatorTravel)
    private void Apply(SaveData data, bool placeAtSave)
    {
        combat.Holster();
        if (placeAtSave)
        {
            agent.ResetPath();
            agent.Warp(data.position);
            agent.transform.rotation = Quaternion.Euler(0f, data.yaw, 0f);
        }
        health.SetCurrent(data.health);

        inventory.Clear();
        foreach (SavedStack s in data.inventory)
        {
            ItemDefinition item = itemDatabase.Find(s.id);
            if (item != null) inventory.TryAdd(item, s.amount);
            else Debug.LogWarning($"[Save] Unknown item id '{s.id}' (missing from the ItemDatabase).");
        }
        for (int i = 0; i < data.magazines.Count; i++) shooter.SetRounds(i, data.magazines[i]);

        // the worlds: every scene she has been to is remembered; this one is applied
        if (session != null && placeAtSave) session.ForgetWorlds();
        SceneWorld here = null;
        foreach (SceneWorld w in data.worlds)
        {
            if (session != null) session.RememberWorld(w);
            if (w.scene == SceneName) here = w;
        }
        if (here == null && data.scene == SceneName) here = new SceneWorld { scene = SceneName, pickups = data.pickups, enemies = data.enemies };
        if (here == null && session != null) session.TryGetWorld(SceneName, out here);   // been here before (elevator)
        ApplyWorld(here);

        combat.SelectWeapon(data.selectedWeapon);
        if (data.armed) combat.Draw();
        if (chipBank != null && data.chips >= 0) chipBank.Set(data.chips);
        if (chipBank != null && data.coins >= 0) chipBank.SetCoins(data.coins);
        if (outfit != null && data.ownedOutfits != null) outfit.SetOwned(data.ownedOutfits);
        if (outfit != null && !string.IsNullOrEmpty(data.outfit)) outfit.WearKey(data.outfit);
        if (story != null) story.Restore(data);

        playTimeOffset = data.playTime - Time.timeSinceLevelLoad;
    }

    // null = as at the start of the scene (pickups as placed, enemies alive)
    private void ApplyWorld(SceneWorld world)
    {
        if (world == null) return;
        foreach (Pickup p in pickups)
        {
            if (p == null) continue;
            SavedStack saved = world.pickups.Find(x => x.id == p.SaveId);
            if (saved != null) p.Restore(saved.amount);
        }
        foreach (EnemyBrain e in enemies)
            if (e != null) e.Restore(world.enemies.Find(x => x.id == e.SaveId));
    }

    // Play time as h:mm:ss.
    public static string FormatPlayTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}";
    }
}
