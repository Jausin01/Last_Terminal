using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Last Terminal/17: the first map of the game, the isolation wing of the asylum. Blockout: walls and floors from
// the facility builder, simple props (Art/Props/Asylum, Art/Props/Operator) to be replaced by real art.
// Helena got herself committed as a patient. She wakes up on the bed of cell 5; the code of the cell is the sum of the
// numbers on the tag on her chest (PATIENT 2716 + BED 103 = 2819), she can't see it, the operator can ("look at me").
// Then: the nurses station (the argument, the tutorial, the knife), the rooms around the service hall (the keycard on
// a guard's body in the therapy room), the guard post (save) and its armory (the pistol), the service corridor (the
// first enemy = the combat tutorial), the stairwell (the rest of the asylum, later).
// Things she looks at and comments on (Examinable): "look at the bed", "olha o corpo".
// Plan (meters, x east, z north):
//   CELL 1 | CELL 2 | CELL 3 | OBSERV.          z [-4, 0]
//   ISOLATION WING  (corridor)        -> NURSES STATION x [18, 26] z [-9, -2]
//   CELL 4 | CELL 5 | CELL 6                    z [-11, -7]   (she starts in cell 5)
//   SERVICE HALL x [22, 25] z [-23, -9]: LINEN (west), BATHROOM (west), THERAPY (east)
//   ARMORY | GUARD POST x [19, 26] z [-28, -23] -> SERVICE CORRIDOR x [26, 48] -> STAIRWELL x [48, 52]
internal static class AsylumBuilder
{
    private const string ScenePath = "Assets/Scenes/Asylum_IsolationWing.unity";
    private const string NavMeshPath = "Assets/Scenes/Asylum_IsolationWing_NavMesh.asset";
    private const string ArtFolder = "Assets/Art/Props/Asylum";
    private const string CastAnimFolder = "Assets/Characters/Cast/Animations";
    private const string HelenaIdlePath = "Assets/Characters/Helena/Animations/HelenaIdle.fbx";
    private const string CellCode = "2819";   // 2716 + 103: the tag on her chest (OutfitBuilder, outfit_patient)

    // her bed (cell 5): she sits on its edge 0.30 m in front of her root (Cast_Bed* clips), its front 0.35 m behind it
    private static readonly Vector3 Start = new Vector3(7.0f, 0f, -9.62f);
    private const float BedDepth = 0.85f;

    private const float H = FacilityBuilder.CameraHeight;

    private struct RoomDef
    {
        public string En, Pt;
        public Rect Area;
        public bool Padded;
        public Color Light;
        public float Intensity;
        public RoomDef(string en, string pt, float x0, float z0, float x1, float z1, bool padded, Color light, float intensity)
        {
            En = en; Pt = pt; Area = new Rect(x0, z0, x1 - x0, z1 - z0); Padded = padded; Light = light; Intensity = intensity;
        }
    }

    private static readonly Color Cold = new Color(0.78f, 0.88f, 1f);
    private static readonly Color Sick = new Color(0.85f, 1f, 0.8f);
    private static readonly Color Warm = new Color(1f, 0.85f, 0.65f);
    private static readonly Color Red = new Color(1f, 0.45f, 0.4f);

    private const float C1 = 4.67f, C2 = 9.33f;   // the walls between the cells

    private static readonly RoomDef[] Rooms =
    {
        new RoomDef("Cell 1", "Cela 1", 0f, -4f, C1, 0f, true, Cold, 0.8f),
        new RoomDef("Cell 2", "Cela 2", C1, -4f, C2, 0f, true, Cold, 0.8f),
        new RoomDef("Cell 3", "Cela 3", C2, -4f, 14f, 0f, true, Cold, 1.0f),
        new RoomDef("Observation", "Observação", 14f, -4f, 18f, 0f, false, Sick, 1.2f),
        new RoomDef("Isolation Wing", "Ala de Isolamento", 0f, -7f, 18f, -4f, false, Sick, 1.6f),
        new RoomDef("Cell 4", "Cela 4", 0f, -11f, C1, -7f, true, Cold, 0.8f),
        new RoomDef("Cell 5", "Cela 5", C1, -11f, C2, -7f, true, Cold, 1.3f),
        new RoomDef("Cell 6", "Cela 6", C2, -11f, 14f, -7f, true, Cold, 0.8f),
        new RoomDef("Nurses Station", "Enfermaria", 18f, -9f, 26f, -2f, false, Warm, 2.0f),
        new RoomDef("Service Hall", "Corredor Interno", 22f, -23f, 25f, -9f, false, Sick, 1.5f),
        new RoomDef("Linen Room", "Rouparia", 17f, -15f, 22f, -11f, false, Warm, 1.2f),
        new RoomDef("Bathroom", "Banheiro", 16f, -21f, 22f, -16f, false, Sick, 1.2f),
        new RoomDef("Therapy Room", "Sala de Terapia", 25f, -17f, 31f, -11f, false, Cold, 1.4f),
        new RoomDef("Guard Post", "Guarita", 19f, -28f, 26f, -23f, false, Warm, 1.8f),
        new RoomDef("Armory", "Arsenal", 16f, -28f, 19f, -24f, false, Warm, 1.2f),
        new RoomDef("Service Corridor", "Corredor de Serviço", 26f, -27f, 48f, -24f, false, Red, 1.3f),
        new RoomDef("Stairwell", "Escada", 48f, -28f, 52f, -23f, false, Sick, 1.2f),
    };

    private struct DoorDef
    {
        public string Name;          // the lexicon key it answers to (the room it leads to)
        public Vector3 Position;
        public float Yaw;            // 0: in a wall along x, 90: in a wall along z
        public bool Security, Open, Locked, Remote;
        public DoorDef(string name, float x, float z, float yaw, bool security, bool open = false, bool locked = false, bool remote = false)
        {
            Name = name; Position = new Vector3(x, 0f, z); Yaw = yaw; Security = security; Open = open; Locked = locked; Remote = remote;
        }
    }

    private const float D1 = C1 / 2f, D2 = (C1 + C2) / 2f, D3 = (C2 + 14f) / 2f;   // cell door centers

    private static readonly DoorDef[] Doors =
    {
        new DoorDef("Cell 1", D1, -4f, 0f, true, locked: true),
        new DoorDef("Cell 2", D2, -4f, 0f, true, locked: true),
        new DoorDef("Cell 3", D3, -4f, 0f, true, open: true),                      // forced open
        new DoorDef("Cell 4", D1, -7f, 0f, true, locked: true),
        new DoorDef("Cell 5", D2, -7f, 0f, true, locked: true),                    // hers: the keypad
        new DoorDef("Cell 6", D3, -7f, 0f, true, locked: true),
        new DoorDef("Observation", 16f, -4f, 0f, false),
        new DoorDef("Nurses Station", 18f, -5.5f, 90f, false),
        new DoorDef("Service Hall", 23.5f, -9f, 0f, false),
        new DoorDef("Linen Room", 22f, -13f, 90f, false),
        new DoorDef("Bathroom", 22f, -18.5f, 90f, false),
        new DoorDef("Therapy Room", 25f, -14f, 90f, false),
        new DoorDef("Guard Post", 23.5f, -23f, 0f, true, remote: true),          // the operator opens it from the map
        new DoorDef("Armory", 19f, -26f, 90f, false, locked: true),               // the keycard (therapy room)
        new DoorDef("Service Corridor", 26f, -25.5f, 90f, false, locked: true),   // unlocked when she gets the pistol
        new DoorDef("Stairwell", 48f, -25.5f, 90f, true, locked: true),           // opens after the first kill
    };

    // ================================================================== menu
    [MenuItem("Last Terminal/17 - Build Asylum: Isolation Wing")]
    public static void BuildScene()
    {
        Shader lit = HelenaSetupMenu.DefaultLitShader();
        if (lit == null) return;
        if (!AssetDatabase.IsValidFolder(ArtFolder))
        {
            Debug.LogError($"[Asylum] {ArtFolder} not found: extract the package at the project root (the folder with Assets).");
            return;
        }
        OperatorRoomBuilder.RemapFolder(ArtFolder, lit);
        EnsureCastAvatar();
        HelenaSetupMenu.BuildLocation(ScenePath, NavMeshPath, BuildContent);
    }

    // the bed clips are made on her skeleton: Humanoid with her avatar (new files get their own one otherwise)
    private static void EnsureCastAvatar()
    {
        Avatar helena = AssetDatabase.LoadAllAssetsAtPath(HelenaIdlePath).OfType<Avatar>().FirstOrDefault();
        if (helena == null) { Debug.LogWarning($"[Asylum] {HelenaIdlePath} has no avatar (run menu 0): the bed clips keep their own."); return; }
        foreach (string guid in AssetDatabase.FindAssets("Cast_Bed t:Model", new[] { CastAnimFolder }))
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (mi == null || mi.sourceAvatar == helena) continue;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = helena;
            mi.SaveAndReimport();
        }
    }

    private static AnimationClip CastClip(string name)
    {
        AnimationClip c = AssetDatabase.LoadAllAssetsAtPath($"{CastAnimFolder}/{name}.fbx").OfType<AnimationClip>()
            .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        if (c == null) Debug.LogWarning($"[Asylum] {CastAnimFolder}/{name}.fbx missing: that part of the cutscene is skipped.");
        return c;
    }

    // what the cutscenes need from the content (filled while building it)
    private class Refs
    {
        public readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        public readonly Dictionary<string, Door> Doors = new Dictionary<string, Door>();
        public readonly Dictionary<string, SecurityCamera> Cameras = new Dictionary<string, SecurityCamera>();
        public readonly List<LanguageVariant> Variants = new List<LanguageVariant>();
        public Transform Marks;
        public Transform StartMark, WritingMark;
    }

    // ================================================================== content
    private static HelenaSetupMenu.LocationContent BuildContent(Scene scene, Shader lit)
    {
        var content = new HelenaSetupMenu.LocationContent
        {
            TestEnemies = false, EmbedOperatorRoom = false, BareStart = true, StartOutfit = OutfitBuilder.PatientOutfitKey,
            PlaceName = "ASYLUM - ISOLATION WING", PlaceNamePt = "HOSPÍCIO - ALA DE ISOLAMENTO",
        };
        var refs = new Refs();
        var level = new FacilityBuilder.Level
        {
            Rooms = new List<Room>(), Doors = new List<Door>(), Cameras = new List<SecurityCamera>(), Keypads = new List<Keypad>(),
        };
        var root = new GameObject("Asylum");
        level.ContentRoot = root.transform;
        var geometry = new GameObject("Geometry").transform;   // baked into the NavMesh
        geometry.SetParent(root.transform, false);

        string env = FacilityBuilder.EnvFolder;
        Material wall = FacilityBuilder.MaterialFor($"{env}/Materials/M_Wall.mat", $"{env}/T_Wall.png", 0.1f, lit, env);
        Material floor = FacilityBuilder.MaterialFor($"{env}/Materials/M_Floor.mat", $"{env}/T_Floor.png", 0.25f, lit, env);
        Material padded = FacilityBuilder.MaterialFor($"{env}/Materials/M_PaddedWall.mat", $"{env}/T_PaddedWall.png", 0.05f, lit, env);
        Material paddedFloor = FacilityBuilder.MaterialFor($"{env}/Materials/M_PaddedFloor.mat", $"{env}/T_PaddedFloor.png", 0.05f, lit, env);

        // rooms + floors + lights
        var rooms = new GameObject("Rooms").transform;
        rooms.SetParent(root.transform, false);
        foreach (RoomDef r in Rooms)
        {
            Room room = FacilityBuilder.AddRoom(rooms, geometry, r.Padded ? paddedFloor : floor, r.En, r.Pt, r.Area, r.Light, r.Intensity);
            level.Rooms.Add(room);
            refs.Rooms[r.En] = room;
        }
        FacilityBuilder.SetSafeZone(refs.Rooms["Cell 5"]);
        FacilityBuilder.SetSafeZone(refs.Rooms["Guard Post"]);

        // walls: every room edge once (shared edges merged), the door gaps cut out; padding inside the cells
        var walls = new GameObject("Walls").transform;
        walls.SetParent(geometry, false);
        BuildWalls(walls, wall);
        foreach (RoomDef r in Rooms) if (r.Padded) PadCell(walls, padded, r.Area);

        // NavMesh from the geometry only (doors and props carve their own holes at runtime)
        FacilityBuilder.EnsureBakeRadius(FacilityBuilder.PartnerRadius);
        var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.BuildNavMesh();
        level.Surface = surface;

        // doors, numbered in order (plates above them)
        var doorsRoot = new GameObject("Doors").transform;
        doorsRoot.SetParent(root.transform, false);
        var signs = new FacilityBuilder.DoorSigns
        {
            Wall = wall,
            Plates = FacilityBuilder.MaterialFor($"{FacilityBuilder.DoorsFolder}/Materials/M_DoorPlates.mat", $"{FacilityBuilder.DoorsFolder}/T_DoorPlates.png",
                                                 0.35f, lit, FacilityBuilder.DoorsFolder),
        };
        Dictionary<string, ItemDefinition> items = HelenaSetupMenu.TestItems();
        for (int i = 0; i < Doors.Length; i++)
        {
            DoorDef d = Doors[i];
            Door door = FacilityBuilder.AddDoor(doorsRoot, d.Security ? FacilityBuilder.SecurityDoorPrefabPath : FacilityBuilder.WoodDoorPrefabPath,
                                                d.Name, d.Position, d.Yaw, d.Open, i + 1, signs);
            if (door == null) continue;
            var so = new SerializedObject(door);
            so.FindProperty(Door.LockedField).boolValue = d.Locked;
            so.FindProperty(Door.RemoteField).boolValue = d.Remote;
            if (d.Name == "Armory") so.FindProperty(Door.RequiredKeyField).objectReferenceValue = items["Keycard"];
            so.ApplyModifiedPropertiesWithoutUndo();
            level.Doors.Add(door);
            refs.Doors[d.Name] = door;
        }
        // her keypad: inside the cell, right of the door, on the padding
        if (refs.Doors.TryGetValue("Cell 5", out Door cellDoor))
        {
            float face = -7f - FacilityBuilder.WallThickness * 0.5f - FacilityBuilder.PaddingThickness;
            Keypad keypad = FacilityBuilder.BuildKeypad(root.transform, cellDoor, lit, new Vector3(D2 + 1.15f, 1.27f, face), Vector3.back, CellCode);
            if (keypad != null) level.Keypads.Add(keypad);
        }

        BuildCameras(level, root.transform, refs);
        level.PartnerStart = Start;
        level.PartnerStartYaw = 0f;

        // props, things to look at, pickups, bodies
        var props = new GameObject("Props").transform;
        props.SetParent(root.transform, false);
        var looks = new GameObject("Examinables").transform;
        looks.SetParent(root.transform, false);
        var pickups = new GameObject("Pickups").transform;
        pickups.SetParent(root.transform, false);
        refs.Marks = new GameObject("CutsceneMarks").transform;
        refs.Marks.SetParent(root.transform, false);
        BuildCells(props, looks, lit, content, refs);
        BuildNurses(props, looks, pickups, lit, content);
        BuildServiceRooms(props, looks, pickups, lit, content);
        BuildGuardPost(props, looks, pickups, lit, content);
        BuildCorridor(props, looks, lit, content);

        // the first enemy: feeding on a body at the end of the service corridor, its back to her
        content.Enemies.Add(new HelenaSetupMenu.EnemySpec
        {
            Prefab = "Enemy_Zombie", Position = new Vector3(45.4f, 0f, -25.5f), Yaw = 90f, Mode = EnemyStartMode.Feeding, SaveId = "asy_corridor_feeder",
        });

        // dim, cold light
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.13f, 0.15f);
        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.25f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(62f, -30f, 0f);

        content.Level = level;
        content.Finish = rig => Finish(rig, refs);
        content.Description = "Asylum, isolation wing. She wakes up in cell 5: \"olha pra mim\" shows the tag on her chest (2716 + 103 = 2819), " +
                              "\"digita 2 8 1 9\". \"Olha a cama\", \"olha o corpo\"...: she comments on things.";
        return content;
    }

    // ------------------------------------------------------------------ walls
    private static void BuildWalls(Transform parent, Material m)
    {
        // edges of every room, per line (along x at z / along z at x), merged where rooms share them
        var lines = new Dictionary<(bool alongX, int key), (float coord, List<Vector2> spans)>();
        foreach (RoomDef r in Rooms)
        {
            Rect a = r.Area;
            AddEdge(lines, true, a.yMin, a.xMin, a.xMax);
            AddEdge(lines, true, a.yMax, a.xMin, a.xMax);
            AddEdge(lines, false, a.xMin, a.yMin, a.yMax);
            AddEdge(lines, false, a.xMax, a.yMin, a.yMax);
        }
        foreach (var kv in lines)
        {
            bool alongX = kv.Key.alongX;
            float coord = kv.Value.coord;
            foreach (Vector2 span in Merge(kv.Value.spans))
            {
                // the doors on this line, inside this span (center along the line, width of their frame)
                var gaps = new List<(float center, float width)>();
                foreach (DoorDef d in Doors)
                {
                    bool doorAlongX = Mathf.Approximately(d.Yaw, 0f);
                    if (doorAlongX != alongX) continue;
                    float onLine = alongX ? d.Position.z : d.Position.x;
                    float along = alongX ? d.Position.x : d.Position.z;
                    if (Mathf.Abs(onLine - coord) > 0.01f || along <= span.x || along >= span.y) continue;
                    gaps.Add((along, d.Security ? FacilityBuilder.SecurityDoorWidth : FacilityBuilder.WoodDoorWidth));
                }
                gaps.Sort((g1, g2) => g1.center.CompareTo(g2.center));
                if (alongX) FacilityBuilder.WallX(parent, m, coord, span.x, span.y, gaps.ToArray());
                else FacilityBuilder.WallZ(parent, m, coord, span.x, span.y, gaps.ToArray());
            }
        }
    }

    private static void AddEdge(Dictionary<(bool, int), (float, List<Vector2>)> lines, bool alongX, float coord, float from, float to)
    {
        var key = (alongX, Mathf.RoundToInt(coord * 100f));
        if (!lines.TryGetValue(key, out (float coord, List<Vector2> spans) line))
        {
            line = (coord, new List<Vector2>());
            lines[key] = line;
        }
        line.spans.Add(new Vector2(from, to));
    }

    private static List<Vector2> Merge(List<Vector2> spans)
    {
        var sorted = spans.OrderBy(s => s.x).ToList();
        var merged = new List<Vector2>();
        foreach (Vector2 s in sorted)
        {
            if (merged.Count > 0 && s.x <= merged[merged.Count - 1].y + 0.01f)
            {
                Vector2 last = merged[merged.Count - 1];
                merged[merged.Count - 1] = new Vector2(last.x, Mathf.Max(last.y, s.y));
            }
            else merged.Add(s);
        }
        return merged;
    }

    // the padded lining inside a cell (its door gap kept: the cell doors are on the wing side)
    private static void PadCell(Transform parent, Material padded, Rect a)
    {
        float t = FacilityBuilder.PaddingThickness, w = FacilityBuilder.WallThickness * 0.5f, h = FacilityBuilder.WallHeight;
        float doorX = a.center.x < C1 ? D1 : a.center.x < C2 ? D2 : D3;
        bool doorSouth = a.yMax > -2f;   // top row: the door on its south wall
        foreach (bool north in new[] { true, false })
        {
            float z = north ? a.yMax - w - t * 0.5f : a.yMin + w + t * 0.5f;
            bool hasDoor = north != doorSouth;
            var gaps = hasDoor ? new[] { (doorX, FacilityBuilder.SecurityDoorWidth) } : new (float, float)[0];
            foreach (var (s0, s1) in FacilityBuilder.Segments(a.xMin + w, a.xMax - w, gaps))
                FacilityBuilder.Block(parent, "Padding", new Vector3((s0 + s1) * 0.5f, h * 0.5f, z), new Vector3(s1 - s0, h, t), padded, 2f, true);
        }
        foreach (bool east in new[] { true, false })
        {
            float x = east ? a.xMax - w - t * 0.5f : a.xMin + w + t * 0.5f;
            float z0 = a.yMin + w + t, z1 = a.yMax - w - t;
            FacilityBuilder.Block(parent, "Padding", new Vector3(x, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(t, h, z1 - z0), padded, 2f, true);
        }
    }

    // ------------------------------------------------------------------ cameras
    private static void BuildCameras(FacilityBuilder.Level level, Transform root, Refs refs)
    {
        var cams = new GameObject("SecurityCameras").transform;
        cams.SetParent(root, false);
        int n = 1;
        SecurityCamera Cam(string room, Vector3 mount, Vector3 look)
        {
            SecurityCamera c = FacilityBuilder.AddCamera(level, cams, $"CAM {n++:00}", refs.Rooms[room], mount, look);
            if (c != null && !refs.Cameras.ContainsKey(room)) refs.Cameras[room] = c;
            return c;
        }
        // her cell first: the operator starts on it (from the corner over the toilet, it sees the bed, the wall, the keypad)
        level.StartCamera = level.Cameras.Count;
        Cam("Cell 5", new Vector3(C2 - 0.3f, H, -10.7f), new Vector3(6.4f, 0f, -8.9f));
        Cam("Cell 4", new Vector3(C1 - 0.3f, H, -10.7f), new Vector3(2.2f, 0f, -8.6f));
        Cam("Cell 6", new Vector3(14f - 0.3f, H, -10.7f), new Vector3(11.4f, 0f, -8.6f));
        Cam("Cell 1", new Vector3(C1 - 0.3f, H, -0.3f), new Vector3(2.2f, 0f, -2.4f));
        Cam("Cell 2", new Vector3(C2 - 0.3f, H, -0.3f), new Vector3(6.8f, 0f, -2.4f));
        Cam("Cell 3", new Vector3(14f - 0.3f, H, -0.3f), new Vector3(11.4f, 0f, -2.4f));
        Cam("Isolation Wing", new Vector3(0.3f, H, -4.3f), new Vector3(9f, 0f, -5.6f));
        Cam("Isolation Wing", new Vector3(17.7f, H, -6.7f), new Vector3(7f, 0f, -5.4f));
        Cam("Observation", new Vector3(14.3f, H, -0.3f), new Vector3(16.2f, 0f, -2.6f));
        Cam("Nurses Station", new Vector3(25.7f, H, -8.7f), new Vector3(21.5f, 0f, -4.5f));
        Cam("Service Hall", new Vector3(24.7f, H, -9.3f), new Vector3(23.5f, 0f, -16f));
        Cam("Service Hall", new Vector3(22.3f, H, -22.7f), new Vector3(23.5f, 0f, -14f));
        Cam("Linen Room", new Vector3(17.3f, H, -11.3f), new Vector3(19.8f, 0f, -13.6f));
        Cam("Bathroom", new Vector3(21.7f, H, -16.3f), new Vector3(18.4f, 0f, -19.2f));
        Cam("Therapy Room", new Vector3(30.7f, H, -11.3f), new Vector3(27.4f, 0f, -14.6f));
        Cam("Guard Post", new Vector3(25.7f, H, -23.3f), new Vector3(21.8f, 0f, -26.2f));
        Cam("Armory", new Vector3(18.7f, H, -24.3f), new Vector3(17.2f, 0f, -26.6f));
        Cam("Service Corridor", new Vector3(26.3f, H, -24.3f), new Vector3(40f, 0f, -25.5f));
        Cam("Service Corridor", new Vector3(47.7f, H, -26.7f), new Vector3(36f, 0f, -25.5f));
        Cam("Stairwell", new Vector3(48.3f, H, -23.3f), new Vector3(50.6f, 0f, -26.2f));
    }

    // ------------------------------------------------------------------ props
    // a model (FBX), placed, with a box collider; solid ones carve the NavMesh (never baked: the bake has walls only)
    private static GameObject Prop(Transform parent, string folder, string model, Vector3 pos, float yaw, bool solid = true, Vector3? tilt = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{model}.fbx");
        if (asset == null) { Debug.LogWarning($"[Asylum] {folder}/{model}.fbx missing: not placed."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt?.x ?? 0f, yaw, tilt?.z ?? 0f));
        OperatorRoomBuilder.FitCollider(go);
        if (solid && go.TryGetComponent(out BoxCollider box)) Carve(go, box.center, box.size);
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    private static GameObject Asy(Transform parent, string model, Vector3 pos, float yaw, bool solid = true) =>
        Prop(parent, ArtFolder, model, pos, yaw, solid);

    private static GameObject Op(Transform parent, string model, Vector3 pos, float yaw, bool solid = true, Vector3? tilt = null) =>
        Prop(parent, OperatorRoomBuilder.ArtFolder, model, pos, yaw, solid, tilt);

    private static void Carve(GameObject go, Vector3 center, Vector3 size)
    {
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = center;
        obstacle.size = size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // a plain box (blockout furniture), solid
    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        Material m = Flat("M_Asy_" + name, color);
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        return go;
    }

    private static Material Flat(string name, Color color)
    {
        Shader lit = HelenaSetupMenu.DefaultLitShader();
        if (lit == null) return null;
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Materials");
        Material m = HelenaSetupMenu.MakeMaterial($"{ArtFolder}/Materials/{name}.mat", null, 0.2f, false, lit);
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    // a flat picture (blood on a wall or the floor, a poster): a quad seen from `facing`
    private static GameObject Decal(Transform parent, string name, string texture, Vector3 pos, Vector3 facing, Vector2 size, bool clip, Shader lit)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{texture}.png");
        if (tex == null) { Debug.LogWarning($"[Asylum] {ArtFolder}/{texture}.png missing."); return null; }
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Materials");
        Material m = HelenaSetupMenu.MakeMaterial($"{ArtFolder}/Materials/M_{texture.Substring(2)}.mat", tex, 0.3f, clip, lit);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        Vector3 up = Mathf.Abs(Vector3.Dot(facing.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing, up));   // a quad shows its face toward -z
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    private static float TopOf(GameObject go, float fallback)
    {
        if (go == null) return fallback;
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return fallback;
        float top = float.MinValue;
        foreach (Renderer r in renderers) top = Mathf.Max(top, r.bounds.max.y);
        return top;
    }

    // something she can look at; stand = where she stands for it (null: in front of it, on her side)
    private static Examinable Look(Transform parent, HelenaSetupMenu.LocationContent content, string key, Vector3 pos, Vector3? stand,
                                   string[] en, string[] pt, string flag = "")
    {
        var go = new GameObject("Look_" + key);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var x = go.AddComponent<Examinable>();
        x.EditorSet(key, en, pt, flag);
        if (stand.HasValue)
        {
            var s = new GameObject("Stand").transform;
            s.SetParent(go.transform, false);
            s.position = stand.Value;
            HelenaSetupMenu.SetReference(x, Examinable.StandPointField, s);
        }
        content.Examinables.Add(x);
        return x;
    }

    private static Transform Mark(Refs refs, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(refs.Marks, false);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    // ------------------------------------------------------------------ the cells
    private static void BuildCells(Transform props, Transform looks, Shader lit, HelenaSetupMenu.LocationContent content, Refs refs)
    {
        // every cell: a bed against the far wall, a steel toilet in the corner
        (string room, float cx, bool south)[] cells =
        {
            ("Cell 1", D1, false), ("Cell 2", D2, false), ("Cell 3", D3, false), ("Cell 4", D1, true), ("Cell 5", D2, true), ("Cell 6", D3, true),
        };
        float inner = FacilityBuilder.WallThickness * 0.5f + FacilityBuilder.PaddingThickness;
        foreach (var c in cells)
        {
            Rect a = Rooms.First(r => r.En == c.room).Area;
            float farWall = c.south ? a.yMin + inner : a.yMax - inner;
            float bedZ = c.south ? farWall + BedDepth * 0.5f + 0.03f : farWall - BedDepth * 0.5f - 0.03f;
            if (c.room == "Cell 5") bedZ = Start.z - 0.35f - BedDepth * 0.5f;   // where her clips put her
            Asy(props, "SM_Asy_Bed", new Vector3(c.cx, 0f, bedZ), c.south ? 0f : 180f);
            float toiletX = a.xMin + inner + 0.35f;
            float toiletZ = c.south ? farWall + 0.32f : farWall - 0.32f;
            if (c.room != "Cell 5") Asy(props, "SM_Asy_Toilet", new Vector3(toiletX, 0f, toiletZ), c.south ? 0f : 180f);
            else Asy(props, "SM_Asy_Toilet", new Vector3(a.xMax - inner - 0.35f, 0f, toiletZ), 0f);   // hers: under the camera
        }

        // her cell: the blood on the west wall, its English / Portuguese version
        float wallX = C1 + FacilityBuilder.WallThickness * 0.5f + FacilityBuilder.PaddingThickness + 0.005f;
        var writing = new GameObject("BloodWriting");
        writing.transform.SetParent(props, false);
        Vector3 writingPos = new Vector3(wallX, 1.55f, -9.0f);
        GameObject enW = Decal(writing.transform, "EN", "T_Asy_BloodWriting_EN", writingPos, Vector3.right, new Vector2(2.2f, 1.1f), true, lit);
        GameObject ptW = Decal(writing.transform, "PT", "T_Asy_BloodWriting_PT", writingPos, Vector3.right, new Vector2(2.2f, 1.1f), true, lit);
        if (enW != null && ptW != null)
        {
            var variant = writing.AddComponent<LanguageVariant>();
            HelenaSetupMenu.SetReference(variant, LanguageVariant.EnglishField, enW);
            HelenaSetupMenu.SetReference(variant, LanguageVariant.PortugueseField, ptW);
            refs.Variants.Add(variant);
        }
        refs.StartMark = Mark(refs, "Start_Bed", Start, 0f);
        refs.WritingMark = Mark(refs, "Look_Writing", writingPos, 0f);

        Look(looks, content, "x_writing", writingPos, new Vector3(5.9f, 0f, -9.0f),
            new[] { "\"The code is in the sum\"... written in blood. Lovely.",
                    "The sum of what? There isn't a single number in this cell...",
                    "Hey, you're the one with the camera. Do you see anything I don't?" },
            new[] { "\"A senha está na soma\"... escrito com sangue. Que ótimo.",
                    "Soma do quê? Não tem um número nessa cela...",
                    "Ei, você é quem tem a câmera. Tá vendo alguma coisa que eu não tô?" });
        Look(looks, content, "x_bed", new Vector3(D2, 0.5f, Start.z - 0.35f - BedDepth * 0.5f), null,
            new[] { "The bed I woke up on. Bolted to the floor, so nobody uses it as a weapon.",
                    "Three nights on that thing. My back's going to make me pay for it." },
            new[] { "A cama onde eu acordei. Parafusada no chão, pra ninguém usar de arma.",
                    "Três noites nisso. Minhas costas vão me cobrar." });
        Look(looks, content, "x_toilet", new Vector3(C2 - inner - 0.35f, 0.45f, -10.5f), null,
            new[] { "Steel toilet. No lid, no privacy. And you watching.", "No. Don't even think about it." },
            new[] { "Privada de aço. Sem tampa, sem privacidade. E você aí olhando.", "Não. Nem pensa." });

        // the dead in the locked cells (seen through the doors, from the wing)
        (string room, float x, float z, float yaw, bool back, string[] en, string[] pt)[] bodies =
        {
            ("Cell 4", D1 - 0.9f, -8.4f, 30f, true,
                new[] { "Dead. For days. Nobody came for him.", "The chart on the door says \"calm patient\". He's calm, all right." },
                new[] { "Morto. Faz dias. Ninguém veio buscar.", "A ficha na porta diz \"paciente calmo\". Calmo ele tá mesmo." }),
            ("Cell 6", D3 + 0.5f, -8.2f, 200f, false,
                new[] { "He clawed at the door until his nails came off... What happened here?" },
                new[] { "Ele arranhou a porta até as unhas saírem... O que aconteceu aqui?" }),
            ("Cell 1", D1 + 0.4f, -1.6f, 120f, true,
                new[] { "Another one. Same empty stare. That could've been me." },
                new[] { "Outro. O mesmo olhar vazio. Podia ser eu." }),
            ("Cell 2", D2 - 0.5f, -2.0f, 300f, false,
                new[] { "Same scrubs as mine... I'm not ending up like that." },
                new[] { "A mesma roupa que a minha... Eu não vou terminar assim." }),
        };
        foreach (var b in bodies)
        {
            Vector3 pos = new Vector3(b.x, 0f, b.z);
            EnemyBuilder.PlaceCorpse(props, "Body_" + b.room.Replace(" ", ""), pos, b.yaw, b.back);
            float doorX = b.room == "Cell 1" || b.room == "Cell 4" ? D1 : b.room == "Cell 2" ? D2 : D3;
            bool topRow = b.room == "Cell 1" || b.room == "Cell 2";
            Look(looks, content, "x_body", pos + Vector3.up * 0.3f, new Vector3(doorX, 0f, topRow ? -4.75f : -6.25f), b.en, b.pt);
        }
        // cell 3: the door forced open, the cell empty
        Look(looks, content, "x_forced", new Vector3(D3, 1.1f, -4f), new Vector3(D3 + 0.9f, 0f, -4.8f),
            new[] { "Forced from the inside. Whoever was in there walked out... or crawled." },
            new[] { "Forçada por dentro. Quem tava aí saiu andando... ou rastejando." });

        // the wing: a blood trail toward the nurses station
        var wing = new GameObject("Wing").transform;
        wing.SetParent(props, false);
        Decal(wing, "BloodTrail", "T_Asy_BloodPool", new Vector3(15.6f, 0.012f, -5.6f), Vector3.up, new Vector2(1.6f, 1.6f), true, lit);
        Decal(wing, "BloodTrail", "T_Asy_BloodPool", new Vector3(13.1f, 0.012f, -5.3f), Vector3.up, new Vector2(0.9f, 0.9f), true, lit);
        Look(looks, content, "x_blood", new Vector3(15.6f, 0.05f, -5.6f), null,
            new[] { "A blood trail... heading to the nurses station. Of course it is." },
            new[] { "Um rastro de sangue... indo pra enfermaria. Claro que vai." });

        // observation: monitors of the cells (dead), a chair knocked over
        var obs = new GameObject("Observation").transform;
        obs.SetParent(props, false);
        GameObject desk = Op(obs, "SM_OpDesk", new Vector3(16f, 0f, -0.65f), 180f);
        float top = TopOf(desk, 0.75f);
        Op(obs, "SM_OperatorPC", new Vector3(16f, top, -0.55f), 180f, false);
        Op(obs, "SM_OpChair", new Vector3(15.3f, 0.3f, -1.9f), 40f, false, new Vector3(0f, 0f, 90f));
        Look(looks, content, "x_monitors", new Vector3(16f, top + 0.3f, -0.55f), null,
            new[] { "Monitors for the cells. All dead... someone used to watch us sleep." },
            new[] { "Monitores das celas. Tudo apagado... alguém ficava vendo a gente dormir." });
        Look(looks, content, "x_chair", new Vector3(15.3f, 0.3f, -1.9f), null,
            new[] { "Knocked over. Whoever sat here left in a hurry." },
            new[] { "Derrubada. Quem sentava aqui saiu com pressa." });
    }

    // ------------------------------------------------------------------ the nurses station
    private static void BuildNurses(Transform props, Transform looks, Transform pickups, Shader lit, HelenaSetupMenu.LocationContent content)
    {
        var room = new GameObject("NursesStation").transform;
        room.SetParent(props, false);
        GameObject desk = Op(room, "SM_OpDesk", new Vector3(21.5f, 0f, -2.65f), 180f);
        float deskTop = TopOf(desk, 0.75f);
        Op(room, "SM_OpChair", new Vector3(21.5f, 0f, -3.5f), 10f);
        Op(room, "SM_OpPapers", new Vector3(21.0f, deskTop, -2.6f), 160f, false);
        Asy(room, "SM_Asy_Cabinet", new Vector3(25.65f, 0f, -4.2f), 270f);
        Asy(room, "SM_Asy_Sink", new Vector3(18.3f, 0f, -8.2f), 90f);
        GameObject gurney = Asy(room, "SM_Asy_Gurney", new Vector3(24.2f, 0f, -7.5f), 0f);
        float gurneyTop = TopOf(gurney, 0.85f);
        float wallFace = -2f - FacilityBuilder.WallThickness * 0.5f - 0.01f;
        Decal(room, "WardMap", "T_Asy_WardMap", new Vector3(19.4f, 1.6f, wallFace), Vector3.back, new Vector2(1.2f, 0.9f), false, lit);

        // the knife on the gurney (her first weapon), a medkit on the desk
        Pickup knife = HelenaSetupMenu.PlaceWeaponPickup(pickups, "KitchenKnife", new Vector3(24.0f, gurneyTop, -7.45f), 20f);
        if (knife != null) content.Pickups.Add(knife);
        Pickup medkit = HelenaSetupMenu.PlaceItemPickup(pickups, "Medkit", 1, new Vector3(22.3f, deskTop, -2.55f), 0f, new Color(0.2f, 0.75f, 0.3f));
        if (medkit != null) content.Pickups.Add(medkit);

        Look(looks, content, "x_desk", new Vector3(21.0f, deskTop, -2.6f), null,
            new[] { "Patient files... mine's here. \"Patient 2716. Persecutory delusions.\" At least the cover held.",
                    "\"Believes she is being watched at all times.\" ...Well." },
            new[] { "Prontuários... o meu tá aqui. \"Paciente 2716. Delírios persecutórios.\" Pelo menos o disfarce colou.",
                    "\"Acredita que está sendo observada o tempo todo.\" ...Pois é." });
        Look(looks, content, "x_map", new Vector3(19.4f, 1.6f, wallFace), new Vector3(19.4f, 0f, -3.1f),
            new[] { "Map of the wing. The way out is the stairwell, past the guard post and the service corridor.",
                    "The armory is next to the guard post. That's where the real guns are." },
            new[] { "Mapa da ala. A saída é pela escada, passando pela guarita e pelo corredor de serviço.",
                    "O arsenal fica do lado da guarita. É lá que estão as armas de verdade." });
        Look(looks, content, "x_cabinet", new Vector3(25.65f, 1.2f, -4.2f), null,
            new[] { "Medicine cabinet. Empty. Someone cleaned it out before me." },
            new[] { "Armário de remédios. Vazio. Alguém limpou tudo antes de mim." });
        Look(looks, content, "x_sink", new Vector3(18.3f, 0.9f, -8.2f), null,
            new[] { "The water comes out brown. I'll stay thirsty, thanks." },
            new[] { "A água sai marrom. Prefiro ficar com sede." });
        Look(looks, content, "x_gurney", new Vector3(24.2f, gurneyTop, -7.5f), null,
            new[] { "A gurney with leather straps... and somebody left a knife on it." },
            new[] { "Uma maca com correias de couro... e alguém deixou uma faca em cima." });
    }

    // ------------------------------------------------------------------ service hall, linen, bathroom, therapy
    private static void BuildServiceRooms(Transform props, Transform looks, Transform pickups, Shader lit, HelenaSetupMenu.LocationContent content)
    {
        var hall = new GameObject("ServiceHall").transform;
        hall.SetParent(props, false);
        Asy(hall, "SM_Asy_Gurney", new Vector3(24.4f, 0f, -17.5f), 90f);
        Decal(hall, "Blood", "T_Asy_BloodPool", new Vector3(23.3f, 0.012f, -19.5f), Vector3.up, new Vector2(1.2f, 1.2f), true, lit);
        Look(looks, content, "x_gurney", new Vector3(24.4f, 0.85f, -17.5f), null,
            new[] { "Another gurney. Straps torn. Whoever was tied here got loose." },
            new[] { "Outra maca. As correias arrebentadas. Quem tava amarrado aqui se soltou." });

        var linen = new GameObject("LinenRoom").transform;
        linen.SetParent(props, false);
        Asy(linen, "SM_Asy_Shelf", new Vector3(17.35f, 0f, -12.2f), 90f);
        Asy(linen, "SM_Asy_Shelf", new Vector3(17.35f, 0f, -13.8f), 90f);
        Look(looks, content, "x_shelf", new Vector3(17.35f, 1.0f, -13.0f), null,
            new[] { "Clean sheets, folded. The only clean thing in this building." },
            new[] { "Lençóis limpos, dobrados. A única coisa limpa nesse prédio." });

        var bath = new GameObject("Bathroom").transform;
        bath.SetParent(props, false);
        for (int i = 0; i < 3; i++) Asy(bath, "SM_Asy_Toilet", new Vector3(17.0f + i * 1.3f, 0f, -20.6f), 0f);
        for (int i = 0; i < 2; i++) Asy(bath, "SM_Asy_Sink", new Vector3(16.3f, 0f, -17.2f - i * 1.1f), 90f);
        Decal(bath, "Blood", "T_Asy_BloodPool", new Vector3(19.5f, 0.012f, -18.6f), Vector3.up, new Vector2(1.4f, 1.4f), true, lit);
        Look(looks, content, "x_toilet", new Vector3(18.3f, 0.45f, -20.6f), null,
            new[] { "Shared bathroom. Smells worse than my cell. Didn't think that was possible." },
            new[] { "Banheiro coletivo. Cheira pior que a minha cela. Achei que não dava." });
        Look(looks, content, "x_sink", new Vector3(16.3f, 0.9f, -17.75f), null,
            new[] { "Somebody washed blood off their hands here. Not very well." },
            new[] { "Alguém lavou sangue das mãos aqui. Não muito bem." });

        // therapy: the chair with the straps, a desk with a tape recorder, the guard (the keycard by him)
        var therapy = new GameObject("TherapyRoom").transform;
        therapy.SetParent(props, false);
        Op(therapy, "SM_OpChair", new Vector3(28.4f, 0f, -14.2f), 250f);
        GameObject desk = Op(therapy, "SM_OpDesk", new Vector3(29.6f, 0f, -11.65f), 180f);
        float deskTop = TopOf(desk, 0.75f);
        GameObject tape = Box(therapy, "TapeRecorder", new Vector3(29.2f, deskTop + 0.04f, -11.6f), new Vector3(0.25f, 0.08f, 0.16f), new Color(0.15f, 0.15f, 0.17f));
        Object.DestroyImmediate(tape.GetComponent<NavMeshObstacle>());
        Vector3 guard = new Vector3(26.6f, 0f, -15.6f);
        EnemyBuilder.PlaceCorpse(therapy, "Body_Guard", guard, 60f, true);
        Decal(therapy, "Blood", "T_Asy_BloodPool", guard + new Vector3(0.2f, 0.012f, 0.2f), Vector3.up, new Vector2(1.8f, 1.8f), true, lit);
        Pickup card = HelenaSetupMenu.PlaceItemPickup(pickups, "Keycard", 1, new Vector3(27.5f, 0f, -15.0f), 35f, new Color(0.25f, 0.45f, 0.9f));
        if (card != null) content.Pickups.Add(card);
        Look(looks, content, "x_body", guard + Vector3.up * 0.3f, null,
            new[] { "A guard. He put up a fight... someone did this with their bare hands.",
                    "His keycard fell out of his belt. I'll need it for the armory." },
            new[] { "Um guarda. Ele lutou... alguém fez isso com as mãos.",
                    "O cartão de acesso dele caiu do cinto. Vou precisar disso pro arsenal." });
        Look(looks, content, "x_chair", new Vector3(28.4f, 0.6f, -14.2f), null,
            new[] { "A \"therapy\" chair. With straps. And electrodes. That's not therapy." },
            new[] { "Uma cadeira de \"terapia\". Com correias. E eletrodos. Isso não é terapia." });
        Look(looks, content, "x_tape", new Vector3(29.2f, deskTop + 0.05f, -11.6f), null,
            new[] { "A tape recorder. \"Session 47. The patient keeps saying she's being watched.\"",
                    "...They had me on tape. Session 47 was yesterday." },
            new[] { "Um gravador. \"Sessão 47. A paciente continua dizendo que está sendo observada.\"",
                    "...Eles me gravaram. A sessão 47 foi ontem." });
    }

    // ------------------------------------------------------------------ guard post, armory
    private static void BuildGuardPost(Transform props, Transform looks, Transform pickups, Shader lit, HelenaSetupMenu.LocationContent content)
    {
        var post = new GameObject("GuardPost").transform;
        post.SetParent(props, false);
        GameObject desk = Op(post, "SM_OpDesk", new Vector3(21.0f, 0f, -23.65f), 180f);
        float deskTop = TopOf(desk, 0.75f);
        Op(post, "SM_OperatorPC", new Vector3(21.3f, deskTop, -23.55f), 180f, false);
        Op(post, "SM_OpChair", new Vector3(21.0f, 0f, -24.5f), 200f);
        Asy(post, "SM_Asy_Cabinet", new Vector3(25.65f, 0f, -26.6f), 270f);
        SaveStation save = HelenaSetupMenu.PlaceSavePoint(post, "SavePoint_Terminal", new Vector3(23.6f, 0f, -26.6f), 0f);
        if (save != null) content.Stations.Add(save);
        Look(looks, content, "x_monitors", new Vector3(21.3f, deskTop + 0.3f, -23.55f), null,
            new[] { "The guard monitors. Static on every one... you're the only camera that works in here, huh." },
            new[] { "Os monitores da guarita. Estática em todos... você é a única câmera que funciona aqui, hein." });
        Look(looks, content, "x_desk", new Vector3(20.6f, deskTop, -23.6f), null,
            new[] { "Shift log. The last entry: \"They're not sleeping anymore. Don't open the cells.\"" },
            new[] { "Registro de turnos. A última anotação: \"Eles não estão mais dormindo. Não abram as celas.\"" });
        Look(looks, content, "x_cabinet", new Vector3(25.65f, 1.2f, -26.6f), null,
            new[] { "Guard lockers. A uniform, a photo of somebody's kids... nothing I can use." },
            new[] { "Armários dos guardas. Um uniforme, a foto dos filhos de alguém... nada que eu possa usar." });

        // the armory: a rack, a table with the pistol and a box of rounds
        var armory = new GameObject("Armory").transform;
        armory.SetParent(props, false);
        Asy(armory, "SM_Asy_Shelf", new Vector3(16.35f, 0f, -27.1f), 90f);
        GameObject table = Box(armory, "ArmoryTable", new Vector3(17.2f, 0.425f, -24.9f), new Vector3(1.4f, 0.85f, 0.6f), new Color(0.28f, 0.3f, 0.32f));
        float tableTop = TopOf(table, 0.85f);
        Pickup pistol = HelenaSetupMenu.PlaceWeaponPickup(pickups, "Pistol1911", new Vector3(16.9f, tableTop, -24.9f), 80f);
        if (pistol != null) content.Pickups.Add(pistol);
        Pickup ammo = HelenaSetupMenu.PlaceItemPickup(pickups, "HandgunAmmo", 14, new Vector3(17.6f, tableTop, -24.95f), 10f, new Color(0.85f, 0.7f, 0.2f));
        if (ammo != null) content.Pickups.Add(ammo);
        Look(looks, content, "x_shelf", new Vector3(16.35f, 1.0f, -27.1f), null,
            new[] { "Empty racks. Somebody got here first and took almost everything." },
            new[] { "Prateleiras vazias. Alguém chegou antes e levou quase tudo." });
    }

    // ------------------------------------------------------------------ service corridor, stairwell
    private static void BuildCorridor(Transform props, Transform looks, Shader lit, HelenaSetupMenu.LocationContent content)
    {
        var corridor = new GameObject("ServiceCorridor").transform;
        corridor.SetParent(props, false);
        // the body the first enemy is feeding on, the blood around it
        EnemyBuilder.PlaceCorpse(corridor, "Body_Corridor", new Vector3(46.3f, 0f, -25.6f), 300f, true);
        Decal(corridor, "Blood", "T_Asy_BloodPool", new Vector3(46.0f, 0.012f, -25.5f), Vector3.up, new Vector2(2.2f, 2.2f), true, lit);
        Decal(corridor, "Blood", "T_Asy_BloodPool", new Vector3(38.0f, 0.012f, -25.0f), Vector3.up, new Vector2(1.0f, 1.0f), true, lit);
        Box(corridor, "Crates", new Vector3(33.0f, 0.4f, -26.55f), new Vector3(1.2f, 0.8f, 0.8f), new Color(0.35f, 0.3f, 0.24f));
        Look(looks, content, "x_body", new Vector3(46.3f, 0.3f, -25.6f), null,
            new[] { "...I'm not looking at that any longer than I have to." },
            new[] { "...Eu não vou olhar pra isso mais do que o necessário." });

        // stairwell: steps up (blockout), the way to the rest of the asylum
        var stairs = new GameObject("Stairwell").transform;
        stairs.SetParent(props, false);
        for (int i = 0; i < 8; i++)
            Box(stairs, "Step", new Vector3(50.9f, (i + 1) * 0.1f, -26.5f + i * 0.28f), new Vector3(1.8f, (i + 1) * 0.2f, 0.28f), new Color(0.4f, 0.4f, 0.42f));
        Look(looks, content, "x_stairs", new Vector3(50.9f, 0.8f, -25.5f), null,
            new[] { "The stairs. The rest of the asylum is up there." },
            new[] { "A escada. O resto do hospício é lá em cima." });
    }

    // ================================================================== cutscenes (once the rig exists)
    private static void Finish(HelenaSetupMenu.LocationRig rig, Refs refs)
    {
        foreach (LanguageVariant v in refs.Variants) HelenaSetupMenu.SetReference(v, LanguageVariant.SettingsField, rig.Settings);
        CutsceneDirector d = rig.Director;
        if (d == null) { Debug.LogWarning("[Asylum] No cutscene director: no cutscenes."); return; }
        CutsceneActor h = d.Helena;
        var root = new GameObject("Cutscenes").transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, rig.Scene);

        AnimationClip lying = CastClip("Cast_BedLying"), sitUp = CastClip("Cast_BedSitUp"), sitting = CastClip("Cast_BedSit");
        SecurityCamera Cam(string room) => refs.Cameras.TryGetValue(room, out SecurityCamera c) ? c : null;
        Door DoorOf(string name) => refs.Doors.TryGetValue(name, out Door door) ? door : null;

        // 1. she wakes up on the bed of her cell
        {
            var steps = New(root, d, "Cutscene_Wake", "asy_wake", CutsceneTrigger.When.SceneStart, null, "", 0f, out Cutscene c);
            Add(steps, new FadeStep { toBlack = true, duration = 0f });
            Add(steps, new CameraCutStep { camera = Cam("Cell 5") });
            Add(steps, new TeleportStep { actor = h, target = refs.StartMark });
            if (lying != null) Add(steps, new ClipStep { actor = h, clip = lying, blend = 0f, waitEnd = false, backToNormal = false });
            Add(steps, new WaitStep { seconds = 1.2f });
            Add(steps, new FadeStep { toBlack = false, duration = 3f });
            Add(steps, new WaitStep { seconds = 1.5f });
            Add(steps, new OperatorLineStep { textPt = "Helena, tá me ouvindo?", textEn = "Helena, can you hear me?" });
            Add(steps, new WaitStep { seconds = 0.8f });
            if (sitUp != null) Add(steps, new ClipStep { actor = h, clip = sitUp, blend = 0.4f, waitEnd = true, backToNormal = false });
            if (sitting != null) Add(steps, new ClipStep { actor = h, clip = sitting, blend = 0.3f, waitEnd = false, backToNormal = false });
            Add(steps, Line(h, "...Você demorou.", "...You took your time."));
            Add(steps, Line(h, "Três dias fingindo que eu sou louca. Remédio, camisa de força, gente gritando a noite inteira.",
                               "Three days pretending I'm crazy. Pills, straitjackets, people screaming all night."));
            Add(steps, Line(h, "Me tiraram tudo. Arma, rádio, roupa... só sobrou o ponto no ouvido. E você.",
                               "They took everything. Gun, radio, clothes... all I've got left is the earpiece. And you."));
            Add(steps, Line(h, "Ontem à noite as luzes apagaram. Teve gritaria... e depois, silêncio. Silêncio demais.",
                               "Last night the lights went out. There was screaming... and then silence. Too much silence."));
            Add(steps, new OperatorLineStep { textPt = "Vamos te tirar daí.", textEn = "Let's get you out of there.", acceptAnything = true });
            Add(steps, Line(h, "É o que eu mais quero ouvir.", "Best thing I've heard all week."));
            Add(steps, new StateStep { actor = h, state = PartnerAnimator.StandUpState, blend = 0.5f, wait = 2.2f });
            Add(steps, new StopAnimationStep { actor = h, blend = 0.4f });
            Add(steps, new WaitStep { seconds = 0.4f });
            Add(steps, new FaceStep { actor = h, lookAt = refs.WritingMark, turnSpeed = 160f });
            Add(steps, Line(h, "...E alguém escreveu na parede. Com sangue.", "...And someone wrote on the wall. In blood."));
            Add(steps, Line(h, "\"A senha está na soma.\" Soma do quê?", "\"The code is in the sum.\" The sum of what?"));
            Add(steps, Line(h, "A porta tem um teclado. Me diz o que digitar.", "The door has a keypad. Tell me what to type."));
            Add(steps, new ThoughtStep { textPt = "Ela pode olhar as coisas: \"olha a parede\", \"olha a cama\". E se olhar pra câmera, eu vejo ela de frente: \"olha pra mim\".",
                                         textEn = "She can look at things: \"look at the wall\", \"look at the bed\". And facing the camera, I see her up front: \"look at me\"." });
            Add(steps, new SetFlagStep { flag = "asy_awake" });
            Commit(c);
        }

        // 2. hints while the door stays shut (no bars, she keeps taking orders)
        {
            var steps = New(root, d, "Cutscene_Hint1", "asy_hint1", CutsceneTrigger.When.FlagSet, null, "asy_awake", 60f, out Cutscene c,
                            blocking: StoryProgress.KeypadSolved, letterbox: false);
            Add(steps, new ThoughtStep { textPt = "A soma... de números que ela não consegue ver. A roupa dela, talvez? Eu vejo pela câmera.",
                                         textEn = "The sum... of numbers she can't see. Her clothes, maybe? I can see them through the camera." });
            Commit(c);
            steps = New(root, d, "Cutscene_Hint2", "asy_hint2", CutsceneTrigger.When.FlagSet, null, "asy_awake", 150f, out c,
                        blocking: StoryProgress.KeypadSolved, letterbox: false);
            Add(steps, Line(h, "Não tem número nenhum nessa cela... a não ser essa etiqueta idiota que colaram em mim.",
                               "There's no number anywhere in this cell... except this stupid tag they stuck on me."));
            Commit(c);
        }

        // 3. the door opens
        {
            var steps = New(root, d, "Cutscene_CellOpen", "asy_cell_open", CutsceneTrigger.When.FlagSet, null, StoryProgress.KeypadSolved, 0.6f, out Cutscene c,
                            letterbox: false);
            Add(steps, Line(h, "Abriu! ...A soma. Claro. Eu nunca fui boa de conta.", "It's open! ...The sum. Of course. I was never good at math."));
            Add(steps, Line(h, "Tá. Fica comigo.", "Okay. Stay with me."));
            Commit(c);
        }

        // 4. the wing
        {
            var steps = New(root, d, "Cutscene_Wing", "asy_wing", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Isolation Wing"], "", 0.3f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Isolation Wing") });
            Add(steps, Line(h, "O corredor das celas... silencioso demais.", "The cell block... way too quiet."));
            Add(steps, Line(h, "Ei! Tem alguém aí? ...Ninguém responde.", "Hey! Anyone in there? ...Nobody answers."));
            Commit(c);
        }

        // 5. the nurses station: the argument, the tutorial, the knife
        {
            var steps = New(root, d, "Cutscene_Nurses", "asy_nurses", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Nurses Station"], "", 0.5f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Nurses Station") });
            Add(steps, Line(h, "Enfermaria. Vazia.", "Nurses station. Empty."));
            Add(steps, Line(h, "Agora me explica uma coisa. \"Três dias. Entra como paciente, acha o arquivo, sai.\" Lembra? Foi o que você me disse.",
                               "Now explain something to me. \"Three days. Go in as a patient, find the file, get out.\" Remember? Your words."));
            Add(steps, Line(h, "Três dias viraram isso. Cadê o resgate? Cadê o plano B?", "Three days turned into this. Where's the extraction? Where's plan B?"));
            Add(steps, new OperatorLineStep { textPt = "Eu não sabia que isso ia acontecer.", textEn = "I didn't know this would happen.", acceptAnything = true });
            Add(steps, Line(h, "Você nunca sabe. Esse é o problema.", "You never know. That's the problem."));
            Add(steps, new WaitStep { seconds = 1.2f });
            Add(steps, Line(h, "...Tá. Brigar não vai me tirar daqui.", "...Fine. Fighting won't get me out of here."));
            Add(steps, Line(h, "O mapa na parede: a saída é pela escada, depois da guarita. E do lado da guarita tem o arsenal.",
                               "The map on the wall: the way out is the stairwell, past the guard post. And the armory is right next to it."));
            Add(steps, Line(h, "Até lá... aquela faca na maca serve.", "Until then... that knife on the gurney will do."));
            Add(steps, new ThoughtStep { textPt = "Itens: \"pega a faca\". Armas: \"saca a faca\", \"guarda a arma\". Ela só usa o que tiver pegado.",
                                         textEn = "Items: \"pick up the knife\". Weapons: \"draw the knife\", \"put it away\". She only uses what she has picked up." });
            Add(steps, new ThoughtStep { textPt = "O mapa das câmeras (Tab) mostra as salas. As portas de segurança, quem abre sou eu, por ele.",
                                         textEn = "The camera map (Tab) shows the rooms. The security doors are mine to open, from there." });
            Commit(c);
        }

        // 6. the therapy room: the guard, the keycard
        {
            var steps = New(root, d, "Cutscene_Therapy", "asy_therapy", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Therapy Room"], "", 0.3f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Therapy Room") });
            Add(steps, Line(h, "Um guarda... morto.", "A guard... dead."));
            Add(steps, Line(h, "O cartão de acesso dele tá no chão. É disso que eu preciso pro arsenal.", "His keycard's on the floor. That's what I need for the armory."));
            Commit(c);
        }

        // 7. the guard post: the save terminal, the armory door
        {
            var steps = New(root, d, "Cutscene_GuardPost", "asy_guard", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Guard Post"], "", 0.3f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Guard Post") });
            Add(steps, Line(h, "A guarita. Esse terminal ainda liga... dá pra mandar um relatório.", "The guard post. This terminal still works... I can send a report."));
            Add(steps, new ThoughtStep { textPt = "Terminal: \"salva\". Aqui ela tá segura.", textEn = "Terminal: \"save\". She's safe in here." });
            Add(steps, Line(h, "E aquela porta é o arsenal. Trancada com cartão.", "And that door's the armory. Keycard lock."));
            Commit(c);
        }

        // 8. the armory: the pistol; the door to the service corridor unlocks
        {
            var steps = New(root, d, "Cutscene_Armory", "asy_armory", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Armory"], "", 0.3f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Armory") });
            Add(steps, Line(h, "Saquearam quase tudo... mas sobrou uma.", "They took almost everything... but they missed one."));
            Add(steps, Line(h, "Uma 1911. Velha, mas atira.", "A 1911. Old, but it shoots."));
            if (DoorOf("Service Corridor") != null) Add(steps, new DoorStep { door = DoorOf("Service Corridor"), action = DoorStep.DoorAction.Unlock });
            Add(steps, Line(h, "Ouviu isso? Destravou alguma porta lá na guarita.", "Did you hear that? A door unlocked back in the guard post."));
            Add(steps, new ThoughtStep { textPt = "\"pega a pistola\", \"pega a munição\".", textEn = "\"pick up the pistol\", \"pick up the ammo\"." });
            Commit(c);
        }

        // 9. the service corridor: the first enemy (the combat tutorial)
        {
            var steps = New(root, d, "Cutscene_Corridor", "asy_corridor", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Service Corridor"], "", 0.2f, out Cutscene c);
            SecurityCamera far = rig.CameraSystem != null ? rig.CameraSystem.Cameras.FirstOrDefault(x => x != null && x.Room == refs.Rooms["Service Corridor"] && x.transform.position.x > 40f) : null;
            Add(steps, new CameraCutStep { camera = far != null ? far : Cam("Service Corridor") });
            Add(steps, Line(h, "Tem alguém no fim do corredor...", "There's someone at the end of the corridor..."));
            Add(steps, Line(h, "Ei! Você tá bem?", "Hey! Are you okay?"));
            Add(steps, new WaitStep { seconds = 1.5f });
            Add(steps, Line(h, "...Ele tá comendo. Ele tá COMENDO alguém.", "...He's eating. He's EATING someone."));
            Add(steps, new ThoughtStep { textPt = "Combate: \"saca a pistola\", \"atira\". Se chegar perto: \"desvia\", \"recua\". \"Recarrega\" quando acabar.",
                                         textEn = "Combat: \"draw the pistol\", \"shoot\". If it gets close: \"dodge\", \"back off\". \"Reload\" when it's empty." });
            Commit(c);
        }

        // 10. the first kill: the stairwell door opens
        {
            var steps = New(root, d, "Cutscene_FirstKill", "asy_first_kill", CutsceneTrigger.When.FlagSet, null, StoryProgress.FirstKill, 1.2f, out Cutscene c);
            Add(steps, Line(h, "...Morreu. De novo, eu acho.", "...It's dead. Again, I guess."));
            Add(steps, Line(h, "Isso não era um paciente. Não mais.", "That wasn't a patient. Not anymore."));
            if (DoorOf("Stairwell") != null) Add(steps, new DoorStep { door = DoorOf("Stairwell"), action = DoorStep.DoorAction.UnlockAndOpen });
            Add(steps, Line(h, "A porta da escada abriu. Vamos sair daqui.", "The stairwell door's open. Let's get out of here."));
            Commit(c);
        }

        // 11. the stairwell: the end of this area
        {
            var steps = New(root, d, "Cutscene_Stairwell", "asy_stairwell", CutsceneTrigger.When.HelenaEntersRoom, refs.Rooms["Stairwell"], "", 0.5f, out Cutscene c);
            Add(steps, new CameraCutStep { camera = Cam("Stairwell") });
            Add(steps, Line(h, "A escada. O resto do hospício é lá em cima.", "The stairs. The rest of the asylum is up there."));
            Add(steps, Line(h, "Não some, operador.", "Don't disappear on me, operator."));
            Add(steps, new FadeStep { toBlack = true, duration = 2f });
            Add(steps, new ThoughtStep { textPt = "Fim da Ala de Isolamento.", textEn = "End of the Isolation Wing." });
            Add(steps, new FadeStep { toBlack = false, duration = 1.5f });
            Commit(c);
        }
        Debug.Log("[Asylum] 12 cutscenes built under 'Cutscenes' (edit their steps freely).");
    }

    // a cutscene + its trigger; returns its steps list (Commit applies it)
    private static readonly Dictionary<Cutscene, SerializedObject> Pending = new Dictionary<Cutscene, SerializedObject>();

    private static SerializedProperty New(Transform root, CutsceneDirector d, string name, string id, CutsceneTrigger.When when, Room room,
                                          string flag, float delay, out Cutscene cutscene, string blocking = "", bool letterbox = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        if (room != null) go.transform.position = room.transform.position;
        cutscene = go.AddComponent<Cutscene>();
        HelenaSetupMenu.SetReference(cutscene, Cutscene.DirectorField, d);
        HelenaSetupMenu.SetString(cutscene, Cutscene.IdField, id);
        var trigger = go.AddComponent<CutsceneTrigger>();
        HelenaSetupMenu.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
        var tso = new SerializedObject(trigger);
        tso.FindProperty(CutsceneTrigger.WhenField).enumValueIndex = (int)when;
        tso.FindProperty(CutsceneTrigger.RoomField).objectReferenceValue = room;
        tso.FindProperty("flag").stringValue = flag;
        tso.FindProperty("blockingFlag").stringValue = blocking;
        tso.FindProperty("delay").floatValue = delay;
        tso.ApplyModifiedPropertiesWithoutUndo();
        var so = new SerializedObject(cutscene);
        so.FindProperty("letterbox").boolValue = letterbox;
        Pending[cutscene] = so;
        return so.FindProperty(Cutscene.StepsField);
    }

    private static void Commit(Cutscene c)
    {
        if (!Pending.TryGetValue(c, out SerializedObject so)) return;
        so.ApplyModifiedPropertiesWithoutUndo();
        Pending.Remove(c);
    }

    private static void Add(SerializedProperty list, CutsceneStep step)
    {
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = step;
    }

    private static SayStep Line(CutsceneActor who, string pt, string en) => new SayStep { actor = who, textPt = pt, textEn = en, pauseAfter = 0.35f };

    // ================================================================== lexicon (CommandDataBuilder)
    // the rooms of the asylum and the things she looks at
    internal static IEnumerable<(string key, string[] en, string[] pt)> LexiconNames()
    {
        yield return ("Isolation Wing", new[] { "isolation wing", "wing", "cell block", "the wing" }, new[] { "ala de isolamento", "ala", "corredor das celas", "bloco das celas" });
        yield return ("Observation", new[] { "observation", "observation room", "monitoring room" }, new[] { "observacao", "sala de observacao", "sala de monitoramento" });
        yield return ("Nurses Station", new[] { "nurses station", "nurse station", "nurses", "infirmary", "nursing station" }, new[] { "enfermaria", "posto de enfermagem", "enfermagem" });
        yield return ("Service Hall", new[] { "service hall", "inner hallway", "inner hall" }, new[] { "corredor interno", "corredor da enfermaria" });
        yield return ("Linen Room", new[] { "linen room", "linen", "laundry" }, new[] { "rouparia", "lavanderia", "sala de roupas" });
        yield return ("Bathroom", new[] { "bathroom", "restroom", "toilets", "showers" }, new[] { "banheiro", "banheiros", "sanitario", "chuveiros" });
        yield return ("Therapy Room", new[] { "therapy room", "therapy", "treatment room" }, new[] { "sala de terapia", "terapia", "sala de tratamento" });
        yield return ("Guard Post", new[] { "guard post", "guard room", "security post", "guard station" }, new[] { "guarita", "sala dos guardas", "posto de guarda" });
        yield return ("Armory", new[] { "armory", "armoury", "weapons room", "gun room" }, new[] { "arsenal", "sala de armas", "armaria" });
        yield return ("Service Corridor", new[] { "service corridor", "long corridor", "back corridor" }, new[] { "corredor de servico", "corredor longo", "corredor comprido" });
        yield return ("Stairwell", new[] { "stairwell", "stairs", "staircase", "exit" }, new[] { "escada", "escadas", "escadaria", "saida" });
        string[] en = { "one", "two", "three", "four", "five", "six" };
        string[] pt = { "um", "dois", "tres", "quatro", "cinco", "seis" };
        for (int i = 1; i <= 6; i++)
            yield return ($"Cell {i}", new[] { $"cell {i}", $"cell {en[i - 1]}" }, new[] { $"cela {i}", $"cela {pt[i - 1]}" });
        // things to look at (Examinable keys)
        yield return ("x_bed", new[] { "bed", "cot", "mattress" }, new[] { "cama", "colchao", "leito" });
        yield return ("x_writing", new[] { "writing", "wall", "blood", "message", "words", "writing on the wall" }, new[] { "parede", "escrita", "sangue", "mensagem", "escrito", "frase" });
        yield return ("x_body", new[] { "body", "corpse", "dead body", "dead man", "guard", "bodies", "dead guy on the floor" }, new[] { "corpo", "cadaver", "defunto", "morto", "guarda morto", "corpo do guarda", "corpos" });
        yield return ("x_toilet", new[] { "toilet", "loo" }, new[] { "privada", "vaso", "vaso sanitario", "latrina" });
        yield return ("x_forced", new[] { "forced door", "broken door", "open door", "open cell" }, new[] { "porta arrombada", "porta quebrada", "porta aberta", "cela aberta" });
        yield return ("x_blood", new[] { "blood trail", "trail", "blood on the floor", "stain" }, new[] { "rastro", "rastro de sangue", "sangue no chao", "mancha" });
        yield return ("x_monitors", new[] { "monitors", "monitor", "screens", "screen", "tv" }, new[] { "monitores", "monitor", "telas", "tela", "televisao" });
        yield return ("x_chair", new[] { "chair" }, new[] { "cadeira" });
        yield return ("x_desk", new[] { "desk", "files", "records", "logbook", "papers", "clipboard" }, new[] { "mesa", "prontuarios", "prontuario", "fichas", "registro", "papeis" });
        yield return ("x_map", new[] { "map", "poster", "floor plan" }, new[] { "mapa", "cartaz", "planta" });
        yield return ("x_cabinet", new[] { "cabinet", "medicine cabinet", "locker", "closet" }, new[] { "armario", "armario de remedios" });
        yield return ("x_gurney", new[] { "gurney", "stretcher", "trolley" }, new[] { "maca", "carrinho" });
        yield return ("x_shelf", new[] { "shelf", "shelves", "rack", "racks", "sheets" }, new[] { "prateleira", "prateleiras", "estante", "lencois" });
        yield return ("x_sink", new[] { "sink", "faucet", "tap" }, new[] { "pia", "torneira" });
        yield return ("x_tape", new[] { "tape recorder", "recorder", "tape", "cassette" }, new[] { "gravador", "fita", "fita cassete" });
        yield return ("x_stairs", new[] { "steps" }, new[] { "degraus", "degrau" });
    }
}
