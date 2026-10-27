using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Save points: the SavePoint_Base prefab and its variants (desk / computer / chair models in slots), their
// materials, and placing one in a scene.
internal static class SavePointBuilder
{
    // Save point models, prefabs and the base prefab the variants come from.
    private const string SavePointPropsFolder = "Assets/Art/Props/SavePoint";
    private const string SavePointPrefabFolder = "Assets/Prefabs/SavePoints";
    private const string SavePointBasePath = SavePointPrefabFolder + "/SavePoint_Base.prefab";

    // save point models (Assets/Art/Props/SavePoint): material name inside the FBX -> texture, smoothness, emissive
    private static readonly (string mat, string tex, float smooth, bool emissive)[] SavePointMaterials =
    {
        ("M_SaveStation",      "T_SaveStation",      0.25f, false),
        ("M_SaveDesk_Wood",    "T_SaveDesk_Wood",    0.35f, false),
        ("M_SaveChair_Office", "T_SaveChair_Office", 0.20f, false),
        ("M_SavePC_Retro",     "T_SavePC_Retro",     0.30f, false),
        ("M_SaveScreen",       "T_SaveScreen",       0.80f, true),
    };

    // save point variants: prefab name, location shown on the save screen, desk / computer / chair models (SM_*.fbx)
    private static readonly (string name, string location, string desk, string pc, string chair)[] SavePointVariants =
    {
        ("SavePoint_Wood",     "Manager's Office", "SM_SaveDesk_Wood",  "SM_SavePC_Retro",    "SM_SaveChair_Office"),
        ("SavePoint_Office",   "Office",           "SM_SaveDesk_Metal", "SM_SavePC_Beige",    "SM_SaveChair_Fabric"),
        ("SavePoint_Security", "Security Room",    "SM_SaveDesk_Metal", "SM_SavePC_Security", "SM_SaveChair_Office"),
        ("SavePoint_Terminal", "Server Room",      "SM_SaveDesk_Wood",  "SM_SavePC_Terminal", "SM_SaveChair_Fabric"),
    };

    // Save points: SavePoint_Base (SaveStation + collider + NavMesh hole + empty Desk / Computer / Chair slots + points)
    // and one Prefab Variant per SavePointVariants entry, with the models in the slots and everything fitted to them.
    // Your own models: make a Prefab Variant of SavePoint_Base, drop them in the slots (gizmos show where), then
    // right-click the SaveStation component > Fit To Models.
    internal static void BuildSavePointPrefabs(Scene preview, Shader lit)
    {
        // materials (always refreshed) + remap on every save point model
        EditorAssets.EnsureFolder(SavePointPropsFolder, "Materials");
        var mats = new Dictionary<string, Material>();
        foreach (var m in SavePointMaterials)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SavePointPropsFolder}/{m.tex}.png");
            Material mat = EditorAssets.MakeMaterial($"{SavePointPropsFolder}/Materials/{m.mat}.mat", tex, m.smooth, false, lit);
            if (m.emissive)
            {
                // the CRT glows a little
                mat.SetTexture(ShaderIds.EmissionMapId, tex);
                mat.SetColor(ShaderIds.EmissionColorId, Color.white * 0.8f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(mat);
            }
            mats[m.mat] = mat;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { SavePointPropsFolder }))
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (mi == null) continue;
            foreach (var m in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
            mi.SaveAndReimport();
        }

        EditorAssets.EnsureFolder("Assets", "Prefabs");
        EditorAssets.EnsureFolder("Assets/Prefabs", "SavePoints");
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SavePointBasePath);
        if (basePrefab == null)
        {
            var root = new GameObject("SavePoint");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.AddComponent<BoxCollider>();
            root.AddComponent<NavMeshObstacle>();
            var station = root.AddComponent<SaveStation>();
            EditorScene.CreateChild(root.transform, "Desk", Vector3.zero);
            EditorScene.CreateChild(root.transform, "Computer", Vector3.zero);
            Transform chair = EditorScene.CreateChild(root.transform, "Chair", Vector3.zero);
            Transform stand = EditorScene.CreateChild(root.transform, "StandPoint", new Vector3(0f, 0f, SaveStation.StandDistance));
            Transform right = EditorScene.CreateChild(root.transform, "WalkInRight", Vector3.zero);
            Transform left = EditorScene.CreateChild(root.transform, "WalkInLeft", Vector3.zero);
            EditorSerialized.SetReference(station, SaveStation.ChairField, chair);
            EditorSerialized.SetReference(station, SaveStation.StandPointField, stand);
            EditorSerialized.SetReference(station, SaveStation.WalkInRightField, right);
            EditorSerialized.SetReference(station, SaveStation.WalkInLeftField, left);
            station.FitToModels();
            basePrefab = PrefabUtility.SaveAsPrefabAsset(root, SavePointBasePath);
            Object.DestroyImmediate(root);
            Debug.Log("[SavePoint] Base prefab built: " + SavePointBasePath);
        }

        foreach (var v in SavePointVariants)
        {
            string path = $"{SavePointPrefabFolder}/{v.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[SavePoint] Prefab already exists, kept: " + path);
                continue;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
            inst.name = v.name;
            bool ok = AddSlotModel(inst.transform, "Desk", v.desk, preview)
                    & AddSlotModel(inst.transform, "Computer", v.pc, preview)
                    & AddSlotModel(inst.transform, "Chair", v.chair, preview);
            if (!ok) { Object.DestroyImmediate(inst); continue; }
            var station = inst.GetComponent<SaveStation>();
            EditorSerialized.SetString(station, SaveStation.LocationNameField, v.location);
            station.FitToModels();
            PrefabUtility.SaveAsPrefabAsset(inst, path);   // instance of the base -> saved as a Prefab Variant
            Object.DestroyImmediate(inst);
            Debug.Log($"[SavePoint] Variant built: {path} ({v.location})");
        }
    }

    // Places a model in a save point slot.
    private static bool AddSlotModel(Transform root, string slotName, string model, Scene preview)
    {
        Transform slot = EditorScene.FindDeep(root, slotName);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePointPropsFolder}/{model}.fbx");
        if (slot == null || asset == null)
        {
            Debug.LogWarning($"[SavePoint] Missing slot '{slotName}' or model {SavePointPropsFolder}/{model}.fbx");
            return false;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
        go.transform.SetParent(slot, false);
        return true;
    }

    // Places a save point prefab variant.
    internal static SaveStation PlaceSavePoint(Transform parent, string variant, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePointPrefabFolder}/{variant}.prefab");
        if (prefab == null) { Debug.LogWarning($"[Helena] No {variant} prefab: run menu 3 to test saving."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return go.GetComponentInChildren<SaveStation>();
    }
}
