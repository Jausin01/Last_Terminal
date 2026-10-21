using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Elevators and their keys: the colored keys (items + pickup prefabs, menu 3), the stops (one asset per place, edit it
// to change every sign that points there), the GameSession, the elevator units (built from boxes: facade, sliding
// doors, car, sign, call lamp) and the ride's dark screen. Used by the scene menus (4 facility, 8 casino).
internal static class ElevatorBuilder
{
    private const string KeyArtFolder = "Assets/Art/Props/Keys";
    private const string KeyModelPath = KeyArtFolder + "/SM_ElevatorKey.fbx";
    private const string KeyDataFolder = "Assets/Data/Items/Keys";
    private const string StopFolder = "Assets/Data/Elevators";
    private const string ElevatorMatFolder = "Assets/Art/Props/Elevator";
    private const string PickupPrefabFolder = "Assets/Prefabs/Pickups";
    internal const string SessionPath = "Assets/Data/GameSession.asset";
    private const float KeyModelScale = 1.6f;   // a real key is 7 cm: a bit bigger so it reads on the CCTV

    // stop ids (the assets are only created once: edit names, scene, key and floor in the inspector)
    internal const string FacilityOfficeStop = "facility_office";
    internal const string CasinoStop = "casino";
    internal const string LaboratoryStop = "laboratory";
    internal const string HospitalStop = "hospital";
    internal const string RooftopStop = "rooftop";

    // elevator unit sizes: facade width (bay), height; the car is DoorDepth deep behind the facade
    internal const float FacilityBay = 2.4f;
    internal const float FacilityHeight = 3.0f;
    internal const float Depth = 1.75f;
    private const float FacadeThickness = 0.15f;
    private const float DoorWidth = 1.1f;
    private const float DoorHeight = 2.2f;
    private const float CarWidth = 1.5f;
    private const float CarHeight = 2.4f;
    private const float LeafWidth = 0.57f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // the key colors: lexicon key color_<name>, Portuguese name (as she says "a chave vermelha"), tint
    internal static readonly (string name, string pt, Color color)[] KeyColors =
    {
        ("red",    "vermelha", new Color(0.85f, 0.10f, 0.10f)),
        ("orange", "laranja",  new Color(1.00f, 0.48f, 0.08f)),
        ("yellow", "amarela",  new Color(1.00f, 0.85f, 0.12f)),
        ("green",  "verde",    new Color(0.12f, 0.68f, 0.25f)),
        ("white",  "branca",   new Color(0.93f, 0.93f, 0.90f)),
        ("blue",   "azul",     new Color(0.12f, 0.32f, 0.90f)),
        ("black",  "preta",    new Color(0.07f, 0.07f, 0.08f)),
        ("purple", "roxa",     new Color(0.52f, 0.18f, 0.75f)),
    };

    private static string Title(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
    internal static string KeyPickupName(string color) => "Key_" + Title(color);

    // ------------------------------------------------------------------ data
    // one key item per color (never overwritten: tune them in the inspector)
    internal static Dictionary<string, ElevatorKey> EnsureKeys()
    {
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Items");
        HelenaSetupMenu.EnsureFolder("Assets/Data/Items", "Keys");
        var keys = new Dictionary<string, ElevatorKey>();
        foreach (var c in KeyColors)
        {
            string path = $"{KeyDataFolder}/Key_{Title(c.name)}.asset";
            var key = AssetDatabase.LoadAssetAtPath<ElevatorKey>(path);
            if (key == null)
            {
                key = ScriptableObject.CreateInstance<ElevatorKey>();
                AssetDatabase.CreateAsset(key, path);
                var so = new SerializedObject(key);
                so.FindProperty(ItemDefinition.IdField).stringValue = "item_key_" + c.name;
                so.FindProperty(ItemDefinition.DisplayNameField).stringValue = Title(c.name) + " Key";
                so.FindProperty(ItemDefinition.MaxStackField).intValue = 1;
                so.FindProperty(KeyItem.ConsumeOnUseField).boolValue = false;   // an elevator key is kept
                so.FindProperty(ElevatorKey.ColorField).colorValue = c.color;
                so.FindProperty(ElevatorKey.ColorNameField).stringValue = c.name;
                so.FindProperty(ElevatorKey.ColorNamePtField).stringValue = c.pt;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            keys[c.name] = key;
        }
        AssetDatabase.SaveAssets();
        return keys;
    }

    // the places (never overwritten). A stop without a scene (or one missing from the Build Settings) is out of service.
    internal static Dictionary<string, ElevatorStop> EnsureStops()
    {
        Dictionary<string, ElevatorKey> keys = EnsureKeys();
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Elevators");
        var stops = new Dictionary<string, ElevatorStop>();
        void Stop(string id, string en, string pt, string scene, ElevatorKey key, string floor)
        {
            string path = $"{StopFolder}/Stop_{id}.asset";
            var stop = AssetDatabase.LoadAssetAtPath<ElevatorStop>(path);
            if (stop == null)
            {
                stop = ScriptableObject.CreateInstance<ElevatorStop>();
                AssetDatabase.CreateAsset(stop, path);
                var so = new SerializedObject(stop);
                so.FindProperty(ElevatorStop.IdField).stringValue = id;
                so.FindProperty(ElevatorStop.DisplayNameField).stringValue = en;
                so.FindProperty(ElevatorStop.DisplayNamePtField).stringValue = pt;
                so.FindProperty(ElevatorStop.SceneNameField).stringValue = scene;
                so.FindProperty(ElevatorStop.RequiredKeyField).objectReferenceValue = key;
                so.FindProperty(ElevatorStop.FloorField).stringValue = floor;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            stops[id] = stop;
        }
        Stop(FacilityOfficeStop, "Facility - Office", "Instalação - Escritório", "HelenaTest", null, "B1");
        Stop(CasinoStop, "Casino", "Cassino", CasinoBuilder.SceneName, keys["red"], "7");
        Stop(LaboratoryStop, "Laboratory", "Laboratório", "", keys["blue"], "B3");
        Stop(HospitalStop, "Hospital", "Hospital", "", keys["green"], "3");
        Stop(RooftopStop, "Rooftop", "Terraço", "", keys["yellow"], "R");
        AssetDatabase.SaveAssets();
        return stops;
    }

    internal static GameSession EnsureSession()
    {
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        var session = AssetDatabase.LoadAssetAtPath<GameSession>(SessionPath);
        if (session != null) return session;
        session = ScriptableObject.CreateInstance<GameSession>();
        AssetDatabase.CreateAsset(session, SessionPath);
        AssetDatabase.SaveAssets();
        return session;
    }

    // ------------------------------------------------------------------ key pickups (menu 3)
    // Pickup_Key_<Color>: SM_ElevatorKey with its cover tinted (M_KeyCover_<Color>). Never overwritten: delete to rebuild.
    internal static void BuildKeyPrefabs(Scene preview, Shader lit)
    {
        if (!(AssetImporter.GetAtPath(KeyModelPath) is ModelImporter mi)) { Debug.LogWarning("[Keys] Model not found: " + KeyModelPath); return; }
        Dictionary<string, ElevatorKey> keys = EnsureKeys();
        HelenaSetupMenu.EnsureFolder(KeyArtFolder, "Materials");
        var coverTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KeyArtFolder}/T_KeyCover.png");
        Material metal = HelenaSetupMenu.MakeMaterial($"{KeyArtFolder}/Materials/M_KeyMetal.mat", null, 0.75f, false, lit);
        metal.SetColor(BaseColorId, new Color(0.72f, 0.72f, 0.75f));
        metal.SetFloat(MetallicId, 0.9f);
        EditorUtility.SetDirty(metal);
        var covers = new Dictionary<string, Material>();
        foreach (var c in KeyColors)
        {
            Material m = HelenaSetupMenu.MakeMaterial($"{KeyArtFolder}/Materials/M_KeyCover_{Title(c.name)}.mat", coverTex, 0.35f, false, lit);
            m.SetColor(BaseColorId, keys[c.name].Color);
            EditorUtility.SetDirty(m);
            covers[c.name] = m;
        }
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_KeyMetal"), metal);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_KeyCover"), covers["red"]);
        mi.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(KeyModelPath);
        HelenaSetupMenu.EnsureFolder("Assets", "Prefabs");
        HelenaSetupMenu.EnsureFolder("Assets/Prefabs", "Pickups");
        foreach (var c in KeyColors)
        {
            string path = $"{PickupPrefabFolder}/Pickup_{KeyPickupName(c.name)}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { Debug.Log("[Keys] Prefab already exists, kept: " + path); continue; }
            var root = new GameObject("Pickup_" + KeyPickupName(c.name));
            SceneManager.MoveGameObjectToScene(root, preview);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            visual.name = "Model";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * KeyModelScale;
            Transform cover = HelenaSetupMenu.FindDeep(visual.transform, "Cover");
            if (cover != null) cover.GetComponent<Renderer>().sharedMaterial = covers[c.name];

            var col = root.AddComponent<BoxCollider>();   // easy to click / target
            col.size = new Vector3(0.3f, 0.15f, 0.3f);
            col.center = new Vector3(0f, 0.075f, 0f);
            var grab = new GameObject("GrabPoint").transform;
            grab.SetParent(root.transform, false);
            grab.localPosition = new Vector3(0f, 0.012f * KeyModelScale, 0f);

            var pickup = root.AddComponent<Pickup>();
            HelenaSetupMenu.SetReference(pickup, Pickup.ItemField, keys[c.name]);
            HelenaSetupMenu.SetInt(pickup, Pickup.AmountField, 1);
            HelenaSetupMenu.SetReference(pickup, Pickup.VisualField, visual.transform);
            HelenaSetupMenu.SetReference(pickup, Pickup.GrabPointField, grab);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[Keys] Prefab built: " + path);
        }
    }

    // a key lying on the floor (prefab from menu 3, colored cube otherwise)
    internal static Pickup PlaceKey(Transform parent, ElevatorKey key, Vector3 position, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PickupPrefabFolder}/Pickup_{KeyPickupName(key.ColorName)}.prefab");
        if (prefab == null) return HelenaSetupMenu.CreateCubePickup(parent, key, 1, position + Vector3.up * 0.15f, key.Color);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go.GetComponent<Pickup>();
    }

    // ------------------------------------------------------------------ elevator units
    private struct UnitMaterials { public Material Frame, Door, Car, Sign, Glow; }

    private static UnitMaterials EnsureMaterials(Shader lit)
    {
        HelenaSetupMenu.EnsureFolder("Assets/Art", "Props");
        HelenaSetupMenu.EnsureFolder("Assets/Art/Props", "Elevator");
        Material Make(string name, Color color, float smooth, float metallic, bool glow)
        {
            Material m = HelenaSetupMenu.MakeMaterial($"{ElevatorMatFolder}/{name}.mat", null, smooth, false, lit);
            m.SetColor(BaseColorId, color);
            m.SetFloat(MetallicId, metallic);
            if (glow)
            {
                m.SetColor(EmissionColorId, Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }
        return new UnitMaterials
        {
            Frame = Make("M_ElevatorFrame", new Color(0.28f, 0.2f, 0.12f), 0.55f, 0.6f, false),   // dark bronze
            Door = Make("M_ElevatorDoor", new Color(0.62f, 0.6f, 0.56f), 0.7f, 0.85f, false),     // brushed steel
            Car = Make("M_ElevatorCar", new Color(0.33f, 0.24f, 0.16f), 0.45f, 0.1f, false),      // wood panels
            Sign = Make("M_ElevatorSign", new Color(0.04f, 0.04f, 0.04f), 0.8f, 0f, false),
            Glow = Make("M_ElevatorGlow", new Color(1f, 0.92f, 0.75f), 0.5f, 0f, true),
        };
    }

    // One elevator: the facade (bay wide, height tall) with the doorway, two sliding doors, the car behind (Depth),
    // the sign above the doors (number, destination, key color stripe) and the call lamp. Origin = the doorway on the
    // floor, yaw = the way out of the car. Carves its own footprint out of the NavMesh.
    internal static Elevator PlaceElevator(Transform parent, Shader lit, string hereId, string destinationId, int number,
                                           Vector3 position, float yaw, float bay, float height, SecurityCamera watch)
    {
        Dictionary<string, ElevatorStop> stops = EnsureStops();
        UnitMaterials m = EnsureMaterials(lit);
        var root = new GameObject($"Elevator_{number}_{destinationId}");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Transform t = root.transform;
        float half = bay * 0.5f, door = DoorWidth * 0.5f, ft = FacadeThickness;

        // facade around the doorway + the block's sides and roof
        Box(t, "FacadeLeft", new Vector3(-(half + door) * 0.5f, height * 0.5f, -ft * 0.5f), new Vector3(half - door, height, ft), m.Frame);
        Box(t, "FacadeRight", new Vector3((half + door) * 0.5f, height * 0.5f, -ft * 0.5f), new Vector3(half - door, height, ft), m.Frame);
        Box(t, "FacadeTop", new Vector3(0f, (DoorHeight + height) * 0.5f, -ft * 0.5f), new Vector3(DoorWidth, height - DoorHeight, ft), m.Frame);
        Box(t, "TrimLeft", new Vector3(-door - 0.05f, (DoorHeight + 0.05f) * 0.5f, 0.02f), new Vector3(0.1f, DoorHeight + 0.05f, 0.05f), m.Door);
        Box(t, "TrimRight", new Vector3(door + 0.05f, (DoorHeight + 0.05f) * 0.5f, 0.02f), new Vector3(0.1f, DoorHeight + 0.05f, 0.05f), m.Door);
        Box(t, "TrimTop", new Vector3(0f, DoorHeight + 0.05f, 0.02f), new Vector3(DoorWidth + 0.3f, 0.1f, 0.05f), m.Door);
        Box(t, "SideLeft", new Vector3(-half + 0.05f, height * 0.5f, -(Depth + ft) * 0.5f), new Vector3(0.1f, height, Depth - ft), m.Frame);
        Box(t, "SideRight", new Vector3(half - 0.05f, height * 0.5f, -(Depth + ft) * 0.5f), new Vector3(0.1f, height, Depth - ft), m.Frame);
        Box(t, "Roof", new Vector3(0f, height - 0.05f, -Depth * 0.5f), new Vector3(bay, 0.1f, Depth), m.Frame);

        // the car (starts behind the door leaves)
        float carFront = -0.25f, carDepth = Depth - 0.25f, carMid = carFront - carDepth * 0.5f;
        Box(t, "CarFloor", new Vector3(0f, 0.01f, carMid), new Vector3(CarWidth + 0.2f, 0.02f, carDepth), m.Door);
        Box(t, "CarBack", new Vector3(0f, CarHeight * 0.5f, -Depth + 0.05f), new Vector3(CarWidth + 0.2f, CarHeight, 0.1f), m.Car);
        Box(t, "CarLeft", new Vector3(-CarWidth * 0.5f - 0.05f, CarHeight * 0.5f, carMid), new Vector3(0.1f, CarHeight, carDepth), m.Car);
        Box(t, "CarRight", new Vector3(CarWidth * 0.5f + 0.05f, CarHeight * 0.5f, carMid), new Vector3(0.1f, CarHeight, carDepth), m.Car);
        Box(t, "CarCeiling", new Vector3(0f, CarHeight + 0.02f, carMid), new Vector3(CarWidth + 0.2f, 0.04f, carDepth), m.Frame);
        Box(t, "CarLight", new Vector3(0f, CarHeight - 0.005f, carMid), new Vector3(0.8f, 0.01f, 0.6f), m.Glow);
        Box(t, "Handrail", new Vector3(0f, 0.95f, -Depth + 0.14f), new Vector3(CarWidth - 0.2f, 0.04f, 0.04f), m.Door);
        var carLight = new GameObject("CarLamp").AddComponent<Light>();
        carLight.transform.SetParent(t, false);
        carLight.transform.localPosition = new Vector3(0f, CarHeight - 0.2f, carMid);
        carLight.type = LightType.Point;
        carLight.color = new Color(1f, 0.9f, 0.75f);
        carLight.intensity = 1.2f;
        carLight.range = 2.6f;
        carLight.shadows = LightShadows.None;

        // the two door leaves, just behind the facade (they slide behind it)
        float leafZ = -ft - 0.04f;
        Transform leftDoor = Box(t, "DoorLeft", new Vector3(-LeafWidth * 0.5f + 0.01f, DoorHeight * 0.5f, leafZ), new Vector3(LeafWidth, DoorHeight, 0.04f), m.Door).transform;
        Transform rightDoor = Box(t, "DoorRight", new Vector3(LeafWidth * 0.5f - 0.01f, DoorHeight * 0.5f, leafZ), new Vector3(LeafWidth, DoorHeight, 0.04f), m.Door).transform;

        // call panel + lamp beside the doors
        Box(t, "CallPanel", new Vector3(door + 0.3f, 1.15f, 0.01f), new Vector3(0.1f, 0.2f, 0.02f), m.Door);
        Renderer lamp = Box(t, "CallLamp", new Vector3(door + 0.3f, 1.15f, 0.025f), new Vector3(0.045f, 0.045f, 0.012f), m.Glow).GetComponent<Renderer>();

        // the sign above the doors: black panel, the text, the key-color stripe
        const float signH = 0.36f;
        const float signY = DoorHeight + 0.37f;   // over the door trim; needs a facade at least 2.8 m tall
        Box(t, "SignPanel", new Vector3(0f, signY, 0.03f), new Vector3(1.7f, signH, 0.04f), m.Sign);
        Renderer stripe = Box(t, "SignStripe", new Vector3(0f, signY - signH * 0.5f + 0.03f, 0.052f), new Vector3(1.7f, 0.05f, 0.004f), m.Glow).GetComponent<Renderer>();
        var textGo = new GameObject("SignText");
        textGo.transform.SetParent(t, false);
        textGo.transform.localPosition = new Vector3(0f, signY + 0.02f, 0.053f);
        textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TMP is read along its +z: turned to face the room
        var text = textGo.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(1.6f, signH - 0.08f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 2.4f;
        text.color = new Color(1f, 0.85f, 0.55f);
        text.text = "ELEVATOR";

        // where she stands: in the car (facing out), in front of it
        var inside = new GameObject("Inside").transform;
        inside.SetParent(t, false);
        inside.localPosition = new Vector3(0f, 0f, carFront - carDepth * 0.55f);
        var outside = new GameObject("Outside").transform;
        outside.SetParent(t, false);
        outside.localPosition = new Vector3(0f, 0f, 0.9f);

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;   // heard through the camera's microphone
        audio.volume = 0.6f;

        var obstacle = root.AddComponent<NavMeshObstacle>();   // the whole block, out of the NavMesh
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, height * 0.5f, -Depth * 0.5f);
        obstacle.size = new Vector3(bay, height, Depth);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        var elevator = root.AddComponent<Elevator>();
        HelenaSetupMenu.SetReference(elevator, Elevator.HereField, stops.TryGetValue(hereId, out ElevatorStop here) ? here : null);
        HelenaSetupMenu.SetReference(elevator, Elevator.DestinationField, stops.TryGetValue(destinationId, out ElevatorStop to) ? to : null);
        HelenaSetupMenu.SetInt(elevator, Elevator.NumberField, number);
        HelenaSetupMenu.SetReference(elevator, Elevator.LeftDoorField, leftDoor);
        HelenaSetupMenu.SetReference(elevator, Elevator.RightDoorField, rightDoor);
        HelenaSetupMenu.SetReference(elevator, Elevator.InsideField, inside);
        HelenaSetupMenu.SetReference(elevator, Elevator.OutsideField, outside);
        HelenaSetupMenu.SetReference(elevator, Elevator.LampField, lamp);
        HelenaSetupMenu.SetReference(elevator, Elevator.AudioSourceField, audio);
        HelenaSetupMenu.SetReference(elevator, Elevator.WatchCameraField, watch);
        var so = new SerializedObject(elevator);
        so.FindProperty(Elevator.DoorTravelField).floatValue = LeafWidth - 0.02f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var sign = textGo.AddComponent<ElevatorSign>();
        HelenaSetupMenu.SetReference(sign, ElevatorSign.ElevatorField, elevator);
        HelenaSetupMenu.SetReference(sign, ElevatorSign.TextField, text);
        HelenaSetupMenu.SetReference(sign, ElevatorSign.StripeField, stripe);
        HelenaSetupMenu.SetReference(elevator, Elevator.SignField, sign);
        return elevator;
    }

    private static GameObject Box(Transform parent, string objectName, Vector3 localCenter, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // ------------------------------------------------------------------ the ride (rig)
    // ElevatorTravel + its dark screen (black, the floor in the middle), above the HUDs; the signs follow the language.
    internal static void WireTravel(ElevatorTravel travel, Transform canvas, SaveSystem saveSystem, GameSession session,
                                    PartnerElevatorUser user, List<Elevator> elevators, CameraSettings settings,
                                    SecurityCameraSystem cameraSystem)
    {
        GameObject fadeGo = HelenaSetupMenu.CreateUiObject(canvas, "ElevatorFader");
        HelenaSetupMenu.Stretch((RectTransform)fadeGo.transform);
        var black = fadeGo.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        var group = fadeGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        TMP_Text label = HelenaSetupMenu.CreateText(fadeGo.transform, "Floor", 54, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.45f));
        Vector2 mid = new Vector2(0.5f, 0.5f);
        HelenaSetupMenu.Place(label.rectTransform, mid, mid, mid, Vector2.zero, new Vector2(1000f, 200f));

        HelenaSetupMenu.SetReference(travel, ElevatorTravel.SaveSystemField, saveSystem);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.SessionField, session);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.UserField, user);
        HelenaSetupMenu.SetReferenceArray(travel, ElevatorTravel.ElevatorsField, elevators);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.FaderField, group);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.LabelField, label);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.SettingsField, settings);
        HelenaSetupMenu.SetReference(travel, ElevatorTravel.CameraSystemField, cameraSystem);
        foreach (Elevator e in elevators)
        {
            var so = new SerializedObject(e);
            Object sign = so.FindProperty(Elevator.SignField).objectReferenceValue;
            if (sign != null) HelenaSetupMenu.SetReference(sign, ElevatorSign.SettingsField, settings);
        }
    }

    // lexicon names: the stops ("go to the casino"), the colors ("the red elevator"), the keys ("pick up the red key")
    internal static IEnumerable<(string key, string[] en, string[] pt)> LexiconNames()
    {
        yield return ("stop_" + FacilityOfficeStop, new[] { "facility", "the facility", "back to the facility", "downstairs" },
                                                     new[] { "instalacao", "instalacoes", "de volta pra instalacao", "la embaixo" });
        yield return ("stop_" + CasinoStop, new[] { "casino", "the casino" }, new[] { "cassino", "casino", "o cassino" });
        yield return ("stop_" + LaboratoryStop, new[] { "laboratory", "lab", "the lab" }, new[] { "laboratorio", "lab" });
        yield return ("stop_" + HospitalStop, new[] { "hospital", "the hospital" }, new[] { "hospital" });
        yield return ("stop_" + RooftopStop, new[] { "rooftop", "roof", "the roof" }, new[] { "terraco", "telhado", "cobertura" });
        foreach (var c in KeyColors)
        {
            string masculine = c.pt.EndsWith("a") && c.pt != "laranja" ? c.pt.Substring(0, c.pt.Length - 1) + "o" : c.pt;
            string[] pt = masculine != c.pt ? new[] { c.pt, masculine } : new[] { c.pt };
            yield return ("color_" + c.name, new[] { c.name }, pt);
            yield return ("item_key_" + c.name, new[] { c.name + " key", c.name + " elevator key" }, new[] { "chave " + c.pt, "chave do elevador " + c.pt });
        }
    }
}
