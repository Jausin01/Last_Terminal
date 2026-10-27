using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu "Last Terminal/8 - Build Casino Scene": the casino (its own scene, reached by elevator). The model
// (SM_Casino: room, bar, roulette, tables, 48 slot machines with separate reels), its materials, colliders and
// NavMesh, lights, rooms + CCTV cameras, the elevator hall (4 elevators), the playable slot machines and a save point.
// Helena and her systems come from the shared rig (LocationRigBuilder.BuildLocation).
// Plan (meters, x east, z north), sections: ELEVATOR HALL z[0,3.9] (elevators on the south wall) | CENTER z[3.9,10.3]
//   (card tables) | BAR x[0,3.8] + ROULETTE z[10.3,15.3] | FRONT SLOTS z[15.3,18.8] | BACK SLOTS z[18.8,25.83]   (x[0,12.33])
public static class CasinoBuilder
{
    // Scene, NavMesh, art and material paths.
    internal const string SceneName = "Casino";
    private const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
    private const string NavMeshPath = "Assets/Scenes/" + SceneName + "_NavMesh.asset";
    private const string ArtFolder = "Assets/Art/Props/Casino";
    private const string ModelPath = ArtFolder + "/SM_Casino.fbx";
    private const string MatFolder = ArtFolder + "/Materials";
    private const int MaterialCount = 11;                         // M_Casino_00 .. 10 (T_Casino_00 .. 10)
    private static readonly int[] GlowMaterials = { 1 };         // light halos: additive
    private static readonly int[] ShadowMaterials = { 7 };       // the baked floor shadows: alpha blended
    private const int SlotCount = 48;                             // Reel_<ii>_<0..2>; rows of 12 face south, north, south, north
    // Room size (m), CCTV camera height (m).
    private const float RoomWidth = 12.33f;
    private const float RoomLength = 25.83f;
    private const float CameraHeight = 3.4f;
    private const float ElevatorDoorZ = 1.8f;                     // the facade of the elevator hall
    // Elevator height (m); name of the roulette wheel's spinning part in the model.
    private const float ElevatorHeight = 3.4f;
    private const string RotorName = "RouletteRotor";
    // card tables (measured on the model): felt height, x of the three tables of a row, the dealer's seat of each row
    // (0.30 m behind the flat edge of the half-moon: north row faces north, south row faces south)
    private const float FeltHeight = 0.736f;
    private static readonly float[] TableX = { 2.27f, 6.17f, 10.06f };
    private const float NorthSeatZ = 7.92f, SouthSeatZ = 6.15f;
    // roulette (world, measured on the felt print): the wheel's axis, her finger on the rim, the layout cells
    private static readonly Vector3 WheelAxis = new Vector3(5.285f, 0f, 12.552f);
    private const float RouletteFelt = 0.843f;

    // URP Lit property ids.
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Menu 8: builds the whole casino scene.
    [MenuItem("Last Terminal/8 - Build Casino Scene")]
    public static void BuildCasinoScene()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogError($"[Casino] {ModelPath} not found: copy the casino art first.");
            return;
        }
        LocationRigBuilder.BuildLocation(ScenePath, NavMeshPath, BuildContent);
    }

    // ------------------------------------------------------------------ content
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

        // the model: materials, colliders on everything solid
        ImportArt(lit);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
        model.name = "Model";
        model.transform.SetParent(geometry, false);
        foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>())
        {
            bool reel = r.name.StartsWith("Reel_") || r.name == RotorName;   // they move: not static, no collider
            bool decal = IsTransparent(r.sharedMaterial);
            if (!reel) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            if (decal) { r.shadowCastingMode = ShadowCastingMode.Off; continue; }
            if (reel) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        // NavMesh from the colliders (the elevators, save point... carve their own holes at runtime)
        FacilityBuilder.EnsureBakeRadius(FacilityBuilder.PartnerRadius);
        var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        level.Surface = surface;

        // sections: the casino is one hall cut in places easy to name, each with a clear spot to send her to
        // (marked on the map). All joined without doors: from any of them she walks to any other.
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

        // cameras: two on the columns watching the elevators, one per section
        var cams = new GameObject("SecurityCameras").transform;
        cams.SetParent(root.transform, false);
        level.StartCamera = 0;
        SecurityCamera westHall = FacilityBuilder.AddCamera(level, cams, "CAM 01", hall, new Vector3(4f, CameraHeight, 6.8f), new Vector3(2.6f, 0f, 2.2f));
        SecurityCamera eastHall = FacilityBuilder.AddCamera(level, cams, "CAM 02", hall, new Vector3(8f, CameraHeight, 6.8f), new Vector3(9.6f, 0f, 2.2f));
        FacilityBuilder.AddCamera(level, cams, "CAM 03", center, new Vector3(RoomWidth - 0.4f, CameraHeight, 10f), new Vector3(5f, 0f, 6.5f));
        FacilityBuilder.AddCamera(level, cams, "CAM 04", bar, new Vector3(0.4f, CameraHeight, 10.6f), new Vector3(2.8f, 0f, 13.6f));
        FacilityBuilder.AddCamera(level, cams, "CAM 05", roulette, new Vector3(RoomWidth - 0.4f, CameraHeight, 15f), new Vector3(5.5f, 0f, 12.2f));
        FacilityBuilder.AddCamera(level, cams, "CAM 06", front, new Vector3(0.4f, CameraHeight, 15.6f), new Vector3(7f, 0f, 17.6f));
        FacilityBuilder.AddCamera(level, cams, "CAM 07", back, new Vector3(RoomWidth - 0.4f, CameraHeight, RoomLength - 0.4f), new Vector3(5f, 0f, 21f));
        FacilityBuilder.AddCamera(level, cams, "CAM 08", gallery, new Vector3(GalleryX + 0.15f, CameraHeight, 10.45f), new Vector3(12f, 0f, 13f));

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
        float bay = RoomWidth / destinations.Length;
        for (int i = 0; i < destinations.Length; i++)
        {
            SecurityCamera watch = i < 2 ? westHall : eastHall;
            Elevator e = ElevatorBuilder.PlaceElevator(elevators, lit, ElevatorBuilder.CasinoStop, destinations[i], i + 1,
                                                       new Vector3(bay * (i + 0.5f), 0f, ElevatorDoorZ), 0f, bay, ElevatorHeight, watch);
            if (e != null) content.Elevators.Add(e);
        }

        // the slot machines (their reels are in the model)
        var machines = new GameObject("SlotMachines").transform;
        machines.SetParent(root.transform, false);
        Material button = ButtonMaterial(lit);
        for (int i = 0; i < SlotCount; i++)
        {
            SlotMachine m = BuildSlot(machines, model.transform, i, button);
            if (m != null) content.Slots.Add(m);
        }

        // card games on four of the six tables (the middle ones stay free: the center aisle is the walkway)
        var games = new GameObject("CardTables").transform;
        games.SetParent(root.transform, false);
        foreach (int i in new[] { 0, 2 })
        {
            BlackjackTable bj = BlackjackBuilder.PlaceCasinoTable(games, new Vector3(TableX[i], 0f, NorthSeatZ), 0f, FeltHeight, false);
            if (bj == null) continue;
            BlackjackBuilder.AddTableCamera(bj, center, level.Cameras);
            content.Tables.Add(bj.GetComponent<BlackjackGame>());
            BlackjackTable he = BlackjackBuilder.PlaceCasinoTable(games, new Vector3(TableX[i], 0f, SouthSeatZ), 180f, FeltHeight, true);
            if (he == null) continue;
            BlackjackBuilder.AddTableCamera(he, center, level.Cameras);
            content.HoldemTables.Add(he.GetComponent<HoldemGame>());
        }

        // the roulette: she stands west of the wheel as the croupier
        content.Roulette = BuildRoulette(root.transform, model.transform, lit);

        // the prize lockers (the gallery, east wall, around the pilaster): one per prize, the outfit on a copy of her
        content.PrizeCases.AddRange(BuildPrizeCases(root.transform, lit));

        // the cashier / prize counter at the bar (where "go to the bar" takes her)
        var counterGo = new GameObject("PrizeCounter");
        counterGo.transform.SetParent(root.transform, false);
        counterGo.transform.position = bar.Spot.position;
        var counter = counterGo.AddComponent<PrizeCounter>();
        EditorSerialized.SetReference(counter, PrizeCounter.SpotField, bar.Spot);
        content.Counter = counter;

        // a save point beside the bar (the casino is a safe zone)
        var savePoints = new GameObject("SavePoints");
        SaveStation station = SavePointBuilder.PlaceSavePoint(savePoints.transform, "SavePoint_Office", new Vector3(0.8f, 0f, 15.5f), 90f);
        if (station != null) content.Stations.Add(station);

        AddLights(root.transform);
        level.PartnerStart = new Vector3(bay * 1.5f, 0f, 2.9f);   // in front of elevator 2 (playing this scene directly)
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
        ("Elevator Hall", "Hall dos Elevadores", new Rect(0f, 0f, RoomWidth, 3.9f), new Vector2(6.17f, 2.9f)),
        ("Center", "Centro", new Rect(0f, 3.9f, RoomWidth, 6.4f), new Vector2(6.17f, 7.1f)),          // between the card tables
        ("Bar", "Bar", new Rect(0f, 10.3f, 3.8f, 5f), new Vector2(3.2f, 12.9f)),
        ("Roulette", "Roleta", new Rect(3.8f, 10.3f, GalleryX - 3.8f, 5f), new Vector2(6.2f, 11.15f)),
        ("Front Slots", "Máquinas da Frente", new Rect(0f, 15.3f, RoomWidth, 3.5f), new Vector2(6.17f, 17.6f)),
        ("Back Slots", "Máquinas de Trás", new Rect(0f, 18.8f, RoomWidth, RoomLength - 18.8f), new Vector2(6.17f, 22.8f)),
        ("Prize Gallery", "Galeria de Prêmios", new Rect(GalleryX, 10.3f, RoomWidth - GalleryX, 5f), new Vector2(10.4f, 12.95f)),   // the lockers
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

    // One machine: its root at the front of the cabinet, forward = where the player stands; the SPIN button below the
    // reels on the player's right (her finger goes there); the three reels from the model.
    private static SlotMachine BuildSlot(Transform parent, Transform model, int index, Material buttonMat)
    {
        var reels = new List<Renderer>();
        for (int r = 0; r < 3; r++)
        {
            Transform t = EditorScene.FindDeep(model, $"Reel_{index:00}_{r}");
            if (t == null) { Debug.LogWarning($"[Casino] Reel_{index:00}_{r} not found in the model: machine {index + 1} skipped."); return null; }
            reels.Add(t.GetComponent<Renderer>());
        }
        float facing = (index / 12) % 2 == 0 ? -1f : 1f;   // rows of 12: south, north, south, north
        Vector3 mid = reels[1].transform.position;
        var go = new GameObject($"Slot_{index + 1:00}");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(mid.x, 0f, mid.z + facing * 0.13f), Quaternion.LookRotation(new Vector3(0f, 0f, facing)));

        // her right side (she faces the machine) = the machine's local -x
        var spin = new GameObject("SpinButton").transform;
        spin.SetParent(go.transform, false);
        spin.localPosition = new Vector3(-0.09f, 0.86f, 0f);
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Cap";
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(spin, false);
        cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cap.transform.localScale = new Vector3(0.045f, 0.006f, 0.045f);
        cap.GetComponent<Renderer>().sharedMaterial = buttonMat;

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;   // heard through the camera's microphone
        audio.volume = 0.5f;

        var machine = go.AddComponent<SlotMachine>();
        EditorSerialized.SetReferenceArray(machine, SlotMachine.ReelsField, reels);
        EditorSerialized.SetReference(machine, SlotMachine.SpinButtonField, spin);
        EditorSerialized.SetReference(machine, SlotMachine.AudioSourceField, audio);
        EditorSerialized.SetInt(machine, SlotMachine.NumberField, index + 1);
        return machine;
    }

    // Ceiling lights, glows and accent lights.
    private static void AddLights(Transform root)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.17f, 0.14f);
        RenderSettings.skybox = null;
        var lights = new GameObject("Lights").transform;
        lights.SetParent(root, false);
        var sun = new GameObject("Directional Light").AddComponent<Light>();   // weak, only for soft contact shadows
        sun.transform.SetParent(lights, false);
        sun.type = LightType.Directional;
        sun.intensity = 0.25f;
        sun.color = new Color(1f, 0.9f, 0.8f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(70f, 20f, 0f);
        float[] rows = { 2.9f, 7.1f, 12.5f, 18.7f, 23.4f };
        foreach (float z in rows)
            foreach (float x in new[] { 3.1f, 9.2f })
            {
                var l = new GameObject($"Lamp_{x:0.0}_{z:0.0}").AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.position = new Vector3(x, 3.9f, z);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.84f, 0.62f);
                l.intensity = 3.2f;
                l.range = 7.5f;
                l.shadows = LightShadows.None;
            }
    }

    // ------------------------------------------------------------------ roulette
    // RouletteGame at the rim, facing her (she stands west of the wheel); the layout cells in its space; the ball.
    private static RouletteGame BuildRoulette(Transform root, Transform model, Shader lit)
    {
        Transform rotor = EditorScene.FindDeep(model, RotorName);
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
        Vector3 L(float x, float z, float y = RouletteFelt) => t.InverseTransformPoint(new Vector3(x, y, z));
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
    // M_Casino_xx (T_Casino_xx) + M_SlotReels (T_SlotReels), remapped on the model. Halos and floor shadows are
    // transparent and unlit, like in the original scene.
    private static void ImportArt(Shader lit)
    {
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");   // editor only: picking the shader for the asset
        if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter mi)) return;
        for (int i = 0; i < MaterialCount; i++)
        {
            string name = $"M_Casino_{i:00}";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Casino_{i:00}.png");
            bool glow = System.Array.IndexOf(GlowMaterials, i) >= 0, shadow = System.Array.IndexOf(ShadowMaterials, i) >= 0;
            Material m = glow || shadow ? TransparentMaterial($"{MatFolder}/{name}.mat", tex, unlit != null ? unlit : lit, glow)
                                        : EditorAssets.MakeMaterial($"{MatFolder}/{name}.mat", tex, 0.35f, false, lit);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), m);
        }
        var reelTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_SlotReels.png");
        if (AssetImporter.GetAtPath($"{ArtFolder}/T_SlotReels.png") is TextureImporter ti && ti.wrapMode != TextureWrapMode.Repeat)
        {
            ti.wrapMode = TextureWrapMode.Repeat;   // the reels scroll their texture
            ti.SaveAndReimport();
        }
        Material reels = EditorAssets.MakeMaterial($"{MatFolder}/M_SlotReels.mat", reelTex, 0.5f, false, lit);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_SlotReels"), reels);
        var rotorTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_RouletteRotor.png");   // European order (casino_convert.py)
        Material rotorMat = EditorAssets.MakeMaterial($"{MatFolder}/M_RouletteRotor.mat", rotorTex, 0.6f, false, lit);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_RouletteRotor"), rotorMat);
        mi.SaveAndReimport();
    }

    // ------------------------------------------------------------------ prize lockers
    private const float GalleryX = 8.6f;
    private const float CaseWidth = 0.95f, CaseDepth = 0.7f, CaseHeight = 2.3f, CaseBackX = 12.3f;   // the wall at 12.32
    private static readonly float[] CaseZ = { 11.95f, 14.1f, 10.9f, 15.15f };   // either side of the pilaster (12.7..13.35) first

    // The prize cases at the counter (one per outfit).
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
            cases.Add(BuildCase(parent, prizes[i], new Vector3(CaseBackX - CaseDepth * 0.5f, 0f, CaseZ[i]), lit, wood, velvet, brass, glass));
        return cases;
    }

    // One locker (origin on the floor at its center, forward = out to the room): wooden base and top, velvet back,
    // brass posts, glass sides and a glass door on a hinge, the plate (name + price), a lamp, a copy of her inside.
    private static PrizeCase BuildCase(Transform parent, OutfitDefinition prize, Vector3 position, Shader lit,
                                       Material wood, Material velvet, Material brass, Material glass)
    {
        var go = new GameObject("Locker_" + prize.Key);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.left));
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
    private static Material TransparentMaterial(string path, Texture2D tex, Shader shader, bool additive)
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

    // Does the material render in the transparent queue?
    private static bool IsTransparent(Material m) => m != null && m.renderQueue >= (int)RenderQueue.Transparent;

    // Lit button material of the slot machines.
    private static Material ButtonMaterial(Shader lit)
    {
        Material m = EditorAssets.MakeMaterial($"{MatFolder}/M_SlotButton.mat", null, 0.7f, false, lit);
        m.SetColor(BaseColorId, new Color(0.9f, 0.1f, 0.08f));
        m.SetColor(EmissionColorId, new Color(0.9f, 0.1f, 0.08f) * 1.5f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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
