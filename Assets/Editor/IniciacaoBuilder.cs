using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cutscene 01 - Iniciação (CUTSCENE_01_Iniciacao.md): a scene of its own where the story cutscenes are made and
// tested (cinema mode: no cameras of the facility, a plain camera moved by the shots). Five sets far apart:
//   1 the front of his house (15h)        at    0
//   2 inside Mary's car, driving          at  500 (the world slides past the windows)
//   3 the governor's office               at 1000 (the sketch: window behind the desk, side door, info board)
//   4 the government building, night      at 1500 (the van waiting)
//   5 the back of the van                 at 2000 (the partition, its little window, the pig)
// The whole script as one Cutscene (Cutscene_01_Iniciacao, starts with the scene): edit its steps in the inspector,
// move the Shot_ / Move_ / Spot_ objects of each set to adjust the camera and where people stand.
// Menu 14 rebuilds everything (the scene is replaced: tune the steps after you are happy with the build, or keep a copy).
internal static class IniciacaoBuilder
{
    private const string ArtFolder = "Assets/Art/Props/Iniciacao";
    private const string MatFolder = ArtFolder + "/Materials";
    private const string OpFolder = "Assets/Art/Props/Operator";
    private const string DoorFolder = "Assets/Art/Props/Doors";
    private const string CastFolder = "Assets/Characters/Cast";
    private const string CastMatFolder = CastFolder + "/Materials";
    private const string CastAnimFolder = CastFolder + "/Animations";
    private const string CastModelFolder = CastFolder + "/Models";
    private const string ArmsCutsceneControllerPath = DataFolder + "/CutsceneArms.controller";
    private const string HelenaAnimFolder = "Assets/Characters/Helena/Animations";
    private const string AudioFolder = "Assets/Audio/Iniciacao";
    private const string DataFolder = "Assets/Data/Iniciacao";
    private const string MeshAssetPath = DataFolder + "/IniciacaoMeshes.asset";
    private const string ControllerPath = CastFolder + "/Cast.controller";
    private const string ArmsModelPath = "Assets/Art/FirstPerson/SM_FPArms.fbx";
    private const string ArmsControllerPath = "Assets/Art/FirstPerson/FPArms.controller";
    private const string SceneFolder = "Assets/Scenes";
    private const string ScenePath = SceneFolder + "/Cutscene01_Iniciacao.unity";
    private const float ArmsEye = 1.7432f;

    private static readonly Vector3 HouseOrigin = Vector3.zero;
    private static readonly Vector3 CarOrigin = new Vector3(500f, 0f, 0f);
    private static readonly Vector3 OfficeOrigin = new Vector3(1000f, 0f, 0f);
    private static readonly Vector3 NightOrigin = new Vector3(1500f, 0f, 0f);
    private static readonly Vector3 VanOrigin = new Vector3(2000f, 0f, 0f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    // controller states
    private const string Locomotion = "Locomotion";
    private static readonly (string state, string clip, bool mirror)[] CastStates =
    {
        ("TalkStand", "Cast_TalkStand", false), ("HoldDoor", "Cast_HoldDoor", false), ("HoldDoorL", "Cast_HoldDoor", true),
        ("ArmsOpen", "Cast_ArmsOpen", false), ("SeatedTalk", "Cast_SeatedTalk", false), ("SeatedIdle", "Cast_SeatedIdle", false),
        ("SeatedLean", "Cast_SeatedLean", false), ("Driving", "Cast_Driving", false), ("DrivingGlance", "Cast_DrivingGlance", false),
        ("DrivingLookAway", "Cast_DrivingLookAway", false), ("PigWait", "Cast_PigWait", false), ("PigLunge", "Cast_PigLunge", false),
        ("PigTurn", "Cast_PigTurn", false),
    };

    private static Shader lit;
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    private static bool meshAssetCreated;
    private static readonly List<string> missing = new List<string>();

    // ================================================================== the menu
    [MenuItem("Last Terminal/14 - Cutscene 01: Build Scene (Iniciação)")]
    public static void Build()
    {
        lit = HelenaSetupMenu.DefaultLitShader();
        if (lit == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        mats.Clear();
        meshes.Clear();
        missing.Clear();
        meshAssetCreated = false;

        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Iniciacao");
        HelenaSetupMenu.EnsureFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(ArtFolder) || !AssetDatabase.IsValidFolder(CastFolder))
        {
            Debug.LogError("[Iniciação] Assets/Art/Props/Iniciacao or Assets/Characters/Cast missing: extract the package at the project root first.");
            return;
        }
        PrepareArt();
        PrepareCast();
        AnimatorController ctrl = BuildController();
        AssetDatabase.DeleteAsset(MeshAssetPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.55f);
        RenderSettings.skybox = null;

        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        sun.color = new Color(1f, 0.93f, 0.82f);
        sun.intensity = 1.1f;

        var settingsGo = new GameObject("Settings");
        var settings = settingsGo.AddComponent<CameraSettings>();

        // the camera (moved by the shots)
        var camGo = new GameObject("CinemaCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.67f, 0.72f, 0.78f);
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 600f;
        cam.fieldOfView = 55f;
        camGo.AddComponent<AudioListener>();

        var house = BuildHouse();
        var car = BuildCar();
        var office = BuildOffice();
        var night = BuildNight();
        var van = BuildVan(camGo.transform);

        // the cast
        CutsceneActor mary = Actor("Mary", "MARY", new Color(0.95f, 0.55f, 0.62f), MaryController(ctrl), house.Find("Spot_MaryDoor"));
        CutsceneActor michael = Actor("Michael", "MICHAEL", new Color(0.55f, 0.78f, 1f), ctrl, office.Find("Spot_MichaelStart"));
        CutsceneActor pig = Actor("PigDriver", "???", new Color(0.8f, 0.4f, 0.4f), ctrl, van.Find("Spot_Pig"));
        CutsceneActor arms = ArmsActor(van);

        // the screen, the operator's voice / typing, the director
        CutsceneDirector director = BuildScreenAndDirector(cam, settings, out GameObject ui);
        camGo.transform.SetPositionAndRotation(house.Find("Shot_Door").position, house.Find("Shot_Door").rotation);

        Cutscene cutscene = BuildCutscene(director, sun, house, car, office, night, van, mary, michael, pig, arms);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = cutscene.gameObject;
        if (missing.Count > 0)
            Debug.LogWarning("[Iniciação] Built with placeholders, missing: " + string.Join(", ", missing.Distinct()));
        Debug.Log($"[Iniciação] {ScenePath} built. Play: the whole cutscene runs (Esc skips). Steps: select Cutscene_01_Iniciacao.", cutscene);
    }

    // ================================================================== assets
    // every model of the folder: its M_x materials get T_x.png (alpha -> cutout, double sided)
    private static void PrepareArt()
    {
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Materials");
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
                if (!n.StartsWith("M_Ini_")) continue;
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), Mat(n.Substring(6)));
                changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
    }

    // M_Ini_<name> from T_Ini_<name>.png (cutout when the texture has transparency)
    private static Material Mat(string name, float smooth = 0.1f)
    {
        if (mats.TryGetValue(name, out Material m)) return m;
        string texPath = $"{ArtFolder}/T_Ini_{name}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        bool clip = false;
        if (tex == null) missing.Add(texPath);
        else if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && ti.DoesSourceTextureHaveAlpha()) clip = true;
        m = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_Ini_{name}.mat", tex, smooth, clip, lit);
        // vehicle shells: hollow models, seen from inside too (no "infinite" through the far side)
        if (name == "Nissan" || name == "Van") { m.SetFloat("_Cull", (float)CullMode.Off); EditorUtility.SetDirty(m); }
        if (tex == null) m.SetColor(BaseColorId, new Color(0.5f, 0.5f, 0.5f));
        mats[name] = m;
        return m;
    }

    private static Material Flat(string name, Color c, float smooth = 0.15f)
    {
        if (mats.TryGetValue("flat_" + name, out Material m)) return m;
        m = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_IniFlat_{name}.mat", null, smooth, false, lit);
        m.SetColor(BaseColorId, c);
        EditorUtility.SetDirty(m);
        mats["flat_" + name] = m;
        return m;
    }

    private static Material Glow(string name, Color c, float strength)
    {
        if (mats.TryGetValue("glow_" + name, out Material m)) return m;
        m = Flat("Glow" + name, c);
        m.EnableKeyword("_EMISSION");
        m.SetColor(EmissionColorId, c * strength);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        mats["glow_" + name] = m;
        return m;
    }

    private static Material Glass(string name, Color c)
    {
        if (mats.TryGetValue("glass_" + name, out Material m)) return m;
        string path = $"{MatFolder}/M_IniGlass_{name}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.shader = lit;
        m.SetColor(BaseColorId, c);
        m.SetFloat(SmoothnessId, 0.85f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        mats["glass_" + name] = m;
        return m;
    }

    // the cast: their materials, and their clips on Helena's avatar
    private static void PrepareCast()
    {
        HelenaSetupMenu.EnsureFolder(CastFolder, "Materials");
        foreach (string name in new[] { "Mary", "Michael", "PigDriver" })
        {
            // the first package had them re-exported from Blender (broken skinning): those go
            if (AssetDatabase.LoadMainAssetAtPath($"{CastFolder}/{name}.fbx") != null)
            {
                AssetDatabase.DeleteAsset($"{CastFolder}/{name}.fbx");
                Debug.Log($"[Iniciação] Old {CastFolder}/{name}.fbx removed (the Mixamo file in Models/ replaces it).");
            }
            string path = $"{CastModelFolder}/{name}.fbx";
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { missing.Add(path); continue; }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CastFolder}/T_{name}.png");
            Material m = HelenaSetupMenu.MakeMaterial($"{CastMatFolder}/M_{name}.mat", tex, 0.12f, false, lit);
            // whatever the file calls its material: ours
            var names = new HashSet<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material) names.Add(o.name);
            foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            foreach (string n in names) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
            mi.SaveAndReimport();
        }
        Avatar helena = AssetDatabase.LoadAllAssetsAtPath($"{HelenaAnimFolder}/HelenaIdle.fbx").OfType<Avatar>().FirstOrDefault();
        if (helena == null)
        {
            missing.Add($"{HelenaAnimFolder}/HelenaIdle.fbx (Helena's avatar: the cast clips use their own)");
            return;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CastAnimFolder }))
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (mi == null || mi.sourceAvatar == helena) continue;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = helena;
            mi.SaveAndReimport();
        }
    }

    private static AnimationClip FileClip(string path)
    {
        AnimationClip c = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        if (c == null) missing.Add(path);
        return c;
    }

    // Locomotion (idle / walk by Speed, the glide of CutsceneActor) + one state per cast clip + the clip slots
    private static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimationClip idle = FileClip($"{HelenaAnimFolder}/HelenaIdle.fbx");
        AnimationClip walk = FileClip($"{HelenaAnimFolder}/HelenaWalking.fbx");
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState loco = ctrl.CreateBlendTreeInController(Locomotion, out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 1.4f);
        sm.defaultState = loco;
        int k = 0;
        foreach (var s in CastStates)
        {
            AnimatorState st = sm.AddState(s.state, new Vector3(300f + 230f * (k % 4), 120f + 70f * (k / 4), 0f));
            st.motion = FileClip($"{CastAnimFolder}/{s.clip}.fbx");
            st.mirror = s.mirror;
            k++;
        }
        sm.AddState("SitDown", new Vector3(300f, 400f, 0f)).motion = FileClip($"{HelenaAnimFolder}/HelenaStandToSit.fbx");
        sm.AddState("StandUp", new Vector3(530f, 400f, 0f)).motion = FileClip($"{HelenaAnimFolder}/HelenaSitToStand.fbx");
        CutsceneBuilder.AddSlotStates(ctrl, new Vector3(300f, 480f, 0f));
        // hands on the wheel / the door (CutsceneActor.SetHandTargets)
        AnimatorControllerLayer[] layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // Mary stands with her own idle (the Mixamo one that came with her) instead of Helena's
    private static RuntimeAnimatorController MaryController(AnimatorController ctrl)
    {
        AnimationClip helenaIdle = AssetDatabase.LoadAllAssetsAtPath($"{HelenaAnimFolder}/HelenaIdle.fbx").OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        AnimationClip maryIdle = AssetDatabase.LoadAllAssetsAtPath(CastImport.MaryFile).OfType<AnimationClip>().FirstOrDefault(x => x.name == CastImport.MaryIdleClip);
        if (helenaIdle == null || maryIdle == null) { missing.Add("Mary_Idle (in Mary.fbx)"); return ctrl; }
        string path = $"{CastFolder}/Mary.overrideController";
        AssetDatabase.DeleteAsset(path);
        var ov = new AnimatorOverrideController(ctrl) { name = "Mary" };
        ov[helenaIdle] = maryIdle;
        AssetDatabase.CreateAsset(ov, path);
        return ov;
    }

    // ================================================================== geometry helpers
    private static Transform Root(string name, Vector3 origin)
    {
        var t = new GameObject(name).transform;
        t.position = origin;
        return t;
    }

    // a box with UVs in meters (density = texture repeats per meter): textures tile instead of stretching
    private static Mesh BoxMesh(Vector3 size, float density)
    {
        string key = $"{size.x:0.###}x{size.y:0.###}x{size.z:0.###}@{density:0.###}";
        if (meshes.TryGetValue(key, out Mesh mesh)) return mesh;
        Vector3 h = size * 0.5f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 nn in normals)
        {
            Vector3 up = Mathf.Abs(nn.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, -nn);
            float hu = Mathf.Abs(Vector3.Dot(h, right)), hv = Mathf.Abs(Vector3.Dot(h, up)), hn = Mathf.Abs(Vector3.Dot(h, nn));
            Vector3 c = nn * hn;
            int b = v.Count;
            v.Add(c - right * hu - up * hv); v.Add(c - right * hu + up * hv); v.Add(c + right * hu + up * hv); v.Add(c + right * hu - up * hv);
            for (int i = 0; i < 4; i++) n.Add(nn);
            float U = 2f * hu * density, V = 2f * hv * density;
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, V)); uv.Add(new Vector2(U, V)); uv.Add(new Vector2(U, 0f));
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        mesh = new Mesh { name = "Box_" + key };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        if (!meshAssetCreated) { AssetDatabase.CreateAsset(mesh, MeshAssetPath); meshAssetCreated = true; }
        else AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
        meshes[key] = mesh;
        return mesh;
    }

    private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, float density = 1f, float yaw = 0f, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, density);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // a box between two heights (bottom at y0), centered on x / z
    private static GameObject Slab(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, Material m, float density = 1f)
        => Box(parent, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), m, density);

    private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    private static GameObject Model(Transform parent, string path, Vector3 pos, float yaw, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { missing.Add(path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static GameObject Ini(Transform parent, string model, Vector3 pos, float yaw) => Model(parent, $"{ArtFolder}/SM_Ini_{model}.fbx", pos, yaw);

    // an empty object at pos looking at lookAt (local coordinates of the set)
    private static Transform Mark(Transform parent, string name, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        Vector3 d = lookAt - pos;
        t.localRotation = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d, Vector3.up) : Quaternion.identity;
        return t;
    }

    // a standing spot: facing a direction on the ground
    private static Transform Spot(Transform parent, string name, Vector3 pos, Vector3 facing)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        facing.y = 0f;
        t.localRotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-6f ? facing : Vector3.forward, Vector3.up);
        return t;
    }

    private static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    private static AudioSource Loop(Transform parent, string name, string clip, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<AudioSource>();
        a.clip = Clip(clip);
        a.loop = true;
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        a.volume = volume;
        return a;
    }

    private static AudioClip Clip(string name)
    {
        string path = $"{AudioFolder}/{name}.wav";
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) missing.Add(path);
        return c;
    }

    // a wooden door (frame + 'Leaf' on its hinge) with its open / closed poses; a plain one if the model is missing
    private static Transform Door(Transform parent, string name, Vector3 pos, float yaw, float openAngle, out Transform open, out Transform closed)
    {
        GameObject go = Model(parent, $"{DoorFolder}/SM_DoorWood.fbx", pos, yaw);
        Transform leaf = null;
        if (go != null) { go.name = name; leaf = HelenaSetupMenu.FindDeep(go.transform, "Leaf"); }
        if (leaf == null)
        {
            if (go == null) { go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); }
            var pivot = new GameObject("Leaf").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.45f, 0f, 0f);
            Box(pivot, "Panel", new Vector3(0.45f, 1.05f, 0f), new Vector3(0.9f, 2.1f, 0.05f), Mat("Wood"));
            leaf = pivot;
        }
        closed = new GameObject(name + "_Closed").transform;
        closed.SetParent(go.transform, false);
        closed.SetPositionAndRotation(leaf.position, leaf.rotation);
        open = new GameObject(name + "_Open").transform;
        open.SetParent(go.transform, false);
        open.SetPositionAndRotation(leaf.position, Quaternion.AngleAxis(openAngle, Vector3.up) * leaf.rotation);
        return leaf;
    }

    // 3D text, readable from -Z of its rotation (auto sized to the box)
    private static TextMeshPro Sign(Transform parent, string name, string text, Vector3 pos, float yaw, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = 60f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    private static void Tree(Transform parent, string name, Vector3 pos, float h)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        Prim(PrimitiveType.Cylinder, t, "Trunk", new Vector3(0f, h * 0.3f, 0f), new Vector3(0.25f, h * 0.3f, 0.25f), Flat("Bark", new Color(0.24f, 0.18f, 0.13f)));
        Prim(PrimitiveType.Sphere, t, "Leaves", new Vector3(0f, h * 0.72f, 0f), new Vector3(h * 0.55f, h * 0.5f, h * 0.55f), Flat("Leaves", new Color(0.2f, 0.3f, 0.16f)));
    }

    // ================================================================== 1 - the front of his house
    private static Transform BuildHouse()
    {
        Transform root = Root("Set1_HouseFront", HouseOrigin);
        Ini(root, "Street", Vector3.zero, 0f);
        // the ground all around the street piece (no edge of the world)
        Box(root, "GroundAround", new Vector3(0f, -0.42f, 0f), new Vector3(500f, 0.1f, 500f), Mat("Grass"), 0.25f, 0f, false);

        // Mary's car at the curb, its rear right door open toward the path
        GameObject car = Ini(root, "Nissan", new Vector3(0.6f, -0.31f, 6.25f), 90f);
        if (car != null)
        {
            car.name = "Car_Mary";
            // inside of the shell (the model is hollow): floor, seats, dash, roof lining
            Material inside = Flat("CarInside", new Color(0.09f, 0.09f, 0.1f));
            Box(car.transform, "InFloor", new Vector3(0f, 0.32f, 0f), new Vector3(1.8f, 0.06f, 3.6f), inside);
            Box(car.transform, "InRearSeat", new Vector3(0f, 0.5f, -0.75f), new Vector3(1.6f, 0.28f, 0.6f), inside);
            Box(car.transform, "InRearBack", new Vector3(0f, 0.85f, -1.12f), new Vector3(1.6f, 0.6f, 0.16f), inside);
            Box(car.transform, "InFrontSeats", new Vector3(0f, 0.55f, 0.45f), new Vector3(1.5f, 0.3f, 0.55f), inside);
            Box(car.transform, "InFrontBacks", new Vector3(0f, 0.9f, 0.12f), new Vector3(1.5f, 0.6f, 0.14f), inside);
            Box(car.transform, "InDash", new Vector3(0f, 0.85f, 1.15f), new Vector3(1.7f, 0.3f, 0.5f), inside);
            Box(car.transform, "InRoof", new Vector3(0f, 1.36f, -0.2f), new Vector3(1.7f, 0.04f, 2.4f), inside);
            Box(car.transform, "InFront", new Vector3(0f, 0.7f, 1.45f), new Vector3(1.8f, 0.7f, 0.06f), inside);
            Box(car.transform, "InBack", new Vector3(0f, 0.8f, -1.7f), new Vector3(1.8f, 0.6f, 0.06f), inside);
            Transform door = HelenaSetupMenu.FindDeep(car.transform, "RearDoorR");
            if (door != null)
            {
                var closed = new GameObject("RearDoorR_Closed").transform;
                closed.SetParent(car.transform, false);
                closed.SetPositionAndRotation(door.position, door.rotation);
                var open = new GameObject("RearDoorR_Open").transform;
                open.SetParent(car.transform, false);
                open.SetPositionAndRotation(door.position, Quaternion.AngleAxis(-65f, Vector3.up) * door.rotation);
                door.SetPositionAndRotation(open.position, open.rotation);   // waiting open
                // where Mary's hand holds it: the top of its free end
                var mf = door.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    float z = Mathf.Abs(b.min.z) > Mathf.Abs(b.max.z) ? b.min.z + 0.08f : b.max.z - 0.08f;
                    var grip = new GameObject("DoorGrip").transform;
                    grip.SetParent(door, false);
                    grip.position = mf.transform.TransformPoint(new Vector3(b.center.x, b.max.y - 0.33f, z));
                }
            }
            else missing.Add("SM_Ini_Nissan RearDoorR");
        }

        // where Mary waits (left hand on the open door), the shots of the walk from the porch
        Spot(root, "Spot_MaryDoor", new Vector3(-0.6f, -0.31f, 4.7f), new Vector3(0.3f, 0f, -1f));
        Mark(root, "Shot_Door", new Vector3(0.05f, 2.42f, -9.55f), new Vector3(0.4f, 0.9f, 6f));   // just out of his front door
        Mark(root, "Move_Porch", new Vector3(0.05f, 2.42f, -8.3f), new Vector3(0.3f, 0.9f, 6f));
        Mark(root, "Move_Path", new Vector3(0.05f, 1.86f, -2.2f), new Vector3(-0.4f, 1.3f, 4.75f));
        // his eyes ~1.68 m over the ground (sidewalk -0.21, street -0.31)
        Mark(root, "Shot_Mary", new Vector3(-0.15f, 1.47f, 3.2f), new Vector3(-0.6f, 1.25f, 4.7f));
        Mark(root, "Shot_Glance", new Vector3(-0.15f, 1.47f, 3.2f), new Vector3(-2.2f, 0.1f, 3.2f));
        Mark(root, "Shot_DoorOpen", new Vector3(0.25f, 1.4f, 3.6f), new Vector3(0.05f, 0.75f, 5.6f));
        Mark(root, "Move_Enter", new Vector3(0.05f, 0.95f, 5.0f), new Vector3(0.05f, 0.85f, 7.5f));
        return root;
    }

    // ================================================================== 2 - inside the car (it doesn't move: the world does)
    private static Transform BuildCar()
    {
        Transform root = Root("Set2_CarInside", CarOrigin);
        Material seat = Flat("CarSeat", new Color(0.22f, 0.2f, 0.19f), 0.05f), dark = Flat("CarTrim", new Color(0.12f, 0.12f, 0.13f)), glass = Glass("Car", new Color(0.55f, 0.65f, 0.7f, 0.18f));
        Slab(root, "Floor", -0.8f, 0.8f, -0.05f, 0f, -1.7f, 1.3f, dark);
        foreach (float x in new[] { -0.38f, 0.38f })
        {
            string side = x < 0f ? "L" : "R";
            Box(root, "FrontSeat" + side, new Vector3(x, 0.24f, 0.2f), new Vector3(0.5f, 0.12f, 0.5f), seat, 2f);
            var back = Box(root, "FrontBack" + side, new Vector3(x, 0.62f, -0.08f), new Vector3(0.5f, 0.68f, 0.12f), seat, 2f);
            back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Box(root, "Headrest" + side, new Vector3(x, 1.03f, -0.16f), new Vector3(0.26f, 0.16f, 0.1f), seat, 2f);
        }
        Box(root, "RearSeat", new Vector3(0f, 0.26f, -0.9f), new Vector3(1.4f, 0.14f, 0.5f), seat, 2f);
        var rb = Box(root, "RearBack", new Vector3(0f, 0.66f, -1.18f), new Vector3(1.4f, 0.7f, 0.12f), seat, 2f);
        rb.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        Box(root, "Dashboard", new Vector3(0f, 0.72f, 1.08f), new Vector3(1.5f, 0.3f, 0.5f), dark);
        Prim(PrimitiveType.Cylinder, root, "SteeringColumn", new Vector3(-0.38f, 0.76f, 0.82f), new Vector3(0.05f, 0.18f, 0.05f), dark, new Vector3(65f, 0f, 0f));
        Transform carWheel = SteeringWheel(root, new Vector3(-0.38f, 0.83f, 0.66f), dark);
        carWheel.Find("GripL").name = "CarGripL";
        carWheel.Find("GripR").name = "CarGripR";
        Slab(root, "Roof", -0.82f, 0.82f, 1.25f, 1.3f, -1.6f, 1.2f, Flat("Headliner", new Color(0.55f, 0.53f, 0.5f)));
        foreach (float x in new[] { -0.8f, 0.8f })
        {
            string side = x < 0f ? "L" : "R";
            Slab(root, "DoorPanel" + side, x - 0.03f, x + 0.03f, 0f, 0.74f, -1.6f, 1.2f, dark);
            foreach (float z in new[] { 1.15f, -0.35f, -1.5f })
                Slab(root, $"Pillar{side}{z:0.0}", x - 0.03f, x + 0.03f, 0.74f, 1.25f, z - 0.05f, z + 0.05f, dark);
            Slab(root, "Window" + side, x - 0.005f, x + 0.005f, 0.74f, 1.25f, -1.5f, 1.15f, glass);
        }
        var ws = Box(root, "Windshield", new Vector3(0f, 1.0f, 1.32f), new Vector3(1.56f, 0.6f, 0.01f), glass);
        ws.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        var rw = Box(root, "RearWindow", new Vector3(0f, 1.0f, -1.62f), new Vector3(1.56f, 0.5f, 0.01f), glass);
        rw.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        Box(root, "Mirror", new Vector3(0.05f, 1.15f, 1.05f), new Vector3(0.22f, 0.06f, 0.03f), dark);

        // the land all around (still: it looks the same going by), the road going by
        Box(root, "GroundAround", new Vector3(0f, -0.38f, 0f), new Vector3(600f, 0.1f, 600f), Mat("Grass"), 0.25f, 0f, false);
        Transform scenery = new GameObject("Scenery").transform;
        scenery.SetParent(root, false);
        var scroll = scenery.gameObject.AddComponent<ScrollingScenery>();
        var so = new SerializedObject(scroll);
        so.FindProperty("speed").floatValue = 13f;
        so.FindProperty("length").floatValue = 140f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Random.InitState(7);
        for (int i = 0; i < 14; i++)
        {
            float z = -70f + i * 10f + 5f;
            Box(scenery, $"Road{i}", new Vector3(0f, -0.35f, z), new Vector3(8f, 0.1f, 10f), Mat("Road"), 0.25f);
            Box(scenery, $"GrassL{i}", new Vector3(-12f, -0.36f, z), new Vector3(16f, 0.1f, 10f), Mat("Grass"), 0.3f);
            Box(scenery, $"GrassR{i}", new Vector3(12f, -0.36f, z), new Vector3(16f, 0.1f, 10f), Mat("Grass"), 0.3f);
        }
        for (int i = 0; i < 26; i++)
        {
            float z = -70f + i * 5.4f + Random.Range(-1f, 1f);
            float side = i % 2 == 0 ? -1f : 1f;
            Tree(scenery, $"Tree{i}", new Vector3(side * Random.Range(6f, 11f), -0.3f, z), Random.Range(4.5f, 7f));
        }
        for (int i = 0; i < 6; i++)
        {
            float z = -70f + i * 23f;
            Prim(PrimitiveType.Cylinder, scenery, $"Pole{i}", new Vector3(4.6f, 3.2f, z), new Vector3(0.18f, 3.5f, 0.18f), Mat("Metal"));
        }
        for (int i = 0; i < 5; i++)
            Box(scenery, $"House{i}", new Vector3(i % 2 == 0 ? -17f : 18f, 2.5f, -70f + i * 29f), new Vector3(8f, 5.6f, 9f), Mat("Brick"), 0.5f);

        Loop(root, "Engine", "EngineLoop", 0.5f);
        Spot(root, "Spot_MaryDriver", new Vector3(-0.38f, 0f, 0.18f), Vector3.forward);
        Mark(root, "Shot_CarPOV", new Vector3(0.36f, 1.03f, -0.86f), new Vector3(-0.25f, 0.95f, 1.4f));
        Mark(root, "Shot_CarLook", new Vector3(0.34f, 1.03f, -0.84f), new Vector3(-0.38f, 0.98f, 0.2f));   // he looks at her
        return root;
    }

    // a ring of short bars (a wheel you can see through), tilted toward the driver
    // returns the wheel; its children GripL / GripR are where the hands go (10 and 2 o'clock)
    private static Transform SteeringWheel(Transform parent, Vector3 pos, Material m)
    {
        var wheel = new GameObject("SteeringWheel").transform;
        wheel.SetParent(parent, false);
        wheel.localPosition = pos;
        wheel.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        const int rimSegments = 12;
        const float r = 0.18f;
        for (int i = 0; i < rimSegments; i++)
        {
            float a = i * Mathf.PI * 2f / rimSegments;
            var bar = Box(wheel, "Rim", new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), new Vector3(0.025f, 2f * r * Mathf.Sin(Mathf.PI / rimSegments) + 0.01f, 0.025f), m);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
        }
        Box(wheel, "Hub", Vector3.zero, new Vector3(0.07f, 0.07f, 0.04f), m);
        Box(wheel, "Spoke", new Vector3(0f, -r * 0.5f, 0f), new Vector3(0.025f, r, 0.02f), m);
        foreach (var (gripName, gripAngle) in new[] { ("GripL", 150f), ("GripR", 30f) })
        {
            var g = new GameObject(gripName).transform;
            g.SetParent(wheel, false);
            g.localPosition = new Vector3(Mathf.Cos(gripAngle * Mathf.Deg2Rad) * r, Mathf.Sin(gripAngle * Mathf.Deg2Rad) * r, -0.03f);
        }
        return wheel;
    }

    // ================================================================== 3 - the governor's office (the sketch)
    private static Transform BuildOffice()
    {
        Transform root = Root("Set3_Office", OfficeOrigin);
        Material plaster = Mat("Plaster"), wood = Mat("Wood"), woodLight = Mat("WoodLight"), floor = Mat("Floor");
        const float W = 3.5f, D = 4f, H = 3.2f, T = 0.2f;

        Slab(root, "Floor", -W, W, -0.1f, 0f, -D, D, floor, 0.7f);
        Slab(root, "Ceiling", -W, W, H, H + 0.1f, -D, D, plaster, 0.5f);
        // front wall (z = -4): the main door at x = -0.8
        Slab(root, "WallFrontL", -W, -1.3f, 0f, H, -D - T, -D, plaster);
        Slab(root, "WallFrontR", -0.3f, W, 0f, H, -D - T, -D, plaster);
        Slab(root, "WallFrontTop", -1.3f, -0.3f, 2.15f, H, -D - T, -D, plaster);
        // back wall (z = +4): the big window behind the desk
        Slab(root, "WallBackL", -W, -1.6f, 0f, H, D, D + T, plaster);
        Slab(root, "WallBackR", 1.6f, W, 0f, H, D, D + T, plaster);
        Slab(root, "WallBackLow", -1.6f, 1.6f, 0f, 0.9f, D, D + T, plaster);
        Slab(root, "WallBackTop", -1.6f, 1.6f, 2.6f, H, D, D + T, plaster);
        // left wall (x = -3.5), right wall (x = +3.5) with the governor's door at z = 2.2
        Slab(root, "WallLeft", -W - T, -W, 0f, H, -D, D, plaster);
        Slab(root, "WallRightA", W, W + T, 0f, H, -D, 1.7f, plaster);
        Slab(root, "WallRightB", W, W + T, 0f, H, 2.7f, D, plaster);
        Slab(root, "WallRightTop", W, W + T, 2.15f, H, 1.7f, 2.7f, plaster);
        // wood wainscot all around (1 m), and a cornice
        Slab(root, "WainscotLeft", -W, -W + 0.03f, 0f, 1.0f, -D, D, woodLight, 1.2f);
        Slab(root, "WainscotRightA", W - 0.03f, W, 0f, 1.0f, -D, 1.7f, woodLight, 1.2f);
        Slab(root, "WainscotRightB", W - 0.03f, W, 0f, 1.0f, 2.7f, D, woodLight, 1.2f);
        Slab(root, "WainscotFrontL", -W, -1.3f, 0f, 1.0f, -D, -D + 0.03f, woodLight, 1.2f);
        Slab(root, "WainscotFrontR", -0.3f, W, 0f, 1.0f, -D, -D + 0.03f, woodLight, 1.2f);
        Slab(root, "WainscotBackL", -W, -1.6f, 0f, 1.0f, D - 0.03f, D, woodLight, 1.2f);
        Slab(root, "WainscotBackR", 1.6f, W, 0f, 1.0f, D - 0.03f, D, woodLight, 1.2f);
        Slab(root, "WainscotBackLow", -1.6f, 1.6f, 0f, 0.9f, D - 0.03f, D, woodLight, 1.2f);
        Slab(root, "CorniceL", -W, -W + 0.08f, H - 0.12f, H, -D, D, wood);
        Slab(root, "CorniceR", W - 0.08f, W, H - 0.12f, H, -D, D, wood);
        Slab(root, "CorniceF", -W, W, H - 0.12f, H, -D, -D + 0.08f, wood);
        Slab(root, "CorniceB", -W, W, H - 0.12f, H, D - 0.08f, D, wood);

        // the window: frame, mullions, glass, curtains, the city outside
        Slab(root, "WindowSill", -1.7f, 1.7f, 0.86f, 0.92f, D - 0.12f, D + T, wood);
        Slab(root, "WindowHead", -1.7f, 1.7f, 2.58f, 2.66f, D - 0.05f, D + T, wood);
        Slab(root, "WindowJambL", -1.68f, -1.58f, 0.9f, 2.6f, D - 0.05f, D + T, wood);
        Slab(root, "WindowJambR", 1.58f, 1.68f, 0.9f, 2.6f, D - 0.05f, D + T, wood);
        Slab(root, "WindowMullion", -0.04f, 0.04f, 0.9f, 2.6f, D + 0.06f, D + 0.12f, wood);
        Slab(root, "WindowTransom", -1.6f, 1.6f, 1.98f, 2.04f, D + 0.06f, D + 0.12f, wood);
        Slab(root, "WindowGlass", -1.6f, 1.6f, 0.9f, 2.6f, D + 0.09f, D + 0.1f, Glass("Window", new Color(0.8f, 0.88f, 0.95f, 0.12f)));
        Material curtain = Flat("Curtain", new Color(0.42f, 0.07f, 0.08f));
        Slab(root, "CurtainL", -2.15f, -1.6f, 0.1f, 2.95f, D - 0.18f, D - 0.1f, curtain);
        Slab(root, "CurtainR", 1.6f, 2.15f, 0.1f, 2.95f, D - 0.18f, D - 0.1f, curtain);
        Slab(root, "CurtainRod", -2.3f, 2.3f, 2.97f, 3.0f, D - 0.16f, D - 0.12f, Mat("Metal"));
        Box(root, "Sky", new Vector3(0f, 3f, 14f), new Vector3(40f, 16f, 0.1f), Glow("Sky", new Color(0.78f, 0.84f, 0.9f), 1.2f), 1f, 0f, false);
        Box(root, "CityGround", new Vector3(0f, -1.05f, 9f), new Vector3(40f, 0.1f, 10f), Mat("Concrete"), 0.3f, 0f, false);
        Random.InitState(3);
        Material city = Flat("City", new Color(0.42f, 0.46f, 0.52f));
        for (int i = 0; i < 12; i++)
        {
            float x = -10f + i * 1.9f + Random.Range(-0.3f, 0.3f);
            float h = Random.Range(2f, 9f);
            Box(root, $"City{i}", new Vector3(x, -1f + h * 0.5f, 11f + Random.Range(-1f, 1f)), new Vector3(1.6f, h, 1.5f), city, 1f, 0f, false);
        }

        // the doors
        Transform mainLeaf = Door(root, "MainDoor", new Vector3(-0.8f, 0f, -D), 0f, -95f, out Transform mainOpen, out Transform mainClosed);
        Transform sideLeaf = Door(root, "SideDoor", new Vector3(W, 0f, 2.2f), -90f, -95f, out Transform sideOpen, out Transform sideClosed);
        mainLeaf.name = "MainDoorLeaf"; mainOpen.name = "MainDoor_Open"; mainClosed.name = "MainDoor_Closed";
        sideLeaf.name = "SideDoorLeaf"; sideOpen.name = "SideDoor_Open"; sideClosed.name = "SideDoor_Closed";
        // the corridor outside the main door, the little passage behind the side door
        Material stone = Mat("Stone");
        Slab(root, "CorridorFloor", -2.2f, 0.6f, -0.1f, 0f, -7.2f, -D - T, stone, 0.7f);
        Slab(root, "CorridorCeiling", -2.2f, 0.6f, 2.8f, 2.9f, -7.2f, -D - T, plaster);
        Slab(root, "CorridorWallL", -2.4f, -2.2f, 0f, 2.9f, -7.2f, -D - T, plaster);
        Slab(root, "CorridorWallR", 0.6f, 0.8f, 0f, 2.9f, -7.2f, -D - T, plaster);
        Slab(root, "CorridorEnd", -2.4f, 0.8f, 0f, 2.9f, -7.4f, -7.2f, plaster);
        PointLight(root, "CorridorLight", new Vector3(-0.8f, 2.6f, -5.8f), new Color(1f, 0.88f, 0.7f), 1.2f, 6f);
        Slab(root, "PassageFloor", W + T, W + 2.2f, -0.1f, 0f, 1.2f, 3.2f, stone, 0.7f);
        Slab(root, "PassageCeiling", W + T, W + 2.2f, 2.8f, 2.9f, 1.2f, 3.2f, plaster);
        Slab(root, "PassageWallA", W + T, W + 2.2f, 0f, 2.9f, 1.0f, 1.2f, plaster);
        Slab(root, "PassageWallB", W + T, W + 2.2f, 0f, 2.9f, 3.2f, 3.4f, plaster);
        Slab(root, "PassageEnd", W + 2.2f, W + 2.4f, 0f, 2.9f, 1.0f, 3.4f, plaster);

        // the desk (a crescent around his chair), his chair, things on it, the flags
        Ini(root, "GovDesk", new Vector3(0f, 0f, 2.7f), 180f);
        Ini(root, "OfficeChair", new Vector3(0f, 0f, 2.95f), 90f);   // the model faces +X: turned to face the visitor
        Model(root, $"{OpFolder}/SM_OpPapers.fbx", new Vector3(0.5f, 0.76f, 1.75f), 15f);
        Model(root, $"{OpFolder}/SM_OpStapler.fbx", new Vector3(-0.55f, 0.76f, 1.8f), -30f);
        Prim(PrimitiveType.Cylinder, root, "LampBase", new Vector3(-0.85f, 0.78f, 2.05f), new Vector3(0.14f, 0.02f, 0.14f), Mat("Metal"));
        Prim(PrimitiveType.Cylinder, root, "LampStem", new Vector3(-0.85f, 0.98f, 2.05f), new Vector3(0.025f, 0.2f, 0.025f), Mat("Metal"));
        Prim(PrimitiveType.Cylinder, root, "LampShade", new Vector3(-0.85f, 1.2f, 2.05f), new Vector3(0.24f, 0.08f, 0.24f), Glow("Lamp", new Color(0.25f, 0.4f, 0.25f), 0.6f));
        PointLight(root, "DeskLamp", new Vector3(-0.85f, 1.1f, 2.0f), new Color(1f, 0.85f, 0.6f), 0.7f, 3f);
        Box(root, "NamePlate", new Vector3(0f, 0.8f, 1.35f), new Vector3(0.42f, 0.08f, 0.06f), wood);
        Sign(root, "NamePlateText", "GOV. MICHAEL", new Vector3(0f, 0.8f, 1.315f), 0f, new Vector2(0.4f, 0.07f), new Color(0.85f, 0.72f, 0.35f));
        foreach (var (x, c, nm) in new[] { (-1.55f, new Color(0.1f, 0.18f, 0.42f), "FlagState"), (1.55f, new Color(0.08f, 0.35f, 0.18f), "FlagCountry") })
        {
            Prim(PrimitiveType.Cylinder, root, nm + "Pole", new Vector3(x, 1.15f, 3.55f), new Vector3(0.04f, 1.15f, 0.04f), Mat("Metal"));
            Prim(PrimitiveType.Cylinder, root, nm + "Base", new Vector3(x, 0.03f, 3.55f), new Vector3(0.32f, 0.03f, 0.32f), Mat("MetalDark"));
            Box(root, nm, new Vector3(x + (x < 0f ? 0.03f : -0.03f), 1.75f, 3.45f), new Vector3(0.02f, 0.62f, 0.32f), Flat(nm, c));
            Prim(PrimitiveType.Sphere, root, nm + "Tip", new Vector3(x, 2.33f, 3.55f), Vector3.one * 0.08f, Glow("Gold", new Color(0.8f, 0.62f, 0.2f), 0.2f));
        }
        Box(root, "Rug", new Vector3(0f, 0.006f, 0.55f), new Vector3(2.4f, 0.012f, 3.6f), Mat("Rug"), 1f / 3.6f);
        // his visitor's chair (where the protagonist sits) facing the desk
        Model(root, $"{OpFolder}/SM_OpChair.fbx", new Vector3(-0.2f, 0f, 0.42f), 90f);   // the model faces -X: turned to the desk

        // the info board (left side, on wheels, turned to the room): cork board + what Michael shows
        Transform board = new GameObject("InfoBoard").transform;
        board.SetParent(root, false);
        board.localPosition = new Vector3(-2.55f, 0f, 0.35f);
        board.localRotation = Quaternion.Euler(0f, 72f, 0f);
        Material metal = Mat("MetalDark");
        foreach (float x in new[] { -0.76f, 0.76f })
        {
            Box(board, "Leg", new Vector3(x, 1.0f, -0.03f), new Vector3(0.05f, 1.9f, 0.05f), metal);
            Box(board, "Foot", new Vector3(x, 0.09f, -0.03f), new Vector3(0.06f, 0.05f, 0.6f), metal);
            foreach (float z in new[] { -0.3f, 0.24f })
                Prim(PrimitiveType.Sphere, board, "Caster", new Vector3(x, 0.04f, z), Vector3.one * 0.08f, Flat("Rubber", new Color(0.05f, 0.05f, 0.05f)));
        }
        Box(board, "Bar", new Vector3(0f, 0.55f, -0.03f), new Vector3(1.5f, 0.04f, 0.04f), metal);
        GameObject cork = Ini(board, "CorkBoard", new Vector3(0f, 0.95f, 0f), 0f);
        Material evA = Mat("EvAsylum"), evM = Mat("EvMap"), evD = Mat("EvDoc"), evP = Mat("EvPlan"), evN = Mat("EvNote");
        Box(board, "EvAsylum", new Vector3(-0.42f, 1.55f, 0.032f), new Vector3(0.28f, 0.3f, 0.004f), evA, 1f / 0.3f);
        Box(board, "EvMap", new Vector3(0.0f, 1.42f, 0.032f), new Vector3(0.36f, 0.36f, 0.004f), evM, 1f / 0.36f);
        Box(board, "EvDoc", new Vector3(0.43f, 1.45f, 0.032f), new Vector3(0.26f, 0.34f, 0.004f), evD, 1f / 0.34f);
        Box(board, "EvPlan", new Vector3(-0.36f, 1.16f, 0.032f), new Vector3(0.36f, 0.27f, 0.004f), evP, 1f / 0.36f);
        Box(board, "EvNote", new Vector3(0.36f, 1.12f, 0.032f), new Vector3(0.2f, 0.13f, 0.004f), evN, 1f / 0.2f);
        Material red = Flat("String", new Color(0.7f, 0.05f, 0.05f));
        Vector3[] pins = { new Vector3(-0.42f, 1.68f, 0.04f), new Vector3(0f, 1.42f, 0.04f), new Vector3(0.43f, 1.6f, 0.04f), new Vector3(-0.36f, 1.27f, 0.04f) };
        foreach (Vector3 p in pins) Prim(PrimitiveType.Sphere, board, "Pin", p, Vector3.one * 0.022f, red);
        for (int i = 1; i < pins.Length; i++)
        {
            Vector3 a = pins[0], b = pins[i];
            if (i == 2) a = pins[1];
            var s = Box(board, "String", (a + b) * 0.5f, new Vector3(Vector3.Distance(a, b), 0.004f, 0.002f), red, 1f, 0f, false);
            s.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }
        if (cork == null) Box(board, "CorkPlaceholder", new Vector3(0f, 1.38f, 0f), new Vector3(1.38f, 0.87f, 0.04f), Mat("Chalk"));

        // decoration: plants in the corners, books, a cabinet, the calendar, a painting
        foreach (var p in new[] { new Vector3(-3.05f, 0f, 3.55f), new Vector3(3.05f, 0f, 3.55f), new Vector3(-3.05f, 0f, -3.55f), new Vector3(3.05f, 0f, -3.55f) })
            Model(root, $"{OpFolder}/SM_OpPlant.fbx", p, 0f);
        Box(root, "Bookshelf", new Vector3(-3.27f, 1.2f, 2.45f), new Vector3(0.42f, 2.4f, 1.6f), Mat("Books"), 0.8f);
        Box(root, "BookshelfTop", new Vector3(-3.25f, 2.42f, 2.45f), new Vector3(0.46f, 0.05f, 1.64f), wood);
        Model(root, $"{OpFolder}/SM_OpFilingCabinet.fbx", new Vector3(3.15f, 0f, -1.2f), -90f);
        Ini(root, "Calendar", new Vector3(W - 0.01f, 1.45f, -2.4f), -90f);
        Box(root, "PaintingFrame", new Vector3(-W + 0.04f, 1.75f, -1.6f), new Vector3(0.05f, 0.9f, 1.3f), wood);
        Box(root, "Painting", new Vector3(-W + 0.07f, 1.75f, -1.6f), new Vector3(0.01f, 0.78f, 1.18f), Mat("EvMap"), 0.85f);

        // lights
        Material panel = Glow("CeilingPanel", new Color(1f, 0.95f, 0.85f), 1.4f);
        foreach (var p in new[] { new Vector3(0f, H - 0.02f, -1.6f), new Vector3(0f, H - 0.02f, 1.8f) })
        {
            Box(root, "CeilingPanel", p, new Vector3(1.2f, 0.03f, 0.6f), panel, 1f, 0f, false);
            PointLight(root, "CeilingLight", p - Vector3.up * 0.25f, new Color(1f, 0.92f, 0.8f), 1.3f, 7f);
        }

        // people and shots
        Spot(root, "Spot_MaryCorridor", new Vector3(-1.6f, 0f, -4.75f), new Vector3(1f, 0f, -0.35f));
        Spot(root, "Spot_MaryGone", new Vector3(-0.8f, 0f, -6.9f), Vector3.back);
        Spot(root, "Spot_MichaelStart", new Vector3(W + 1.3f, 0f, 2.2f), Vector3.left);
        Spot(root, "Spot_MichaelIn", new Vector3(2.85f, 0f, 2.15f), new Vector3(-1f, 0f, -0.4f));
        Spot(root, "Spot_MichaelGreet", new Vector3(1.35f, 0f, 1.0f), new Vector3(-1.5f, 0f, -0.55f));
        Spot(root, "Spot_MichaelAround1", new Vector3(1.85f, 0f, 2.35f), Vector3.forward);
        Spot(root, "Spot_MichaelAround2", new Vector3(0.95f, 0f, 3.3f), Vector3.left);
        Spot(root, "Spot_MichaelChair", new Vector3(0f, 0f, 2.88f), Vector3.back);
        Vector3 seated = new Vector3(-0.2f, 1.17f, 0.5f), face = new Vector3(0f, 1.18f, 2.8f);
        Mark(root, "Shot_Corridor", new Vector3(-0.8f, 1.65f, -6.5f), new Vector3(-0.8f, 1.35f, -4f));
        Mark(root, "Shot_MaryTalk", new Vector3(-0.65f, 1.6f, -5.7f), new Vector3(-1.6f, 1.5f, -4.75f));
        Mark(root, "Move_Doorway", new Vector3(-0.75f, 1.65f, -3.55f), new Vector3(0f, 1.15f, 2.5f));
        Mark(root, "Move_ToChair", new Vector3(-0.3f, 1.65f, -0.15f), new Vector3(0f, 1.1f, 2.6f));
        Mark(root, "Move_Sit", seated, face);
        Mark(root, "Shot_SideDoor", seated, new Vector3(3.3f, 1.3f, 2.2f));
        Mark(root, "Shot_Greet", seated, new Vector3(1.35f, 1.45f, 1.0f));
        // all from his eyes, sitting: focusing on Michael (narrower), leaning in, turning to the board
        Mark(root, "Shot_SeatFocus", seated, face);
        Mark(root, "Shot_SeatLean", new Vector3(-0.17f, 1.13f, 0.8f), face);
        Mark(root, "Shot_SeatBoard", seated, new Vector3(-2.5f, 1.38f, 0.35f));
        return root;
    }

    // ================================================================== 4 - the government building at night
    private static Transform BuildNight()
    {
        Transform root = Root("Set4_GovBuilding", NightOrigin);
        Material stone = Mat("Stone"), wall = Mat("Plaster"), road = Mat("Road");
        Slab(root, "Sidewalk", -22f, 22f, -0.1f, 0f, -2f, 6f, Mat("Sidewalk"), 0.5f);
        Slab(root, "Road", -22f, 22f, -0.3f, -0.2f, -14f, -2f, road, 0.25f);
        Slab(root, "Curb", -22f, 22f, -0.2f, 0f, -2.1f, -1.9f, Mat("Concrete"));
        Slab(root, "FarSidewalk", -22f, 22f, -0.1f, 0f, -18f, -14f, Mat("Sidewalk"), 0.5f);
        // the ground of the city, the road going on, buildings across the street and further away
        Box(root, "GroundAround", new Vector3(0f, -0.35f, 0f), new Vector3(400f, 0.1f, 400f), Mat("Concrete"), 0.2f, 0f, false);
        Slab(root, "RoadOn", -200f, 200f, -0.3f, -0.21f, -14f, -2f, road, 0.25f);
        Slab(root, "SidewalkOn", -200f, 200f, -0.1f, -0.01f, -2f, 6f, Mat("Sidewalk"), 0.5f);
        Slab(root, "FarSidewalkOn", -200f, 200f, -0.1f, -0.01f, -18f, -14f, Mat("Sidewalk"), 0.5f);
        Slab(root, "FacadeFarL", -60f, -16f, 0f, 7f, 6f, 9f, Mat("Brick"), 0.5f);
        Slab(root, "FacadeFarR", 16f, 60f, 0f, 8f, 6f, 9f, Mat("Brick"), 0.5f);
        Material winLit = Glow("WindowLitFar", new Color(1f, 0.8f, 0.48f), 1.2f), winDark = Flat("WindowDarkFar", new Color(0.04f, 0.05f, 0.07f), 0.7f);
        Random.InitState(11);
        for (int i = 0; i < 14; i++)
        {
            float x = -45f + i * 7f + Random.Range(-1f, 1f), h = Random.Range(6f, 16f), w = Random.Range(5f, 6.5f);
            Box(root, $"Across{i}", new Vector3(x, h * 0.5f, -22f - Random.Range(0f, 2f)), new Vector3(w, h, 6f), Mat(i % 3 == 0 ? "Brick" : "Plaster"), 0.4f);
            for (int f = 0; f < (int)(h / 3f); f++)
                for (int k = -1; k <= 1; k++)
                    Box(root, $"AcrossWin{i}_{f}_{k}", new Vector3(x + k * w * 0.3f, 1.8f + f * 3f, -18.95f - 0.02f), new Vector3(0.9f, 1.2f, 0.05f), Random.value < 0.25f ? winLit : winDark, 1f, 0f, false);
        }
        Material far = Flat("FarCity", new Color(0.06f, 0.07f, 0.1f));
        for (int i = 0; i < 24; i++)
            Box(root, $"Skyline{i}", new Vector3(-120f + i * 10f, 12f, -70f - Random.Range(0f, 30f)), new Vector3(9f, Random.Range(14f, 40f), 9f), far, 1f, 0f, false);
        foreach (float x in new[] { -18f, -9f, 0f, 9f, 18f })
        {
            Prim(PrimitiveType.Cylinder, root, "LampPostFar", new Vector3(x, 2.1f, -14.6f), new Vector3(0.12f, 2.1f, 0.12f), Mat("MetalDark"));
            Box(root, "LampHeadFar", new Vector3(x, 4.3f, -14.6f), new Vector3(0.35f, 0.25f, 0.35f), Glow("StreetLamp", new Color(1f, 0.78f, 0.45f), 3f), 1f, 0f, false);
            PointLight(root, "LampLightFar", new Vector3(x, 4.0f, -14.6f), new Color(1f, 0.78f, 0.5f), 2.5f, 12f);
        }
        // the facade, its entrance
        Slab(root, "FacadeL", -16f, -1.4f, 0f, 10f, 6f, 7f, wall, 0.5f);
        Slab(root, "FacadeR", 1.4f, 16f, 0f, 10f, 6f, 7f, wall, 0.5f);
        Slab(root, "FacadeTop", -1.4f, 1.4f, 3.2f, 10f, 6f, 7f, wall, 0.5f);
        Slab(root, "Base", -16f, 16f, 0f, 0.9f, 5.9f, 6.05f, stone, 1f);
        Slab(root, "Cornice", -16.2f, 16.2f, 9.6f, 10.1f, 5.7f, 7f, stone);
        Slab(root, "Lobby", -1.4f, 1.4f, 0.45f, 3.2f, 7f, 9f, Glow("Lobby", new Color(1f, 0.85f, 0.6f), 0.9f));
        Transform dl = Door(root, "EntranceL", new Vector3(-0.45f, 0.45f, 6.5f), 0f, 0f, out _, out _);
        Transform dr = Door(root, "EntranceR", new Vector3(0.45f, 0.45f, 6.5f), 0f, 0f, out _, out _);
        // windows: some lit
        Material lit1 = Glow("WindowLit", new Color(1f, 0.82f, 0.5f), 1.1f), dark = Flat("WindowDark", new Color(0.05f, 0.06f, 0.08f), 0.7f);
        Random.InitState(5);
        for (int row = 0; row < 2; row++)
            for (int i = -5; i <= 5; i++)
            {
                if (i == 0 && row == 0) continue;
                float x = i * 2.8f, y = row == 0 ? 2.2f : 6.0f;
                Box(root, $"Window{row}_{i}", new Vector3(x, y, 5.97f), new Vector3(1.2f, 1.7f, 0.05f), Random.value < 0.3f ? lit1 : dark, 1f, 0f, false);
                Box(root, $"WindowFrame{row}_{i}", new Vector3(x, y - 0.92f, 5.92f), new Vector3(1.4f, 0.12f, 0.16f), stone);
            }
        // steps, portico, columns
        Slab(root, "Landing", -4.5f, 4.5f, 0f, 0.45f, 4.4f, 6f, stone, 1f);
        Slab(root, "Step2", -4.5f, 4.5f, 0f, 0.3f, 4.0f, 4.4f, stone, 1f);
        Slab(root, "Step1", -4.5f, 4.5f, 0f, 0.15f, 3.6f, 4.0f, stone, 1f);
        Slab(root, "Portico", -4.6f, 4.6f, 4.9f, 5.4f, 4.0f, 6f, stone, 1f);
        foreach (float x in new[] { -3.6f, -1.9f, 1.9f, 3.6f })
            Prim(PrimitiveType.Cylinder, root, "Column", new Vector3(x, 2.675f, 4.65f), new Vector3(0.5f, 2.225f, 0.5f), stone);
        Sign(root, "Sign", "PALÁCIO DO GOVERNO", new Vector3(0f, 5.15f, 3.97f), 0f, new Vector2(8f, 0.42f), new Color(0.85f, 0.7f, 0.35f));
        // street lights
        Material lamp = Glow("StreetLamp", new Color(1f, 0.78f, 0.45f), 3f);
        foreach (float x in new[] { -13f, -5.5f, 5.5f, 13f })
        {
            Prim(PrimitiveType.Cylinder, root, "LampPost", new Vector3(x, 2.1f, -1.4f), new Vector3(0.12f, 2.1f, 0.12f), Mat("MetalDark"));
            Box(root, "LampHead", new Vector3(x, 4.3f, -1.4f), new Vector3(0.35f, 0.25f, 0.35f), lamp, 1f, 0f, false);
            PointLight(root, "LampLight", new Vector3(x, 4.0f, -1.4f), new Color(1f, 0.78f, 0.5f), 3f, 13f);
        }
        PointLight(root, "EntranceLight", new Vector3(0f, 4.4f, 4.9f), new Color(1f, 0.85f, 0.6f), 2f, 8f);
        // the van waiting
        GameObject v = Ini(root, "Van", new Vector3(2.4f, -0.2f, -3.1f), -90f);
        if (v != null) v.name = "Van";
        Spot(root, "Spot_MaryNight", new Vector3(-0.85f, 0.45f, 5.0f), new Vector3(0.6f, 0f, -1f));
        Mark(root, "Shot_NightExit", new Vector3(0f, 2.1f, 6.7f), new Vector3(0.2f, 1.6f, -3f));   // out through the doors
        Mark(root, "Shot_NightMary", new Vector3(0.35f, 2.08f, 4.35f), new Vector3(-0.85f, 1.95f, 5.0f));
        Mark(root, "Shot_NightVan", new Vector3(0.3f, 2.08f, 4.3f), new Vector3(2.4f, 0.9f, -3.1f));
        Mark(root, "Move_NightWalk", new Vector3(1.6f, 1.55f, -1.0f), new Vector3(2.4f, 0.9f, -3.1f));
        return root;
    }

    // ================================================================== 5 - the back of the van
    private static Transform BuildVan(Transform camera)
    {
        Transform root = Root("Set5_VanInside", VanOrigin);
        Material metal = Mat("MetalDark"), metal2 = Mat("Metal"), seat = Mat("Seat");
        // cargo box (floor 0, roof 1.35, partition at z = 0)
        Slab(root, "Floor", -0.8f, 0.8f, -0.05f, 0f, -2.7f, 0f, metal2, 2f);
        Slab(root, "Roof", -0.8f, 0.8f, 1.35f, 1.4f, -2.7f, 1.7f, metal, 1f);
        Slab(root, "SideL", -0.84f, -0.78f, 0f, 1.35f, -2.7f, 1.7f, metal, 1.5f);
        Slab(root, "SideR", 0.78f, 0.84f, 0f, 1.35f, -2.7f, 1.7f, metal, 1.5f);
        Slab(root, "BackDoors", -0.8f, 0.8f, 0f, 1.35f, -2.76f, -2.7f, metal, 1.5f);
        for (int i = 0; i < 5; i++)
            Slab(root, "Rib", -0.78f, -0.74f, 0.05f, 1.33f, -2.4f + i * 0.5f, -2.36f + i * 0.5f, metal2);
        // his bench, facing the partition
        Box(root, "Bench", new Vector3(0.1f, 0.4f, -1.25f), new Vector3(1.3f, 0.1f, 0.45f), seat, 2f);
        Box(root, "BenchBack", new Vector3(0.1f, 0.75f, -1.5f), new Vector3(1.3f, 0.6f, 0.08f), seat, 2f);
        Box(root, "BenchLegs", new Vector3(0.1f, 0.18f, -1.25f), new Vector3(1.2f, 0.35f, 0.35f), metal);
        // the partition and its little window (the glass slides to the right)
        // the little window right behind the driver's head (x = -0.38)
        Slab(root, "PartitionL", -0.8f, -0.63f, 0f, 1.35f, -0.03f, 0.03f, metal, 1.5f);
        Slab(root, "PartitionR", -0.13f, 0.8f, 0f, 1.35f, -0.03f, 0.03f, metal, 1.5f);
        Slab(root, "PartitionLow", -0.63f, -0.13f, 0f, 0.85f, -0.03f, 0.03f, metal, 1.5f);
        Slab(root, "PartitionTop", -0.63f, -0.13f, 1.2f, 1.35f, -0.03f, 0.03f, metal, 1.5f);
        GameObject glass = Box(root, "PartitionGlass", new Vector3(-0.38f, 1.025f, -0.05f), new Vector3(0.52f, 0.37f, 0.012f), Flat("TintedGlass", new Color(0.02f, 0.025f, 0.03f), 0.9f));
        var glassOpen = new GameObject("PartitionGlass_Open").transform;
        glassOpen.SetParent(root, false);
        glassOpen.localPosition = new Vector3(0.12f, 1.025f, -0.05f);
        // the cab behind it, the windshield, the night road going by
        Slab(root, "CabFloor", -0.8f, 0.8f, -0.15f, -0.1f, 0f, 1.7f, metal);
        Box(root, "CabSeatL", new Vector3(-0.4f, 0.25f, 0.55f), new Vector3(0.5f, 0.12f, 0.5f), seat, 2f);
        Box(root, "CabSeatR", new Vector3(0.4f, 0.25f, 0.55f), new Vector3(0.5f, 0.12f, 0.5f), seat, 2f);
        Box(root, "CabDash", new Vector3(0f, 0.65f, 1.45f), new Vector3(1.55f, 0.3f, 0.4f), metal);
        Box(root, "CabSeatBackL", new Vector3(-0.4f, 0.6f, 0.25f), new Vector3(0.5f, 0.6f, 0.1f), seat, 2f);
        Transform vanWheel = SteeringWheel(root, new Vector3(-0.38f, 0.73f, 1.03f), metal);
        vanWheel.Find("GripL").name = "VanGripL";
        vanWheel.Find("GripR").name = "VanGripR";
        var ws = Box(root, "Windshield", new Vector3(0f, 1.0f, 1.66f), new Vector3(1.56f, 0.6f, 0.01f), Glass("Van", new Color(0.3f, 0.35f, 0.4f, 0.25f)));
        ws.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        Box(root, "GroundAround", new Vector3(0f, -0.6f, 0f), new Vector3(400f, 0.1f, 400f), Mat("Grass"), 0.2f, 0f, false);
        Transform road = new GameObject("NightRoad").transform;
        road.SetParent(root, false);
        var scroll = road.gameObject.AddComponent<ScrollingScenery>();
        var so = new SerializedObject(scroll);
        so.FindProperty("speed").floatValue = 12f;
        so.FindProperty("length").floatValue = 120f;
        so.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < 12; i++)
            Box(road, $"Road{i}", new Vector3(0f, -0.5f, -60f + i * 10f + 5f), new Vector3(8f, 0.1f, 10f), Mat("Road"), 0.25f);
        Material lamp = Glow("StreetLampVan", new Color(1f, 0.75f, 0.42f), 3f);
        for (int i = 0; i < 4; i++)
        {
            float z = -60f + i * 30f;
            Box(road, $"Lamp{i}", new Vector3(3.2f, 4.2f, z), new Vector3(0.3f, 0.2f, 0.3f), lamp, 1f, 0f, false);
            PointLight(road, $"LampLight{i}", new Vector3(2.6f, 3.6f, z), new Color(1f, 0.75f, 0.45f), 5f, 14f);
        }
        PointLight(root, "CargoBulb", new Vector3(-0.1f, 1.28f, -1.7f), new Color(1f, 0.55f, 0.35f), 0.7f, 3.2f);
        Loop(root, "Engine", "EngineLoop", 0.55f);

        // where the pig waits (crouched at the little window, facing back), the shots
        // the pig drives (his back to the little window), the shots
        Spot(root, "Spot_Pig", new Vector3(-0.38f, -0.1f, 0.55f), Vector3.forward);
        Mark(root, "Shot_VanPOV", new Vector3(0.1f, 1.1f, -1.25f), new Vector3(-0.38f, 1.0f, 0f));
        Mark(root, "Move_Lean", new Vector3(-0.33f, 1.05f, -0.5f), new Vector3(-0.38f, 1.03f, 0.2f));
        return root;
    }

    // ================================================================== the cast and the arms
    private static CutsceneActor Actor(string model, string display, Color color, RuntimeAnimatorController ctrl, Transform spot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CastModelFolder}/{model}.fbx");
        GameObject go;
        if (prefab != null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        else
        {
            missing.Add($"{CastModelFolder}/{model}.fbx");
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        go.name = model;
        if (spot != null) go.transform.SetPositionAndRotation(spot.position, spot.rotation);
        Animator animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var actor = go.AddComponent<CutsceneActor>();
        CutsceneBuilder.EnsureSlotClips(out AnimationClip a, out AnimationClip b);
        HelenaSetupMenu.SetString(actor, CutsceneActor.DisplayNameField, display);
        HelenaSetupMenu.SetReference(actor, CutsceneActor.AnimatorField, animator);
        HelenaSetupMenu.SetReference(actor, CutsceneActor.SlotAField, a);
        HelenaSetupMenu.SetReference(actor, CutsceneActor.SlotBField, b);
        var so = new SerializedObject(actor);
        so.FindProperty("nameColor").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
        return actor;
    }

    // the operator's first person arms under the camera (off until the van: they knock on the glass)
    private static CutsceneActor ArmsActor(Transform van)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == "CinemaCamera") cam = r.transform;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModelPath);
        RuntimeAnimatorController ctrl = ArmsController();
        if (prefab == null || ctrl == null || cam == null)
        {
            missing.Add(ArmsModelPath + " (relax / push.R)");
            return null;
        }
        var arms = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cam);
        arms.name = "Arms";
        arms.transform.localPosition = new Vector3(0f, -ArmsEye, 0f);
        arms.transform.localRotation = Quaternion.identity;
        Animator animator = arms.GetComponent<Animator>();
        if (animator == null) animator = arms.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Renderer rend in arms.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;
        var actor = arms.AddComponent<CutsceneActor>();
        HelenaSetupMenu.SetString(actor, CutsceneActor.DisplayNameField, "");
        HelenaSetupMenu.SetReference(actor, CutsceneActor.AnimatorField, animator);
        HelenaSetupMenu.SetString(actor, "speedParameter", "");
        arms.SetActive(false);
        return actor;
    }

    // relax (default) and push.R (the knock, back to relax by itself), from the takes of the arms file
    private static RuntimeAnimatorController ArmsController()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ArmsModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip relax = clips.FirstOrDefault(c => c.name.EndsWith(FirstPersonPlayer.RelaxClip));
        AnimationClip push = clips.FirstOrDefault(c => c.name.EndsWith(FirstPersonPlayer.PushClip));
        if (relax == null || push == null) return null;
        AssetDatabase.DeleteAsset(ArmsCutsceneControllerPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ArmsCutsceneControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = sm.AddState(FirstPersonPlayer.RelaxState, new Vector3(300f, 100f, 0f));
        idle.motion = relax;
        sm.defaultState = idle;
        AnimatorState knock = sm.AddState(FirstPersonPlayer.PushState, new Vector3(550f, 100f, 0f));
        knock.motion = push;
        knock.speed = 1.6f;
        AnimatorStateTransition back = knock.AddTransition(idle);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.12f;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ================================================================== the screen + director
    private static CutsceneDirector BuildScreenAndDirector(Camera cam, CameraSettings settings, out GameObject canvasGo)
    {
        canvasGo = new GameObject("UI", typeof(RectTransform));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform c = canvasGo.transform;
        Vector2 bc = new Vector2(0.5f, 0f);

        var root = HelenaSetupMenu.CreateUiObject(c, "Cutscene");
        HelenaSetupMenu.Stretch((RectTransform)root.transform);

        // scare: tint + static (under the bars and texts)
        var scareGo = HelenaSetupMenu.CreateUiObject(root.transform, "Scare");
        HelenaSetupMenu.Stretch((RectTransform)scareGo.transform);
        var scare = scareGo.AddComponent<CanvasGroup>();
        scare.blocksRaycasts = false;
        Image tint = HelenaSetupMenu.CreateUiObject(scareGo.transform, "Tint").AddComponent<Image>();
        HelenaSetupMenu.Stretch(tint.rectTransform);
        tint.raycastTarget = false;
        RawImage noise = HelenaSetupMenu.CreateUiObject(scareGo.transform, "Noise").AddComponent<RawImage>();
        HelenaSetupMenu.Stretch(noise.rectTransform);
        noise.raycastTarget = false;
        noise.color = new Color(1f, 1f, 1f, 0.55f);

        var barsGo = HelenaSetupMenu.CreateUiObject(root.transform, "Bars");
        HelenaSetupMenu.Stretch((RectTransform)barsGo.transform);
        var bars = barsGo.AddComponent<CanvasGroup>();
        bars.blocksRaycasts = false;
        foreach (var (n, min, max, pivot) in new[] { ("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)), ("Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f)) })
        {
            Image img = HelenaSetupMenu.CreateUiObject(barsGo.transform, n).AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            HelenaSetupMenu.Place(img.rectTransform, min, max, pivot, Vector2.zero, new Vector2(0f, 70f));
        }
        TMP_Text hint = HelenaSetupMenu.CreateText(barsGo.transform, "Skip", 18, TextAlignmentOptions.Right, new Color(0.7f, 0.7f, 0.7f));
        HelenaSetupMenu.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(300f, 30f));

        var subGo = HelenaSetupMenu.CreateUiObject(root.transform, "Subtitle");
        HelenaSetupMenu.Place((RectTransform)subGo.transform, bc, bc, bc, new Vector2(0f, 96f), new Vector2(1100f, 90f));
        var subGroup = subGo.AddComponent<CanvasGroup>();
        subGroup.blocksRaycasts = false;
        Image band = HelenaSetupMenu.CreateUiObject(subGo.transform, "Band").AddComponent<Image>();
        HelenaSetupMenu.Stretch(band.rectTransform);
        band.color = new Color(0f, 0f, 0f, 0.5f);
        band.raycastTarget = false;
        TMP_Text sub = HelenaSetupMenu.CreateText(subGo.transform, "Text", 28, TextAlignmentOptions.Center, Color.white);
        HelenaSetupMenu.Stretch(sub.rectTransform);
        sub.margin = new Vector4(20f, 6f, 20f, 6f);

        // the thoughts block (top-left)
        ThoughtView thoughts = ThoughtViewBuilder.Build(root.transform, settings);

        // "say: ..." (the operator's line)
        var promptGo = HelenaSetupMenu.CreateUiObject(root.transform, "Prompt");
        HelenaSetupMenu.Place((RectTransform)promptGo.transform, bc, bc, bc, new Vector2(0f, 290f), new Vector2(1000f, 110f));
        var promptGroup = promptGo.AddComponent<CanvasGroup>();
        promptGroup.blocksRaycasts = false;
        Image promptBack = HelenaSetupMenu.CreateUiObject(promptGo.transform, "Back").AddComponent<Image>();
        HelenaSetupMenu.Stretch(promptBack.rectTransform);
        promptBack.color = new Color(0f, 0.04f, 0.08f, 0.8f);
        promptBack.raycastTarget = false;
        TMP_Text prompt = HelenaSetupMenu.CreateText(promptGo.transform, "Text", 30, TextAlignmentOptions.Center, Color.white);
        HelenaSetupMenu.Stretch(prompt.rectTransform);
        prompt.margin = new Vector4(20f, 8f, 20f, 8f);

        Image black = HelenaSetupMenu.CreateUiObject(root.transform, "Fade").AddComponent<Image>();
        HelenaSetupMenu.Stretch(black.rectTransform);
        black.color = Color.black;
        black.raycastTarget = false;
        var fade = black.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;

        // over the black: "loading..."
        var loadGo = HelenaSetupMenu.CreateUiObject(root.transform, "Loading");
        HelenaSetupMenu.Place((RectTransform)loadGo.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(420f, 50f));
        var loadGroup = loadGo.AddComponent<CanvasGroup>();
        loadGroup.blocksRaycasts = false;
        TMP_Text loadText = HelenaSetupMenu.CreateText(loadGo.transform, "Text", 26, TextAlignmentOptions.Right, new Color(0.75f, 0.75f, 0.75f));
        HelenaSetupMenu.Stretch(loadText.rectTransform);

        // the operator's line typed (Enter) and the microphone (V)
        var consoleGo = HelenaSetupMenu.CreateUiObject(c, "Console");
        HelenaSetupMenu.Place((RectTransform)consoleGo.transform, bc, bc, bc, new Vector2(0f, 24f), new Vector2(900f, 44f));
        var panelGo = HelenaSetupMenu.CreateUiObject(consoleGo.transform, "Panel");
        HelenaSetupMenu.Stretch((RectTransform)panelGo.transform);
        var panel = panelGo.AddComponent<CanvasGroup>();
        panel.blocksRaycasts = false;
        Image pb = HelenaSetupMenu.CreateUiObject(panelGo.transform, "Back").AddComponent<Image>();
        HelenaSetupMenu.Stretch(pb.rectTransform);
        pb.color = new Color(0f, 0f, 0f, 0.75f);
        pb.raycastTarget = false;
        TMP_Text input = HelenaSetupMenu.CreateText(panelGo.transform, "Input", 24, TextAlignmentOptions.Left, new Color(0.85f, 0.95f, 0.85f));
        HelenaSetupMenu.Stretch(input.rectTransform);
        input.margin = new Vector4(16f, 4f, 16f, 4f);
        TMP_Text echo = HelenaSetupMenu.CreateText(consoleGo.transform, "Echo", 20, TextAlignmentOptions.Center, new Color(0.7f, 0.85f, 0.7f));
        HelenaSetupMenu.Place(echo.rectTransform, bc, bc, bc, new Vector2(0f, 50f), new Vector2(900f, 30f));
        var console = consoleGo.AddComponent<CommandConsole>();
        HelenaSetupMenu.SetReference(console, CommandConsole.SettingsField, settings);
        HelenaSetupMenu.SetReference(console, CommandConsole.PanelField, panel);
        HelenaSetupMenu.SetReference(console, CommandConsole.InputTextField, input);
        HelenaSetupMenu.SetReference(console, CommandConsole.EchoTextField, echo);
        VoiceBuilder.ConfigurePlugins();
        VoiceCommandInput voice = VoiceBuilder.Build(c, null, null, null, settings, console);

        var go = new GameObject("CutsceneDirector");
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        var d = go.AddComponent<CutsceneDirector>();
        HelenaSetupMenu.SetReference(d, CutsceneDirector.CinemaCameraField, cam);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SettingsField, settings);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.AudioField, audio);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.BarsField, bars);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.FadeField, fade);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SubtitleGroupField, subGroup);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SubtitleTextField, sub);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SkipHintField, hint);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.VoiceInputField, voice);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ConsoleField, console);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.PromptGroupField, promptGroup);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.PromptTextField, prompt);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ThoughtViewField, thoughts);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.LoadingGroupField, loadGroup);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.LoadingTextField, loadText);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ScareGroupField, scare);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ScareTintField, tint);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ScareNoiseField, noise);
        return d;
    }

    // ================================================================== the script
    private static SerializedProperty steps;

    private static void Add(CutsceneStep step)
    {
        steps.arraySize++;
        steps.GetArrayElementAtIndex(steps.arraySize - 1).managedReferenceValue = step;
    }

    private static Transform F(Transform set, string name)
    {
        Transform t = set.Find(name);
        if (t == null) t = HelenaSetupMenu.FindDeep(set, name);
        if (t == null) missing.Add($"{set.name}/{name}");
        return t;
    }

    private static SayStep Say(CutsceneActor who, string pt, string en) => new SayStep { actor = who, textPt = pt, textEn = en, pauseAfter = 0.35f };
    private static ThoughtStep Think(string pt, string en) => new ThoughtStep { textPt = pt, textEn = en, pauseAfter = 0.3f };
    private static OperatorLineStep You(string pt, string en) => new OperatorLineStep { textPt = pt, textEn = en };
    private static CameraShotStep Shot(Transform t, float fov) => new CameraShotStep { shot = t, label = "", fieldOfView = fov };
    private static CameraMoveStep Move(Transform t, float s, float fov = 0f, bool wait = true) => new CameraMoveStep { to = t, seconds = s, fieldOfView = fov, wait = wait };
    private static StateStep State(CutsceneActor a, string s, float blend = 0.35f, float wait = 0f) => new StateStep { actor = a, state = s, blend = blend, wait = wait };
    private static MoveStep Walk(CutsceneActor a, Transform to, bool wait = true, bool face = true) => new MoveStep { actor = a, target = to, speed = 1f, waitArrival = wait, faceTargetForward = face };
    private static TeleportStep Tele(CutsceneActor a, Transform to) => new TeleportStep { actor = a, target = to };
    private static WaitStep Wait(float s) => new WaitStep { seconds = s };
    private static FadeStep Fade(bool toBlack, float s) => new FadeStep { toBlack = toBlack, duration = s };
    private static SoundStep Sfx(string clip, bool wait = false, float volume = 1f) => new SoundStep { clip = Clip(clip), volume = volume, wait = wait };
    private static SoundLoopStep Engine(AudioSource s, bool on) => new SoundLoopStep { source = s, play = on, volume = 0.5f, fade = on ? 1.2f : 1.0f };
    private static TransformStep Xform(Transform target, Transform to, float s, bool wait = true) => new TransformStep { target = target, to = to, seconds = s, wait = wait };
    private static HandheldStep Hand(float amount) => new HandheldStep { amount = amount };
    private static ParallelStep Together(params CutsceneStep[] list) => new ParallelStep { steps = new List<CutsceneStep>(list) };

    private static LightingStep Day(Light sun) => new LightingStep
    {
        sun = sun, sunAngles = new Vector3(35f, -40f, 0f), sunColor = new Color(1f, 0.93f, 0.82f), sunIntensity = 1.1f,
        ambient = new Color(0.5f, 0.52f, 0.55f), sky = new Color(0.67f, 0.72f, 0.78f), fog = true, fogColor = new Color(0.67f, 0.72f, 0.78f), fogStart = 25f, fogEnd = 140f,
    };

    private static LightingStep OfficeLight(Light sun) => new LightingStep
    {
        sun = sun, sunAngles = new Vector3(28f, 195f, 0f), sunColor = new Color(1f, 0.88f, 0.7f), sunIntensity = 1.3f,
        ambient = new Color(0.36f, 0.34f, 0.32f), sky = new Color(0.78f, 0.84f, 0.9f), fog = false,
    };

    private static LightingStep NightLight(Light sun) => new LightingStep
    {
        sun = sun, sunAngles = new Vector3(45f, 30f, 0f), sunColor = new Color(0.55f, 0.65f, 0.9f), sunIntensity = 0.12f,
        ambient = new Color(0.09f, 0.1f, 0.15f), sky = new Color(0.05f, 0.07f, 0.13f), fog = true, fogColor = new Color(0.05f, 0.07f, 0.12f), fogStart = 18f, fogEnd = 110f,
    };

    private static LightingStep VanLight(Light sun) => new LightingStep
    {
        sun = sun, sunAngles = new Vector3(45f, 30f, 0f), sunColor = new Color(0.55f, 0.65f, 0.9f), sunIntensity = 0.05f,
        ambient = new Color(0.05f, 0.05f, 0.07f), sky = new Color(0.04f, 0.05f, 0.1f), fog = true, fogColor = new Color(0.04f, 0.05f, 0.09f), fogStart = 10f, fogEnd = 70f,
    };

    private static Cutscene BuildCutscene(CutsceneDirector d, Light sun, Transform house, Transform car, Transform office, Transform night, Transform van,
                                          CutsceneActor mary, CutsceneActor michael, CutsceneActor pig, CutsceneActor arms)
    {
        var go = new GameObject("Cutscene_01_Iniciacao");
        var cutscene = go.AddComponent<Cutscene>();
        HelenaSetupMenu.SetReference(cutscene, Cutscene.DirectorField, d);
        HelenaSetupMenu.SetString(cutscene, Cutscene.IdField, "iniciacao_01");
        var cso = new SerializedObject(cutscene);
        cso.FindProperty("once").boolValue = false;
        cso.FindProperty(Cutscene.ClearScreenAtEndField).boolValue = false;
        cso.ApplyModifiedPropertiesWithoutUndo();
        var trigger = go.AddComponent<CutsceneTrigger>();
        HelenaSetupMenu.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
        var tso = new SerializedObject(trigger);
        tso.FindProperty(CutsceneTrigger.WhenField).enumValueIndex = (int)CutsceneTrigger.When.SceneStart;
        tso.FindProperty("delay").floatValue = 0.2f;
        tso.ApplyModifiedPropertiesWithoutUndo();

        AudioSource carEngine = F(car, "Engine")?.GetComponent<AudioSource>();
        AudioSource vanEngine = F(van, "Engine")?.GetComponent<AudioSource>();
        Transform carDoor = F(house, "RearDoorR"), carDoorClosed = F(house, "RearDoorR_Closed");
        Transform mainLeaf = F(office, "MainDoorLeaf"), sideLeaf = F(office, "SideDoorLeaf");
        Transform glass = F(van, "PartitionGlass");

        var so = new SerializedObject(cutscene);
        steps = so.FindProperty(Cutscene.StepsField);

        // ---------------------------------------------------------- setup (everyone where they start)
        Add(Fade(true, 0f));
        Add(Day(sun));
        Add(Tele(mary, F(house, "Spot_MaryDoor")));
        Add(State(mary, Locomotion, 0f));
        Add(Tele(michael, F(office, "Spot_MichaelStart")));
        Add(Tele(pig, F(van, "Spot_Pig")));
        Add(State(pig, "Driving", 0f));
        Add(new HandTargetStep { actor = pig, left = F(van, "VanGripL"), right = F(van, "VanGripR"), blend = 0f });

        // ---------------------------------------------------------- CENA 1 - the front of the house (~15h)
        Add(Shot(F(house, "Shot_Door"), 60f));
        Add(Hand(1f));
        Add(Sfx("OfficeDoor", false, 0.7f));
        Add(Fade(false, 2f));
        Add(Move(F(house, "Move_Porch"), 2.2f));
        Add(Move(F(house, "Move_Path"), 3.4f));
        Add(Together(State(mary, "TalkStand"), Move(F(house, "Shot_Mary"), 1.6f)));
        Add(Say(mary, "Olá. Eu sou a Mary, secretária do governador Michael.", "Hello. I'm Mary, Governor Michael's secretary."));
        Add(Say(mary, "Ele gostaria de conversar com você. Pessoalmente.", "He'd like to speak with you. In person."));
        Add(State(mary, Locomotion, 0.5f));
        Add(Move(F(house, "Shot_Glance"), 1.3f));
        Add(Think("Mike...", "Mike..."));
        Add(Move(F(house, "Shot_Mary"), 0.9f));
        Add(State(mary, "HoldDoorL", 0.5f));
        Add(new HandTargetStep { actor = mary, left = F(house, "DoorGrip"), blend = 0.5f });
        Add(Wait(0.4f));
        Add(Say(mary, "Por favor.", "Please."));
        Add(Move(F(house, "Shot_DoorOpen"), 1.4f));
        Add(Hand(1.8f));
        Add(Wait(1.8f));   // he hesitates
        Add(Hand(1f));
        Add(Move(F(house, "Move_Enter"), 1.3f));
        Add(Fade(true, 0.4f));
        Add(new HandTargetStep { actor = mary, blend = 0f });
        Add(Together(Xform(carDoor, carDoorClosed, 0.3f), Sfx("CarDoor")));
        Add(Engine(carEngine, true));
        Add(Wait(0.6f));

        // ---------------------------------------------------------- CENA 2 - in the car
        Add(Tele(mary, F(car, "Spot_MaryDriver")));
        Add(State(mary, "Driving", 0f));
        Add(new HandTargetStep { actor = mary, left = F(car, "CarGripL"), right = F(car, "CarGripR"), blend = 0f });
        Add(Shot(F(car, "Shot_CarPOV"), 62f));
        Add(Hand(1.3f));
        Add(Fade(false, 1.2f));
        Add(Wait(1.8f));
        Add(Say(mary, "O governador disse que vocês têm um assunto pendente.", "The governor said you two have some unfinished business."));
        Add(Think("Não tenho nada pra falar com ele.", "I've got nothing to say to him."));
        Add(Think("Mas não é alguém pra quem se diz não.", "But he's not someone you say no to."));
        Add(State(mary, "DrivingGlance", 0.6f, 0.5f));
        Add(Say(mary, "Ele... me contou o que aconteceu. Há dois anos.", "He... told me what happened. Two years ago."));
        Add(Move(F(car, "Shot_CarLook"), 0.8f, 50f));
        Add(State(mary, "DrivingLookAway", 0.5f, 0.4f));
        Add(Say(mary, "Eu sinto muito.", "I'm sorry."));
        Add(Move(F(car, "Shot_CarPOV"), 1.0f, 62f));
        Add(State(mary, "Driving", 0.8f));
        Add(Think("Então ela sabe.", "So she knows."));
        Add(Wait(1.2f));
        Add(Fade(true, 1.2f));
        Add(Engine(carEngine, false));
        Add(new HandTargetStep { actor = mary, blend = 0f });
        Add(new LoadingStep { seconds = 2.5f });

        // ---------------------------------------------------------- CENA 3 - the governor's office
        Add(OfficeLight(sun));
        Add(Tele(mary, F(office, "Spot_MaryCorridor")));
        Add(State(mary, Locomotion, 0f));
        Add(Shot(F(office, "Shot_Corridor"), 60f));
        Add(Hand(1f));
        Add(Fade(false, 1f));
        Add(Together(Xform(mainLeaf, F(office, "MainDoor_Open"), 1.3f), Sfx("OfficeDoor")));
        Add(Move(F(office, "Shot_MaryTalk"), 0.9f));
        Add(State(mary, "TalkStand"));
        Add(Say(mary, "Pode se sentar. O governador já vem.", "Please, have a seat. The governor will be right with you."));
        Add(State(mary, Locomotion));
        Add(Move(F(office, "Move_Doorway"), 1.9f));
        Add(Move(F(office, "Move_ToChair"), 2.3f));
        Add(Move(F(office, "Move_Sit"), 1.4f));
        Add(Together(Xform(mainLeaf, F(office, "MainDoor_Closed"), 1.0f, false), Tele(mary, F(office, "Spot_MaryGone"))));
        Add(Wait(2.2f));
        Add(Together(Xform(sideLeaf, F(office, "SideDoor_Open"), 1.0f), Sfx("OfficeDoor"), Move(F(office, "Shot_SideDoor"), 0.9f)));
        Add(Walk(michael, F(office, "Spot_MichaelIn"), true, false));
        Add(Together(Walk(michael, F(office, "Spot_MichaelGreet")), Move(F(office, "Shot_Greet"), 1.6f)));
        Add(State(michael, "ArmsOpen", 0.25f));
        Add(Say(michael, "Olha só quem veio! Primeiro mandato... quem diria, hein?", "Look who made it! First term... who'd have thought, huh?"));
        Add(Wait(1.9f));   // no reaction
        Add(State(michael, Locomotion, 0.5f));
        Add(Together(Say(michael, "Tá. Você nunca foi de comemorar.", "Right. You never were one for celebrating."),
                     Walk(michael, F(office, "Spot_MichaelAround1"), true, false)));
        Add(Together(Walk(michael, F(office, "Spot_MichaelAround2"), true, false), Move(F(office, "Move_Sit"), 1.8f)));
        Add(Walk(michael, F(office, "Spot_MichaelChair")));
        Add(State(michael, "SitDown", 0.3f, 2.0f));
        Add(State(michael, "SeatedTalk", 0.5f));
        Add(Say(michael, "Vou direto ao ponto. Preciso de você de volta. Como operador.", "I'll get straight to the point. I need you back. As an operator."));
        Add(State(michael, "SeatedIdle", 0.5f));
        Add(You("Não.", "No."));
        Add(State(michael, "SeatedTalk", 0.4f));
        Add(Say(michael, "Me escuta primeiro.", "Hear me out first."));
        Add(Move(F(office, "Shot_SeatBoard"), 2.0f, 50f, false));
        Add(Say(michael, "O governo anterior desviou mais de cinco milhões pra um hospício. Numa área isolada, cercada de montanha e floresta.",
                         "The last administration funneled over five million into an asylum. Somewhere remote, surrounded by mountains and forest."));
        Add(Say(michael, "Tem alguma coisa sendo construída embaixo dele. Não sabemos o quê.", "Something's being built underneath it. We don't know what."));
        Add(Move(F(office, "Move_Sit"), 1.8f, 60f));
        Add(Think("De novo não.", "Not again."));
        Add(State(michael, "SeatedLean", 0.8f));
        Add(Move(F(office, "Shot_SeatFocus"), 1.2f, 40f));
        Add(Say(michael, "Se for o que eu acho que é... tudo que vocês fizeram há dois anos foi em vão.", "If it's what I think it is... everything you did two years ago was for nothing."));
        Add(Say(michael, "Tudo que ela fez.", "Everything she did."));
        Add(Move(F(office, "Shot_SeatLean"), 3.2f, 34f));   // long pause: he leans in a little
        Add(Move(F(office, "Move_Sit"), 1.2f, 60f));
        Add(You("Tudo bem.", "Fine."));
        Add(State(michael, "SeatedIdle", 0.6f));
        Add(Say(michael, "A Helena já está lá.", "Helena's already there."));
        Add(new ScareStep { sound = Clip("Heartbeat"), volume = 0.9f, seconds = 1.8f, flashColor = new Color(0.35f, 0f, 0f), flash = 0.45f, noise = false, shake = 0.8f, zoomKick = -6f, endOnBlack = false });
        Add(Think("Helena. Sozinha.", "Helena. Alone."));
        Add(State(michael, "SeatedTalk", 0.5f));

        Add(Say(michael, "Ela é a parte ativa. Você vai ser os olhos dela.", "She's the one on the ground. You'll be her eyes."));
        Add(Say(michael, "Temos alguém lá dentro. Essa pessoa vai te levar até um lugar seguro, ligado a todas as câmeras do prédio.",
                         "We have someone on the inside. They'll take you somewhere safe, wired into every camera in the building."));
        Add(State(michael, "SeatedIdle", 0.6f));
        Add(Wait(1f));
        Add(Fade(true, 1.5f));
        Add(new LoadingStep { seconds = 2f });

        // ---------------------------------------------------------- CENA 4 - leaving (~20h, dark)
        Add(NightLight(sun));
        Add(Tele(mary, F(night, "Spot_MaryNight")));
        Add(State(mary, Locomotion, 0f));
        Add(Shot(F(night, "Shot_NightExit"), 58f));
        Add(Hand(1f));
        Add(Fade(false, 1.4f));
        Add(Move(F(night, "Shot_NightMary"), 2.6f, 55f));
        Add(State(mary, "TalkStand"));
        Add(Say(mary, "O carro está esperando na frente do prédio. O motorista vai levar você.", "There's a car waiting out front. The driver will take you."));
        Add(State(mary, Locomotion, 0.5f));
        Add(Move(F(night, "Shot_NightVan"), 1.3f));
        Add(Think("Ela não vem.", "She's not coming."));
        Add(Move(F(night, "Shot_NightMary"), 1.0f));
        Add(Say(mary, "Boa sorte.", "Good luck."));
        Add(Wait(0.8f));
        Add(Move(F(night, "Move_NightWalk"), 3.2f));
        Add(Fade(true, 0.6f));
        Add(Sfx("CarDoor"));
        Add(Engine(vanEngine, true));
        Add(Wait(0.9f));

        // ---------------------------------------------------------- CENA 5 - the van with the partition
        Add(VanLight(sun));
        if (arms != null) Add(new SetActiveStep { target = arms.gameObject, active = true });
        Add(Shot(F(van, "Shot_VanPOV"), 58f));
        Add(Hand(1.3f));
        Add(Fade(false, 1.3f));
        Add(Wait(1.6f));
        Add(You("Me leva pra casa.", "Take me home."));
        Add(Wait(3f));   // silence
        Add(Move(F(van, "Move_Lean"), 1.0f));
        if (arms != null) Add(Together(State(arms, FirstPersonPlayer.PushState, 0.1f), Sfx("Knock3", true)));
        else Add(Sfx("Knock3", true));
        Add(Wait(1.6f));
        if (arms != null) Add(Together(State(arms, FirstPersonPlayer.PushState, 0.1f), Sfx("Knock2", true)));
        else Add(Sfx("Knock2", true));
        Add(Wait(2.2f));
        // the glass slides: the back of his head... he turns, slowly, a pig mask - and jerks at us
        Add(Together(Xform(glass, F(van, "PartitionGlass_Open"), 0.22f), Sfx("GlassSlide")));
        Add(Hand(0.5f));
        Add(Wait(1.2f));
        Add(State(pig, "PigTurn", 0.15f, 1.55f));
        Add(new ScareStep { sound = Clip("PigScream"), volume = 1f, seconds = 1.3f, flashColor = new Color(1f, 0.12f, 0.08f), flash = 0.85f, noise = true, shake = 3f, zoomKick = -14f, endOnBlack = true });
        Add(Engine(vanEngine, false));
        Add(Wait(2f));
        // -> he wakes up in the operator room: put that scene's name here (it must be in the build profile)
        Add(new LoadSceneStep { sceneName = "" });

        so.ApplyModifiedPropertiesWithoutUndo();
        return cutscene;
    }
}
