using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// The rig every location scene gets (BuildLocation): the content builds the level, then Helena and everything that
// runs her - her components, the save system and screens, the operator's camera program, commands and voice, the
// casino games the content placed, elevators, his room and the cutscene director - and the scene is saved with its NavMesh.
// LocationContent is what a content builder hands over; LocationRig is what its Finish callback gets back.
internal static class LocationRigBuilder
{
    // What a scene puts in the world (the rig - Helena, the systems, the UI, the cameras - is the same in every scene).
    internal class LocationContent
    {
        // The level built (rooms, doors, cameras...).
        public FacilityBuilder.Level Level;
        // Things placed by the content (wired to the systems by the rig).
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

    // An enemy to place.
    internal struct EnemySpec
    {
        public string Prefab;         // Enemy_Zombie, Enemy_Zombie_Pipe, Enemy_Killer_Axe, Enemy_Runner
        // Pose, start mode and save id.
        public Vector3 Position;
        public float Yaw;
        public EnemyStartMode Mode;
        public string SaveId;
    }

    // what the content's Finish gets: the systems of the scene
    internal class LocationRig
    {
        // What the content's Finish gets: the scene and its main systems.
        public Scene Scene;
        public GameObject Helena;
        public CutsceneDirector Director;
        public SecurityCameraSystem CameraSystem;
        public CameraSettings Settings;
        public StoryProgress Story;
        public CommandTargets Targets;
        public PartnerVoice Voice;
        public Camera ViewCamera;     // the operator's view (sits on the active CCTV camera)
        // Enemies placed.
        public readonly List<EnemyBrain> Enemies = new List<EnemyBrain>();
    }

    // Builds a scene: its content (rooms, cameras, things) + Helena and everything that runs her (the rig).
    // Saved with its NavMesh and added to the Build Settings (elevators and saves load scenes by name).
    internal static void BuildLocation(string scenePath, string navMeshPath, System.Func<Scene, Shader, LocationContent> buildContent)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PartnerAnimatorBuilder.ControllerPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HelenaSetupMenu.CharacterModelPath);
        if (HelenaSetupMenu.UseHelena2 && AssetDatabase.LoadAssetAtPath<Material>(HelenaCompareBuilder.BodyMaterialPath) == null)
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

        Shader lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LocationContent content = buildContent(scene, lit);
        if (content == null) return;

        // the rig, in this order (later steps use what the earlier ones made)
        var rig = new RigBuild(scene, lit, content);
        rig.AddCharacter(ctrl, prefab);
        rig.AddGameplay();
        rig.AddCombat();
        rig.AddGameSystems();
        rig.AddViewCamera();
        if (rig.CanRunCameraProgram) rig.AddCameraProgram();
        else Debug.LogWarning("[Helena] No CCTV prefab / UI: run menu 3 (and import TMP) to get the camera system.");

        // save scene + NavMesh as assets (an unsaved NavMesh is lost on Play)
        EditorAssets.EnsureFolder("Assets", "Scenes");
        AssetDatabase.DeleteAsset(navMeshPath);
        AssetDatabase.CreateAsset(content.Level.Surface.navMeshData, navMeshPath);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorAssets.AddToBuild(scenePath);   // RESTART (game over) reloads it by build index; elevators load it by name
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = rig.Helena;
        Debug.Log("[Helena] Scene saved at " + scenePath + ". " + content.Description);
    }

    // One run of BuildLocation: what each step made is kept here for the steps after it.
    private sealed class RigBuild
    {
        // The new scene, the lit shader, what the content placed and its level.
        private readonly Scene scene;
        private readonly Shader lit;
        private readonly LocationContent content;
        private readonly FacilityBuilder.Level level;

        // Helena and her components.
        public GameObject Helena { get; private set; }
        private NavMeshAgent agent;
        private PartnerAnimator partnerAnimator;
        private PartnerOutfit outfit;
        private Health health;
        private Inventory inventory;
        private PartnerInteractor interactor;
        private PartnerTestHarness harness;
        private PartnerCombat combat;              // null when there are no weapon prefabs (menu 3 not run)
        private PartnerShooter shooter;
        private WeaponHolder holder;
        private PartnerSaveUser saveUser;
        private PartnerDoorUser doorUser;
        private PartnerVoice voice;
        private PartnerEvasion evasion;
        private PartnerCommands commands;
        private PartnerKeypadUser keypadUser;
        private PartnerDamage damage;

        // The scene's systems.
        private GameSession session;
        private SaveSystem saveSystem;
        private SaveScreen saveScreen;             // also the parent canvas of every HUD
        private ChipBank chipBank;
        private StoryProgress story;
        private CommandTargets targets;
        private CombatEncounter encounter;
        private List<EnemyBrain> enemies;

        // The operator's side: his view, the test inputs, the camera program, his screen.
        private Camera viewCamera;
        private AudioListener viewListener;
        private ClickToMoveTest click;
        private CombatTestInput combatInput;       // null without combat
        private CameraSettings camSettings;
        private SecurityCameraSystem cameraSystem;
        private SecurityCamera shotCamera;
        private Volume volume;
        private Transform canvas;
        private CommandConsole console;
        private VoiceCommandInput mic;
        private OperatorStation station;           // null when his room is its own scene

        public RigBuild(Scene scene, Shader lit, LocationContent content)
        {
            this.scene = scene;
            this.lit = lit;
            this.content = content;
            level = content.Level;
        }

        // The camera program needs the CCTV cameras of the level and the screen UI.
        public bool CanRunCameraProgram => level.Cameras.Count > 0 && saveScreen != null;

        // Pickups get a save id; her model with the Animator, the NavMeshAgent, PartnerAnimator and her outfits.
        public void AddCharacter(RuntimeAnimatorController ctrl, GameObject prefab)
        {
            // pickups get a save id (unique in the scene)
            List<Pickup> placed = content.Pickups;
            for (int i = 0; i < placed.Count; i++) EditorSerialized.SetString(placed[i], Pickup.SaveIdField, $"pickup_{i:00}_{placed[i].Item.Id}");

            Helena = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Helena.name = "Helena";
            Helena.transform.SetPositionAndRotation(level.PartnerStart, Quaternion.Euler(0f, level.PartnerStartYaw, 0f));
            Animator animator = Helena.GetComponent<Animator>();
            if (animator == null) animator = Helena.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = true;   // "Handled by Script": PartnerAnimator.OnAnimatorMove

            agent = Helena.AddComponent<NavMeshAgent>();
            agent.speed = PartnerAnimatorBuilder.WalkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.05f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;

            partnerAnimator = Helena.AddComponent<PartnerAnimator>();
            EditorSerialized.SetReference(partnerAnimator, PartnerAnimator.AgentField, agent);

            // outfits (skins): same model, other materials
            outfit = OutfitBuilder.AddTo(Helena, lit, content.StartOutfit);
        }

        // Health, slot inventory (spare ammo unless she starts with nothing), interaction, test keys (H heal / J damage).
        public void AddGameplay()
        {
            health = Helena.AddComponent<Health>();
            inventory = Helena.AddComponent<Inventory>();
            EditorSerialized.SetInt(inventory, Inventory.SlotCountField, 8);
            Dictionary<string, ItemDefinition> startItems = ItemBuilder.EnsureTestItems();
            if (!content.BareStart)
                SetStartingItems(inventory, (startItems["HandgunAmmo"], 14), (startItems["ShotgunShells"], 6), (startItems["MagnumAmmo"], 7));   // spare ammo to test reloads
            interactor = Helena.AddComponent<PartnerInteractor>();
            EditorSerialized.SetReference(interactor, PartnerInteractor.PartnerAnimatorField, partnerAnimator);
            harness = Helena.AddComponent<PartnerTestHarness>();
        }

        // Loadout in the right hand, draw / holster / switch / aim / melee (G / 1..9 / right mouse / F).
        public void AddCombat()
        {
            var loadout = new List<Weapon>();
            foreach (var def in ItemBuilder.Weapons)
            {
                var w = AssetDatabase.LoadAssetAtPath<Weapon>(ItemBuilder.WeaponPrefabPath(def.Name));
                if (w != null) loadout.Add(w);
            }
            if (loadout.Count == 0)
            {
                Debug.LogWarning("[Helena] No weapon prefabs in " + ItemBuilder.WeaponPrefabFolder + ": run menu 3 to test combat.");
                return;
            }

            holder = Helena.AddComponent<WeaponHolder>();
            EditorSerialized.SetReference(holder, WeaponHolder.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReferenceArray(holder, WeaponHolder.LoadoutField, loadout);
            if (content.BareStart)
            {
                // her weapons are only the ones she picked up (WeaponItem in her inventory)
                EditorSerialized.SetReference(holder, WeaponHolder.InventoryField, inventory);
                var hso = new SerializedObject(holder);
                hso.FindProperty(WeaponHolder.RequireItemsField).boolValue = true;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }
            var melee = Helena.AddComponent<PartnerMelee>();
            EditorSerialized.SetReference(melee, PartnerMelee.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(melee, PartnerMelee.WeaponHolderField, holder);
            var audio = Helena.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            shooter = Helena.AddComponent<PartnerShooter>();
            EditorSerialized.SetReference(shooter, PartnerShooter.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(shooter, PartnerShooter.WeaponHolderField, holder);
            EditorSerialized.SetReference(shooter, PartnerShooter.EffectsMaterialField, GraphicsSettings.currentRenderPipeline.defaultParticleMaterial);
            EditorSerialized.SetReference(shooter, PartnerShooter.AudioSourceField, audio);
            var reloadVisuals = Helena.AddComponent<ReloadVisuals>();
            EditorSerialized.SetReference(reloadVisuals, ReloadVisuals.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(reloadVisuals, ReloadVisuals.ShooterField, shooter);
            combat = Helena.AddComponent<PartnerCombat>();
            EditorSerialized.SetReference(combat, PartnerCombat.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(combat, PartnerCombat.WeaponHolderField, holder);
            EditorSerialized.SetReference(combat, PartnerCombat.InteractorField, interactor);
            EditorSerialized.SetReference(combat, PartnerCombat.MeleeField, melee);
            EditorSerialized.SetReference(combat, PartnerCombat.ShooterField, shooter);
        }

        // Save files + UI (HUD, save screen) + her save and door routines. Needs combat (the save keeps her weapons).
        public void AddGameSystems()
        {
            session = ElevatorBuilder.EnsureSession();
            if (combat == null) return;

            var systems = new GameObject("GameSystems");
            saveSystem = systems.AddComponent<SaveSystem>();
            EditorSerialized.SetReference(saveSystem, SaveSystem.AgentField, agent);
            EditorSerialized.SetReference(saveSystem, SaveSystem.HealthField, health);
            EditorSerialized.SetReference(saveSystem, SaveSystem.InventoryField, inventory);
            EditorSerialized.SetReference(saveSystem, SaveSystem.CombatField, combat);
            EditorSerialized.SetReference(saveSystem, SaveSystem.ShooterField, shooter);
            EditorSerialized.SetReference(saveSystem, SaveSystem.WeaponHolderField, holder);
            EditorSerialized.SetReference(saveSystem, SaveSystem.ItemDatabaseField, ItemBuilder.EnsureItemDatabase(ItemBuilder.AllItems()));
            EditorSerialized.SetReferenceArray(saveSystem, SaveSystem.PickupsField, content.Pickups);
            EditorSerialized.SetReference(saveSystem, SaveSystem.SessionField, session);   // her state between scenes (elevators)
            chipBank = systems.AddComponent<ChipBank>();   // the operator's chips and prize coins (saved, carried between scenes)
            EditorSerialized.SetReference(saveSystem, SaveSystem.ChipBankField, chipBank);
            EditorSerialized.SetReference(saveSystem, SaveSystem.OutfitField, outfit);

            saveScreen = LocationUiBuilder.BuildUi(scene, saveSystem, shooter);

            saveUser = Helena.AddComponent<PartnerSaveUser>();
            EditorSerialized.SetReference(saveUser, PartnerSaveUser.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(saveUser, PartnerSaveUser.InteractorField, interactor);
            EditorSerialized.SetReference(saveUser, PartnerSaveUser.CombatField, combat);
            EditorSerialized.SetReference(saveUser, PartnerSaveUser.SaveScreenField, saveScreen);
            EditorSerialized.SetReference(saveUser, PartnerSaveUser.SaveSystemField, saveSystem);

            doorUser = Helena.AddComponent<PartnerDoorUser>();
            EditorSerialized.SetReference(doorUser, PartnerDoorUser.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(doorUser, PartnerDoorUser.InteractorField, interactor);
            EditorSerialized.SetReference(doorUser, PartnerDoorUser.CombatField, combat);
        }

        // The operator's view: one camera that sits on the active CCTV camera (SecurityCameraSystem), with the test inputs.
        public void AddViewCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            viewCamera = camGo.AddComponent<Camera>();
            viewCamera.nearClipPlane = 0.05f;
            viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            viewListener = camGo.AddComponent<AudioListener>();
            click = camGo.AddComponent<ClickToMoveTest>();
            EditorSerialized.SetReference(click, ClickToMoveTest.PartnerField, interactor);
            if (saveUser != null) EditorSerialized.SetReference(click, ClickToMoveTest.SaveUserField, saveUser);
            if (doorUser != null) EditorSerialized.SetReference(click, ClickToMoveTest.DoorUserField, doorUser);
            if (combat != null)
            {
                combatInput = camGo.AddComponent<CombatTestInput>();
                EditorSerialized.SetReference(combatInput, CombatTestInput.CombatField, combat);
            }
        }

        // Camera system + settings + filter + HUD; then (when she has her routines) everything that runs on commands.
        public void AddCameraProgram()
        {
            var security = new GameObject("SecurityRoom");
            camSettings = security.AddComponent<CameraSettings>();
            cameraSystem = security.AddComponent<SecurityCameraSystem>();
            EditorSerialized.SetReference(cameraSystem, SecurityCameraSystem.ViewCameraField, viewCamera);
            shotCamera = CutsceneBuilder.CreateShotCamera(level.ContentRoot);   // cutscenes' free shots
            level.Cameras.Add(shotCamera);
            EditorSerialized.SetReferenceArray(cameraSystem, SecurityCameraSystem.CamerasField, level.Cameras);
            EditorSerialized.SetReference(cameraSystem, SecurityCameraSystem.SettingsField, camSettings);
            EditorSerialized.SetReference(cameraSystem, SecurityCameraSystem.SaveScreenField, saveScreen);
            EditorSerialized.SetInt(cameraSystem, SecurityCameraSystem.StartCameraField, level.StartCamera);
            volume = security.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = FacilityBuilder.EnsureFilterProfile();
            FacilityBuilder.BuildCameraHud(saveScreen.transform.parent, cameraSystem, camSettings, Helena.transform, level.Rooms, level.Doors, volume,
                                           content.PlaceName, content.PlaceNamePt);
            EditorSerialized.SetReference(click, ClickToMoveTest.CameraSystemField, cameraSystem);
            if (combatInput != null) EditorSerialized.SetReference(combatInput, CombatTestInput.CameraSystemField, cameraSystem);
            EditorSerialized.SetReference(harness, PartnerTestHarness.CameraSystemField, cameraSystem);

            if (doorUser == null) return;
            AddCommandTargets();
            AddCommands();
            AddDamage();
            AddCommandUi();
            AddCasino();
            AddElevators();
            LocationUiBuilder.BuildGameOver(canvas, damage, health, saveSystem);   // last: over every HUD
            AddOperator();
            AddCutscenes();
        }

        // What commands can name (rooms, doors, things, tables...), the enemies (shootable, saved with her) and the encounter.
        private void AddCommandTargets()
        {
            targets = level.ContentRoot.gameObject.AddComponent<CommandTargets>();
            EditorSerialized.SetReferenceArray(targets, CommandTargets.RoomsField, level.Rooms);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.DoorsField, level.Doors);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.PickupsField, content.Pickups);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.StationsField, content.Stations);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.TablesField, content.Tables);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.ElevatorsField, content.Elevators);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.SlotsField, content.Slots);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.HoldemTablesField, content.HoldemTables);
            EditorSerialized.SetReference(targets, CommandTargets.RouletteField, content.Roulette);
            EditorSerialized.SetReference(targets, CommandTargets.CounterField, content.Counter);
            // enemies (Last Terminal/7 prefabs): shootable by their type's name ("zombie", "killer") or "enemy", saved with her
            var enemyRoot = new GameObject("Enemies");
            enemies = content.TestEnemies
                ? EnemyBuilder.PlaceTestEnemies(scene, enemyRoot.transform, Helena.transform, health, shooter, targets)
                : new List<EnemyBrain>();
            foreach (EnemySpec e in content.Enemies)
            {
                EnemyBrain brain = EnemyBuilder.PlaceEnemy(scene, enemyRoot.transform, e.Prefab, e.Position, e.Yaw, e.Mode, e.SaveId,
                                                           Helena.transform, health, shooter, targets);
                if (brain != null) enemies.Add(brain);
            }
            var shootables = new List<(string key, Health target)>();
            foreach (Health d in content.Dummies) shootables.Add((CommandTargets.DummyKey, d));
            foreach (EnemyBrain e in enemies) shootables.Add((e.Type.CommandKey, e.GetComponent<Health>()));
            SetShootables(targets, shootables);
            EditorSerialized.SetReferenceArray(saveSystem, SaveSystem.EnemiesField, enemies);
            EditorSerialized.SetReference(targets, CommandTargets.CameraSystemField, cameraSystem);
            EditorSerialized.SetReference(targets, CommandTargets.PartnerField, Helena.transform);
            EditorSerialized.SetReference(targets, CommandTargets.CenterRoomField, content.CenterRoom);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.KeypadsField, level.Keypads);
            EditorSerialized.SetReferenceArray(targets, CommandTargets.ExaminablesField, content.Examinables);
            encounter = targets.gameObject.AddComponent<CombatEncounter>();
            EditorSerialized.SetReference(encounter, CombatEncounter.TargetsField, targets);
            EditorSerialized.SetReference(encounter, CombatEncounter.PartnerField, Helena.transform);
            EditorSerialized.SetReference(encounter, CombatEncounter.PartnerHealthField, health);
        }

        // Her voice, evasion and PartnerCommands with every routine it drives (camera pose, combat stance, keypads...).
        private void AddCommands()
        {
            var voiceSource = Helena.AddComponent<AudioSource>();   // her radio: 2D, heard the same from any camera
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
            voice = Helena.AddComponent<PartnerVoice>();
            EditorSerialized.SetReference(voice, PartnerVoice.BankField, CommandDataBuilder.EnsureVoiceBank());
            EditorSerialized.SetReference(voice, PartnerVoice.SettingsField, camSettings);
            EditorSerialized.SetReference(voice, PartnerVoice.AudioSourceField, voiceSource);

            evasion = Helena.AddComponent<PartnerEvasion>();
            EditorSerialized.SetReference(evasion, PartnerEvasion.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(evasion, PartnerEvasion.HealthField, health);

            commands = Helena.AddComponent<PartnerCommands>();
            EditorSerialized.SetReference(commands, PartnerCommands.EvasionField, evasion);
            EditorSerialized.SetReference(commands, PartnerCommands.OutfitField, outfit);
            var pose = Helena.AddComponent<PartnerCameraPose>();   // "look at the camera"
            EditorSerialized.SetReference(pose, PartnerCameraPose.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(pose, PartnerCameraPose.InteractorField, interactor);
            EditorSerialized.SetReference(pose, PartnerCameraPose.CameraSystemField, cameraSystem);
            EditorSerialized.SetReference(pose, PartnerCameraPose.TargetsField, targets);
            EditorSerialized.SetReference(pose, PartnerCameraPose.VoiceField, voice);
            EditorSerialized.SetReference(commands, PartnerCommands.CameraPoseField, pose);
            var stance = Helena.AddComponent<PartnerCombatStance>();   // "combat position"
            EditorSerialized.SetReference(stance, PartnerCombatStance.CombatField, combat);
            EditorSerialized.SetReference(stance, PartnerCombatStance.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(stance, PartnerCombatStance.InteractorField, interactor);
            EditorSerialized.SetReference(stance, PartnerCombatStance.EvasionField, evasion);
            EditorSerialized.SetReference(stance, PartnerCombatStance.EncounterField, encounter);
            EditorSerialized.SetReference(stance, PartnerCombatStance.HealthField, health);
            EditorSerialized.SetReference(commands, PartnerCommands.StanceField, stance);
            keypadUser = Helena.AddComponent<PartnerKeypadUser>();   // "type 1 7", the slot machines' SPIN button
            EditorSerialized.SetReference(keypadUser, PartnerKeypadUser.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(keypadUser, PartnerKeypadUser.InteractorField, interactor);
            EditorSerialized.SetReference(keypadUser, PartnerKeypadUser.CombatField, combat);
            EditorSerialized.SetReference(commands, PartnerCommands.KeypadUserField, keypadUser);
            EditorSerialized.SetReference(commands, PartnerCommands.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(commands, PartnerCommands.InteractorField, interactor);
            EditorSerialized.SetReference(commands, PartnerCommands.CombatField, combat);
            EditorSerialized.SetReference(commands, PartnerCommands.ShooterField, shooter);
            EditorSerialized.SetReference(commands, PartnerCommands.WeaponHolderField, holder);
            EditorSerialized.SetReference(commands, PartnerCommands.DoorUserField, doorUser);
            EditorSerialized.SetReference(commands, PartnerCommands.SaveUserField, saveUser);
            EditorSerialized.SetReference(commands, PartnerCommands.HealthField, health);
            EditorSerialized.SetReference(commands, PartnerCommands.TargetsField, targets);
            EditorSerialized.SetReference(commands, PartnerCommands.VoiceField, voice);
            EditorSerialized.SetReference(commands, PartnerCommands.SettingsField, camSettings);
            EditorSerialized.SetReference(commands, PartnerCommands.EnglishField, CommandDataBuilder.EnsureLexicon(GameLanguage.English));
            EditorSerialized.SetReference(commands, PartnerCommands.PortugueseField, CommandDataBuilder.EnsureLexicon(GameLanguage.Portuguese));
            EditorSerialized.SetReference(commands, PartnerCommands.EncounterField, encounter);
            EditorSerialized.SetReference(click, ClickToMoveTest.CommandsField, commands);
            AssetDatabase.SaveAssets();
        }

        // Getting hurt: hit reactions, limping, death (the game over screen is added last, over every HUD).
        private void AddDamage()
        {
            damage = Helena.AddComponent<PartnerDamage>();
            EditorSerialized.SetReference(damage, PartnerDamage.HealthField, health);
            EditorSerialized.SetReference(damage, PartnerDamage.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(damage, PartnerDamage.EvasionField, evasion);
            EditorSerialized.SetReference(damage, PartnerDamage.CombatField, combat);
            EditorSerialized.SetReference(damage, PartnerDamage.InteractorField, interactor);
            EditorSerialized.SetReference(damage, PartnerDamage.VoiceField, voice);
        }

        // The command line, the microphone (hold V: same commands, spoken - Vosk, offline) and the combat HUD.
        private void AddCommandUi()
        {
            canvas = saveScreen.transform.parent;
            console = LocationUiBuilder.BuildCommandUi(canvas, commands, voice, cameraSystem, saveScreen, camSettings);
            mic = VoiceBuilder.Build(canvas, commands, cameraSystem, saveScreen, camSettings, console);
            LocationUiBuilder.BuildCombatHud(canvas, encounter, cameraSystem, camSettings, health);
        }

        // Blackjack, slot machines, Casino Hold'em, roulette, the prize counter and the close game camera (when the content has them).
        private void AddCasino()
        {
            // blackjack: her dealer hands + the tables + the panel
            var dealer = Helena.AddComponent<PartnerBlackjackDealer>();
            EditorSerialized.SetReference(dealer, PartnerBlackjackDealer.PartnerAnimatorField, partnerAnimator);
            foreach (BlackjackGame t in content.Tables) BlackjackBuilder.WireGame(t, chipBank, saveUser, dealer, voice, encounter, cameraSystem);
            if (content.Tables.Count > 0) BlackjackBuilder.BuildHud(canvas, content.Tables, camSettings);

            // slot machines: the chips they take and pay, their panel
            foreach (SlotMachine m in content.Slots) EditorSerialized.SetReference(m, SlotMachine.BankField, chipBank);
            if (content.Slots.Count > 0) CasinoBuilder.BuildHud(canvas, keypadUser, chipBank, camSettings);

            // Casino Hold'em (same dealer and seat routine), the roulette (she spins it like a button), the counter
            foreach (HoldemGame t in content.HoldemTables) BlackjackBuilder.WireHoldem(t, chipBank, saveUser, dealer, voice, encounter, cameraSystem);
            if (content.HoldemTables.Count > 0) CasinoBuilder.BuildHoldemHud(canvas, content.HoldemTables, camSettings);
            if (content.Roulette != null)
            {
                EditorSerialized.SetReference(content.Roulette, RouletteGame.BankField, chipBank);
                CasinoBuilder.BuildRouletteHud(canvas, content.Roulette, camSettings);
            }
            if (content.Counter != null)
            {
                EditorSerialized.SetReference(content.Counter, PrizeCounter.BankField, chipBank);
                EditorSerialized.SetReference(content.Counter, PrizeCounter.OutfitField, outfit);
                EditorSerialized.SetReference(content.Counter, PrizeCounter.PartnerField, agent);
                EditorSerialized.SetReferenceArray(content.Counter, PrizeCounter.CasesField, content.PrizeCases);
                foreach (PrizeCase c in content.PrizeCases)
                {
                    EditorSerialized.SetReference(c, PrizeCase.OwnerField, outfit);
                    EditorSerialized.SetReference(c, PrizeCase.SettingsField, camSettings);
                }
                CasinoBuilder.BuildCounterHud(canvas, content.Counter, Helena.transform, camSettings);
            }

            // the close camera of the slot machines and the roulette
            if (content.GameCamera != null)
            {
                var director = targets.gameObject.AddComponent<GameCameraDirector>();
                EditorSerialized.SetReference(director, GameCameraDirector.KeypadUserField, keypadUser);
                EditorSerialized.SetReference(director, GameCameraDirector.GameCameraField, content.GameCamera);
                EditorSerialized.SetReference(director, GameCameraDirector.CameraSystemField, cameraSystem);
                EditorSerialized.SetReference(director, GameCameraDirector.TargetsField, targets);
            }
        }

        // Elevators: her ride, the trip to the other scene (the dark screen with the floor), the signs' language.
        private void AddElevators()
        {
            if (content.Elevators.Count == 0) return;
            var travel = saveSystem.gameObject.AddComponent<ElevatorTravel>();
            var elevatorUser = Helena.AddComponent<PartnerElevatorUser>();
            EditorSerialized.SetReference(elevatorUser, PartnerElevatorUser.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(elevatorUser, PartnerElevatorUser.InteractorField, interactor);
            EditorSerialized.SetReference(elevatorUser, PartnerElevatorUser.CombatField, combat);
            EditorSerialized.SetReference(elevatorUser, PartnerElevatorUser.TravelField, travel);
            EditorSerialized.SetReference(commands, PartnerCommands.ElevatorUserField, elevatorUser);
            ElevatorBuilder.WireTravel(travel, canvas, saveSystem, session, elevatorUser, content.Elevators, camSettings, cameraSystem);
        }

        // The operator: story flags (his files), the things she comments on, Helena waiting on the floor while he is
        // away, and his own room (first person: the PC opens all of the above, Esc closes it; coffee, files, the pinball).
        private void AddOperator()
        {
            story = saveSystem.gameObject.AddComponent<StoryProgress>();
            EditorSerialized.SetReference(story, StoryProgress.SaveSystemField, saveSystem);
            EditorSerialized.SetReference(story, StoryProgress.EncounterField, encounter);
            EditorSerialized.SetReferenceArray(story, StoryProgress.KeypadsField, level.Keypads);
            EditorSerialized.SetReference(saveSystem, SaveSystem.StoryField, story);
            // "look at the bed": she walks up to things and comments on them
            var examiner = Helena.AddComponent<PartnerExaminer>();
            EditorSerialized.SetReference(examiner, PartnerExaminer.InteractorField, interactor);
            EditorSerialized.SetReference(examiner, PartnerExaminer.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(examiner, PartnerExaminer.VoiceField, voice);
            EditorSerialized.SetReference(examiner, PartnerExaminer.StoryField, story);
            EditorSerialized.SetReference(commands, PartnerCommands.ExaminerField, examiner);
            var rest = Helena.AddComponent<PartnerRest>();
            EditorSerialized.SetReference(rest, PartnerRest.PartnerAnimatorField, partnerAnimator);
            EditorSerialized.SetReference(rest, PartnerRest.AgentField, agent);
            EditorSerialized.SetReference(rest, PartnerRest.HealthField, health);
            var operatorRig = new OperatorRoomBuilder.Rig
            {
                ViewCamera = viewCamera, ViewListener = viewListener,
                OperatorCanvas = canvas.GetComponent<Canvas>(), CameraSystem = cameraSystem, Rest = rest,
                Partner = Helena.transform, Targets = targets, Encounter = encounter, Voice = voice,
                Session = session, Settings = camSettings, Story = story,
            };
            operatorRig.OperatorBehaviours.AddRange(new Behaviour[] { cameraSystem, console, mic, click, combatInput, harness, volume });
            // (the story scenes: not in the scene; it will be its own scene, loaded with them)
            station = content.EmbedOperatorRoom ? OperatorRoomBuilder.Build(lit, operatorRig) : null;
            // no room inside: his room (Operator.unity) loads this scene over it and takes this camera program
            if (station == null) OperatorRoomBuilder.AddLocationLink(scene, operatorRig);
        }

        // The cutscene director (Last Terminal/12 adds a cutscene to the scene), the facility's intro, then the content's Finish.
        private void AddCutscenes()
        {
            CutsceneDirector cutsceneDirector = CutsceneBuilder.Build(canvas, cameraSystem, shotCamera, station, encounter, Helena, partnerAnimator, voice, story, camSettings, mic, console);
            if (level.Rooms.Exists(r => r.DisplayName == "Padded Cell")) CutsceneBuilder.BuildIntro(scene, cutsceneDirector, cameraSystem, station);   // the facility: the intro
            if (content.Finish == null) return;
            var located = new LocationRig
            {
                Scene = scene, Helena = Helena, Director = cutsceneDirector, CameraSystem = cameraSystem, Settings = camSettings,
                Story = story, Targets = targets, Voice = voice, ViewCamera = viewCamera,
            };
            located.Enemies.AddRange(enemies);
            content.Finish(located);
        }
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
}
