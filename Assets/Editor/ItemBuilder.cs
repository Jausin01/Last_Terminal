using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Items and their prefabs: the test items (ammo, medkit, keycard), the weapon items, the item database, the pickup
// prefabs and the weapon prefabs (her loadout: WeaponDef table), plus placing pickups in a scene (other builders).
internal static class ItemBuilder
{
    // Asset folders: item data, pickup / weapon models and prefabs, the item database.
    private const string ItemsFolder = "Assets/Data/Items";
    private const string PickupPropsFolder = "Assets/Art/Props/Pickups";
    private const string PickupPrefabFolder = "Assets/Prefabs/Pickups";
    private const string WeaponPropsFolder = "Assets/Art/Props/Weapons";
    internal const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const string ItemDatabasePath = ItemsFolder + "/ItemDatabase.asset";

    // weapons: asset name (SM_<name>.fbx, T_<name>.png, optional T_<name>_N.png), hold type, smoothness, two-handed,
    // holds in the right-hand frame measured on her animations (aim pose / idle pose). Loadout order = this order.
    // Melee weapons also get a MeleeWeapon: damage, hit radius and the speed of each phase of the swing (feel).
    internal struct WeaponDef
    {
        // Name, hold type, smoothness, two-handed.
        public string Name; public WeaponHoldType Hold; public float Smooth; public bool TwoHanded;
        // Hold poses (aim / idle): grip point, barrel direction, up hint.
        public Vector3 AimGrip, AimBarrel, AimUp, IdleGrip, IdleBarrel, IdleUp;
        // Melee: damage, hit radius, phase speeds and hit-stop.
        public bool Melee; public int Damage; public float Radius, Windup, Strike, Recovery, HitStop;
        // Firearm: ammo item, magazine, pellets, spread, range, fire interval, reload time.
        public bool Ranged; public string AmmoItem; public int Magazine, Pellets; public float Spread, Range, Interval, Reload, Recoil;
        // Reloads shell by shell.
        public bool ReloadPerRound;
        // clip-driven reload: style, prop mesh name in the model, its pose in the left hand (measured in Blender)
        public RangedWeapon.ReloadMode ReloadStyle; public string ReloadProp; public bool Pump;
        public Vector3 CarryPos, CarryFwd, CarryUp;
    }

    // melee holds, measured on the knife / blunt clips (same pose idle and "aim")
    // blade: reverse grip, the tip comes out of the pinky side (the stabs strike pinky first)
    private static readonly Vector3 BladeGrip = new Vector3(0f, 0f, 0.02f);
    private static readonly Vector3 BladeBarrel = new Vector3(0f, -1f, 0f);
    private static readonly Vector3 BladeUp = new Vector3(1f, 0f, 0f);
    // blunt: hammer grip, the head comes out of the thumb side
    private static readonly Vector3 BluntGrip = new Vector3(0f, 0f, 0.02f);
    private static readonly Vector3 BluntBarrel = new Vector3(0f, 1f, 0f);
    private static readonly Vector3 BluntUp = new Vector3(1f, 0f, 0f);

    // A melee weapon definition.
    private static WeaponDef MeleeDef(string name, WeaponHoldType hold, float smooth, int damage, float radius,
                                      float windup, float strike, float recovery, float hitStop)
    {
        bool blade = hold == WeaponHoldType.Blade;
        Vector3 grip = blade ? BladeGrip : BluntGrip;
        Vector3 barrel = blade ? BladeBarrel : BluntBarrel;
        Vector3 up = blade ? BladeUp : BluntUp;
        return new WeaponDef
        {
            Name = name, Hold = hold, Smooth = smooth, TwoHanded = false,
            AimGrip = grip, AimBarrel = barrel, AimUp = up, IdleGrip = grip, IdleBarrel = barrel, IdleUp = up,
            Melee = true, Damage = damage, Radius = radius, Windup = windup, Strike = strike, Recovery = recovery, HitStop = hitStop,
        };
    }

    // Every weapon of hers.
    internal static readonly WeaponDef[] Weapons =
    {
        new WeaponDef
        {
            Name = "Pistol1911", Hold = WeaponHoldType.Pistol, Smooth = 0.45f, TwoHanded = false,
            AimGrip = new Vector3(0f, 0f, 0.025f), AimBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f), AimUp = new Vector3(0.1564f, 0.9877f, 0f),
            IdleGrip = new Vector3(0f, 0f, 0.025f), IdleBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f), IdleUp = new Vector3(0.1564f, 0.9877f, 0f),
            // 7-round magazine, accurate, quick
            Ranged = true, AmmoItem = "HandgunAmmo", Magazine = 7, Damage = 25, Pellets = 1, Spread = 1f, Range = 40f,
            Interval = 0.3f, Reload = 1.6f, ReloadPerRound = false, Recoil = 5f,
            ReloadStyle = RangedWeapon.ReloadMode.Magazine, ReloadProp = "Magazine", CarryPos = new Vector3(-0.0185f, 0.0657f, 0.0653f), CarryFwd = new Vector3(0.0525f, 0.9949f, 0.0865f), CarryUp = new Vector3(-0.9014f, 0.0099f, 0.4328f),
        },
        new WeaponDef
        {
            Name = "DesertEagle", Hold = WeaponHoldType.Pistol, Smooth = 0.5f, TwoHanded = false,
            AimGrip = new Vector3(0f, 0f, 0.025f), AimBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f), AimUp = new Vector3(0.1564f, 0.9877f, 0f),
            IdleGrip = new Vector3(0f, 0f, 0.025f), IdleBarrel = new Vector3(0.9785f, 0.2060f, 0.0014f), IdleUp = new Vector3(0.1564f, 0.9877f, 0f),
            // .50 AE magnum: 7 rounds, huge damage, heavy kick, slow follow-up
            Ranged = true, AmmoItem = "MagnumAmmo", Magazine = 7, Damage = 70, Pellets = 1, Spread = 1.5f, Range = 45f,
            Interval = 0.6f, Reload = 1.8f, ReloadPerRound = false, Recoil = 13f,
            ReloadStyle = RangedWeapon.ReloadMode.Magazine, ReloadProp = "Magazine", CarryPos = new Vector3(-0.0375f, 0.0528f, 0.0711f), CarryFwd = new Vector3(0.0525f, 0.9949f, 0.0865f), CarryUp = new Vector3(-0.9014f, 0.0099f, 0.4328f),
        },
        new WeaponDef
        {
            Name = "ShotgunMossberg", Hold = WeaponHoldType.Shotgun, Smooth = 0.40f, TwoHanded = true,
            AimGrip = new Vector3(0f, 0f, 0.02f), AimBarrel = new Vector3(0.9391f, -0.3360f, 0.0727f), AimUp = new Vector3(0.3634f, 0.9076f, 0.2102f),
            IdleGrip = new Vector3(0f, 0f, 0.02f), IdleBarrel = new Vector3(0.7628f, 0.2461f, 0.5980f), IdleUp = new Vector3(-0.0678f, 0.9715f, -0.2271f),
            // 5-shell tube, 8 pellets x 10 in a 7 deg cone, pump between shots, loaded shell by shell
            Ranged = true, AmmoItem = "ShotgunShells", Magazine = 5, Damage = 10, Pellets = 8, Spread = 7f, Range = 15f,
            Interval = 1.0f, Reload = 0.55f, ReloadPerRound = true, Recoil = 10f,
            ReloadStyle = RangedWeapon.ReloadMode.Shells, ReloadProp = "Shell", Pump = true, CarryPos = new Vector3(0.0190f, -0.0231f, 0.0425f), CarryFwd = new Vector3(0.7033f, 0.7109f, 0.0000f), CarryUp = new Vector3(-0.2917f, 0.2886f, 0.9119f),
        },
        //       name            hold                   smooth dmg radius  windup strike recov  hit-stop
        // blades: knife = fast and weak; cleaver = chopping weight; machete = long reach, quick recovery
        MeleeDef("KitchenKnife", WeaponHoldType.Blade, 0.55f, 15, 0.05f, 1.35f, 1.25f, 1.40f, 0.04f),
        MeleeDef("Cleaver",      WeaponHoldType.Blade, 0.50f, 24, 0.07f, 1.10f, 1.10f, 1.15f, 0.06f),
        MeleeDef("Machete",      WeaponHoldType.Blade, 0.45f, 28, 0.06f, 1.05f, 1.15f, 1.05f, 0.06f),
        // blunt: hammer = short snappy swing; bat = balanced; nail bat = heavier, wider; axe = slow wind-up, huge hit
        MeleeDef("Hammer",       WeaponHoldType.Blunt, 0.40f, 22, 0.06f, 1.45f, 1.30f, 1.40f, 0.07f),
        MeleeDef("Bat",          WeaponHoldType.Blunt, 0.25f, 30, 0.06f, 1.05f, 1.10f, 1.05f, 0.08f),
        MeleeDef("NailBat",      WeaponHoldType.Blunt, 0.20f, 38, 0.08f, 1.00f, 1.05f, 1.00f, 0.09f),
        MeleeDef("Axe",          WeaponHoldType.Blunt, 0.35f, 55, 0.08f, 0.70f, 1.00f, 0.75f, 0.12f),
    };

    // pickups smaller than this get a bigger collider, so they are easy to click / target
    private const float MinClickSize = 0.3f;
    private const float MinClickHeight = 0.15f;

    // pickup props: asset name (SM_<name>.fbx, T_<name>.png, M_<name>), default amount, smoothness
    private static readonly (string name, int amount, float smooth)[] PickupProps =
    {
        ("HandgunAmmo",   15, 0.15f),
        ("ShotgunShells",  6, 0.15f),
        ("Medkit",         1, 0.35f),
        ("Keycard",        1, 0.55f),
        ("MagnumAmmo",     7, 0.20f),
    };

    // The pickup prefabs (models + materials, built in the preview scene of menu 3). Existing prefabs are kept.
    internal static void BuildPickupPrefabs(Scene preview, Shader lit, Dictionary<string, ItemDefinition> items)
    {
        EditorAssets.EnsureFolder(PickupPropsFolder, "Materials");
        EditorAssets.EnsureFolder("Assets", "Prefabs");
        EditorAssets.EnsureFolder("Assets/Prefabs", "Pickups");

        int built = 0;
        foreach (var prop in PickupProps)
        {
            string fbxPath = $"{PickupPropsFolder}/SM_{prop.name}.fbx";
            var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (mi == null) { Debug.LogWarning("[Pickups] Model not found: " + fbxPath); continue; }

            // material (always refreshed) + remap on the model
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PickupPropsFolder}/T_{prop.name}.png");
            Material mat = EditorAssets.MakeMaterial($"{PickupPropsFolder}/Materials/M_{prop.name}.mat", tex, prop.smooth, false, lit);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_" + prop.name), mat);
            mi.SaveAndReimport();

            // prefab (never overwritten: delete it to rebuild)
            string prefabPath = PickupPrefabPath(prop.name);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("[Pickups] Prefab already exists, kept: " + prefabPath);
                continue;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject("Pickup_" + prop.name);
            SceneManager.MoveGameObjectToScene(root, preview);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            visual.name = "Model";
            visual.transform.SetParent(root.transform, false);

            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            var col = root.AddComponent<BoxCollider>();
            Vector3 size = new Vector3(Mathf.Max(b.size.x, MinClickSize), Mathf.Max(b.size.y, MinClickHeight), Mathf.Max(b.size.z, MinClickSize));
            col.size = size;
            col.center = new Vector3(b.center.x, size.y * 0.5f, b.center.z);

            var grabPoint = new GameObject("GrabPoint").transform;
            grabPoint.SetParent(root.transform, false);
            grabPoint.localPosition = new Vector3(b.center.x, b.max.y, b.center.z);

            var pickup = root.AddComponent<Pickup>();
            EditorSerialized.SetReference(pickup, Pickup.ItemField, items[prop.name]);
            EditorSerialized.SetInt(pickup, Pickup.AmountField, prop.amount);
            EditorSerialized.SetReference(pickup, Pickup.VisualField, visual.transform);
            EditorSerialized.SetReference(pickup, Pickup.GrabPointField, grabPoint);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            built++;
        }
        Debug.Log($"[Pickups] {built} prefab(s) built in {PickupPrefabFolder}. Materials in {PickupPropsFolder}/Materials.");
    }

    // Every weapon prefab of her loadout (menu 3).
    internal static void BuildWeaponPrefabs(Scene preview, Shader lit, Dictionary<string, ItemDefinition> items)
    {
        foreach (var def in Weapons) BuildWeaponPrefab(preview, lit, def, items);
    }

    // every item the game knows (saves turn ids back into assets): the test items + the elevator keys
    internal static Dictionary<string, ItemDefinition> AllItems()
    {
        Dictionary<string, ItemDefinition> all = EnsureTestItems();
        foreach (var k in ElevatorBuilder.EnsureKeys()) all["Key_" + k.Key] = k.Value;
        foreach (var def in Weapons) all[WeaponItemName(def.Name)] = EnsureWeaponItem(def.Name);
        return all;
    }

    // The item of a weapon: id = the weapon prefab's name (Weapon_<name>), the lexicon key of the weapon too,
    // so "pick up the knife" finds the pickup and, once she has it, "draw the knife" finds the weapon.
    private static string WeaponItemName(string weapon) => "Weapon_" + weapon;

    // The item of a weapon (story scenes pick weapons up).
    internal static WeaponItem EnsureWeaponItem(string weapon) =>
        GetOrCreateItem<WeaponItem>(WeaponItemName(weapon), WeaponItemName(weapon), weapon, 1);

    // a weapon lying somewhere: its model on its side + the pickup (its item). Null when the model is missing.
    internal static Pickup PlaceWeaponPickup(Transform parent, string weapon, Vector3 pos, float yaw)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponPropsFolder}/SM_{weapon}.fbx");
        WeaponItem item = EnsureWeaponItem(weapon);
        if (model == null)
        {
            Debug.LogWarning($"[Helena] {WeaponPropsFolder}/SM_{weapon}.fbx missing: a cube stands for it.");
            return CreateCubePickup(parent, item, 1, pos + Vector3.up * 0.15f, new Color(0.6f, 0.6f, 0.65f));
        }
        var root = new GameObject("Pickup_" + WeaponItemName(weapon));
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.name = "Model";
        // the models lie along x with their thin side along z: turned so the thin side is up (lying on its side)
        visual.transform.localPosition = new Vector3(0.07f, 0.015f, 0f);   // (their grip end at the origin)
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.04f, 0f);
        box.size = new Vector3(0.3f, 0.08f, 0.2f);
        var pickup = root.AddComponent<Pickup>();
        EditorSerialized.SetReference(pickup, Pickup.ItemField, item);
        EditorSerialized.SetInt(pickup, Pickup.AmountField, 1);
        EditorSerialized.SetReference(pickup, Pickup.VisualField, visual.transform);
        return pickup;
    }

    // a pickup of one of the test items (ammo, medkit, keycard) for other builders
    internal static Pickup PlaceItemPickup(Transform parent, string itemName, int amount, Vector3 pos, float yaw, Color fallback) =>
        PlacePickup(parent, itemName, EnsureTestItems(), amount, pos, yaw, fallback);

    // The test items by id.
    internal static Dictionary<string, ItemDefinition> TestItems() => EnsureTestItems();

    // asset name -> item; creates the test items once, never overwrites tuned ones
    internal static Dictionary<string, ItemDefinition> EnsureTestItems()
    {
        return new Dictionary<string, ItemDefinition>
        {
            { "HandgunAmmo",   GetOrCreateItem<AmmoItem>("HandgunAmmo", "item_ammo_handgun", "Handgun Ammo", 30) },
            { "ShotgunShells", GetOrCreateItem<AmmoItem>("ShotgunShells", "item_ammo_shotgun", "Shotgun Shells", 10) },
            { "Medkit",        GetOrCreateItem<HealingItem>("Medkit", "item_medkit", "Medkit", 3) },   // heals 50 (asset default)
            { "Keycard",       GetOrCreateItem<KeyItem>("Keycard", "item_keycard", "Keycard", 1) },
            { "MagnumAmmo",    GetOrCreateItem<AmmoItem>("MagnumAmmo", "item_ammo_magnum", "Magnum Ammo", 14) },
        };
    }

    private static T GetOrCreateItem<T>(string assetName, string id, string displayName, int maxStack) where T : ItemDefinition
    {
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "Items");
        string path = ItemsFolder + "/" + assetName + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<T>(path);
        if (item != null) return item;   // never overwrite an item you already tuned

        item = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(item, path);
        var so = new SerializedObject(item);
        so.FindProperty(ItemDefinition.IdField).stringValue = id;
        so.FindProperty(ItemDefinition.DisplayNameField).stringValue = displayName;
        so.FindProperty(ItemDefinition.MaxStackField).intValue = maxStack;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return item;
    }

    // Pickup prefab path of an item.
    private static string PickupPrefabPath(string name) => $"{PickupPrefabFolder}/Pickup_{name}.prefab";

    // Weapon prefab path.
    internal static string WeaponPrefabPath(string name) => $"{WeaponPrefabFolder}/Weapon_{name}.prefab";

    // Weapon_<name> prefab: model + Weapon with the markers exported from Blender (GripPoint, Muzzle, UpHint)
    // and the measured holds.
    private static void BuildWeaponPrefab(Scene preview, Shader lit, WeaponDef def, Dictionary<string, ItemDefinition> items)
    {
        string fbxPath = $"{WeaponPropsFolder}/SM_{def.Name}.fbx";
        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[Weapons] Model not found: " + fbxPath); return; }

        // material (always refreshed) + optional normal map
        EditorAssets.EnsureFolder(WeaponPropsFolder, "Materials");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WeaponPropsFolder}/T_{def.Name}.png");
        Material mat = EditorAssets.MakeMaterial($"{WeaponPropsFolder}/Materials/M_{def.Name}.mat", tex, def.Smooth, false, lit);
        string normalPath = $"{WeaponPropsFolder}/T_{def.Name}_N.png";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti)
        {
            if (ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            mat.SetTexture(ShaderIds.BumpMapId, AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat);
        }
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_" + def.Name), mat);
        // other materials of the model with their own texture (e.g. M_ShotgunShell -> T_ShotgunShell.png)
        foreach (Material embedded in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath).OfType<Material>())
        {
            if (embedded.name == "M_" + def.Name || !embedded.name.StartsWith("M_")) continue;
            string extraName = embedded.name.Substring(2);
            var extraTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WeaponPropsFolder}/T_{extraName}.png");
            if (extraTex == null) continue;
            Material extra = EditorAssets.MakeMaterial($"{WeaponPropsFolder}/Materials/M_{extraName}.mat", extraTex, 0.3f, false, lit);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), extra);
        }
        mi.SaveAndReimport();

        string prefabPath = WeaponPrefabPath(def.Name);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            // kept (your tuning stays), only the missing combat component is added
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            bool added = AddCombatComponent(contents, def, items);
            if (added) PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[Weapons] Prefab already exists, kept: " + prefabPath + (added ? " (combat data added)" : ""));
            return;
        }
        EditorAssets.EnsureFolder("Assets", "Prefabs");
        EditorAssets.EnsureFolder("Assets/Prefabs", "Weapons");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var root = new GameObject("Weapon_" + def.Name);
        SceneManager.MoveGameObjectToScene(root, preview);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);

        Transform grip = EditorScene.FindDeep(visual.transform, "GripPoint");
        Transform muzzle = EditorScene.FindDeep(visual.transform, "Muzzle");
        Transform up = EditorScene.FindDeep(visual.transform, "UpHint");
        if (grip == null || muzzle == null || up == null)
        {
            Debug.LogError("[Weapons] Markers GripPoint / Muzzle / UpHint not found in " + fbxPath);
            Object.DestroyImmediate(root);
            return;
        }
        var weapon = root.AddComponent<Weapon>();
        var so = new SerializedObject(weapon);
        so.FindProperty(Weapon.GripPointField).objectReferenceValue = grip;
        so.FindProperty(Weapon.MuzzleField).objectReferenceValue = muzzle;
        so.FindProperty(Weapon.UpHintField).objectReferenceValue = up;
        so.FindProperty(Weapon.HoldTypeField).intValue = (int)def.Hold;
        so.FindProperty(Weapon.AimGripField).vector3Value = def.AimGrip;
        so.FindProperty(Weapon.AimBarrelField).vector3Value = def.AimBarrel;
        so.FindProperty(Weapon.AimUpField).vector3Value = def.AimUp;
        so.FindProperty(Weapon.IdleGripField).vector3Value = def.IdleGrip;
        so.FindProperty(Weapon.IdleBarrelField).vector3Value = def.IdleBarrel;
        so.FindProperty(Weapon.IdleUpField).vector3Value = def.IdleUp;
        so.FindProperty(Weapon.TwoHandedField).boolValue = def.TwoHanded;
        so.ApplyModifiedPropertiesWithoutUndo();
        WeaponGrips.Apply(weapon, def.Name);   // her hands fitted to this weapon (grip point + fingers)

        AddCombatComponent(root, def, items);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Weapons] Prefab built: " + prefabPath + (def.TwoHanded ? " (two-handed)" : "") + (def.Melee ? $" (melee, {def.Damage} dmg)" : "") + (def.Ranged ? $" (firearm, {def.Magazine} rounds)" : ""));
    }

    // MeleeWeapon / RangedWeapon with the values of the def, only if the prefab doesn't have it yet. True if added.
    private static bool AddCombatComponent(GameObject root, WeaponDef def, Dictionary<string, ItemDefinition> items)
    {
        if (def.Melee && !root.TryGetComponent(out MeleeWeapon _))
        {
            var melee = root.AddComponent<MeleeWeapon>();
            var mso = new SerializedObject(melee);
            mso.FindProperty(MeleeWeapon.DamageField).intValue = def.Damage;
            mso.FindProperty(MeleeWeapon.HitRadiusField).floatValue = def.Radius;
            mso.FindProperty(MeleeWeapon.WindupSpeedField).floatValue = def.Windup;
            mso.FindProperty(MeleeWeapon.StrikeSpeedField).floatValue = def.Strike;
            mso.FindProperty(MeleeWeapon.RecoverySpeedField).floatValue = def.Recovery;
            mso.FindProperty(MeleeWeapon.HitStopField).floatValue = def.HitStop;
            mso.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
        if (def.Ranged && !root.TryGetComponent(out RangedWeapon _))
        {
            var ranged = root.AddComponent<RangedWeapon>();
            var rso = new SerializedObject(ranged);
            rso.FindProperty(RangedWeapon.AmmoField).objectReferenceValue = items[def.AmmoItem];
            rso.FindProperty(RangedWeapon.MagazineSizeField).intValue = def.Magazine;
            rso.FindProperty(RangedWeapon.DamageField).intValue = def.Damage;
            rso.FindProperty(RangedWeapon.PelletsField).intValue = def.Pellets;
            rso.FindProperty(RangedWeapon.SpreadField).floatValue = def.Spread;
            rso.FindProperty(RangedWeapon.RangeField).floatValue = def.Range;
            rso.FindProperty(RangedWeapon.FireIntervalField).floatValue = def.Interval;
            rso.FindProperty(RangedWeapon.ReloadTimeField).floatValue = def.Reload;
            rso.FindProperty(RangedWeapon.ReloadPerRoundField).boolValue = def.ReloadPerRound;
            rso.FindProperty(RangedWeapon.RecoilField).floatValue = def.Recoil;
            // clip-driven reload: the prop mesh of the model (magazine / shell), its pose in the left hand
            Transform prop = string.IsNullOrEmpty(def.ReloadProp) ? null : EditorScene.FindDeep(root.transform, def.ReloadProp);
            if (prop != null)
            {
                rso.FindProperty(RangedWeapon.ReloadStyleField).enumValueIndex = (int)def.ReloadStyle;
                rso.FindProperty(RangedWeapon.ReloadPropField).objectReferenceValue = prop;
                rso.FindProperty(RangedWeapon.CarryPositionField).vector3Value = def.CarryPos;
                rso.FindProperty(RangedWeapon.CarryForwardField).vector3Value = def.CarryFwd;
                rso.FindProperty(RangedWeapon.CarryUpField).vector3Value = def.CarryUp;
                if (def.ReloadStyle == RangedWeapon.ReloadMode.Shells) prop.gameObject.SetActive(false);   // only shown in her hand
            }
            else if (def.ReloadStyle != RangedWeapon.ReloadMode.Timer)
                Debug.LogWarning($"[Weapons] '{def.ReloadProp}' not found in {def.Name}: timer reload instead (re-export the model).");
            Transform pump = def.Pump ? EditorScene.FindDeep(root.transform, "Pump") : null;
            if (pump != null) rso.FindProperty(RangedWeapon.PumpField).objectReferenceValue = pump;
            rso.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
        return false;
    }

    // Places a pickup prefab of an item.
    internal static Pickup PlacePickup(Transform parent, string name, Dictionary<string, ItemDefinition> items,
                                      int amount, Vector3 pos, float yaw, Color fallbackColor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath(name));
        if (prefab != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var placed = go.GetComponent<Pickup>();
            EditorSerialized.SetInt(placed, Pickup.AmountField, amount);
            return placed;
        }
        return CreateCubePickup(parent, items[name], amount, pos + Vector3.up * 0.15f, fallbackColor);
    }

    // A cube pickup of an item (no prefab).
    internal static Pickup CreateCubePickup(Transform parent, ItemDefinition item, int amount, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pickup_" + item.name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        var pickup = go.AddComponent<Pickup>();
        EditorSerialized.SetReference(pickup, Pickup.ItemField, item);
        EditorSerialized.SetInt(pickup, Pickup.AmountField, amount);

        Material mat = EditorAssets.GetOrCreateColorMaterial("Pickup_" + item.name, color);
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return pickup;
    }

    // every test item, so saves can turn ids back into assets (refreshed each time)
    internal static ItemDatabase EnsureItemDatabase(Dictionary<string, ItemDefinition> items)
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, ItemDatabasePath);
        }
        EditorSerialized.SetReferenceArray(db, ItemDatabase.ItemsField, items.Values.ToList());
        EditorUtility.SetDirty(db);
        return db;
    }
}
