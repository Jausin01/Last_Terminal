using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Blackjack: materials + prefabs (menu 3: card, chip, table) and the scene parts (menu 4: table wiring, HUD).
// The table prefab is a seat (SaveStation, chair pulled 0.45 m so she can stand close to the table edge),
// the table logic (BlackjackTable + BlackjackGame), the low-poly table, her chair, three stools and a deck.
internal static class BlackjackBuilder
{
    private const string ArtFolder = "Assets/Art/Props/Blackjack";
    private const string PrefabFolder = "Assets/Prefabs/Blackjack";
    private const string CardPrefabPath = PrefabFolder + "/Card.prefab";
    internal const string ChipPrefabPath = PrefabFolder + "/Chip.prefab";
    internal const string TablePrefabPath = PrefabFolder + "/BlackjackTable.prefab";
    private const string ChairModelPath = "Assets/Art/Props/SavePoint/SM_SaveChair_Office.fbx";
    private const float ChairPull = 0.45f;     // table edge 0.30 m in front of her seat
    // the player's eyes on the middle stool (framing checked with a render: her, the shoe, both hands of cards, the bet)
    private static readonly Vector3 TableCamPosition = new Vector3(0f, 1.40f, 1.62f);

    private static readonly (string model, string mat, string tex, float smooth)[] Models =
    {
        ("SM_Card", "M_Cards", "T_Cards", 0.35f),
        ("SM_Chip", "M_Chips", "T_Chips", 0.45f),
        ("SM_BlackjackTable", "M_BlackjackTable", "T_BlackjackTable", 0.2f),
        ("SM_BarStool", "M_BlackjackTable", "T_BlackjackTable", 0.2f),
    };

    // ------------------------------------------------------------------ menu 3
    internal static void BuildPrefabs(Scene preview, Shader lit)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/SM_BlackjackTable.fbx") == null)
        {
            Debug.LogWarning($"[Blackjack] Models not found in {ArtFolder}: copy the Blackjack art first.");
            return;
        }
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Materials");
        HelenaSetupMenu.EnsureFolder("Assets", "Prefabs");
        HelenaSetupMenu.EnsureFolder("Assets/Prefabs", "Blackjack");

        foreach (var m in Models)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{m.tex}.png");
            Material mat = HelenaSetupMenu.MakeMaterial($"{ArtFolder}/Materials/{m.mat}.mat", tex, m.smooth, false, lit);
            if (AssetImporter.GetAtPath($"{ArtFolder}/{m.model}.fbx") is ModelImporter mi)
            {
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.mat), mat);
                mi.SaveAndReimport();
            }
        }
        Material deckEdge = HelenaSetupMenu.MakeMaterial($"{ArtFolder}/Materials/M_DeckEdge.mat", null, 0.2f, false, lit);
        deckEdge.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.84f));
        EditorUtility.SetDirty(deckEdge);

        GameObject card = ModelPrefab(preview, "SM_Card", CardPrefabPath, "Card");
        GameObject chip = ModelPrefab(preview, "SM_Chip", ChipPrefabPath, "Chip");
        if (card == null || chip == null) return;
        BuildTable(preview, card, chip, deckEdge);
    }

    private static GameObject ModelPrefab(Scene preview, string model, string path, string objectName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{model}.fbx");
        if (fbx == null) { Debug.LogWarning("[Blackjack] Model not found: " + model); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, preview);
        go.name = objectName;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[Blackjack] Prefab built: " + path);
        return prefab;
    }

    private static void BuildTable(Scene preview, GameObject cardPrefab, GameObject chipPrefab, Material deckEdge)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath) != null) { Debug.Log("[Blackjack] Prefab already exists, kept: " + TablePrefabPath); return; }

        var root = new GameObject("BlackjackTable");
        SceneManager.MoveGameObjectToScene(root, preview);
        root.AddComponent<BoxCollider>();
        root.AddComponent<NavMeshObstacle>();
        var seat = root.AddComponent<SaveStation>();
        var table = root.AddComponent<BlackjackTable>();
        var game = root.AddComponent<BlackjackGame>();

        Instance(root.transform, $"{ArtFolder}/SM_BlackjackTable.fbx", "Model", Vector3.zero, 0f);
        Transform chair = Instance(root.transform, ChairModelPath, "Chair", Vector3.zero, 0f);
        if (chair == null) chair = Child(root.transform, "Chair", Vector3.zero);
        for (int i = -1; i <= 1; i++)
        {
            float a = Mathf.Deg2Rad * (90f + 35f * i);
            Instance(root.transform, $"{ArtFolder}/SM_BarStool.fbx", "Stool", new Vector3(1.35f * Mathf.Cos(a), 0f, 0.30f + 1.35f * Mathf.Sin(a)), 180f - 35f * i);
        }

        // the deck she deals from (waits by the shoe)
        Transform deck = Child(root.transform, "Deck", new Vector3(0.30f, 0.751f, 0.47f));
        deck.localRotation = Quaternion.Euler(0f, -12f, 0f);
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(block.GetComponent<Collider>());
        block.name = "Edge";
        block.transform.SetParent(deck, false);
        block.transform.localPosition = new Vector3(0f, 0.0125f, 0f);
        block.transform.localScale = new Vector3(0.08f, 0.025f, 0.112f);
        block.GetComponent<Renderer>().sharedMaterial = deckEdge;
        var top = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, preview);
        top.transform.SetParent(deck, false);
        top.transform.localPosition = new Vector3(0f, 0.0251f, 0f);
        top.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);   // back up

        // camera at the player's seat, facing her (off until a game starts; the operator's view cuts to it)
        Transform camT = Child(root.transform, "TableCam", TableCamPosition);
        camT.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tableCam = camT.gameObject.AddComponent<SecurityCamera>();
        HelenaSetupMenu.SetString(tableCam, SecurityCamera.LabelField, "TABLE");
        var camSo = new SerializedObject(tableCam);
        camSo.FindProperty(SecurityCamera.PitchField).floatValue = 27f;
        camSo.FindProperty(SecurityCamera.YawRangeField).floatValue = 15f;
        camSo.FindProperty("fieldOfView").floatValue = 56f;
        camSo.ApplyModifiedPropertiesWithoutUndo();
        camT.gameObject.SetActive(false);

        Transform stand = Child(root.transform, "StandPoint", Vector3.zero);
        Transform right = Child(root.transform, "WalkInRight", Vector3.zero);
        Transform left = Child(root.transform, "WalkInLeft", Vector3.zero);
        HelenaSetupMenu.SetReference(seat, SaveStation.ChairField, chair);
        HelenaSetupMenu.SetReference(seat, SaveStation.StandPointField, stand);
        HelenaSetupMenu.SetReference(seat, SaveStation.WalkInRightField, right);
        HelenaSetupMenu.SetReference(seat, SaveStation.WalkInLeftField, left);
        HelenaSetupMenu.SetString(seat, SaveStation.LocationNameField, "Blackjack");
        var so = new SerializedObject(seat);
        so.FindProperty(SaveStation.ChairPullDistanceField).floatValue = ChairPull;
        so.ApplyModifiedPropertiesWithoutUndo();
        seat.FitToModels();

        HelenaSetupMenu.SetReference(table, BlackjackTable.SeatField, seat);
        HelenaSetupMenu.SetReference(table, BlackjackTable.CardPrefabField, cardPrefab);
        HelenaSetupMenu.SetReference(table, BlackjackTable.ChipPrefabField, chipPrefab);
        HelenaSetupMenu.SetReference(table, BlackjackTable.DeckField, deck);
        HelenaSetupMenu.SetReference(table, BlackjackTable.CardAtlasField, AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Cards.png"));
        HelenaSetupMenu.SetReference(table, BlackjackTable.TableCameraField, tableCam);
        HelenaSetupMenu.SetReference(game, BlackjackGame.TableField, table);

        PrefabUtility.SaveAsPrefabAsset(root, TablePrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Blackjack] Prefab built: " + TablePrefabPath);
    }

    private static Transform Instance(Transform parent, string path, string objectName, Vector3 localPosition, float yaw)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) { Debug.LogWarning("[Blackjack] Model not found: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent.gameObject.scene);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    private static Transform Child(Transform parent, string objectName, Vector3 localPosition)
    {
        var t = new GameObject(objectName).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    // ------------------------------------------------------------------ menu 4
    internal static BlackjackGame PlaceTable(Transform parent, Vector3 position, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
        if (prefab == null) { Debug.LogWarning("[Blackjack] No table prefab: run menu 3."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go.GetComponent<BlackjackGame>();
    }

    // the table camera is one of the facility cameras (in the table's room), only usable during a game
    internal static void AddTableCamera(BlackjackGame game, Room room, List<SecurityCamera> cameras) => AddTableCamera(game.Table, room, cameras);

    internal static void AddTableCamera(BlackjackTable table, Room room, List<SecurityCamera> cameras)
    {
        SecurityCamera cam = table.TableCamera;
        if (cam == null) { Debug.LogWarning("[Blackjack] The table prefab has no TableCam: delete it and run menu 3 again."); return; }
        if (room != null) HelenaSetupMenu.SetReference(cam, SecurityCamera.RoomField, room);
        cameras.Add(cam);
    }

    internal static void WireGame(BlackjackGame game, ChipBank bank, PartnerSaveUser seatUser, PartnerBlackjackDealer dealer,
                                  PartnerVoice voice, CombatEncounter encounter, SecurityCameraSystem cameraSystem)
    {
        HelenaSetupMenu.SetReference(game, BlackjackGame.CameraSystemField, cameraSystem);
        HelenaSetupMenu.SetReference(game, BlackjackGame.BankField, bank);
        HelenaSetupMenu.SetReference(game, BlackjackGame.SeatUserField, seatUser);
        HelenaSetupMenu.SetReference(game, BlackjackGame.DealerField, dealer);
        HelenaSetupMenu.SetReference(game, BlackjackGame.VoiceField, voice);
        if (encounter != null) HelenaSetupMenu.SetReference(game, BlackjackGame.EncounterField, encounter);
    }

    // ------------------------------------------------------------------ casino tables (menu 8)
    // The table prefab on one of the casino's own tables: its model and stools go (the casino's table is there),
    // the felt height moves the whole thing (the prefab's felt is at 0.751). Blackjack keeps its BlackjackGame;
    // Casino Hold'em swaps it for a HoldemGame. Returns the BlackjackTable (its game is on the same object).
    internal static BlackjackTable PlaceCasinoTable(Transform parent, Vector3 seat, float yaw, float feltHeight, bool holdem)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
        if (prefab == null) { Debug.LogWarning("[Blackjack] No table prefab: run menu 3."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = holdem ? "HoldemTable" : "BlackjackTable";
        go.transform.SetPositionAndRotation(seat + Vector3.up * (feltHeight - 0.751f), Quaternion.Euler(0f, yaw, 0f));
        var strip = new List<GameObject>();
        foreach (Transform child in go.transform)
            if (child.name == "Model" || child.name == "Stool") strip.Add(child.gameObject);
        foreach (GameObject g in strip) Object.DestroyImmediate(g);

        var table = go.GetComponent<BlackjackTable>();
        if (holdem)
        {
            var bj = go.GetComponent<BlackjackGame>();
            if (bj != null) Object.DestroyImmediate(bj);
            var game = go.AddComponent<HoldemGame>();
            HelenaSetupMenu.SetReference(game, HoldemGame.TableField, table);
            HelenaSetupMenu.SetString(go.GetComponent<SaveStation>(), SaveStation.LocationNameField, "Hold'em");
        }
        // no shoe / chip tray on the casino's felt: they lie on it
        var so = new SerializedObject(table);
        so.FindProperty(BlackjackTable.ShoeField).vector3Value = new Vector3(0.40f, 0.76f, 0.50f);
        so.FindProperty(BlackjackTable.ChipTrayField).vector3Value = new Vector3(-0.23f, 0.751f, 0.38f);
        so.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<SaveStation>().FitToModels();
        return table;
    }

    internal static void WireHoldem(HoldemGame game, ChipBank bank, PartnerSaveUser seatUser, PartnerBlackjackDealer dealer,
                                    PartnerVoice voice, CombatEncounter encounter, SecurityCameraSystem cameraSystem)
    {
        HelenaSetupMenu.SetReference(game, HoldemGame.CameraSystemField, cameraSystem);
        HelenaSetupMenu.SetReference(game, HoldemGame.BankField, bank);
        HelenaSetupMenu.SetReference(game, HoldemGame.SeatUserField, seatUser);
        HelenaSetupMenu.SetReference(game, HoldemGame.DealerField, dealer);
        HelenaSetupMenu.SetReference(game, HoldemGame.VoiceField, voice);
        if (encounter != null) HelenaSetupMenu.SetReference(game, HoldemGame.EncounterField, encounter);
    }

    // small text panel on the right (the table camera shows the cards): header, dealer + hand lines, prompt
    internal static void BuildHud(Transform canvas, List<BlackjackGame> games, CameraSettings settings)
    {
        Color felt = new Color(0.02f, 0.16f, 0.08f, 0.72f);
        Color gold = new Color(0.9f, 0.76f, 0.36f);
        var root = HelenaSetupMenu.CreateUiObject(canvas, "BlackjackHud");
        root.transform.SetSiblingIndex(1);
        Vector2 rc = new Vector2(1f, 0.5f);
        HelenaSetupMenu.Place((RectTransform)root.transform, rc, rc, rc, new Vector2(-24f, 0f), new Vector2(290f, 170f));
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image back = HelenaSetupMenu.CreateUiObject(root.transform, "Back").AddComponent<Image>();
        HelenaSetupMenu.Stretch(back.rectTransform);
        back.color = felt;
        back.raycastTarget = false;
        var outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(1f, -1f);

        Vector2 tl = new Vector2(0f, 1f);
        TMP_Text header = HelenaSetupMenu.CreateText(root.transform, "Header", 18, TextAlignmentOptions.Left, gold);
        HelenaSetupMenu.Place(header.rectTransform, tl, tl, tl, new Vector2(12f, -8f), new Vector2(266f, 24f));
        TMP_Text body = HelenaSetupMenu.CreateText(root.transform, "Body", 16, TextAlignmentOptions.TopLeft, Color.white);
        HelenaSetupMenu.Place(body.rectTransform, tl, tl, tl, new Vector2(12f, -36f), new Vector2(266f, 92f));
        Vector2 bl = Vector2.zero;
        TMP_Text prompt = HelenaSetupMenu.CreateText(root.transform, "Prompt", 13, TextAlignmentOptions.BottomLeft, gold);
        HelenaSetupMenu.Place(prompt.rectTransform, bl, bl, bl, new Vector2(12f, 8f), new Vector2(266f, 34f));

        var hud = root.AddComponent<BlackjackHud>();
        HelenaSetupMenu.SetReferenceArray(hud, BlackjackHud.GamesField, games);
        HelenaSetupMenu.SetReference(hud, BlackjackHud.SettingsField, settings);
        HelenaSetupMenu.SetReference(hud, BlackjackHud.GroupField, group);
        HelenaSetupMenu.SetReference(hud, BlackjackHud.HeaderField, header);
        HelenaSetupMenu.SetReference(hud, BlackjackHud.BodyField, body);
        HelenaSetupMenu.SetReference(hud, BlackjackHud.PromptField, prompt);
    }
}
