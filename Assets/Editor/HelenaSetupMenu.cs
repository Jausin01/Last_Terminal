using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

// Menu "Last Terminal" — run the items in order (0 → 4; 8 = the casino scene, after 3).
// No scene searches: every object the tool needs is created here and referenced directly.
public static class HelenaSetupMenu
{
    private const string Root = "Assets/Characters/Helena";
    private const string ModelPath = Root + "/Animations/HelenaIdle.fbx";   // the old Helena: her clips' avatar (the idle clip)
    // the character in the scenes: Helena 2 (Model2/Helena2.fbx, menus 18 + 21) when she is there, else the old model
    internal static bool UseHelena2 => AssetDatabase.LoadAssetAtPath<GameObject>(HelenaCompareBuilder.ModelPath) != null;
    internal static string CharacterModelPath => UseHelena2 ? HelenaCompareBuilder.ModelPath : ModelPath;
    private const string WalkPath = Root + "/Animations/HelenaWalking.fbx";
    private const string TurnLeftPath = Root + "/Animations/HelenaTurnLeft.fbx";
    private const string TurnRightPath = Root + "/Animations/HelenaTurnRight.fbx";
    private const string TurnLeft180Path = Root + "/Animations/HelenaTurnLeft180.fbx";
    private const string TurnRight180Path = Root + "/Animations/HelenaTurnRight180.fbx";
    private const string PickupLowPath = Root + "/Animations/HelenaPickUpLowerItem.fbx";
    private const string PickupMediumPath = Root + "/Animations/HelenaPickUpMediumItem.fbx";
    private const string PistolIdlePath = Root + "/Animations/HelenaIdleWithPistol.fbx";
    private const string PistolAimPath = Root + "/Animations/HelenaAimWithPistol.fbx";
    private const string PistolStrafeLeftPath = Root + "/Animations/HelenaStrafeWithPistolLeft.fbx";
    private const string PistolStrafeRightPath = Root + "/Animations/HelenaStrafeWithPistolRight.fbx";
    private const string PistolBackPath = Root + "/Animations/HelenaWalkBackwardsWithPistol.fbx";
    private const string ShotgunIdlePath = Root + "/Animations/HelenaShotgunIdle.fbx";
    private const string ShotgunAimPath = Root + "/Animations/HelenaShotgunGunplay.fbx";
    private const string KnifeIdlePath = Root + "/Animations/HelenaKnifeIdle.fbx";
    private const string KnifeStab1Path = Root + "/Animations/HelenaKnifeStabbing1.fbx";
    private const string KnifeStab2Path = Root + "/Animations/HelenaKnifeStabbing2.fbx";
    private const string BluntStrikePath = Root + "/Animations/HelenaBluntStrike.fbx";
    private const string BluntStrikeClip = "HelenaBluntStrike";   // clips cut by HelenaImport
    private const string BluntReadyClip = "HelenaBluntReady";
    private const string ReloadPistolPath = Root + "/Animations/HelenaReloadPistol.fbx";
    private const string ReloadShotgunPath = Root + "/Animations/HelenaShotgunReloadLoop.fbx";
    // the pistol reload clip is long (3.7 s): played faster
    private const float ReloadPistolSpeed = 1.5f;
    private const string StandToSitPath = Root + "/Animations/HelenaStandToSit.fbx";
    private const string FloorSitPath = Root + "/Animations/HelenaSittingPose.fbx";
    private const string TypingPath = Root + "/Animations/HelenaComputerTyping.fbx";
    private const string SitToStandPath = Root + "/Animations/HelenaSitToStand.fbx";
    private const string DoorInwardsPath = Root + "/Animations/" + PartnerAnimator.DoorInwardsClip + ".fbx";
    private const string DoorOutwardsPath = Root + "/Animations/" + PartnerAnimator.DoorOutwardsClip + ".fbx";
    private const string DoorLockedPath = Root + "/Animations/" + PartnerAnimator.DoorLockedClip + ".fbx";
    // seated dealer clips (blackjack), state speeds
    private static readonly float[] DealerSpeeds = { 1f, 1.3f, 1.35f, 1f, 1.3f, 1.9f };
    // evasion clips (dodge, duck), state speeds
    private static readonly float[] EvadeSpeeds = { 1.4f, 1.5f };
    private const string UpperBodyMaskPath = Root + "/UpperBody.mask";
    private const string ControllerPath = Root + "/PartnerAnimator.controller";
    private const string SceneFolder = "Assets/Scenes";
    private const string ScenePath = SceneFolder + "/HelenaTest.unity";
    private const string NavMeshPath = SceneFolder + "/HelenaTest_NavMesh.asset";
    private const string ItemsFolder = "Assets/Data/Items";
    private const string TestMaterialsFolder = "Assets/Data/TestMaterials";
    private const string PickupPropsFolder = "Assets/Art/Props/Pickups";
    private const string PickupPrefabFolder = "Assets/Prefabs/Pickups";
    private const string WeaponPropsFolder = "Assets/Art/Props/Weapons";
    private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const string SavePointPropsFolder = "Assets/Art/Props/SavePoint";
    private const string SavePointPrefabFolder = "Assets/Prefabs/SavePoints";
    private const string SavePointBasePath = SavePointPrefabFolder + "/SavePoint_Base.prefab";
    private const string ItemDatabasePath = ItemsFolder + "/ItemDatabase.asset";
    private const string UiDataFolder = "Assets/Data/UI";
    private const string ScanlinesPath = UiDataFolder + "/Scanlines.asset";

    // weapons: asset name (SM_<name>.fbx, T_<name>.png, optional T_<name>_N.png), hold type, smoothness, two-handed,
    // holds in the right-hand frame measured on her animations (aim pose / idle pose). Loadout order = this order.
    // Melee weapons also get a MeleeWeapon: damage, hit radius and the speed of each phase of the swing (feel).
    private struct WeaponDef
    {
        public string Name; public WeaponHoldType Hold; public float Smooth; public bool TwoHanded;
        public Vector3 AimGrip, AimBarrel, AimUp, IdleGrip, IdleBarrel, IdleUp;
        public bool Melee; public int Damage; public float Radius, Windup, Strike, Recovery, HitStop;
        public bool Ranged; public string AmmoItem; public int Magazine, Pellets; public float Spread, Range, Interval, Reload, Recoil;
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

    private static readonly WeaponDef[] Weapons =
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

    // melee clips: exit time = where the swing is back to a guard pose (measured)
    private const float KnifeStab1Exit = 0.55f;
    private const float KnifeStab2Exit = 0.62f;
    private const float BluntStrikeExit = 0.92f;

    // measured from the planted foot of HelenaWalking (in place): ~1.8 m/s
    public const float WalkSpeed = 1.8f;
    // pistol clips, measured the same way (they are faster than the walk: jog cadence)
    private const float PistolStrafeLeftSpeed = 2.39f;
    private const float PistolStrafeRightSpeed = 2.66f;
    private const float PistolBackSpeed = 3.59f;

    // pickups smaller than this get a bigger collider, so they are easy to click / target
    private const float MinClickSize = 0.3f;
    private const float MinClickHeight = 0.15f;

    // material name inside the FBX -> texture file, smoothness, alpha clip
    private static readonly (string mat, string tex, float smooth, bool clip)[] Materials =
    {
        ("jacket",         "Jacket",   0.50f, false),
        ("shirt",          "Shirt",    0.15f, false),
        ("suit",           "Suit",     0.65f, false),
        ("sneakers",       "Sneakers", 0.20f, false),
        ("skin",           "Skin",     0.25f, false),
        ("hair",           "Hair",     0.30f, true),
        ("Miscelaneo.001", null,       0.00f, false),
    };

    // pickup props: asset name (SM_<name>.fbx, T_<name>.png, M_<name>), default amount, smoothness
    private static readonly (string name, int amount, float smooth)[] PickupProps =
    {
        ("HandgunAmmo",   15, 0.15f),
        ("ShotgunShells",  6, 0.15f),
        ("Medkit",         1, 0.35f),
        ("Keycard",        1, 0.55f),
        ("MagnumAmmo",     7, 0.20f),
    };

    // save point models (Assets/Art/Props/SavePoint): material name inside the FBX -> texture, smoothness, emissive
    private static readonly (string mat, string tex, float smooth, bool emissive)[] SavePointMaterials =
    {
        ("M_SaveStation",      "T_SaveStation",      0.25f, false),
        ("M_SaveDesk_Wood",    "T_SaveDesk_Wood",    0.35f, false),
        ("M_SaveChair_Office", "T_SaveChair_Office", 0.20f, false),
        ("M_SavePC_Retro",     "T_SavePC_Retro",     0.30f, false),
        ("M_SaveScreen",       "T_SaveScreen",       0.80f, true),
    };

    // save point variants: prefab name, location shown on the save screen, desk / computer / chair models (SM_*.fbx)
    private static readonly (string name, string location, string desk, string pc, string chair)[] SavePointVariants =
    {
        ("SavePoint_Wood",     "Manager's Office", "SM_SaveDesk_Wood",  "SM_SavePC_Retro",    "SM_SaveChair_Office"),
        ("SavePoint_Office",   "Office",           "SM_SaveDesk_Metal", "SM_SavePC_Beige",    "SM_SaveChair_Fabric"),
        ("SavePoint_Security", "Security Room",    "SM_SaveDesk_Metal", "SM_SavePC_Security", "SM_SaveChair_Office"),
        ("SavePoint_Terminal", "Server Room",      "SM_SaveDesk_Wood",  "SM_SavePC_Terminal", "SM_SaveChair_Fabric"),
    };

    // shader property ids
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int CullId = Shader.PropertyToID("_Cull");
    private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [MenuItem("Last Terminal/0 - Reimport Helena Assets")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("[Helena] Reimported models with Humanoid + clip settings.");
    }

    [MenuItem("Last Terminal/1 - Setup Helena Materials")]
    public static void SetupMaterials()
    {
        var mi = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (mi == null) { Debug.LogError("[Helena] Model not found: " + ModelPath); return; }

        Shader lit = DefaultLitShader();
        if (lit == null) return;

        EnsureFolder(Root, "Materials");
        var hairTex = AssetImporter.GetAtPath(Root + "/Textures/Hair.png") as TextureImporter;
        if (hairTex != null && !hairTex.alphaIsTransparency)
        {
            hairTex.alphaIsTransparency = true;
            hairTex.SaveAndReimport();
        }

        foreach (var entry in Materials)
        {
            string matPath = Root + "/Materials/" + entry.mat.Replace(".", "_") + ".mat";
            Texture2D tex = entry.tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + entry.tex + ".png") : null;
            Material mat = MakeMaterial(matPath, tex, entry.smooth, entry.clip, lit);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.mat), mat);
        }
        mi.SaveAndReimport();
        AssetDatabase.SaveAssets();
        Debug.Log("[Helena] Materials created in " + Root + "/Materials and remapped on the model.");
    }

    [MenuItem("Last Terminal/2 - Build Partner Animator")]
    public static void BuildAnimator()
    {
        MissingClips.Clear();
        AnimationClip idle = LoadClip(ModelPath);
        AnimationClip walk = LoadClip(WalkPath);
        AnimationClip turnL = LoadClip(TurnLeftPath);
        AnimationClip turnR = LoadClip(TurnRightPath);
        AnimationClip turnL180 = LoadClip(TurnLeft180Path);
        AnimationClip turnR180 = LoadClip(TurnRight180Path);
        AnimationClip pickLow = LoadClip(PickupLowPath);
        AnimationClip pickMedium = LoadClip(PickupMediumPath);
        AnimationClip pistolIdle = LoadClip(PistolIdlePath);
        AnimationClip pistolAim = LoadClip(PistolAimPath);
        AnimationClip pistolLeft = LoadClip(PistolStrafeLeftPath);
        AnimationClip pistolRight = LoadClip(PistolStrafeRightPath);
        AnimationClip pistolBack = LoadClip(PistolBackPath);
        AnimationClip shotgunIdle = LoadClip(ShotgunIdlePath);
        AnimationClip shotgunAim = LoadClip(ShotgunAimPath);
        AnimationClip knifeIdle = LoadClip(KnifeIdlePath);
        AnimationClip knifeStab1 = LoadClip(KnifeStab1Path);
        AnimationClip knifeStab2 = LoadClip(KnifeStab2Path);
        AnimationClip bluntStrike = LoadClip(BluntStrikePath, BluntStrikeClip);
        AnimationClip bluntReady = LoadClip(BluntStrikePath, BluntReadyClip);
        AnimationClip standToSit = LoadClip(StandToSitPath);
        AnimationClip typing = LoadClip(TypingPath);
        AnimationClip sitToStand = LoadClip(SitToStandPath);
        AnimationClip reloadPistol = LoadClip(ReloadPistolPath);
        AnimationClip reloadShotgun = LoadClip(ReloadShotgunPath);
        AnimationClip doorInwards = LoadClip(DoorInwardsPath);
        AnimationClip doorOutwards = LoadClip(DoorOutwardsPath);
        AnimationClip doorLocked = LoadClip(DoorLockedPath);
        var dealerClips = new AnimationClip[PartnerAnimator.DealerClips.Length];
        bool dealerOk = true;
        for (int i = 0; i < dealerClips.Length; i++)
        {
            dealerClips[i] = LoadClip(Root + "/Animations/" + PartnerAnimator.DealerClips[i] + ".fbx");
            dealerOk &= dealerClips[i] != null;
        }
        // hurt: limping locomotion, hit reactions (unarmed + one per weapon hold), death
        AnimationClip injuredIdle = LoadClip(Root + "/Animations/HelenaInjuredHurtingIdle.fbx");
        AnimationClip injuredWalk = LoadClip(Root + "/Animations/HelenaInjuredWalk.fbx");
        AnimationClip injuredBack = LoadClip(Root + "/Animations/HelenaInjuredWalkBackwards.fbx");
        AnimationClip death = LoadClip(Root + "/Animations/" + PartnerAnimator.DeathClip + ".fbx");
        AnimationClip fallen = LoadClip(Root + "/Animations/" + PartnerAnimator.FallenClip + ".fbx");
        bool hurtOk = injuredIdle != null && injuredWalk != null && injuredBack != null && death != null && fallen != null;
        var hitClips = new List<AnimationClip>();
        foreach (string name in PartnerAnimator.HitClips) hitClips.Add(LoadClip(Root + "/Animations/" + name + ".fbx"));
        foreach (string prefix in PartnerAnimator.ArmedHitPrefixes)
            foreach (string hold in PartnerAnimator.HoldNames) hitClips.Add(LoadClip(Root + "/Animations/" + prefix + "_" + hold + ".fbx"));
        hurtOk &= hitClips.TrueForAll(c => c != null);
        var evadeClips = new AnimationClip[PartnerAnimator.EvadeClips.Length];
        for (int i = 0; i < evadeClips.Length; i++)
        {
            evadeClips[i] = LoadClip(Root + "/Animations/" + PartnerAnimator.EvadeClips[i] + ".fbx");
            dealerOk &= evadeClips[i] != null;
        }
        // keypad: three clips cut from HelenaButton_Pushing (HelenaImport)
        var keypadClips = new AnimationClip[PartnerAnimator.KeypadClips.Length];
        for (int i = 0; i < keypadClips.Length; i++)
        {
            string keypadPath = Root + "/Animations/" + PartnerAnimator.KeypadClipFile + ".fbx";
            keypadClips[i] = LoadClip(keypadPath, PartnerAnimator.KeypadClips[i]);
            if (keypadClips[i] == null) MissingClips.Add(keypadPath + " (" + PartnerAnimator.KeypadClips[i] + ")");
            dealerOk &= keypadClips[i] != null;
        }
        if (idle == null || walk == null || turnL == null || turnR == null || turnL180 == null || turnR180 == null ||
            pickLow == null || pickMedium == null ||
            pistolIdle == null || pistolAim == null || pistolLeft == null || pistolRight == null || pistolBack == null ||
            shotgunIdle == null || shotgunAim == null ||
            knifeIdle == null || knifeStab1 == null || knifeStab2 == null || bluntStrike == null || bluntReady == null ||
            standToSit == null || typing == null || sitToStand == null || reloadPistol == null || reloadShotgun == null ||
            doorInwards == null || doorOutwards == null || doorLocked == null || !dealerOk || !hurtOk)
        {
            Debug.LogError("[Helena] Clips not found (" + MissingClips.Count + "), run 'Last Terminal/0 - Reimport Helena Assets' after fixing:\n" + string.Join("\n", MissingClips));
            return;
        }

        // rebuilt in place (same asset, same GUID): the Animator in the scene keeps its controller after this menu
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ClearController(ctrl);
        ctrl.AddParameter(PartnerAnimator.SpeedParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.TurnParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.TurnAngleParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.PickupParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.PickupHeightParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(PartnerAnimator.ArmedParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.AimingParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.MoveXParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.MoveZParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.WeaponTypeParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.AttackParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.AttackIdParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(PartnerAnimator.SeatedParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.ReloadingParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.ReloadAbortParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.AttackSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });
        ctrl.AddParameter(PartnerAnimator.SeatActivityParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.DoorSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });
        ctrl.AddParameter(PartnerAnimator.InjuredParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.EvadeSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });

        // IK pass on the base layer: hand IK + look-at while picking up
        AnimatorControllerLayer[] layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;

        AnimatorState locomotion = ctrl.CreateBlendTreeInController(PartnerAnimator.LocomotionState, out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = PartnerAnimator.SpeedParam;
        tree.useAutomaticThresholds = false;
        // each one blends to its injured version by Injured (0 fine .. 1 limping)
        HurtBlend(tree.CreateBlendTreeChild(0f), idle, injuredIdle);
        HurtBlend(tree.CreateBlendTreeChild(WalkSpeed), walk, injuredWalk);
        ctrl.layers[0].stateMachine.defaultState = locomotion;

        // turn in place: one state, the clip is picked by TurnAngle (exact thresholds)
        AnimatorState turn = ctrl.CreateBlendTreeInController("Turn", out BlendTree turnTree, 0);
        turn.tag = PartnerAnimator.TurnTag;
        turnTree.blendType = BlendTreeType.Simple1D;
        turnTree.blendParameter = PartnerAnimator.TurnAngleParam;
        turnTree.useAutomaticThresholds = false;
        turnTree.AddChild(turnL180, PartnerAnimator.SlotLeft180);
        turnTree.AddChild(turnL, PartnerAnimator.SlotLeft90);
        turnTree.AddChild(turnR, PartnerAnimator.SlotRight90);
        turnTree.AddChild(turnR180, PartnerAnimator.SlotRight180);

        // armed locomotion: 2D blend by local velocity, each clip placed at its real speed
        AnimatorState armedLocomotion = ctrl.CreateBlendTreeInController(PartnerAnimator.ArmedLocomotionState, out BlendTree armedTree, 0);
        armedTree.blendType = BlendTreeType.FreeformDirectional2D;
        armedTree.blendParameter = PartnerAnimator.MoveXParam;
        armedTree.blendParameterY = PartnerAnimator.MoveZParam;
        armedTree.AddChild(pistolIdle, Vector2.zero);
        HurtBlend(armedTree.CreateBlendTreeChild(new Vector2(0f, WalkSpeed)), walk, injuredWalk);   // no pistol walk: legs walk, the upper-body layer holds the gun
        HurtBlend(armedTree.CreateBlendTreeChild(new Vector2(0f, -PistolBackSpeed)), pistolBack, injuredBack);
        armedTree.AddChild(pistolLeft, new Vector2(-PistolStrafeLeftSpeed, 0f));
        armedTree.AddChild(pistolRight, new Vector2(PistolStrafeRightSpeed, 0f));

        AnimatorStateTransition toArmed = locomotion.AddTransition(armedLocomotion);
        toArmed.hasExitTime = false;
        toArmed.duration = 0.25f;
        toArmed.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ArmedParam);
        AnimatorStateTransition toUnarmed = armedLocomotion.AddTransition(locomotion);
        toUnarmed.hasExitTime = false;
        toUnarmed.duration = 0.25f;
        toUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.ArmedParam);

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition toTurn = source.AddTransition(turn);
            toTurn.hasExitTime = false;
            toTurn.duration = 0.15f;
            toTurn.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.TurnParam);
        }

        AnimatorStateTransition toLocomotion = turn.AddTransition(locomotion);
        toLocomotion.hasExitTime = true;
        toLocomotion.exitTime = 0.85f;
        toLocomotion.duration = 0.2f;

        // pick up: one state per height, chosen by PickupHeight
        AddPickupState(ctrl, locomotion, armedLocomotion, "PickupLow", pickLow, PickupHeight.Low, new Vector3(300f, 120f, 0f));
        AddPickupState(ctrl, locomotion, armedLocomotion, "PickupMedium", pickMedium, PickupHeight.Medium, new Vector3(300f, 200f, 0f));

        // melee: full-body swings chosen by AttackId, speed driven by AttackSpeed (phases of each weapon)
        AnimatorState stab1 = AddAttackState(ctrl, locomotion, armedLocomotion, "KnifeStab1", knifeStab1, PartnerMelee.KnifeStab1, new Vector3(550f, 0f, 0f));
        AnimatorState stab2 = AddAttackState(ctrl, locomotion, armedLocomotion, "KnifeStab2", knifeStab2, PartnerMelee.KnifeStab2, new Vector3(550f, 80f, 0f));
        AnimatorState blunt = AddAttackState(ctrl, locomotion, armedLocomotion, "BluntStrike", bluntStrike, PartnerMelee.BluntStrike, new Vector3(550f, 160f, 0f));
        // blade combo: stab -> other stab directly (PartnerMelee only fires it after the strike), before the exits
        AddComboTransition(stab1, stab2, PartnerMelee.KnifeStab2);
        AddComboTransition(stab2, stab1, PartnerMelee.KnifeStab1);
        AddAttackExit(stab1, armedLocomotion, KnifeStab1Exit);
        AddAttackExit(stab2, armedLocomotion, KnifeStab2Exit);
        AddAttackExit(blunt, armedLocomotion, BluntStrikeExit);

        // seat (save terminal): sit down -> typing loop while Seated -> stand up. Root motion applied by PartnerAnimator.
        AddSeatStates(ctrl, locomotion, armedLocomotion, standToSit, typing, sitToStand, dealerClips);

        // doors: entered / left from code (PartnerDoorUser via PartnerAnimator), played forward or backward (DoorSpeed)
        AddDoorState(ctrl, PartnerAnimator.DoorInwardsState, doorInwards, new Vector3(300f, 460f, 0f));
        AddDoorState(ctrl, PartnerAnimator.DoorOutwardsState, doorOutwards, new Vector3(550f, 460f, 0f));
        AddDoorState(ctrl, PartnerAnimator.DoorLockedState, doorLocked, new Vector3(800f, 460f, 0f));
        // keypad states, entered from code (PartnerKeypadUser): no transitions, each holds its last frame
        for (int i = 0; i < keypadClips.Length; i++)
        {
            AnimatorState k = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.KeypadStates[i], new Vector3(300f + 250f * i, 540f, 0f));
            k.motion = keypadClips[i];
            k.tag = PartnerAnimator.KeypadTag;
        }

        // hit reactions and death: entered from code (PartnerDamage via PartnerAnimator); hits end from code too
        for (int i = 0; i < hitClips.Count; i++)
        {
            AnimatorState hit = ctrl.layers[0].stateMachine.AddState(hitClips[i].name, new Vector3(1300f + 220f * (i / 4), 60f + 70f * (i % 4), 0f));
            hit.motion = hitClips[i];
            hit.tag = PartnerAnimator.HitTag;
            hit.speed = 1.15f;
        }
        AnimatorState deathState = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.DeathState, new Vector3(1300f, 400f, 0f));
        deathState.motion = death;
        deathState.tag = PartnerAnimator.DeadTag;
        // sitting on the floor while the operator is away (PartnerRest): optional, from HelenaSittingPose.fbx
        AnimationClip floorSit = LoadClip(FloorSitPath, PartnerAnimator.FloorSitClip);
        if (floorSit != null)
        {
            AnimatorState sitFloor = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.FloorSitState, new Vector3(1300f, 540f, 0f));
            sitFloor.motion = floorSit;
        }
        else Debug.LogWarning("[Helena] " + FloorSitPath + " not found: she stays standing while the operator is away (copy HelenaSittingPose.fbx, run menu 0 and 2 again).");
        // cutscenes: two empty states whose clips are swapped at runtime (any clip plays through them)
        CutsceneBuilder.AddSlotStates(ctrl, new Vector3(1300f, 620f, 0f));
        AnimatorState fallenState = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.FallenState, new Vector3(1300f, 470f, 0f));
        fallenState.motion = fallen;
        fallenState.tag = PartnerAnimator.DeadTag;

        // evasion: dodge / duck, entered and left from code (PartnerEvasion), the duck played backward to get up (EvadeSpeed)
        for (int i = 0; i < evadeClips.Length; i++)
        {
            AnimatorState evade = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.EvadeStates[i], new Vector3(1050f, 300f + 80f * i, 0f));
            evade.motion = evadeClips[i];
            evade.tag = PartnerAnimator.EvadeTag;
            evade.speed = EvadeSpeeds[i];
            evade.speedParameter = PartnerAnimator.EvadeSpeedParam;
            evade.speedParameterActive = true;
        }

        // upper body: weapon idle / aim over any locomotion (weight set by PartnerAnimator), pose picked by WeaponType
        AvatarMask mask = CreateUpperBodyMask();
        ctrl.AddLayer(PartnerAnimator.UpperBodyLayer);
        AnimatorControllerLayer[] allLayers = ctrl.layers;
        int upper = allLayers.Length - 1;
        allLayers[upper].avatarMask = mask;
        allLayers[upper].defaultWeight = 0f;
        allLayers[upper].blendingMode = AnimatorLayerBlendingMode.Override;
        ctrl.layers = allLayers;

        AnimatorState weaponIdleState = ctrl.CreateBlendTreeInController("WeaponIdle", out BlendTree idleByWeapon, upper);
        idleByWeapon.blendType = BlendTreeType.Simple1D;
        idleByWeapon.blendParameter = PartnerAnimator.WeaponTypeParam;
        idleByWeapon.useAutomaticThresholds = false;
        idleByWeapon.AddChild(pistolIdle, (float)WeaponHoldType.Pistol);
        idleByWeapon.AddChild(shotgunIdle, (float)WeaponHoldType.Shotgun);
        idleByWeapon.AddChild(knifeIdle, (float)WeaponHoldType.Blade);
        idleByWeapon.AddChild(bluntReady, (float)WeaponHoldType.Blunt);

        AnimatorState weaponAimState = ctrl.CreateBlendTreeInController("WeaponAim", out BlendTree aimByWeapon, upper);
        aimByWeapon.blendType = BlendTreeType.Simple1D;
        aimByWeapon.blendParameter = PartnerAnimator.WeaponTypeParam;
        aimByWeapon.useAutomaticThresholds = false;
        aimByWeapon.AddChild(pistolAim, (float)WeaponHoldType.Pistol);
        aimByWeapon.AddChild(shotgunAim, (float)WeaponHoldType.Shotgun);
        aimByWeapon.AddChild(knifeIdle, (float)WeaponHoldType.Blade);    // melee: same guard pose while "aiming"
        aimByWeapon.AddChild(bluntReady, (float)WeaponHoldType.Blunt);

        AnimatorStateMachine upperSm = ctrl.layers[upper].stateMachine;
        upperSm.defaultState = weaponIdleState;
        AnimatorStateTransition toAim = weaponIdleState.AddTransition(weaponAimState);
        toAim.hasExitTime = false;
        toAim.duration = 0.15f;
        toAim.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AimingParam);
        AnimatorStateTransition toLowReady = weaponAimState.AddTransition(weaponIdleState);
        toLowReady.hasExitTime = false;
        toLowReady.duration = 0.25f;
        toLowReady.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.AimingParam);

        // reloads (upper body: she can keep walking), clip picked by WeaponType
        AnimatorState reloadPistolState = upperSm.AddState("ReloadPistol", new Vector3(500f, 60f, 0f));
        reloadPistolState.motion = reloadPistol;
        reloadPistolState.tag = PartnerAnimator.ReloadTag;
        reloadPistolState.speed = ReloadPistolSpeed;
        AnimatorState reloadShotgunState = upperSm.AddState("ReloadShotgun", new Vector3(500f, 140f, 0f));
        reloadShotgunState.motion = reloadShotgun;
        reloadShotgunState.tag = PartnerAnimator.ReloadTag;
        foreach (AnimatorState from in new[] { weaponIdleState, weaponAimState })
        {
            AnimatorStateTransition toPistol = from.AddTransition(reloadPistolState);
            toPistol.hasExitTime = false;
            toPistol.duration = 0.2f;
            toPistol.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadingParam);
            toPistol.AddCondition(AnimatorConditionMode.Less, 0.5f, PartnerAnimator.WeaponTypeParam);
            AnimatorStateTransition toShotgun = from.AddTransition(reloadShotgunState);
            toShotgun.hasExitTime = false;
            toShotgun.duration = 0.2f;
            toShotgun.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadingParam);
            toShotgun.AddCondition(AnimatorConditionMode.Greater, 0.5f, PartnerAnimator.WeaponTypeParam);
            toShotgun.AddCondition(AnimatorConditionMode.Less, 1.5f, PartnerAnimator.WeaponTypeParam);
        }
        foreach (AnimatorState reload in new[] { reloadPistolState, reloadShotgunState })
        {
            AnimatorStateTransition abort = reload.AddTransition(weaponIdleState);   // shot / weapon switch: leave at once
            abort.hasExitTime = false;
            abort.duration = 0.15f;
            abort.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadAbortParam);
        }
        // pistol: one magazine, then back (the clip's hands are back on the gun at ~0.92)
        AnimatorStateTransition pistolDone = reloadPistolState.AddTransition(weaponIdleState);
        pistolDone.hasExitTime = true;
        pistolDone.exitTime = 0.92f;
        pistolDone.duration = 0.2f;
        // shotgun: one shell per loop, leaves at the end of the loop once Reloading is off
        AnimatorStateTransition shotgunDone = reloadShotgunState.AddTransition(weaponIdleState);
        shotgunDone.hasExitTime = true;
        shotgunDone.exitTime = 0.98f;
        shotgunDone.duration = 0.15f;
        shotgunDone.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.ReloadingParam);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Selection.activeObject = ctrl;
        Debug.Log("[Helena] PartnerAnimator.controller built: Locomotion (Idle 0 -> Walk " + WalkSpeed + ") + Turn (L/R 90, L/R 180) + Pickup (Low, Medium) + Armed (2D) + Melee (2 stabs, blunt strike) + Seat (sit, type, stand) + UpperBody (pistol / shotgun / blade / blunt, reloads).");
    }

    [MenuItem("Last Terminal/3 - Build Prefabs + Data (Pickups, Weapons, Save Points, Facility, Blackjack, Commands)")]
    public static void BuildPickupPrefabs()
    {
        Shader lit = DefaultLitShader();
        if (lit == null) return;

        Dictionary<string, ItemDefinition> items = EnsureTestItems();
        EnsureFolder(PickupPropsFolder, "Materials");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Pickups");

        // build in an isolated preview scene, so the open scene is never touched
        Scene preview = EditorSceneManager.NewPreviewScene();
        int built = 0;
        foreach (var prop in PickupProps)
        {
            string fbxPath = $"{PickupPropsFolder}/SM_{prop.name}.fbx";
            var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (mi == null) { Debug.LogWarning("[Pickups] Model not found: " + fbxPath); continue; }

            // material (always refreshed) + remap on the model
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PickupPropsFolder}/T_{prop.name}.png");
            Material mat = MakeMaterial($"{PickupPropsFolder}/Materials/M_{prop.name}.mat", tex, prop.smooth, false, lit);
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
            SetReference(pickup, Pickup.ItemField, items[prop.name]);
            SetInt(pickup, Pickup.AmountField, prop.amount);
            SetReference(pickup, Pickup.VisualField, visual.transform);
            SetReference(pickup, Pickup.GrabPointField, grabPoint);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            built++;
        }
        foreach (var def in Weapons) BuildWeaponPrefab(preview, lit, def, items);
        BuildSavePointPrefabs(preview, lit);
        FacilityBuilder.BuildPrefabs(preview, lit);
        BlackjackBuilder.BuildPrefabs(preview, lit);
        ElevatorBuilder.BuildKeyPrefabs(preview, lit);   // the colored elevator keys
        OperatorRoomBuilder.PrepareAssets(lit);           // the operator's room: materials, his arms, his files
        VoiceBuilder.ConfigurePlugins();                  // voice commands: Vosk libraries + models check
        EditorSceneManager.ClosePreviewScene(preview);
        // command data: EN / PT lexicons, Helena's voice lines (completed, never reset) + the recording script
        CommandDataBuilder.EnsureLexicon(GameLanguage.English);
        CommandDataBuilder.EnsureLexicon(GameLanguage.Portuguese);
        CommandDataBuilder.ExportScript();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Pickups] {built} prefab(s) built in {PickupPrefabFolder}. Materials in {PickupPropsFolder}/Materials.");
    }

    // What a scene puts in the world (the rig - Helena, the systems, the UI, the cameras - is the same in every scene).
    internal class LocationContent
    {
        public FacilityBuilder.Level Level;
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<Health> Dummies = new List<Health>();
        public readonly List<SaveStation> Stations = new List<SaveStation>();
        public readonly List<BlackjackGame> Tables = new List<BlackjackGame>();
        public readonly List<Elevator> Elevators = new List<Elevator>();
        public readonly List<SlotMachine> Slots = new List<SlotMachine>();
        public readonly List<HoldemGame> HoldemTables = new List<HoldemGame>();
        public RouletteGame Roulette;     // optional
        public PrizeCounter Counter;      // optional: the cashier / prize counter
        public readonly List<PrizeCase> PrizeCases = new List<PrizeCase>();   // the prize lockers
        public SecurityCamera GameCamera; // optional: the close camera of the standing games (GameCameraDirector)
        public bool TestEnemies;      // the test enemies of Last Terminal/7
        public string Description;    // for the log
        public string PlaceName = "FACILITY", PlaceNamePt = "INSTALAÇÃO";   // the map title
        public Room CenterRoom;       // optional: where "the center" goes (the middle of a big hall)
        // the story scenes (the asylum): no operator room inside the scene, she starts with nothing (her weapons only
        // once she picks them up), in another outfit; enemies placed by the content; things she comments on
        public bool EmbedOperatorRoom = true;
        public bool BareStart;
        public string StartOutfit;    // outfit key (empty = the default one)
        public readonly List<EnemySpec> Enemies = new List<EnemySpec>();
        public readonly List<Examinable> Examinables = new List<Examinable>();
        // called once the rig is built (cutscenes, things that need her systems)
        public System.Action<LocationRig> Finish;
    }

    internal struct EnemySpec
    {
        public string Prefab;         // Enemy_Zombie, Enemy_Zombie_Pipe, Enemy_Killer_Axe, Enemy_Runner
        public Vector3 Position;
        public float Yaw;
        public EnemyStartMode Mode;
        public string SaveId;
    }

    // what the content's Finish gets: the systems of the scene
    internal class LocationRig
    {
        public Scene Scene;
        public GameObject Helena;
        public CutsceneDirector Director;
        public SecurityCameraSystem CameraSystem;
        public CameraSettings Settings;
        public StoryProgress Story;
        public CommandTargets Targets;
        public PartnerVoice Voice;
        public Camera ViewCamera;     // the operator's view (sits on the active CCTV camera)
        public readonly List<EnemyBrain> Enemies = new List<EnemyBrain>();
    }

    [MenuItem("Last Terminal/4 - Build Test Scene")]
    public static void BuildTestScene() => BuildLocation(ScenePath, NavMeshPath, BuildTestContent);

    // The facility (test scene): rooms, doors, keypad (FacilityBuilder) + the test content.
    private static LocationContent BuildTestContent(Scene scene, Shader lit)
    {
        var content = new LocationContent { TestEnemies = true };
        // facility: rooms, walls (NavMesh baked from them), doors, CCTV cameras, lights (prefabs from menu 3)
        FacilityBuilder.Level level = FacilityBuilder.BuildLevel(scene, lit);
        content.Level = level;
        // storage counter (the medkit on top tests the medium pick up); carves its own hole
        GameObject counter = CreateBox(level.ContentRoot, new Vector3(3f, 0.45f, 10.6f), new Vector3(1.4f, 0.9f, 0.6f));
        counter.name = "Counter";
        var counterObstacle = counter.AddComponent<NavMeshObstacle>();
        counterObstacle.carving = true;

        // pickups: prefab when built (menu 3), colored cube otherwise
        Dictionary<string, ItemDefinition> items = EnsureTestItems();
        Dictionary<string, ElevatorKey> keys = ElevatorBuilder.EnsureKeys();
        var pickups = new GameObject("Pickups");
        List<Pickup> placed = content.Pickups;
        placed.Add(PlacePickup(pickups.transform, "HandgunAmmo", items, 15, new Vector3(3f, 0f, -6.5f), 25f, new Color(0.85f, 0.7f, 0.2f)));     // lobby
        placed.Add(PlacePickup(pickups.transform, "Medkit", items, 1, new Vector3(-3f, 0f, -1.5f), 70f, new Color(0.2f, 0.75f, 0.3f)));         // lobby
        placed.Add(PlacePickup(pickups.transform, "ShotgunShells", items, 6, new Vector3(-4f, 0f, 2f), 15f, new Color(0.8f, 0.25f, 0.15f)));   // corridor
        placed.Add(PlacePickup(pickups.transform, "MagnumAmmo", items, 7, new Vector3(-12f, 0f, -7f), 35f, new Color(0.6f, 0.1f, 0.1f)));      // training
        placed.Add(PlacePickup(pickups.transform, "HandgunAmmo", items, 15, new Vector3(2f, 0f, 6.5f), -40f, new Color(0.85f, 0.7f, 0.2f)));    // storage
        placed.Add(PlacePickup(pickups.transform, "Medkit", items, 1, new Vector3(3.2f, 0.9f, 10.45f), -10f, new Color(0.2f, 0.75f, 0.3f)));    // on the counter
        placed.Add(PlacePickup(pickups.transform, "Keycard", items, 1, new Vector3(4.5f, 0f, 8f), 30f, new Color(0.25f, 0.45f, 0.9f)));         // storage
        // the red elevator key (the office elevator goes to the casino): in the corridor
        placed.Add(ElevatorBuilder.PlaceKey(pickups.transform, keys["red"], new Vector3(-4.6f, 0f, 3.2f), 40f));
        placed.RemoveAll(p => p == null);

        // the training room door needs the keycard (found in the storage room)
        Door trainingDoor = level.Doors.Find(d => d.DisplayName == "Training Room");
        if (trainingDoor != null)
        {
            var doorSo = new SerializedObject(trainingDoor);
            doorSo.FindProperty(Door.LockedField).boolValue = true;
            doorSo.FindProperty(Door.RequiredKeyField).objectReferenceValue = items["Keycard"];
            doorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // melee targets
        var dummies = new GameObject("TrainingDummies");
        content.Dummies.Add(CreateTrainingDummy(dummies.transform, new Vector3(-10f, 0f, -5f), 20f));
        content.Dummies.Add(CreateTrainingDummy(dummies.transform, new Vector3(-9f, 0f, -2f), -60f));

        // save points (prefabs from menu 3): one of each variant, carved out of the NavMesh
        var savePoints = new GameObject("SavePoints");
        content.Stations.Add(PlaceSavePoint(savePoints.transform, "SavePoint_Wood", new Vector3(10f, 0f, -6.5f), 0f));        // office
        content.Stations.Add(PlaceSavePoint(savePoints.transform, "SavePoint_Security", new Vector3(4f, 0f, 1f), 0f));        // corridor
        content.Stations.Add(PlaceSavePoint(savePoints.transform, "SavePoint_Terminal", new Vector3(-3f, 0f, 8f), 0f));      // storage
        content.Stations.RemoveAll(s => s == null);

        // blackjack table (lobby, prefab from menu 3): "let's play" and she deals
        var blackjack = new GameObject("Blackjack");
        BlackjackGame lobbyTable = BlackjackBuilder.PlaceTable(blackjack.transform, new Vector3(-3f, 0f, -6.95f), 0f);
        if (lobbyTable != null) content.Tables.Add(lobbyTable);
        foreach (BlackjackGame t in content.Tables) BlackjackBuilder.AddTableCamera(t, level.Rooms.Count > 0 ? level.Rooms[0] : null, level.Cameras);

        // the office elevator (east wall) up to the casino; it needs the red key. Its own camera (CAM 08).
        Room office = level.Rooms.Find(r => r.DisplayName == "Office");
        if (office != null)
        {
            var elevators = new GameObject("Elevators").transform;
            SecurityCamera watch = FacilityBuilder.AddCamera(level, level.ContentRoot, "CAM 08", office,
                                                             new Vector3(6.25f, 2.75f, -7.75f), new Vector3(12f, 0f, -5f));
            Elevator up = ElevatorBuilder.PlaceElevator(elevators, lit, ElevatorBuilder.FacilityOfficeStop, ElevatorBuilder.CasinoStop, 1,
                                                        new Vector3(12.15f, 0f, -5f), 270f, ElevatorBuilder.FacilityBay, ElevatorBuilder.FacilityHeight, watch);
            if (up != null) content.Elevators.Add(up);
        }

        content.Description = "Play: camera = mouse at the screen edges (or A / D, settings), Q / E = camera, Tab = map (cameras, security doors), Esc = settings; click floor = walk, click item = pick up, H = heal, J = damage, G = draw/holster, 1..9, 0 = weapon (1 pistol, 2 magnum, 3 shotgun, 4 knife, 5 cleaver, 6 machete, 7 hammer, 8 bat, 9 nail bat, 0 axe), hold right mouse = aim, F = attack toward the cursor (gun: shoot, knife: F again = combo), R = reload, click the computer = save / load (arrows, Enter, Esc). Save files: " + Application.persistentDataPath;
        return content;
    }

    // Builds a scene: its content (rooms, cameras, things) + Helena and everything that runs her (the rig).
    // Saved with its NavMesh and added to the Build Settings (elevators and saves load scenes by name).
    internal static void BuildLocation(string scenePath, string navMeshPath, System.Func<Scene, Shader, LocationContent> buildContent)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
        if (UseHelena2 && AssetDatabase.LoadAssetAtPath<Material>(HelenaCompareBuilder.BodyMaterialPath) == null)
            Debug.LogWarning("[Helena] Helena 2's materials are missing: run menu 18 (and 21 for her adjusted weapon clips, then 2).");
        if (ctrl == null || prefab == null)
        {
            Debug.LogError("[Helena] Missing controller or model. Run menus 0, 1 and 2 first.");
            return;
        }
        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            Debug.LogError("[Helena] TextMeshPro resources missing (save screen / HUD text). Window > TextMeshPro > Import TMP Essential Resources, then run the menu again.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Shader lit = DefaultLitShader();
        if (lit == null) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LocationContent content = buildContent(scene, lit);
        if (content == null) return;
        FacilityBuilder.Level level = content.Level;
        NavMeshSurface surface = level.Surface;
        List<Pickup> placed = content.Pickups;
        List<SaveStation> stations = content.Stations;
        List<BlackjackGame> tables = content.Tables;
        // pickups get a save id (unique in the scene)
        for (int i = 0; i < placed.Count; i++) SetString(placed[i], Pickup.SaveIdField, $"pickup_{i:00}_{placed[i].Item.Id}");

        // character
        var helena = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        helena.name = "Helena";
        helena.transform.SetPositionAndRotation(level.PartnerStart, Quaternion.Euler(0f, level.PartnerStartYaw, 0f));
        Animator animator = helena.GetComponent<Animator>();
        if (animator == null) animator = helena.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = true;   // "Handled by Script": PartnerAnimator.OnAnimatorMove

        var agent = helena.AddComponent<NavMeshAgent>();
        agent.speed = WalkSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.05f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;

        var partnerAnimator = helena.AddComponent<PartnerAnimator>();
        SetReference(partnerAnimator, PartnerAnimator.AgentField, agent);

        // outfits (skins): same model, other materials
        PartnerOutfit outfit = OutfitBuilder.AddTo(helena, lit, content.StartOutfit);

        // gameplay: health, slot inventory, interaction, test keys (H heal / J damage)
        helena.AddComponent<Health>();
        var inventory = helena.AddComponent<Inventory>();
        SetInt(inventory, Inventory.SlotCountField, 8);
        Dictionary<string, ItemDefinition> startItems = EnsureTestItems();
        if (!content.BareStart)
            SetStartingItems(inventory, (startItems["HandgunAmmo"], 14), (startItems["ShotgunShells"], 6), (startItems["MagnumAmmo"], 7));   // spare ammo to test reloads
        var interactor = helena.AddComponent<PartnerInteractor>();
        SetReference(interactor, PartnerInteractor.PartnerAnimatorField, partnerAnimator);
        var harness = helena.AddComponent<PartnerTestHarness>();

        // combat: loadout in the right hand, draw / holster / switch / aim / melee (G / 1..9 / right mouse / F)
        PartnerCombat combat = null;
        PartnerShooter shooter = null;
        WeaponHolder holder = null;
        var loadout = new List<Weapon>();
        foreach (var def in Weapons)
        {
            var w = AssetDatabase.LoadAssetAtPath<Weapon>(WeaponPrefabPath(def.Name));
            if (w != null) loadout.Add(w);
        }
        if (loadout.Count > 0)
        {
            holder = helena.AddComponent<WeaponHolder>();
            SetReference(holder, WeaponHolder.PartnerAnimatorField, partnerAnimator);
            SetReferenceArray(holder, WeaponHolder.LoadoutField, loadout);
            if (content.BareStart)
            {
                // her weapons are only the ones she picked up (WeaponItem in her inventory)
                SetReference(holder, WeaponHolder.InventoryField, inventory);
                var hso = new SerializedObject(holder);
                hso.FindProperty(WeaponHolder.RequireItemsField).boolValue = true;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }
            var melee = helena.AddComponent<PartnerMelee>();
            SetReference(melee, PartnerMelee.PartnerAnimatorField, partnerAnimator);
            SetReference(melee, PartnerMelee.WeaponHolderField, holder);
            var audio = helena.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            shooter = helena.AddComponent<PartnerShooter>();
            SetReference(shooter, PartnerShooter.PartnerAnimatorField, partnerAnimator);
            SetReference(shooter, PartnerShooter.WeaponHolderField, holder);
            SetReference(shooter, PartnerShooter.EffectsMaterialField, GraphicsSettings.currentRenderPipeline.defaultParticleMaterial);
            SetReference(shooter, PartnerShooter.AudioSourceField, audio);
            var reloadVisuals = helena.AddComponent<ReloadVisuals>();
            SetReference(reloadVisuals, ReloadVisuals.PartnerAnimatorField, partnerAnimator);
            SetReference(reloadVisuals, ReloadVisuals.ShooterField, shooter);
            combat = helena.AddComponent<PartnerCombat>();
            SetReference(combat, PartnerCombat.PartnerAnimatorField, partnerAnimator);
            SetReference(combat, PartnerCombat.WeaponHolderField, holder);
            SetReference(combat, PartnerCombat.InteractorField, interactor);
            SetReference(combat, PartnerCombat.MeleeField, melee);
            SetReference(combat, PartnerCombat.ShooterField, shooter);
        }
        else Debug.LogWarning("[Helena] No weapon prefabs in " + WeaponPrefabFolder + ": run menu 3 to test combat.");

        // game systems: save files + UI (HUD, save screen) + the partner's save routine
        PartnerSaveUser saveUser = null;
        PartnerDoorUser doorUser = null;
        ChipBank chipBank = null;
        SaveScreen saveScreenRef = null;
        SaveSystem saveSystemRef = null;
        GameSession session = ElevatorBuilder.EnsureSession();
        if (combat != null)
        {
            var systems = new GameObject("GameSystems");
            var saveSystem = systems.AddComponent<SaveSystem>();
            saveSystemRef = saveSystem;
            SetReference(saveSystem, SaveSystem.AgentField, agent);
            SetReference(saveSystem, SaveSystem.HealthField, helena.GetComponent<Health>());
            SetReference(saveSystem, SaveSystem.InventoryField, inventory);
            SetReference(saveSystem, SaveSystem.CombatField, combat);
            SetReference(saveSystem, SaveSystem.ShooterField, shooter);
            SetReference(saveSystem, SaveSystem.WeaponHolderField, holder);
            SetReference(saveSystem, SaveSystem.ItemDatabaseField, EnsureItemDatabase(AllItems()));
            SetReferenceArray(saveSystem, SaveSystem.PickupsField, placed);
            SetReference(saveSystem, SaveSystem.SessionField, session);   // her state between scenes (elevators)
            chipBank = systems.AddComponent<ChipBank>();   // the operator's chips and prize coins (saved, carried between scenes)
            SetReference(saveSystem, SaveSystem.ChipBankField, chipBank);
            SetReference(saveSystem, SaveSystem.OutfitField, outfit);

            SaveScreen saveScreen = BuildUi(scene, saveSystem, shooter);
            saveScreenRef = saveScreen;

            saveUser = helena.AddComponent<PartnerSaveUser>();
            SetReference(saveUser, PartnerSaveUser.PartnerAnimatorField, partnerAnimator);
            SetReference(saveUser, PartnerSaveUser.InteractorField, interactor);
            SetReference(saveUser, PartnerSaveUser.CombatField, combat);
            SetReference(saveUser, PartnerSaveUser.SaveScreenField, saveScreen);
            SetReference(saveUser, PartnerSaveUser.SaveSystemField, saveSystem);

            doorUser = helena.AddComponent<PartnerDoorUser>();
            SetReference(doorUser, PartnerDoorUser.PartnerAnimatorField, partnerAnimator);
            SetReference(doorUser, PartnerDoorUser.InteractorField, interactor);
            SetReference(doorUser, PartnerDoorUser.CombatField, combat);
        }

        // the operator's view: one camera that sits on the active CCTV camera (SecurityCameraSystem)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var viewCamera = camGo.AddComponent<Camera>();
        viewCamera.nearClipPlane = 0.05f;
        viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var viewListener = camGo.AddComponent<AudioListener>();
        var click = camGo.AddComponent<ClickToMoveTest>();
        SetReference(click, ClickToMoveTest.PartnerField, interactor);
        if (saveUser != null) SetReference(click, ClickToMoveTest.SaveUserField, saveUser);
        if (doorUser != null) SetReference(click, ClickToMoveTest.DoorUserField, doorUser);
        CombatTestInput combatInput = null;
        if (combat != null)
        {
            combatInput = camGo.AddComponent<CombatTestInput>();
            SetReference(combatInput, CombatTestInput.CombatField, combat);
        }

        // camera system + settings + filter + HUD
        if (level.Cameras.Count > 0 && saveScreenRef != null)
        {
            var security = new GameObject("SecurityRoom");
            var camSettings = security.AddComponent<CameraSettings>();
            var cameraSystem = security.AddComponent<SecurityCameraSystem>();
            SetReference(cameraSystem, SecurityCameraSystem.ViewCameraField, viewCamera);
            SecurityCamera shotCamera = CutsceneBuilder.CreateShotCamera(level.ContentRoot);   // cutscenes' free shots
            level.Cameras.Add(shotCamera);
            SetReferenceArray(cameraSystem, SecurityCameraSystem.CamerasField, level.Cameras);
            SetReference(cameraSystem, SecurityCameraSystem.SettingsField, camSettings);
            SetReference(cameraSystem, SecurityCameraSystem.SaveScreenField, saveScreenRef);
            SetInt(cameraSystem, SecurityCameraSystem.StartCameraField, level.StartCamera);
            var volume = security.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = FacilityBuilder.EnsureFilterProfile();
            FacilityBuilder.BuildCameraHud(saveScreenRef.transform.parent, cameraSystem, camSettings, helena.transform, level.Rooms, level.Doors, volume,
                                           content.PlaceName, content.PlaceNamePt);
            SetReference(click, ClickToMoveTest.CameraSystemField, cameraSystem);
            if (combatInput != null) SetReference(combatInput, CombatTestInput.CameraSystemField, cameraSystem);
            SetReference(harness, PartnerTestHarness.CameraSystemField, cameraSystem);

            // commands (typed now, voice later) + Helena's voice and subtitles
            if (doorUser != null)
            {
                var targets = level.ContentRoot.gameObject.AddComponent<CommandTargets>();
                SetReferenceArray(targets, CommandTargets.RoomsField, level.Rooms);
                SetReferenceArray(targets, CommandTargets.DoorsField, level.Doors);
                SetReferenceArray(targets, CommandTargets.PickupsField, placed);
                SetReferenceArray(targets, CommandTargets.StationsField, stations);
                SetReferenceArray(targets, CommandTargets.TablesField, tables);
                SetReferenceArray(targets, CommandTargets.ElevatorsField, content.Elevators);
                SetReferenceArray(targets, CommandTargets.SlotsField, content.Slots);
                SetReferenceArray(targets, CommandTargets.HoldemTablesField, content.HoldemTables);
                SetReference(targets, CommandTargets.RouletteField, content.Roulette);
                SetReference(targets, CommandTargets.CounterField, content.Counter);
                // enemies (Last Terminal/7 prefabs): shootable by their type's name ("zombie", "killer") or "enemy", saved with her
                var enemyRoot = new GameObject("Enemies");
                List<EnemyBrain> enemies = content.TestEnemies
                    ? EnemyBuilder.PlaceTestEnemies(scene, enemyRoot.transform, helena.transform, helena.GetComponent<Health>(), shooter, targets)
                    : new List<EnemyBrain>();
                foreach (EnemySpec e in content.Enemies)
                {
                    EnemyBrain brain = EnemyBuilder.PlaceEnemy(scene, enemyRoot.transform, e.Prefab, e.Position, e.Yaw, e.Mode, e.SaveId,
                                                               helena.transform, helena.GetComponent<Health>(), shooter, targets);
                    if (brain != null) enemies.Add(brain);
                }
                var shootables = new List<(string key, Health target)>();
                foreach (Health d in content.Dummies) shootables.Add((CommandTargets.DummyKey, d));
                foreach (EnemyBrain e in enemies) shootables.Add((e.Type.CommandKey, e.GetComponent<Health>()));
                SetShootables(targets, shootables);
                SetReferenceArray(saveSystemRef, SaveSystem.EnemiesField, enemies);
                SetReference(targets, CommandTargets.CameraSystemField, cameraSystem);
                SetReference(targets, CommandTargets.PartnerField, helena.transform);
                SetReference(targets, CommandTargets.CenterRoomField, content.CenterRoom);
                SetReferenceArray(targets, CommandTargets.KeypadsField, level.Keypads);
                SetReferenceArray(targets, CommandTargets.ExaminablesField, content.Examinables);
                var encounter = targets.gameObject.AddComponent<CombatEncounter>();
                SetReference(encounter, CombatEncounter.TargetsField, targets);
                SetReference(encounter, CombatEncounter.PartnerField, helena.transform);
                SetReference(encounter, CombatEncounter.PartnerHealthField, helena.GetComponent<Health>());

                var voiceSource = helena.AddComponent<AudioSource>();   // her radio: 2D, heard the same from any camera
                voiceSource.playOnAwake = false;
                voiceSource.spatialBlend = 0f;
                var voice = helena.AddComponent<PartnerVoice>();
                SetReference(voice, PartnerVoice.BankField, CommandDataBuilder.EnsureVoiceBank());
                SetReference(voice, PartnerVoice.SettingsField, camSettings);
                SetReference(voice, PartnerVoice.AudioSourceField, voiceSource);

                var evasion = helena.AddComponent<PartnerEvasion>();
                SetReference(evasion, PartnerEvasion.PartnerAnimatorField, partnerAnimator);
                SetReference(evasion, PartnerEvasion.HealthField, helena.GetComponent<Health>());

                var commands = helena.AddComponent<PartnerCommands>();
                SetReference(commands, PartnerCommands.EvasionField, evasion);
                SetReference(commands, PartnerCommands.OutfitField, outfit);
                var pose = helena.AddComponent<PartnerCameraPose>();   // "look at the camera"
                SetReference(pose, PartnerCameraPose.PartnerAnimatorField, partnerAnimator);
                SetReference(pose, PartnerCameraPose.InteractorField, interactor);
                SetReference(pose, PartnerCameraPose.CameraSystemField, cameraSystem);
                SetReference(pose, PartnerCameraPose.TargetsField, targets);
                SetReference(pose, PartnerCameraPose.VoiceField, voice);
                SetReference(commands, PartnerCommands.CameraPoseField, pose);
                var stance = helena.AddComponent<PartnerCombatStance>();   // "combat position"
                SetReference(stance, PartnerCombatStance.CombatField, combat);
                SetReference(stance, PartnerCombatStance.PartnerAnimatorField, partnerAnimator);
                SetReference(stance, PartnerCombatStance.InteractorField, interactor);
                SetReference(stance, PartnerCombatStance.EvasionField, evasion);
                SetReference(stance, PartnerCombatStance.EncounterField, encounter);
                SetReference(stance, PartnerCombatStance.HealthField, helena.GetComponent<Health>());
                SetReference(commands, PartnerCommands.StanceField, stance);
                var keypadUser = helena.AddComponent<PartnerKeypadUser>();   // "type 1 7", the slot machines' SPIN button
                SetReference(keypadUser, PartnerKeypadUser.PartnerAnimatorField, partnerAnimator);
                SetReference(keypadUser, PartnerKeypadUser.InteractorField, interactor);
                SetReference(keypadUser, PartnerKeypadUser.CombatField, combat);
                SetReference(commands, PartnerCommands.KeypadUserField, keypadUser);
                SetReference(commands, PartnerCommands.PartnerAnimatorField, partnerAnimator);
                SetReference(commands, PartnerCommands.InteractorField, interactor);
                SetReference(commands, PartnerCommands.CombatField, combat);
                SetReference(commands, PartnerCommands.ShooterField, shooter);
                SetReference(commands, PartnerCommands.WeaponHolderField, holder);
                SetReference(commands, PartnerCommands.DoorUserField, doorUser);
                SetReference(commands, PartnerCommands.SaveUserField, saveUser);
                SetReference(commands, PartnerCommands.HealthField, helena.GetComponent<Health>());
                SetReference(commands, PartnerCommands.TargetsField, targets);
                SetReference(commands, PartnerCommands.VoiceField, voice);
                SetReference(commands, PartnerCommands.SettingsField, camSettings);
                SetReference(commands, PartnerCommands.EnglishField, CommandDataBuilder.EnsureLexicon(GameLanguage.English));
                SetReference(commands, PartnerCommands.PortugueseField, CommandDataBuilder.EnsureLexicon(GameLanguage.Portuguese));
                SetReference(commands, PartnerCommands.EncounterField, encounter);
                AssetDatabase.SaveAssets();

                // getting hurt: hit reactions, limping, death + the game over screen
                var damage = helena.AddComponent<PartnerDamage>();
                SetReference(damage, PartnerDamage.HealthField, helena.GetComponent<Health>());
                SetReference(damage, PartnerDamage.PartnerAnimatorField, partnerAnimator);
                SetReference(damage, PartnerDamage.EvasionField, evasion);
                SetReference(damage, PartnerDamage.CombatField, combat);
                SetReference(damage, PartnerDamage.InteractorField, interactor);
                SetReference(damage, PartnerDamage.VoiceField, voice);

                Transform canvas = saveScreenRef.transform.parent;
                CommandConsole console = BuildCommandUi(canvas, commands, voice, cameraSystem, saveScreenRef, camSettings);
                // the microphone (hold V): same commands, spoken (Vosk, offline)
                VoiceCommandInput mic = VoiceBuilder.Build(canvas, commands, cameraSystem, saveScreenRef, camSettings, console);
                BuildCombatHud(canvas, encounter, cameraSystem, camSettings, helena.GetComponent<Health>());

                // blackjack: her dealer hands + the tables + the panel
                var dealer = helena.AddComponent<PartnerBlackjackDealer>();
                SetReference(dealer, PartnerBlackjackDealer.PartnerAnimatorField, partnerAnimator);
                foreach (BlackjackGame t in tables) BlackjackBuilder.WireGame(t, chipBank, saveUser, dealer, voice, encounter, cameraSystem);
                if (tables.Count > 0) BlackjackBuilder.BuildHud(canvas, tables, camSettings);
                SetReference(click, ClickToMoveTest.CommandsField, commands);

                // slot machines: the chips they take and pay, their panel
                foreach (SlotMachine m in content.Slots) SetReference(m, SlotMachine.BankField, chipBank);
                if (content.Slots.Count > 0) CasinoBuilder.BuildHud(canvas, keypadUser, chipBank, camSettings);

                // Casino Hold'em (same dealer and seat routine), the roulette (she spins it like a button), the counter
                foreach (HoldemGame t in content.HoldemTables) BlackjackBuilder.WireHoldem(t, chipBank, saveUser, dealer, voice, encounter, cameraSystem);
                if (content.HoldemTables.Count > 0) CasinoBuilder.BuildHoldemHud(canvas, content.HoldemTables, camSettings);
                if (content.Roulette != null)
                {
                    SetReference(content.Roulette, RouletteGame.BankField, chipBank);
                    CasinoBuilder.BuildRouletteHud(canvas, content.Roulette, camSettings);
                }
                if (content.Counter != null)
                {
                    SetReference(content.Counter, PrizeCounter.BankField, chipBank);
                    SetReference(content.Counter, PrizeCounter.OutfitField, outfit);
                    SetReference(content.Counter, PrizeCounter.PartnerField, agent);
                    SetReferenceArray(content.Counter, PrizeCounter.CasesField, content.PrizeCases);
                    foreach (PrizeCase c in content.PrizeCases)
                    {
                        SetReference(c, PrizeCase.OwnerField, outfit);
                        SetReference(c, PrizeCase.SettingsField, camSettings);
                    }
                    CasinoBuilder.BuildCounterHud(canvas, content.Counter, helena.transform, camSettings);
                }

                // the close camera of the slot machines and the roulette
                if (content.GameCamera != null)
                {
                    var director = targets.gameObject.AddComponent<GameCameraDirector>();
                    SetReference(director, GameCameraDirector.KeypadUserField, keypadUser);
                    SetReference(director, GameCameraDirector.GameCameraField, content.GameCamera);
                    SetReference(director, GameCameraDirector.CameraSystemField, cameraSystem);
                    SetReference(director, GameCameraDirector.TargetsField, targets);
                }

                // elevators: her ride, the trip to the other scene (the dark screen with the floor), the signs' language
                if (content.Elevators.Count > 0)
                {
                    var travel = saveSystemRef.gameObject.AddComponent<ElevatorTravel>();
                    var elevatorUser = helena.AddComponent<PartnerElevatorUser>();
                    SetReference(elevatorUser, PartnerElevatorUser.PartnerAnimatorField, partnerAnimator);
                    SetReference(elevatorUser, PartnerElevatorUser.InteractorField, interactor);
                    SetReference(elevatorUser, PartnerElevatorUser.CombatField, combat);
                    SetReference(elevatorUser, PartnerElevatorUser.TravelField, travel);
                    SetReference(commands, PartnerCommands.ElevatorUserField, elevatorUser);
                    ElevatorBuilder.WireTravel(travel, canvas, saveSystemRef, session, elevatorUser, content.Elevators, camSettings, cameraSystem);
                }

                BuildGameOver(canvas, damage, helena.GetComponent<Health>(), saveSystemRef);   // last: over every HUD

                // the operator: story flags (his files), Helena waiting on the floor while he is away, his own room
                // (first person: the PC opens all of the above, Esc closes it; coffee, files, the pinball)
                var story = saveSystemRef.gameObject.AddComponent<StoryProgress>();
                SetReference(story, StoryProgress.SaveSystemField, saveSystemRef);
                SetReference(story, StoryProgress.EncounterField, encounter);
                SetReferenceArray(story, StoryProgress.KeypadsField, level.Keypads);
                SetReference(saveSystemRef, SaveSystem.StoryField, story);
                // "look at the bed": she walks up to things and comments on them
                var examiner = helena.AddComponent<PartnerExaminer>();
                SetReference(examiner, PartnerExaminer.InteractorField, interactor);
                SetReference(examiner, PartnerExaminer.PartnerAnimatorField, partnerAnimator);
                SetReference(examiner, PartnerExaminer.VoiceField, voice);
                SetReference(examiner, PartnerExaminer.StoryField, story);
                SetReference(commands, PartnerCommands.ExaminerField, examiner);
                var rest = helena.AddComponent<PartnerRest>();
                SetReference(rest, PartnerRest.PartnerAnimatorField, partnerAnimator);
                SetReference(rest, PartnerRest.AgentField, agent);
                SetReference(rest, PartnerRest.HealthField, helena.GetComponent<Health>());
                var rig = new OperatorRoomBuilder.Rig
                {
                    ViewCamera = viewCamera, ViewListener = viewListener,
                    OperatorCanvas = canvas.GetComponent<Canvas>(), CameraSystem = cameraSystem, Rest = rest,
                    Partner = helena.transform, Targets = targets, Encounter = encounter, Voice = voice,
                    Session = session, Settings = camSettings, Story = story,
                };
                rig.OperatorBehaviours.AddRange(new Behaviour[] { cameraSystem, console, mic, click, combatInput, harness, volume });
                // (the story scenes: not in the scene; it will be its own scene, loaded with them)
                OperatorStation station = content.EmbedOperatorRoom ? OperatorRoomBuilder.Build(lit, rig) : null;

                // cutscenes: the director (Last Terminal/12 adds a cutscene to the scene)
                CutsceneDirector cutsceneDirector = CutsceneBuilder.Build(canvas, cameraSystem, shotCamera, station, encounter, helena, partnerAnimator, voice, story, camSettings, mic, console);
                if (level.Rooms.Exists(r => r.DisplayName == "Padded Cell")) CutsceneBuilder.BuildIntro(scene, cutsceneDirector, cameraSystem, station);   // the facility: the intro
                if (content.Finish != null)
                {
                    var located = new LocationRig
                    {
                        Scene = scene, Helena = helena, Director = cutsceneDirector, CameraSystem = cameraSystem, Settings = camSettings,
                        Story = story, Targets = targets, Voice = voice, ViewCamera = viewCamera,
                    };
                    located.Enemies.AddRange(enemies);
                    content.Finish(located);
                }
            }
        }
        else Debug.LogWarning("[Helena] No CCTV prefab / UI: run menu 3 (and import TMP) to get the camera system.");

        // save scene + NavMesh as assets (an unsaved NavMesh is lost on Play)
        EnsureFolder("Assets", "Scenes");
        AssetDatabase.DeleteAsset(navMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuild(scenePath);   // RESTART (game over) reloads it by build index; elevators load it by name
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = helena;
        Debug.Log("[Helena] Scene saved at " + scenePath + ". " + content.Description);
    }

    // A still copy of her wearing an outfit (the casino's prize lockers): her model posed with the first frame of her
    // idle clip, its outfits set up like hers (PartnerOutfit wears the outfit at start), no Animator (it stays posed).
    internal static GameObject BuildMannequin(Transform parent, Shader lit, OutfitDefinition outfit)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
        if (prefab == null) { Debug.LogWarning("[Helena] Model missing: run menus 0 and 1."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Mannequin";
        PartnerOutfit wardrobe = OutfitBuilder.AddTo(go, lit);   // needs her Animator (accessories go on her bones)
        HelenaSetupMenu.SetReference(wardrobe, PartnerOutfit.StartOutfitField, outfit);
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (idle != null) idle.SampleAnimation(go, idle.length * 0.25f);   // humanoid: sampled through her Animator
        Animator animator = go.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = false;
        return go;
    }

    // every item the game knows (saves turn ids back into assets): the test items + the elevator keys
    private static Dictionary<string, ItemDefinition> AllItems()
    {
        Dictionary<string, ItemDefinition> all = EnsureTestItems();
        foreach (var k in ElevatorBuilder.EnsureKeys()) all["Key_" + k.Key] = k.Value;
        foreach (var def in Weapons) all[WeaponItemName(def.Name)] = EnsureWeaponItem(def.Name);
        return all;
    }

    // ------------------------------------------------------------------ weapons as items (the story scenes)
    // The item of a weapon: id = the weapon prefab's name (Weapon_<name>), the lexicon key of the weapon too,
    // so "pick up the knife" finds the pickup and, once she has it, "draw the knife" finds the weapon.
    private static string WeaponItemName(string weapon) => "Weapon_" + weapon;

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
        SetReference(pickup, Pickup.ItemField, item);
        SetInt(pickup, Pickup.AmountField, 1);
        SetReference(pickup, Pickup.VisualField, visual.transform);
        return pickup;
    }

    // a pickup of one of the test items (ammo, medkit, keycard) for other builders
    internal static Pickup PlaceItemPickup(Transform parent, string itemName, int amount, Vector3 pos, float yaw, Color fallback) =>
        PlacePickup(parent, itemName, EnsureTestItems(), amount, pos, yaw, fallback);

    internal static Dictionary<string, ItemDefinition> TestItems() => EnsureTestItems();

    // ------------------------------------------------------------------ helpers
    internal static Shader DefaultLitShader()
    {
        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null || rp.defaultShader == null)
        {
            Debug.LogError("[Helena] No render pipeline asset active. Use a Universal 3D (URP) project.");
            return null;
        }
        return rp.defaultShader;
    }

    internal static Material MakeMaterial(string path, Texture2D tex, float smooth, bool clip, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;

        m.SetTexture(BaseMapId, tex);
        m.SetColor(BaseColorId, tex != null ? Color.white : new Color(0.03f, 0.03f, 0.035f));
        m.SetFloat(MetallicId, 0f);
        m.SetFloat(SmoothnessId, smooth);
        m.DisableKeyword("_EMISSION");

        if (clip)
        {
            m.SetFloat(AlphaClipId, 1f);
            m.SetFloat(CutoffId, 0.5f);
            m.SetFloat(CullId, (float)CullMode.Off);   // hair cards visible from both sides
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // empties a controller asset: no parameters, no layers, no states / trees / transitions left inside it; one empty base layer
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

    private static readonly List<string> MissingClips = new List<string>();   // paths LoadClip could not find (listed in the error)

    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) MissingClips.Add(path);
        return Adjusted(clip);
    }

    // one of several clips cut from the same file (HelenaImport)
    private static AnimationClip LoadClip(string path, string clipName)
    {
        return Adjusted(AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName));
    }

    // with Helena 2: her copy of the clip adjusted to her (weapon clips, Last Terminal/21) when there is one
    private static AnimationClip Adjusted(AnimationClip clip)
    {
        if (clip == null || !UseHelena2) return clip;
        var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{HelenaClipBaker.OutFolder}/{clip.name}{HelenaClipBaker.Suffix}.anim");
        return copy != null ? copy : clip;
    }

    // asset name -> item; creates the test items once, never overwrites tuned ones
    private static Dictionary<string, ItemDefinition> EnsureTestItems()
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
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Items");
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

    private static string PickupPrefabPath(string name) => $"{PickupPrefabFolder}/Pickup_{name}.prefab";

    private static AvatarMask CreateUpperBodyMask()
    {
        var mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        AvatarMaskBodyPart[] upper =
        {
            AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
        };
        foreach (AvatarMaskBodyPart part in upper) mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.DeleteAsset(UpperBodyMaskPath);
        AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        return mask;
    }

    private static string WeaponPrefabPath(string name) => $"{WeaponPrefabFolder}/Weapon_{name}.prefab";

    // Weapon_<name> prefab: model + Weapon with the markers exported from Blender (GripPoint, Muzzle, UpHint)
    // and the measured holds.
    private static void BuildWeaponPrefab(Scene preview, Shader lit, WeaponDef def, Dictionary<string, ItemDefinition> items)
    {
        string fbxPath = $"{WeaponPropsFolder}/SM_{def.Name}.fbx";
        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[Weapons] Model not found: " + fbxPath); return; }

        // material (always refreshed) + optional normal map
        EnsureFolder(WeaponPropsFolder, "Materials");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WeaponPropsFolder}/T_{def.Name}.png");
        Material mat = MakeMaterial($"{WeaponPropsFolder}/Materials/M_{def.Name}.mat", tex, def.Smooth, false, lit);
        string normalPath = $"{WeaponPropsFolder}/T_{def.Name}_N.png";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti)
        {
            if (ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            mat.SetTexture(BumpMapId, AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
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
            Material extra = MakeMaterial($"{WeaponPropsFolder}/Materials/M_{extraName}.mat", extraTex, 0.3f, false, lit);
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
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Weapons");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var root = new GameObject("Weapon_" + def.Name);
        SceneManager.MoveGameObjectToScene(root, preview);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);

        Transform grip = FindDeep(visual.transform, "GripPoint");
        Transform muzzle = FindDeep(visual.transform, "Muzzle");
        Transform up = FindDeep(visual.transform, "UpHint");
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
            Transform prop = string.IsNullOrEmpty(def.ReloadProp) ? null : FindDeep(root.transform, def.ReloadProp);
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
            Transform pump = def.Pump ? FindDeep(root.transform, "Pump") : null;
            if (pump != null) rso.FindProperty(RangedWeapon.PumpField).objectReferenceValue = pump;
            rso.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
        return false;
    }

    // Save points: SavePoint_Base (SaveStation + collider + NavMesh hole + empty Desk / Computer / Chair slots + points)
    // and one Prefab Variant per SavePointVariants entry, with the models in the slots and everything fitted to them.
    // Your own models: make a Prefab Variant of SavePoint_Base, drop them in the slots (gizmos show where), then
    // right-click the SaveStation component > Fit To Models.
    private static void BuildSavePointPrefabs(Scene preview, Shader lit)
    {
        // materials (always refreshed) + remap on every save point model
        EnsureFolder(SavePointPropsFolder, "Materials");
        var mats = new Dictionary<string, Material>();
        foreach (var m in SavePointMaterials)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SavePointPropsFolder}/{m.tex}.png");
            Material mat = MakeMaterial($"{SavePointPropsFolder}/Materials/{m.mat}.mat", tex, m.smooth, false, lit);
            if (m.emissive)
            {
                // the CRT glows a little
                mat.SetTexture(EmissionMapId, tex);
                mat.SetColor(EmissionColorId, Color.white * 0.8f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(mat);
            }
            mats[m.mat] = mat;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { SavePointPropsFolder }))
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (mi == null) continue;
            foreach (var m in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
            mi.SaveAndReimport();
        }

        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "SavePoints");
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SavePointBasePath);
        if (basePrefab == null)
        {
            var root = new GameObject("SavePoint");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.AddComponent<BoxCollider>();
            root.AddComponent<NavMeshObstacle>();
            var station = root.AddComponent<SaveStation>();
            CreateChild(root.transform, "Desk", Vector3.zero);
            CreateChild(root.transform, "Computer", Vector3.zero);
            Transform chair = CreateChild(root.transform, "Chair", Vector3.zero);
            Transform stand = CreateChild(root.transform, "StandPoint", new Vector3(0f, 0f, SaveStation.StandDistance));
            Transform right = CreateChild(root.transform, "WalkInRight", Vector3.zero);
            Transform left = CreateChild(root.transform, "WalkInLeft", Vector3.zero);
            SetReference(station, SaveStation.ChairField, chair);
            SetReference(station, SaveStation.StandPointField, stand);
            SetReference(station, SaveStation.WalkInRightField, right);
            SetReference(station, SaveStation.WalkInLeftField, left);
            station.FitToModels();
            basePrefab = PrefabUtility.SaveAsPrefabAsset(root, SavePointBasePath);
            Object.DestroyImmediate(root);
            Debug.Log("[SavePoint] Base prefab built: " + SavePointBasePath);
        }

        foreach (var v in SavePointVariants)
        {
            string path = $"{SavePointPrefabFolder}/{v.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[SavePoint] Prefab already exists, kept: " + path);
                continue;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
            inst.name = v.name;
            bool ok = AddSlotModel(inst.transform, "Desk", v.desk, preview)
                    & AddSlotModel(inst.transform, "Computer", v.pc, preview)
                    & AddSlotModel(inst.transform, "Chair", v.chair, preview);
            if (!ok) { Object.DestroyImmediate(inst); continue; }
            var station = inst.GetComponent<SaveStation>();
            SetString(station, SaveStation.LocationNameField, v.location);
            station.FitToModels();
            PrefabUtility.SaveAsPrefabAsset(inst, path);   // instance of the base -> saved as a Prefab Variant
            Object.DestroyImmediate(inst);
            Debug.Log($"[SavePoint] Variant built: {path} ({v.location})");
        }
    }

    private static bool AddSlotModel(Transform root, string slotName, string model, Scene preview)
    {
        Transform slot = FindDeep(root, slotName);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePointPropsFolder}/{model}.fbx");
        if (slot == null || asset == null)
        {
            Debug.LogWarning($"[SavePoint] Missing slot '{slotName}' or model {SavePointPropsFolder}/{model}.fbx");
            return false;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
        go.transform.SetParent(slot, false);
        return true;
    }

    private static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        var t = new GameObject(childName).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    internal static SaveStation PlaceSavePoint(Transform parent, string variant, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePointPrefabFolder}/{variant}.prefab");
        if (prefab == null) { Debug.LogWarning($"[Helena] No {variant} prefab: run menu 3 to test saving."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return go.GetComponentInChildren<SaveStation>();
    }

    internal static void SetReferenceArray<T>(Object target, string fieldName, List<T> values) where T : Object
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null || !prop.isArray) { Debug.LogError($"[Helena] Array field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // editor-time lookup inside an imported model (names come from our own export pipeline)
    internal static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Pickup PlacePickup(Transform parent, string name, Dictionary<string, ItemDefinition> items,
                                      int amount, Vector3 pos, float yaw, Color fallbackColor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath(name));
        if (prefab != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var placed = go.GetComponent<Pickup>();
            SetInt(placed, Pickup.AmountField, amount);
            return placed;
        }
        return CreateCubePickup(parent, items[name], amount, pos + Vector3.up * 0.15f, fallbackColor);
    }

    internal static Pickup CreateCubePickup(Transform parent, ItemDefinition item, int amount, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pickup_" + item.name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        var pickup = go.AddComponent<Pickup>();
        SetReference(pickup, Pickup.ItemField, item);
        SetInt(pickup, Pickup.AmountField, amount);

        Material mat = GetOrCreateColorMaterial("Pickup_" + item.name, color);
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return pickup;
    }

    private static Material GetOrCreateColorMaterial(string name, Color color)
    {
        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null || rp.defaultShader == null) return null;
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "TestMaterials");
        string path = TestMaterialsFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(rp.defaultShader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor(BaseColorId, color);
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static void SetInt(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetReference(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateBox(Transform parent, Vector3 pos, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Box";
        box.transform.SetParent(parent);
        box.transform.position = pos;
        box.transform.localScale = size;
        return box;
    }

    private static void AddPickupState(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                       string stateName, AnimationClip clip, PickupHeight height, Vector3 graphPosition)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.AddState(stateName, graphPosition);
        state.motion = clip;
        state.tag = PartnerAnimator.PickupTag;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.2f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.PickupParam);
            enter.AddCondition(AnimatorConditionMode.Equals, (int)height, PartnerAnimator.PickupHeightParam);
        }

        AnimatorStateTransition exit = state.AddTransition(locomotion);
        exit.hasExitTime = true;
        exit.exitTime = 0.92f;
        exit.duration = 0.25f;
    }

    private static AnimatorState AddAttackState(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                                string stateName, AnimationClip clip, int attackId, Vector3 graphPosition)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.AddState(stateName, graphPosition);
        state.motion = clip;
        state.tag = PartnerAnimator.AttackTag;
        state.speedParameterActive = true;
        state.speedParameter = PartnerAnimator.AttackSpeedParam;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AttackParam);
            enter.AddCondition(AnimatorConditionMode.Equals, attackId, PartnerAnimator.AttackIdParam);
        }
        return state;
    }

    private static void AddComboTransition(AnimatorState from, AnimatorState to, int attackId)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.12f;
        t.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AttackParam);
        t.AddCondition(AnimatorConditionMode.Equals, attackId, PartnerAnimator.AttackIdParam);
    }

    private static void AddAttackExit(AnimatorState state, AnimatorState armedLocomotion, float exitTime)
    {
        AnimatorStateTransition exit = state.AddTransition(armedLocomotion);
        exit.hasExitTime = true;
        exit.exitTime = exitTime;
        exit.duration = 0.25f;
    }

    // capsule target: Health + TrainingDummy, collider on the root, wobbling visual as a child, carves the NavMesh
    private static Health CreateTrainingDummy(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("TrainingDummy");
        root.transform.SetParent(parent);
        root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.25f;
        var obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = col.center;
        obstacle.height = col.height;
        obstacle.radius = col.radius;
        obstacle.carving = true;

        var visual = new GameObject("Visual").transform;   // pivot at the feet: it tilts from the base
        visual.SetParent(root.transform, false);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(visual, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        Material mat = GetOrCreateColorMaterial("TrainingDummy", new Color(0.62f, 0.5f, 0.32f));
        if (mat != null) bodyRenderer.sharedMaterial = mat;

        var health = root.AddComponent<Health>();
        var dummy = root.AddComponent<TrainingDummy>();
        SetReference(dummy, TrainingDummy.VisualField, visual);
        SetReference(dummy, TrainingDummy.BodyRendererField, bodyRenderer);
        return health;
    }

    // shootable targets of the command system, each answering to its key ("dummy", "zombie"...; "enemy" = any)
    private static void SetShootables(CommandTargets targets, List<(string key, Health target)> shootables)
    {
        var so = new SerializedObject(targets);
        SerializedProperty list = so.FindProperty(CommandTargets.ShootablesField);
        list.arraySize = shootables.Count;
        for (int i = 0; i < shootables.Count; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(CommandTargets.Shootable.KeyField).stringValue = shootables[i].key;
            e.FindPropertyRelative(CommandTargets.Shootable.TargetField).objectReferenceValue = shootables[i].target;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // combat overlay: red frame, title, target list + her health (left), floating numbers over the hostiles
    private static void BuildCombatHud(Transform canvas, CombatEncounter encounter, SecurityCameraSystem cameraSystem,
                                       CameraSettings settings, Health partnerHealth)
    {
        Color red = new Color(1f, 0.22f, 0.18f);
        var root = CreateUiObject(canvas, "CombatHud");
        root.transform.SetSiblingIndex(1);   // over the camera HUD, under subtitles / command line / save screen
        Stretch((RectTransform)root.transform);
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // frame: four thin bars on the screen edges
        foreach (var (min, max) in new[] { (new Vector2(0f, 1f), new Vector2(1f, 1f)), (Vector2.zero, new Vector2(1f, 0f)),
                                           (Vector2.zero, new Vector2(0f, 1f)), (new Vector2(1f, 0f), Vector2.one) })
        {
            Image bar = CreateUiObject(root.transform, "Frame").AddComponent<Image>();
            bar.color = new Color(red.r, red.g, red.b, 0.6f);
            bar.raycastTarget = false;
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            bool horizontal = min.y == max.y;
            rt.sizeDelta = horizontal ? new Vector2(0f, 6f) : new Vector2(6f, 0f);
        }

        var labels = CreateUiObject(root.transform, "TargetLabels").GetComponent<RectTransform>();
        Stretch(labels);

        Vector2 tl = new Vector2(0f, 1f);
        TMP_Text title = CreateText(root.transform, "Title", 30, TextAlignmentOptions.Left, red);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, tl, tl, tl, new Vector2(52f, -150f), new Vector2(300f, 36f));
        TMP_Text list = CreateText(root.transform, "Targets", 22, TextAlignmentOptions.TopLeft, red);
        Place(list.rectTransform, tl, tl, tl, new Vector2(52f, -190f), new Vector2(300f, 200f));

        TMP_Text name = CreateText(root.transform, "PartnerName", 18, TextAlignmentOptions.Left, Phosphor);
        name.text = "HELENA";
        Vector2 bl = Vector2.zero;
        Place(name.rectTransform, bl, bl, bl, new Vector2(52f, 96f), new Vector2(200f, 24f));
        Image back = CreateUiObject(root.transform, "PartnerBarBack").AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;
        Place(back.rectTransform, bl, bl, bl, new Vector2(52f, 80f), new Vector2(220f, 12f));
        Image fill = CreateUiObject(back.transform, "Fill").AddComponent<Image>();
        fill.raycastTarget = false;
        Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 12f));

        var hud = root.AddComponent<CombatHud>();
        SetReference(hud, CombatHud.EncounterField, encounter);
        SetReference(hud, CombatHud.CameraSystemField, cameraSystem);
        SetReference(hud, CombatHud.SettingsField, settings);
        SetReference(hud, CombatHud.PartnerHealthField, partnerHealth);
        SetReference(hud, CombatHud.GroupField, group);
        SetReference(hud, CombatHud.LabelsRootField, labels);
        SetReference(hud, CombatHud.TitleField, title);
        SetReference(hud, CombatHud.ListField, list);
        SetReference(hud, CombatHud.PartnerBarField, fill);
    }

    // game over: a red flash on every hit; on her death "SIGNAL LOST" + LOAD LAST SAVE / RESTART
    private static void BuildGameOver(Transform canvas, PartnerDamage damage, Health health, SaveSystem saveSystem)
    {
        Color red = new Color(0.9f, 0.12f, 0.1f);
        var root = CreateUiObject(canvas, "GameOver");
        root.transform.SetAsLastSibling();
        Stretch((RectTransform)root.transform);

        Image flash = CreateUiObject(root.transform, "HitFlash").AddComponent<Image>();   // outside the group: shown while playing
        Stretch(flash.rectTransform);
        flash.color = new Color(0.6f, 0f, 0f, 0f);
        flash.raycastTarget = false;

        var screenGo = CreateUiObject(root.transform, "Screen");
        Stretch((RectTransform)screenGo.transform);
        var group = screenGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        Image backdrop = CreateUiObject(screenGo.transform, "Backdrop").AddComponent<Image>();
        Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0.04f, 0f, 0f, 0.9f);

        RawImage scan = CreateUiObject(screenGo.transform, "Scanlines").AddComponent<RawImage>();
        Stretch(scan.rectTransform);
        scan.texture = EnsureScanlines();
        scan.uvRect = new Rect(0f, 0f, 1f, 180f);   // 720 px / 4 px per line
        scan.color = new Color(1f, 1f, 1f, 0.3f);
        scan.raycastTarget = false;

        Vector2 c = new Vector2(0.5f, 0.5f);
        TMP_Text title = CreateText(screenGo.transform, "Title", 64, TextAlignmentOptions.Center, red);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        title.text = "SIGNAL LOST";
        Place(title.rectTransform, c, c, c, new Vector2(0f, 110f), new Vector2(900f, 80f));
        TMP_Text detail = CreateText(screenGo.transform, "Detail", 26, TextAlignmentOptions.Center, new Color(0.85f, 0.75f, 0.72f));
        Place(detail.rectTransform, c, c, c, new Vector2(0f, 30f), new Vector2(900f, 80f));

        Button load = CreateButton(screenGo.transform, "LoadButton", "LOAD LAST SAVE", red, new Vector2(-150f, -80f));
        Button restart = CreateButton(screenGo.transform, "RestartButton", "RESTART", red, new Vector2(150f, -80f));

        var over = root.AddComponent<GameOverScreen>();
        SetReference(over, GameOverScreen.DamageField, damage);
        SetReference(over, GameOverScreen.HealthField, health);
        SetReference(over, GameOverScreen.SaveSystemField, saveSystem);
        SetReference(over, GameOverScreen.GroupField, group);
        SetReference(over, GameOverScreen.FlashField, flash);
        SetReference(over, GameOverScreen.DetailField, detail);
        SetReference(over, GameOverScreen.LoadButtonField, load);
        SetReference(over, GameOverScreen.RestartButtonField, restart);
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Color color, Vector2 position)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        Image back = CreateUiObject(parent, objectName).AddComponent<Image>();
        back.color = Color.white;
        Place(back.rectTransform, c, c, c, position, new Vector2(260f, 56f));
        var outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
        var button = back.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.02f, 0.02f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.05f, 0.04f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.55f, 0.08f, 0.06f, 1f);
        colors.disabledColor = new Color(0.08f, 0.06f, 0.06f, 0.6f);
        button.colors = colors;
        button.targetGraphic = back;
        TMP_Text text = CreateText(back.transform, "Label", 24, TextAlignmentOptions.Center, color);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    internal static void AddToBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // command line (Enter) above the subtitles, at the bottom of the operator's screen
    private static CommandConsole BuildCommandUi(Transform canvas, PartnerCommands commands, PartnerVoice voice, SecurityCameraSystem cameraSystem,
                                       SaveScreen saveScreen, CameraSettings settings)
    {
        Vector2 bc = new Vector2(0.5f, 0f);

        var subtitleGo = CreateUiObject(canvas, "Subtitles");
        subtitleGo.transform.SetSiblingIndex(1);   // over the camera HUD, under the save screen and fader
        var subtitleGroup = subtitleGo.AddComponent<CanvasGroup>();
        subtitleGroup.blocksRaycasts = false;
        Place((RectTransform)subtitleGo.transform, bc, bc, bc, new Vector2(0f, 96f), new Vector2(1000f, 90f));
        Image band = CreateUiObject(subtitleGo.transform, "Band").AddComponent<Image>();
        Stretch(band.rectTransform);
        band.color = new Color(0f, 0f, 0f, 0.55f);
        band.raycastTarget = false;
        TMP_Text subtitle = CreateText(subtitleGo.transform, "Text", 28, TextAlignmentOptions.Center, Color.white);
        Stretch(subtitle.rectTransform);
        subtitle.margin = new Vector4(20f, 6f, 20f, 6f);
        var view = subtitleGo.AddComponent<SubtitleView>();
        SetReference(view, SubtitleView.VoiceField, voice);
        SetReference(view, SubtitleView.SettingsField, settings);
        SetReference(view, SubtitleView.TextField, subtitle);
        SetReference(view, SubtitleView.GroupField, subtitleGroup);

        var consoleGo = CreateUiObject(canvas, "CommandConsole");
        consoleGo.transform.SetSiblingIndex(2);
        Place((RectTransform)consoleGo.transform, bc, bc, bc, new Vector2(0f, 196f), new Vector2(900f, 44f));
        var panelGo = CreateUiObject(consoleGo.transform, "Line");
        Stretch((RectTransform)panelGo.transform);
        var panel = panelGo.AddComponent<CanvasGroup>();
        panel.blocksRaycasts = false;
        Image back = CreateUiObject(panelGo.transform, "Back").AddComponent<Image>();
        Stretch(back.rectTransform);
        back.color = new Color(0f, 0.06f, 0.02f, 0.8f);
        back.raycastTarget = false;
        var frame = back.gameObject.AddComponent<Outline>();
        frame.effectColor = PhosphorDim;
        frame.effectDistance = new Vector2(2f, -2f);
        TMP_Text input = CreateText(panelGo.transform, "Input", 24, TextAlignmentOptions.Left, Phosphor);
        Stretch(input.rectTransform);
        input.margin = new Vector4(16f, 0f, 16f, 0f);
        TMP_Text echo = CreateText(consoleGo.transform, "Echo", 20, TextAlignmentOptions.Center, PhosphorDim);
        Stretch(echo.rectTransform);

        var console = consoleGo.AddComponent<CommandConsole>();
        SetReference(console, CommandConsole.CommandsField, commands);
        SetReference(console, CommandConsole.CameraSystemField, cameraSystem);
        SetReference(console, CommandConsole.SaveScreenField, saveScreen);
        SetReference(console, CommandConsole.SettingsField, settings);
        SetReference(console, CommandConsole.PanelField, panel);
        SetReference(console, CommandConsole.InputTextField, input);
        SetReference(console, CommandConsole.EchoTextField, echo);
        return console;
    }

    // a 1D child tree on Injured: the normal clip at 0, the hurt one at 1
    private static void HurtBlend(BlendTree child, AnimationClip normal, AnimationClip hurt)
    {
        child.blendType = BlendTreeType.Simple1D;
        child.blendParameter = PartnerAnimator.InjuredParam;
        child.useAutomaticThresholds = false;
        child.AddChild(normal, 0f);
        child.AddChild(hurt, 1f);
    }

    private static void AddDoorState(AnimatorController ctrl, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = ctrl.layers[0].stateMachine.AddState(name, position);
        state.motion = clip;
        state.tag = PartnerAnimator.DoorTag;
        state.speedParameter = PartnerAnimator.DoorSpeedParam;
        state.speedParameterActive = true;
    }

    private static void AddSeatStates(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                      AnimationClip standToSit, AnimationClip typing, AnimationClip sitToStand, AnimationClip[] dealerClips)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sit = sm.AddState(PartnerAnimator.SitDownState, new Vector3(300f, 300f, 0f));
        sit.motion = standToSit;
        sit.tag = PartnerAnimator.SeatTag;
        AnimatorState seated = sm.AddState(PartnerAnimator.SeatedState, new Vector3(550f, 300f, 0f));
        seated.motion = typing;
        seated.tag = PartnerAnimator.SeatTag;
        AnimatorState stand = sm.AddState(PartnerAnimator.StandUpState, new Vector3(550f, 380f, 0f));
        stand.motion = sitToStand;
        stand.tag = PartnerAnimator.SeatTag;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(sit);
            enter.hasExitTime = false;
            enter.duration = 0.25f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.SeatedParam);
        }
        // seated by ~0.9 of StandToSit (hips settle at 0.72, hands still moving): typing or dealing (SeatActivity)
        AnimatorStateTransition toTyping = sit.AddTransition(seated);
        toTyping.hasExitTime = true;
        toTyping.exitTime = 0.9f;
        toTyping.duration = 0.35f;
        toTyping.AddCondition(AnimatorConditionMode.Equals, (int)PartnerAnimator.SeatAnimation.Typing, PartnerAnimator.SeatActivityParam);

        // dealer (blackjack table): played from code (PartnerBlackjackDealer), back to holding / waiting at the end
        var dealer = new AnimatorState[dealerClips.Length];
        for (int i = 0; i < dealerClips.Length; i++)
        {
            dealer[i] = sm.AddState(PartnerAnimator.DealerStates[i], new Vector3(300f + 200f * (i % 3), 560f + 70f * (i / 3), 0f));
            dealer[i].motion = dealerClips[i];
            dealer[i].tag = PartnerAnimator.SeatTag;
            dealer[i].speed = DealerSpeeds[i];
            AnimatorStateTransition up = dealer[i].AddTransition(stand);   // getting up from any dealer clip
            up.hasExitTime = false;
            up.duration = 0.3f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.SeatedParam);
        }
        AnimatorState dealerIdle = dealer[(int)PartnerAnimator.DealerClip.Idle];
        AnimatorState dealerHold = dealer[(int)PartnerAnimator.DealerClip.Hold];
        AnimatorStateTransition toDealer = sit.AddTransition(dealerIdle);
        toDealer.hasExitTime = true;
        toDealer.exitTime = 0.9f;
        toDealer.duration = 0.35f;
        toDealer.AddCondition(AnimatorConditionMode.Equals, (int)PartnerAnimator.SeatAnimation.Dealer, PartnerAnimator.SeatActivityParam);
        foreach (PartnerAnimator.DealerClip one in new[] { PartnerAnimator.DealerClip.PickDeck, PartnerAnimator.DealerClip.Deal, PartnerAnimator.DealerClip.Check })
        {
            AnimatorStateTransition back = dealer[(int)one].AddTransition(dealerHold);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.2f;
        }
        AnimatorStateTransition putBack = dealer[(int)PartnerAnimator.DealerClip.PutBack].AddTransition(dealerIdle);
        putBack.hasExitTime = true;
        putBack.exitTime = 0.95f;
        putBack.duration = 0.25f;
        AnimatorStateTransition toStand = seated.AddTransition(stand);
        toStand.hasExitTime = false;
        toStand.duration = 0.3f;
        toStand.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.SeatedParam);
        // standing still by ~0.9 of SitToStand
        AnimatorStateTransition toIdle = stand.AddTransition(locomotion);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.9f;
        toIdle.duration = 0.25f;
    }

    // Inventory starting stacks (item, amount)
    private static void SetStartingItems(Inventory inventory, params (ItemDefinition item, int amount)[] stacks)
    {
        var so = new SerializedObject(inventory);
        SerializedProperty list = so.FindProperty(Inventory.StartingItemsField);
        if (list == null) { Debug.LogError("[Helena] Inventory.startingItems not found."); return; }
        list.arraySize = stacks.Length;
        for (int i = 0; i < stacks.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative(ItemStack.ItemField).objectReferenceValue = stacks[i].item;
            e.FindPropertyRelative(ItemStack.AmountField).intValue = stacks[i].amount;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // every test item, so saves can turn ids back into assets (refreshed each time)
    private static ItemDatabase EnsureItemDatabase(Dictionary<string, ItemDefinition> items)
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, ItemDatabasePath);
        }
        SetReferenceArray(db, ItemDatabase.ItemsField, items.Values.ToList());
        EditorUtility.SetDirty(db);
        return db;
    }

    // ------------------------------------------------------------------ UI (uGUI + TextMeshPro)
    private static readonly Color Phosphor = new Color(0.45f, 1f, 0.55f);
    private static readonly Color PhosphorDim = new Color(0.2f, 0.55f, 0.28f);

    // Canvas: ammo HUD (bottom right), save screen (terminal panel + scanlines), black fader on top.
    private static SaveScreen BuildUi(Scene scene, SaveSystem saveSystem, PartnerShooter shooter)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        var canvasGo = new GameObject("UI", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        canvasGo.layer = uiLayer;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();   // map / settings buttons
        var eventSystem = new GameObject("EventSystem");
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        // ammo HUD
        TMP_Text ammoText = CreateText(canvasGo.transform, "AmmoHud", 44, TextAlignmentOptions.BottomRight, new Color(0.9f, 0.95f, 0.85f));
        Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(420f, 130f));
        var hud = ammoText.gameObject.AddComponent<AmmoHud>();
        SetReference(hud, AmmoHud.ShooterField, shooter);
        SetReference(hud, AmmoHud.TextField, ammoText);

        // save screen
        var screenGo = CreateUiObject(canvasGo.transform, "SaveScreen");
        Stretch((RectTransform)screenGo.transform);
        var panel = screenGo.AddComponent<CanvasGroup>();
        Image backdrop = CreateUiObject(screenGo.transform, "Backdrop").AddComponent<Image>();
        Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0f, 0f, 0.85f);
        backdrop.raycastTarget = false;

        Image terminal = CreateUiObject(screenGo.transform, "Terminal").AddComponent<Image>();
        Place(terminal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
        terminal.color = new Color(0.015f, 0.06f, 0.03f, 1f);
        terminal.raycastTarget = false;
        var frame = terminal.gameObject.AddComponent<Outline>();
        frame.effectColor = PhosphorDim;
        frame.effectDistance = new Vector2(3f, -3f);

        Vector2 topLeft = new Vector2(0f, 1f);
        TMP_Text header = CreateText(terminal.transform, "Header", 26, TextAlignmentOptions.TopLeft, Phosphor);
        Place(header.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -30f), new Vector2(920f, 110f));
        TMP_Text body = CreateText(terminal.transform, "Body", 26, TextAlignmentOptions.TopLeft, Phosphor);
        Place(body.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -150f), new Vector2(920f, 240f));
        TMP_Text status = CreateText(terminal.transform, "Status", 24, TextAlignmentOptions.TopLeft, Phosphor);
        Place(status.rectTransform, topLeft, topLeft, topLeft, new Vector2(40f, -400f), new Vector2(920f, 80f));
        Vector2 bottomLeft = Vector2.zero;
        TMP_Text hint = CreateText(terminal.transform, "Hint", 18, TextAlignmentOptions.BottomLeft, PhosphorDim);
        Place(hint.rectTransform, bottomLeft, bottomLeft, bottomLeft, new Vector2(40f, 22f), new Vector2(920f, 30f));

        RawImage scan = CreateUiObject(terminal.transform, "Scanlines").AddComponent<RawImage>();
        Stretch(scan.rectTransform);
        scan.texture = EnsureScanlines();
        scan.uvRect = new Rect(0f, 0f, 1f, 140f);   // 560 px / 4 px per line
        scan.color = new Color(1f, 1f, 1f, 0.35f);
        scan.raycastTarget = false;

        // fader (on top of everything)
        Image fade = CreateUiObject(canvasGo.transform, "Fader").AddComponent<Image>();
        Stretch(fade.rectTransform);
        fade.color = Color.black;
        fade.raycastTarget = false;
        var fader = fade.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f;
        panel.alpha = 0f;

        var screen = screenGo.AddComponent<SaveScreen>();
        SetReference(screen, SaveScreen.SaveSystemField, saveSystem);
        SetReference(screen, SaveScreen.PanelField, panel);
        SetReference(screen, SaveScreen.FaderField, fader);
        SetReference(screen, SaveScreen.HeaderTextField, header);
        SetReference(screen, SaveScreen.BodyTextField, body);
        SetReference(screen, SaveScreen.StatusTextField, status);
        SetReference(screen, SaveScreen.HintTextField, hint);
        return screen;
    }

    internal static GameObject CreateUiObject(Transform parent, string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    internal static TMP_Text CreateText(Transform parent, string objectName, float size, TextAlignmentOptions align, Color color)
    {
        var text = CreateUiObject(parent, objectName).AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    internal static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 1 x 4 texture: 2 clear rows + 2 dark rows, repeated = CRT scanlines
    internal static Texture2D EnsureScanlines()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ScanlinesPath);
        if (tex != null) return tex;
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "UI");
        tex = new Texture2D(1, 4, TextureFormat.RGBA32, false)
        {
            name = "Scanlines",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
        };
        tex.SetPixels(new[] { new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.8f), new Color(0f, 0f, 0f, 0.8f) });
        tex.Apply();
        AssetDatabase.CreateAsset(tex, ScanlinesPath);
        return tex;
    }

    internal static void SetString(Object target, string fieldName, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
