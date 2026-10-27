using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The look of the indoor scenes (Silent Hill 2 / 3 style): closed rooms (a ceiling over every room), lit only by
// ceiling lamps (spot lights straight down, soft shadows, a glowing fixture), some of them failing (LightFlicker),
// dark fog, low ambient light, and a color grade (neutral tonemapping, low saturation, greenish shadows, soft bloom).
// The grade is its own profile (Assets/Data/Atmosphere, made once, tune it in the inspector); the CCTV filter
// (grain, scanlines...) stays on top of it. Used by the facility (Last Terminal/4) and the asylum (Last Terminal/17).
internal static class AtmosphereBuilder
{
    // Where the grade profile is saved.
    private const string DataFolder = "Assets/Data/Atmosphere";
    private const string ProfilePath = DataFolder + "/Atmosphere_Indoor2.asset";   // (v1 was too dark: ACES)
    // Where the lamp fixture materials are saved.
    private const string MaterialFolder = "Assets/Art/Environment/Materials";
    private const float LampSpacing = 6f;      // a long room gets a lamp every 6 m
    private const float LampHeight = 2.88f;    // just under the ceiling (the CCTV cameras hang at 2.75)

    // One scene's look: fog color / density, ambient light and the grade values.
    internal struct Look
    {
        // Fog color and density, ambient color.
        public Color Fog;
        public float FogDensity;
        public Color Ambient;
        public float LampScale;              // the rooms' light intensities x this
        public string[] FlickerRooms;        // the rooms whose lamps fail (display names)
        public string CeilingTexture;        // empty = the facility wall texture, tinted
    }

    // The asylum look.
    internal static readonly Look Asylum = new Look
    {
        Fog = new Color(0.07f, 0.075f, 0.07f), FogDensity = 0.045f, Ambient = new Color(0.1f, 0.105f, 0.11f), LampScale = 6f,
        FlickerRooms = new[] { "Isolation Wing", "Service Hall", "Bathroom", "Service Corridor", "Observation" },
        CeilingTexture = "Assets/Art/Props/Asylum/T_AsyCeiling.png",
    };

    // The facility look.
    internal static readonly Look Facility = new Look
    {
        Fog = new Color(0.08f, 0.08f, 0.085f), FogDensity = 0.03f, Ambient = new Color(0.12f, 0.125f, 0.13f), LampScale = 5f,
        FlickerRooms = new[] { "Corridor", "Storage" },
    };

    // ceilings + lamps + fog + grade for these rooms (their "Light" child from FacilityBuilder.AddRoom is replaced)
    internal static void Apply(Transform root, IEnumerable<Room> rooms, Shader lit, Look look)
    {
        EnsurePipeline();
        EditorAssets.EnsureFolder("Assets/Art/Environment", "Materials");
        string env = FacilityBuilder.EnvFolder;
        bool own = !string.IsNullOrEmpty(look.CeilingTexture);
        Material ceiling = FacilityBuilder.MaterialFor(own ? $"{MaterialFolder}/M_CeilingTiles.mat" : $"{MaterialFolder}/M_Ceiling.mat",
                                                       own ? look.CeilingTexture : $"{env}/T_Wall.png", 0.05f, lit, env);
        ceiling.SetColor("_BaseColor", own ? Color.white : new Color(0.55f, 0.55f, 0.52f));
        EditorUtility.SetDirty(ceiling);

        // ceilings: not in the NavMesh geometry (nothing walks up there), they close the rooms for the lamps' shadows
        var ceilings = new GameObject("Ceilings").transform;
        ceilings.SetParent(root, false);
        float h = FacilityBuilder.WallHeight, t = FacilityBuilder.WallThickness;
        var flicker = new HashSet<string>(look.FlickerRooms ?? new string[0]);
        foreach (Room room in rooms)
        {
            if (room == null) continue;
            Rect r = room.FloorRect;
            FacilityBuilder.Block(ceilings, "Ceiling_" + room.DisplayName.Replace(" ", ""), new Vector3(r.center.x, h + 0.05f, r.center.y),
                                  new Vector3(r.width + t, 0.1f, r.height + t), ceiling, 2f);
            Lamps(room, lit, look, flicker.Contains(room.DisplayName));
        }

        // fog, ambient
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = look.Fog;
        RenderSettings.fogDensity = look.FogDensity;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = look.Ambient;

        // the grade: under the CCTV filter volume
        var grade = new GameObject("Atmosphere");
        grade.transform.SetParent(root, false);
        var volume = grade.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = -1f;
        volume.sharedProfile = EnsureProfile();
    }

    // the room's lamps: spots down from the ceiling (one per LampSpacing along the long side), soft shadows
    private static void Lamps(Room room, Shader lit, Look look, bool failing)
    {
        Transform old = room.transform.Find("Light");
        Color color = Color.white;
        float intensity = 1.5f;
        if (old != null && old.TryGetComponent(out Light oldLight))
        {
            color = oldLight.color;
            intensity = oldLight.intensity;
        }
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Rect r = room.FloorRect;
        bool alongX = r.width >= r.height;
        float length = alongX ? r.width : r.height;
        int count = Mathf.Max(1, Mathf.RoundToInt(length / LampSpacing));
        Material glow = FixtureMaterial(lit, color);
        for (int i = 0; i < count; i++)
        {
            float k = (i + 0.5f) / count;
            Vector3 at = alongX ? new Vector3(r.xMin + r.width * k, LampHeight, r.center.y) : new Vector3(r.center.x, LampHeight, r.yMin + r.height * k);
            var go = new GameObject(count > 1 ? $"Lamp_{i + 1}" : "Lamp");
            go.transform.SetParent(room.transform, true);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));   // pointing down
            var lamp = go.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = color;
            lamp.intensity = intensity * look.LampScale;
            lamp.range = 10f;
            lamp.spotAngle = 140f;
            lamp.innerSpotAngle = 55f;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.9f;
            lamp.shadowBias = 0.03f;
            lamp.shadowNormalBias = 0.3f;
            // low shadow resolution tier: many lamps share the atlas (read-only property: set through its serialized field)
            var tier = new SerializedObject(lamp.GetUniversalAdditionalLightData());
            SerializedProperty tierProp = tier.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tierProp != null) { tierProp.intValue = 0; tier.ApplyModifiedPropertiesWithoutUndo(); }

            // the fixture: a glowing panel on the ceiling
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.name = "Fixture";
            panel.transform.SetParent(go.transform, false);
            panel.transform.position = new Vector3(at.x, FacilityBuilder.WallHeight - 0.03f, at.z);
            panel.transform.rotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(0.6f, 0.05f, 0.25f);
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // a failing lamp: the first one of the room
            if (failing && i == 0)
            {
                var f = go.AddComponent<LightFlicker>();
                EditorSerialized.SetReference(f, LightFlicker.LampField, lamp);
                EditorSerialized.SetReference(f, LightFlicker.FixtureField, renderer);
            }
        }
    }

    // Glowing lamp fixture material (made once).
    private static Material FixtureMaterial(Shader lit, Color color)
    {
        string path = $"{MaterialFolder}/M_LampFixture_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material m = EditorAssets.MakeMaterial(path, null, 0.3f, false, lit);
        m.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.5f));
        OperatorRoomBuilder.Glow(m, null, color * 1.6f);
        return m;
    }

    // the color grade (made once: tune it in the inspector, never overwritten)
    private static VolumeProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null) return profile;
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "Atmosphere");
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var tone = Add<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);
        var color = Add<ColorAdjustments>(profile);
        color.postExposure.Override(0.5f);
        color.contrast.Override(22f);
        color.saturation.Override(-28f);
        color.colorFilter.Override(new Color(0.97f, 0.96f, 0.9f));
        var smh = Add<ShadowsMidtonesHighlights>(profile);
        smh.shadows.Override(new Vector4(0.92f, 1.02f, 0.97f, -0.06f));     // dark, a little green
        smh.highlights.Override(new Vector4(1.02f, 1f, 0.94f, 0f));         // warm, dirty light
        var bloom = Add<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.45f);
        bloom.scatter.Override(0.75f);
        var vignette = Add<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.5f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    // URP: per-pixel lamps that cast soft shadows, HDR (the bloom of the lamps). Only the settings found are changed.
    private static void EnsurePipeline()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
        var so = new SerializedObject(urp);
        SerializedProperty mode = so.FindProperty("m_AdditionalLightsRenderingMode");
        if (mode != null) mode.intValue = (int)LightRenderingMode.PerPixel;
        SetBool(so, "m_AdditionalLightShadowsSupported", true);
        SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096);   // ~25 lamps with shadows in the asylum
        SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
        SetBool(so, "m_SoftShadowsSupported", true);
        SetBool(so, "m_SupportsHDR", true);
        if (so.ApplyModifiedPropertiesWithoutUndo()) Debug.Log($"[Atmosphere] {urp.name}: lamp shadows / soft shadows / HDR turned on.");
    }

    // Sets an int / bool on a serialized object by name.
    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null && p.intValue < value) p.intValue = value;
    }

    // Sets a bool on a serialized object by name.
    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }
}
