using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The casino's hall, wine and gold (menu 8, called by CasinoBuilder): floor (marble in the elevator hall, patterned
// carpet elsewhere), walls (damask wallpaper over a dark wood wainscot, gold rails), the coffered ceiling, columns
// between the west wing and the floor, the bar (back bar with bottles, counter, stools, the cashier's grille) and the
// chandeliers with their lights. Boxes with UVs in meters (FacilityBuilder.Block), textures in Art/Props/Casino/Hall.
// Plan (meters, x east, z north): ELEVATOR HALL z[0,3.9] (the elevators' facades at z 1.8) | WEST WING x[0,4.8]:
// PRIZE GALLERY z[3.9,10.4], BAR z[10.4,22] | FLOOR x[4.8,16]: FRONT SLOTS z[3.9,8], CENTER (card tables) z[8,14],
// ROULETTE z[14,17.4], BACK SLOTS z[17.4,22].
internal static class CasinoHallBuilder
{
    // Room size, ceiling height, the west wing's edge, the elevator hall's depth (m).
    internal const float Width = 16f, Length = 22f, Height = 4.2f, WingX = 4.8f, HallDepth = 3.9f;
    // The elevators: their facades and height.
    internal const float ElevatorFacadeZ = 1.8f, ElevatorHeight = 3.4f;
    // Wainscot height, wall thickness.
    private const float WainscotHeight = 1.1f, Wall = 0.2f;
    // The bar (west wing): back bar against the west wall, the counter, the cashier's part at its south end.
    internal const float BarZ0 = 13.2f, BarZ1 = 20.4f, CounterX0 = 1.4f, CounterX1 = 2.0f, CashierZ0 = 11.6f, CashierZ1 = 12.9f;
    // Where she stands at the cashier (the bar's spot), the stools' line.
    internal static readonly Vector2 CashierSpot = new Vector2(2.8f, 12.25f);
    private const float StoolX = 2.45f;
    // Chandeliers (x, z): over the card tables, the roulette, the bar, the elevator hall.
    private static readonly Vector2[] Chandeliers =
        { new Vector2(7.6f, 11f), new Vector2(13.2f, 11f), new Vector2(10.0f, 15.7f), new Vector2(2.4f, 16.8f), new Vector2(8f, 2.6f) };
    // Columns along the west wing's edge (z).
    private static readonly float[] ColumnZ = { HallDepth, 10.4f, 14f, 17.4f };

    // Art folders.
    private const string TexFolder = "Assets/Art/Props/Casino/Hall";
    private const string MatFolder = "Assets/Art/Props/Casino/Materials";
    private const string StoolPath = "Assets/Art/Props/Blackjack/SM_BarStool.fbx";

    // Every material of the hall.
    private struct Mats
    {
        public Material Carpet, Marble, Wallpaper, Wainscot, Ceiling, Gold, Wood, Dark, Mirror, Crystal, Felt;
        public Material[] Bottles;
    }

    // URP Lit property ids.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Builds the hall: solid parts under `geometry` (their colliders make the NavMesh), the rest under `decor`.
    internal static void Build(Transform geometry, Transform decor, Shader lit)
    {
        Mats m = MakeMaterials(lit);
        BuildShell(geometry, m);
        BuildColumns(geometry, m);
        BuildBar(geometry, m);
        BuildChandeliers(decor, m);
        // a dark medallion under the roulette, a gold line where the marble meets the carpet
        Flat(decor, "RouletteMedallion", new Vector3(10.0f, 0.004f, 15.66f), new Vector3(4.2f, 0.004f, 2.6f), m.Dark);
        Flat(decor, "HallEdge", new Vector3(Width * 0.5f, 0.003f, HallDepth), new Vector3(Width, 0.004f, 0.06f), m.Gold);
    }

    // ------------------------------------------------------------------ shell
    // Floors, ceiling, the four walls (wallpaper, wainscot, rails), the header above the elevators.
    private static void BuildShell(Transform g, Mats m)
    {
        FacilityBuilder.Block(g, "FloorMarble", new Vector3(Width * 0.5f, -0.05f, HallDepth * 0.5f), new Vector3(Width, 0.1f, HallDepth), m.Marble, 1.2f);
        float carpetLength = Length - HallDepth;
        FacilityBuilder.Block(g, "FloorCarpet", new Vector3(Width * 0.5f, -0.05f, HallDepth + carpetLength * 0.5f), new Vector3(Width, 0.1f, carpetLength), m.Carpet, 2.4f);
        FacilityBuilder.Block(g, "Ceiling", new Vector3(Width * 0.5f, Height + 0.05f, Length * 0.5f), new Vector3(Width, 0.1f, Length), m.Ceiling, 1.5f);

        // walls: inner face on the room's edge; the south wall's lower part is the elevators (their own block)
        WallRun(g, m, "West", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, Length), Vector3.right);
        WallRun(g, m, "East", new Vector3(Width, 0f, 0f), new Vector3(Width, 0f, Length), Vector3.left);
        WallRun(g, m, "North", new Vector3(0f, 0f, Length), new Vector3(Width, 0f, Length), Vector3.back);
        FacilityBuilder.Block(g, "WallSouth", new Vector3(Width * 0.5f, Height * 0.5f, -Wall * 0.5f), new Vector3(Width, Height, Wall), m.Wallpaper, 1.2f);
        // above the elevator facades, up to the ceiling, with a gold cornice
        float header = Height - ElevatorHeight;
        FacilityBuilder.Block(g, "ElevatorHeader", new Vector3(Width * 0.5f, ElevatorHeight + header * 0.5f, ElevatorFacadeZ - 0.1f), new Vector3(Width, header, 0.2f), m.Wallpaper, 1.2f);
        Trim(g, "ElevatorCornice", new Vector3(Width * 0.5f, ElevatorHeight + 0.06f, ElevatorFacadeZ + 0.02f), new Vector3(Width, 0.12f, 0.08f), m.Gold);
    }

    // One wall from a to b (its inner face on that line, `inward` = toward the room): wallpaper, wainscot, baseboard,
    // chair rail and crown.
    private static void WallRun(Transform g, Mats m, string wallName, Vector3 a, Vector3 b, Vector3 inward)
    {
        Vector3 along = (b - a).normalized;
        float len = (b - a).magnitude;
        Vector3 mid = (a + b) * 0.5f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        Vector3 Size(float thick, float height) => alongX ? new Vector3(len, height, thick) : new Vector3(thick, height, len);
        FacilityBuilder.Block(g, "Wall" + wallName, mid - inward * (Wall * 0.5f) + Vector3.up * (Height * 0.5f), Size(Wall, Height), m.Wallpaper, 1.2f);
        FacilityBuilder.Block(g, "Wainscot" + wallName, mid + inward * 0.02f + Vector3.up * (WainscotHeight * 0.5f), Size(0.04f, WainscotHeight), m.Wainscot, WainscotHeight);
        Trim(g, "ChairRail" + wallName, mid + inward * 0.045f + Vector3.up * WainscotHeight, Size(0.05f, 0.06f), m.Gold);
        Trim(g, "Baseboard" + wallName, mid + inward * 0.045f + Vector3.up * 0.06f, Size(0.05f, 0.12f), m.Dark);
        Trim(g, "Crown" + wallName, mid + inward * 0.06f + Vector3.up * (Height - 0.09f), Size(0.12f, 0.18f), m.Gold);
    }

    // ------------------------------------------------------------------ columns
    // Square columns on the west wing's edge: wine shaft, gold base and capital; matching pilasters on the east wall.
    private static void BuildColumns(Transform g, Mats m)
    {
        foreach (float z in ColumnZ) Column(g, m, new Vector3(WingX, 0f, z), 0.5f);
        for (float z = 4.4f; z < Length; z += 4.4f) Column(g, m, new Vector3(Width - 0.12f, 0f, z), 0.24f);
    }

    // One column (or pilaster against a wall).
    private static void Column(Transform g, Mats m, Vector3 at, float size)
    {
        var c = new GameObject("Column").transform;
        c.SetParent(g, false);
        FacilityBuilder.Block(c, "Shaft", at + Vector3.up * (Height * 0.5f), new Vector3(size, Height, size), m.Wallpaper, 1.2f);
        Trim(c, "Base", at + Vector3.up * 0.35f, new Vector3(size + 0.12f, 0.7f, size + 0.12f), m.Wood);
        Trim(c, "BaseRing", at + Vector3.up * 0.72f, new Vector3(size + 0.14f, 0.06f, size + 0.14f), m.Gold);
        Trim(c, "Capital", at + Vector3.up * (Height - 0.25f), new Vector3(size + 0.16f, 0.3f, size + 0.16f), m.Gold);
    }

    // ------------------------------------------------------------------ bar
    // The back bar on the west wall (cabinet, mirror, three lit shelves of bottles), the counter with its gold rail,
    // the cashier at its south end (counter + brass grille), the stools.
    private static void BuildBar(Transform g, Mats m)
    {
        var bar = new GameObject("Bar").transform;
        bar.SetParent(g, false);
        float zMid = (BarZ0 + BarZ1) * 0.5f, zLen = BarZ1 - BarZ0;
        FacilityBuilder.Block(bar, "BackCabinet", new Vector3(0.25f, 0.5f, zMid), new Vector3(0.5f, 1.0f, zLen), m.Wood, 1.1f);
        Trim(bar, "BackTop", new Vector3(0.27f, 1.02f, zMid), new Vector3(0.54f, 0.04f, zLen), m.Gold);
        Trim(bar, "Mirror", new Vector3(0.03f, 2.0f, zMid), new Vector3(0.02f, 1.9f, zLen - 0.4f), m.Mirror);
        var rng = new System.Random(11);
        for (int s = 0; s < 3; s++)
        {
            float y = 1.35f + s * 0.45f;
            Trim(bar, "Shelf", new Vector3(0.14f, y, zMid), new Vector3(0.26f, 0.03f, zLen - 0.4f), m.Gold);
            for (float z = BarZ0 + 0.35f; z < BarZ1 - 0.3f; z += 0.16f + (float)rng.NextDouble() * 0.08f)
            {
                float h = 0.22f + (float)rng.NextDouble() * 0.12f;
                Trim(bar, "Bottle", new Vector3(0.12f, y + 0.015f + h * 0.5f, z), new Vector3(0.07f, h, 0.07f), m.Bottles[rng.Next(m.Bottles.Length)]);
            }
        }
        // the counter: wood front, gold kick and rail, dark top
        float cMid = (CounterX0 + CounterX1) * 0.5f, cw = CounterX1 - CounterX0;
        FacilityBuilder.Block(bar, "Counter", new Vector3(cMid, 0.55f, zMid), new Vector3(cw, 1.1f, zLen), m.Wainscot, 1.1f);
        Trim(bar, "CounterTop", new Vector3(cMid + 0.04f, 1.12f, zMid), new Vector3(cw + 0.16f, 0.05f, zLen + 0.1f), m.Dark);
        Trim(bar, "CounterRail", new Vector3(CounterX1 + 0.12f, 1.0f, zMid), new Vector3(0.04f, 0.04f, zLen), m.Gold);
        Trim(bar, "CounterKick", new Vector3(CounterX1 + 0.01f, 0.06f, zMid), new Vector3(0.03f, 0.12f, zLen), m.Gold);
        // the cashier: its own counter and a brass grille with the window in the middle
        float kMid = (CashierZ0 + CashierZ1) * 0.5f, kLen = CashierZ1 - CashierZ0;
        FacilityBuilder.Block(bar, "CashierCounter", new Vector3(cMid, 0.55f, kMid), new Vector3(cw, 1.1f, kLen), m.Wainscot, 1.1f);
        Trim(bar, "CashierTop", new Vector3(cMid + 0.04f, 1.12f, kMid), new Vector3(cw + 0.16f, 0.05f, kLen + 0.1f), m.Dark);
        for (float z = CashierZ0 + 0.05f; z <= CashierZ1 - 0.04f; z += 0.09f)
        {
            if (Mathf.Abs(z - kMid) < 0.25f) continue;   // the window
            Trim(bar, "Grille", new Vector3(CounterX0 + 0.1f, 1.62f, z), new Vector3(0.02f, 0.95f, 0.02f), m.Gold);
        }
        Trim(bar, "GrilleTop", new Vector3(CounterX0 + 0.1f, 2.12f, kMid), new Vector3(0.05f, 0.06f, kLen), m.Gold);
        Trim(bar, "CashierSign", new Vector3(CounterX0 + 0.1f, 2.32f, kMid), new Vector3(0.04f, 0.3f, kLen * 0.8f), m.Felt);

        var stool = AssetDatabase.LoadAssetAtPath<GameObject>(StoolPath);
        if (stool == null) { Debug.LogWarning($"[Casino] {StoolPath} missing (run menu 3): no bar stools."); return; }
        for (float z = BarZ0 + 0.6f; z < BarZ1 - 0.3f; z += 1.0f)
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(stool, bar);   // solid: she walks around them
            s.name = "BarStool";
            s.transform.SetPositionAndRotation(new Vector3(StoolX, 0f, z), Quaternion.Euler(0f, 90f, 0f));
            foreach (MeshFilter mf in s.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }

    // ------------------------------------------------------------------ chandeliers
    // A gold ring with hanging crystals and a center drop, on a rod from the ceiling, and its warm light.
    private static void BuildChandeliers(Transform decor, Mats m)
    {
        var parent = new GameObject("Chandeliers").transform;
        parent.SetParent(decor, false);
        foreach (Vector2 p in Chandeliers)
        {
            var c = new GameObject($"Chandelier_{p.x:0.0}_{p.y:0.0}").transform;
            c.SetParent(parent, false);
            c.position = new Vector3(p.x, Height, p.y);
            Trim(c, "Rod", c.position + Vector3.down * 0.35f, new Vector3(0.04f, 0.7f, 0.04f), m.Gold);
            Trim(c, "Hub", c.position + Vector3.down * 0.75f, new Vector3(0.22f, 0.12f, 0.22f), m.Gold);
            const int Arms = 12;
            for (int i = 0; i < Arms; i++)
            {
                float a = i * Mathf.PI * 2f / Arms;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tip = c.position + Vector3.down * 0.8f + dir * 0.6f;
                Transform arm = Trim(c, "Ring", tip, new Vector3(0.04f, 0.04f, 0.33f), m.Gold).transform;
                arm.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
                Trim(c, "Crystal", tip + Vector3.down * 0.16f, new Vector3(0.05f, 0.24f, 0.05f), m.Crystal).transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 45f, 0f);
            }
            Trim(c, "Drop", c.position + Vector3.down * 1.05f, new Vector3(0.12f, 0.4f, 0.12f), m.Crystal);

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(c, false);
            light.transform.position = c.position + Vector3.down * 1.3f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.52f);
            light.intensity = 4.2f;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }
    }

    // ------------------------------------------------------------------ helpers
    // A trim / decor box: tiled UVs (0.5 m), no collider, no shadow.
    private static GameObject Trim(Transform parent, string objectName, Vector3 center, Vector3 size, Material m)
    {
        GameObject go = FacilityBuilder.Block(parent, objectName, center, size, m, 0.5f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // A flat decal box on the floor (no collider).
    private static void Flat(Transform parent, string objectName, Vector3 center, Vector3 size, Material m) => Trim(parent, objectName, center, size, m);

    // The hall's materials (textures generated by tools/casino2 textures.py; plain colors for gold, wood, glass).
    private static Mats MakeMaterials(Shader lit)
    {
        EditorAssets.EnsureFolder("Assets/Art/Props/Casino", "Materials");
        Material Tex(string name, string tex, float smooth)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{tex}.png");
            if (t == null) Debug.LogWarning($"[Casino] {TexFolder}/{tex}.png missing: copy the casino art.");
            return EditorAssets.MakeMaterial($"{MatFolder}/{name}.mat", t, smooth, false, lit);
        }
        Material Plain(string name, Color color, float smooth, float metal = 0f, Color? glow = null)
        {
            Material mat = EditorAssets.MakeMaterial($"{MatFolder}/{name}.mat", null, smooth, false, lit);
            mat.SetColor(BaseColorId, color);
            mat.SetFloat("_Metallic", metal);
            if (glow.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, glow.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
        return new Mats
        {
            Carpet = Tex("M_HallCarpet", "T_CasinoCarpet", 0.05f),
            Marble = Tex("M_HallMarble", "T_CasinoMarble", 0.7f),
            Wallpaper = Tex("M_HallWallpaper", "T_CasinoWallpaper", 0.15f),
            Wainscot = Tex("M_HallWainscot", "T_CasinoWainscot", 0.45f),
            Ceiling = Tex("M_HallCeiling", "T_CasinoCeiling", 0.2f),
            Gold = Plain("M_HallGold", new Color(0.78f, 0.58f, 0.24f), 0.7f, 0.85f),
            Wood = Plain("M_HallWood", new Color(0.2f, 0.09f, 0.05f), 0.45f),
            Dark = Plain("M_HallDark", new Color(0.07f, 0.03f, 0.03f), 0.35f),
            Mirror = Plain("M_HallMirror", new Color(0.12f, 0.1f, 0.1f), 0.95f, 0.6f),
            Crystal = Plain("M_HallCrystal", new Color(1f, 0.93f, 0.8f), 0.9f, 0f, new Color(1.6f, 1.25f, 0.8f)),
            Felt = Plain("M_HallSign", new Color(0.35f, 0.05f, 0.08f), 0.2f, 0f, new Color(0.5f, 0.32f, 0.1f)),
            Bottles = new[]
            {
                Plain("M_HallBottleGreen", new Color(0.1f, 0.35f, 0.15f), 0.9f, 0f, new Color(0.05f, 0.18f, 0.06f)),
                Plain("M_HallBottleAmber", new Color(0.55f, 0.3f, 0.08f), 0.9f, 0f, new Color(0.3f, 0.15f, 0.03f)),
                Plain("M_HallBottleClear", new Color(0.75f, 0.75f, 0.7f), 0.9f, 0f, new Color(0.25f, 0.24f, 0.2f)),
                Plain("M_HallBottleRed", new Color(0.45f, 0.06f, 0.08f), 0.9f, 0f, new Color(0.22f, 0.02f, 0.03f)),
            },
        };
    }
}
