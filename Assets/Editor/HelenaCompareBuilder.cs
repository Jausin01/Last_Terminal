using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Helena 2 (the Meshy model, cleaned, smoothed, re-rigged on Helena's Mixamo skeleton, new hair cards, PS2 shading):
// materials + import settings, and a test scene with the old and the new model side by side playing every clip of
// hers (ModelCompare). The model sits under Characters/Helena, so HelenaImport makes it Humanoid like the clips.
internal static class HelenaCompareBuilder
{
    // Helena 2 asset paths (model, textures), the old model, clip folders, the test scene and the weapon prefabs.
    private const string Folder = "Assets/Characters/Helena/Model2";
    internal const string ModelPath = Folder + "/Helena2.fbx";
    private const string BasePath = Folder + "/T_Helena2_Base.png";
    internal const string NormalPath = Folder + "/T_Helena2_Normal.png";
    internal const string BodyMaterialPath = Folder + "/Materials/M_Helena2_Body.mat";   // made by menu 18
    private const string MetalSmoothPath = Folder + "/T_Helena2_MetalSmooth.png";
    private const string HairPath = Folder + "/T_Helena2_Hair.png";
    internal const string OldModelPath = "Assets/Characters/Helena/Animations/HelenaIdle.fbx";
    private const string AnimFolder = "Assets/Characters/Helena/Animations";
    private const string CastAnimFolder = "Assets/Characters/Cast/Animations";
    private const string ScenePath = "Assets/Scenes/HelenaCompare.unity";
    internal const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const float BodySmoothness = 0.7f;   // x the map (Meshy roughness)
    private const float HairCutoff = 0.35f;      // lower = fuller locks

    // URP Lit property ids set on her materials.
    private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int SpecularHighlightsId = Shader.PropertyToID("_SpecularHighlights");
    private static readonly int EnvironmentReflectionsId = Shader.PropertyToID("_EnvironmentReflections");

    // Menu: materials, import settings and the comparison scene.
    [MenuItem("Last Terminal/18 - Helena 2: Materials + Compare Scene (old vs new, every clip)")]
    private static void Build()
    {
        Shader lit = EditorAssets.DefaultLitShader();
        if (lit == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) { Debug.LogError("[Helena2] Model not found: " + ModelPath); return; }
        var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(OldModelPath);
        if (oldModel == null) { Debug.LogError("[Helena2] Old model not found: " + OldModelPath + " (menus 0, 1 first)."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SetupMaterials(lit);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman || !avatar.isValid)
            Debug.LogWarning("[Helena2] The model's avatar is not a valid Humanoid: open Helena2.fbx > Rig > Configure.");

        AnimationClip[] clips = Clips();
        WeaponGrips.ApplyAll();   // the weapon prefabs get her grips (shown on the weapon clips)
        BuildScene(ScenePath, oldModel, "Antiga", model, "Nova", clips, null, true);
        Debug.Log($"[Helena2] Compare scene: {ScenePath}, {clips.Length} clips. Play: Right / Left = clip, Space = pause, Q / E = turn.");
    }

    // what Unity made of both models' rigs (bone mapping + the T-pose it stores), for Claude: <project>/PoseReports/avatars.txt
    [MenuItem("Last Terminal/20 - Helena 2: Export Avatar Report (for Claude)")]
    private static void ExportAvatars()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string path in new[] { OldModelPath, ModelPath })
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            sb.AppendLine($"=== {path}  type {(mi != null ? mi.animationType.ToString() : "?")}  avatar {(avatar != null ? avatar.name : "none")}  human {(avatar != null && avatar.isHuman)}  valid {(avatar != null && avatar.isValid)}");
            if (avatar == null) continue;
            HumanDescription hd = avatar.humanDescription;
            sb.AppendLine($"armStretch {hd.armStretch} legStretch {hd.legStretch} upperArmTwist {hd.upperArmTwist} lowerArmTwist {hd.lowerArmTwist} upperLegTwist {hd.upperLegTwist} lowerLegTwist {hd.lowerLegTwist} feetSpacing {hd.feetSpacing} hasTranslationDoF {hd.hasTranslationDoF}");
            foreach (HumanBone b in hd.human) sb.AppendLine($"H {b.humanName} -> {b.boneName}");
            foreach (SkeletonBone b in hd.skeleton)
                sb.AppendLine($"S {b.name} p {b.position.x:0.#####} {b.position.y:0.#####} {b.position.z:0.#####} r {b.rotation.x:0.#####} {b.rotation.y:0.#####} {b.rotation.z:0.#####} {b.rotation.w:0.#####}");
        }
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "PoseReports"));
        System.IO.Directory.CreateDirectory(folder);
        string file = System.IO.Path.Combine(folder, "avatars.txt");
        System.IO.File.WriteAllText(file, sb.ToString());
        Debug.Log("[Helena2] Avatar report: " + file);
    }

    // ------------------------------------------------------------------ materials
    private static void SetupMaterials(Shader lit)
    {
        Texture(NormalPath, t => { t.textureType = TextureImporterType.NormalMap; });
        Texture(MetalSmoothPath, t => { t.sRGBTexture = false; t.alphaSource = TextureImporterAlphaSource.FromInput; });
        Texture(HairPath, t => { t.alphaIsTransparency = true; t.mipMapsPreserveCoverage = true; t.alphaTestReferenceValue = HairCutoff; });
        Texture(BasePath, t => { t.maxTextureSize = 2048; });

        EditorAssets.EnsureFolder(Folder, "Materials");
        Material body = EditorAssets.MakeMaterial(Folder + "/Materials/M_Helena2_Body.mat",
                                                     AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath), BodySmoothness, false, lit);
        body.SetTexture(BumpMapId, AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
        body.EnableKeyword("_NORMALMAP");
        body.SetTexture(MetallicGlossMapId, AssetDatabase.LoadAssetAtPath<Texture2D>(MetalSmoothPath));
        body.SetFloat(MetallicId, 1f);   // x the map (it is ~0: leather, cloth, skin)
        body.SetFloat(SmoothnessId, BodySmoothness);
        body.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(body);

        Material hair = EditorAssets.MakeMaterial(Folder + "/Materials/M_Helena2_Hair.mat",
                                                     AssetDatabase.LoadAssetAtPath<Texture2D>(HairPath), 0.12f, true, lit);
        hair.SetFloat(CutoffId, HairCutoff);
        // no sky reflection / highlights on the dark hair: at grazing angles they drew a pale blue outline
        hair.SetFloat(SpecularHighlightsId, 0f);
        hair.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        hair.SetFloat(EnvironmentReflectionsId, 0f);
        hair.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(hair);

        var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Helena2_Body"), body);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Helena2_Hair"), hair);
        mi.importNormals = ModelImporterNormals.Import;   // the hair's normals are radial (one soft volume)
        // the avatar mapping is remade from the file (a mapping kept from an older Helena2.fbx no longer matches its bones)
        HumanDescription human = mi.humanDescription;
        human.human = new HumanBone[0];
        human.skeleton = new SkeletonBone[0];
        mi.humanDescription = human;
        mi.SaveAndReimport();
        AssetDatabase.SaveAssets();
    }

    // Changes a texture's import settings.
    private static void Texture(string path, System.Action<TextureImporter> set)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter t)) { Debug.LogWarning("[Helena2] Texture not found: " + path); return; }
        set(t);
        t.SaveAndReimport();
    }

    // every clip of hers: her animation files + the bed clips of the asylum
    internal static AnimationClip[] Clips()
    {
        var clips = new List<AnimationClip>();
        var paths = AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        if (AssetDatabase.IsValidFolder(CastAnimFolder))
            paths.AddRange(AssetDatabase.FindAssets("Cast_Bed t:Model", new[] { CastAnimFolder }).Select(AssetDatabase.GUIDToAssetPath));
        foreach (string path in paths.Distinct())
            clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")));
        return clips.Distinct().OrderBy(c => c.name).ToArray();
    }

    // ------------------------------------------------------------------ scene
    // two models side by side playing the clips (the right one plays rightClips[i] instead when set)
    internal static void BuildScene(string scenePath, GameObject leftModel, string leftLabel, GameObject rightModel, string rightLabel,
                                    AnimationClip[] clips, AnimationClip[] rightClips, bool closeFingers)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.28f, 0.29f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.1f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);

        var key = new GameObject("Key Light").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.3f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.Euler(40f, 155f, 0f);
        var rim = new GameObject("Rim Light").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.intensity = 0.7f;
        rim.color = new Color(0.8f, 0.86f, 1f);
        rim.transform.rotation = Quaternion.Euler(30f, -20f, 0f);

        var camGo = new GameObject("Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.fieldOfView = 38f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
        camGo.transform.position = new Vector3(0f, 1.25f, -4.4f);
        camGo.transform.LookAt(new Vector3(0f, 0.95f, 0f));

        Animator oldA = Place(leftModel, "Helena " + leftLabel, -0.65f);
        Animator newA = Place(rightModel, "Helena " + rightLabel, 0.65f);

        // weapons: a display copy of each weapon prefab per model (only its Weapon component: no combat logic here)
        var weaponRoot = new GameObject("Weapons").transform;
        var display = new List<Weapon>();
        foreach (Animator a in new[] { oldA, newA })
            foreach (string w in ShownWeapons) display.Add(DisplayCopy(w, weaponRoot));
        var weaponOfClip = new int[clips.Length];
        var aimOfClip = new bool[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            weaponOfClip[i] = WeaponFor(clips[i].name);
            aimOfClip[i] = IsAim(clips[i].name);
        }

        var compare = new GameObject("Compare").AddComponent<ModelCompare>();
        compare.EditorSet(new[] { oldA, newA }, new[] { leftLabel, rightLabel }, clips, cam,
                          display.ToArray(), ShownWeapons.Length, weaponOfClip, aimOfClip, rightClips, closeFingers);

        EditorAssets.EnsureFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
    }

    // the weapon shown on a clip, from its name (index in ShownWeapons, -1 = none)
    internal static readonly string[] ShownWeapons = { "Pistol1911", "ShotgunMossberg", "KitchenKnife", "Bat" };
    internal static bool IsAim(string clip) => clip.Contains("Aim") || clip.Contains("Gunplay");
    internal static int WeaponFor(string clip)
    {
        if (clip.Contains("Shotgun")) return 1;
        if (clip.Contains("Pistol")) return 0;
        if (clip.Contains("Knife") || clip.Contains("Blade")) return 2;
        if (clip.Contains("Blunt")) return 3;
        return -1;
    }

    // A display copy of a weapon prefab (no game logic).
    private static Weapon DisplayCopy(string weapon, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponPrefabFolder}/Weapon_{weapon}.prefab");
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(b is Weapon)) Object.DestroyImmediate(b);
        go.SetActive(false);
        return go.GetComponent<Weapon>();
    }

    // An instance of a model placed at x in the scene.
    private static Animator Place(GameObject model, string name, float x)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = name;
        go.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));   // facing the camera
        Animator a = go.GetComponent<Animator>();
        if (a == null) a = go.AddComponent<Animator>();
        a.runtimeAnimatorController = null;
        a.applyRootMotion = false;
        a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return a;
    }
}
