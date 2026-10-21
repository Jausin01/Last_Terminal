using System;
using UnityEngine;

public enum SlotSymbol { Cherry, Grapes, Coin, Seven, Clown, Bell }

// A slot machine of the casino. She presses its SPIN button (a ButtonPanel, PartnerKeypadUser); the bet is taken from
// the chips, the three reels spin (their texture scrolls: T_SlotReels, 8 symbols per reel, 3 in the window) and stop
// one after the other on random positions; the middle row pays (Paytable). Wins go back into the chips.
// Reel strips = the columns of T_SlotReels (tools/slot_reels.py), bottom -> top: keep them in sync.
public class SlotMachine : ButtonPanel
{
    public const string ReelsField = nameof(reels);
    public const string SpinButtonField = nameof(spinButton);
    public const string BankField = nameof(bank);
    public const string AudioSourceField = nameof(audioSource);
    public const string NumberField = nameof(number);
    public const char Spin = 'S';
    public const int SymbolsPerReel = 8;

    public static readonly SlotSymbol[][] Strips =
    {
        new[] { SlotSymbol.Cherry, SlotSymbol.Grapes, SlotSymbol.Coin, SlotSymbol.Seven, SlotSymbol.Clown, SlotSymbol.Bell, SlotSymbol.Grapes, SlotSymbol.Coin },
        new[] { SlotSymbol.Bell, SlotSymbol.Coin, SlotSymbol.Cherry, SlotSymbol.Grapes, SlotSymbol.Seven, SlotSymbol.Coin, SlotSymbol.Clown, SlotSymbol.Grapes },
        new[] { SlotSymbol.Coin, SlotSymbol.Grapes, SlotSymbol.Bell, SlotSymbol.Cherry, SlotSymbol.Coin, SlotSymbol.Clown, SlotSymbol.Grapes, SlotSymbol.Seven },
    };

    // three of a kind on the line (times the bet); a cherry on the first reel alone returns the bet (~93% back)
    private static readonly int[] ThreeOfAKind = { 30, 12, 12, 100, 50, 40 };   // SlotSymbol order
    private const int FirstCherry = 1;

    [SerializeField] private Renderer[] reels = new Renderer[3];
    [Tooltip("Where her finger goes (the SPIN button).")]
    [SerializeField] private Transform spinButton;
    [SerializeField] private ChipBank bank;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Min(1)] private int number = 1;
    [SerializeField] private int minBet = 5;
    [SerializeField] private int maxBet = 100;
    [Tooltip("Symbols per second while spinning.")]
    [SerializeField] private float spinSpeed = 14f;
    [Tooltip("When each reel stops (s after the press).")]
    [SerializeField] private Vector3 stopTimes = new Vector3(1.1f, 1.6f, 2.1f);

    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
    private MaterialPropertyBlock block;
    private readonly float[] position = new float[3];     // in symbols (window bottom); the middle row = position + 1
    private readonly int[] target = new int[3];
    private readonly bool[] stopped = { true, true, true };
    private float spinTime;
    private bool spinning;
    private int bet = 10;
    private int lastWin;
    private AudioClip tick, win, lose;

    public event Action<SlotMachine, int> SpinStarted;    // bet
    public event Action<SlotMachine, int> SpinEnded;      // win (0 = nothing)
    public event Action<SlotMachine> NotEnoughChips;

    public int Number => number;
    public int Bet => bet;
    public int MinBet => minBet;
    public int MaxBet => maxBet;
    public bool IsSpinning => spinning;
    public int LastWin => lastWin;
    public override float StandDistance => 0.45f;
    public override float HoldTime => 60f;   // she stays at the machine between spins
    public override bool KeepHandUp => false; // the hand comes down while the reels spin

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (bank == null || spinButton == null || reels.Length != 3)
        {
            Debug.LogError($"{name}: SlotMachine is missing references (run the casino menu).", this);
            enabled = false;
            return;
        }
        for (int r = 0; r < 3; r++)
        {
            position[r] = UnityEngine.Random.Range(0, SymbolsPerReel);
            Show(r);
        }
        tick = Tone(0.03f, 900f, false);
        win = Tone(0.6f, 660f, true);
        lose = Tone(0.25f, 220f, false);
    }

    public int SetBet(int chips)
    {
        bet = Mathf.Clamp(chips, minBet, maxBet);
        return bet;
    }

    // the game camera: in the aisle, at her right shoulder, looking at her and the reels
    public override bool TryGetGameView(out Vector3 from, out Vector3 lookAt)
    {
        Transform t = transform;
        from = t.position + t.forward * 1.3f - t.right * 0.75f + Vector3.up * 1.65f;
        lookAt = t.position + t.forward * 0.3f + Vector3.up * 1.05f;
        return true;
    }

    public override bool TryGetKey(char key, out Vector3 at)
    {
        at = spinButton.position;
        return char.ToUpperInvariant(key) == Spin;
    }

    public override void Press(char key)
    {
        if (char.ToUpperInvariant(key) != Spin || spinning) return;
        if (bank.Chips < bet) { NotEnoughChips?.Invoke(this); return; }
        bank.Add(-bet);
        for (int r = 0; r < 3; r++)
        {
            target[r] = UnityEngine.Random.Range(0, SymbolsPerReel);   // the middle symbol it will stop on
            stopped[r] = false;
        }
        spinTime = 0f;
        spinning = true;
        lastWin = 0;
        SpinStarted?.Invoke(this, bet);
    }

    private void Update()
    {
        if (!spinning) return;
        spinTime += Time.deltaTime;
        for (int r = 0; r < 3; r++)
        {
            if (stopped[r]) continue;
            float stopAt = stopTimes[r];
            if (spinTime < stopAt)
            {
                position[r] = Mathf.Repeat(position[r] + spinSpeed * Time.deltaTime, SymbolsPerReel);
            }
            else
            {
                position[r] = Mathf.Repeat(target[r] - 1, SymbolsPerReel);   // its symbol in the middle row
                stopped[r] = true;
                Play(tick);
            }
            Show(r);
        }
        if (stopped[0] && stopped[1] && stopped[2]) Finish();
    }

    private void Finish()
    {
        spinning = false;
        SlotSymbol a = Strips[0][target[0]], b = Strips[1][target[1]], c = Strips[2][target[2]];
        int multiplier = a == b && b == c ? ThreeOfAKind[(int)a] : a == SlotSymbol.Cherry ? FirstCherry : 0;
        lastWin = bet * multiplier;
        if (lastWin > 0) bank.Add(lastWin);
        Play(lastWin > bet ? win : lose);
        SpinEnded?.Invoke(this, lastWin);
    }

    // the reel texture moved so the window starts at position (in symbols)
    private void Show(int r)
    {
        if (reels[r] == null) return;
        block.Clear();
        var st = new Vector4(1f, 1f, 0f, position[r] / SymbolsPerReel);
        block.SetVector(BaseMapSt, st);
        block.SetVector(MainTexSt, st);
        reels[r].SetPropertyBlock(block);
    }

    // the symbols on the middle row right now (after a spin: the result)
    public SlotSymbol Middle(int reel) => Strips[reel][Mathf.RoundToInt(position[reel] + 1) % SymbolsPerReel];

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private static AudioClip Tone(float seconds, float frequency, bool arpeggio)
    {
        const int rate = 22050;
        int n = Mathf.CeilToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = arpeggio ? frequency * (1f + Mathf.Floor(t / (seconds / 4f)) * 0.25f) : frequency;
            data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Clamp01((seconds - t) / 0.05f) * 0.3f;
        }
        AudioClip clip = AudioClip.Create("Slot", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
