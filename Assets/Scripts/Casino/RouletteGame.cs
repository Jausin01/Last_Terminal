using System;
using System.Collections.Generic;
using UnityEngine;

// The roulette (European, one zero). She is the croupier: she stands at the wheel; the operator places bets by voice
// ("25 on red", "10 on 17", "first dozen"); "spin" = she flicks the wheel (a ButtonPanel: her finger on the rim).
// The rotor turns, the ball runs the other way, slows down and drops into the pocket of the result; bets are paid
// (straight 35:1, dozens 2:1, even chances 1:1) and the chips leave the layout.
// This transform = where her finger flicks the wheel (on the rim), forward = toward her.
// Wheel: RouletteRotor (pocket i of Order at angle i * 360/37 from its local +x toward +z, tools/casino_convert.py).
public class RouletteGame : ButtonPanel
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string RotorField = nameof(rotor);
    public const string BallField = nameof(ball);
    public const string BankField = nameof(bank);
    public const string ChipPrefabField = nameof(chipPrefab);
    public const string AudioSourceField = nameof(audioSource);
    public const string NumberOneField = nameof(numberOne);
    public const string ColumnStepField = nameof(columnStep);
    public const string RowStepField = nameof(rowStep);
    public const string ZeroSpotField = nameof(zeroSpot);
    public const string DozenSpotsField = nameof(dozenSpots);
    public const string OutsideSpotsField = nameof(outsideSpots);
    public const string HouseSpotField = nameof(houseSpot);
    public const string PlayerSpotField = nameof(playerSpot);
    public const string ViewFromField = nameof(viewFrom);
    public const string ViewAtField = nameof(viewAt);
    public const char SpinKey = 'S';

    // Pocket numbers in wheel order (European wheel), and the red numbers.
    public static readonly int[] Order = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1,
                                           20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
    // The red numbers of the wheel.
    private static readonly HashSet<int> Reds = new HashSet<int> { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    // Bet types the operator can say, and why a bet can't be placed.
    public enum BetKind { Straight, Red, Black, Even, Odd, Low, High, Dozen1, Dozen2, Dozen3 }
    public enum BetResult { Ok, Spinning, BelowMinimum, AboveMaximum, NotEnoughChips, NoSuchNumber }

    // One bet on the layout: its type, the number (straight bets), the amount and its chip objects.
    public class Bet
    {
        // What kind of bet.
        public BetKind Kind;
        public int Number;     // straight only
        // Chips on it, and the chip objects stacked on its cell.
        public int Amount;
        public readonly List<GameObject> Chips = new List<GameObject>();
    }

    // The wheel's rotor, the ball, the operator's chips, the chip prefab and the sound source.
    [SerializeField] private Transform rotor;
    [SerializeField] private Transform ball;
    [SerializeField] private ChipBank bank;
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private AudioSource audioSource;

    [Header("Layout (local space; the cells printed on the felt)")]
    [SerializeField] private Vector3 numberOne;              // center of the cell of 1 (on the felt)
    [SerializeField] private Vector3 columnStep;             // 1 -> 4
    [SerializeField] private Vector3 rowStep;                // 1 -> 2
    // The zero cell and the three dozen cells.
    [SerializeField] private Vector3 zeroSpot;
    [SerializeField] private Vector3[] dozenSpots = new Vector3[3];
    [Tooltip("1-18, EVEN, RED, BLACK, ODD, 19-36")]
    [SerializeField] private Vector3[] outsideSpots = new Vector3[6];
    [Tooltip("Where lost chips go (the croupier's side).")]
    [SerializeField] private Vector3 houseSpot;
    [Tooltip("Where won chips go (the player's side).")]
    [SerializeField] private Vector3 playerSpot;

    // The game camera's position and look-at point (local space).
    [Header("Game camera (local space): a player's view of her at the wheel")]
    [SerializeField] private Vector3 viewFrom;
    [SerializeField] private Vector3 viewAt;

    // Table limits per bet.
    [Header("Rules")]
    [SerializeField] private int minBet = 1;
    [SerializeField] private int maxBet = 500;

    // Wheel geometry and spin timing (pockets, ball track, how long it spins / drops, start speeds, chip move time).
    [Header("Wheel")]
    [SerializeField] private float pocketRadius = 0.215f;
    [SerializeField] private float pocketHeight = 0.968f;   // the rotor's disc (world height)
    [SerializeField] private float trackRadius = 0.355f;
    [SerializeField] private float trackHeight = 1.01f;
    [SerializeField] private float spinTime = 5.5f;
    [SerializeField] private float dropTime = 1.4f;
    [SerializeField] private float rotorSpeed = 160f;       // degrees / s at the start
    [SerializeField] private float ballSpeed = 900f;
    [SerializeField] private float chipTime = 0.45f;

    // Height of one chip.
    private const float ChipHeight = 0.0046f;
    // The bets on the layout now.
    private readonly List<Bet> bets = new List<Bet>();
    private readonly List<(BetKind kind, int number, int amount)> lastBets = new List<(BetKind, int, int)>();
    // Amount used when a bet names none ("on red") and whether the game is open.
    private int stake = 10;
    private bool open;
    // Chip pool and a property block for the chip faces.
    private readonly List<GameObject> chipPool = new List<GameObject>();
    private MaterialPropertyBlock block;
    // Spin state: rotor speed, ball angle, time since the spin started, spinning, the result.
    private float rotorOmega;
    private float ballAngle;
    private float t;
    private bool spinning;
    private int result = -1;
    // Chips won / staked on the last spin (the HUD and her line).
    private int lastWin, lastStake;
    // Where the ball started its drop into the pocket.
    private Vector3 dropFrom;
    // Generated click sounds (ball ticks, chips) and the tick timer.
    private AudioClip tick, clack;
    private float tickTimer;

    // A chip sliding away (to the house or the player) and the list of them.
    private class Tween { public Transform T; public Vector3 From, To; public float Time, Duration; public bool Hide; }
    private readonly List<Tween> tweens = new List<Tween>();

    // Raised when a spin starts and when the spin had no bets (she says so).
    public event Action<RouletteGame> SpinStarted;
    public event Action<RouletteGame, int, int, int> Landed;   // result, chips won (back to the player), chips staked
    public event Action<RouletteGame> NoBets;
    public event Action Changed;   // bets, the stake, the result (the HUD)

    // State read by the HUD and the commands.
    public IReadOnlyList<Bet> Bets => bets;
    public bool IsSpinning => spinning;
    public int LastResult => result;
    public int LastWin => lastWin;
    public int LastStake => lastStake;
    public int MinBet => minBet;
    public int MaxBet => maxBet;
    public int TotalBet { get { int s = 0; foreach (Bet b in bets) s += b.Amount; return s; } }
    public ChipBank Bank => bank;
    public int Stake => stake;
    public bool IsOpen => open;          // she is the croupier now (between "let's play roulette" and "I'm done")
    public bool HasLastBets => lastBets.Count > 0;

    // Where she stands and how she uses the wheel as a button panel (she stays until the game ends).
    public override float StandDistance => 0.32f;
    public override float HoldTime => 600f;   // she stays at the wheel until the game ends
    public override bool KeepHandUp => false;

    // Is this number red?
    public static bool IsRed(int n) => Reds.Contains(n);

    // Checks the references, hides the ball and makes the click sounds.
    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (rotor == null || ball == null || bank == null || chipPrefab == null)
        {
            Debug.LogError($"{name}: RouletteGame is missing references (run the casino menu).", this);
            enabled = false;
            return;
        }
        ball.gameObject.SetActive(false);
        MakeSounds();
    }

    // Clips made by code are lost when the sound output changes: they are made again then (GeneratedAudio).
    private void OnEnable() => GeneratedAudio.Reset += MakeSounds;
    private void OnDisable() => GeneratedAudio.Reset -= MakeSounds;

    // The ball's ticks on the frets and its clack in the pocket.
    private void MakeSounds()
    {
        tick = Tone(0.02f, 2400f);
        clack = Tone(0.06f, 900f);
    }

    // ------------------------------------------------------------------ the game on / off
    public void Open()
    {
        open = true;
        result = -1;
        Changed?.Invoke();
    }

    // every bet on the layout goes back to the player
    public void Close()
    {
        ClearBets();
        open = false;
        Changed?.Invoke();
    }

    // the amount used when a bet names no amount ("on red", "number 17")
    public void SetStake(int amount)
    {
        stake = Mathf.Clamp(amount, minBet, maxBet);
        Changed?.Invoke();
    }

    // the bets of the last spin, again ("same bets")
    public BetResult RepeatLastBets()
    {
        if (lastBets.Count == 0) return BetResult.BelowMinimum;
        int total = 0;
        foreach (var b in lastBets) total += b.amount;
        if (bank.Chips < total) return BetResult.NotEnoughChips;
        BetResult r = BetResult.Ok;
        foreach (var b in lastBets)
        {
            BetResult one = PlaceBet(b.kind, b.number, b.amount);
            if (one != BetResult.Ok) r = one;
        }
        return r;
    }

    // ------------------------------------------------------------------ bets
    public BetResult PlaceBet(BetKind kind, int number, int amount)
    {
        if (spinning) return BetResult.Spinning;
        if (kind == BetKind.Straight && (number < 0 || number > 36)) return BetResult.NoSuchNumber;
        Bet bet = bets.Find(b => b.Kind == kind && (kind != BetKind.Straight || b.Number == number));
        int total = amount + (bet != null ? bet.Amount : 0);
        if (amount < minBet) return BetResult.BelowMinimum;
        if (total > maxBet) return BetResult.AboveMaximum;
        if (bank.Chips < amount) return BetResult.NotEnoughChips;
        bank.Add(-amount);
        if (bet == null) { bet = new Bet { Kind = kind, Number = number }; bets.Add(bet); }
        bet.Amount = total;
        Restack(bet);
        Play(clack);
        Changed?.Invoke();
        return BetResult.Ok;
    }

    // every bet back to the player (she leaves the wheel, "clear the bets")
    public void ClearBets()
    {
        if (spinning) return;
        foreach (Bet b in bets)
        {
            bank.Add(b.Amount);
            MoveAway(b.Chips, playerSpot);
        }
        bets.Clear();
        Changed?.Invoke();
    }

    // Rebuilds the chip stack of a bet on its cell.
    private void Restack(Bet bet)
    {
        foreach (GameObject c in bet.Chips) c.SetActive(false);
        bet.Chips.Clear();
        Vector3 at = transform.TransformPoint(Spot(bet));
        int amount = bet.Amount, i = 0;
        for (int v = 0; v < BlackjackTable.ChipValues.Length && amount > 0; v++)
            while (amount >= BlackjackTable.ChipValues[v])
            {
                amount -= BlackjackTable.ChipValues[v];
                bet.Chips.Add(SpawnChip(v, at + Vector3.up * (ChipHeight * (i++ + 0.5f))));
            }
    }

    // The cell of a bet on the layout (local space).
    public Vector3 Spot(Bet bet)
    {
        switch (bet.Kind)
        {
            case BetKind.Straight:
                if (bet.Number == 0) return zeroSpot;
                int n = bet.Number - 1;
                return numberOne + columnStep * (n / 3) + rowStep * (n % 3);
            case BetKind.Dozen1: return dozenSpots[0];
            case BetKind.Dozen2: return dozenSpots[1];
            case BetKind.Dozen3: return dozenSpots[2];
            case BetKind.Low: return outsideSpots[0];
            case BetKind.Even: return outsideSpots[1];
            case BetKind.Red: return outsideSpots[2];
            case BetKind.Black: return outsideSpots[3];
            case BetKind.Odd: return outsideSpots[4];
            default: return outsideSpots[5];
        }
    }

    // chips paid for every chip bet (0 = lost)
    public static int Payout(BetKind kind, int number, int result)
    {
        bool zero = result == 0;
        switch (kind)
        {
            case BetKind.Straight: return number == result ? 35 : -1;
            case BetKind.Red: return !zero && IsRed(result) ? 1 : -1;
            case BetKind.Black: return !zero && !IsRed(result) ? 1 : -1;
            case BetKind.Even: return !zero && result % 2 == 0 ? 1 : -1;
            case BetKind.Odd: return !zero && result % 2 == 1 ? 1 : -1;
            case BetKind.Low: return result >= 1 && result <= 18 ? 1 : -1;
            case BetKind.High: return result >= 19 ? 1 : -1;
            case BetKind.Dozen1: return result >= 1 && result <= 12 ? 2 : -1;
            case BetKind.Dozen2: return result >= 13 && result <= 24 ? 2 : -1;
            default: return result >= 25 ? 2 : -1;
        }
    }

    // The game camera's view for GameCameraDirector.
    public override bool TryGetGameView(out Vector3 from, out Vector3 lookAt)
    {
        from = transform.TransformPoint(viewFrom);
        lookAt = transform.TransformPoint(viewAt);
        return viewFrom != viewAt;
    }

    // ------------------------------------------------------------------ the spin (her finger on the rim)
    public override bool TryGetKey(char key, out Vector3 at)
    {
        at = transform.position + Vector3.up * (trackHeight + 0.01f);
        return char.ToUpperInvariant(key) == SpinKey;
    }

    // "Spin": the result is drawn now (the animation then lands the ball on it); the bets are remembered for "same bets".
    public override void Press(char key)
    {
        if (char.ToUpperInvariant(key) != SpinKey || spinning) return;
        if (bets.Count == 0) { NoBets?.Invoke(this); return; }
        result = Order[UnityEngine.Random.Range(0, Order.Length)];
        lastStake = TotalBet;
        lastBets.Clear();
        foreach (Bet b in bets) lastBets.Add((b.Kind, b.Number, b.Amount));
        lastWin = 0;
        spinning = true;
        t = 0f;
        rotorOmega = rotorSpeed;
        ballAngle = UnityEngine.Random.Range(0f, 360f);
        ball.SetParent(transform.parent, true);
        ball.gameObject.SetActive(true);
        SpinStarted?.Invoke(this);
        Changed?.Invoke();
    }

    // Animates the rotor, the ball around the track and its drop into the result's pocket; then settles the bets.
    private void Update()
    {
        UpdateTweens();
        // the rotor keeps turning, slowing down
        if (rotorOmega > 0.01f)
        {
            rotor.Rotate(0f, rotorOmega * Time.deltaTime, 0f, Space.World);
            rotorOmega = Mathf.MoveTowards(rotorOmega, 0f, (spinning ? 12f : 18f) * Time.deltaTime);
        }
        if (!spinning) return;

        t += Time.deltaTime;
        Vector3 axis = rotor.position;
        if (t < spinTime - dropTime)
        {
            // around the track the other way, slowing down
            float k = t / (spinTime - dropTime);
            ballAngle += Mathf.Lerp(ballSpeed, ballSpeed * 0.25f, k) * Time.deltaTime;   // the rotor turns clockwise (+yaw): the ball the other way
            ball.position = Track(axis, ballAngle, trackRadius, trackHeight);
            dropFrom = ball.position;
            tickTimer -= Time.deltaTime;
            if (k > 0.6f && tickTimer <= 0f) { Play(tick); tickTimer = Mathf.Lerp(0.25f, 0.08f, k); }
            return;
        }
        // drops into its pocket (which moves with the rotor), with a couple of bounces
        float d = Mathf.Clamp01((t - (spinTime - dropTime)) / dropTime);
        Vector3 target = Pocket(result);
        float e = Mathf.SmoothStep(0f, 1f, d);
        Vector3 p = Vector3.Lerp(dropFrom, target, e);
        p.y += Mathf.Abs(Mathf.Sin(d * Mathf.PI * 3f)) * 0.02f * (1f - d);
        ball.position = p;
        if (d < 1f) return;

        ball.SetParent(rotor, true);   // rides with the rotor
        spinning = false;
        Play(clack);
        Settle();
    }

    // World position of a number's pocket (it turns with the rotor).
    private Vector3 Pocket(int number)
    {
        int i = Array.IndexOf(Order, number);
        float a = i * 2f * Mathf.PI / Order.Length;
        Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * pocketRadius;
        Vector3 w = rotor.TransformPoint(local);
        w.y = pocketHeight + 0.008f;
        return w;
    }

    // A point on the ball track at an angle.
    private static Vector3 Track(Vector3 axis, float degrees, float radius, float height)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(axis.x + Mathf.Cos(a) * radius, height, axis.z + Mathf.Sin(a) * radius);
    }

    // Pays every bet on the result: lost chips go to the house, won chips (bet + winnings) to the player.
    private void Settle()
    {
        int won = 0;
        foreach (Bet b in bets)
        {
            int pay = Payout(b.Kind, b.Number, result);
            if (pay < 0) { MoveAway(b.Chips, houseSpot); continue; }
            int back = b.Amount * (pay + 1);
            won += back;
            // the winnings next to the bet, then everything to the player
            var paid = new List<GameObject>();
            Vector3 at = transform.TransformPoint(Spot(b)) + transform.right * 0.04f;
            int amount = b.Amount * pay, i = 0;
            for (int v = 0; v < BlackjackTable.ChipValues.Length && amount > 0; v++)
                while (amount >= BlackjackTable.ChipValues[v])
                {
                    amount -= BlackjackTable.ChipValues[v];
                    paid.Add(SpawnChip(v, at + Vector3.up * (ChipHeight * (i++ + 0.5f))));
                }
            paid.AddRange(b.Chips);
            MoveAway(paid, playerSpot);
        }
        bets.Clear();
        lastWin = won;
        if (won > 0) bank.Add(won);
        Landed?.Invoke(this, result, won, lastStake);
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ chips
    private GameObject SpawnChip(int valueIndex, Vector3 position)
    {
        GameObject go = chipPool.Find(c => !c.activeSelf);
        if (go == null)
        {
            go = Instantiate(chipPrefab, transform.parent);
            chipPool.Add(go);
        }
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        go.SetActive(true);
        BlackjackTable.PaintChip(go.GetComponentInChildren<Renderer>(), valueIndex, block);
        return go;
    }

    // Sends chips sliding away (and hides them at the end).
    private void MoveAway(List<GameObject> chips, Vector3 localTo)
    {
        Vector3 to = transform.TransformPoint(localTo);
        foreach (GameObject c in chips)
            tweens.Add(new Tween { T = c.transform, From = c.transform.position, To = to + (c.transform.position - chips[0].transform.position), Duration = chipTime + 0.6f, Hide = true });
        chips.Clear();
    }

    // Advances the chip slides.
    private void UpdateTweens()
    {
        for (int i = tweens.Count - 1; i >= 0; i--)
        {
            Tween w = tweens[i];
            w.Time += Time.deltaTime;
            float k = Mathf.Clamp01((w.Time - 0.5f) / Mathf.Max(0.01f, w.Duration - 0.5f));   // a short pause, then they slide
            w.T.position = Vector3.Lerp(w.From, w.To, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.04f;
            if (k < 1f) continue;
            if (w.Hide) w.T.gameObject.SetActive(false);
            tweens.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ sounds
    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // A short decaying sine tone (procedural click sound).
    private static AudioClip Tone(float seconds, float frequency)
    {
        // Sample rate of the generated sounds.
        const int rate = 22050;
        int n = Mathf.CeilToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float x = (float)i / rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * x) * Mathf.Exp(-x * 60f) * 0.4f;
        }
        AudioClip clip = AudioClip.Create("Roulette", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
