using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu "Last Terminal/8 - Build Casino Scene": the casino (its own scene, reached by elevator), wine and gold.
// The hall (CasinoHallBuilder: floors, walls, columns, the bar, chandeliers), the elevator hall (4 elevators), 24 slot
// machines (SM_CasinoSlotMachine, reels in the model), 2 blackjack + 2 Casino Hold'em tables (SM_Casino*Table on the
// table prefab), the roulette (SM_CasinoRoulette, the old casino's table and European rotor), the prize lockers,
// the cashier at the bar, a save point; colliders + NavMesh, rooms + CCTV cameras, lights.
// Helena and her systems come from the shared rig (LocationRigBuilder.BuildLocation).
// Plan: see CasinoHallBuilder (sections keep their names: the lexicon and the save files use them).
public static class CasinoBuilder
{
    // Scene, NavMesh, art and material paths.
    internal const string SceneName = "Casino";
    private const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
    private const string NavMeshPath = "Assets/Scenes/" + SceneName + "_NavMesh.asset";
    private const string ArtFolder = "Assets/Art/Props/Casino";
    private const string MatFolder = ArtFolder + "/Materials";
    private const string SlotModelPath = ArtFolder + "/SM_CasinoSlotMachine.fbx";
    private const string RouletteModelPath = ArtFolder + "/SM_CasinoRoulette.fbx";
    // Room size and the CCTV cameras' height (m).
    private const float RoomWidth = CasinoHallBuilder.Width;
    private const float RoomLength = CasinoHallBuilder.Length;
    private const float CameraHeight = 3.85f;
    // Name of the roulette wheel's spinning part in its model.
    private const string RotorName = "RouletteRotor";

    // slot banks: 6 machines a row, two rows back to back (south row faces south, north row faces north), x of the
    // first machine, pitch; the bank's middle line (z) of the front and back banks
    private const int MachinesPerRow = 6;
    private const float SlotX0 = 7.15f, SlotPitch = 1.3f;
    private static readonly float[] BankZ = { 5.95f, 19.7f };
    private const float SlotHalfDepth = 0.37f;                    // machine center to the bank's middle line
    // measured on SM_CasinoSlotMachine (machine space, front toward +z): the SPIN button (her finger), the machine's
    // stand point = the button's x moved 0.09 to her left so the button is at her right hand
    private static readonly Vector3 SpinButton = new Vector3(0.168f, 0.851f, 0.305f);
    private const float SpinToHerRight = 0.09f;

    // card tables: x of the two tables of a row, the dealer's seat of each row (Hold'em faces south, blackjack north)
    private static readonly float[] TableX = { 7.6f, 13.2f };
    private const float HoldemSeatZ = 10.2f, BlackjackSeatZ = 11.8f;
    // roulette: the wheel's axis here, and where it was in the old casino (the felt spots below were measured there)
    private static readonly Vector3 WheelAxis = new Vector3(9.2f, 0f, 15.4f);
    private static readonly Vector3 OldWheelAxis = new Vector3(5.285f, 0f, 12.552f);
    private const float RouletteFelt = 0.843f;
    // prize lockers: on the west wall (back at x 0), facing east
    private const float CaseWidth = 0.95f, CaseDepth = 0.7f, CaseHeight = 2.3f;
    private static readonly float[] CaseZ = { 4.9f, 6.2f, 7.5f, 8.8f };

    // URP Lit property ids.
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Menu 8: builds the whole casino scene.
    [MenuItem("Last Terminal/8 - Build Casino Scene")]
    public static void BuildCasinoScene()
    {
        foreach (string path in new[] { SlotModelPath, RouletteModelPath, BlackjackBuilder.CasinoBlackjackModelPath, BlackjackBuilder.CasinoPokerModelPath })
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"[Casino] {path} not found: copy the casino art first.");
                return;
            }
        LocationRigBuilder.BuildLocation(ScenePath, NavMeshPath, BuildContent);
    }

    // The scene's content (everything but Helena's rig): hall, games, rooms, cameras, elevators, lights.
    private static LocationRigBuilder.LocationContent BuildContent(Scene scene, Shader lit)
    {
        var content = new LocationRigBuilder.LocationContent { TestEnemies = false };
        var level = new FacilityBuilder.Level
        {
            Rooms = new List<Room>(), Doors = new List<Door>(), Cameras = new List<SecurityCamera>(), Keypads = new List<Keypad>(),
        };

        var root = new GameObject("Casino");
        level.ContentRoot = root.transform;
        var geometry = new GameObject("Geometry").transform;   // baked into the NavMesh (its colliders)
        geometry.SetParent(root.transform, false);
        var decor = new GameObject("Decor").transform;          // no colliders: not in the NavMesh
        decor.SetParent(root.transform, false);

        ImportArt(lit);
        CasinoHallBuilder.Build(geometry, decor, lit);

        // the slot machines (their models are solid: in the NavMesh) and the roulette's table
        var machines = new GameObject("SlotMachines").transform;
        machines.SetParent(root.transform, false);
        var slotModels = new GameObject("SlotModels").transform;
        slotModels.SetParent(geometry, false);
        for (int bank = 0; bank < BankZ.Length; bank++)
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < MachinesPerRow; i++)
                {
                    int number = content.Slots.Count + 1;
                    bool south = side == 0;
                    var at = new Vector3(SlotX0 + i * SlotPitch, 0f, BankZ[bank] + (south ? -SlotHalfDepth : SlotHalfDepth));
                    SlotMachine m = BuildSlot(machines, slotModels, number, at, south ? 180f : 0f);
                    if (m != null) content.Slots.Add(m);
                }
        Transform rouletteModel = PlaceModel(RouletteModelPath, geometry, "RouletteTable", WheelAxis, 0f, RotorName);

        // NavMesh from the colliders (the elevators, tables, save point... carve their own holes at runtime)
        FacilityBuilder.EnsureBakeRadius(FacilityBuilder.PartnerRadius);
        var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        level.Surface = surface;

        // sections: one hall cut in places easy to name, each with a clear spot to send her to (marked on the map).
        // All joined without doors: from any of them she walks to any other.
        var rooms = new GameObject("Rooms").transform;
        rooms.SetParent(root.transform, false);
        var sections = new List<Room>();
        foreach (var sec in Sections) sections.Add(AddRoom(rooms, sec.en, sec.pt, sec.area, sec.spot));
        foreach (Room a in sections)
        {
            var open = new List<Room>();
            foreach (Room b in sections) if (b != a && Touch(a, b)) open.Add(b);
            EditorSerialized.SetReferenceArray(a, Room.OpenToField, open);
        }
        level.Rooms.AddRange(sections);
        Room hall = sections[0], center = sections[1], bar = sections[2], roulette = sections[3], front = sections[4], back = sections[5];
        Room gallery = sections[6];
        content.CenterRoom = center;   // "go to the center" anywhere in the casino

        // cameras: two in the corners watching the elevators, one per section
        var cams = new GameObject("SecurityCameras").transform;
        cams.SetParent(root.transform, false);
        level.StartCamera = 0;
        SecurityCamera westHall = FacilityBuilder.AddCamera(level, cams, "CAM 01", hall, new Vector3(0.4f, CameraHeight, 4.4f), new Vector3(5f, 0f, 1.9f));
        SecurityCamera eastHall = FacilityBuilder.AddCamera(level, cams, "CAM 02", hall, new Vector3(RoomWidth - 0.4f, CameraHeight, 4.4f), new Vector3(11f, 0f, 1.9f));
        FacilityBuilder.AddCamera(level, cams, "CAM 03", center, new Vector3(RoomWidth - 0.4f, CameraHeight, 14f), new Vector3(9f, 0f, 10.5f));
        FacilityBuilder.AddCamera(level, cams, "CAM 04", bar, new Vector3(4.4f, CameraHeight, RoomLength - 0.4f), new Vector3(1.8f, 0f, 14.5f));
        FacilityBuilder.AddCamera(level, cams, "CAM 05", roulette, new Vector3(5.2f, CameraHeight, 17.2f), new Vector3(10f, 0f, 15.6f));
        FacilityBuilder.AddCamera(level, cams, "CAM 06", front, new Vector3(RoomWidth - 0.4f, CameraHeight, 8.2f), new Vector3(9f, 0f, 5.6f));
        FacilityBuilder.AddCamera(level, cams, "CAM 07", back, new Vector3(RoomWidth - 0.4f, CameraHeight, RoomLength - 0.4f), new Vector3(9f, 0f, 19.4f));
        FacilityBuilder.AddCamera(level, cams, "CAM 08", gallery, new Vector3(4.4f, CameraHeight, 4.3f), new Vector3(0.8f, 0f, 7.2f));

        // the close camera of the standing games (slots, roulette): moved to the game she is at (GameCameraDirector)
        var gameCamGo = new GameObject("GameCam");
        gameCamGo.transform.SetParent(cams, false);
        var gameCam = gameCamGo.AddComponent<SecurityCamera>();
        EditorSerialized.SetString(gameCam, SecurityCamera.LabelField, "LIVE");
        var gcSo = new SerializedObject(gameCam);
        gcSo.FindProperty(SecurityCamera.YawRangeField).floatValue = 15f;
        gcSo.FindProperty("fieldOfView").floatValue = 55f;
        gcSo.ApplyModifiedPropertiesWithoutUndo();
        gameCamGo.SetActive(false);
        level.Cameras.Add(gameCam);
        content.GameCamera = gameCam;

        // the elevator hall: 4 elevators side by side on the south wall (edit Data/Elevators/Stop_*.asset for names / keys)
        var elevators = new GameObject("Elevators").transform;
        elevators.SetParent(root.transform, false);
        string[] destinations = { ElevatorBuilder.FacilityOfficeStop, ElevatorBuilder.LaboratoryStop, ElevatorBuilder.HospitalStop, ElevatorBuilder.RooftopStop };
        float bayWidth = RoomWidth / destinations.Length;
        for (int i = 0; i < destinations.Length; i++)
        {
            SecurityCamera watch = i < 2 ? westHall : eastHall;
            Elevator e = ElevatorBuilder.PlaceElevator(elevators, lit, ElevatorBuilder.CasinoStop, destinations[i], i + 1,
                                                       new Vector3(bayWidth * (i + 0.5f), 0f, CasinoHallBuilder.ElevatorFacadeZ), 0f, bayWidth,
                                                       CasinoHallBuilder.ElevatorHeight, watch);
            if (e != null) content.Elevators.Add(e);
        }

        // card games: Hold'em in the south row (the dealer faces south), blackjack in the north row (faces north)
        var games = new GameObject("CardTables").transform;
        games.SetParent(root.transform, false);
        foreach (float x in TableX)
        {
            BlackjackTable he = BlackjackBuilder.PlaceCasinoTable(games, new Vector3(x, 0f, HoldemSeatZ), 180f, true);
            if (he != null)
            {
                BlackjackBuilder.AddTableCamera(he, center, level.Cameras);
                content.HoldemTables.Add(he.GetComponent<HoldemGame>());
            }
            BlackjackTable bj = BlackjackBuilder.PlaceCasinoTable(games, new Vector3(x, 0f, BlackjackSeatZ), 0f, false);
            if (bj != null)
            {
                BlackjackBuilder.AddTableCamera(bj, center, level.Cameras);
                content.Tables.Add(bj.GetComponent<BlackjackGame>());
            }
        }

        // the roulette: she stands west of the wheel as the croupier
        content.Roulette = BuildRoulette(root.transform, rouletteModel, lit);

        // the prize lockers (the gallery, west wall): one per prize, the outfit on a copy of her
        content.PrizeCases.AddRange(BuildPrizeCases(root.transform, lit));

        // the cashier / prize counter at the bar (where "go to the bar" takes her)
        var counterGo = new GameObject("PrizeCounter");
        counterGo.transform.SetParent(root.transform, false);
        counterGo.transform.position = bar.Spot.position;
        var counter = counterGo.AddComponent<PrizeCounter>();
        EditorSerialized.SetReference(counter, PrizeCounter.SpotField, bar.Spot);
        content.Counter = counter;

        // a save point at the west wall, between the gallery and the bar (the casino is a safe zone)
        var savePoints = new GameObject("SavePoints");
        SaveStation station = SavePointBuilder.PlaceSavePoint(savePoints.transform, "SavePoint_Office", new Vector3(0.8f, 0f, 10.9f), 90f);
        if (station != null) content.Stations.Add(station);

        AddLights(root.transform);
        level.PartnerStart = new Vector3(bayWidth * 1.5f, 0f, 2.9f);   // in front of elevator 2 (playing this scene directly)
        level.PartnerStartYaw = 0f;
        content.Level = level;
        content.PlaceName = "CASINO";
        content.PlaceNamePt = "CASSINO";
        content.Description = $"Casino: {content.Slots.Count} slot machines, {content.Tables.Count} blackjack + {content.HoldemTables.Count} Hold'em tables, roulette {(content.Roulette != null ? "on" : "missing")}, cashier at the bar, {content.Elevators.Count} elevators.";
        return content;
    }

    // the sections: English name (= lexicon key), Portuguese name, area (x, z, width, depth), where she goes
    private static readonly (string en, string pt, Rect area, Vector2 spot)[] Sections =
    {
        ("Elevator Hall", "Hall dos Elevadores", new Rect(0f, 0f, RoomWidth, CasinoHallBuilder.HallDepth), new Vector2(8f, 2.9f)),
        ("Center", "Centro", new Rect(CasinoHallBuilder.WingX, 8f, RoomWidth - CasinoHallBuilder.WingX, 6f), new Vector2(10.4f, 11f)),    // between the card tables
        ("Bar", "Bar", new Rect(0f, 10.4f, CasinoHallBuilder.WingX, RoomLength - 10.4f), CasinoHallBuilder.CashierSpot),                   // at the cashier
        ("Roulette", "Roleta", new Rect(CasinoHallBuilder.WingX, 14f, RoomWidth - CasinoHallBuilder.WingX, 3.4f), new Vector2(8.2f, 14.6f)),
        ("Front Slots", "Máquinas da Frente", new Rect(CasinoHallBuilder.WingX, CasinoHallBuilder.HallDepth, RoomWidth - CasinoHallBuilder.WingX, 8f - CasinoHallBuilder.HallDepth), new Vector2(5.6f, 5.95f)),
        ("Back Slots", "Máquinas de Trás", new Rect(CasinoHallBuilder.WingX, 17.4f, RoomWidth - CasinoHallBuilder.WingX, RoomLength - 17.4f), new Vector2(5.6f, 19.7f)),
        ("Prize Gallery", "Galeria de Prêmios", new Rect(0f, CasinoHallBuilder.HallDepth, CasinoHallBuilder.WingX, 10.4f - CasinoHallBuilder.HallDepth), new Vector2(2.6f, 7f)),   // the lockers
    };

    // A named area with its map spot.
    private static Room AddRoom(Transform parent, string displayName, string displayNamePt, Rect r, Vector2 spotAt)
    {
        var go = new GameObject("Room_" + displayName.Replace(" ", ""));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(r.center.x, 0f, r.center.y);
        var room = go.AddComponent<Room>();
        EditorSerialized.SetString(room, Room.DisplayNameField, displayName);
        EditorSerialized.SetString(room, Room.DisplayNamePtField, displayNamePt);
        var so = new SerializedObject(room);
        so.FindProperty(Room.SizeField).vector2Value = r.size;
        so.FindProperty(Room.SafeZoneField).boolValue = true;   // no enemies in the casino: the operator may leave her anywhere
        so.ApplyModifiedPropertiesWithoutUndo();
        var spot = new GameObject("Spot").transform;
        spot.SetParent(go.transform, false);
        spot.position = new Vector3(spotAt.x, 0f, spotAt.y);
        EditorSerialized.SetReference(room, Room.SpotField, spot);
        if (!UnityEngine.AI.NavMesh.SamplePosition(spot.position, out _, 0.3f, UnityEngine.AI.NavMesh.AllAreas))
            Debug.LogWarning($"[Casino] The spot of '{displayName}' is not on the NavMesh: move Rooms/{go.name}/Spot.", spot);
        return room;
    }

    // two sections side by side (a shared edge)
    private static bool Touch(Room a, Room b)
    {
        Rect p = a.FloorRect, q = b.FloorRect;
        // Tolerance for "touching" edges (m).
        const float e = 0.05f;
        bool xOverlap = p.xMin < q.xMax - e && q.xMin < p.xMax - e;
        bool zOverlap = p.yMin < q.yMax - e && q.yMin < p.yMax - e;
        return (xOverlap && (Mathf.Abs(p.yMax - q.yMin) < e || Mathf.Abs(q.yMax - p.yMin) < e)) ||
               (zOverlap && (Mathf.Abs(p.xMax - q.xMin) < e || Mathf.Abs(q.xMax - p.xMin) < e));
    }

    // A model placed in the scene, every part solid (mesh colliders) except the named moving part (no collider).
    private static Transform PlaceModel(string path, Transform parent, string objectName, Vector3 position, float yaw, string movingPart = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = objectName;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            bool moving = (movingPart != null && mf.name == movingPart) || mf.name.StartsWith("Reel_");
            if (moving) continue;
            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic);
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        return go.transform;
    }

    // One machine: the model (solid, under Geometry) and the playable machine: its root in front of the SPIN button,
    // shifted so the button is at her right hand, forward = where she stands; the three reels of the model.
    private static SlotMachine BuildSlot(Transform parent, Transform models, int number, Vector3 at, float yaw)
    {
        Transform model = PlaceModel(SlotModelPath, models, $"SlotModel_{number:00}", at, yaw);
        var reels = new List<Renderer>();
        for (int r = 0; r < 3; r++)
        {
            Transform t = EditorScene.FindDeep(model, $"Reel_{r}");
            if (t == null) { Debug.LogWarning($"[Casino] Reel_{r} not in {SlotModelPath}: machine {number} skipped."); return null; }
            reels.Add(t.GetComponent<Renderer>());
        }
        // her right side (she faces the machine) = the machine's local -x
        var go = new GameObject($"Slot_{number:00}");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(model.TransformPoint(new Vector3(SpinButton.x + SpinToHerRight, 0f, SpinButton.z)), model.rotation);
        var spin = new GameObject("SpinButton").transform;
        spin.SetParent(go.transform, false);
        spin.localPosition = new Vector3(-SpinToHerRight, SpinButton.y, 0f);

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;   // heard through the camera's microphone
        audio.volume = 0.5f;

        var machine = go.AddComponent<SlotMachine>();
        EditorSerialized.SetReferenceArray(machine, SlotMachine.ReelsField, reels);
        EditorSerialized.SetReference(machine, SlotMachine.SpinButtonField, spin);
        EditorSerialized.SetReference(machine, SlotMachine.AudioSourceField, audio);
        EditorSerialized.SetInt(machine, SlotMachine.NumberField, number);
        return machine;
    }

    // Dim warm ambient (the chandeliers carry the light: CasinoHallBuilder), a weak sun for contact shadows, and a
    // soft colored glow over each slot bank.
    private static void AddLights(Transform root)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.1f, 0.09f);
        RenderSettings.skybox = null;
        var lights = new GameObject("Lights").transform;
        lights.SetParent(root, false);
        var sun = new GameObject("Directional Light").AddComponent<Light>();   // weak, only for soft contact shadows
        sun.transform.SetParent(lights, false);
        sun.type = LightType.Directional;
        sun.intensity = 0.2f;
        sun.color = new Color(1f, 0.85f, 0.75f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(70f, 20f, 0f);
        foreach (float z in BankZ)
            foreach (float x in new[] { 8.4f, 12.4f })
            {
                var l = new GameObject($"SlotGlow_{x:0.0}_{z:0.0}").AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.position = new Vector3(x, 3.2f, z);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.75f);
                l.intensity = 2.2f;
                l.range = 6f;
                l.shadows = LightShadows.None;
            }
    }

    // ------------------------------------------------------------------ roulette
    // RouletteGame at the rim, facing her (she stands west of the wheel); the layout cells in its space; the ball.
    // The felt spots were measured on the old casino's floor plan: W() moves them to where the table is now.
    private static RouletteGame BuildRoulette(Transform root, Transform model, Shader lit)
    {
        Transform rotor = model != null ? EditorScene.FindDeep(model, RotorName) : null;
        var chip = AssetDatabase.LoadAssetAtPath<GameObject>(BlackjackBuilder.ChipPrefabPath);
        if (rotor == null || chip == null)
        {
            Debug.LogWarning($"[Casino] No roulette: {(rotor == null ? RotorName + " not in the model" : "no chip prefab (run menu 3)")}.");
            return null;
        }
        var go = new GameObject("Roulette");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(WheelAxis + Vector3.left * 0.405f, Quaternion.LookRotation(Vector3.left));
        Transform t = go.transform;
        Vector3 shift = WheelAxis - OldWheelAxis;
        Vector3 L(float x, float z, float y = RouletteFelt) => t.InverseTransformPoint(new Vector3(x + shift.x, y, z + shift.z));
        Vector3 D(float x, float z) => t.InverseTransformDirection(new Vector3(x, 0f, z));

        var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballGo.name = "Ball";
        Object.DestroyImmediate(ballGo.GetComponent<Collider>());
        ballGo.transform.SetParent(root, false);
        ballGo.transform.localScale = Vector3.one * 0.018f;
        Material ballMat = EditorAssets.MakeMaterial($"{MatFolder}/M_RouletteBall.mat", null, 0.9f, false, lit);
        ballMat.SetColor(BaseColorId, new Color(0.95f, 0.95f, 0.93f));
        EditorUtility.SetDirty(ballMat);
        ballGo.GetComponent<Renderer>().sharedMaterial = ballMat;

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.6f;

        var game = go.AddComponent<RouletteGame>();
        EditorSerialized.SetReference(game, RouletteGame.RotorField, rotor);
        EditorSerialized.SetReference(game, RouletteGame.BallField, ballGo.transform);
        EditorSerialized.SetReference(game, RouletteGame.ChipPrefabField, chip);
        EditorSerialized.SetReference(game, RouletteGame.AudioSourceField, audio);
        var so = new SerializedObject(game);
        // cells of the felt print: numbers in 12 columns (x) by 3 rows (z; 1, 4, 7... is the row nearest the player)
        so.FindProperty(RouletteGame.NumberOneField).vector3Value = L(5.732f, 12.92f);
        so.FindProperty(RouletteGame.ColumnStepField).vector3Value = D(0.1033f, 0f);
        so.FindProperty(RouletteGame.RowStepField).vector3Value = D(0f, 0.106f);
        so.FindProperty(RouletteGame.ZeroSpotField).vector3Value = L(5.645f, 13.025f);
        SerializedProperty dozens = so.FindProperty(RouletteGame.DozenSpotsField);
        dozens.arraySize = 3;
        float[] dozenX = { 5.89f, 6.30f, 6.71f };
        for (int i = 0; i < 3; i++) dozens.GetArrayElementAtIndex(i).vector3Value = L(dozenX[i], 12.79f);
        SerializedProperty outside = so.FindProperty(RouletteGame.OutsideSpotsField);
        outside.arraySize = 6;
        float[] outsideX = { 5.78f, 5.99f, 6.19f, 6.40f, 6.61f, 6.82f };   // 1-18, EVEN, RED, BLACK, ODD, 19-36
        for (int i = 0; i < 6; i++) outside.GetArrayElementAtIndex(i).vector3Value = L(outsideX[i], 12.69f);
        so.FindProperty(RouletteGame.HouseSpotField).vector3Value = L(5.9f, 13.38f);
        so.FindProperty(RouletteGame.PlayerSpotField).vector3Value = L(6.3f, 12.42f, RouletteFelt + 0.01f);
        // the game camera: a player's view from the south-east corner of the wheel, on her and the wheel
        so.FindProperty(RouletteGame.ViewFromField).vector3Value = L(5.75f, 11.45f, 1.75f);
        so.FindProperty(RouletteGame.ViewAtField).vector3Value = L(4.95f, 12.5f, 1.0f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return game;
    }

    // ------------------------------------------------------------------ art
    // The models' materials, remapped on import: the tables and the slot machine (one baked texture each, reels on
    // T_SlotReels), the roulette (the old casino's T_Casino_05 / 06 / 10 and the rotor's T_RouletteRotor).
    private static void ImportArt(Shader lit)
    {
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        var reelTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_SlotReels.png");
        if (AssetImporter.GetAtPath($"{ArtFolder}/T_SlotReels.png") is TextureImporter ti && ti.wrapMode != TextureWrapMode.Repeat)
        {
            ti.wrapMode = TextureWrapMode.Repeat;   // the reels scroll their texture
            ti.SaveAndReimport();
        }
        Material Mat(string name, string tex, float smooth) =>
            EditorAssets.MakeMaterial($"{MatFolder}/{name}.mat", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{tex}.png"), smooth, false, lit);
        Material reels = EditorAssets.MakeMaterial($"{MatFolder}/M_SlotReels.mat", reelTex, 0.5f, false, lit);
        Remap(SlotModelPath, ("M_CasinoSlotMachine", Mat("M_CasinoSlotMachine", "T_CasinoSlotMachine", 0.45f)), ("M_SlotReels", reels));
        Remap(BlackjackBuilder.CasinoBlackjackModelPath, ("M_CasinoBlackjackTable", Mat("M_CasinoBlackjackTable", "T_CasinoBlackjackTable", 0.3f)));
        Remap(BlackjackBuilder.CasinoPokerModelPath, ("M_CasinoPokerTable", Mat("M_CasinoPokerTable", "T_CasinoPokerTable", 0.3f)));
        Remap(RouletteModelPath, ("M_Casino_05", Mat("M_Casino_05", "T_Casino_05", 0.35f)), ("M_Casino_06", Mat("M_Casino_06", "T_Casino_06", 0.35f)),
              ("M_Casino_10", Mat("M_Casino_10", "T_Casino_10", 0.35f)), ("M_RouletteRotor", Mat("M_RouletteRotor", "T_RouletteRotor", 0.6f)));
    }

    // Points a model's materials (by name in the FBX) at project materials, and reimports it.
    private static void Remap(string modelPath, params (string name, Material material)[] maps)
    {
        if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter mi)) return;
        foreach (var (name, material) in maps) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        mi.SaveAndReimport();
    }

    // ------------------------------------------------------------------ prize lockers
    // The prize lockers along the gallery's wall (one per outfit with a price, cheapest first).
    private static List<PrizeCase> BuildPrizeCases(Transform root, Shader lit)
    {
        var cases = new List<PrizeCase>();
        var prizes = new List<OutfitDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:OutfitDefinition", new[] { "Assets/Data/Outfits" }))
        {
            var o = AssetDatabase.LoadAssetAtPath<OutfitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (o != null && o.Price > 0) prizes.Add(o);
        }
        prizes.Sort((a, b) => a.Price.CompareTo(b.Price));
        if (prizes.Count == 0) { Debug.LogWarning("[Casino] No outfit with a price in Assets/Data/Outfits (run menu 4 first): no prize lockers."); return cases; }
        if (prizes.Count > CaseZ.Length) Debug.LogWarning($"[Casino] {prizes.Count} prizes, room for {CaseZ.Length} lockers: the rest are sold at the cashier.");

        Material wood = EditorAssets.MakeMaterial($"{MatFolder}/M_CaseWood.mat", null, 0.55f, false, lit);
        wood.SetColor(BaseColorId, new Color(0.22f, 0.12f, 0.06f));
        Material velvet = EditorAssets.MakeMaterial($"{MatFolder}/M_CaseVelvet.mat", null, 0.15f, false, lit);
        velvet.SetColor(BaseColorId, new Color(0.35f, 0.04f, 0.06f));
        Material brass = EditorAssets.MakeMaterial($"{MatFolder}/M_CaseBrass.mat", null, 0.8f, false, lit);
        brass.SetColor(BaseColorId, new Color(0.8f, 0.6f, 0.25f));
        brass.SetFloat("_Metallic", 0.9f);
        Material glass = TransparentMaterial($"{MatFolder}/M_CaseGlass.mat", null, lit, false);
        glass.SetColor(BaseColorId, new Color(0.75f, 0.9f, 0.95f, 0.16f));
        glass.SetFloat("_Smoothness", 0.95f);
        foreach (Material m in new[] { wood, velvet, brass, glass }) EditorUtility.SetDirty(m);

        var parent = new GameObject("PrizeLockers").transform;
        parent.SetParent(root, false);
        for (int i = 0; i < prizes.Count && i < CaseZ.Length; i++)
            cases.Add(BuildCase(parent, prizes[i], new Vector3(CaseDepth * 0.5f + 0.06f, 0f, CaseZ[i]), lit, wood, velvet, brass, glass));
        return cases;
    }

    // One locker (origin on the floor at its center, forward = out to the room): wooden base and top, velvet back,
    // brass posts, glass sides and a glass door on a hinge, the plate (name + price), a lamp, a copy of her inside.
    private static PrizeCase BuildCase(Transform parent, OutfitDefinition prize, Vector3 position, Shader lit,
                                       Material wood, Material velvet, Material brass, Material glass)
    {
        var go = new GameObject("Locker_" + prize.Key);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.right));   // out of the west wall
        Transform t = go.transform;
        float w = CaseWidth * 0.5f, d = CaseDepth * 0.5f, inner = CaseHeight - 0.3f;
        Part(t, "Base", new Vector3(0f, 0.09f, 0f), new Vector3(CaseWidth, 0.18f, CaseDepth), wood);
        Part(t, "Top", new Vector3(0f, CaseHeight - 0.06f, 0f), new Vector3(CaseWidth, 0.12f, CaseDepth), wood);
        Part(t, "Back", new Vector3(0f, 0.18f + inner * 0.5f, -d + 0.02f), new Vector3(CaseWidth - 0.04f, inner, 0.04f), velvet);
        foreach (float x in new[] { -w + 0.02f, w - 0.02f })
            foreach (float z in new[] { -d + 0.02f, d - 0.02f })
                Part(t, "Post", new Vector3(x, 0.18f + inner * 0.5f, z), new Vector3(0.04f, inner, 0.04f), brass);
        foreach (float x in new[] { -w + 0.02f, w - 0.02f })
            Part(t, "SideGlass", new Vector3(x, 0.18f + inner * 0.5f, 0f), new Vector3(0.01f, inner, CaseDepth - 0.08f), glass, true);
        Part(t, "Pedestal", new Vector3(0f, 0.2f, -0.03f), new Vector3(0.55f, 0.04f, 0.45f), brass);
        // the door: on a hinge at one side, opens outward
        var hinge = new GameObject("Door").transform;
        hinge.SetParent(t, false);
        hinge.localPosition = new Vector3(-w + 0.04f, 0f, d - 0.02f);
        Part(hinge, "Glass", new Vector3(CaseWidth * 0.5f - 0.04f, 0.18f + inner * 0.5f, 0f), new Vector3(CaseWidth - 0.08f, inner - 0.02f, 0.01f), glass, true);
        Part(hinge, "Handle", new Vector3(CaseWidth - 0.14f, 1.1f, 0.02f), new Vector3(0.03f, 0.18f, 0.02f), brass, true);
        // the coin slot (a survival horror coin cage), the plate
        Part(t, "CoinSlot", new Vector3(w - 0.02f, 1.1f, d + 0.012f), new Vector3(0.07f, 0.11f, 0.02f), brass, true);
        Part(t, "PlateBack", new Vector3(0f, 0.09f, d + 0.006f), new Vector3(0.72f, 0.14f, 0.01f), brass, true);
        var textGo = new GameObject("Plate");
        textGo.transform.SetParent(t, false);
        textGo.transform.localPosition = new Vector3(0f, 0.09f, d + 0.012f);
        textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TMP is read along its +z: turned to face the room
        var text = textGo.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(0.68f, 0.12f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 0.6f;
        text.color = new Color(0.12f, 0.07f, 0.02f);
        text.text = prize.Key;

        var lamp = new GameObject("Lamp").AddComponent<Light>();
        lamp.transform.SetParent(t, false);
        lamp.transform.localPosition = new Vector3(0f, CaseHeight - 0.25f, 0.1f);
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.88f, 0.7f);
        lamp.intensity = 1.3f;
        lamp.range = 2.2f;
        lamp.shadows = LightShadows.None;

        GameObject mannequin = HelenaSetupMenu.BuildMannequin(t, lit, prize);
        if (mannequin != null) mannequin.transform.localPosition = new Vector3(0f, 0.22f, -0.05f);

        var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, CaseHeight * 0.5f, 0f);
        obstacle.size = new Vector3(CaseWidth, CaseHeight, CaseDepth);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        var spot = new GameObject("Spot").transform;   // where she stands to buy it
        spot.SetParent(t, false);
        spot.localPosition = new Vector3(0f, 0f, d + 0.6f);

        var locker = go.AddComponent<PrizeCase>();
        EditorSerialized.SetReference(locker, PrizeCase.PrizeField, prize);
        EditorSerialized.SetReference(locker, PrizeCase.DoorField, hinge);
        EditorSerialized.SetReference(locker, PrizeCase.PlateField, text);
        EditorSerialized.SetReference(locker, PrizeCase.SpotField, spot);
        return locker;
    }

    // A box part with a material (and collider when asked).
    private static GameObject Part(Transform parent, string objectName, Vector3 localCenter, Vector3 size, Material m, bool noCollider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (noCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // A transparent (alpha / additive) material from a texture.
    internal static Material TransparentMaterial(string path, Texture2D tex, Shader shader, bool additive)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.SetTexture(BaseMapId, tex);
        m.SetColor(BaseColorId, Color.white);
        m.SetFloat("_Surface", 1f);                         // transparent
        m.SetFloat("_Blend", additive ? 2f : 0f);           // additive / alpha
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ HUDs (rig)
    // A small panel on the right (like the blackjack one): header, body, prompt. Only one shows at a time.
    private static CanvasGroup Panel(Transform canvas, string objectName, Color back, Color gold, out TMP_Text header, out TMP_Text body, out TMP_Text prompt)
    {
        var root = EditorUi.CreateUiObject(canvas, objectName);
        root.transform.SetSiblingIndex(1);
        Vector2 rc = new Vector2(1f, 0.5f);
        EditorUi.Place((RectTransform)root.transform, rc, rc, rc, new Vector2(-24f, 0f), new Vector2(330f, 170f));
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image bg = EditorUi.CreateUiObject(root.transform, "Back").AddComponent<Image>();
        EditorUi.Stretch(bg.rectTransform);
        bg.color = back;
        bg.raycastTarget = false;
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(1f, -1f);
        Vector2 tl = new Vector2(0f, 1f), bl = Vector2.zero;
        header = EditorUi.CreateText(root.transform, "Header", 17, TextAlignmentOptions.Left, gold);
        EditorUi.Place(header.rectTransform, tl, tl, tl, new Vector2(12f, -8f), new Vector2(306f, 24f));
        body = EditorUi.CreateText(root.transform, "Body", 15, TextAlignmentOptions.TopLeft, Color.white);
        EditorUi.Place(body.rectTransform, tl, tl, tl, new Vector2(12f, -36f), new Vector2(306f, 100f));
        prompt = EditorUi.CreateText(root.transform, "Prompt", 12, TextAlignmentOptions.BottomLeft, gold);
        EditorUi.Place(prompt.rectTransform, bl, bl, bl, new Vector2(12f, 6f), new Vector2(306f, 24f));
        return group;
    }

    // Accent color of the casino panels.
    private static readonly Color Gold = new Color(0.95f, 0.78f, 0.36f);

    // the slot machine panel
    internal static void BuildHud(Transform canvas, PartnerKeypadUser keypadUser, ChipBank bank, CameraSettings settings)
    {
        CanvasGroup group = Panel(canvas, "CasinoHud", new Color(0.12f, 0.03f, 0.04f, 0.75f), Gold, out TMP_Text header, out TMP_Text body, out TMP_Text prompt);
        var hud = group.gameObject.AddComponent<CasinoHud>();
        EditorSerialized.SetReference(hud, CasinoHud.KeypadUserField, keypadUser);
        EditorSerialized.SetReference(hud, CasinoHud.BankField, bank);
        EditorSerialized.SetReference(hud, CasinoHud.SettingsField, settings);
        EditorSerialized.SetReference(hud, CasinoHud.GroupField, group);
        EditorSerialized.SetReference(hud, CasinoHud.HeaderField, header);
        EditorSerialized.SetReference(hud, CasinoHud.BodyField, body);
        EditorSerialized.SetReference(hud, CasinoHud.PromptField, prompt);
    }

    // Builds the hold'em side panel.
    internal static void BuildHoldemHud(Transform canvas, List<HoldemGame> games, CameraSettings settings)
    {
        CanvasGroup group = Panel(canvas, "HoldemHud", new Color(0.02f, 0.16f, 0.08f, 0.75f), Gold, out TMP_Text header, out TMP_Text body, out TMP_Text prompt);
        var hud = group.gameObject.AddComponent<HoldemHud>();
        EditorSerialized.SetReferenceArray(hud, HoldemHud.GamesField, games);
        EditorSerialized.SetReference(hud, HoldemHud.SettingsField, settings);
        EditorSerialized.SetReference(hud, HoldemHud.GroupField, group);
        EditorSerialized.SetReference(hud, HoldemHud.HeaderField, header);
        EditorSerialized.SetReference(hud, HoldemHud.BodyField, body);
        EditorSerialized.SetReference(hud, HoldemHud.PromptField, prompt);
    }

    // Builds the roulette side panel.
    internal static void BuildRouletteHud(Transform canvas, RouletteGame game, CameraSettings settings)
    {
        CanvasGroup group = Panel(canvas, "RouletteHud", new Color(0.02f, 0.16f, 0.08f, 0.75f), Gold, out TMP_Text header, out TMP_Text body, out TMP_Text prompt);
        var hud = group.gameObject.AddComponent<RouletteHud>();
        EditorSerialized.SetReference(hud, RouletteHud.GameField, game);
        EditorSerialized.SetReference(hud, RouletteHud.SettingsField, settings);
        EditorSerialized.SetReference(hud, RouletteHud.GroupField, group);
        EditorSerialized.SetReference(hud, RouletteHud.HeaderField, header);
        EditorSerialized.SetReference(hud, RouletteHud.BodyField, body);
        EditorSerialized.SetReference(hud, RouletteHud.PromptField, prompt);
    }

    // Builds the prize counter side panel.
    internal static void BuildCounterHud(Transform canvas, PrizeCounter counter, Transform partner, CameraSettings settings)
    {
        CanvasGroup group = Panel(canvas, "PrizeCounterHud", new Color(0.12f, 0.08f, 0.02f, 0.78f), Gold, out TMP_Text header, out TMP_Text body, out TMP_Text prompt);
        var hud = group.gameObject.AddComponent<PrizeCounterHud>();
        EditorSerialized.SetReference(hud, PrizeCounterHud.CounterField, counter);
        EditorSerialized.SetReference(hud, PrizeCounterHud.PartnerField, partner);
        EditorSerialized.SetReference(hud, PrizeCounterHud.SettingsField, settings);
        EditorSerialized.SetReference(hud, PrizeCounterHud.GroupField, group);
        EditorSerialized.SetReference(hud, PrizeCounterHud.HeaderField, header);
        EditorSerialized.SetReference(hud, PrizeCounterHud.BodyField, body);
        EditorSerialized.SetReference(hud, PrizeCounterHud.PromptField, prompt);
    }

    // the games, the counter and the roulette bets (understood in every scene: "let's play poker")
    internal static IEnumerable<(string key, string[] en, string[] pt)> GameNames()
    {
        yield return (CommandTargets.BlackjackKey, new[] { "blackjack", "black jack" }, new[] { "blackjack", "black jack" });
        yield return (CommandTargets.PokerKey, new[] { "poker", "hold em", "holdem", "casino hold em", "texas hold em" },
                                               new[] { "poker", "poquer", "hold em", "holdem" });
        yield return (CommandTargets.RouletteKey, new[] { "roulette" }, new[] { "roleta" });
        yield return (CommandTargets.CounterKey, new[] { "cashier", "cash desk", "prize counter" }, new[] { "caixa", "balcao de premios" });
        yield return (CommandTargets.EvenKey, new[] { "even" }, new[] { "par", "pares" });
        yield return (CommandTargets.OddKey, new[] { "odd" }, new[] { "impar", "impares" });
        yield return (CommandTargets.LowKey, new[] { "low", "first half", "low numbers" }, new[] { "baixo", "baixos", "numeros baixos", "primeira metade" });
        yield return (CommandTargets.HighKey, new[] { "high", "second half", "high numbers" }, new[] { "alto", "altos", "numeros altos", "segunda metade" });
        yield return (CommandTargets.Dozen1Key, new[] { "first dozen", "first twelve" }, new[] { "primeira duzia", "primeira duzia de numeros" });
        yield return (CommandTargets.Dozen2Key, new[] { "second dozen", "second twelve", "middle dozen" }, new[] { "segunda duzia", "duzia do meio" });
        yield return (CommandTargets.Dozen3Key, new[] { "third dozen", "third twelve", "last dozen" }, new[] { "terceira duzia", "ultima duzia" });
        yield return (CommandTargets.NumberBetKey, new[] { "number", "straight", "straight up" }, new[] { "numero", "pleno", "no numero" });
        yield return (CommandTargets.ZeroKey, new[] { "zero" }, new[] { "zero" });
    }

    // lexicon names of the casino's rooms and things
    internal static IEnumerable<(string key, string[] en, string[] pt)> LexiconNames()
    {
        yield return ("Elevator Hall", new[] { "elevator hall", "elevator lobby", "elevators area" },
                                       new[] { "hall dos elevadores", "saguao dos elevadores", "area dos elevadores" });
        yield return ("Center", new[] { "casino center", "center of the casino", "middle of the casino", "card tables", "tables", "table area" },
                                new[] { "centro do cassino", "meio do cassino", "mesas", "area das mesas", "mesas de cartas" });
        yield return ("Bar", new[] { "bar", "the bar", "counter" }, new[] { "bar", "o bar", "balcao" });
        yield return ("Prize Gallery", new[] { "prize gallery", "gallery", "prizes", "the prizes", "lockers", "prize lockers", "showcases" },
                                       new[] { "galeria de premios", "galeria", "premios", "os premios", "armarios", "vitrines", "armarios de premios" });
        yield return ("Roulette", new[] { "roulette", "the roulette", "roulette table", "wheel" }, new[] { "roleta", "a roleta", "mesa da roleta" });
        yield return ("Front Slots", new[] { "front slots", "front machines", "first machines" },
                                     new[] { "maquinas da frente", "primeiras maquinas", "caca niqueis da frente" });
        // no modifier words inside ("far", "do fundo" are farthest): the modifier would take them first
        yield return ("Back Slots", new[] { "back slots", "back machines", "back of the casino", "the back" },
                                    new[] { "maquinas de tras", "fundo", "fundo do cassino", "la no fundo", "caca niqueis de tras" });
        yield return (CommandTargets.ElevatorKey, new[] { "elevator", "elevators", "lift" }, new[] { "elevador", "elevadores" });
        yield return (CommandTargets.SlotKey, new[] { "slot machine", "slot machines", "slots", "slot", "machine", "one armed bandit", "the slots" },
                                              new[] { "caca niquel", "caca niqueis", "maquina", "maquininha", "slot", "slots", "maquina de caca niquel" });
    }
}
