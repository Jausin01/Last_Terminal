using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only builders for the facility: prefabs (CCTV camera, doors), the test level (rooms, walls, doors, cameras,
// lights), the camera filter profile and the operator HUD. Called by HelenaSetupMenu (menus 3 and 4).
internal static class FacilityBuilder
{
    private const string CctvFolder = "Assets/Art/Props/CCTV";
    private const string DoorsFolder = "Assets/Art/Props/Doors";
    private const string EnvFolder = "Assets/Art/Environment";
    private const string KeypadFolder = "Assets/Art/Props/Keypad";
    private const string PrefabFolder = "Assets/Prefabs/Facility";
    private const string CctvPrefabPath = PrefabFolder + "/SecurityCamera.prefab";
    private const string WoodDoorPrefabPath = PrefabFolder + "/Door_Wood.prefab";
    private const string SecurityDoorPrefabPath = PrefabFolder + "/Door_Security.prefab";
    private const string DataFolder = "Assets/Data/Facility";
    private const string FilterProfilePath = DataFolder + "/CameraFilter.asset";
    private const string StaticNoisePath = DataFolder + "/StaticNoise.asset";

    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const float CameraHeight = 2.75f;
    internal const float PartnerRadius = 0.3f;      // her NavMeshAgent radius
    private const float WoodDoorWidth = 1.06f;       // outer width of the frames (SM_Door*)
    private const float SecurityDoorWidth = 1.44f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    internal struct Level
    {
        public NavMeshSurface Surface;
        public List<Room> Rooms;
        public List<Door> Doors;
        public List<SecurityCamera> Cameras;
        public Transform ContentRoot;
        public List<Keypad> Keypads;
        public int StartCamera;          // the camera the operator starts on (her cell)
        public Vector3 PartnerStart;     // where she starts (in her cell)
        public float PartnerStartYaw;
    }

    // the padded cell where she starts: x[-2,2] z[-12,-8], under the lobby; its door (5) opens with the keypad
    private static readonly Rect CellRect = new Rect(-2f, -12f, 4f, 4f);
    private const string CellCode = "17";                                  // 0+9+1+7: the number on her back (OutfitBuilder)
    private static readonly Vector3 KeypadCenter = new Vector3(1.15f, 1.27f, -8.18f);
    private static readonly Vector2 KeypadSize = new Vector2(0.16f, 0.24f);
    private const float PaddingThickness = 0.08f;

    // ================================================================== prefabs (menu 3)
    internal static void BuildPrefabs(Scene preview, Shader lit)
    {
        HelenaSetupMenu.EnsureFolder("Assets", "Prefabs");
        HelenaSetupMenu.EnsureFolder("Assets/Prefabs", "Facility");

        // materials
        Material cctvMat = RemapSingle($"{CctvFolder}/SM_CCTV.fbx", "M_CCTV", $"{CctvFolder}/T_CCTV.png", 0.35f, lit, CctvFolder);
        Material doorMat = MaterialFor($"{DoorsFolder}/Materials/M_Doors.mat", $"{DoorsFolder}/T_Doors.png", 0.3f, lit, DoorsFolder);
        Material lampMat = MaterialFor($"{DoorsFolder}/Materials/M_DoorLamp.mat", null, 0.6f, lit, DoorsFolder);
        if (lampMat != null)
        {
            lampMat.SetColor(EmissionColorId, Color.white);
            lampMat.EnableKeyword("_EMISSION");
            lampMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(lampMat);
        }
        foreach (string door in new[] { "SM_DoorWood", "SM_DoorSecurity" })
        {
            if (!(AssetImporter.GetAtPath($"{DoorsFolder}/{door}.fbx") is ModelImporter mi)) continue;
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Doors"), doorMat);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_DoorLamp"), lampMat);
            mi.SaveAndReimport();
        }

        if (cctvMat != null) BuildCctvPrefab(preview);
        BuildDoorPrefab(preview, "SM_DoorWood", WoodDoorPrefabPath, Door.Kind.Hinged, "Leaf", new Vector3(0.9f, 2.1f, 0.25f));
        BuildDoorPrefab(preview, "SM_DoorSecurity", SecurityDoorPrefabPath, Door.Kind.Sliding, "Panel", new Vector3(1.2f, 2.24f, 0.3f));
    }

    private static void BuildCctvPrefab(Scene preview)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CctvPrefabPath) != null) { Debug.Log("[Facility] Prefab already exists, kept: " + CctvPrefabPath); return; }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{CctvFolder}/SM_CCTV.fbx");
        if (model == null) { Debug.LogWarning("[Facility] Model not found: SM_CCTV.fbx"); return; }
        var root = new GameObject("SecurityCamera");
        SceneManager.MoveGameObjectToScene(root, preview);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);
        var cam = root.AddComponent<SecurityCamera>();
        HelenaSetupMenu.SetReference(cam, SecurityCamera.PanField, HelenaSetupMenu.FindDeep(visual.transform, "Pan"));
        HelenaSetupMenu.SetReference(cam, SecurityCamera.TiltField, HelenaSetupMenu.FindDeep(visual.transform, "Tilt"));
        HelenaSetupMenu.SetReference(cam, SecurityCamera.LensField, HelenaSetupMenu.FindDeep(visual.transform, "Lens"));
        PrefabUtility.SaveAsPrefabAsset(root, CctvPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Facility] Prefab built: " + CctvPrefabPath);
    }

    private static void BuildDoorPrefab(Scene preview, string modelName, string path, Door.Kind kind, string partName, Vector3 blockSize)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { Debug.Log("[Facility] Prefab already exists, kept: " + path); return; }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{DoorsFolder}/{modelName}.fbx");
        if (model == null) { Debug.LogWarning($"[Facility] Model not found: {modelName}.fbx"); return; }
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        SceneManager.MoveGameObjectToScene(root, preview);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);
        Transform part = HelenaSetupMenu.FindDeep(visual.transform, partName);
        Transform lampT = HelenaSetupMenu.FindDeep(visual.transform, "Lamp");
        if (part != null) part.gameObject.AddComponent<BoxCollider>();   // shots / clicks hit the door, moves with it

        // NavMesh hole while closed (its own object, so turning it off touches nothing else)
        var block = new GameObject("NavBlock");
        block.transform.SetParent(root.transform, false);
        var obstacle = block.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, blockSize.y * 0.5f, 0f);
        obstacle.size = blockSize;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        var door = root.AddComponent<Door>();
        var so = new SerializedObject(door);
        so.FindProperty(Door.KindField).enumValueIndex = (int)kind;
        so.FindProperty(Door.MovingPartField).objectReferenceValue = part;
        so.FindProperty(Door.LampField).objectReferenceValue = lampT != null ? lampT.GetComponent<Renderer>() : null;
        so.FindProperty(Door.ObstacleField).objectReferenceValue = obstacle;
        so.FindProperty(Door.RemoteField).boolValue = kind == Door.Kind.Sliding;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[Facility] Prefab built: " + path);
    }

    private static Material RemapSingle(string fbxPath, string matName, string texPath, float smooth, Shader lit, string folder)
    {
        if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter mi)) { Debug.LogWarning("[Facility] Model not found: " + fbxPath); return null; }
        Material m = MaterialFor($"{folder}/Materials/{matName}.mat", texPath, smooth, lit, folder);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), m);
        mi.SaveAndReimport();
        return m;
    }

    private static Material MaterialFor(string matPath, string texPath, float smooth, Shader lit, string folder)
    {
        if (lit == null) return null;
        HelenaSetupMenu.EnsureFolder(folder, "Materials");
        Texture2D tex = texPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) : null;
        Material m = HelenaSetupMenu.MakeMaterial(matPath, tex, smooth, false, lit);
        if (texPath == null) { m.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(m); }
        return m;
    }

    // ================================================================== level (menu 4)
    // Plan (meters, x right, z north):   STORAGE  x[-6,6]  z[4,12]
    //                                    CORRIDOR x[-6,6]  z[0,4]
    //   TRAINING x[-14,-6] z[-8,0]  |  LOBBY x[-6,6] z[-8,0]  |  OFFICE x[6,14] z[-8,0]
    //                                    PADDED CELL x[-2,2] z[-12,-8]  (she starts here)
    internal static Level BuildLevel(Scene scene, Shader lit)
    {
        var level = new Level { Rooms = new List<Room>(), Doors = new List<Door>(), Cameras = new List<SecurityCamera>(), Keypads = new List<Keypad>() };
        var root = new GameObject("Facility");
        var geometry = new GameObject("Geometry").transform;   // baked into the NavMesh
        geometry.SetParent(root.transform, false);
        level.ContentRoot = root.transform;

        Material wall = MaterialFor($"{EnvFolder}/Materials/M_Wall.mat", $"{EnvFolder}/T_Wall.png", 0.1f, lit, EnvFolder);
        Material floor = MaterialFor($"{EnvFolder}/Materials/M_Floor.mat", $"{EnvFolder}/T_Floor.png", 0.25f, lit, EnvFolder);
        Material padded = MaterialFor($"{EnvFolder}/Materials/M_PaddedWall.mat", $"{EnvFolder}/T_PaddedWall.png", 0.05f, lit, EnvFolder);
        Material paddedFloor = MaterialFor($"{EnvFolder}/Materials/M_PaddedFloor.mat", $"{EnvFolder}/T_PaddedFloor.png", 0.05f, lit, EnvFolder);

        // rooms + floors + lights
        var rooms = new GameObject("Rooms").transform; rooms.SetParent(root.transform, false);
        level.Rooms.Add(AddRoom(rooms, geometry, floor, "Lobby", new Rect(-6f, -8f, 12f, 8f), new Color(1f, 0.92f, 0.8f), 2.6f));
        level.Rooms.Add(AddRoom(rooms, geometry, floor, "Office", new Rect(6f, -8f, 8f, 8f), new Color(1f, 0.85f, 0.65f), 2.2f));
        level.Rooms.Add(AddRoom(rooms, geometry, floor, "Training Room", new Rect(-14f, -8f, 8f, 8f), new Color(0.8f, 0.9f, 1f), 2.2f));
        level.Rooms.Add(AddRoom(rooms, geometry, floor, "Corridor", new Rect(-6f, 0f, 12f, 4f), new Color(0.75f, 1f, 0.8f), 1.6f));
        level.Rooms.Add(AddRoom(rooms, geometry, floor, "Storage", new Rect(-6f, 4f, 12f, 8f), new Color(1f, 0.95f, 0.85f), 1.8f));
        Room cell = AddRoom(rooms, geometry, paddedFloor, "Padded Cell", CellRect, new Color(0.85f, 0.95f, 1f), 1.5f);
        level.Rooms.Add(cell);
        // safe zones (the operator may leave her there): the cell and the rooms with a save point
        foreach (Room room in level.Rooms)
            if (room == cell || room.DisplayName == "Office" || room.DisplayName == "Corridor" || room.DisplayName == "Storage")
                SetSafeZone(room);

        // walls (door gaps = outer width of the frames)
        var walls = new GameObject("Walls").transform; walls.SetParent(geometry, false);
        WallX(walls, wall, -8f, -14f, 14f, (0f, SecurityDoorWidth));
        // the cell: padded walls, and padding over the lobby wall on its side (door gap kept)
        WallX(walls, padded, CellRect.yMin, CellRect.xMin, CellRect.xMax);
        WallZ(walls, padded, CellRect.xMin, CellRect.yMin, CellRect.yMax);
        WallZ(walls, padded, CellRect.xMax, CellRect.yMin, CellRect.yMax);
        float liningZ = CellRect.yMax - WallThickness * 0.5f - PaddingThickness * 0.5f;
        foreach (var (a, b) in Segments(CellRect.xMin + WallThickness * 0.5f, CellRect.xMax - WallThickness * 0.5f, new[] { (0f, SecurityDoorWidth) }))
            Block(walls, "Padding", new Vector3((a + b) * 0.5f, WallHeight * 0.5f, liningZ), new Vector3(b - a, WallHeight, PaddingThickness), padded, 2f, true);
        WallX(walls, wall, 0f, -14f, -6f);
        WallX(walls, wall, 0f, 6f, 14f);
        WallX(walls, wall, 0f, -6f, 6f, (0f, SecurityDoorWidth));
        WallX(walls, wall, 4f, -6f, 6f, (0f, WoodDoorWidth));
        WallX(walls, wall, 12f, -6f, 6f);
        WallZ(walls, wall, -14f, -8f, 0f);
        WallZ(walls, wall, 14f, -8f, 0f);
        WallZ(walls, wall, -6f, -8f, 0f, (-4f, WoodDoorWidth));
        WallZ(walls, wall, 6f, -8f, 0f, (-4f, SecurityDoorWidth));
        WallZ(walls, wall, -6f, 0f, 12f);
        WallZ(walls, wall, 6f, 0f, 12f);

        // NavMesh from the geometry only (doors and props carve their own holes at runtime),
        // baked for her real size so the doorways stay open
        EnsureBakeRadius(PartnerRadius);
        var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.BuildNavMesh();
        level.Surface = surface;

        // doors, numbered (plate above each one, both faces): "open door 2"
        var doors = new GameObject("Doors").transform; doors.SetParent(root.transform, false);
        var signs = new DoorSigns
        {
            Wall = wall,
            Plates = MaterialFor($"{DoorsFolder}/Materials/M_DoorPlates.mat", $"{DoorsFolder}/T_DoorPlates.png", 0.35f, lit, DoorsFolder),
        };
        level.Doors.Add(AddDoor(doors, SecurityDoorPrefabPath, "Corridor", new Vector3(0f, 0f, 0f), 0f, true, 1, signs));
        level.Doors.Add(AddDoor(doors, SecurityDoorPrefabPath, "Office", new Vector3(6f, 0f, -4f), 90f, false, 2, signs));
        // wooden doors start closed: she opens them (the training room is locked later, keycard in the storage room)
        level.Doors.Add(AddDoor(doors, WoodDoorPrefabPath, "Training Room", new Vector3(-6f, 0f, -4f), 90f, false, 3, signs));
        level.Doors.Add(AddDoor(doors, WoodDoorPrefabPath, "Storage", new Vector3(0f, 0f, 4f), 0f, false, 4, signs));
        // her cell: a locked security door, opened by the keypad beside it (not from the map)
        Door cellDoor = AddDoor(doors, SecurityDoorPrefabPath, "Padded Cell", new Vector3(0f, 0f, CellRect.yMax), 0f, false, 5, signs);
        if (cellDoor != null)
        {
            var so = new SerializedObject(cellDoor);
            so.FindProperty(Door.LockedField).boolValue = true;
            so.FindProperty(Door.RemoteField).boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            level.Doors.Add(cellDoor);
            Keypad keypad = BuildKeypad(root.transform, cellDoor, lit);
            if (keypad != null) level.Keypads.Add(keypad);
        }
        level.Doors.RemoveAll(d => d == null);

        // cameras: one per room, in a corner, aimed at the room
        var cams = new GameObject("SecurityCameras").transform; cams.SetParent(root.transform, false);
        AddCamera(level, cams, "CAM 01", level.Rooms[0], new Vector3(-5.75f, CameraHeight, -7.75f), new Vector3(0.5f, 0f, -3.5f));
        AddCamera(level, cams, "CAM 02", level.Rooms[1], new Vector3(13.75f, CameraHeight, -0.25f), new Vector3(9.5f, 0f, -5f));
        AddCamera(level, cams, "CAM 03", level.Rooms[2], new Vector3(-13.75f, CameraHeight, -0.25f), new Vector3(-9.5f, 0f, -4.5f));
        AddCamera(level, cams, "CAM 04", level.Rooms[3], new Vector3(-5.75f, CameraHeight, 3.75f), new Vector3(2.5f, 0f, 1.5f));
        AddCamera(level, cams, "CAM 05", level.Rooms[4], new Vector3(5.75f, CameraHeight, 11.75f), new Vector3(-0.5f, 0f, 7.5f));
        // lobby, watching the blackjack table (Last Terminal/4 puts it at x -3, z -6.95)
        AddCamera(level, cams, "CAM 06", level.Rooms[0], new Vector3(0.75f, CameraHeight, -0.25f), new Vector3(-3f, 0f, -6.1f));
        // her cell, from the corner opposite the keypad: it sees her back when she types
        level.StartCamera = level.Cameras.Count;
        AddCamera(level, cams, "CAM 07", cell, new Vector3(CellRect.xMin + 0.3f, CameraHeight, CellRect.yMin + 0.3f), new Vector3(0.6f, 0f, -8.8f));
        level.PartnerStart = new Vector3(-0.4f, 0f, -10.6f);
        level.PartnerStartYaw = 210f;

        // dim, moody light: no sky, low ambient, a weak "moon" for shadows
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.19f);
        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.35f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(62f, -30f, 0f);
        return level;
    }

    // The keypad beside her cell door: a metal plate with the keys printed on it (T_Keypad, 256 x 384 px for 0.16 x 0.24 m),
    // a key transform at each printed key (her finger goes there), the code display (3D text) and a speaker for the beeps.
    // Its forward points into the cell.
    private static Keypad BuildKeypad(Transform parent, Door door, Shader lit)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KeypadFolder}/T_Keypad.png");
        if (tex == null) { Debug.LogWarning($"[Facility] {KeypadFolder}/T_Keypad.png missing: no keypad (the cell door stays locked)."); return null; }
        HelenaSetupMenu.EnsureFolder(KeypadFolder, "Materials");
        Material face = HelenaSetupMenu.MakeMaterial($"{KeypadFolder}/Materials/M_Keypad.mat", tex, 0.45f, false, lit);

        var root = new GameObject("Keypad_" + door.DisplayName.Replace(" ", ""));
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(KeypadCenter, Quaternion.LookRotation(Vector3.back));   // facing into the cell

        // the plate: its printed face toward +z (the cell), u to the viewer's right (= local -x)
        var plate = new GameObject("Plate");
        plate.transform.SetParent(root.transform, false);
        float w = KeypadSize.x * 0.5f, h = KeypadSize.y * 0.5f, z = 0.012f;
        var mesh = new Mesh { name = "Keypad_Plate" };
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(w, -h, z), new Vector3(-w, -h, z), new Vector3(-w, h, z), new Vector3(w, h, z),        // face
            new Vector3(w, -h, 0f), new Vector3(-w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(w, h, 0f),    // back (on the wall)
            new Vector3(w, -h, z), new Vector3(-w, -h, z), new Vector3(-w, h, z), new Vector3(w, h, z),        // front rim (own normals)
        });
        var frame = new Vector2(0.01f, 0.01f);   // edges: the dark frame of the texture
        mesh.SetUVs(0, new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            frame, frame, frame, frame, frame, frame, frame, frame,
        });
        mesh.SetTriangles(new[] { 0, 3, 2, 0, 2, 1,   4, 8, 9, 4, 9, 5,   5, 9, 10, 5, 10, 6,   6, 10, 11, 6, 11, 7,   7, 11, 8, 7, 8, 4 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        plate.AddComponent<MeshFilter>().sharedMesh = mesh;
        plate.AddComponent<MeshRenderer>().sharedMaterial = face;

        // keys at the printed centers (px -> local m)
        var keys = new List<Transform>();
        for (int i = 0; i < Keypad.KeyOrder.Length; i++)
        {
            int row = i / 3, col = i % 3;
            float px = 28f + 66.67f * (col + 0.5f), py = 112f + 63f * (row + 0.5f);
            var key = new GameObject("Key_" + Keypad.KeyOrder[i]).transform;
            key.SetParent(root.transform, false);
            key.localPosition = new Vector3((0.5f - px / 256f) * KeypadSize.x, (0.5f - py / 384f) * KeypadSize.y, z);
            keys.Add(key);
        }

        // the display window (px 24..232 x 22..92): 3D text facing the cell
        var textGo = new GameObject("Display");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, (0.5f - 57f / 384f) * KeypadSize.y, z + 0.002f);
        textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TMP reads along its +z: toward the wall
        var text = textGo.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(0.12f, 0.04f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.01f;
        text.fontSizeMax = 0.4f;
        text.text = "CODE";

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;   // heard through the camera's microphone, like her voice
        audio.volume = 0.6f;

        var keypad = root.AddComponent<Keypad>();
        HelenaSetupMenu.SetReference(keypad, Keypad.DoorField, door);
        HelenaSetupMenu.SetString(keypad, Keypad.CodeField, CellCode);
        HelenaSetupMenu.SetReferenceArray(keypad, Keypad.KeysField, keys);
        HelenaSetupMenu.SetReference(keypad, Keypad.DisplayField, text);
        HelenaSetupMenu.SetReference(keypad, Keypad.AudioSourceField, audio);
        return keypad;
    }

    // The default agent type (Humanoid) is what NavMeshSurface bakes with: its radius must match her agent (0.3),
    // otherwise a 0.5 m erosion closes the 1.16 m wooden doorways.
    internal static void EnsureBakeRadius(float radius)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        SerializedProperty list = so.FindProperty("m_Settings");
        if (list == null) return;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("agentTypeID").intValue != 0) continue;
            SerializedProperty r = e.FindPropertyRelative("agentRadius");
            if (r == null || Mathf.Approximately(r.floatValue, radius)) return;
            Debug.Log($"[Facility] Navigation > Agents > Humanoid radius {r.floatValue} -> {radius} (her NavMeshAgent radius).");
            r.floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return;
        }
    }

    internal static void SetSafeZone(Room room)
    {
        var so = new SerializedObject(room);
        so.FindProperty(Room.SafeZoneField).boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Portuguese names of the facility's rooms (the English one is the lexicon key)
    private static string Portuguese(string displayName)
    {
        switch (displayName)
        {
            case "Lobby": return "Recepção";
            case "Office": return "Escritório";
            case "Training Room": return "Sala de Treino";
            case "Corridor": return "Corredor";
            case "Storage": return "Depósito";
            case "Padded Cell": return "Cela Acolchoada";
            default: return displayName;
        }
    }

    private static Room AddRoom(Transform rooms, Transform geometry, Material floorMat, string displayName, Rect r, Color lightColor, float intensity)
    {
        var go = new GameObject("Room_" + displayName.Replace(" ", ""));
        go.transform.SetParent(rooms, false);
        go.transform.position = new Vector3(r.center.x, 0f, r.center.y);
        var room = go.AddComponent<Room>();
        HelenaSetupMenu.SetString(room, Room.DisplayNameField, displayName);
        HelenaSetupMenu.SetString(room, Room.DisplayNamePtField, Portuguese(displayName));
        var so = new SerializedObject(room);
        so.FindProperty(Room.SizeField).vector2Value = r.size;
        so.ApplyModifiedPropertiesWithoutUndo();

        Block(geometry, "Floor_" + displayName.Replace(" ", ""), new Vector3(r.center.x, -0.05f, r.center.y), new Vector3(r.width, 0.1f, r.height), floorMat, 2f);

        var lamp = new GameObject("Light").AddComponent<Light>();
        lamp.transform.SetParent(go.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        lamp.type = LightType.Point;
        lamp.color = lightColor;
        lamp.intensity = intensity;
        lamp.range = Mathf.Max(r.width, r.height) * 0.9f;
        lamp.shadows = LightShadows.None;
        return room;
    }

    // wall along X at z, from x0 to x1, with gaps (center x, width)
    private static void WallX(Transform parent, Material m, float z, float x0, float x1, params (float center, float width)[] gaps)
    {
        foreach (var (a, b) in Segments(x0, x1, gaps))
            Block(parent, "Wall", new Vector3((a + b) * 0.5f, WallHeight * 0.5f, z), new Vector3(b - a + WallThickness, WallHeight, WallThickness), m, 2f, true);
    }

    private static void WallZ(Transform parent, Material m, float x, float z0, float z1, params (float center, float width)[] gaps)
    {
        foreach (var (a, b) in Segments(z0, z1, gaps))
            Block(parent, "Wall", new Vector3(x, WallHeight * 0.5f, (a + b) * 0.5f), new Vector3(WallThickness, WallHeight, b - a + WallThickness), m, 2f, true);
    }

    private static List<(float, float)> Segments(float from, float to, (float center, float width)[] gaps)
    {
        var result = new List<(float, float)>();
        float start = from;
        foreach (var g in gaps)
        {
            float a = g.center - g.width * 0.5f, b = g.center + g.width * 0.5f;
            if (a - start > 0.01f) result.Add((start, a - WallThickness * 0.5f));
            start = b + WallThickness * 0.5f;
        }
        if (to - start > 0.01f) result.Add((start, to));
        return result;
    }

    // a box with UVs in meters / tile (texture repeats instead of stretching); wall UVs: 2 m x 3 m per tile
    private static GameObject Block(Transform parent, string objectName, Vector3 center, Vector3 size, Material m, float tile, bool wallUv = false)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var mesh = BoxMesh(size, tile, wallUv);
        mesh.name = objectName + "_Mesh";
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    private static Mesh BoxMesh(Vector3 s, float tile, bool wallUv)
    {
        Vector3 h = s * 0.5f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        void Face(Vector3 normal, Vector3 u, Vector3 w, float du, float dw)
        {
            int i = v.Count;
            Vector3 c = Vector3.Scale(normal, h);
            Vector3 hu = u * (du * 0.5f), hw = w * (dw * 0.5f);
            v.Add(c - hu - hw); v.Add(c + hu - hw); v.Add(c + hu + hw); v.Add(c - hu + hw);
            for (int k = 0; k < 4; k++) n.Add(normal);
            float su = du / tile, sw = wallUv ? dw / WallHeight : dw / tile;
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(su, 0f)); uv.Add(new Vector2(su, sw)); uv.Add(new Vector2(0f, sw));
            tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        Face(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
        Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
        Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
        Face(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
        Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
        Face(Vector3.down, Vector3.left, Vector3.forward, s.x, s.z);
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private struct DoorSigns { public Material Wall, Plates; }

    private static Door AddDoor(Transform parent, string prefabPath, string displayName, Vector3 pos, float yaw, bool open,
                                int number, DoorSigns signs)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning($"[Facility] {prefabPath} missing: run menu 3."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "Door_" + displayName.Replace(" ", "");
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var door = go.GetComponent<Door>();
        HelenaSetupMenu.SetString(door, Door.DisplayNameField, displayName);
        var so = new SerializedObject(door);
        so.FindProperty(Door.StartOpenField).boolValue = open;
        so.FindProperty(Door.NumberField).intValue = number;
        so.ApplyModifiedPropertiesWithoutUndo();
        AddHeader(door, prefabPath == SecurityDoorPrefabPath ? "SM_DoorHeader_Security" : "SM_DoorHeader_Wood", signs);
        return door;
    }

    // the wall above the frame + its number plates (SM_DoorHeader_*: Lintel, PlateA, PlateB), child of the door
    private static void AddHeader(Door door, string modelName, DoorSigns signs)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{DoorsFolder}/{modelName}.fbx");
        if (model == null) { Debug.LogWarning($"[Facility] {DoorsFolder}/{modelName}.fbx missing: the door has no number plate."); return; }
        var header = (GameObject)PrefabUtility.InstantiatePrefab(model, door.transform);
        header.name = "Header";
        header.transform.localPosition = Vector3.zero;
        header.transform.localRotation = Quaternion.identity;
        var plates = new List<Renderer>();
        foreach (Renderer r in header.GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("Plate")) { r.sharedMaterial = signs.Plates; plates.Add(r); }
            else r.sharedMaterial = signs.Wall;
        }
        var sign = header.AddComponent<DoorSign>();
        HelenaSetupMenu.SetReference(sign, DoorSign.DoorField, door);
        HelenaSetupMenu.SetReferenceArray(sign, DoorSign.PlatesField, plates);
    }

    // a CCTV camera in a corner of the room, aimed at a point (any scene: the casino uses it too)
    internal static SecurityCamera AddCamera(Level level, Transform parent, string label, Room room, Vector3 mount, Vector3 lookAt)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CctvPrefabPath);
        if (prefab == null) { Debug.LogWarning($"[Facility] {CctvPrefabPath} missing: run menu 3."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = label.Replace(" ", "") + "_" + room.DisplayName.Replace(" ", "");
        Vector3 flat = lookAt - mount; flat.y = 0f;
        go.transform.SetPositionAndRotation(mount, Quaternion.LookRotation(flat));
        var cam = go.GetComponent<SecurityCamera>();
        HelenaSetupMenu.SetString(cam, SecurityCamera.LabelField, label);
        HelenaSetupMenu.SetReference(cam, SecurityCamera.RoomField, room);
        // look down at a point ~1 m above the floor at the target
        float pitch = Mathf.Atan2(mount.y - 1f, flat.magnitude) * Mathf.Rad2Deg;
        var so = new SerializedObject(cam);
        so.FindProperty(SecurityCamera.PitchField).floatValue = pitch;
        so.ApplyModifiedPropertiesWithoutUndo();
        level.Cameras.Add(cam);
        return cam;
    }

    // ================================================================== filter + HUD
    internal static VolumeProfile EnsureFilterProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FilterProfilePath);
        if (profile != null) return profile;   // tune it in the inspector, never overwritten
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Facility");
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, FilterProfilePath);

        var grain = Add<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.55f);
        grain.response.Override(0.8f);
        var vignette = Add<Vignette>(profile);
        vignette.intensity.Override(0.42f);
        vignette.smoothness.Override(0.45f);
        var aberration = Add<ChromaticAberration>(profile);
        aberration.intensity.Override(0.35f);
        var lens = Add<LensDistortion>(profile);
        lens.intensity.Override(-0.18f);
        var color = Add<ColorAdjustments>(profile);
        color.saturation.Override(-45f);
        color.contrast.Override(18f);
        color.colorFilter.Override(new Color(0.88f, 1f, 0.92f));
        var bloom = Add<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    private static Texture2D EnsureStaticNoise()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(StaticNoisePath);
        if (tex != null) return tex;
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Facility");
        const int size = 128;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "StaticNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var rnd = new System.Random(7);
        var px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++)
        {
            byte g = (byte)rnd.Next(0, 256);
            px[i] = new Color32(g, g, g, 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        AssetDatabase.CreateAsset(tex, StaticNoisePath);
        return tex;
    }

    private static readonly Color Phosphor = new Color(0.45f, 1f, 0.55f);
    private static readonly Color Dim = new Color(0.2f, 0.5f, 0.27f);

    // Operator HUD under the save screen: filter overlays, viewfinder, labels, pan bar, map + settings panels.
    internal static void BuildCameraHud(Transform canvas, SecurityCameraSystem system, CameraSettings settings, Transform partner,
                                        List<Room> rooms, List<Door> doors, Volume volume,
                                        string placeName = "FACILITY", string placeNamePt = "INSTALAÇÃO")
    {
        var root = HelenaSetupMenu.CreateUiObject(canvas, "CameraHud");
        root.transform.SetSiblingIndex(0);   // under the ammo HUD, save screen and fader
        HelenaSetupMenu.Stretch((RectTransform)root.transform);

        // filter overlays (strength set by CameraFilter)
        RawImage scan = Overlay(root.transform, "Scanlines", HelenaSetupMenu.EnsureScanlines(), new Rect(0f, 0f, 1f, 180f));
        RawImage noise = Overlay(root.transform, "Grain", EnsureStaticNoise(), new Rect(0f, 0f, 6f, 4f));
        RawImage staticBurst = Overlay(root.transform, "Static", EnsureStaticNoise(), new Rect(0f, 0f, 2f, 2f));
        staticBurst.color = new Color(1f, 1f, 1f, 0f);

        // viewfinder corners
        foreach (Vector2 corner in new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) })
        {
            Vector2 dir = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
            Vector2 at = new Vector2(dir.x * 28f, dir.y * 28f);
            Bar(root.transform, corner, at, new Vector2(dir.x * 50f, 3f), corner);
            Bar(root.transform, corner, at, new Vector2(3f, dir.y * 50f), corner);
        }

        Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f), bl = Vector2.zero, bc = new Vector2(0.5f, 0f);
        var rec = HelenaSetupMenu.CreateUiObject(root.transform, "RecDot").AddComponent<Image>();
        rec.color = new Color(1f, 0.15f, 0.1f);
        rec.raycastTarget = false;
        HelenaSetupMenu.Place(rec.rectTransform, tl, tl, tl, new Vector2(52f, -50f), new Vector2(14f, 14f));
        TMP_Text recText = HelenaSetupMenu.CreateText(root.transform, "RecText", 20, TextAlignmentOptions.Left, new Color(1f, 0.3f, 0.25f));
        recText.text = "REC";
        HelenaSetupMenu.Place(recText.rectTransform, tl, tl, tl, new Vector2(74f, -44f), new Vector2(80f, 26f));
        TMP_Text camLabel = HelenaSetupMenu.CreateText(root.transform, "CameraLabel", 38, TextAlignmentOptions.Left, Phosphor);
        HelenaSetupMenu.Place(camLabel.rectTransform, tl, tl, tl, new Vector2(50f, -72f), new Vector2(400f, 44f));
        TMP_Text roomLabel = HelenaSetupMenu.CreateText(root.transform, "RoomLabel", 22, TextAlignmentOptions.Left, Phosphor);
        HelenaSetupMenu.Place(roomLabel.rectTransform, tl, tl, tl, new Vector2(52f, -116f), new Vector2(400f, 28f));
        TMP_Text clock = HelenaSetupMenu.CreateText(root.transform, "Clock", 22, TextAlignmentOptions.Right, Phosphor);
        HelenaSetupMenu.Place(clock.rectTransform, tr, tr, tr, new Vector2(-50f, -44f), new Vector2(420f, 28f));
        TMP_Text subject = HelenaSetupMenu.CreateText(root.transform, "Subject", 18, TextAlignmentOptions.Right, Phosphor);
        HelenaSetupMenu.Place(subject.rectTransform, tr, tr, tr, new Vector2(-50f, -76f), new Vector2(420f, 26f));
        TMP_Text hint = HelenaSetupMenu.CreateText(root.transform, "Hint", 15, TextAlignmentOptions.Left, Dim);
        HelenaSetupMenu.Place(hint.rectTransform, bl, bl, bl, new Vector2(50f, 40f), new Vector2(600f, 24f));

        // pan indicator: a line with a marker, < > at the ends
        var bar = HelenaSetupMenu.CreateUiObject(root.transform, "PanBar").AddComponent<Image>();
        bar.color = Dim;
        bar.raycastTarget = false;
        HelenaSetupMenu.Place(bar.rectTransform, bc, bc, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(320f, 2f));
        var marker = HelenaSetupMenu.CreateUiObject(bar.transform, "Marker").AddComponent<Image>();
        marker.color = Phosphor;
        marker.raycastTarget = false;
        HelenaSetupMenu.Place(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 18f));
        TMP_Text left = HelenaSetupMenu.CreateText(bar.transform, "Left", 18, TextAlignmentOptions.Center, Dim); left.text = "<";
        HelenaSetupMenu.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(20f, 24f));
        TMP_Text right = HelenaSetupMenu.CreateText(bar.transform, "Right", 18, TextAlignmentOptions.Center, Dim); right.text = ">";
        HelenaSetupMenu.Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(20f, 24f));

        // map panel: the blueprint (left, dark well with a frame) + the info column (right); content built by the HUD
        Vector2 mid = new Vector2(0.5f, 0.5f);
        CanvasGroup map = Panel(root.transform, "MapPanel", "FACILITY MAP  //  SECURITY NETWORK", new Vector2(1060f, 640f));
        var well = HelenaSetupMenu.CreateUiObject(map.transform.GetChild(1), "MapWell").AddComponent<Image>();
        well.color = new Color(0f, 0.025f, 0.012f, 0.95f);
        well.raycastTarget = false;
        HelenaSetupMenu.Place(well.rectTransform, mid, mid, mid, new Vector2(-150f, -16f), new Vector2(720f, 570f));
        var wellLine = well.gameObject.AddComponent<Outline>();
        wellLine.effectColor = Dim;
        wellLine.effectDistance = new Vector2(1f, -1f);
        var area = HelenaSetupMenu.CreateUiObject(well.transform, "MapArea").GetComponent<RectTransform>();
        HelenaSetupMenu.Place(area, mid, mid, mid, Vector2.zero, new Vector2(700f, 550f));
        area.gameObject.AddComponent<RectMask2D>();   // the grid stays inside the well
        var side = HelenaSetupMenu.CreateUiObject(map.transform.GetChild(1), "MapSide").GetComponent<RectTransform>();
        HelenaSetupMenu.Place(side, mid, mid, mid, new Vector2(368f, -16f), new Vector2(290f, 570f));

        // settings panel
        CanvasGroup settingsPanel = Panel(root.transform, "SettingsPanel", "SETTINGS", new Vector2(760f, 600f));
        var rows = HelenaSetupMenu.CreateUiObject(settingsPanel.transform.GetChild(1), "Rows").GetComponent<RectTransform>();
        HelenaSetupMenu.Place(rows, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), Vector2.zero);

        var hud = root.AddComponent<SecurityCameraHud>();
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SystemField, system);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SettingsField, settings);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.PartnerField, partner);
        HelenaSetupMenu.SetReferenceArray(hud, SecurityCameraHud.RoomsField, rooms);
        HelenaSetupMenu.SetReferenceArray(hud, SecurityCameraHud.DoorsField, doors);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.CameraLabelField, camLabel);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.RoomLabelField, roomLabel);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.RecDotField, rec);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.ClockField, clock);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SubjectField, subject);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.PanBarField, bar.rectTransform);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.PanMarkerField, marker.rectTransform);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.StaticImageField, staticBurst);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.HintField, hint);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.MapPanelField, map);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.MapAreaField, area);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.MapSideField, side);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SettingsPanelField, settingsPanel);
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SettingsRowsField, rows);
        // the panel titles (Panel: Frame/Title) follow the language too
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.MapTitleField, map.transform.GetChild(1).Find("Title").GetComponent<TMP_Text>());
        HelenaSetupMenu.SetReference(hud, SecurityCameraHud.SettingsTitleField, settingsPanel.transform.GetChild(1).Find("Title").GetComponent<TMP_Text>());
        HelenaSetupMenu.SetString(hud, SecurityCameraHud.PlaceNameField, placeName);
        HelenaSetupMenu.SetString(hud, SecurityCameraHud.PlaceNamePtField, placeNamePt);

        var filter = root.AddComponent<CameraFilter>();
        HelenaSetupMenu.SetReference(filter, CameraFilter.SettingsField, settings);
        HelenaSetupMenu.SetReference(filter, CameraFilter.VolumeField, volume);
        HelenaSetupMenu.SetReference(filter, CameraFilter.ScanlinesField, scan);
        HelenaSetupMenu.SetReference(filter, CameraFilter.NoiseField, noise);
    }

    private static RawImage Overlay(Transform parent, string objectName, Texture tex, Rect uv)
    {
        var img = HelenaSetupMenu.CreateUiObject(parent, objectName).AddComponent<RawImage>();
        HelenaSetupMenu.Stretch(img.rectTransform);
        img.texture = tex;
        img.uvRect = uv;
        img.raycastTarget = false;
        return img;
    }

    private static void Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var img = HelenaSetupMenu.CreateUiObject(parent, "Corner").AddComponent<Image>();
        img.color = Phosphor;
        img.raycastTarget = false;
        // negative sizes flip the pivot side
        Vector2 abs = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        Vector2 p = new Vector2(size.x < 0f ? 1f : 0f, size.y < 0f ? 1f : 0f);
        HelenaSetupMenu.Place(img.rectTransform, anchor, anchor, p, position, abs);
    }

    // backdrop (child 0) + frame (child 1, holds the content) + title
    private static CanvasGroup Panel(Transform parent, string objectName, string title, Vector2 size)
    {
        var go = HelenaSetupMenu.CreateUiObject(parent, objectName);
        HelenaSetupMenu.Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
        var backdrop = HelenaSetupMenu.CreateUiObject(go.transform, "Backdrop").AddComponent<Image>();
        HelenaSetupMenu.Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0.02f, 0.01f, 0.55f);
        var frame = HelenaSetupMenu.CreateUiObject(go.transform, "Frame").AddComponent<Image>();
        HelenaSetupMenu.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        frame.color = new Color(0.01f, 0.05f, 0.025f, 0.92f);
        var outline = frame.gameObject.AddComponent<Outline>();
        outline.effectColor = Dim;
        outline.effectDistance = new Vector2(3f, -3f);
        TMP_Text t = HelenaSetupMenu.CreateText(frame.transform, "Title", 18, TextAlignmentOptions.Center, Phosphor);
        t.text = "<mspace=0.6em>" + title + "</mspace>";
        HelenaSetupMenu.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(size.x - 20f, 28f));
        return group;
    }
}
