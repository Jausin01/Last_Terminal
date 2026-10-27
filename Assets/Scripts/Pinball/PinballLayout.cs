using UnityEngine;

// The ARCANE QUEST table, in table space (meters): x = 0 at the left wall to the right, y = 0 at the bottom (the
// drain) up the table. The simulation (PinballTable) and the builder (the visuals and the playfield art) both read
// it, so what you see is what the ball hits.
public static class PinballLayout
{
    public const float Width = 0.52f;          // inside the cabinet
    // Playfield length (m).
    public const float Length = 1.07f;
    public const float LaneWall = 0.475f;      // the shooter lane is right of this
    public const float BallRadius = 0.0135f;   // a 27 mm ball
    // Playfield slope (deg).
    public const float TiltDegrees = 6.5f;
    public const float Gravity = 1.8f;         // along the table (a bit more than the real slope: snappier)
    public const float ArcCenterY = 0.81f;     // the top of the table is a half circle
    // Middle of the playfield (left of the shooter lane).
    public const float CenterX = LaneWall * 0.5f;

    // What a wall segment is (bounce and sound differ).
    public enum WallKind { Wall, Rail, Rubber, Sling, Gate }

    // A wall: a capsule from A to B with a radius.
    public struct Segment
    {
        // Ends, thickness and kind.
        public readonly Vector2 A, B;
        public readonly float Radius;
        public readonly WallKind Kind;
        public Segment(Vector2 a, Vector2 b, float radius, WallKind kind) { A = a; B = b; Radius = radius; Kind = kind; }
    }

    // A round post or bumper.
    public struct Circle
    {
        // Center and radius.
        public readonly Vector2 C;
        public readonly float R;
        public Circle(float x, float y, float r) { C = new Vector2(x, y); R = r; }
    }

    // angles in degrees, counterclockwise from +x
    public struct FlipperDef
    {
        // Pivot, length, base / tip radius, rest and raised angles, left or right flipper.
        public readonly Vector2 Pivot;
        public readonly float Length, BaseRadius, TipRadius, RestAngle, UpAngle;
        public readonly bool Left;
        public FlipperDef(Vector2 pivot, float length, float baseRadius, float tipRadius, float rest, float up, bool left)
        { Pivot = pivot; Length = length; BaseRadius = baseRadius; TipRadius = tipRadius; RestAngle = rest; UpAngle = up; Left = left; }
    }

    // The table geometry: walls, posts, the three pop bumpers and the two flippers.
    public static readonly Segment[] Walls;
    public static readonly Circle[] Posts;
    public static readonly Circle[] Bumpers = { new Circle(0.15f, 0.70f, 0.026f), new Circle(0.30f, 0.70f, 0.026f), new Circle(0.225f, 0.60f, 0.026f) };
    public static readonly FlipperDef[] Flippers;
    // drop targets on the left wall (they face +x)
    public static readonly Segment[] Targets =
    {
        new Segment(new Vector2(0.026f, 0.455f), new Vector2(0.026f, 0.487f), 0.004f, WallKind.Wall),
        new Segment(new Vector2(0.026f, 0.494f), new Vector2(0.026f, 0.526f), 0.004f, WallKind.Wall),
        new Segment(new Vector2(0.026f, 0.533f), new Vector2(0.026f, 0.565f), 0.004f, WallKind.Wall),
    };
    // the M-A-G-E rollover lanes at the top
    public static readonly float[] LaneGuideX = { 0.095f, 0.165f, 0.235f, 0.305f, 0.375f };
    public const float LaneGuideBottom = 0.86f, LaneGuideTop = 0.93f;
    public static readonly Circle[] Lanes = { new Circle(0.13f, 0.895f, 0.012f), new Circle(0.20f, 0.895f, 0.012f), new Circle(0.27f, 0.895f, 0.012f), new Circle(0.34f, 0.895f, 0.012f) };
    public static readonly string[] LaneLetters = { "M", "A", "G", "E" };
    // left outlane, left inlane, right inlane, right outlane (sensors)
    public static readonly Circle[] SideLanes = { new Circle(0.0225f, 0.24f, 0.012f), new Circle(0.065f, 0.25f, 0.012f), new Circle(0.41f, 0.25f, 0.012f), new Circle(0.4525f, 0.24f, 0.012f) };
    // the Dragon's Lair saucer
    public static readonly Circle Saucer = new Circle(0.40f, 0.52f, 0.016f);
    public static readonly Vector2 SaucerKick = new Vector2(-0.55f, -0.84f);

    // plunger: the ball rests on it in the shooter lane
    public const float PlungerY = 0.03f;
    public const float PlungerTravel = 0.045f;
    public static readonly Vector2 BallStart = new Vector2((LaneWall + Width) * 0.5f, PlungerY + BallRadius + 0.001f);
    public const float GateY = 0.79f;

    // slingshots (A top, B bottom outer, C bottom inner; A-C kicks)
    public static readonly Vector2[] LeftSling = { new Vector2(0.085f, 0.31f), new Vector2(0.085f, 0.215f), new Vector2(0.13f, 0.185f) };
    public static readonly Vector2[] RightSling = { Mirror(LeftSling[0]), Mirror(LeftSling[1]), Mirror(LeftSling[2]) };

    // light inserts (playfield art + glowing quads)
    public static readonly Vector2[] LaneLights = { new Vector2(0.13f, 0.84f), new Vector2(0.20f, 0.84f), new Vector2(0.27f, 0.84f), new Vector2(0.34f, 0.84f) };
    public static readonly Vector2[] TargetLights = { new Vector2(0.06f, 0.471f), new Vector2(0.06f, 0.510f), new Vector2(0.06f, 0.549f) };
    public static readonly Vector2[] ManaLights = BuildArc(new Vector2(CenterX, 0.395f), 0.078f, 165f, 15f, 10);
    public static readonly Vector2[] MultiplierLights = { new Vector2(0.1895f, 0.30f), new Vector2(0.2215f, 0.30f), new Vector2(0.2535f, 0.30f), new Vector2(0.2855f, 0.30f) };
    public static readonly Vector2 ShootAgainLight = new Vector2(CenterX, 0.175f);
    public static readonly Vector2 ExtraBallLight = new Vector2(0.40f, 0.462f);
    public static readonly Vector2[] DragonLights = { new Vector2(0.365f, 0.575f), new Vector2(0.40f, 0.585f), new Vector2(0.435f, 0.575f) };
    public static readonly Vector2 EnchantedLight = new Vector2(CenterX, 0.525f);
    public const float InsertSize = 0.022f;
    public const float EnchantedSize = 0.07f;

    static PinballLayout()
    {
        var walls = new System.Collections.Generic.List<Segment>();
        var posts = new System.Collections.Generic.List<Circle>();
        // outer walls + the top arc
        walls.Add(new Segment(new Vector2(0f, -0.05f), new Vector2(0f, ArcCenterY), 0f, WallKind.Wall));
        // Segments of the top half circle.
        const int arcSteps = 28;
        float r = Width * 0.5f;
        Vector2 prev = new Vector2(0f, ArcCenterY);
        for (int i = 1; i <= arcSteps; i++)
        {
            float a = Mathf.PI * (1f - i / (float)arcSteps);
            var p = new Vector2(r + r * Mathf.Cos(a), ArcCenterY + r * Mathf.Sin(a));
            walls.Add(new Segment(prev, p, 0f, WallKind.Wall));
            prev = p;
        }
        walls.Add(new Segment(new Vector2(Width, ArcCenterY), new Vector2(Width, -0.05f), 0f, WallKind.Wall));
        // shooter lane: inner wall, the one-way gate at its top, the plunger is dynamic (PinballTable)
        walls.Add(new Segment(new Vector2(LaneWall, -0.05f), new Vector2(LaneWall, 0.78f), 0.003f, WallKind.Rail));
        posts.Add(new Circle(LaneWall, 0.78f, 0.004f));
        walls.Add(new Segment(new Vector2(LaneWall + 0.002f, GateY), new Vector2(Width - 0.002f, GateY), 0f, WallKind.Gate));

        // outlane / inlane separators and the inlane guides down to the flippers (both sides)
        foreach (bool left in new[] { true, false })
        {
            walls.Add(new Segment(Side(left, 0.045f, 0.30f), Side(left, 0.045f, -0.05f), 0.003f, WallKind.Rail));
            walls.Add(new Segment(Side(left, 0.045f, 0.19f), Side(left, 0.14f, 0.132f), 0.003f, WallKind.Rail));
            posts.Add(new Circle(Side(left, 0.045f, 0.30f).x, 0.30f, 0.006f));
            Vector2[] s = left ? LeftSling : RightSling;
            walls.Add(new Segment(s[0], s[1], 0.004f, WallKind.Rubber));
            walls.Add(new Segment(s[1], s[2], 0.004f, WallKind.Rubber));
            walls.Add(new Segment(s[0], s[2], 0.004f, WallKind.Sling));
        }
        // top lane guides
        foreach (float x in LaneGuideX)
        {
            walls.Add(new Segment(new Vector2(x, LaneGuideBottom), new Vector2(x, LaneGuideTop), 0.004f, WallKind.Rail));
            posts.Add(new Circle(x, LaneGuideTop, 0.005f));
            posts.Add(new Circle(x, LaneGuideBottom, 0.005f));
        }
        Walls = walls.ToArray();
        Posts = posts.ToArray();

        Flippers = new[]
        {
            new FlipperDef(new Vector2(0.1525f, 0.12f), 0.072f, 0.011f, 0.0055f, -32f, 28f, true),
            new FlipperDef(new Vector2(LaneWall - 0.1525f, 0.12f), 0.072f, 0.011f, 0.0055f, 212f, 152f, false),
        };
    }

    // A point on the left side, or its mirror on the right.
    private static Vector2 Side(bool left, float x, float y) => left ? new Vector2(x, y) : Mirror(new Vector2(x, y));

    // Mirrors a point across the middle of the playfield.
    public static Vector2 Mirror(Vector2 p) => new Vector2(LaneWall - p.x, p.y);

    // Points along an arc (deg, counterclockwise from +x).
    private static Vector2[] BuildArc(Vector2 center, float radius, float fromDeg, float toDeg, int count)
    {
        var pts = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, count > 1 ? i / (float)(count - 1) : 0f) * Mathf.Deg2Rad;
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return pts;
    }
}
