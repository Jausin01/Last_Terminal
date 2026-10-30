using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Menu "Last Terminal/22 - Build Title Screen": Title.unity, the first scene of the build. The protagonist's desk by the
// window on a rainy night (Art/Props/Menu: SM_MenuRoom / Props / PhotoFrame / Town from tools/menu models.py, SM_MenuMug from mug.py):
// the photo of the four of them, books and the ivy, the cat mug, the diary; rain on the glass and outside, the warm
// lamp behind the curtain, the town's lit windows; the scene variants (TitleVariants). The menu over it is built by
// TitleMenuUiBuilder.
// Scene space: x right, y up, z toward the window (the wall at z 1.0, the window x 0.05..1.85, y 0.86..2.15), the
// desk top at 0.75, the street outside at y -6.5.
public static class TitleSceneBuilder
{
    // Scene and art paths.
    private const string ScenePath = "Assets/Scenes/Title.unity";
    internal const string ArtFolder = "Assets/Art/Props/Menu";
    private const string MatFolder = ArtFolder + "/Materials";
    // The window opening (x0, x1, y0, y1) and the glass plane.
    private static readonly Vector4 Window = new Vector4(0.05f, 1.85f, 0.86f, 2.15f);
    private const float GlassZ = 1.03f;
    // The camera: where it stands, what it looks at, its vertical field of view.
    private static readonly Vector3 CameraAt = new Vector3(0.50f, 1.00f, -0.50f);
    private static readonly Vector3 CameraLook = new Vector3(0.45f, 0.90f, 1.0f);
    private const float CameraFov = 49f;
    // The photo frame on the desk (turned a little toward the camera).
    private static readonly Vector3 FramePosition = new Vector3(0.12f, 0.75f, 0.52f);
    private const float FrameYaw = 16f;
    // The mug on its coaster (right of the window), turned so the cat faces the camera.
    private static readonly Vector3 MugPosition = new Vector3(0.98f, 0.756f, 0.26f);
    private const float MugYaw = 25f;
    // The models of the scene (Art/Props/Menu).
    private static readonly string[] Models = { "SM_MenuRoom", "SM_MenuProps", "SM_MenuPhotoFrame", "SM_MenuMug", "SM_MenuTown" };
    // Glass smoothness: wet but not mirror-like (higher shows the lights as bright blobs on the drops).
    private const float GlassSmoothness = 0.45f;

    // URP Lit property ids.
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    // Menu 22: builds the title scene and puts it first in the build.
    [MenuItem("Last Terminal/22 - Build Title Screen")]
    public static void BuildTitleScene()
    {
        foreach (string model in Models)
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{model}.fbx") == null)
            {
                Debug.LogError($"[Title] {ArtFolder}/{model}.fbx not found: copy the menu art first.");
                return;
            }
        Shader lit = EditorAssets.DefaultLitShader();
        ImportArt(lit);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("Title").transform;
        var set = new GameObject("Set").transform;
        set.SetParent(root, false);
        Place("SM_MenuRoom", set, Vector3.zero, 0f);
        Place("SM_MenuProps", set, Vector3.zero, 0f);
        Place("SM_MenuPhotoFrame", set, FramePosition, FrameYaw);
        Place("SM_MenuMug", set, MugPosition, MugYaw);
        Place("SM_MenuTown", set, Vector3.zero, 0f);
        BuildGlass(set, lit);
        BuildRain(set);
        BuildLights(root);
        BuildCamera(root);
        TitleVariants variants = BuildVariants(root);

        var settingsGo = new GameObject("Settings");
        var settings = settingsGo.AddComponent<CameraSettings>();
        var ambience = new GameObject("RainSound");
        var rainSource = ambience.AddComponent<AudioSource>();
        rainSource.spatialBlend = 0f;
        rainSource.volume = 0.35f;
        ambience.AddComponent<RainAmbience>();
        var uiAudio = new GameObject("UiAudio").AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.spatialBlend = 0f;

        TitleMenuUiBuilder.Build(root, settings, uiAudio, variants);

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuild(ScenePath);
        Debug.Log($"[Title] {ScenePath} built and first in the Build Settings.");
    }

    // ------------------------------------------------------------------ art
    // The models' materials: textured Lit; the curtain and the glass transparent, the ivy alpha-tested, the town's
    // windows glowing, the sky unlit.
    private static void ImportArt(Shader lit)
    {
        EditorAssets.EnsureFolder("Assets/Art/Props", "Menu");
        EditorAssets.EnsureFolder(ArtFolder, "Materials");
        Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{name}.png");
        Material Opaque(string name, string tex, float smooth) => EditorAssets.MakeMaterial($"{MatFolder}/{name}.mat", Tex(tex), smooth, false, lit);

        var map = new Dictionary<string, Material>
        {
            ["M_MenuWood"] = Opaque("M_MenuWood", "T_MenuWood", 0.55f),
            ["M_MenuWoodDark"] = Opaque("M_MenuWoodDark", "T_MenuWoodDark", 0.45f),
            ["M_MenuWall"] = Opaque("M_MenuWall", "T_MenuWall", 0.1f),
            ["M_MenuBooks"] = Opaque("M_MenuBooks", "T_MenuBooks", 0.2f),
            ["M_MenuPages"] = Opaque("M_MenuPages", "T_MenuPages", 0.05f),
            ["M_MenuCeramic"] = Opaque("M_MenuCeramic", "T_MenuCeramic", 0.6f),
            ["M_MenuMug"] = Opaque("M_MenuMug", "T_MenuMug", 0.65f),
            ["M_MenuCoffee"] = Opaque("M_MenuCoffee", "T_MenuCoffee", 0.9f),   // liquid
            ["M_MenuCork"] = Opaque("M_MenuCork", "T_MenuCork", 0.05f),
            ["M_MenuNotebook"] = Opaque("M_MenuNotebook", "T_MenuNotebook", 0.25f),
            ["M_MenuPhoto"] = Opaque("M_MenuPhoto", "T_MenuPhoto", 0.75f),
            ["M_MenuRoof"] = Opaque("M_MenuRoof", "T_MenuRoof", 0.5f),
            ["M_MenuStreet"] = Opaque("M_MenuStreet", "T_MenuStreet", 0.85f),   // wet
            ["M_MenuLeaf"] = EditorAssets.MakeMaterial($"{MatFolder}/M_MenuLeaf.mat", Tex("T_MenuLeaf"), 0.35f, true, lit),
        };
        Material curtain = CasinoBuilder.TransparentMaterial($"{MatFolder}/M_MenuCurtain.mat", Tex("T_MenuCurtain"), lit, false);
        curtain.SetFloat("_Cull", (float)CullMode.Off);
        map["M_MenuCurtain"] = curtain;
        Material town = Opaque("M_MenuTown", "T_MenuTown", 0.3f);
        town.EnableKeyword("_EMISSION");
        town.SetTexture(EmissionMapId, Tex("T_MenuTownGlow"));
        town.SetColor(EmissionColorId, new Color(1.6f, 1.15f, 0.7f));
        town.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        map["M_MenuTown"] = town;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        map["M_MenuSky"] = EditorAssets.MakeMaterial($"{MatFolder}/M_MenuSky.mat", Tex("T_MenuSky"), 0f, false, unlit != null ? unlit : lit);
        foreach (Material m in map.Values) EditorUtility.SetDirty(m);

        foreach (string model in Models)
        {
            if (!(AssetImporter.GetAtPath($"{ArtFolder}/{model}.fbx") is ModelImporter mi)) continue;
            foreach (var kv in map) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    // A model in the scene (static, no colliders: nothing walks here).
    private static GameObject Place(string model, Transform parent, Vector3 position, float yaw)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{model}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = model;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        foreach (Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // ------------------------------------------------------------------ rain
    // The window glass: droplets (still) and streaks running down (RainGlass), two transparent quads in the frame.
    private static void BuildGlass(Transform parent, Shader lit)
    {
        var glass = new GameObject("WindowGlass").transform;
        glass.SetParent(parent, false);
        Vector3 center = new Vector3((Window.x + Window.y) * 0.5f, (Window.z + Window.w) * 0.5f, GlassZ);
        Vector3 size = new Vector3(Window.y - Window.x - 0.1f, Window.w - Window.z - 0.1f, 1f);
        Material drops = CasinoBuilder.TransparentMaterial($"{MatFolder}/M_MenuGlassDrops.mat", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_MenuGlassDrops.png"), lit, false);
        drops.SetTextureScale("_BaseMap", new Vector2(2f, 1.4f));
        drops.SetFloat("_Smoothness", GlassSmoothness);
        Material streaks = CasinoBuilder.TransparentMaterial($"{MatFolder}/M_MenuGlassStreaks.mat", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_MenuGlassStreaks.png"), lit, false);
        streaks.SetFloat("_Smoothness", GlassSmoothness);
        EditorUtility.SetDirty(drops);
        EditorUtility.SetDirty(streaks);
        Pane(glass, "Drops", center + Vector3.back * 0.004f, size, drops);
        GameObject run = Pane(glass, "Streaks", center, size, streaks);
        run.AddComponent<RainGlass>();
    }

    // A quad facing the room (-z), no collider.
    private static GameObject Pane(Transform parent, string objectName, Vector3 center, Vector3 size, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = objectName;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.position = center;
        q.transform.localScale = size;
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return q;
    }

    // The rain outside: stretched drops falling from a wide box above the street, in front of the houses.
    private static void BuildRain(Transform parent)
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0.9f, 4.5f, 9f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 1.0f;
        main.startSpeed = 14f;
        main.startSize = 0.025f;
        main.startColor = new Color(0.72f, 0.78f, 0.9f, 0.45f);
        main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 2200f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = new Vector3(90f, 0f, 0f);       // the box's +z (its emitting direction) points down
        shape.scale = new Vector3(26f, 14f, 0.1f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.035f;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material drop = CasinoBuilder.TransparentMaterial($"{MatFolder}/M_MenuRain.mat", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_MenuRainDrop.png"),
                                                         particles != null ? particles : EditorAssets.DefaultLitShader(), false);
        renderer.sharedMaterial = drop;
    }

    // ------------------------------------------------------------------ light
    // Night: a dark blue ambient and fog over the town, the warm lamp left of the window (behind the curtain and the
    // ivy), a cool fill coming in through the window, a few warm street lights outside.
    private static void BuildLights(Transform root)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.08f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.14f);
        RenderSettings.fogDensity = 0.022f;
        var lights = new GameObject("Lights").transform;
        lights.SetParent(root, false);
        Light Point(string n, Vector3 at, Color c, float intensity, float range, LightShadows shadows)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(lights, false);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            return l;
        }
        Light lamp = Point("DeskLamp", new Vector3(-1.0f, 1.35f, 0.55f), new Color(1f, 0.62f, 0.32f), 3.2f, 3.6f, LightShadows.Soft);
        lamp.shadowStrength = 0.75f;
        Point("LampBounce", new Vector3(0.3f, 0.95f, 0.2f), new Color(1f, 0.7f, 0.45f), 0.6f, 1.6f, LightShadows.None);
        var moon = new GameObject("WindowFill").AddComponent<Light>();
        moon.transform.SetParent(lights, false);
        moon.type = LightType.Directional;
        moon.color = new Color(0.45f, 0.55f, 0.85f);
        moon.intensity = 0.35f;
        moon.shadows = LightShadows.None;
        moon.transform.rotation = Quaternion.LookRotation(new Vector3(-0.15f, -0.35f, -1f));
        Point("WindowRim", new Vector3(1.2f, 1.6f, 0.85f), new Color(0.5f, 0.6f, 0.95f), 0.5f, 1.8f, LightShadows.None);
        foreach (Vector3 p in new[] { new Vector3(-3f, -4.2f, 6.5f), new Vector3(6.5f, -4.2f, 7f), new Vector3(1.5f, -3.5f, 13f) })
            Point("StreetLight", p, new Color(1f, 0.72f, 0.4f), 2.5f, 7f, LightShadows.None);
    }

    // The camera at the desk, swaying a little; it hears the rain.
    private static Camera BuildCamera(Transform root)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(CameraAt, Quaternion.LookRotation(CameraLook - CameraAt));
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = CameraFov;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 150f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.1f);
        go.AddComponent<AudioListener>();
        go.AddComponent<MenuCameraSway>();
        return cam;
    }

    // ------------------------------------------------------------------ variants
    // The scene's versions for the newest save. The first: the desk as it is built (no save yet). Add more in the
    // Inspector of SceneVariants (objects to show / hide + flags).
    private static TitleVariants BuildVariants(Transform root)
    {
        var variants = new GameObject("SceneVariants").AddComponent<TitleVariants>();
        variants.transform.SetParent(root, false);
        var so = new SerializedObject(variants);
        SerializedProperty list = so.FindProperty(TitleVariants.VariantsField);
        list.arraySize = 1;
        SerializedProperty first = list.GetArrayElementAtIndex(0);
        first.FindPropertyRelative(nameof(TitleVariant.name)).stringValue = "First time (no save)";
        first.FindPropertyRelative(nameof(TitleVariant.save)).enumValueIndex = (int)TitleVariant.SaveCondition.NoSave;
        so.ApplyModifiedPropertiesWithoutUndo();
        return variants;
    }

    // The scene goes first in the Build Settings (the game starts on it); the others keep their order.
    private static void PutFirstInBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == scenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
