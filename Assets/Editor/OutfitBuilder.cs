using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Her outfits (skins): one material per recolored texture (the textures follow the UV layout of her own clothes,
// so the same meshes and every animation keep working) and one OutfitDefinition asset per outfit.
// Accessories (bunny ears, tail) are static meshes modeled in her rest pose: placed on her model, then parented
// to their bone keeping where they are, so they follow her head / hips.
// Textures: Assets/Characters/Helena/Textures/Outfits/T_Helena_<Outfit>_<Part>.png,
// accessories: Assets/Art/Props/Outfits/SM_Helena_<Outfit>Accessories.fbx + its texture. Called by Last Terminal/4.
public static class OutfitBuilder
{
    private const string TextureFolder = HelenaImport.Folder + "Textures/Outfits";
    private const string MaterialFolder = HelenaImport.Folder + "Materials/Outfits";
    private const string OriginalMaterialFolder = HelenaImport.Folder + "Materials";
    private const string AccessoryFolder = PropImport.Folder + "Outfits";
    private const string DataFolder = "Assets/Data/Outfits";
    private const string MeshFolder = HelenaImport.Folder + "Meshes";
    private static readonly string[] ArmBones = { "LeftShoulder", "LeftArm", "LeftForeArm", "RightShoulder", "RightArm", "RightForeArm" };

    private struct Outfit
    {
        public string key, asset, en, pt;
        public int price;
        public PartnerLine line;
        public (string original, string texture, float smooth)[] swaps;   // material of her model -> outfit texture
        public string[] hidden;                                           // her parts that go away
        public string accessoryModel, accessoryTexture;                   // optional
        public (string name, HumanBodyBones bone)[] accessories;
        public string[] smoothArms;                                       // parts reshaped: cloth folds of the arms smoothed
    }

    // holster, belt and pouch (they belong to the suit)
    private static readonly string[] Gear = { "Plane.001", "retop.001", "retop.003", "retop.004", "retop.005", "billas.001", "pants.003" };

    // the story: she starts the game locked up (test rooms); the asylum starts her as a patient (AsylumBuilder)
    private const string StartOutfitKey = "outfit_prison";
    internal const string PatientOutfitKey = "outfit_patient";

    private static readonly Outfit[] Outfits =
    {
        new Outfit
        {
            key = "outfit_prison", asset = "Outfit_Prison", en = "Prison Uniform", pt = "Uniforme de Prisão", price = 0, line = PartnerLine.OutfitChanged,
            swaps = new[]
            {
                ("jacket",   "T_Helena_Prison_Top",   0.15f),
                ("shirt",    "T_Helena_Prison_Shirt", 0.15f),
                ("suit",     "T_Helena_Prison_Pants", 0.15f),
                ("sneakers", "T_Helena_Prison_Shoes", 0.30f),
                ("skin",     "T_Helena_Prison_Skin",  0.25f),   // the bare midriff under her clothes = shirt
            },
            hidden = Gear,
            // the prisoner number: a patch on her left chest and a big one on her back (the jacket UVs are mirrored,
            // a painted one would show twice); its digits add up to the code of her cell's keypad (FacilityBuilder)
            accessoryModel = "SM_Helena_PrisonAccessories", accessoryTexture = "T_Helena_Prison_Tag",
            accessories = new[] { ("PrisonTag", HumanBodyBones.UpperChest), ("PrisonBack", HumanBodyBones.UpperChest) },
        },
        // the asylum: she got herself committed as a patient. The tag on her chest (PATIENT 2716 / BED 103) adds up to
        // the code of her cell (2819, AsylumBuilder): she doesn't see it, the operator does ("look at me")
        new Outfit
        {
            key = PatientOutfitKey, asset = "Outfit_Patient", en = "Patient Scrubs", pt = "Roupa de Paciente", price = 0, line = PartnerLine.OutfitChanged,
            swaps = new[]
            {
                ("jacket",   "T_Helena_Patient_Top",   0.12f),
                ("shirt",    "T_Helena_Patient_Shirt", 0.12f),
                ("suit",     "T_Helena_Patient_Pants", 0.12f),
                ("sneakers", "T_Helena_Patient_Shoes", 0.20f),
                ("skin",     "T_Helena_Patient_Skin",  0.25f),
            },
            hidden = Gear,
            accessoryModel = "SM_Helena_PatientAccessories", accessoryTexture = "T_Helena_Patient_Tag",
            accessories = new[] { ("PatientTag", HumanBodyBones.UpperChest) },
        },
        new Outfit
        {
            key = "outfit_pajama", asset = "Outfit_Pajama", en = "Pajamas", pt = "Pijama", price = 2500, line = PartnerLine.OutfitChanged,
            swaps = new[]
            {
                ("jacket",   "T_Helena_Pajama_Top",      0.20f),
                ("shirt",    "T_Helena_Pajama_Shirt",    0.15f),
                ("suit",     "T_Helena_Pajama_Shorts",   0.20f),
                ("sneakers", "T_Helena_Pajama_Slippers", 0.05f),
            },
            hidden = Gear,
        },
        // the top prize of the casino
        new Outfit
        {
            key = "outfit_bunny", asset = "Outfit_Bunny", en = "Bunny Suit", pt = "Coelhinha", price = 10000, line = PartnerLine.OutfitBunny,
            swaps = new[]
            {
                ("shirt",    "T_Helena_Bunny_Leotard", 0.45f),
                ("suit",     "T_Helena_Bunny_Tights",  0.35f),
                ("sneakers", "T_Helena_Bunny_Shoes",   0.60f),
                ("skin",     "T_Helena_Bunny_Skin",    0.25f),   // the bare midriff under her clothes = leotard
            },
            hidden = With(Gear, "jacket"),
            accessoryModel = "SM_Helena_BunnyAccessories", accessoryTexture = "T_Helena_Bunny_Accessories",
            accessories = new[] { ("BunnyEars", HumanBodyBones.Head), ("BunnyTail", HumanBodyBones.Hips) },
            smoothArms = new[] { "top.002" },   // bare arms: no sleeve folds on the shoulders / elbows
        },
    };

    private static string[] With(string[] list, string extra)
    {
        var l = new List<string>(list) { extra };
        return l.ToArray();
    }

    internal static List<OutfitDefinition> EnsureOutfits(Shader lit, GameObject helena)
    {
        var result = new List<OutfitDefinition>();
        HelenaSetupMenu.EnsureFolder(HelenaImport.Folder + "Materials", "Outfits");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Outfits");
        foreach (Outfit o in Outfits)
        {
            var swaps = new List<(Material original, Material replacement)>();
            bool ok = true;
            foreach (var s in o.swaps)
            {
                var original = AssetDatabase.LoadAssetAtPath<Material>($"{OriginalMaterialFolder}/{s.original.Replace(".", "_")}.mat");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{s.texture}.png");
                if (original == null || texture == null)
                {
                    Debug.LogWarning($"[Outfits] {o.asset}: missing {(original == null ? "material " + s.original + " (run menu 1)" : "texture " + TextureFolder + "/" + s.texture + ".png")}.");
                    ok = false;
                    continue;
                }
                Material m = HelenaSetupMenu.MakeMaterial($"{MaterialFolder}/M_{s.texture.Substring(2)}.mat", texture, s.smooth, false, lit);
                swaps.Add((original, m));
            }
            if (!ok) continue;

            string path = $"{DataFolder}/{o.asset}.asset";
            var outfit = AssetDatabase.LoadAssetAtPath<OutfitDefinition>(path);
            if (outfit == null)
            {
                outfit = ScriptableObject.CreateInstance<OutfitDefinition>();
                AssetDatabase.CreateAsset(outfit, path);
            }
            var so = new SerializedObject(outfit);
            so.FindProperty(OutfitDefinition.KeyField).stringValue = o.key;
            so.FindProperty(OutfitDefinition.DisplayNameField).stringValue = o.en;
            so.FindProperty(OutfitDefinition.DisplayNamePtField).stringValue = o.pt;
            so.FindProperty(OutfitDefinition.PriceField).intValue = o.price;
            so.FindProperty(OutfitDefinition.LineField).enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(PartnerLine)), o.line);
            SerializedProperty list = so.FindProperty(OutfitDefinition.SwapsField);
            list.arraySize = swaps.Count;
            for (int i = 0; i < swaps.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative(OutfitDefinition.MaterialSwap.OriginalField).objectReferenceValue = swaps[i].original;
                e.FindPropertyRelative(OutfitDefinition.MaterialSwap.ReplacementField).objectReferenceValue = swaps[i].replacement;
            }
            SetStrings(so.FindProperty(OutfitDefinition.HiddenPartsField), o.hidden);
            var meshes = new List<(string part, Mesh mesh)>();
            if (o.smoothArms != null)
                foreach (string part in o.smoothArms)
                {
                    Mesh m = SmoothedMesh(helena, part, o.asset);
                    if (m != null) meshes.Add((part, m));
                }
            SerializedProperty meshList = so.FindProperty(OutfitDefinition.MeshSwapsField);
            meshList.arraySize = meshes.Count;
            for (int i = 0; i < meshes.Count; i++)
            {
                SerializedProperty e = meshList.GetArrayElementAtIndex(i);
                e.FindPropertyRelative(OutfitDefinition.MeshSwap.PartField).stringValue = meshes[i].part;
                e.FindPropertyRelative(OutfitDefinition.MeshSwap.MeshField).objectReferenceValue = meshes[i].mesh;
            }
            var extras = new List<string>();
            if (o.accessories != null) foreach (var a in o.accessories) extras.Add(a.name);
            SetStrings(so.FindProperty(OutfitDefinition.ExtraPartsField), extras.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(outfit);
            result.Add(outfit);
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    // her part with the arm folds smoothed, saved as a mesh asset (rebuilt every time)
    private static Mesh SmoothedMesh(GameObject helena, string part, string outfitAsset)
    {
        SkinnedMeshRenderer source = null;
        foreach (SkinnedMeshRenderer r in helena.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.gameObject.name == part) { source = r; break; }
        if (source == null || source.sharedMesh == null) { Debug.LogWarning($"[Outfits] {outfitAsset}: part '{part}' not found on her model."); return null; }
        HelenaSetupMenu.EnsureFolder(HelenaImport.Folder.TrimEnd('/'), "Meshes");
        string path = $"{MeshFolder}/Mesh_{outfitAsset}_{part.Replace(".", "_")}.asset";
        AssetDatabase.DeleteAsset(path);
        Mesh mesh = MeshSmoother.SmoothByBones(source, ArmBones, 12);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void SetStrings(SerializedProperty prop, string[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    // her outfits on the scene instance: every skinned part of her model, plus the accessories on her bones
    internal static PartnerOutfit AddTo(GameObject helena, Shader lit, string startOutfit = null)
    {
        var parts = new List<SkinnedMeshRenderer>(helena.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        List<OutfitDefinition> outfits = EnsureOutfits(lit, helena);
        var extras = new List<Renderer>();
        Animator animator = helena.GetComponent<Animator>();
        foreach (Outfit o in Outfits)
            if (o.accessories != null) AddAccessories(o, helena.transform, animator, lit, extras);

        var outfit = helena.AddComponent<PartnerOutfit>();
        HelenaSetupMenu.SetReferenceArray(outfit, PartnerOutfit.PartsField, parts);
        HelenaSetupMenu.SetReferenceArray(outfit, PartnerOutfit.OutfitsField, outfits);
        HelenaSetupMenu.SetReferenceArray(outfit, PartnerOutfit.ExtrasField, extras);
        string startKey = string.IsNullOrEmpty(startOutfit) ? StartOutfitKey : startOutfit;
        OutfitDefinition start = outfits.Find(x => x.Key == startKey);
        if (start != null) HelenaSetupMenu.SetReference(outfit, PartnerOutfit.StartOutfitField, start);
        return outfit;
    }

    // The pieces are modeled in her bind pose (model space). Each one goes under its bone with the offset the bone
    // had in the bind pose (from the bind poses of her skinned parts), so it fits whatever pose the prefab is in.
    private static void AddAccessories(Outfit o, Transform helena, Animator animator, Shader lit, List<Renderer> extras)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{AccessoryFolder}/{o.accessoryModel}.fbx");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AccessoryFolder}/{o.accessoryTexture}.png");
        if (model == null || texture == null || animator == null)
        {
            Debug.LogWarning($"[Outfits] {o.asset}: accessories missing ({AccessoryFolder}/{o.accessoryModel}.fbx / {o.accessoryTexture}.png).");
            return;
        }
        Material mat = HelenaSetupMenu.MakeMaterial($"{MaterialFolder}/M_{o.accessoryTexture.Substring(2)}.mat", texture, 0.4f, false, lit);
        SkinnedMeshRenderer[] skinned = helena.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var temp = (GameObject)Object.Instantiate(model, helena, false);
        foreach (var a in o.accessories)
        {
            Transform piece = temp.transform.Find(a.name);
            // a model with a single mesh: Unity makes that mesh the root of the model (no child named after it),
            // so the piece is copied out of the root (the root is destroyed below)
            if (piece == null && temp.TryGetComponent(out MeshFilter rootMesh) && rootMesh.sharedMesh != null)
            {
                var copy = new GameObject(a.name);
                copy.transform.SetParent(temp.transform, false);
                copy.AddComponent<MeshFilter>().sharedMesh = rootMesh.sharedMesh;
                copy.AddComponent<MeshRenderer>();
                piece = copy.transform;
            }
            Transform bone = animator.GetBoneTransform(a.bone);
            if (bone == null && a.bone == HumanBodyBones.UpperChest) bone = animator.GetBoneTransform(HumanBodyBones.Chest);   // avatar without an upper chest
            if (piece == null || bone == null) { Debug.LogWarning($"[Outfits] {o.asset}: '{a.name}' or bone {a.bone} not found."); continue; }
            // piece in model space (as imported) -> bone space of the bind pose
            Matrix4x4 pieceInModel = helena.worldToLocalMatrix * piece.localToWorldMatrix;
            Matrix4x4 local = bone.worldToLocalMatrix * piece.localToWorldMatrix;   // fallback: the prefab pose
            if (TryBindPose(skinned, helena, bone, out Matrix4x4 boneInModel)) local = boneInModel.inverse * pieceInModel;
            else Debug.LogWarning($"[Outfits] {a.name}: no bind pose for {a.bone}, placed from the prefab pose.");
            piece.SetParent(bone, false);
            piece.localPosition = local.GetColumn(3);
            piece.localRotation = local.rotation;
            piece.localScale = local.lossyScale;
            var r = piece.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.enabled = false;   // shown by PartnerOutfit with the outfit
            extras.Add(r);
        }
        Object.DestroyImmediate(temp);
    }

    // where the bone was in her bind pose, in model space (from any skinned part it drives)
    private static bool TryBindPose(SkinnedMeshRenderer[] skinned, Transform helena, Transform bone, out Matrix4x4 boneInModel)
    {
        foreach (SkinnedMeshRenderer r in skinned)
        {
            if (r.sharedMesh == null) continue;
            int i = System.Array.IndexOf(r.bones, bone);
            Matrix4x4[] bind = r.sharedMesh.bindposes;
            if (i < 0 || i >= bind.Length) continue;
            Matrix4x4 meshInModel = helena.worldToLocalMatrix * r.transform.localToWorldMatrix;
            boneInModel = meshInModel * bind[i].inverse;
            return true;
        }
        boneInModel = Matrix4x4.identity;
        return false;
    }
}
