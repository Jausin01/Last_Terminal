using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu "Last Terminal" - run the items in order (0 -> 4; 8 = the casino scene, after 3).
// This file: Helena's own assets (menus 0 and 1: import settings, materials), the still "mannequin" copy of her, and
// menu 3, which runs every prefab / data builder. Menu 2 is PartnerAnimatorBuilder, menu 4 TestSceneBuilder; the rig
// every location scene gets is LocationRigBuilder.
// No scene searches: every object the tools need is created by them and referenced directly.
public static class HelenaSetupMenu
{
    // Helena's asset root.
    internal const string Root = "Assets/Characters/Helena";
    internal const string ModelPath = Root + "/Animations/HelenaIdle.fbx";   // the old Helena: her clips' avatar (the idle clip)
    // the character in the scenes: Helena 2 (Model2/Helena2.fbx, menus 18 + 21) when she is there, else the old model
    internal static bool UseHelena2 => AssetDatabase.LoadAssetAtPath<GameObject>(HelenaCompareBuilder.ModelPath) != null;
    internal static string CharacterModelPath => UseHelena2 ? HelenaCompareBuilder.ModelPath : ModelPath;

    // material name inside the FBX -> texture file, smoothness, alpha clip
    private static readonly (string mat, string tex, float smooth, bool clip)[] Materials =
    {
        ("jacket",         "Jacket",   0.50f, false),
        ("shirt",          "Shirt",    0.15f, false),
        ("suit",           "Suit",     0.65f, false),
        ("sneakers",       "Sneakers", 0.20f, false),
        ("skin",           "Skin",     0.25f, false),
        ("hair",           "Hair",     0.30f, true),
        ("Miscelaneo.001", null,       0.00f, false),
    };

    // Menu 0: reimports her assets (import settings applied again).
    [MenuItem("Last Terminal/0 - Reimport Helena Assets")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("[Helena] Reimported models with Humanoid + clip settings.");
    }

    // Menu 1: her materials.
    [MenuItem("Last Terminal/1 - Setup Helena Materials")]
    public static void SetupMaterials()
    {
        var mi = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (mi == null) { Debug.LogError("[Helena] Model not found: " + ModelPath); return; }

        Shader lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;

        EditorAssets.EnsureFolder(Root, "Materials");
        var hairTex = AssetImporter.GetAtPath(Root + "/Textures/Hair.png") as TextureImporter;
        if (hairTex != null && !hairTex.alphaIsTransparency)
        {
            hairTex.alphaIsTransparency = true;
            hairTex.SaveAndReimport();
        }

        foreach (var entry in Materials)
        {
            string matPath = Root + "/Materials/" + entry.mat.Replace(".", "_") + ".mat";
            Texture2D tex = entry.tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + entry.tex + ".png") : null;
            Material mat = EditorAssets.MakeMaterial(matPath, tex, entry.smooth, entry.clip, lit);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.mat), mat);
        }
        mi.SaveAndReimport();
        AssetDatabase.SaveAssets();
        Debug.Log("[Helena] Materials created in " + Root + "/Materials and remapped on the model.");
    }

    // A still copy of her wearing an outfit (the casino's prize lockers): her model posed with the first frame of her
    // idle clip, its outfits set up like hers (PartnerOutfit wears the outfit at start), no Animator (it stays posed).
    internal static GameObject BuildMannequin(Transform parent, Shader lit, OutfitDefinition outfit)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
        if (prefab == null) { Debug.LogWarning("[Helena] Model missing: run menus 0 and 1."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Mannequin";
        PartnerOutfit wardrobe = OutfitBuilder.AddTo(go, lit);   // needs her Animator (accessories go on her bones)
        EditorSerialized.SetReference(wardrobe, PartnerOutfit.StartOutfitField, outfit);
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (idle != null) idle.SampleAnimation(go, idle.length * 0.25f);   // humanoid: sampled through her Animator
        Animator animator = go.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = false;
        return go;
    }

    // Menu 3: the prefabs and data assets every scene uses, built in an isolated preview scene (the open scene is
    // never touched): pickups, weapons, save points, facility, blackjack, elevator keys, the operator's room assets,
    // the voice plugins and the command / voice line data.
    [MenuItem("Last Terminal/3 - Build Prefabs + Data (Pickups, Weapons, Save Points, Facility, Blackjack, Commands)")]
    public static void BuildPrefabsAndData()
    {
        Shader lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;

        Dictionary<string, ItemDefinition> items = ItemBuilder.EnsureTestItems();
        Scene preview = EditorSceneManager.NewPreviewScene();
        ItemBuilder.BuildPickupPrefabs(preview, lit, items);
        ItemBuilder.BuildWeaponPrefabs(preview, lit, items);
        SavePointBuilder.BuildSavePointPrefabs(preview, lit);
        FacilityBuilder.BuildPrefabs(preview, lit);
        BlackjackBuilder.BuildPrefabs(preview, lit);
        ElevatorBuilder.BuildKeyPrefabs(preview, lit);   // the colored elevator keys
        OperatorRoomBuilder.PrepareAssets(lit);           // the operator's room: materials, his arms, his files
        VoiceBuilder.ConfigurePlugins();                  // voice commands: Vosk libraries + models check
        EditorSceneManager.ClosePreviewScene(preview);
        // command data: EN / PT lexicons, Helena's voice lines (completed, never reset) + the recording script
        CommandDataBuilder.EnsureLexicon(GameLanguage.English);
        CommandDataBuilder.EnsureLexicon(GameLanguage.Portuguese);
        CommandDataBuilder.ExportScript();
        AssetDatabase.SaveAssets();
    }
}
