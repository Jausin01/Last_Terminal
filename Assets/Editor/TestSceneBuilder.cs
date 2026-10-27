using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Last Terminal/4: the test scene (HelenaTest.unity) - the facility test level with pickups, save points and
// training dummies, built through LocationRigBuilder.
internal static class TestSceneBuilder
{
    // The test scene and its NavMesh asset.
    private const string SceneFolder = "Assets/Scenes";
    private const string ScenePath = SceneFolder + "/HelenaTest.unity";
    private const string NavMeshPath = SceneFolder + "/HelenaTest_NavMesh.asset";

    // Menu 4: the test scene.
    [MenuItem("Last Terminal/4 - Build Test Scene")]
    public static void BuildTestScene() => LocationRigBuilder.BuildLocation(ScenePath, NavMeshPath, BuildTestContent);

    // The facility (test scene): rooms, doors, keypad (FacilityBuilder) + the test content.
    private static LocationRigBuilder.LocationContent BuildTestContent(Scene scene, Shader lit)
    {
        var content = new LocationRigBuilder.LocationContent { TestEnemies = true };
        // facility: rooms, walls (NavMesh baked from them), doors, CCTV cameras, lights (prefabs from menu 3)
        FacilityBuilder.Level level = FacilityBuilder.BuildLevel(scene, lit);
        content.Level = level;
        // storage counter (the medkit on top tests the medium pick up); carves its own hole
        GameObject counter = EditorScene.CreateBox(level.ContentRoot, new Vector3(3f, 0.45f, 10.6f), new Vector3(1.4f, 0.9f, 0.6f));
        counter.name = "Counter";
        var counterObstacle = counter.AddComponent<NavMeshObstacle>();
        counterObstacle.carving = true;

        // pickups: prefab when built (menu 3), colored cube otherwise
        Dictionary<string, ItemDefinition> items = ItemBuilder.EnsureTestItems();
        Dictionary<string, ElevatorKey> keys = ElevatorBuilder.EnsureKeys();
        var pickups = new GameObject("Pickups");
        List<Pickup> placed = content.Pickups;
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "HandgunAmmo", items, 15, new Vector3(3f, 0f, -6.5f), 25f, new Color(0.85f, 0.7f, 0.2f)));     // lobby
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "Medkit", items, 1, new Vector3(-3f, 0f, -1.5f), 70f, new Color(0.2f, 0.75f, 0.3f)));         // lobby
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "ShotgunShells", items, 6, new Vector3(-4f, 0f, 2f), 15f, new Color(0.8f, 0.25f, 0.15f)));   // corridor
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "MagnumAmmo", items, 7, new Vector3(-12f, 0f, -7f), 35f, new Color(0.6f, 0.1f, 0.1f)));      // training
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "HandgunAmmo", items, 15, new Vector3(2f, 0f, 6.5f), -40f, new Color(0.85f, 0.7f, 0.2f)));    // storage
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "Medkit", items, 1, new Vector3(3.2f, 0.9f, 10.45f), -10f, new Color(0.2f, 0.75f, 0.3f)));    // on the counter
        placed.Add(ItemBuilder.PlacePickup(pickups.transform, "Keycard", items, 1, new Vector3(4.5f, 0f, 8f), 30f, new Color(0.25f, 0.45f, 0.9f)));         // storage
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
        content.Stations.Add(SavePointBuilder.PlaceSavePoint(savePoints.transform, "SavePoint_Wood", new Vector3(10f, 0f, -6.5f), 0f));        // office
        content.Stations.Add(SavePointBuilder.PlaceSavePoint(savePoints.transform, "SavePoint_Security", new Vector3(4f, 0f, 1f), 0f));        // corridor
        content.Stations.Add(SavePointBuilder.PlaceSavePoint(savePoints.transform, "SavePoint_Terminal", new Vector3(-3f, 0f, 8f), 0f));      // storage
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
        Material mat = EditorAssets.GetOrCreateColorMaterial("TrainingDummy", new Color(0.62f, 0.5f, 0.32f));
        if (mat != null) bodyRenderer.sharedMaterial = mat;

        var health = root.AddComponent<Health>();
        var dummy = root.AddComponent<TrainingDummy>();
        EditorSerialized.SetReference(dummy, TrainingDummy.VisualField, visual);
        EditorSerialized.SetReference(dummy, TrainingDummy.BodyRendererField, bodyRenderer);
        return health;
    }
}
