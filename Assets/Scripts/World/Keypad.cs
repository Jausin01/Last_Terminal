using System;
using TMPro;
using UnityEngine;

// A code keypad on the wall beside a locked door. She presses the keys (PartnerKeypadUser); the right code + E
// unlocks and opens the door. C clears, a wrong code flashes ERROR and clears. Like a real lock, the last digits
// typed are what count: a stray key before the code ("1 1 7") still opens it.
// Layout (this transform = the panel center, forward = out of the wall): keys 1-9, then C 0 E on the last row;
// the key transforms (in that order) are where her finger goes. Beeps are made here (no audio files needed).
public class Keypad : ButtonPanel
{
    public const string DoorField = nameof(door);
    public const string CodeField = nameof(code);
    public const string KeysField = nameof(keys);
    public const string DisplayField = nameof(display);
    public const string AudioSourceField = nameof(audioSource);
    public const string KeyOrder = "123456789C0E";
    public const char Clear = 'C';
    public const char Enter = 'E';

    public enum Result { Typed, Cleared, Accepted, Rejected, AlreadyOpen }

    [SerializeField] private Door door;
    [Tooltip("Digits only.")]
    [SerializeField] private string code = "17";
    [Tooltip("Key transforms in KeyOrder: 1 2 3 4 5 6 7 8 9 C 0 E.")]
    [SerializeField] private Transform[] keys = new Transform[12];
    [SerializeField] private TMP_Text display;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Min(1)] private int maxDigits = 8;
    [SerializeField] private float errorTime = 1.5f;
    [SerializeField] private Color typingColor = new Color(1f, 0.75f, 0.2f);
    [SerializeField] private Color errorColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] private Color openColor = new Color(0.25f, 1f, 0.35f);

    private string entered = "";
    private bool open;
    private float errorTimer;
    private AudioClip beep, buzz, chime;

    public event Action<Keypad, Result> Used;

    public Door Door => door;
    public Result LastResult { get; private set; } = Result.Typed;
    public bool IsOpen => open;
    public string Entered => entered;

    private void Awake()
    {
        if (door == null || keys == null || keys.Length != KeyOrder.Length)
        {
            Debug.LogError($"{name}: Keypad is missing references (run Last Terminal/4).", this);
            enabled = false;
            return;
        }
        beep = Tone("Beep", 0.07f, 1350f, 0f, false);
        buzz = Tone("Buzz", 0.45f, 180f, 0f, true);
        chime = Tone("Chime", 0.35f, 880f, 1320f, false);
        Refresh();
    }

    // where her fingertip goes for that key (world)
    public override bool TryGetKey(char key, out Vector3 position)
    {
        int i = KeyOrder.IndexOf(char.ToUpperInvariant(key));
        position = i >= 0 && keys[i] != null ? keys[i].position : transform.position;
        return i >= 0 && keys[i] != null;
    }

    public override void Press(char key)
    {
        key = char.ToUpperInvariant(key);
        if (open) { Play(beep); LastResult = Result.AlreadyOpen; Used?.Invoke(this, LastResult); return; }
        errorTimer = 0f;
        Result result;
        if (key == Clear) { entered = ""; result = Result.Cleared; Play(beep); }
        else if (key == Enter) result = Submit();
        else
        {
            if (char.IsDigit(key) && entered.Length < maxDigits) entered += key;
            result = Result.Typed;
            Play(beep);
        }
        Refresh();
        LastResult = result;
        Used?.Invoke(this, result);
    }

    private Result Submit()
    {
        Debug.Log($"[Keypad] {name}: \"{entered}\" entered ({(entered.EndsWith(code) ? "right" : "wrong")}).", this);
        if (code.Length > 0 && entered.EndsWith(code, StringComparison.Ordinal))
        {
            open = true;
            Play(chime);
            door.SetLocked(false);
            door.Open();
            return Result.Accepted;
        }
        entered = "";
        errorTimer = errorTime;
        Play(buzz);
        return Result.Rejected;
    }

    private void Update()
    {
        if (errorTimer <= 0f) return;
        errorTimer -= Time.deltaTime;
        if (errorTimer <= 0f) Refresh();
    }

    private void Refresh()
    {
        if (display == null) return;
        if (open) { display.text = "OPEN"; display.color = openColor; }
        else if (errorTimer > 0f) { display.text = "ERROR"; display.color = errorColor; }
        else { display.text = entered.Length > 0 ? entered : "CODE"; display.color = typingColor; }
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // a short tone (two notes when toFrequency > 0), square wave for the error buzz
    private static AudioClip Tone(string clipName, float seconds, float frequency, float secondFrequency, bool square)
    {
        const int rate = 22050;
        int n = Mathf.CeilToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = secondFrequency > 0f && t > seconds * 0.45f ? secondFrequency : frequency;
            float s = Mathf.Sin(2f * Mathf.PI * f * t);
            if (square) s = Mathf.Sign(s) * 0.6f;
            float envelope = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((seconds - t) / 0.03f);
            data[i] = s * envelope * 0.35f;
        }
        AudioClip clip = AudioClip.Create(clipName, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
