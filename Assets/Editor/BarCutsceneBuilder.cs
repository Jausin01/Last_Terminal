using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Last Terminal/25 - Cutscene 02, the bar (design doc: CUTSCENE_02_Bar.md): the flashback between the asylum's isolation
// wing and the next ward. Two and a half years earlier, first person: he stands at a high table with Mia by the dance
// floor; up on the mezzanine, Jeremy and Helena (18) talk where nobody hears them; the crowd dances. Mia reaches out
// for their song - hard cut to black - Helena's voice, today, in his earpiece. Then back to the asylum.
// The set is the Barranco bar (PS1 environment pack, Art/Props/Bar: positions below are in its space, Unity metres),
// the crowd the NPC pack (Characters/NPC) on the Mixamo dances. Also makes the CCTV signal loading screen
// (Prefabs/UI/SignalLoader.prefab) used to enter and leave it.
// The whole script is one Cutscene (Cutscene_02_Bar, starts with the scene): edit its steps in the inspector, move
// the Shot_ / Spot_ / Look_ objects to adjust. Rebuilding replaces the scene.
internal static class BarCutsceneBuilder
{
    // Paths.
    private const string ArtFolder = "Assets/Art/Props/Bar";
    private const string MatFolder = ArtFolder + "/Materials";
    private const string CastFolder = "Assets/Characters/Cast";
    private const string CastModels = CastFolder + "/Models";
    private const string CastController = CastFolder + "/Cast.controller";     // Last Terminal/14 makes it
    private const string NpcFolder = "Assets/Characters/NPC";
    private const string DataFolder = "Assets/Data/Bar";
    private const string AudioFolder = "Assets/Audio/Bar";
    private const string ArmsModelPath = "Assets/Art/FirstPerson/SM_FPArms.fbx";
    private const string StaticTexPath = "Assets/Art/UI/T_UI_Static.png";
    internal const string LoaderPrefabPath = "Assets/Prefabs/UI/SignalLoader.prefab";
    internal const string SceneName = "Cutscene02_Bar";
    private const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
    private const float ArmsEye = 1.7432f;     // the arms model's eye height (as in cutscene 01)

    // Where things are (the bar's space): his eyes at the high table, Mia across it, the dance floor, the mezzanine.
    private static readonly Vector3 Eye = new Vector3(4.6f, 1.62f, -23.45f);
    private static readonly Vector3 Table = new Vector3(4.6f, 0f, -24.05f);
    private static readonly Vector3 MiaSpot = new Vector3(4.6f, 0f, -24.65f);
    private static readonly Rect Floor = new Rect(1.4f, -28.8f, 3.2f, 3.6f);          // x, z, width, depth
    private static readonly Vector3 JeremySeat = new Vector3(4.8f, 2.0f, -31.75f);    // the mezzanine floor is at 2 m (clear of its table, x 2.9..3.6)
    private static readonly Vector3 HelenaSeat = new Vector3(4.05f, 2.0f, -31.75f);
    private const float SeatHeight = 0.47f, SeatedLift = 0.08f;                        // as Michael's chair (cutscene 01)

    // The crowd: dancers (NPC folder names), the people at the bar and the bartender.
    private static readonly string[] Dancers =
    {
        "F_Civ_RedCropTop_Shorts", "M_Civ_RedTee_Khakis", "F_Civ_BlackCorset_Cargo", "M_Civ_LeatherJacket_RippedJeans",
        "F_Civ_Goth_BlackSkirt", "M_Civ_GreyVNeck_Shorts", "F_Civ_PrintTop_DenimShorts", "M_Civ_BlackSweater_GreenCargo",
        "F_Civ_DenimJacket_BandTee", "M_Civ_WhitePolo_Jeans", "F_Civ_YellowRaglan_CargoShorts", "M_Civ_TealPolo_BlackPants",
        "F_Civ_GreySweater_Fishnets", "M_Civ_BrownJacket_GreyJeans", "F_Civ_Redhead_BlackTee_Jeans", "M_Civ_GreySweatshirt",
    };
    private static readonly (string npc, Vector3 at, float yaw)[] BarPeople =
    {
        // at the counter between its stools (the stools: x 1.5, z -18.5 / -20.6 / -22.4 / -24.5)
        ("M_Civ_Mustache_GreenPolo", new Vector3(1.1f, 0f, -19.55f), 270f), ("F_Civ_BlackTee_Jeans", new Vector3(1.1f, 0f, -21.5f), 270f),
        ("M_Civ_Glasses_Overshirt", new Vector3(1.15f, 0f, -23.4f), 255f), ("M_Civ_WhiteShirt_Jeans", new Vector3(-1.3f, 0f, -21f), 90f),
    };
    private static readonly string[] DanceClips = { "Dance_Arms", "Dance_Groove", "Dance_Snake", "Dance_Step", "Dance_Wave" };

    // Build state.
    private static Shader lit;
    private static readonly List<string> missing = new List<string>();
    private static SerializedProperty steps;

    // ================================================================== the menu
    [MenuItem("Last Terminal/25 - Cutscene 02: Build Scene (Bar)")]
    public static void Build()
    {
        lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        missing.Clear();
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CastController);
        if (!AssetDatabase.IsValidFolder(ArtFolder) || ctrl == null)
        {
            Debug.LogError("[Bar] Art/Props/Bar or Characters/Cast/Cast.controller missing: extract the package, run Last Terminal/14 once (it makes the cast's controller).");
            return;
        }
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "Bar");
        EditorAssets.EnsureFolder("Assets", "Scenes");
        PrepareArt();
        SignalLoader loader = EnsureSignalLoader();
        RuntimeAnimatorController dance = DanceController(), idle = IdleController();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.12f, 0.13f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.09f, 0.06f, 0.07f);
        RenderSettings.fogStartDistance = 6f;
        RenderSettings.fogEndDistance = 30f;
        var settings = new GameObject("Settings").AddComponent<CameraSettings>();

        var camGo = new GameObject("CinemaCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.02f, 0.025f);
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 120f;
        cam.fieldOfView = 42f;
        camGo.AddComponent<AudioListener>();

        Transform set = BuildSet(out DanceLights lights);
        Transform marks = new GameObject("Marks").transform;
        // the cast
        CutsceneActor mia = Actor("Mia", "MIA", new Color(0.95f, 0.6f, 0.75f), ctrl, MiaSpot, 0f);
        CutsceneActor jeremy = Actor("Jeremy", "JEREMY", new Color(0.6f, 0.8f, 0.55f), ctrl, Seated(JeremySeat), 0f);
        CutsceneActor helena = Actor("HelenaBar", "HELENA", new Color(0.85f, 0.25f, 0.3f), ctrl, Seated(HelenaSeat), 0f);
        foreach (Vector3 seat in new[] { JeremySeat, HelenaSeat }) Chair(set, seat);
        // the crowd
        var crowd = new GameObject("Crowd").transform;
        var dancers = new List<Animator>();
        var rnd = new System.Random(7);
        foreach (string n in Dancers)
        {
            Vector3 at = FreeSpot(rnd, dancers.Select(a => a.transform.position).ToList());
            Animator dancer = Npc(crowd, n, at, (float)rnd.NextDouble() * 360f, dance);
            if (dancer != null) dancers.Add(dancer);
        }
        var standing = new List<Animator>();
        foreach (var p in BarPeople)
        {
            Animator a = Npc(crowd, p.npc, p.at, p.yaw, idle);
            if (a != null) standing.Add(a);
        }
        var dancing = crowd.gameObject.AddComponent<CrowdDancers>();
        EditorSerialized.SetReferenceArray(dancing, CrowdDancers.AnimatorsField, dancers);
        var so = new SerializedObject(dancing);
        SerializedProperty st = so.FindProperty(CrowdDancers.StatesField);
        st.arraySize = DanceClips.Length;
        for (int i = 0; i < DanceClips.Length; i++) st.GetArrayElementAtIndex(i).stringValue = DanceClips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var people = crowd.gameObject.AddComponent<CrowdDancers>();
        EditorSerialized.SetReferenceArray(people, CrowdDancers.AnimatorsField, standing);
        so = new SerializedObject(people);
        st = so.FindProperty(CrowdDancers.StatesField);
        st.arraySize = 1;
        st.GetArrayElementAtIndex(0).stringValue = "Idle";
        so.ApplyModifiedPropertiesWithoutUndo();

        // his arms (they reach for her hand at the end)
        CutsceneActor arms = ArmsActor(camGo.transform);
        // the music, the crowd, the screen and the director
        AudioSource party = Loop(set, "Music_Party", "A_Bar_Party", new Vector3(3f, 2.5f, -27f));
        AudioSource ourSong = Loop(set, "Music_OurSong", "A_Bar_OurSong", new Vector3(3f, 2.5f, -27f));
        AudioSource chatter = Loop(set, "Crowd", "A_Bar_Crowd", new Vector3(3f, 1.5f, -24f));
        CutsceneDirector director = InitiationBuilder.BuildScreenAndDirector(cam, settings, out GameObject ui);
        var back = new GameObject("FlashbackReturn").AddComponent<FlashbackReturn>();
        EditorSerialized.SetReference(back, FlashbackReturn.SessionField, ElevatorBuilder.EnsureSession());
        EditorSerialized.SetReference(back, FlashbackReturn.SettingsField, settings);
        EditorSerialized.SetReference(back, FlashbackReturn.LoaderField, loader);

        Cutscene cutscene = BuildCutscene(director, marks, camGo.transform, mia, jeremy, helena, arms, party, ourSong, chatter, back);
        camGo.transform.SetPositionAndRotation(marks.Find("Shot_Wide").position, marks.Find("Shot_Wide").rotation);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorAssets.AddToBuild(ScenePath);
        Selection.activeGameObject = cutscene.gameObject;
        if (missing.Count > 0) Debug.LogWarning("[Bar] Built with placeholders, missing: " + string.Join(", ", missing.Distinct()));
        Debug.Log($"[Bar] {ScenePath} built ({dancers.Count} dancers). Play: the flashback runs (Esc skips) and goes back to the asylum. Steps: select Cutscene_02_Bar.", cutscene);
    }

    // ================================================================== art
    // the bar's materials (M_Bar_x <- T_Bar_x.png): the atlas, its glowing lamps, glass, a mirror, the plants (cutout);
    // the NPC pack's big files scaled (0.4)
    private static void PrepareArt()
    {
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;
            var names = new HashSet<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material) names.Add(o.name);
            foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            bool changed = false;
            foreach (string n in names)
            {
                if (!n.StartsWith("M_Bar_")) continue;
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), BarMaterial(n));
                changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
        foreach (string cast in new[] { "Mia", "Jeremy", "HelenaBar" }) CastMaterials.Apply(cast, lit);   // (as menu 23)
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { NpcFolder })) EnsureNpcAvatar(AssetDatabase.GUIDToAssetPath(guid));
    }

    // The NPC files were replaced by normalized ones (metres, bind pose): the avatar mapping kept in their .meta still
    // describes the old skeleton (100x, turned), so the Humanoid avatar comes out unusable and they stand in their
    // A-pose. Once per file: the mapping cleared, the avatar made again from the file (automatic mapping).
    private const string NpcAvatarMark = "npc-avatar-2";
    private static void EnsureNpcAvatar(string path)
    {
        if (path.StartsWith(NpcImport.AnimFolder)) return;
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) return;
        if (mi.userData != NpcAvatarMark)
        {
            HumanDescription d = mi.humanDescription;
            d.human = new HumanBone[0];
            d.skeleton = new SkeletonBone[0];
            mi.humanDescription = d;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.sourceAvatar = null;
            mi.userData = NpcAvatarMark;
            mi.SaveAndReimport();
        }
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            Debug.LogWarning($"[Bar] {path}: its Humanoid avatar is not valid (it will not dance). Select the file > Rig > Avatar Definition: Create From This Model > Apply.", AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    // One material of the bar (by its name in the files).
    private static Material BarMaterial(string n)
    {
        string key = n.Substring("M_Bar_".Length);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Bar_{key}.png");
        Material m = EditorAssets.MakeMaterial($"{MatFolder}/{n}.mat", tex, key == "Mirror" ? 0.9f : 0.12f, key == "Plant", lit);
        if (key == "Mirror") m.SetColor("_BaseColor", new Color(0.35f, 0.37f, 0.4f));
        if (key == "AtlasGlow") OperatorRoomBuilder.Glow(m, tex, new Color(1f, 0.8f, 0.55f) * 1.4f);
        if (key == "Glass")
        {
            m.SetColor("_BaseColor", new Color(0.75f, 0.85f, 0.9f, 0.35f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================== the set
    // the bar model, the dance floor and its lights, the jukebox, the high table, the lamps
    private static Transform BuildSet(out DanceLights danceLights)
    {
        var root = new GameObject("Bar").transform;
        Model(root, $"{ArtFolder}/SM_Bar_Barranco.fbx", Vector3.zero, 0f, true);
        Model(root, $"{ArtFolder}/SM_Bar_Jukebox.fbx", new Vector3(5.65f, 0f, -26.0f), 270f, false);

        // the dance floor: lit tiles (their glow pulses with the beat)
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Bar_DanceFloor.png");
        Material floorMat = EditorAssets.MakeMaterial($"{MatFolder}/M_Bar_DanceFloor.mat", tex, 0.6f, false, lit);
        OperatorRoomBuilder.Glow(floorMat, tex, Color.white * 0.55f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.name = "DanceFloor";
        floor.transform.SetParent(root, false);
        floor.transform.SetPositionAndRotation(new Vector3(Floor.center.x, 0.012f, Floor.center.y), Quaternion.Euler(90f, 0f, 0f));
        floor.transform.localScale = new Vector3(Floor.width, Floor.height, 1f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
        floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        // the spots over it (they sweep and change color: DanceLights)
        var lightsGo = new GameObject("DanceLights");
        lightsGo.transform.SetParent(root, false);
        var spots = new List<Light>();
        foreach (Vector3 at in new[] { new Vector3(1.9f, 3.85f, -26f), new Vector3(4.1f, 3.85f, -26.2f), new Vector3(3.0f, 3.85f, -28.4f) })
        {
            var go = new GameObject("DanceSpot");
            go.transform.SetParent(lightsGo.transform, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 9f;
            l.spotAngle = 55f;
            l.innerSpotAngle = 20f;
            l.intensity = 9f;
            l.shadows = LightShadows.None;
            spots.Add(l);
        }
        danceLights = lightsGo.AddComponent<DanceLights>();
        EditorSerialized.SetReferenceArray(danceLights, DanceLights.LightsField, spots);
        EditorSerialized.SetReference(danceLights, DanceLights.FloorField, floor.GetComponent<MeshRenderer>());

        // the high table he and Mia stand at
        Material wood = Flat("TableWood", new Color(0.18f, 0.1f, 0.06f), 0.35f), metal = Flat("TableMetal", new Color(0.2f, 0.2f, 0.22f), 0.6f);
        Prim(PrimitiveType.Cylinder, root, "HighTableTop", Table + new Vector3(0f, 1.04f, 0f), new Vector3(0.7f, 0.025f, 0.7f), wood);
        Prim(PrimitiveType.Cylinder, root, "HighTablePost", Table + new Vector3(0f, 0.52f, 0f), new Vector3(0.07f, 0.52f, 0.07f), metal);
        Prim(PrimitiveType.Cylinder, root, "HighTableFoot", Table + new Vector3(0f, 0.015f, 0f), new Vector3(0.45f, 0.015f, 0.45f), metal);
        Material glass = Flat("Drink", new Color(0.85f, 0.45f, 0.15f), 0.8f);
        Prim(PrimitiveType.Cylinder, root, "Drink_Mia", Table + new Vector3(-0.12f, 1.11f, -0.14f), new Vector3(0.07f, 0.06f, 0.07f), glass);
        Prim(PrimitiveType.Cylinder, root, "Drink_His", Table + new Vector3(0.1f, 1.11f, 0.16f), new Vector3(0.07f, 0.06f, 0.07f), glass);

        // the lights of the room: warm over the bar, his table, the mezzanine (so Jeremy and Helena read from below)
        PointLight(root, "BarLight_1", new Vector3(0f, 3.4f, -18.5f), new Color(1f, 0.62f, 0.32f), 2.2f, 7f);
        PointLight(root, "BarLight_2", new Vector3(0f, 3.4f, -23.5f), new Color(1f, 0.62f, 0.32f), 2.2f, 7f);
        PointLight(root, "TableLight", Table + new Vector3(0f, 2.3f, 0.3f), new Color(1f, 0.72f, 0.48f), 1.6f, 4f);
        PointLight(root, "MezzanineLight", new Vector3(3.2f, 3.8f, -31.3f), new Color(1f, 0.66f, 0.42f), 2.4f, 4.5f);
        PointLight(root, "RoomFill", new Vector3(4f, 3.6f, -21f), new Color(0.6f, 0.35f, 0.5f), 1.0f, 12f);
        return root;
    }

    // a seated actor's root on a chair at `seat` (the clip's hips land on the seat)
    private static Vector3 Seated(Vector3 seat) => seat + new Vector3(0f, SeatedLift, 0.07f);

    // a plain bar chair (seat 0.47 m) facing +z, its back behind
    private static void Chair(Transform parent, Vector3 at)
    {
        Material leather = Flat("ChairLeather", new Color(0.25f, 0.05f, 0.05f), 0.3f), metal = Flat("TableMetal", new Color(0.2f, 0.2f, 0.22f), 0.6f);
        var c = new GameObject("Chair").transform;
        c.SetParent(parent, false);
        c.position = at;
        Prim(PrimitiveType.Cube, c, "Seat", at + new Vector3(0f, SeatHeight - 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.42f), leather);
        Prim(PrimitiveType.Cube, c, "Back", at + new Vector3(0f, 0.78f, -0.22f), new Vector3(0.44f, 0.52f, 0.05f), leather);
        foreach (Vector2 leg in new[] { new Vector2(-0.19f, 0.17f), new Vector2(0.19f, 0.17f), new Vector2(-0.19f, -0.2f), new Vector2(0.19f, -0.2f) })
            Prim(PrimitiveType.Cylinder, c, "Leg", at + new Vector3(leg.x, 0.22f, leg.y), new Vector3(0.03f, 0.22f, 0.03f), metal);
    }

    // a free spot on the dance floor: apart from the others, out of his line of sight to the mezzanine
    private static Vector3 FreeSpot(System.Random rnd, List<Vector3> taken)
    {
        for (int tries = 0; tries < 400; tries++)
        {
            float x = Floor.xMin + 0.25f + (float)rnd.NextDouble() * (Floor.width - 0.5f);
            float z = Floor.yMin + 0.25f + (float)rnd.NextDouble() * (Floor.height - 0.5f);
            float k = (z - Eye.z) / (JeremySeat.z - Eye.z);
            float lane = Mathf.Lerp(Eye.x, (JeremySeat.x + HelenaSeat.x) * 0.5f, k);
            if (Mathf.Abs(x - lane) < 0.45f) continue;
            if (taken.Any(p => (new Vector2(p.x - x, p.z - z)).sqrMagnitude < 0.7f * 0.7f)) continue;
            return new Vector3(x, 0f, z);
        }
        return new Vector3(Floor.xMin + (float)rnd.NextDouble() * Floor.width, 0f, Floor.yMin + (float)rnd.NextDouble() * Floor.height);
    }

    // ================================================================== people
    // a cast member as a cutscene actor (Cast/Models/<model>.fbx, the cast controller)
    private static CutsceneActor Actor(string model, string display, Color color, RuntimeAnimatorController ctrl, Vector3 at, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CastModels}/{model}.fbx");
        GameObject go;
        if (prefab != null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        else
        {
            missing.Add($"{CastModels}/{model}.fbx");
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        go.name = display.Substring(0, 1) + display.Substring(1).ToLowerInvariant();
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Animator animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var actor = go.AddComponent<CutsceneActor>();
        CutsceneBuilder.EnsureSlotClips(out AnimationClip a, out AnimationClip b);
        EditorSerialized.SetString(actor, CutsceneActor.DisplayNameField, display);
        EditorSerialized.SetReference(actor, CutsceneActor.AnimatorField, animator);
        EditorSerialized.SetReference(actor, CutsceneActor.SlotAField, a);
        EditorSerialized.SetReference(actor, CutsceneActor.SlotBField, b);
        var so = new SerializedObject(actor);
        so.FindProperty("nameColor").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
        return actor;
    }

    // one of the crowd: the NPC model with a controller (dances / idle)
    private static Animator Npc(Transform parent, string name, Vector3 at, float yaw, RuntimeAnimatorController ctrl)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{NpcFolder}/{name}/{name}.fbx");
        if (prefab == null) { missing.Add($"{NpcFolder}/{name}/{name}.fbx"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Animator animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        return animator;
    }

    // the dancers' controller: one state per dance clip (CrowdDancers picks one each)
    private static RuntimeAnimatorController DanceController()
    {
        string path = $"{DataFolder}/Dancers.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        int k = 0;
        foreach (string clip in DanceClips)
        {
            AnimatorState s = sm.AddState(clip, new Vector3(300f, 80f + 70f * k++, 0f));
            s.motion = FileClip($"{NpcImport.AnimFolder}{clip}.fbx");
            if (s.motion == null) missing.Add($"{NpcImport.AnimFolder}{clip}.fbx");
            if (k == 1) sm.defaultState = s;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // the people at the bar: an idle (the Mixamo one in Mary's file)
    private static RuntimeAnimatorController IdleController()
    {
        string path = $"{DataFolder}/BarPeople.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorState s = ctrl.layers[0].stateMachine.AddState("Idle", new Vector3(300f, 80f, 0f));
        s.motion = AssetDatabase.LoadAllAssetsAtPath(CastImport.MaryFile).OfType<AnimationClip>().FirstOrDefault(x => x.name == CastImport.MaryIdleClip);
        if (s.motion == null) missing.Add(CastImport.MaryIdleClip + " (in Mary.fbx)");
        ctrl.layers[0].stateMachine.defaultState = s;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // The first animation clip of a file.
    private static AnimationClip FileClip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    // his first person arms under the camera (off until he reaches for her hand)
    private static CutsceneActor ArmsActor(Transform cam)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModelPath);
        AnimationClip[] clips = prefab != null ? AssetDatabase.LoadAllAssetsAtPath(ArmsModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray() : new AnimationClip[0];
        AnimationClip relax = clips.FirstOrDefault(c => c.name.EndsWith(FirstPersonPlayer.RelaxClip));
        AnimationClip push = clips.FirstOrDefault(c => c.name.EndsWith(FirstPersonPlayer.PushClip));
        if (prefab == null || relax == null || push == null) { missing.Add(ArmsModelPath + " (relax / push.R)"); return null; }
        string path = $"{DataFolder}/BarArms.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = sm.AddState(FirstPersonPlayer.RelaxState, new Vector3(300f, 100f, 0f));
        idle.motion = relax;
        sm.defaultState = idle;
        AnimatorState reach = sm.AddState("Reach", new Vector3(550f, 100f, 0f));
        reach.motion = push;
        reach.speed = ReachSpeed;   // the knock, slowed: the hand goes out toward hers (the cut comes before it pulls back)
        var arms = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cam);
        arms.name = "Arms";
        arms.transform.localPosition = new Vector3(0f, -ArmsEye, 0f);
        arms.transform.localRotation = Quaternion.identity;
        Animator animator = arms.GetComponent<Animator>();
        if (animator == null) animator = arms.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Renderer r in arms.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        var actor = arms.AddComponent<CutsceneActor>();
        EditorSerialized.SetString(actor, CutsceneActor.DisplayNameField, "");
        EditorSerialized.SetReference(actor, CutsceneActor.AnimatorField, animator);
        EditorSerialized.SetString(actor, "speedParameter", "");
        reachLength = push.length;
        arms.SetActive(false);
        return actor;
    }
    private const float ReachSpeed = 0.35f;
    private static float reachLength = 1f;

    // ================================================================== the script
    private static Cutscene BuildCutscene(CutsceneDirector d, Transform marks, Transform cam, CutsceneActor mia, CutsceneActor jeremy, CutsceneActor helena,
                                          CutsceneActor arms, AudioSource party, AudioSource ourSong, AudioSource chatter, FlashbackReturn back)
    {
        // camera poses (all from his eyes: he only turns his head, then steps toward her) and where people look
        Vector3 miaFace = MiaSpot + new Vector3(0f, 1.5f, 0f);
        Vector3 mezzanine = (JeremySeat + HelenaSeat) * 0.5f + new Vector3(0f, 1.05f, 0f);
        Transform wide = Mark(marks, "Shot_Wide", Eye, new Vector3(4.3f, 1.8f, -29.5f));
        Transform onMia = Mark(marks, "Shot_Mia", Eye, miaFace);
        Transform toThem = Mark(marks, "Shot_Mezzanine", Eye, mezzanine);
        Transform reach = Mark(marks, "Shot_Reach", Eye + new Vector3(0f, -0.04f, -0.45f), miaFace + new Vector3(0f, -0.1f, 0f));
        Transform hisEyes = Mark(marks, "Look_HisEyes", Eye, Eye + Vector3.back);
        Transform floorDown = Mark(marks, "Look_DanceFloor", new Vector3(3.4f, 0.4f, -28.4f), Vector3.zero);
        Transform miaHead = Mark(marks, "Look_Mia", miaFace, Vector3.zero);
        Transform hand = Mark(marks, "Hand_MiaReach", new Vector3(4.52f, 1.22f, -23.92f), new Vector3(4.52f, 1.22f, -23.0f));
        // Helena's hand on Jeremy's shoulder: a point on his left shoulder that moves with him
        Transform shoulder = ShoulderMark(jeremy);

        var go = new GameObject("Cutscene_02_Bar");
        var cutscene = go.AddComponent<Cutscene>();
        EditorSerialized.SetReference(cutscene, Cutscene.DirectorField, d);
        EditorSerialized.SetString(cutscene, Cutscene.IdField, "bar_flashback");
        var trigger = go.AddComponent<CutsceneTrigger>();
        EditorSerialized.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
        var tso = new SerializedObject(trigger);
        tso.FindProperty(CutsceneTrigger.WhenField).enumValueIndex = (int)CutsceneTrigger.When.SceneStart;
        tso.ApplyModifiedPropertiesWithoutUndo();
        var so = new SerializedObject(cutscene);
        so.FindProperty("letterbox").boolValue = true;
        so.FindProperty("once").boolValue = false;   // (a memory: no save in this scene)
        so.FindProperty(Cutscene.ClearScreenAtEndField).boolValue = false;   // ends on black: the signal screen takes over
        steps = so.FindProperty(Cutscene.StepsField);

        // 1. the memory comes up (the signal screen fades over it): music, the crowd, everyone in place
        Add(Together(Shot(wide, 42f), Hand(0.6f), Loop(party, true, 0.75f, 0.2f), Loop(chatter, true, 0.45f, 0.2f),
                     State(mia, "TalkStand"), State(helena, "SeatedTalk"), State(jeremy, "SeatedIdle"),
                     Look(mia, hisEyes, 0.8f), Look(jeremy, floorDown, 0.7f)));
        Add(Wait(2.2f));
        Add(Think("Eu lembro dessa noite. De cada detalhe.", "I remember this night. Every detail."));
        Add(Move(onMia, 1.3f, 38f));
        Add(Say(mia, "Você tá sorrindo. Faz tempo que eu não via isso.", "You're smiling. I haven't seen that in a while."));
        Add(You("Tá tudo dando certo.", "Everything's working out."));
        Add(Say(mia, "Tá mesmo, né? O trabalho, a gente... até o Jeremy parou de implicar com o seu carro.",
                     "It really is, huh? Work, us... even Jeremy stopped picking on your car."));
        // 2. his eyes drift over her shoulder: up on the mezzanine, Jeremy and Helena
        Add(Move(toThem, 2.4f, 16f));
        Add(Think("O Jeremy e a Helena. Sempre num canto, conversando.", "Jeremy and Helena. Always off in a corner, talking."));
        Add(Wait(1.2f));
        Add(Move(onMia, 1.5f, 38f));
        Add(Say(mia, "Obrigada. Por ficar. Eu sei que eu não sou fácil.", "Thank you. For staying. I know I'm not easy."));
        Add(You("Eu não ia pra lugar nenhum.", "I wasn't going anywhere."));
        // 3. again: Jeremy's head goes down, Helena's hand on his shoulder, a glance at Mia
        Add(Together(Move(toThem, 2.2f, 20f), State(jeremy, "SeatedLean", 0.6f), State(helena, "SeatedIdle", 0.6f)));
        Add(new HandTargetStep { actor = helena, right = shoulder, weight = 0.85f, blend = 0.9f });
        Add(Wait(1.0f));
        Add(Look(jeremy, miaHead, 0.5f));
        Add(Wait(1.1f));
        Add(Look(jeremy, floorDown, 0.8f));
        Add(Think("Eu não percebi nada naquela noite.", "I didn't notice a thing that night."));
        Add(Move(wide, 1.6f, 42f));
        // 4. their song
        Add(Together(Loop(party, false, 0f, 2.2f), Loop(ourSong, true, 0.75f, 2.2f)));
        Add(Wait(1.6f));
        Add(State(mia, "ArmsOpen", 0.3f, 1.4f));
        Add(State(mia, "TalkStand", 0.4f));
        Add(Say(mia, "Não acredito. Essa é a nossa.", "No way. This is ours."));
        Add(new HandTargetStep { actor = mia, right = hand, weight = 1f, blend = 0.9f, orient = true });
        Add(Say(mia, "Vem. Só uma.", "Come on. Just one."));
        // 5. up there, Helena lets go of Jeremy and looks at him; holds it; looks away
        Add(new HandTargetStep { actor = helena, weight = 0f, blend = 0.6f });
        Add(Look(helena, hisEyes, 0.7f));
        Add(Wait(1.8f));
        Add(new LookStep { actor = helena, target = null, blend = 0.9f });
        // 6. he reaches for her hand... cut
        float reachTime = reachLength / ReachSpeed * 0.42f;
        if (arms != null)
            Add(Together(new SetActiveStep { target = arms.gameObject, active = true }, State(arms, "Reach", 0.2f), Move(reach, reachTime, 40f, false)));
        else Add(Move(reach, reachTime, 40f, false));
        Add(Wait(reachTime));
        Add(Together(new FadeStep { toBlack = true, duration = 0f }, Loop(party, false, 0f, 0.05f), Loop(ourSong, false, 0f, 0.05f), Loop(chatter, false, 0f, 0.05f)));
        if (arms != null) Add(new SetActiveStep { target = arms.gameObject, active = false });
        Add(Wait(1.6f));
        // 7. today: Helena in his earpiece
        Add(Voice("...Ei. Operador?", "...Hey. Operator?"));
        Add(Voice("Você ficou quieto. Aconteceu alguma coisa?", "You went quiet. Did something happen?"));
        Add(You("Tô aqui.", "I'm here."));
        Add(Voice("Então fala alguma coisa. Esse silêncio me dá nos nervos.", "Then say something. The silence gets on my nerves."));
        // 8. back to the asylum
        var ev = new EventStep();
        UnityEventTools.AddVoidPersistentListener(ev.onRun, back.Go);
        Add(ev);
        so.ApplyModifiedPropertiesWithoutUndo();
        return cutscene;
    }

    // ================================================================== step helpers
    private static void Add(CutsceneStep step)
    {
        steps.arraySize++;
        steps.GetArrayElementAtIndex(steps.arraySize - 1).managedReferenceValue = step;
    }
    private static ParallelStep Together(params CutsceneStep[] list) => new ParallelStep { steps = new List<CutsceneStep>(list) };
    private static SayStep Say(CutsceneActor who, string pt, string en) => new SayStep { actor = who, textPt = pt, textEn = en, pauseAfter = 0.35f };
    private static SayStep Voice(string pt, string en) => new SayStep { speaker = "HELENA", textPt = pt, textEn = en, pauseAfter = 0.4f };
    private static ThoughtStep Think(string pt, string en) => new ThoughtStep { textPt = pt, textEn = en, pauseAfter = 0.3f };
    private static OperatorLineStep You(string pt, string en) => new OperatorLineStep { textPt = pt, textEn = en };
    private static CameraShotStep Shot(Transform t, float fov) => new CameraShotStep { shot = t, label = "", fieldOfView = fov };
    private static CameraMoveStep Move(Transform t, float s, float fov = 0f, bool wait = true) => new CameraMoveStep { to = t, seconds = s, fieldOfView = fov, wait = wait };
    private static StateStep State(CutsceneActor a, string s, float blend = 0.35f, float wait = 0f) => new StateStep { actor = a, state = s, blend = blend, wait = wait };
    private static LookStep Look(CutsceneActor a, Transform t, float blend) => new LookStep { actor = a, target = t, weight = 0.85f, blend = blend };
    private static WaitStep Wait(float s) => new WaitStep { seconds = s };
    private static HandheldStep Hand(float amount) => new HandheldStep { amount = amount };
    private static SoundLoopStep Loop(AudioSource s, bool on, float volume, float fade) => new SoundLoopStep { source = s, play = on, volume = volume, fade = fade };

    // ================================================================== scene helpers
    // A model placed (its prefab unpacked); `whole`: the set itself (static).
    private static GameObject Model(Transform parent, string path, Vector3 pos, float yaw, bool whole)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { missing.Add(path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (whole)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // A primitive without collider.
    private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    // A plain colored material of the bar.
    private static Material Flat(string name, Color c, float smooth)
    {
        Material m = EditorAssets.MakeMaterial($"{MatFolder}/M_Bar_{name}.mat", null, smooth, false, lit);
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    // A point light.
    private static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // A looping sound of the scene (silent until a step plays it).
    private static AudioSource Loop(Transform parent, string name, string clip, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{clip}.wav");
        if (s.clip == null) missing.Add($"{AudioFolder}/{clip}.wav");
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    // A named pose: at `pos`, looking at `lookAt` (or no rotation when lookAt is zero).
    private static Transform Mark(Transform parent, string name, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        if (lookAt != Vector3.zero) t.rotation = Quaternion.LookRotation(lookAt - pos);
        return t;
    }

    // a point on Jeremy's left shoulder (a child of his upper arm bone: it follows him)
    private static Transform ShoulderMark(CutsceneActor jeremy)
    {
        Animator a = jeremy != null ? jeremy.GetComponent<Animator>() : null;
        Transform bone = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        var t = new GameObject("Hand_JeremyShoulder").transform;
        if (bone != null) { t.SetParent(bone, false); t.localPosition = new Vector3(0f, 0.04f, 0f); }
        else { t.position = JeremySeat + new Vector3(-0.2f, 1.45f, 0f); missing.Add("Jeremy's avatar (shoulder for Helena's hand)"); }
        return t;
    }

    // ================================================================== the CCTV signal loading screen (prefab)
    internal static SignalLoader EnsureSignalLoader()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SignalLoader>(LoaderPrefabPath);
        if (existing != null) return existing;
        EditorAssets.EnsureFolder("Assets", "Prefabs");
        EditorAssets.EnsureFolder("Assets/Prefabs", "UI");
        var root = new GameObject("SignalLoader", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        Image black = root.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        RawImage noise = EditorUi.CreateUiObject(root.transform, "Static").AddComponent<RawImage>();
        EditorUi.Stretch(noise.rectTransform);
        var staticTex = AssetDatabase.LoadAssetAtPath<Texture2D>(StaticTexPath);
        if (staticTex != null)
        {
            if (AssetImporter.GetAtPath(StaticTexPath) is TextureImporter ti && ti.wrapMode != TextureWrapMode.Repeat)
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }
            noise.texture = staticTex;
        }
        else missing.Add(StaticTexPath);
        noise.color = new Color(0.55f, 0.58f, 0.6f, 0.85f);
        noise.raycastTarget = false;
        Image tint = EditorUi.CreateUiObject(root.transform, "Tint").AddComponent<Image>();
        EditorUi.Stretch(tint.rectTransform);
        tint.color = new Color(1f, 0.62f, 0.25f, 0f);
        tint.raycastTarget = false;

        Vector2 mid = new Vector2(0.5f, 0.5f);
        TMP_Text title = EditorUi.CreateText(root.transform, "Title", 30, TextAlignmentOptions.Center, new Color(0.92f, 0.92f, 0.9f));
        EditorUi.Place(title.rectTransform, mid, mid, mid, new Vector2(0f, 28f), new Vector2(1000f, 44f));
        title.characterSpacing = 6f;
        TMP_Text place = EditorUi.CreateText(root.transform, "Place", 18, TextAlignmentOptions.Center, new Color(0.75f, 0.78f, 0.78f));
        EditorUi.Place(place.rectTransform, mid, mid, mid, new Vector2(0f, -10f), new Vector2(1000f, 30f));
        place.characterSpacing = 4f;
        var barBack = EditorUi.CreateUiObject(root.transform, "Bar").AddComponent<Image>();
        EditorUi.Place(barBack.rectTransform, mid, mid, mid, new Vector2(0f, -48f), new Vector2(420f, 6f));
        barBack.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
        barBack.raycastTarget = false;
        var fill = EditorUi.CreateUiObject(barBack.transform, "Fill").AddComponent<Image>();
        EditorUi.Stretch(fill.rectTransform);
        fill.color = new Color(0.95f, 0.12f, 0.1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;
        Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f);
        TMP_Text timecode = EditorUi.CreateText(root.transform, "Timecode", 16, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f));
        EditorUi.Place(timecode.rectTransform, tl, tl, tl, new Vector2(28f, -22f), new Vector2(420f, 26f));
        TMP_Text rec = EditorUi.CreateText(root.transform, "Rec", 18, TextAlignmentOptions.TopRight, new Color(1f, 0.15f, 0.12f));
        EditorUi.Place(rec.rectTransform, tr, tr, tr, new Vector2(-28f, -20f), new Vector2(200f, 28f));
        rec.text = "● REC";

        var audio = root.AddComponent<AudioSource>();
        audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/A_Signal_Static.wav");
        audio.loop = true;
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.5f;
        audio.ignoreListenerPause = true;

        var loader = root.AddComponent<SignalLoader>();
        var so = new SerializedObject(loader);
        so.FindProperty(SignalLoader.GroupField).objectReferenceValue = group;
        so.FindProperty(SignalLoader.StaticImageField).objectReferenceValue = noise;
        so.FindProperty(SignalLoader.TintField).objectReferenceValue = tint;
        so.FindProperty(SignalLoader.BarFillField).objectReferenceValue = fill;
        so.FindProperty(SignalLoader.TitleField).objectReferenceValue = title;
        so.FindProperty(SignalLoader.PlaceField).objectReferenceValue = place;
        so.FindProperty(SignalLoader.TimecodeField).objectReferenceValue = timecode;
        so.FindProperty(SignalLoader.RecField).objectReferenceValue = rec;
        so.FindProperty(SignalLoader.AudioField).objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LoaderPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<SignalLoader>();
    }
}
