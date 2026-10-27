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
    // Scene, NavMesh and art paths; the cast clips and Helena's idle model.
    private const string ScenePath = "Assets/Scenes/Asylum_IsolationWing.unity";
    private const string NavMeshPath = "Assets/Scenes/Asylum_IsolationWing_NavMesh.asset";
    private const string ArtFolder = "Assets/Art/Props/Asylum";
    private const string CastAnimFolder = "Assets/Characters/Cast/Animations";
    private const string HelenaIdlePath = "Assets/Characters/Helena/Animations/HelenaIdle.fbx";
    private const string CellCode = "2819";   // 2716 + 103: the tag on her chest (OutfitBuilder, outfit_patient)

    // her bed (cell 5): she sits on its edge 0.30 m in front of her root (Cast_Bed* clips), its front 0.35 m behind it
    private static readonly Vector3 Start = new Vector3(7.0f, 0f, -9.62f);
    private const float BedDepth = 0.85f;

    // CCTV camera height (short name: it is in every camera position below).
    private const float H = FacilityBuilder.CameraHeight;

    // One room of the wing.
    private struct RoomDef
    {
        // Names per language and the floor area.
        public string En, Pt;
        public Rect Area;
        public string Floor;      // PaddedFloor (a cell: padded walls too), Lino, Tiles, Concrete
        // Lamp color and intensity; padded cell?
        public Color Light;
        public float Intensity;
        public bool Padded => Floor == PaddedFloor;
        public RoomDef(string en, string pt, float x0, float z0, float x1, float z1, string floor, Color light, float intensity)
        {
            En = en; Pt = pt; Area = new Rect(x0, z0, x1 - x0, z1 - z0); Floor = floor; Light = light; Intensity = intensity;
        }
    }

    // Lamp colors.
    private static readonly Color Cold = new Color(0.78f, 0.88f, 1f);
    private static readonly Color Sick = new Color(0.85f, 1f, 0.8f);
    private static readonly Color Warm = new Color(1f, 0.85f, 0.65f);
    private static readonly Color Red = new Color(1f, 0.45f, 0.4f);

    private const float C1 = 4.67f, C2 = 9.33f;   // the walls between the cells
    // Floor textures.
    private const string PaddedFloor = "T_AsyPaddedFloor", Lino = "T_AsyFloor", Tiles = "T_AsyTiles", Concrete = "T_AsyConcrete";

    // Every room of the wing.
    private static readonly RoomDef[] Rooms =
    {
        new RoomDef("Cell 1", "Cela 1", 0f, -4f, C1, 0f, PaddedFloor, Cold, 0.8f),
        new RoomDef("Cell 2", "Cela 2", C1, -4f, C2, 0f, PaddedFloor, Cold, 0.8f),
        new RoomDef("Cell 3", "Cela 3", C2, -4f, 14f, 0f, PaddedFloor, Cold, 1.0f),
        new RoomDef("Observation", "Observação", 14f, -4f, 18f, 0f, Lino, Sick, 1.2f),
        new RoomDef("Isolation Wing", "Ala de Isolamento", 0f, -7f, 18f, -4f, Lino, Sick, 1.6f),
        new RoomDef("Cell 4", "Cela 4", 0f, -11f, C1, -7f, PaddedFloor, Cold, 0.8f),
        new RoomDef("Cell 5", "Cela 5", C1, -11f, C2, -7f, PaddedFloor, Cold, 1.3f),
        new RoomDef("Cell 6", "Cela 6", C2, -11f, 14f, -7f, PaddedFloor, Cold, 0.8f),
        new RoomDef("Nurses Station", "Enfermaria", 18f, -9f, 26f, -2f, Lino, Warm, 2.0f),
        new RoomDef("Service Hall", "Corredor Interno", 22f, -23f, 25f, -9f, Lino, Sick, 1.5f),
        new RoomDef("Linen Room", "Rouparia", 17f, -15f, 22f, -11f, Lino, Warm, 1.2f),
        new RoomDef("Bathroom", "Banheiro", 16f, -21f, 22f, -16f, Tiles, Sick, 1.2f),
        new RoomDef("Therapy Room", "Sala de Terapia", 25f, -17f, 31f, -11f, Lino, Cold, 1.4f),
        new RoomDef("Guard Post", "Guarita", 19f, -28f, 26f, -23f, Concrete, Warm, 1.8f),
        new RoomDef("Armory", "Arsenal", 16f, -28f, 19f, -24f, Concrete, Warm, 1.2f),
        new RoomDef("Service Corridor", "Corredor de Serviço", 26f, -27f, 48f, -24f, Concrete, Red, 1.3f),
        new RoomDef("Stairwell", "Escada", 48f, -28f, 52f, -23f, Concrete, Sick, 1.2f),
    };

    // One door of the wing.
    private struct DoorDef
    {
        public string Name;          // the lexicon key it answers to (the room it leads to)
        // Position (yaw below), and its kind / state.
        public Vector3 Position;
        public float Yaw;            // 0: in a wall along x, 90: in a wall along z
        public bool Security, Open, Locked, Remote;
        public DoorDef(string name, float x, float z, float yaw, bool security, bool open = false, bool locked = false, bool remote = false)
        {
            Name = name; Position = new Vector3(x, 0f, z); Yaw = yaw; Security = security; Open = open; Locked = locked; Remote = remote;
        }
    }

    private const float D1 = C1 / 2f, D2 = (C1 + C2) / 2f, D3 = (C2 + 14f) / 2f;   // cell door centers

    // Every door of the wing.
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
        Shader lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;
        if (!AssetDatabase.IsValidFolder(ArtFolder))
        {
            Debug.LogError($"[Asylum] {ArtFolder} not found: extract the package at the project root (the folder with Assets).");
            return;
        }
        OperatorRoomBuilder.RemapFolder(ArtFolder, lit);
        EnsureCastAvatar();
        LocationRigBuilder.BuildLocation(ScenePath, NavMeshPath, BuildContent);
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

    // A cast clip by name.
    private static AnimationClip CastClip(string name)
    {
        AnimationClip c = AssetDatabase.LoadAllAssetsAtPath($"{CastAnimFolder}/{name}.fbx").OfType<AnimationClip>()
            .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        if (c == null) Debug.LogWarning($"[Asylum] {CastAnimFolder}/{name}.fbx missing: that part of the cutscene is skipped.");
        return c;
    }

    // a surface material of the asylum (its texture in Art/Props/Asylum)
    private static Material Env(string texture, float smooth) =>
        FacilityBuilder.MaterialFor($"{ArtFolder}/Materials/M_{texture.Substring(2)}.mat", $"{ArtFolder}/{texture}.png", smooth, EditorAssets.DefaultLitShader(), ArtFolder);

    // what the cutscenes need from the content (filled while building it)
    private class Refs
    {
        // Rooms, doors, cameras and language variants by name, the marks root and the start / writing marks.
        public readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        public readonly Dictionary<string, Door> Doors = new Dictionary<string, Door>();
        public readonly Dictionary<string, SecurityCamera> Cameras = new Dictionary<string, SecurityCamera>();
        public readonly List<LanguageVariant> Variants = new List<LanguageVariant>();
        public Transform Marks;
        public Transform StartMark, WritingMark;
    }

    // ================================================================== content
    private static LocationRigBuilder.LocationContent BuildContent(Scene scene, Shader lit)
    {
        var content = new LocationRigBuilder.LocationContent
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

        // the asylum's own surfaces (Art/Props/Asylum): two-tone hospital wall, padding, the floors
        Material wall = Env("T_AsyWall", 0.08f);
        Material padded = Env("T_AsyPadded", 0.04f);
        var floors = new Dictionary<string, Material>
        {
            { PaddedFloor, Env(PaddedFloor, 0.04f) }, { Lino, Env(Lino, 0.35f) }, { Tiles, Env(Tiles, 0.45f) }, { Concrete, Env(Concrete, 0.12f) },
        };
        EnsureSparkleAssets();
        sparkles.Clear();

        // rooms + floors + lights
        var rooms = new GameObject("Rooms").transform;
        rooms.SetParent(root.transform, false);
        foreach (RoomDef r in Rooms)
        {
            Room room = FacilityBuilder.AddRoom(rooms, geometry, floors[r.Floor], r.En, r.Pt, r.Area, r.Light, r.Intensity);
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
        Dictionary<string, ItemDefinition> items = ItemBuilder.TestItems();
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
        content.Enemies.Add(new LocationRigBuilder.EnemySpec
        {
            Prefab = "Enemy_Zombie", Position = new Vector3(45.4f, 0f, -25.5f), Yaw = 90f, Mode = EnemyStartMode.Feeding, SaveId = "asy_corridor_feeder",
        });

        // closed rooms lit by their ceiling lamps (some failing), fog, the color grade
        AtmosphereBuilder.Apply(root.transform, level.Rooms, lit, AtmosphereBuilder.Asylum);

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

    // Adds a wall edge span to the outline lines.
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

    // Merges overlapping spans.
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
    // A model (FBX) of Art/Props/Asylum: its "Mesh" is what shows; its "Col_n" boxes are the collision (box colliders,
    // NavMesh carving when solid), their renderers removed. Models without Col boxes are decor (no collision).
    private static GameObject Prop(Transform parent, string model, Vector3 pos, float yaw, bool solid = true, Vector3? tilt = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{model}.fbx");
        if (asset == null) { Debug.LogWarning($"[Asylum] {ArtFolder}/{model}.fbx missing: not placed."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = model.Replace("SM_Asy_", "");
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt?.x ?? 0f, yaw, tilt?.z ?? 0f));
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (!mf.name.StartsWith("Col_") || mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            GameObject c = mf.gameObject;
            Object.DestroyImmediate(c.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(mf);
            var box = c.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            if (solid) Carve(c, b.center, b.size);
        }
        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Cuts a NavMesh hole around a prop (obstacle carve).
    private static void Carve(GameObject go, Vector3 center, Vector3 size)
    {
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = center;
        obstacle.size = size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // a flat picture (blood, stains, a poster, a sign): a quad seen from `facing`
    private static GameObject Decal(Transform parent, string name, string texture, Vector3 pos, Vector3 facing, Vector2 size, bool clip, Shader lit,
                                    float spin = 0f)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{texture}.png");
        if (tex == null) { Debug.LogWarning($"[Asylum] {ArtFolder}/{texture}.png missing."); return null; }
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        Material m = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_{texture.Substring(2)}.mat", tex, 0.3f, clip, lit);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        Vector3 up = Mathf.Abs(Vector3.Dot(facing.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing, up) * Quaternion.Euler(0f, 0f, spin));   // a quad shows -z
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // a decal on the inside face of one of a room's walls: side N / S / E / W, `along` = world x (N, S) or z (E, W)
    private static int decalCount;
    private static GameObject WallDecal(Transform parent, string room, char side, float along, float y, Vector2 size, string texture, Shader lit)
    {
        RoomDef def = Rooms.First(r => r.En == room);
        Rect a = def.Area;
        float inner = FacilityBuilder.WallThickness * 0.5f + (def.Floor == PaddedFloor ? FacilityBuilder.PaddingThickness : 0f) + 0.006f + 0.001f * (decalCount++ % 5);
        Vector3 pos, facing;
        switch (side)
        {
            case 'N': pos = new Vector3(along, y, a.yMax - inner); facing = Vector3.back; break;
            case 'S': pos = new Vector3(along, y, a.yMin + inner); facing = Vector3.forward; break;
            case 'E': pos = new Vector3(a.xMax - inner, y, along); facing = Vector3.left; break;
            default: pos = new Vector3(a.xMin + inner, y, along); facing = Vector3.right; break;
        }
        return Decal(parent, "Decal_" + texture.Replace("T_Asy_", ""), texture, pos, facing, size, true, lit);
    }

    // A floor decal (blood, stains).
    private static GameObject FloorDecal(Transform parent, string texture, float x, float z, float size, Shader lit, float spin = 0f) =>
        Decal(parent, "Floor_" + texture.Replace("T_Asy_", ""), texture, new Vector3(x, 0.011f + 0.0006f * (decalCount++ % 8), z), Vector3.up,
              new Vector2(size, size), true, lit, spin);

    // water stains, grime and cracks on the walls / floor of a room (seeded: the same every build), away from its doors
    private static void Weather(Transform parent, string room, int seed, Shader lit, int wallStains = 3, int floorStains = 2, bool cracks = true)
    {
        var rnd = new System.Random(seed);
        RoomDef def = Rooms.First(r => r.En == room);
        Rect a = def.Area;
        char[] sides = { 'N', 'S', 'E', 'W' };
        int placed = 0, tries = 0;
        while (placed < wallStains && tries++ < 40)
        {
            char side = sides[rnd.Next(4)];
            bool alongX = side == 'N' || side == 'S';
            float min = (alongX ? a.xMin : a.yMin) + 0.8f, max = (alongX ? a.xMax : a.yMax) - 0.8f;
            if (max <= min) continue;
            float along = min + (float)rnd.NextDouble() * (max - min);
            if (NearDoor(a, side, along)) continue;
            bool top = rnd.NextDouble() < 0.6;
            string tex = top ? "T_Asy_StainWater" : "T_Asy_StainGrime";
            float s = 0.9f + (float)rnd.NextDouble() * 0.9f;
            WallDecal(parent, room, side, along, top ? 2.4f : 0.4f, new Vector2(s, s), tex, lit);
            if (cracks && placed == 0) WallDecal(parent, room, side, along + (alongX ? 0.4f : -0.4f), 1.4f, new Vector2(0.8f, 0.8f), "T_Asy_Crack", lit);
            placed++;
        }
        for (int i = 0; i < floorStains; i++)
        {
            float x = a.xMin + 0.6f + (float)rnd.NextDouble() * (a.width - 1.2f);
            float z = a.yMin + 0.6f + (float)rnd.NextDouble() * (a.height - 1.2f);
            FloorDecal(parent, "T_Asy_StainGrime", x, z, 1.2f + (float)rnd.NextDouble(), lit, (float)rnd.NextDouble() * 360f);
        }
    }

    // Is this point of a wall near a door?
    private static bool NearDoor(Rect a, char side, float along)
    {
        float line = side == 'N' ? a.yMax : side == 'S' ? a.yMin : side == 'E' ? a.xMax : a.xMin;
        bool alongX = side == 'N' || side == 'S';
        foreach (DoorDef d in Doors)
        {
            bool doorAlongX = Mathf.Approximately(d.Yaw, 0f);
            if (doorAlongX != alongX) continue;
            float onLine = alongX ? d.Position.z : d.Position.x;
            float at = alongX ? d.Position.x : d.Position.z;
            if (Mathf.Abs(onLine - line) < 0.05f && Mathf.Abs(at - along) < 1.4f) return true;
        }
        return false;
    }

    // something she can look at (with its sparkle); stand = where she stands for it (null: in front of it, on her side)
    private static Examinable Look(Transform parent, LocationRigBuilder.LocationContent content, string key, Vector3 pos, Vector3? stand,
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
            EditorSerialized.SetReference(x, Examinable.StandPointField, s);
        }
        content.Examinables.Add(x);
        Sparkle(go.transform, Vector3.up * 0.12f, x);
        return x;
    }

    // Registers a pickup in the location content.
    private static Pickup Take(LocationRigBuilder.LocationContent content, Pickup p)
    {
        if (p == null) return null;
        content.Pickups.Add(p);
        Sparkle(p.transform, Vector3.up * 0.15f, null);
        return p;
    }

    // the twinkling white star (InteractSparkle): a child of the thing; its camera is wired once the rig exists
    private static Sprite sparkleSprite;
    private static Material sparkleMaterial;
    private static readonly List<InteractSparkle> sparkles = new List<InteractSparkle>();

    // The sparkle material / mesh (made once).
    private static void EnsureSparkleAssets()
    {
        string path = $"{ArtFolder}/T_Asy_Sparkle.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
            (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 64f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        sparkleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sparkleSprite == null) Debug.LogWarning($"[Asylum] {path} missing: no sparkles.");
        Shader sprites = Shader.Find("Sprites/Default");   // editor time: saved in the material asset
        if (sprites == null) return;
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        string matPath = $"{ArtFolder}/Materials/M_Asy_Sparkle.mat";
        sparkleMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (sparkleMaterial == null)
        {
            sparkleMaterial = new Material(sprites);
            AssetDatabase.CreateAsset(sparkleMaterial, matPath);
        }
        sparkleMaterial.shader = sprites;
        sparkleMaterial.renderQueue = 3100;   // over the decals
        EditorUtility.SetDirty(sparkleMaterial);
    }

    // Adds a sparkle over something to examine.
    private static void Sparkle(Transform parent, Vector3 offset, Examinable examinable)
    {
        if (sparkleSprite == null) return;
        var root = new GameObject("Sparkle");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = offset;
        var stars = new List<Transform>();
        foreach (Vector3 at in new[] { Vector3.zero, new Vector3(0.08f, 0.06f, 0f) })
        {
            var star = new GameObject("Star").transform;
            star.SetParent(root.transform, false);
            star.localPosition = at;
            var sr = star.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sparkleSprite;
            if (sparkleMaterial != null) sr.sharedMaterial = sparkleMaterial;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            stars.Add(star);
        }
        var sparkle = root.AddComponent<InteractSparkle>();
        EditorSerialized.SetReferenceArray(sparkle, InteractSparkle.StarsField, stars);
        if (examinable != null) EditorSerialized.SetReference(sparkle, InteractSparkle.ExaminableField, examinable);
        sparkles.Add(sparkle);
    }

    // A named mark (position + facing) for the cutscenes.
    private static Transform Mark(Refs refs, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(refs.Marks, false);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    // A group object under the props root.
    private static Transform Group(Transform props, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(props, false);
        return t;
    }

    // ------------------------------------------------------------------ the cells
    private const float BedTop = 0.5f, DeskTop = 0.8f, TableTop = 0.8f, GurneyTop = 0.85f, CartTop = 0.935f;

    // The cells: beds, props, examinables, her start.
    private static void BuildCells(Transform props, Transform looks, Shader lit, LocationRigBuilder.LocationContent content, Refs refs)
    {
        // every cell: a bed against the far wall, a steel toilet in the corner, stains
        (string room, float cx, bool south)[] cells =
        {
            ("Cell 1", D1, false), ("Cell 2", D2, false), ("Cell 3", D3, false), ("Cell 4", D1, true), ("Cell 5", D2, true), ("Cell 6", D3, true),
        };
        float inner = FacilityBuilder.WallThickness * 0.5f + FacilityBuilder.PaddingThickness;
        int seed = 10;
        foreach (var c in cells)
        {
            Transform g = Group(props, c.room.Replace(" ", ""));
            Rect a = Rooms.First(r => r.En == c.room).Area;
            float farWall = c.south ? a.yMin + inner : a.yMax - inner;
            float bedZ = c.south ? farWall + BedDepth * 0.5f + 0.03f : farWall - BedDepth * 0.5f - 0.03f;
            if (c.room == "Cell 5") bedZ = Start.z - 0.35f - BedDepth * 0.5f;   // where her clips put her
            Prop(g, "SM_Asy_Bed", new Vector3(c.cx, 0f, bedZ), c.south ? 0f : 180f);
            float toiletZ = c.south ? farWall + 0.32f : farWall - 0.32f;
            float toiletX = c.room == "Cell 5" ? a.xMax - inner - 0.35f : a.xMin + inner + 0.35f;   // hers: under the camera
            Prop(g, "SM_Asy_Toilet", new Vector3(toiletX, 0f, toiletZ), c.south ? 0f : 180f);
            Weather(g, c.room, seed++, lit, 2, 2, false);
        }
        Prop(Group(props, "Cell3Mess"), "SM_Asy_Papers", new Vector3(D3 + 0.4f, 0f, -2.2f), 30f);
        FloorDecal(props, "T_Asy_StainBlood", D3 - 0.5f, -3.0f, 1.3f, lit, 40f);

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
            EditorSerialized.SetReference(variant, LanguageVariant.EnglishField, enW);
            EditorSerialized.SetReference(variant, LanguageVariant.PortugueseField, ptW);
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
        Look(looks, content, "x_bed", new Vector3(D2, BedTop, Start.z - 0.35f - BedDepth * 0.5f), null,
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
            FloorDecal(props, "T_Asy_StainBlood", b.x + 0.2f, b.z, 1.5f, lit, b.yaw);
            float doorX = b.room == "Cell 1" || b.room == "Cell 4" ? D1 : b.room == "Cell 2" ? D2 : D3;
            bool topRow = b.room == "Cell 1" || b.room == "Cell 2";
            Look(looks, content, "x_body", pos + Vector3.up * 0.3f, new Vector3(doorX, 0f, topRow ? -4.75f : -6.25f), b.en, b.pt);
        }
        // cell 3: the door forced open, the cell empty
        Look(looks, content, "x_forced", new Vector3(D3, 1.1f, -4f), new Vector3(D3 + 0.9f, 0f, -4.8f),
            new[] { "Forced from the inside. Whoever was in there walked out... or crawled." },
            new[] { "Forçada por dentro. Quem tava aí saiu andando... ou rastejando." });

        // the wing: a bench, a wheelchair, an IV stand, posters, the blood trail toward the nurses station
        Transform wing = Group(props, "Wing");
        Prop(wing, "SM_Asy_Bench", new Vector3(4.67f, 0f, -4.3f), 180f);
        Prop(wing, "SM_Asy_Wheelchair", new Vector3(9.3f, 0f, -6.45f), 25f);
        Prop(wing, "SM_Asy_IVStand", new Vector3(13.6f, 0f, -6.6f), 0f);
        Prop(wing, "SM_Asy_TrashBin", new Vector3(0.4f, 0f, -4.4f), 0f);
        Prop(wing, "SM_Asy_Papers", new Vector3(5.2f, 0f, -5.8f), 80f);
        WallDecal(wing, "Isolation Wing", 'N', 9.3f, 1.5f, new Vector2(0.55f, 0.75f), "T_Asy_PosterSilence", lit);
        WallDecal(wing, "Isolation Wing", 'S', 14.8f, 1.5f, new Vector2(0.55f, 0.75f), "T_Asy_PosterRules", lit);
        WallDecal(wing, "Isolation Wing", 'W', -5.5f, 2.1f, new Vector2(0.9f, 0.23f), "T_Asy_SignIsolation", lit);
        WallDecal(wing, "Isolation Wing", 'E', -4.55f, 2.1f, new Vector2(0.9f, 0.23f), "T_Asy_SignNurses", lit);
        WallDecal(wing, "Isolation Wing", 'N', 14.7f, 2.1f, new Vector2(0.9f, 0.23f), "T_Asy_SignObservation", lit);
        Weather(wing, "Isolation Wing", 3, lit, 4, 3);
        FloorDecal(wing, "T_Asy_StainBlood", 15.6f, -5.6f, 1.4f, lit, 10f);
        FloorDecal(wing, "T_Asy_StainBlood", 13.1f, -5.3f, 0.8f, lit, 70f);
        FloorDecal(wing, "T_Asy_StainBlood", 10.8f, -5.5f, 0.6f, lit, 200f);
        Look(looks, content, "x_blood", new Vector3(15.6f, 0.05f, -5.6f), null,
            new[] { "A blood trail... heading to the nurses station. Of course it is." },
            new[] { "Um rastro de sangue... indo pra enfermaria. Claro que vai." });

        // observation: monitors of the cells (dead), a chair knocked over, files
        Transform obs = Group(props, "Observation");
        Prop(obs, "SM_Asy_Table", new Vector3(16f, 0f, -0.5f), 180f);
        Prop(obs, "SM_Asy_Monitors", new Vector3(16f, TableTop, -0.42f), 180f, false);
        Prop(obs, "SM_Asy_Chair", new Vector3(15.2f, 0.23f, -1.8f), 40f, false, new Vector3(0f, 0f, 90f));
        Prop(obs, "SM_Asy_FilingCabinet", new Vector3(17.6f, 0f, -1.4f), 270f);
        Prop(obs, "SM_Asy_Papers", new Vector3(15.6f, 0f, -2.7f), 10f);
        Prop(obs, "SM_Asy_Clock", new Vector3(16f, 2.3f, -0.13f), 180f, false);
        Weather(obs, "Observation", 5, lit, 2, 2);
        Look(looks, content, "x_monitors", new Vector3(16f, TableTop + 0.3f, -0.42f), null,
            new[] { "Monitors for the cells. All dead... someone used to watch us sleep." },
            new[] { "Monitores das celas. Tudo apagado... alguém ficava vendo a gente dormir." });
        Look(looks, content, "x_chair", new Vector3(15.2f, 0.3f, -1.8f), null,
            new[] { "Knocked over. Whoever sat here left in a hurry." },
            new[] { "Derrubada. Quem sentava aqui saiu com pressa." });
    }

    // ------------------------------------------------------------------ the nurses station
    private static void BuildNurses(Transform props, Transform looks, Transform pickups, Shader lit, LocationRigBuilder.LocationContent content)
    {
        Transform room = Group(props, "NursesStation");
        // the counter (its high front toward the room), the nurse's chair behind it, files, the medicine cabinet, the cart
        Prop(room, "SM_Asy_NurseDesk", new Vector3(21.5f, 0f, -3.3f), 180f);
        Prop(room, "SM_Asy_Chair", new Vector3(21.5f, 0f, -2.55f), 180f);
        Prop(room, "SM_Asy_FilingCabinet", new Vector3(19.0f, 0f, -2.45f), 180f);
        Prop(room, "SM_Asy_Cabinet", new Vector3(25.65f, 0f, -4.2f), 270f);
        Prop(room, "SM_Asy_MedCart", new Vector3(25.4f, 0f, -6.0f), 270f);
        Prop(room, "SM_Asy_Gurney", new Vector3(24.6f, 0f, -7.6f), 90f);
        Prop(room, "SM_Asy_Sink", new Vector3(18.3f, 0f, -8.2f), 90f);
        Prop(room, "SM_Asy_IVStand", new Vector3(19.3f, 0f, -7.3f), 0f);
        Prop(room, "SM_Asy_Wheelchair", new Vector3(20.3f, 0f, -8.35f), 200f);
        Prop(room, "SM_Asy_TrashBin", new Vector3(18.35f, 0f, -3.1f), 0f);
        Prop(room, "SM_Asy_Bottles", new Vector3(25.45f, CartTop, -5.85f), 0f, false);
        Prop(room, "SM_Asy_Papers", new Vector3(22.6f, 0f, -5.4f), 140f);
        Prop(room, "SM_Asy_Clock", new Vector3(23.6f, 2.3f, -2.13f), 180f, false);
        float wallFace = -2f - FacilityBuilder.WallThickness * 0.5f - 0.01f;
        Decal(room, "WardMap", "T_Asy_WardMap", new Vector3(24.3f, 1.6f, wallFace), Vector3.back, new Vector2(1.2f, 0.9f), false, lit);
        WallDecal(room, "Nurses Station", 'W', -6.9f, 1.5f, new Vector2(0.55f, 0.75f), "T_Asy_PosterHands", lit);
        WallDecal(room, "Nurses Station", 'W', -4.4f, 2.1f, new Vector2(0.9f, 0.23f), "T_Asy_SignIsolation", lit);
        Weather(room, "Nurses Station", 7, lit, 3, 3);
        FloorDecal(room, "T_Asy_StainBlood", 23.6f, -7.8f, 1.0f, lit, 120f);

        // the knife on the gurney (her first weapon), a medkit on the cart
        Take(content, ItemBuilder.PlaceWeaponPickup(pickups, "KitchenKnife", new Vector3(24.6f, GurneyTop, -7.3f), 110f));
        Take(content, ItemBuilder.PlaceItemPickup(pickups, "Medkit", 1, new Vector3(25.35f, CartTop, -6.2f), 0f, new Color(0.2f, 0.75f, 0.3f)));

        Look(looks, content, "x_desk", new Vector3(21.2f, DeskTop, -3.25f), null,
            new[] { "Patient files... mine's here. \"Patient 2716. Persecutory delusions.\" At least the cover held.",
                    "\"Believes she is being watched at all times.\" ...Well." },
            new[] { "Prontuários... o meu tá aqui. \"Paciente 2716. Delírios persecutórios.\" Pelo menos o disfarce colou.",
                    "\"Acredita que está sendo observada o tempo todo.\" ...Pois é." });
        Look(looks, content, "x_map", new Vector3(24.3f, 1.6f, wallFace), new Vector3(24.3f, 0f, -3.0f),
            new[] { "Map of the wing. The way out is the stairwell, past the guard post and the service corridor.",
                    "The armory is next to the guard post. That's where the real guns are." },
            new[] { "Mapa da ala. A saída é pela escada, passando pela guarita e pelo corredor de serviço.",
                    "O arsenal fica do lado da guarita. É lá que estão as armas de verdade." });
        Look(looks, content, "x_cabinet", new Vector3(25.65f, 1.2f, -4.2f), null,
            new[] { "Medicine cabinet. Empty bottles. Someone cleaned it out before me." },
            new[] { "Armário de remédios. Só frasco vazio. Alguém limpou tudo antes de mim." });
        Look(looks, content, "x_sink", new Vector3(18.3f, 0.9f, -8.2f), null,
            new[] { "The water comes out brown. I'll stay thirsty, thanks.", "...That mirror. I look like an actual patient." },
            new[] { "A água sai marrom. Prefiro ficar com sede.", "...Esse espelho. Eu pareço uma paciente de verdade." });
        Look(looks, content, "x_gurney", new Vector3(24.6f, GurneyTop, -7.9f), null,
            new[] { "A gurney with leather straps... and somebody left a knife on it." },
            new[] { "Uma maca com correias de couro... e alguém deixou uma faca em cima." });
    }

    // ------------------------------------------------------------------ service hall, linen, bathroom, therapy
    private static void BuildServiceRooms(Transform props, Transform looks, Transform pickups, Shader lit, LocationRigBuilder.LocationContent content)
    {
        Transform hall = Group(props, "ServiceHall");
        Prop(hall, "SM_Asy_Gurney", new Vector3(24.55f, 0f, -17.2f), 90f);
        Prop(hall, "SM_Asy_Wheelchair", new Vector3(22.5f, 0f, -20.9f), 160f);
        Prop(hall, "SM_Asy_MopBucket", new Vector3(22.4f, 0f, -15.6f), 70f);
        Prop(hall, "SM_Asy_TrashBin", new Vector3(24.6f, 0f, -10.0f), 0f);
        Prop(hall, "SM_Asy_Papers", new Vector3(23.4f, 0f, -12.0f), 200f);
        WallDecal(hall, "Service Hall", 'W', -11.9f, 1.9f, new Vector2(0.9f, 0.23f), "T_Asy_SignLinen", lit);
        WallDecal(hall, "Service Hall", 'W', -19.6f, 1.9f, new Vector2(0.9f, 0.23f), "T_Asy_SignBath", lit);
        WallDecal(hall, "Service Hall", 'E', -15.1f, 1.9f, new Vector2(0.9f, 0.23f), "T_Asy_SignTherapy", lit);
        WallDecal(hall, "Service Hall", 'S', 22.5f, 1.9f, new Vector2(0.7f, 0.18f), "T_Asy_SignGuard", lit);
        Weather(hall, "Service Hall", 11, lit, 4, 4);
        FloorDecal(hall, "T_Asy_StainBlood", 23.3f, -19.5f, 1.2f, lit, 15f);
        Look(looks, content, "x_gurney", new Vector3(24.55f, GurneyTop, -17.2f), null,
            new[] { "Another gurney. Straps torn. Whoever was tied here got loose." },
            new[] { "Outra maca. As correias arrebentadas. Quem tava amarrado aqui se soltou." });

        Transform linen = Group(props, "LinenRoom");
        Prop(linen, "SM_Asy_Shelf", new Vector3(17.35f, 0f, -12.2f), 90f);
        Prop(linen, "SM_Asy_Shelf", new Vector3(17.35f, 0f, -13.8f), 90f);
        Prop(linen, "SM_Asy_Shelf", new Vector3(19.5f, 0f, -14.7f), 0f);
        Prop(linen, "SM_Asy_Boxes", new Vector3(19.6f, 0f, -11.4f), 180f);
        Prop(linen, "SM_Asy_MopBucket", new Vector3(21.2f, 0f, -14.4f), 200f);
        Weather(linen, "Linen Room", 13, lit, 2, 2);
        Look(looks, content, "x_shelf", new Vector3(17.35f, 1.0f, -13.0f), null,
            new[] { "Clean sheets, folded. The only clean thing in this building." },
            new[] { "Lençóis limpos, dobrados. A única coisa limpa nesse prédio." });

        Transform bath = Group(props, "Bathroom");
        for (int i = 0; i < 3; i++) Prop(bath, "SM_Asy_Toilet", new Vector3(17.0f + i * 1.3f, 0f, -20.6f), 0f);
        for (int i = 0; i < 2; i++) Prop(bath, "SM_Asy_Sink", new Vector3(16.3f, 0f, -17.2f - i * 1.1f), 90f);
        Prop(bath, "SM_Asy_TrashBin", new Vector3(21.5f, 0f, -16.45f), 0f);
        WallDecal(bath, "Bathroom", 'N', 19.2f, 1.5f, new Vector2(0.55f, 0.75f), "T_Asy_PosterHands", lit);
        Weather(bath, "Bathroom", 17, lit, 3, 3);
        FloorDecal(bath, "T_Asy_StainBlood", 19.5f, -18.6f, 1.3f, lit, 60f);
        Look(looks, content, "x_toilet", new Vector3(18.3f, 0.45f, -20.6f), null,
            new[] { "Shared bathroom. Smells worse than my cell. Didn't think that was possible." },
            new[] { "Banheiro coletivo. Cheira pior que a minha cela. Achei que não dava." });
        Look(looks, content, "x_sink", new Vector3(16.3f, 0.9f, -17.75f), null,
            new[] { "Somebody washed blood off their hands here. Not very well." },
            new[] { "Alguém lavou sangue das mãos aqui. Não muito bem." });

        // therapy: the chair with the straps, a table with a tape recorder, the guard (the keycard by him)
        Transform therapy = Group(props, "TherapyRoom");
        Prop(therapy, "SM_Asy_TherapyChair", new Vector3(28.3f, 0f, -14.0f), 270f);
        Prop(therapy, "SM_Asy_Table", new Vector3(29.8f, 0f, -11.45f), 180f);
        Prop(therapy, "SM_Asy_TapeRecorder", new Vector3(29.4f, TableTop, -11.4f), 170f, false);
        Prop(therapy, "SM_Asy_Tray", new Vector3(30.2f, TableTop, -11.45f), 10f, false);
        Prop(therapy, "SM_Asy_Chair", new Vector3(29.8f, 0f, -12.25f), 0f);
        Prop(therapy, "SM_Asy_Cabinet", new Vector3(30.65f, 0f, -15.6f), 270f);
        Prop(therapy, "SM_Asy_IVStand", new Vector3(27.3f, 0f, -12.4f), 0f);
        Prop(therapy, "SM_Asy_Screen", new Vector3(26.2f, 0f, -11.3f), 0f);
        Weather(therapy, "Therapy Room", 19, lit, 3, 2);
        Vector3 guard = new Vector3(26.6f, 0f, -15.6f);
        EnemyBuilder.PlaceCorpse(therapy, "Body_Guard", guard, 60f, true);
        FloorDecal(therapy, "T_Asy_StainBlood", guard.x + 0.2f, guard.z + 0.2f, 1.8f, lit, 30f);
        Take(content, ItemBuilder.PlaceItemPickup(pickups, "Keycard", 1, new Vector3(27.5f, 0f, -15.0f), 35f, new Color(0.25f, 0.45f, 0.9f)));
        Look(looks, content, "x_body", guard + Vector3.up * 0.3f, null,
            new[] { "A guard. He put up a fight... someone did this with their bare hands.",
                    "His keycard fell out of his belt. I'll need it for the armory." },
            new[] { "Um guarda. Ele lutou... alguém fez isso com as mãos.",
                    "O cartão de acesso dele caiu do cinto. Vou precisar disso pro arsenal." });
        Look(looks, content, "x_chair", new Vector3(28.3f, 0.7f, -14.0f), null,
            new[] { "A \"therapy\" chair. With straps. And electrodes. That's not therapy." },
            new[] { "Uma cadeira de \"terapia\". Com correias. E eletrodos. Isso não é terapia." });
        Look(looks, content, "x_tape", new Vector3(29.4f, TableTop + 0.05f, -11.4f), null,
            new[] { "A tape recorder. \"Session 47. The patient keeps saying she's being watched.\"",
                    "...They had me on tape. Session 47 was yesterday." },
            new[] { "Um gravador. \"Sessão 47. A paciente continua dizendo que está sendo observada.\"",
                    "...Eles me gravaram. A sessão 47 foi ontem." });
    }

    // ------------------------------------------------------------------ guard post, armory
    private static void BuildGuardPost(Transform props, Transform looks, Transform pickups, Shader lit, LocationRigBuilder.LocationContent content)
    {
        Transform post = Group(props, "GuardPost");
        Prop(post, "SM_Asy_Table", new Vector3(21.0f, 0f, -23.45f), 180f);
        Prop(post, "SM_Asy_Monitors", new Vector3(21.0f, TableTop, -23.35f), 180f, false);
        Prop(post, "SM_Asy_Chair", new Vector3(21.0f, 0f, -24.25f), 0f);
        Prop(post, "SM_Asy_Locker", new Vector3(21.0f, 0f, -27.65f), 0f);
        Prop(post, "SM_Asy_Bench", new Vector3(25.65f, 0f, -27.1f), 270f);
        Prop(post, "SM_Asy_Boxes", new Vector3(19.55f, 0f, -27.5f), 90f);
        Prop(post, "SM_Asy_Clock", new Vector3(19.9f, 2.3f, -23.13f), 180f, false);
        WallDecal(post, "Guard Post", 'W', -24.8f, 1.9f, new Vector2(0.9f, 0.23f), "T_Asy_SignArmory", lit);
        Weather(post, "Guard Post", 23, lit, 3, 2);
        SaveStation save = SavePointBuilder.PlaceSavePoint(post, "SavePoint_Terminal", new Vector3(23.6f, 0f, -26.6f), 0f);
        if (save != null) content.Stations.Add(save);
        Look(looks, content, "x_monitors", new Vector3(21.0f, TableTop + 0.3f, -23.35f), null,
            new[] { "The guard monitors. Static on every one... you're the only camera that works in here, huh." },
            new[] { "Os monitores da guarita. Estática em todos... você é a única câmera que funciona aqui, hein." });
        Look(looks, content, "x_desk", new Vector3(20.5f, TableTop, -23.5f), null,
            new[] { "Shift log. The last entry: \"They're not sleeping anymore. Don't open the cells.\"" },
            new[] { "Registro de turnos. A última anotação: \"Eles não estão mais dormindo. Não abram as celas.\"" });
        Look(looks, content, "x_cabinet", new Vector3(21.0f, 1.2f, -27.65f), null,
            new[] { "Guard lockers. A uniform, a photo of somebody's kids... nothing I can use." },
            new[] { "Armários dos guardas. Um uniforme, a foto dos filhos de alguém... nada que eu possa usar." });

        // the armory: a rack, lockers, a table with the pistol and a box of rounds
        Transform armory = Group(props, "Armory");
        Prop(armory, "SM_Asy_Shelf", new Vector3(16.35f, 0f, -27.1f), 90f);
        Prop(armory, "SM_Asy_Locker", new Vector3(17.9f, 0f, -27.65f), 0f);
        Prop(armory, "SM_Asy_Table", new Vector3(17.2f, 0f, -24.45f), 180f);
        Weather(armory, "Armory", 29, lit, 2, 1);
        Take(content, ItemBuilder.PlaceWeaponPickup(pickups, "Pistol1911", new Vector3(16.9f, TableTop, -24.4f), 80f));
        Take(content, ItemBuilder.PlaceItemPickup(pickups, "HandgunAmmo", 14, new Vector3(17.6f, TableTop, -24.45f), 10f, new Color(0.85f, 0.7f, 0.2f)));
        Look(looks, content, "x_shelf", new Vector3(16.35f, 1.0f, -27.1f), null,
            new[] { "Empty racks. Somebody got here first and took almost everything." },
            new[] { "Prateleiras vazias. Alguém chegou antes e levou quase tudo." });
    }

    // ------------------------------------------------------------------ service corridor, stairwell
    private static void BuildCorridor(Transform props, Transform looks, Shader lit, LocationRigBuilder.LocationContent content)
    {
        Transform corridor = Group(props, "ServiceCorridor");
        // the body the first enemy is feeding on, the blood around it
        EnemyBuilder.PlaceCorpse(corridor, "Body_Corridor", new Vector3(46.3f, 0f, -25.6f), 300f, true);
        FloorDecal(corridor, "T_Asy_StainBlood", 46.0f, -25.5f, 2.2f, lit, 0f);
        FloorDecal(corridor, "T_Asy_StainBlood", 38.0f, -25.0f, 1.0f, lit, 90f);
        Prop(corridor, "SM_Asy_Boxes", new Vector3(33.0f, 0f, -26.5f), 0f);
        Prop(corridor, "SM_Asy_Boxes", new Vector3(29.5f, 0f, -24.5f), 180f);
        Prop(corridor, "SM_Asy_TrashBin", new Vector3(37.5f, 0f, -24.4f), 0f);
        Prop(corridor, "SM_Asy_MopBucket", new Vector3(28.0f, 0f, -26.6f), 30f);
        Prop(corridor, "SM_Asy_Wheelchair", new Vector3(41.0f, 0f, -24.55f), 100f);
        Prop(corridor, "SM_Asy_Papers", new Vector3(35.5f, 0f, -25.4f), 300f);
        WallDecal(corridor, "Service Corridor", 'E', -24.45f, 2.0f, new Vector2(0.7f, 0.18f), "T_Asy_SignStairs", lit);
        Weather(corridor, "Service Corridor", 31, lit, 6, 5);
        Look(looks, content, "x_body", new Vector3(46.3f, 0.3f, -25.6f), null,
            new[] { "...I'm not looking at that any longer than I have to." },
            new[] { "...Eu não vou olhar pra isso mais do que o necessário." });

        // stairwell: the stairs up, the way to the rest of the asylum
        Transform stairs = Group(props, "Stairwell");
        Prop(stairs, "SM_Asy_Stairs", new Vector3(50.9f, 0f, -27.6f), 180f);
        Weather(stairs, "Stairwell", 37, lit, 2, 2);
        Look(looks, content, "x_stairs", new Vector3(50.9f, 0.8f, -26.2f), new Vector3(49.6f, 0f, -26.0f),
            new[] { "The stairs. The rest of the asylum is up there." },
            new[] { "A escada. O resto do hospício é lá em cima." });
    }

    // ================================================================== cutscenes (once the rig exists)
    private static void Finish(LocationRigBuilder.LocationRig rig, Refs refs)
    {
        foreach (LanguageVariant v in refs.Variants) EditorSerialized.SetReference(v, LanguageVariant.SettingsField, rig.Settings);
        // the sparkles face the operator's view
        if (rig.ViewCamera != null)
            foreach (InteractSparkle s in sparkles) if (s != null) EditorSerialized.SetReference(s, InteractSparkle.ViewCameraField, rig.ViewCamera.transform);
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
            // as she steps through the door (the enemy is still far: no fight yet, the cutscene can start)
            var steps = New(root, d, "Cutscene_Corridor", "asy_corridor", CutsceneTrigger.When.HelenaEntersArea, null, "", 0f, out Cutscene c);
            c.transform.position = new Vector3(26.7f, 0f, -25.5f);
            var area = new SerializedObject(c.GetComponent<CutsceneTrigger>());
            area.FindProperty(CutsceneTrigger.AreaSizeField).vector3Value = new Vector3(1.4f, 2f, 3f);
            area.ApplyModifiedPropertiesWithoutUndo();
            SecurityCamera far = rig.CameraSystem != null ? rig.CameraSystem.Cameras.FirstOrDefault(x => x != null && x.Room == refs.Rooms["Service Corridor"] && x.transform.position.x > 40f) : null;
            Add(steps, new CameraCutStep { camera = far != null ? far : Cam("Service Corridor") });
            Add(steps, Line(h, "Tem alguém no fim do corredor...", "There's someone at the end of the corridor..."));
            Add(steps, Line(h, "Ei! Você tá bem?", "Hey! Are you okay?"));
            Add(steps, new WaitStep { seconds = 1.5f });
            Add(steps, Line(h, "...Ele tá comendo. Ele tá COMENDO alguém.", "...He's eating. He's EATING someone."));
            Add(steps, new ThoughtStep { textPt = "Ele tá longe. \"Avança\" pra chegar mais perto, \"saca a pistola\", \"atira\".",
                                         textEn = "It's far. \"Advance\" to get closer, \"draw the pistol\", \"shoot\"." });
            Add(steps, new ThoughtStep { textPt = "Se chegar perto: \"recua\", \"desvia\". \"Recarrega\" quando acabar a munição.",
                                         textEn = "If it gets close: \"back off\", \"dodge\". \"Reload\" when it's empty." });
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

    // Creates a cutscene with its trigger.
    private static SerializedProperty New(Transform root, CutsceneDirector d, string name, string id, CutsceneTrigger.When when, Room room,
                                          string flag, float delay, out Cutscene cutscene, string blocking = "", bool letterbox = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        if (room != null) go.transform.position = room.transform.position;
        cutscene = go.AddComponent<Cutscene>();
        EditorSerialized.SetReference(cutscene, Cutscene.DirectorField, d);
        EditorSerialized.SetString(cutscene, Cutscene.IdField, id);
        var trigger = go.AddComponent<CutsceneTrigger>();
        EditorSerialized.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
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

    // Applies the pending steps of a cutscene.
    private static void Commit(Cutscene c)
    {
        if (!Pending.TryGetValue(c, out SerializedObject so)) return;
        so.ApplyModifiedPropertiesWithoutUndo();
        Pending.Remove(c);
    }

    // Adds a step.
    private static void Add(SerializedProperty list, CutsceneStep step)
    {
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = step;
    }

    // A line of a cast member.
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
