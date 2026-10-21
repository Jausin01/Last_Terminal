using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Last Terminal/7: the enemies, from the K_Burnt model, its clips and the melee weapon pack.
//   Assets/Characters/Enemies/K_Burnt.fbx + Character_Killer_03.png        (model + texture)
//   Assets/Characters/Enemies/Animations/*.fbx                             (clips, EnemyImport)
//   Assets/Art/Props/EnemyWeapons/SM_EW_<Name>.fbx + T_EW_<Name>.png        (weapons, markers GripPoint / Tip / UpHint)
// Builds: material, one Animator controller (+ an override for the killer's upright idle / walk), the enemy types
// (Assets/Data/Enemies, created once: tune them there), the weapon prefabs and the enemy prefabs:
//   Enemy_Zombie, Enemy_Runner, Enemy_Killer (bare hands) and their armed variants (Enemy_Zombie_Pipe, Enemy_Killer_Axe...).
// Prefabs, controller and material are rebuilt in place every run (same assets: scene references survive).
// Last Terminal/4 places some of them in the test scene (PlaceTestEnemies).
public static class EnemyBuilder
{
    private const string Root = "Assets/Characters/Enemies";
    private const string AnimFolder = Root + "/Animations";
    private const string ModelPath = Root + "/K_Burnt.fbx";
    private const string TexturePath = Root + "/Character_Killer_03.png";
    private const string ModelMaterialName = "Character_Killer_03";
    private const string GameTexturePath = Root + "/T_Burnt_Game.png";
    // the texture is painted very dark (average ~9% brightness): lifted for the game lighting / camera filter
    private const float TextureWhitePoint = 0.4f;   // this brightness (and up) becomes white
    private const float TextureGamma = 0.7f;        // < 1 opens the shadows
    private const string ControllerPath = Root + "/EnemyAnimator.controller";
    private const string KillerOverridePath = Root + "/EnemyAnimator_Killer.overrideController";
    private const string WeaponArt = "Assets/Art/Props/EnemyWeapons";
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string WeaponPrefabFolder = "Assets/Prefabs/EnemyWeapons";
    private const string DataFolder = "Assets/Data/Enemies";

    // where the fist closes on the handle, from the hand bone: along hand -> index (x hand length), toward the palm
    private const float GripAlong = 0.5f;
    private const float GripPalm = 0.3f;

    // clips: (file in Animations, clip name)
    private static readonly (string file, string clip) Idle = ("Zombie_Idle", "Zombie_Idle");
    private static readonly (string file, string clip) Walk = ("Zombie_Walk", "Zombie_Walk");
    private static readonly (string file, string clip) Run = ("Zombie_Run", "Zombie_Run");
    private static readonly (string file, string clip) Swipe = ("K_BurntZombie_Attack_1", "K_BurntZombie_Attack_1");
    private static readonly (string file, string clip) Smash = ("K_BurntZombie_Attack", "K_BurntZombie_Attack");
    private static readonly (string file, string clip) Bite = ("Zombie_NeckBite", "Zombie_NeckBite");
    private static readonly (string file, string clip) Scream = ("Zombie_Scream", "Zombie_Scream");
    private static readonly (string file, string clip) Feed = ("Zombie_Feed", "Zombie_Feed");
    private static readonly (string file, string clip) Rise = ("Zombie_Biting", "Zombie_Rise");
    private static readonly (string file, string clip) DeathBack = ("Zombie_Death", "Zombie_Death");
    private static readonly (string file, string clip) DeathFront = ("K_BurntZombie_Dying", "K_BurntZombie_Dying");
    private static readonly (string file, string clip) KillerIdle = ("K_BurntBreathing_Idle", "K_BurntBreathing_Idle");
    private static readonly (string file, string clip) KillerWalk = ("K_BurntWalking", "K_BurntWalking");

    // weapons: damage added to the attack (0 = can't do it with this), extra reach, mass
    private struct WeaponDef
    {
        public string Name;
        public int SwipeDamage, SmashDamage;
        public float Reach, Mass;
    }

    private static readonly WeaponDef[] Weapons =
    {
        new WeaponDef { Name = "Knife", SwipeDamage = 12, SmashDamage = 0, Reach = 0.1f, Mass = 0.3f },
        new WeaponDef { Name = "Bottle", SwipeDamage = 10, SmashDamage = 0, Reach = 0f, Mass = 0.4f },
        new WeaponDef { Name = "Cleaver", SwipeDamage = 16, SmashDamage = 0, Reach = 0.1f, Mass = 0.8f },
        new WeaponDef { Name = "Sickle", SwipeDamage = 16, SmashDamage = 0, Reach = 0.2f, Mass = 0.6f },
        new WeaponDef { Name = "Machete", SwipeDamage = 18, SmashDamage = 20, Reach = 0.3f, Mass = 0.8f },
        new WeaponDef { Name = "Pipe", SwipeDamage = 12, SmashDamage = 18, Reach = 0.4f, Mass = 1.5f },
        new WeaponDef { Name = "Crowbar", SwipeDamage = 14, SmashDamage = 20, Reach = 0.4f, Mass = 1.5f },
        new WeaponDef { Name = "Bat", SwipeDamage = 14, SmashDamage = 20, Reach = 0.5f, Mass = 1f },
        new WeaponDef { Name = "Axe", SwipeDamage = 18, SmashDamage = 30, Reach = 0.5f, Mass = 2f },
        new WeaponDef { Name = "Sledgehammer", SwipeDamage = 0, SmashDamage = 35, Reach = 0.5f, Mass = 5f },
    };

    // the armed variants built for each base enemy
    private static readonly (string enemy, string[] weapons)[] Variants =
    {
        ("Zombie", new[] { "Knife", "Bottle", "Cleaver", "Pipe", "Crowbar", "Bat", "Machete" }),
        ("Runner", new[] { "Knife", "Bottle" }),
        ("Killer", new[] { "Axe", "Sledgehammer", "Machete", "Sickle", "Cleaver" }),
    };

    private struct AttackDef
    {
        public EnemyAttackSlot Slot;
        public float Weight, Range, HitTime, ExitTime, Cooldown;
        public int Damage;
    }

    // hit / exit times measured on the clips (hand speed peak, end of the follow-through)
    private static AttackDef SwipeAttack(int damage, float weight) =>
        new AttackDef { Slot = EnemyAttackSlot.Swipe, Weight = weight, Range = 1.1f, HitTime = 0.4f, ExitTime = 0.68f, Cooldown = 1.2f, Damage = damage };
    private static AttackDef SmashAttack(int damage, float weight) =>
        new AttackDef { Slot = EnemyAttackSlot.Smash, Weight = weight, Range = 1f, HitTime = 0.335f, ExitTime = 0.46f, Cooldown = 1.6f, Damage = damage };
    private static AttackDef BiteAttack(int damage, float weight) =>
        new AttackDef { Slot = EnemyAttackSlot.Bite, Weight = weight, Range = 0.9f, HitTime = 0.24f, ExitTime = 0.72f, Cooldown = 2f, Damage = damage };

    private struct TypeDef
    {
        public string Name, DisplayName, Key;
        public int Health, StaggerDamage;
        public float WalkSpeed, WalkClipSpeed, RunSpeed, RunClipSpeed, TurnSpeed, StepIn;
        public bool Runs, Screams, Killer;
        public float Sight, SightAngle, Hear, FeedHear, Lose, AttackSpeed, StaggerTime, WanderPause;
        public AttackDef[] Attacks;
    }

    // clip speeds measured on the files (planted foot): zombie walk 0.37 m/s, zombie run 2.9 m/s, K_BurntWalking 1.75 m/s
    private static readonly TypeDef[] Types =
    {
        new TypeDef
        {
            Name = "Zombie", DisplayName = "Zombie", Key = "zombie", Health = 80, StaggerDamage = 30,
            WalkSpeed = 0.45f, WalkClipSpeed = 0.37f, RunSpeed = 2.6f, RunClipSpeed = 2.9f, TurnSpeed = 150f, StepIn = 0.4f,
            Runs = false, Screams = true, Sight = 8f, SightAngle = 140f, Hear = 3f, FeedHear = 2f, Lose = 6f,
            AttackSpeed = 1f, StaggerTime = 0.7f, WanderPause = 4f,
            Attacks = new[] { SwipeAttack(10, 1f), SmashAttack(12, 0.6f), BiteAttack(20, 0.6f) },
        },
        new TypeDef
        {
            Name = "Runner", DisplayName = "Runner", Key = "zombie", Health = 60, StaggerDamage = 25,
            WalkSpeed = 0.45f, WalkClipSpeed = 0.37f, RunSpeed = 2.6f, RunClipSpeed = 2.9f, TurnSpeed = 220f, StepIn = 0.35f,
            Runs = true, Screams = true, Sight = 11f, SightAngle = 160f, Hear = 4f, FeedHear = 2.5f, Lose = 8f,
            AttackSpeed = 1.15f, StaggerTime = 0.6f, WanderPause = 3f,
            Attacks = new[] { SwipeAttack(8, 1f), BiteAttack(15, 0.8f) },
        },
        new TypeDef
        {
            Name = "Killer", DisplayName = "Killer", Key = "killer", Health = 160, StaggerDamage = 45,
            WalkSpeed = 1.25f, WalkClipSpeed = 1.75f, RunSpeed = 2.6f, RunClipSpeed = 2.9f, TurnSpeed = 240f, StepIn = 0.3f,
            Runs = false, Screams = false, Sight = 12f, SightAngle = 150f, Hear = 4f, FeedHear = 2f, Lose = 10f,
            AttackSpeed = 1.1f, StaggerTime = 0.5f, WanderPause = 5f, Killer = true,
            Attacks = new[] { SwipeAttack(12, 1f), SmashAttack(15, 1f) },
        },
    };

    [MenuItem("Last Terminal/7 - Build Enemies (Animator, Types, Weapons, Prefabs)")]
    public static void Build()
    {
        Shader lit = HelenaSetupMenu.DefaultLitShader();
        if (lit == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
        {
            Debug.LogError($"[Enemies] Put K_Burnt.fbx and Character_Killer_03.png in {Root}.");
            return;
        }
        // the import rules (EnemyImport) on every model of the folder, also files imported before the script existed
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

        if (!BuildController(out AnimatorController ctrl, out AnimatorOverrideController killer)) return;

        HelenaSetupMenu.EnsureFolder("Assets", "Prefabs");
        HelenaSetupMenu.EnsureFolder("Assets/Prefabs", "Enemies");
        HelenaSetupMenu.EnsureFolder("Assets/Prefabs", "EnemyWeapons");
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Enemies");
        HelenaSetupMenu.EnsureFolder(Root, "Materials");
        HelenaSetupMenu.EnsureFolder(WeaponArt, "Materials");

        // model material
        var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        Material body = HelenaSetupMenu.MakeMaterial(Root + "/Materials/M_Burnt.mat", EnsureGameTexture(), 0.15f, false, lit);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), ModelMaterialName), body);
        mi.SaveAndReimport();
        if (body.GetTexture("_BaseMap") == null) Debug.LogError($"[Enemies] M_Burnt has no texture: check {TexturePath}.");

        Scene preview = EditorSceneManager.NewPreviewScene();
        var weapons = new Dictionary<string, GameObject>();
        foreach (WeaponDef w in Weapons)
        {
            GameObject prefab = BuildWeaponPrefab(preview, lit, w);
            if (prefab != null) weapons[w.Name] = prefab;
        }

        int built = 0;
        foreach (TypeDef t in Types)
        {
            EnemyType type = EnsureType(t);
            GameObject basePrefab = BuildEnemyPrefab(preview, t, type, t.Killer ? (RuntimeAnimatorController)killer : ctrl, body);
            if (basePrefab == null) continue;
            built++;
            foreach (var v in Variants)
            {
                if (v.enemy != t.Name) continue;
                foreach (string weaponName in v.weapons)
                    if (weapons.TryGetValue(weaponName, out GameObject wp) && BuildArmedVariant(preview, basePrefab, wp, $"{PrefabFolder}/Enemy_{t.Name}_{weaponName}.prefab"))
                        built++;
            }
        }
        EditorSceneManager.ClosePreviewScene(preview);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Enemies] {built} enemy prefab(s) in {PrefabFolder}, {weapons.Count} weapon(s) in {WeaponPrefabFolder}, types in {DataFolder}. Run Last Terminal/4 to place them in the test scene.");
    }

    // Character_Killer_03.png with its levels lifted -> T_Burnt_Game.png (the original is kept as it is)
    private static Texture2D EnsureGameTexture()
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(System.IO.File.ReadAllBytes(TexturePath)))
        {
            Debug.LogError("[Enemies] Could not read " + TexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
        Color32[] px = source.GetPixels32();
        var curve = new byte[256];
        for (int i = 0; i < 256; i++)
            curve[i] = (byte)Mathf.RoundToInt(255f * Mathf.Pow(Mathf.Clamp01(i / 255f / TextureWhitePoint), TextureGamma));
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(curve[px[i].r], curve[px[i].g], curve[px[i].b], px[i].a);
        source.SetPixels32(px);
        System.IO.File.WriteAllBytes(GameTexturePath, source.EncodeToPNG());
        Object.DestroyImmediate(source);
        AssetDatabase.ImportAsset(GameTexturePath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(GameTexturePath);
    }

    // ------------------------------------------------------------------ animator
    private static AnimationClip LoadClip((string file, string clip) c)
    {
        string path = $"{AnimFolder}/{c.file}.fbx";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip clip && clip.name == c.clip) return clip;
        Debug.LogError($"[Enemies] Clip '{c.clip}' not found in {path}.");
        return null;
    }

    private static bool BuildController(out AnimatorController ctrl, out AnimatorOverrideController killer)
    {
        ctrl = null;
        killer = null;
        AnimationClip idle = LoadClip(Idle), walk = LoadClip(Walk), run = LoadClip(Run);
        AnimationClip swipe = LoadClip(Swipe), smash = LoadClip(Smash), bite = LoadClip(Bite);
        AnimationClip scream = LoadClip(Scream), feed = LoadClip(Feed), rise = LoadClip(Rise);
        AnimationClip deathBack = LoadClip(DeathBack), deathFront = LoadClip(DeathFront);
        AnimationClip killerIdle = LoadClip(KillerIdle), killerWalk = LoadClip(KillerWalk);
        if (idle == null || walk == null || run == null || swipe == null || smash == null || bite == null || scream == null ||
            feed == null || rise == null || deathBack == null || deathFront == null || killerIdle == null || killerWalk == null)
        {
            Debug.LogError($"[Enemies] Clips missing in {AnimFolder} (see the errors above).");
            return false;
        }

        // rebuilt in place (same asset, same GUID)
        ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ClearController(ctrl);

        ctrl.AddParameter(EnemyAnimator.GaitParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = EnemyAnimator.MoveSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter(new AnimatorControllerParameter { name = EnemyAnimator.AttackSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimatorState locomotion = ctrl.CreateBlendTreeInController(EnemyAnimator.LocomotionState, out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = EnemyAnimator.GaitParam;
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        tree.AddChild(run, 2f);
        locomotion.speedParameterActive = true;
        locomotion.speedParameter = EnemyAnimator.MoveSpeedParam;
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        sm.defaultState = locomotion;

        // single states, entered from code (EnemyAnimator): no transitions
        AnimationClip[] attacks = { swipe, smash, bite };   // EnemyAttackSlot order
        for (int i = 0; i < attacks.Length; i++)
        {
            AnimatorState a = sm.AddState(EnemyAnimator.AttackStates[i], new Vector3(300f, 80f * i, 0f));
            a.motion = attacks[i];
            a.speedParameterActive = true;
            a.speedParameter = EnemyAnimator.AttackSpeedParam;
        }
        AddState(sm, EnemyAnimator.ScreamState, scream, new Vector3(550f, 0f, 0f));
        AddState(sm, EnemyAnimator.FeedState, feed, new Vector3(550f, 80f, 0f));
        AddState(sm, EnemyAnimator.RiseState, rise, new Vector3(550f, 160f, 0f));
        AddState(sm, EnemyAnimator.DeathBackState, deathBack, new Vector3(800f, 0f, 0f));
        AddState(sm, EnemyAnimator.DeathFrontState, deathFront, new Vector3(800f, 80f, 0f));
        EditorUtility.SetDirty(ctrl);

        // the killer: upright breathing idle and walk instead of the zombie's
        killer = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(KillerOverridePath);
        if (killer == null)
        {
            killer = new AnimatorOverrideController(ctrl);
            AssetDatabase.CreateAsset(killer, KillerOverridePath);
        }
        killer.runtimeAnimatorController = ctrl;
        killer[idle] = killerIdle;
        killer[walk] = killerWalk;
        EditorUtility.SetDirty(killer);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static void AddState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState s = sm.AddState(name, position);
        s.motion = clip;
    }

    // empties a controller asset (parameters, layers, states / trees / transitions inside it); one empty base layer
    private static void ClearController(AnimatorController ctrl)
    {
        ctrl.layers = new AnimatorControllerLayer[0];
        ctrl.parameters = new AnimatorControllerParameter[0];
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (sub == null || sub == ctrl) continue;
            AssetDatabase.RemoveObjectFromAsset(sub);
            Object.DestroyImmediate(sub, true);
        }
        ctrl.AddLayer("Base Layer");
    }

    // ------------------------------------------------------------------ types (created once, then tuned in the asset)
    private static EnemyType EnsureType(TypeDef t)
    {
        string path = $"{DataFolder}/EnemyType_{t.Name}.asset";
        var type = AssetDatabase.LoadAssetAtPath<EnemyType>(path);
        if (type != null) return type;
        type = ScriptableObject.CreateInstance<EnemyType>();
        AssetDatabase.CreateAsset(type, path);

        var so = new SerializedObject(type);
        so.FindProperty(EnemyType.DisplayNameField).stringValue = t.DisplayName;
        so.FindProperty(EnemyType.CommandKeyField).stringValue = t.Key;
        so.FindProperty(EnemyType.MaxHealthField).intValue = t.Health;
        so.FindProperty(EnemyType.WalkSpeedField).floatValue = t.WalkSpeed;
        so.FindProperty(EnemyType.WalkClipSpeedField).floatValue = t.WalkClipSpeed;
        so.FindProperty(EnemyType.RunsField).boolValue = t.Runs;
        so.FindProperty(EnemyType.RunSpeedField).floatValue = t.RunSpeed;
        so.FindProperty(EnemyType.RunClipSpeedField).floatValue = t.RunClipSpeed;
        so.FindProperty(EnemyType.TurnSpeedField).floatValue = t.TurnSpeed;
        so.FindProperty(EnemyType.StepInTimeField).floatValue = t.StepIn;
        so.FindProperty(EnemyType.SightRangeField).floatValue = t.Sight;
        so.FindProperty(EnemyType.SightAngleField).floatValue = t.SightAngle;
        so.FindProperty(EnemyType.HearRangeField).floatValue = t.Hear;
        so.FindProperty(EnemyType.FeedingHearRangeField).floatValue = t.FeedHear;
        so.FindProperty(EnemyType.ScreamsOnAlertField).boolValue = t.Screams;
        so.FindProperty(EnemyType.LoseTimeField).floatValue = t.Lose;
        so.FindProperty(EnemyType.AttackSpeedField).floatValue = t.AttackSpeed;
        so.FindProperty(EnemyType.StaggerDamageField).intValue = t.StaggerDamage;
        so.FindProperty(EnemyType.StaggerTimeField).floatValue = t.StaggerTime;
        so.FindProperty(EnemyType.WanderPauseField).floatValue = t.WanderPause;
        SerializedProperty list = so.FindProperty(EnemyType.AttacksField);
        list.arraySize = t.Attacks.Length;
        for (int i = 0; i < t.Attacks.Length; i++)
        {
            AttackDef a = t.Attacks[i];
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(nameof(EnemyType.Attack.slot)).enumValueIndex = (int)a.Slot;
            e.FindPropertyRelative(nameof(EnemyType.Attack.weight)).floatValue = a.Weight;
            e.FindPropertyRelative(nameof(EnemyType.Attack.range)).floatValue = a.Range;
            e.FindPropertyRelative(nameof(EnemyType.Attack.hitTime)).floatValue = a.HitTime;
            e.FindPropertyRelative(nameof(EnemyType.Attack.exitTime)).floatValue = a.ExitTime;
            e.FindPropertyRelative(nameof(EnemyType.Attack.damage)).intValue = a.Damage;
            e.FindPropertyRelative(nameof(EnemyType.Attack.cooldown)).floatValue = a.Cooldown;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return type;
    }

    // ------------------------------------------------------------------ weapons
    // root = the grip (origin of the model), Rigidbody + collider for when it drops, EnemyWeapon (damage, reach)
    private static GameObject BuildWeaponPrefab(Scene preview, Shader lit, WeaponDef def)
    {
        string fbxPath = $"{WeaponArt}/SM_EW_{def.Name}.fbx";
        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (mi == null || model == null) { Debug.LogWarning("[Enemies] Weapon model not found: " + fbxPath); return null; }
        Material mat = HelenaSetupMenu.MakeMaterial($"{WeaponArt}/Materials/M_EW_{def.Name}.mat",
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{WeaponArt}/T_EW_{def.Name}.png"), 0.3f, false, lit);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_EW_" + def.Name), mat);
        mi.SaveAndReimport();

        var root = new GameObject("EW_" + def.Name);
        SceneManager.MoveGameObjectToScene(root, preview);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);

        Transform grip = HelenaSetupMenu.FindDeep(visual.transform, "GripPoint");
        Transform tip = HelenaSetupMenu.FindDeep(visual.transform, "Tip");
        Transform up = HelenaSetupMenu.FindDeep(visual.transform, "UpHint");
        if (grip == null || tip == null || up == null)
        {
            Debug.LogWarning($"[Enemies] {fbxPath}: markers GripPoint / Tip / UpHint missing.");
            Object.DestroyImmediate(root);
            return null;
        }
        visual.transform.localPosition = -root.transform.InverseTransformPoint(grip.position);   // grip on the root

        // collider along the handle -> tip (off while held)
        Vector3 t = root.transform.InverseTransformPoint(tip.position);
        int axis = Mathf.Abs(t.x) >= Mathf.Abs(t.y) && Mathf.Abs(t.x) >= Mathf.Abs(t.z) ? 0 : Mathf.Abs(t.y) >= Mathf.Abs(t.z) ? 1 : 2;
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = axis;
        col.center = t * 0.5f;
        col.radius = 0.05f;
        col.height = t.magnitude + 0.1f;
        col.enabled = false;
        var body = root.AddComponent<Rigidbody>();
        body.mass = def.Mass;
        body.isKinematic = true;

        var weapon = root.AddComponent<EnemyWeapon>();
        var so = new SerializedObject(weapon);
        so.FindProperty(EnemyWeapon.SwipeDamageField).intValue = def.SwipeDamage;
        so.FindProperty(EnemyWeapon.SmashDamageField).intValue = def.SmashDamage;
        so.FindProperty(EnemyWeapon.BiteDamageField).intValue = 0;   // no biting with a weapon in hand
        so.FindProperty(EnemyWeapon.ReachField).floatValue = def.Reach;
        so.FindProperty(EnemyWeapon.DropColliderField).objectReferenceValue = col;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{WeaponPrefabFolder}/EW_{def.Name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ enemies
    private static GameObject BuildEnemyPrefab(Scene preview, TypeDef t, EnemyType type, RuntimeAnimatorController controller, Material body)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = new GameObject("Enemy_" + t.Name);
        SceneManager.MoveGameObjectToScene(root, preview);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.speed = t.WalkSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.6f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.3f;
        var health = root.AddComponent<Health>();
        HelenaSetupMenu.SetInt(health, Health.MaxHealthField, t.Health);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);
        // the body material set on the renderers too: it does not depend on the material name inside the FBX
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = body;
            r.sharedMaterials = mats;
        }
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;   // the agent moves it (EnemyImport: walks extracted, the rest baked)
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var enemyAnimator = visual.AddComponent<EnemyAnimator>();

        var brain = root.AddComponent<EnemyBrain>();
        var so = new SerializedObject(brain);
        so.FindProperty(EnemyBrain.TypeField).objectReferenceValue = type;
        so.FindProperty(EnemyBrain.EnemyAnimatorField).objectReferenceValue = enemyAnimator;
        so.FindProperty(EnemyBrain.BodyColliderField).objectReferenceValue = col;
        so.FindProperty(EnemyBrain.SaveIdField).stringValue = "enemy_" + t.Name.ToLowerInvariant();
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Enemy_{t.Name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // a prefab variant of the base enemy with a weapon in its right hand
    private static bool BuildArmedVariant(Scene preview, GameObject basePrefab, GameObject weaponPrefab, string path)
    {
        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
        Animator animator = enemy.GetComponentInChildren<Animator>();
        Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        Transform index = hand != null ? HelenaSetupMenu.FindDeep(hand, "mixamorig:RightHandIndex1") : null;
        if (index == null && animator != null) index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        if (hand == null || index == null)
        {
            Debug.LogWarning($"[Enemies] {basePrefab.name}: right hand / index bone not found, {path} not built.");
            Object.DestroyImmediate(enemy);
            return false;
        }

        var weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, preview);
        PlaceInHand(weapon.transform, enemy.transform, hand, index);
        weapon.name = weaponPrefab.name;

        var brain = enemy.GetComponent<EnemyBrain>();
        var so = new SerializedObject(brain);
        so.FindProperty(EnemyBrain.WeaponField).objectReferenceValue = weapon.GetComponent<EnemyWeapon>();
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(enemy, path);
        Object.DestroyImmediate(enemy);
        return true;
    }

    // In the bind pose (T-pose: arms out, palms down): the fist around the grip, the weapon pointing forward,
    // its edge / head (UpHint) down - the hold of the attack clips (checked on renders of the swing and the smash).
    private static void PlaceInHand(Transform weapon, Transform enemy, Transform hand, Transform index)
    {
        SkinnedMeshRenderer[] skinned = enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Matrix4x4 handWorld = BindWorld(skinned, hand);
        Matrix4x4 indexWorld = BindWorld(skinned, index);
        Vector3 h = handWorld.GetColumn(3);
        Vector3 k = indexWorld.GetColumn(3);
        float length = (k - h).magnitude;
        Vector3 along = (k - h).normalized;
        Vector3 palm = Vector3.ProjectOnPlane(Vector3.down, along).normalized;
        Vector3 grip = h + along * length * GripAlong + palm * length * GripPalm;

        Vector3 forward = enemy.forward;
        Transform gripMark = HelenaSetupMenu.FindDeep(weapon, "GripPoint");
        Transform tipMark = HelenaSetupMenu.FindDeep(weapon, "Tip");
        Transform upMark = HelenaSetupMenu.FindDeep(weapon, "UpHint");
        Vector3 g = weapon.InverseTransformPoint(gripMark.position);
        Vector3 t = weapon.InverseTransformPoint(tipMark.position) - g;
        Vector3 u = Vector3.ProjectOnPlane(weapon.InverseTransformPoint(upMark.position) - g, t);
        Quaternion rotation = Quaternion.LookRotation(forward, -enemy.up) * Quaternion.Inverse(Quaternion.LookRotation(t, u));
        Matrix4x4 weaponWorld = Matrix4x4.TRS(grip, rotation, Vector3.one) * Matrix4x4.Translate(-g);

        Matrix4x4 local = handWorld.inverse * weaponWorld;   // in the hand bone's space (bind pose)
        weapon.SetParent(hand, false);
        weapon.localPosition = local.GetColumn(3);
        weapon.localRotation = local.rotation;
        weapon.localScale = local.lossyScale;
    }

    // where the bone was in the bind pose (world), from a skinned mesh it drives; the current pose otherwise
    private static Matrix4x4 BindWorld(SkinnedMeshRenderer[] skinned, Transform bone)
    {
        foreach (SkinnedMeshRenderer r in skinned)
        {
            if (r.sharedMesh == null) continue;
            int i = System.Array.IndexOf(r.bones, bone);
            Matrix4x4[] bind = r.sharedMesh.bindposes;
            if (i < 0 || i >= bind.Length) continue;
            return r.transform.localToWorldMatrix * bind[i].inverse;
        }
        Debug.LogWarning($"[Enemies] No bind pose for {bone.name}: weapon placed from the prefab pose.");
        return bone.localToWorldMatrix;
    }

    // ------------------------------------------------------------------ test scene (called by Last Terminal/4)
    private static readonly (string prefab, Vector3 position, float yaw, EnemyStartMode mode, string saveId)[] TestEnemies =
    {
        ("Enemy_Zombie", new Vector3(0f, 0f, 10f), 0f, EnemyStartMode.Feeding, "storage_feeder"),       // storage, back to the door
        ("Enemy_Zombie_Pipe", new Vector3(-3.5f, 0f, 5.8f), 120f, EnemyStartMode.Wander, "storage_pipe"),
        ("Enemy_Killer_Axe", new Vector3(12f, 0f, -2f), 250f, EnemyStartMode.Idle, "office_killer"),     // office, facing the door
        ("Enemy_Runner", new Vector3(-12.5f, 0f, -1.5f), 120f, EnemyStartMode.Idle, "training_runner"),  // training room corner
    };

    // places the test enemies (the prefabs of Last Terminal/7; none if it was not run) and wires them to her
    internal static List<EnemyBrain> PlaceTestEnemies(Scene scene, Transform parent, Transform partner, Health partnerHealth,
                                                      PartnerShooter shooter, CommandTargets targets)
    {
        var placed = new List<EnemyBrain>();
        foreach (var e in TestEnemies)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{e.prefab}.prefab");
            if (prefab == null) { Debug.LogWarning($"[Enemies] {e.prefab} not built (run Last Terminal/7): not placed."); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(e.position, Quaternion.Euler(0f, e.yaw, 0f));
            var brain = go.GetComponent<EnemyBrain>();
            var so = new SerializedObject(brain);
            so.FindProperty(EnemyBrain.StartModeField).enumValueIndex = (int)e.mode;
            so.FindProperty(EnemyBrain.SaveIdField).stringValue = e.saveId;
            so.FindProperty(EnemyBrain.PartnerField).objectReferenceValue = partner;
            so.FindProperty(EnemyBrain.PartnerHealthField).objectReferenceValue = partnerHealth;
            so.FindProperty(EnemyBrain.ShooterField).objectReferenceValue = shooter;
            so.FindProperty(EnemyBrain.TargetsField).objectReferenceValue = targets;
            so.ApplyModifiedPropertiesWithoutUndo();
            placed.Add(brain);
        }
        return placed;
    }
}
