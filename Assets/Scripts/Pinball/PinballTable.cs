using System;
using UnityEngine;
using L = PinballLayout;

// ARCANE QUEST: the game and its physics, in table space (PinballLayout). A ball (circle) under the table's slope
// against walls and posts (segments / circles), pop bumpers and slingshots that kick, two flippers (tapered capsules
// that swing and hit), drop targets, rollover lanes, a saucer that holds the ball, a plunger. Small fixed steps
// (~1000 / s) keep the ball from going through anything.
// Rules: M-A-G-E lanes (the flippers move the lit ones; all four = bonus multiplier up to 5x), three crystal drop
// targets, the Dragon's Lair saucer (3rd dragon = extra ball), mana from everything - full = ENCHANTED (score x2
// for 20 s). Ball save for the first seconds of each ball. Three balls. No visuals here (PinballMachine shows it).
public class PinballTable
{
    public enum Sound { Flipper, Bumper, Sling, Lane, Target, Saucer, Kickout, Launch, Drain, Enchanted, BallSaved, ExtraBall }

    public const int BallsPerGame = 3;
    public const int ManaMax = 100;
    private const float Step = 1f / 960f;
    private const float MaxSpeed = 4.6f;
    private const float FlipperSpeed = 30f;   // rad/s
    private const float BallSaveTime = 8f;
    private const float EnchantedTime = 20f;

    // ------------------------------------------------------------------ state (read by PinballMachine)
    public Vector2 BallPos { get; private set; }
    public Vector2 BallVel { get; private set; }
    public bool BallVisible { get; private set; }
    public readonly float[] FlipperAngles = new float[2];   // degrees
    public readonly bool[] TargetDown = new bool[3];
    public readonly bool[] LaneLit = new bool[4];
    public readonly float[] BumperFlash = new float[3];
    public readonly float[] SlingFlash = new float[2];
    public float Plunger { get; private set; }      // 0..1 pulled
    public long Score { get; private set; }
    public int BallNumber { get; private set; }
    public bool GameOver { get; private set; } = true;
    public int Mana { get; private set; }
    public float EnchantedLeft { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public int Dragons { get; private set; }
    public bool ExtraBallLit { get; private set; }
    public int ExtraBalls { get; private set; }
    public float BallSaveLeft { get; private set; }
    public bool BallInLane => BallVisible && BallPos.x > L.LaneWall && BallPos.y < 0.2f;
    public bool Captured => captureTimer > 0f;

    public event Action<Sound> Played;
    public event Action<string, float> Message;    // text, seconds
    public event Action<long> GameEnded;           // final score

    private readonly bool[] flipperHeld = new bool[2];
    private readonly float[] flipperOmega = new float[2];
    private readonly bool[] inSensor = new bool[8];
    private Vector2 pos, vel;
    private float accumulator;
    private float captureTimer, saucerCooldown;
    private float stuckTimer;
    private float drainTimer;          // the pause after a drain before the next ball
    private bool ballLaunched;         // left the shooter lane
    private bool launchHeld;
    private long bonus;
    private int extraBallsAwarded;
    private readonly System.Random rng = new System.Random();

    public PinballTable()
    {
        for (int i = 0; i < 2; i++) FlipperAngles[i] = L.Flippers[i].RestAngle;
    }

    // ------------------------------------------------------------------ input
    public void SetFlipper(int index, bool held)
    {
        if (flipperHeld[index] == held) return;
        flipperHeld[index] = held;
        if (held && !GameOver)
        {
            Played?.Invoke(Sound.Flipper);
            RotateLanes(index == 0 ? -1 : 1);
        }
    }

    // hold to pull the plunger, release to launch; with the game over it starts a game
    public void SetLaunch(bool held)
    {
        if (held && !launchHeld && GameOver) { StartGame(); launchHeld = true; return; }
        if (!held && launchHeld && BallVisible && BallInLane && Plunger > 0.02f)
        {
            if (pos.y - L.BallRadius <= L.PlungerY + 0.01f)
            {
                vel = new Vector2(0f, Mathf.Lerp(1.3f, 3.5f, Plunger));
                Played?.Invoke(Sound.Launch);
            }
            Plunger = 0f;
        }
        launchHeld = held;
    }

    public void StartGame()
    {
        Score = 0;
        BallNumber = 1;
        GameOver = false;
        Mana = 0;
        EnchantedLeft = 0f;
        Dragons = 0;
        ExtraBallLit = false;
        ExtraBalls = 0;
        extraBallsAwarded = 0;
        Multiplier = 1;
        for (int i = 0; i < 4; i++) LaneLit[i] = false;
        for (int i = 0; i < 3; i++) TargetDown[i] = false;
        Message?.Invoke("BALL 1", 2f);
        ServeBall();
    }

    private void ServeBall()
    {
        pos = L.BallStart;
        vel = Vector2.zero;
        BallVisible = true;
        ballLaunched = false;
        captureTimer = 0f;
        stuckTimer = 0f;
        bonus = 0;
        Plunger = 0f;
        Sync();
    }

    // ------------------------------------------------------------------ simulation
    public void Tick(float dt)
    {
        dt = Mathf.Min(dt, 0.05f);
        for (int i = 0; i < 3; i++) BumperFlash[i] = Mathf.Max(0f, BumperFlash[i] - dt);
        for (int i = 0; i < 2; i++) SlingFlash[i] = Mathf.Max(0f, SlingFlash[i] - dt);
        if (EnchantedLeft > 0f)
        {
            EnchantedLeft -= dt;
            if (EnchantedLeft <= 0f) { EnchantedLeft = 0f; Message?.Invoke("THE SPELL FADES", 1.5f); }
        }
        if (BallSaveLeft > 0f) BallSaveLeft = Mathf.Max(0f, BallSaveLeft - dt);
        if (launchHeld && BallInLane) Plunger = Mathf.MoveTowards(Plunger, 1f, dt * 1.1f);

        if (drainTimer > 0f)
        {
            drainTimer -= dt;
            if (drainTimer <= 0f) AfterDrain();
        }

        accumulator += dt;
        while (accumulator >= Step)
        {
            accumulator -= Step;
            StepFlippers(Step);
            if (BallVisible) StepBall(Step);
        }
        Sync();
    }

    private void Sync()
    {
        BallPos = pos;
        BallVel = vel;
    }

    private void StepFlippers(float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            L.FlipperDef f = L.Flippers[i];
            float target = flipperHeld[i] && !GameOver ? f.UpAngle : f.RestAngle;
            float before = FlipperAngles[i];
            FlipperAngles[i] = Mathf.MoveTowards(before, target, FlipperSpeed * Mathf.Rad2Deg * dt);
            flipperOmega[i] = (FlipperAngles[i] - before) * Mathf.Deg2Rad / dt;
        }
    }

    private void StepBall(float dt)
    {
        if (captureTimer > 0f)
        {
            captureTimer -= dt;
            if (captureTimer <= 0f) KickOut();
            return;
        }
        if (saucerCooldown > 0f) saucerCooldown -= dt;

        vel += new Vector2(0f, -L.Gravity) * dt;
        vel *= 1f - 0.12f * dt;
        if (vel.sqrMagnitude > MaxSpeed * MaxSpeed) vel = vel.normalized * MaxSpeed;
        pos += vel * dt;

        foreach (L.Segment s in L.Walls) CollideSegment(s);
        foreach (L.Circle c in L.Posts) CollideCircle(c.C, c.R, 0.5f);
        for (int i = 0; i < L.Bumpers.Length; i++)
            if (CollideCircle(L.Bumpers[i].C, L.Bumpers[i].R, 0.3f)) BumperHit(i);
        for (int i = 0; i < L.Targets.Length; i++)
            if (!TargetDown[i] && CollideSegmentRaw(L.Targets[i].A, L.Targets[i].B, L.Targets[i].Radius, 0.35f, out float vn) && vn < -0.15f) TargetHit(i);
        for (int i = 0; i < 2; i++) CollideFlipper(i);
        CollidePlunger();

        Sensors();
        Saucer();
        CheckStuck(dt);

        if (!ballLaunched && pos.x < L.LaneWall - 0.01f) { ballLaunched = true; BallSaveLeft = BallSaveTime; }
        if (pos.y < -0.03f) Drain();
    }

    // ------------------------------------------------------------------ collisions
    private void CollideSegment(L.Segment s)
    {
        switch (s.Kind)
        {
            case L.WallKind.Gate:
                // one way: only stops a ball coming down onto it from above
                if (vel.y < 0f && pos.y > L.GateY && pos.x > L.LaneWall) CollideSegmentRaw(s.A, s.B, s.Radius, 0.2f, out _);
                return;
            case L.WallKind.Sling:
                if (CollideSegmentRaw(s.A, s.B, s.Radius, 0.4f, out float vn, out Vector2 n) && vn < -0.25f)
                {
                    float outward = Vector2.Dot(vel, n);
                    if (outward < 0.95f) vel += n * (0.95f - outward);   // kicked, not endlessly: gravity wins
                    int side = s.A.x < L.CenterX ? 0 : 1;
                    SlingFlash[side] = 0.12f;
                    AddScore(10);
                    AddMana(1);
                    Played?.Invoke(Sound.Sling);
                }
                return;
            case L.WallKind.Rubber:
                CollideSegmentRaw(s.A, s.B, s.Radius, 0.55f, out _);
                return;
            default:
                CollideSegmentRaw(s.A, s.B, s.Radius, 0.4f, out _);
                return;
        }
    }

    private bool CollideSegmentRaw(Vector2 a, Vector2 b, float radius, float bounce, out float vn) =>
        CollideSegmentRaw(a, b, radius, bounce, out vn, out _);

    // pushes the ball out of a capsule (segment + radius), bounces it; vn = approach speed (negative = into it)
    private bool CollideSegmentRaw(Vector2 a, Vector2 b, float radius, float bounce, out float vn, out Vector2 n)
    {
        vn = 0f;
        n = Vector2.zero;
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(pos - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
        Vector2 q = a + ab * t;
        return Resolve(q, radius, Vector2.zero, bounce, out vn, out n);
    }

    private bool CollideCircle(Vector2 c, float r, float bounce)
    {
        return Resolve(c, r, Vector2.zero, bounce, out _, out _);
    }

    // contact with the closest point q of a shape of radius r moving at surfaceVel
    private bool Resolve(Vector2 q, float r, Vector2 surfaceVel, float bounce, out float vn, out Vector2 n)
    {
        vn = 0f;
        n = Vector2.zero;
        Vector2 d = pos - q;
        float min = L.BallRadius + r;
        float dist2 = d.sqrMagnitude;
        if (dist2 >= min * min) return false;
        float dist = Mathf.Sqrt(dist2);
        n = dist > 1e-6f ? d / dist : Vector2.up;
        pos = q + n * min;
        Vector2 rel = vel - surfaceVel;
        vn = Vector2.Dot(rel, n);
        if (vn < 0f)
        {
            rel -= (1f + bounce) * vn * n;
            // a little friction along the surface
            Vector2 tangent = new Vector2(-n.y, n.x);
            rel -= tangent * Vector2.Dot(rel, tangent) * 0.02f;
            vel = rel + surfaceVel;
        }
        return true;
    }

    private void CollideFlipper(int i)
    {
        L.FlipperDef f = L.Flippers[i];
        float a = FlipperAngles[i] * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 tip = f.Pivot + dir * f.Length;
        float t = Mathf.Clamp01(Vector2.Dot(pos - f.Pivot, dir) / f.Length);
        Vector2 q = f.Pivot + dir * f.Length * t;
        float r = Mathf.Lerp(f.BaseRadius, f.TipRadius, t);
        // the flipper's surface speed where the ball touches it (omega x arm)
        Vector2 arm = q - f.Pivot;
        Vector2 surface = flipperOmega[i] * new Vector2(-arm.y, arm.x);
        Resolve(q, r, surface, 0.3f, out _, out _);
    }

    private void CollidePlunger()
    {
        if (pos.x < L.LaneWall) return;
        float top = L.PlungerY - Plunger * L.PlungerTravel;
        if (pos.y - L.BallRadius < top)
        {
            pos.y = top + L.BallRadius;
            if (vel.y < 0f) vel.y = -vel.y * 0.15f;
            if (Mathf.Abs(vel.y) < 0.05f) vel.y = 0f;
            vel.x = 0f;
            pos.x = L.BallStart.x;
        }
    }

    // ------------------------------------------------------------------ playfield features
    private void Sensors()
    {
        for (int i = 0; i < L.Lanes.Length; i++)
            if (Enter(i, L.Lanes[i])) LaneHit(i);
        for (int i = 0; i < L.SideLanes.Length; i++)
            if (Enter(4 + i, L.SideLanes[i]))
            {
                bool outlane = i == 0 || i == 3;
                AddScore(outlane ? 2000 : 500);
                Played?.Invoke(Sound.Lane);
            }
    }

    private bool Enter(int index, L.Circle c)
    {
        bool inside = (pos - c.C).sqrMagnitude < c.R * c.R;
        bool entered = inside && !inSensor[index];
        inSensor[index] = inside;
        return entered;
    }

    private void Saucer()
    {
        if (saucerCooldown > 0f) return;
        if ((pos - L.Saucer.C).sqrMagnitude > L.Saucer.R * L.Saucer.R || vel.magnitude > 1.6f) return;
        pos = L.Saucer.C;
        vel = Vector2.zero;
        captureTimer = 1.3f;
        Dragons++;
        long award = 2500L * Dragons;
        AddScore(award);
        AddMana(20);
        bonus += 1000;
        Played?.Invoke(Sound.Saucer);
        if (Dragons % 3 == 0 && extraBallsAwarded < 2)
        {
            extraBallsAwarded++;
            ExtraBalls++;
            ExtraBallLit = true;
            Message?.Invoke("EXTRA BALL!", 2.5f);
            Played?.Invoke(Sound.ExtraBall);
        }
        else Message?.Invoke($"DRAGON'S LAIR  {Format(award * (EnchantedLeft > 0f ? 2 : 1))}", 1.6f);
    }

    private void KickOut()
    {
        Vector2 dir = (L.SaucerKick + new Vector2((float)(rng.NextDouble() - 0.5) * 0.25f, 0f)).normalized;
        vel = dir * 1.25f;
        pos += dir * (L.Saucer.R + 0.004f);
        saucerCooldown = 0.6f;
        Played?.Invoke(Sound.Kickout);
    }

    private void BumperHit(int i)
    {
        Vector2 n = (pos - L.Bumpers[i].C).normalized;
        float outSpeed = Vector2.Dot(vel, n);
        if (outSpeed < 1.4f) vel += n * (1.4f - outSpeed);
        BumperFlash[i] = 0.15f;
        AddScore(100);
        AddMana(3);
        bonus += 25;
        Played?.Invoke(Sound.Bumper);
    }

    private void TargetHit(int i)
    {
        TargetDown[i] = true;
        AddScore(750);
        AddMana(8);
        bonus += 300;
        Played?.Invoke(Sound.Target);
        if (TargetDown[0] && TargetDown[1] && TargetDown[2])
        {
            AddScore(5000);
            AddMana(15);
            Message?.Invoke("CRYSTALS!  5,000", 1.8f);
            for (int k = 0; k < 3; k++) TargetDown[k] = false;   // they rise again
        }
    }

    private void LaneHit(int i)
    {
        AddScore(LaneLit[i] ? 100 : 500);
        Played?.Invoke(Sound.Lane);
        if (LaneLit[i]) return;
        LaneLit[i] = true;
        AddMana(5);
        bonus += 250;
        if (LaneLit[0] && LaneLit[1] && LaneLit[2] && LaneLit[3])
        {
            for (int k = 0; k < 4; k++) LaneLit[k] = false;
            AddScore(5000);
            if (Multiplier < 5) Multiplier++;
            Message?.Invoke($"M-A-G-E!  BONUS {Multiplier}X", 2f);
        }
    }

    // the flippers move the lit lanes (lane change)
    private void RotateLanes(int dir)
    {
        if (!BallVisible || BallInLane) return;
        bool[] old = (bool[])LaneLit.Clone();
        for (int i = 0; i < 4; i++) LaneLit[(i + dir + 4) % 4] = old[i];
    }

    private void AddMana(int amount)
    {
        if (EnchantedLeft > 0f) return;
        Mana = Mathf.Min(ManaMax, Mana + amount);
        if (Mana < ManaMax) return;
        Mana = 0;
        EnchantedLeft = EnchantedTime;
        Message?.Invoke("ENCHANTED!  2X SCORE", 2.5f);
        Played?.Invoke(Sound.Enchanted);
    }

    private void AddScore(long points) => Score += points * (EnchantedLeft > 0f ? 2 : 1);

    private void CheckStuck(float dt)
    {
        if (vel.sqrMagnitude > 0.03f * 0.03f || pos.y < 0.22f || pos.x > L.LaneWall) { stuckTimer = 0f; return; }
        stuckTimer += dt;
        if (stuckTimer < 3f) return;
        stuckTimer = 0f;
        vel = new Vector2((float)(rng.NextDouble() - 0.5), -0.6f);   // a nudge
    }

    // ------------------------------------------------------------------ drain / end of ball
    private void Drain()
    {
        BallVisible = false;
        if (BallSaveLeft > 0f && ballLaunched)
        {
            BallSaveLeft = 0f;
            Message?.Invoke("BALL SAVED", 1.5f);
            Played?.Invoke(Sound.BallSaved);
            drainTimer = 1.2f;
            pendingBonus = false;
            return;
        }
        Played?.Invoke(Sound.Drain);
        long total = bonus * Multiplier;
        Score += total;
        Message?.Invoke($"BONUS {Format(bonus)} X{Multiplier} = {Format(total)}", 2.2f);
        drainTimer = 2.4f;
        pendingBonus = true;
    }

    private bool pendingBonus;

    private void AfterDrain()
    {
        if (!pendingBonus) { ServeBall(); return; }   // saved
        Multiplier = 1;
        if (ExtraBalls > 0)
        {
            ExtraBalls--;
            ExtraBallLit = ExtraBalls > 0;
            Message?.Invoke("SHOOT AGAIN", 2f);
            ServeBall();
            return;
        }
        if (BallNumber >= BallsPerGame)
        {
            GameOver = true;
            BallVisible = false;
            EnchantedLeft = 0f;
            Message?.Invoke("GAME OVER", 3f);
            GameEnded?.Invoke(Score);
            return;
        }
        BallNumber++;
        Message?.Invoke($"BALL {BallNumber}", 2f);
        ServeBall();
    }

    public static string Format(long n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
