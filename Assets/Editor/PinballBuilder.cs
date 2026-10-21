using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using L = PinballLayout;

// Builds the ARCANE QUEST pinball machine (OperatorRoomBuilder places it): the cabinet, the playfield with its art,
// everything on it from PinballLayout (walls, posts, bumpers, slingshots, flippers, targets, saucer, plunger, light
// inserts), the backbox with its backglass and score display, and the PinballMachine wired to all of it.
// Meshes (flippers, slingshots) and materials are assets under Assets/Art/Props/Pinball, created once and updated
// in place (scenes keep their references).
internal static class PinballBuilder
{
    internal const string ArtFolder = "Assets/Art/Props/Pinball";
    private const string MatFolder = ArtFolder + "/Materials";
    private const string MeshFolder = ArtFolder + "/Meshes";
    private const float FlipperHeight = 0.022f;
    private const float RailHeight = 0.025f;
    private const float WallHeight = 0.06f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    private class Mats
    {
        public Material Playfield, Backglass, Cabinet, Wood, Chrome, Rubber, Plastic, Insert, Cap, Flipper, Target, Dark, BumperBase;
    }

    private static Mats EnsureMaterials(Shader lit)
    {
        HelenaSetupMenu.EnsureFolder("Assets/Art/Props", "Pinball");
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Materials");
        var m = new Mats
        {
            Playfield = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_PB_Playfield.mat", T("T_Pinball_Playfield"), 0.75f, false, lit),
            Backglass = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_PB_Backglass.mat", T("T_Pinball_Backglass"), 0.6f, false, lit),
            Cabinet = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_PB_Cabinet.mat", T("T_Pinball_Cabinet"), 0.45f, false, lit),
            Wood = ColorMat("M_PB_Wood", new Color(0.3f, 0.17f, 0.09f), 0.35f, 0f, lit),
            Chrome = ColorMat("M_PB_Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f, lit),
            Rubber = ColorMat("M_PB_Rubber", new Color(0.92f, 0.92f, 0.9f), 0.25f, 0f, lit),
            Plastic = ColorMat("M_PB_Plastic", new Color(0.45f, 0.2f, 0.75f), 0.7f, 0f, lit),
            Flipper = ColorMat("M_PB_Flipper", new Color(0.95f, 0.95f, 0.92f), 0.6f, 0f, lit),
            Target = ColorMat("M_PB_Target", new Color(0.3f, 0.9f, 1f), 0.6f, 0f, lit),
            Dark = ColorMat("M_PB_Dark", new Color(0.02f, 0.01f, 0.02f), 0.2f, 0f, lit),
            BumperBase = ColorMat("M_PB_BumperBase", new Color(0.12f, 0.05f, 0.2f), 0.6f, 0f, lit),
            Cap = ColorMat("M_PB_BumperCap", new Color(0.7f, 0.35f, 1f), 0.7f, 0f, lit),
            Insert = HelenaSetupMenu.MakeMaterial($"{MatFolder}/M_PB_Insert.mat", T("T_Pinball_Insert"), 0.8f, false, lit),
        };
        Emissive(m.Backglass, Color.white * 0.85f, T("T_Pinball_Backglass"));
        Emissive(m.Cap, Color.black, null);
        Emissive(m.Insert, Color.black, null);
        Emissive(m.Rubber, Color.black, null);
        Transparent(m.Insert);
        return m;
    }

    private static Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{n}.png");

    private static Material ColorMat(string name, Color c, float smooth, float metallic, Shader lit)
    {
        Material m = HelenaSetupMenu.MakeMaterial($"{MatFolder}/{name}.mat", null, smooth, false, lit);
        m.SetColor(BaseColorId, c);
        m.SetFloat(MetallicId, metallic);
        m.SetFloat(SmoothnessId, smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    // emission on (lights driven by a MaterialPropertyBlock need the keyword on the material)
    private static void Emissive(Material m, Color c, Texture2D map)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor(EmissionColorId, c);
        if (map != null) m.SetTexture(EmissionMapId, map);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
    }

    // URP Lit, transparent (alpha blended): the inserts let the art under them show
    private static void Transparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }

    // ------------------------------------------------------------------ meshes (convex prisms: flat top + sides)
    private static Mesh Prism(string name, IList<Vector2> outline, float height)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 up = Vector3.up;
        // top: fan from the centroid
        Vector2 c = Vector2.zero;
        foreach (Vector2 p in outline) c += p;
        c /= outline.Count;
        int center = verts.Count;
        verts.Add(new Vector3(c.x, height, c.y));
        int first = verts.Count;
        foreach (Vector2 p in outline) verts.Add(new Vector3(p.x, height, p.y));
        for (int i = 0; i < outline.Count; i++) AddTri(verts, tris, center, first + i, first + (i + 1) % outline.Count, up);
        // sides
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
            Vector3 mid = new Vector3((a.x + b.x) * 0.5f - c.x, 0f, (a.y + b.y) * 0.5f - c.y);
            int k = verts.Count;
            verts.Add(new Vector3(a.x, 0f, a.y)); verts.Add(new Vector3(b.x, 0f, b.y));
            verts.Add(new Vector3(b.x, height, b.y)); verts.Add(new Vector3(a.x, height, a.y));
            AddTri(verts, tris, k, k + 1, k + 2, mid);
            AddTri(verts, tris, k, k + 2, k + 3, mid);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    // winding chosen so the triangle faces 'outward' (Unity: clockwise seen from the front)
    private static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, outward) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
        else { t.Add(a); t.Add(c); t.Add(b); }
    }

    private static Mesh SaveMesh(Mesh mesh)
    {
        HelenaSetupMenu.EnsureFolder(ArtFolder, "Meshes");
        string path = $"{MeshFolder}/{mesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);   // same asset (GUID) for the scenes that use it
        Object.DestroyImmediate(mesh);
        return existing;
    }

    // tapered capsule outline (pivot at the origin, pointing +x)
    private static Vector2[] FlipperOutline(L.FlipperDef f)
    {
        var pts = new List<Vector2>();
        float d = Mathf.Asin(Mathf.Clamp((f.BaseRadius - f.TipRadius) / f.Length, -1f, 1f));
        const int n = 10;
        for (int i = 0; i <= n; i++)   // tip half circle (right side)
        {
            float a = Mathf.Lerp(-Mathf.PI / 2f + d, Mathf.PI / 2f - d, i / (float)n);
            pts.Add(new Vector2(f.Length + Mathf.Cos(a) * f.TipRadius, Mathf.Sin(a) * f.TipRadius));
        }
        for (int i = 0; i <= n; i++)   // base half circle (left side)
        {
            float a = Mathf.Lerp(Mathf.PI / 2f - d, 3f * Mathf.PI / 2f + d, i / (float)n);
            pts.Add(new Vector2(Mathf.Cos(a) * f.BaseRadius, Mathf.Sin(a) * f.BaseRadius));
        }
        return pts.ToArray();
    }

    // ------------------------------------------------------------------ the machine
    // localPos / yaw: where in parent; the player stands in front of it (its -z side)
    internal static PinballMachine Build(Transform parent, Shader lit, Vector3 localPos, float yaw, OperatorStation station, StoryProgress story)
    {
        Mats mats = EnsureMaterials(lit);
        var root = new GameObject("Pinball_ArcaneQuest");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var machine = root.AddComponent<PinballMachine>();
        HelenaSetupMenu.SetString(machine, OperatorInteractable.LabelEnField, "Pinball");
        HelenaSetupMenu.SetString(machine, OperatorInteractable.LabelPtField, "Fliperama");
        var col = root.AddComponent<BoxCollider>();   // used / blocks him (on the interactable itself)
        col.center = new Vector3(0f, 0.95f, 0.06f);
        col.size = new Vector3(0.68f, 1.9f, 1.32f);
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.6f;
        audio.volume = 0.8f;

        // the playfield frame: table space (x right, z up the table), tilted
        var pf = new GameObject("Playfield").transform;
        pf.SetParent(root.transform, false);
        pf.localPosition = new Vector3(-L.Width * 0.5f, 0.90f, -0.55f);
        pf.localRotation = Quaternion.Euler(-L.TiltDegrees, 0f, 0f);

        Quad(pf, "Art", new Vector3(L.Width * 0.5f, 0f, L.Length * 0.5f), new Vector2(L.Width, L.Length), mats.Playfield);

        // the tub around it
        Box(pf, "SideLeft", new Vector3(-0.035f, -0.09f, L.Length * 0.5f), new Vector3(0.05f, 0.30f, L.Length + 0.12f), mats.Cabinet);
        Box(pf, "SideRight", new Vector3(L.Width + 0.035f, -0.09f, L.Length * 0.5f), new Vector3(0.05f, 0.30f, L.Length + 0.12f), mats.Cabinet);
        Box(pf, "Front", new Vector3(L.Width * 0.5f, -0.09f, -0.035f), new Vector3(L.Width + 0.12f, 0.30f, 0.05f), mats.Cabinet);
        Box(pf, "Back", new Vector3(L.Width * 0.5f, -0.09f, L.Length + 0.035f), new Vector3(L.Width + 0.12f, 0.30f, 0.05f), mats.Cabinet);
        Box(pf, "Bottom", new Vector3(L.Width * 0.5f, -0.235f, L.Length * 0.5f), new Vector3(L.Width + 0.12f, 0.02f, L.Length + 0.12f), mats.Dark);
        Box(pf, "LockdownBar", new Vector3(L.Width * 0.5f, 0.07f, -0.035f), new Vector3(L.Width + 0.13f, 0.02f, 0.07f), mats.Chrome);
        Box(pf, "Apron", new Vector3(L.CenterX, 0.004f, 0.026f), new Vector3(0.39f, 0.008f, 0.052f), mats.Dark);
        foreach (float x in new[] { -0.062f, L.Width + 0.062f })   // flipper buttons
            Cylinder(pf, "FlipperButton", new Vector3(x, -0.06f, 0.07f), 0.012f, 0.01f, mats.Target, Quaternion.Euler(0f, 0f, 90f));

        // walls, rails, rubbers
        var walls = new GameObject("Walls").transform;
        walls.SetParent(pf, false);
        var rubbers = new List<Renderer>();
        foreach (L.Segment s in L.Walls)
        {
            switch (s.Kind)
            {
                case L.WallKind.Wall:
                {
                    Vector2 d = (s.B - s.A).normalized;
                    Vector2 n = new Vector2(-d.y, d.x);
                    Vector2 mid = (s.A + s.B) * 0.5f;
                    Vector2 inner = mid.y > L.ArcCenterY ? new Vector2(L.Width * 0.5f, L.ArcCenterY) : new Vector2(L.Width * 0.5f, 0.6f);
                    if (Vector2.Dot(mid - inner, n) < 0f) n = -n;   // outward
                    SegmentBox(walls, "Wall", s.A + n * 0.012f, s.B + n * 0.012f, 0.024f, WallHeight, mats.Wood, 0.012f);
                    break;
                }
                case L.WallKind.Gate:
                    SegmentBox(walls, "Gate", s.A, s.B, 0.004f, 0.012f, mats.Chrome, 0f, 0.02f);
                    break;
                case L.WallKind.Rubber:
                case L.WallKind.Sling:
                    rubbers.Add(SegmentBox(walls, s.Kind == L.WallKind.Sling ? "SlingRubber" : "Rubber", s.A, s.B, Mathf.Max(0.006f, s.Radius * 2f), 0.02f, mats.Rubber, 0f));
                    break;
                default:
                    SegmentBox(walls, "Rail", s.A, s.B, Mathf.Max(0.006f, s.Radius * 2f), RailHeight, mats.Chrome, 0f);
                    break;
            }
        }
        var slingRubbers = new List<Renderer>();
        foreach (Renderer r in rubbers)
            if (r.name == "SlingRubber") slingRubbers.Add(r);
        slingRubbers.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));   // left, right
        foreach (L.Circle c in L.Posts) Cylinder(walls, "Post", new Vector3(c.C.x, 0.015f, c.C.y), c.R, 0.03f, mats.Rubber, Quaternion.identity);

        // slingshot plastics
        Mesh slingL = Prism("PB_SlingLeft", L.LeftSling, 0.03f);
        Mesh slingR = Prism("PB_SlingRight", L.RightSling, 0.03f);
        MeshObject(pf, "SlingLeft", slingL, mats.Plastic, Vector3.zero);
        MeshObject(pf, "SlingRight", slingR, mats.Plastic, Vector3.zero);

        // bumpers
        var caps = new Renderer[L.Bumpers.Length];
        for (int i = 0; i < L.Bumpers.Length; i++)
        {
            L.Circle b = L.Bumpers[i];
            Cylinder(pf, "Bumper" + i, new Vector3(b.C.x, 0.0175f, b.C.y), b.R, 0.035f, mats.BumperBase, Quaternion.identity);
            caps[i] = Cylinder(pf, "BumperCap" + i, new Vector3(b.C.x, 0.039f, b.C.y), b.R * 0.85f, 0.008f, mats.Cap, Quaternion.identity);
        }

        // drop targets (the parent drops)
        var targets = new Transform[L.Targets.Length];
        for (int i = 0; i < L.Targets.Length; i++)
        {
            L.Segment t = L.Targets[i];
            Vector2 mid = (t.A + t.B) * 0.5f;
            targets[i] = new GameObject("Target" + i).transform;
            targets[i].SetParent(pf, false);
            targets[i].localPosition = new Vector3(mid.x, 0f, mid.y);
            Box(targets[i], "Face", new Vector3(0f, 0.0175f, 0f), new Vector3(0.008f, 0.035f, (t.B - t.A).magnitude), mats.Target);
        }

        // saucer
        Cylinder(pf, "Saucer", new Vector3(L.Saucer.C.x, 0.0012f, L.Saucer.C.y), L.Saucer.R + 0.004f, 0.002f, mats.Dark, Quaternion.identity);

        // flippers
        var flippers = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            L.FlipperDef f = L.Flippers[i];
            Mesh fm = Prism("PB_Flipper", FlipperOutline(f), FlipperHeight);
            flippers[i] = new GameObject(f.Left ? "FlipperLeft" : "FlipperRight").transform;
            flippers[i].SetParent(pf, false);
            flippers[i].localPosition = new Vector3(f.Pivot.x, 0.001f, f.Pivot.y);
            flippers[i].localRotation = Quaternion.Euler(0f, -f.RestAngle, 0f);
            MeshObject(flippers[i], "Mesh", fm, mats.Flipper, Vector3.zero);
            Cylinder(flippers[i], "Pivot", new Vector3(0f, FlipperHeight + 0.002f, 0f), 0.004f, 0.004f, mats.Chrome, Quaternion.identity);
        }

        // plunger (its tip is where the ball rests)
        var plunger = new GameObject("Plunger").transform;
        plunger.SetParent(pf, false);
        plunger.localPosition = new Vector3(L.BallStart.x, L.BallRadius, L.PlungerY);
        Cylinder(plunger, "Rod", new Vector3(0f, 0f, -0.035f), 0.006f, 0.07f, mats.Chrome, Quaternion.Euler(90f, 0f, 0f));
        Cylinder(plunger, "Tip", new Vector3(0f, 0f, -0.003f), 0.009f, 0.006f, mats.Rubber, Quaternion.Euler(90f, 0f, 0f));

        // light inserts
        var lights = new GameObject("Inserts").transform;
        lights.SetParent(pf, false);
        Renderer[] lane = Inserts(lights, mats.Insert, L.LaneLights, L.InsertSize);
        Renderer[] target = Inserts(lights, mats.Insert, L.TargetLights, L.InsertSize);
        Renderer[] mana = Inserts(lights, mats.Insert, L.ManaLights, 0.015f);
        Renderer[] mult = Inserts(lights, mats.Insert, L.MultiplierLights, 0.024f);
        Renderer[] dragon = Inserts(lights, mats.Insert, L.DragonLights, 0.018f);
        Renderer shoot = Inserts(lights, mats.Insert, new[] { L.ShootAgainLight }, 0.026f)[0];
        Renderer extra = Inserts(lights, mats.Insert, new[] { L.ExtraBallLight }, L.InsertSize)[0];
        Renderer spell = Inserts(lights, mats.Insert, new[] { L.EnchantedLight }, 0.03f)[0];

        // ball
        var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballGo.name = "Ball";
        Object.DestroyImmediate(ballGo.GetComponent<Collider>());
        ballGo.transform.SetParent(root.transform, false);
        ballGo.transform.localScale = Vector3.one * (L.BallRadius * 2f);
        ballGo.GetComponent<Renderer>().sharedMaterial = mats.Chrome;

        // legs (floor to the tub's bottom)
        foreach (float x in new[] { -0.27f, 0.27f })
            foreach (float tz in new[] { 0.05f, L.Length - 0.02f })
            {
                Vector3 top = root.transform.InverseTransformPoint(pf.TransformPoint(new Vector3(L.Width * 0.5f + x, -0.245f, tz)));
                Cylinder(root.transform, "Leg", new Vector3(top.x, top.y * 0.5f, top.z), 0.022f, top.y, mats.Chrome, Quaternion.identity);
            }

        // backbox: neck, box, backglass, the score display
        Box(root.transform, "Neck", new Vector3(0f, 1.0f, 0.60f), new Vector3(0.6f, 0.12f, 0.15f), mats.Cabinet);
        Box(root.transform, "Backbox", new Vector3(0f, 1.46f, 0.615f), new Vector3(0.64f, 0.82f, 0.16f), mats.Cabinet);
        Renderer glass = Quad(root.transform, "Backglass", new Vector3(0f, 1.585f, 0.533f), new Vector2(0.60f, 0.45f), mats.Backglass, Quaternion.identity);
        glass.shadowCastingMode = ShadowCastingMode.Off;
        TMP_Text score = DisplayText(root.transform, "DisplayScore", new Vector3(0f, 1.425f, 0.531f), new Vector2(0.40f, 0.045f));
        TMP_Text info = DisplayText(root.transform, "DisplayInfo", new Vector3(0f, 1.384f, 0.531f), new Vector2(0.40f, 0.026f));

        var glowLight = new GameObject("Glow").AddComponent<Light>();
        glowLight.transform.SetParent(root.transform, false);
        glowLight.transform.localPosition = new Vector3(0f, 1.35f, -0.05f);
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.7f, 0.35f, 1f);
        glowLight.intensity = 0.8f;
        glowLight.range = 1.4f;
        glowLight.shadows = LightShadows.None;

        var view = new GameObject("ViewPoint").transform;
        view.SetParent(root.transform, false);
        view.localPosition = new Vector3(0f, 1.58f, -0.95f);
        view.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.98f, 0f) - view.localPosition, Vector3.up);

        // wire
        var so = new SerializedObject(machine);
        so.FindProperty(PinballMachine.StationField).objectReferenceValue = station;
        so.FindProperty(PinballMachine.StoryField).objectReferenceValue = story;
        so.FindProperty(PinballMachine.ViewPointField).objectReferenceValue = view;
        so.FindProperty(PinballMachine.PlayfieldField).objectReferenceValue = pf;
        so.FindProperty(PinballMachine.BallField).objectReferenceValue = ballGo.transform;
        so.FindProperty(PinballMachine.PlungerField).objectReferenceValue = plunger;
        so.FindProperty(PinballMachine.ShootAgainField).objectReferenceValue = shoot;
        so.FindProperty(PinballMachine.ExtraBallField).objectReferenceValue = extra;
        so.FindProperty(PinballMachine.EnchantedField).objectReferenceValue = spell;
        so.FindProperty(PinballMachine.ScoreTextField).objectReferenceValue = score;
        so.FindProperty(PinballMachine.InfoTextField).objectReferenceValue = info;
        so.FindProperty(PinballMachine.AudioField).objectReferenceValue = audio;
        so.FindProperty(PinballMachine.GlowField).objectReferenceValue = glowLight;
        SetArray(so, PinballMachine.FlippersField, flippers);
        SetArray(so, PinballMachine.TargetsField, targets);
        SetArray(so, PinballMachine.BumperCapsField, caps);
        SetArray(so, PinballMachine.SlingRubbersField, slingRubbers.ToArray());
        SetArray(so, PinballMachine.LaneLightsField, lane);
        SetArray(so, PinballMachine.TargetLightsField, target);
        SetArray(so, PinballMachine.ManaLightsField, mana);
        SetArray(so, PinballMachine.MultiplierLightsField, mult);
        SetArray(so, PinballMachine.DragonLightsField, dragon);
        so.ApplyModifiedPropertiesWithoutUndo();
        return machine;
    }

    // ------------------------------------------------------------------ helpers
    private static Renderer[] Inserts(Transform parent, Material mat, Vector2[] points, float size)
    {
        var r = new Renderer[points.Length];
        for (int i = 0; i < points.Length; i++)
            r[i] = Quad(parent, "Insert", new Vector3(points[i].x, 0.0008f, points[i].y), new Vector2(size, size), mat);
        return r;
    }

    private static void SetArray<T>(SerializedObject so, string field, IList<T> values) where T : Object
    {
        SerializedProperty p = so.FindProperty(field);
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Renderer Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        return r;
    }

    // a box along a table space segment (thickness across, height up from the surface + lift)
    private static Renderer SegmentBox(Transform parent, string name, Vector2 a, Vector2 b, float thickness, float height, Material mat, float extend, float lift = 0f)
    {
        Vector2 d = b - a;
        float length = d.magnitude + extend * 2f;
        Vector2 mid = (a + b) * 0.5f;
        Renderer r = Box(parent, name, new Vector3(mid.x, lift + height * 0.5f, mid.y), new Vector3(length, height, thickness), mat);
        r.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
        return r;
    }

    private static Renderer Cylinder(Transform parent, string name, Vector3 pos, float radius, float height, Material mat, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        return r;
    }

    // a quad lying on the table (facing up), or standing with the given rotation (facing -z)
    private static Renderer Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat) =>
        Quad(parent, name, pos, size, mat, Quaternion.Euler(90f, 0f, 0f));

    private static Renderer Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return r;
    }

    private static Renderer MeshObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return r;
    }

    // the orange dot-matrix-like score display (3D text on the backglass)
    private static TMP_Text DisplayText(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = 2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = new Color(1f, 0.5f, 0.12f);
        t.fontStyle = FontStyles.Bold;
        t.text = "ARCANE QUEST";
        return t;
    }
}
