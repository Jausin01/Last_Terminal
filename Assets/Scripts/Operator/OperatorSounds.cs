using System.Collections.Generic;
using UnityEngine;

// Small sounds of the operator's room and the pinball, synthesized once (no audio files needed): replace them
// with real clips later by assigning AudioClips where the components play them.
public static class OperatorSounds
{
    // Every synthesized sound (the enum is stored by number: add new ones at the end).
    public enum Kind
    {
        Click, Beep, Brew, Sip, Rattle, Glug, Paper,
        Flipper, Bumper, Sling, Lane, Target, Saucer, Launch, Drain, Enchanted, BallSaved, ExtraBall, Kickout,
        // the operator room (Operator scene): new kinds go at the end (the enum is saved by number)
        Metal, KeyBeep, Denied, Accepted, PowerButton, Tick, Knob, Locker, Drawer
    }

    // Sample rate and the clips made so far.
    private const int Rate = 22050;
    private static readonly Dictionary<Kind, AudioClip> Cache = new Dictionary<Kind, AudioClip>();

    // The clip of a kind (made on first use).
    public static AudioClip Get(Kind kind)
    {
        if (Cache.TryGetValue(kind, out AudioClip clip) && clip != null) return clip;
        clip = Make(kind);
        Cache[kind] = clip;
        return clip;
    }

    // Plays a kind once on a source.
    public static void Play(AudioSource source, Kind kind, float volume = 1f)
    {
        if (source != null) source.PlayOneShot(Get(kind), volume);
    }

    // Synthesizes one kind from the building blocks below.
    private static AudioClip Make(Kind kind)
    {
        float[] d;
        var rng = new System.Random((int)kind * 7919 + 13);
        switch (kind)
        {
            case Kind.Click: d = Noise(rng, 0.03f, 0.5f, 60f); break;
            case Kind.Beep: d = Tone(0.12f, 880f, 880f, 0.35f, 20f, square: true); break;
            case Kind.Brew: d = Brew(rng); break;
            case Kind.Sip: d = Mix(Noise(rng, 0.35f, 0.18f, 6f), Tone(0.35f, 180f, 140f, 0.08f, 6f)); break;
            case Kind.Rattle: d = Rattle(rng); break;
            case Kind.Glug: d = Glug(); break;
            case Kind.Paper: d = Noise(rng, 0.25f, 0.25f, 9f); break;
            case Kind.Flipper: d = Mix(Noise(rng, 0.07f, 0.6f, 45f), Tone(0.07f, 120f, 60f, 0.5f, 45f)); break;
            case Kind.Bumper: d = Mix(Tone(0.18f, 520f, 180f, 0.5f, 16f, square: true), Noise(rng, 0.05f, 0.4f, 60f)); break;
            case Kind.Sling: d = Mix(Tone(0.1f, 300f, 150f, 0.45f, 30f, square: true), Noise(rng, 0.04f, 0.4f, 70f)); break;
            case Kind.Lane: d = Seq(new[] { 988f, 1319f }, 0.06f, 0.3f); break;
            case Kind.Target: d = Mix(Tone(0.12f, 700f, 400f, 0.4f, 25f, square: true), Noise(rng, 0.04f, 0.5f, 80f)); break;
            case Kind.Saucer: d = Seq(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.08f, 0.35f); break;
            case Kind.Kickout: d = Mix(Noise(rng, 0.09f, 0.7f, 35f), Tone(0.09f, 90f, 50f, 0.6f, 30f)); break;
            case Kind.Launch: d = Mix(Noise(rng, 0.2f, 0.5f, 14f), Tone(0.2f, 200f, 600f, 0.2f, 10f)); break;
            case Kind.Drain: d = Tone(0.9f, 440f, 70f, 0.4f, 2.5f, square: true); break;
            case Kind.Enchanted: d = Seq(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f, 1568f }, 0.09f, 0.35f); break;
            case Kind.BallSaved: d = Seq(new[] { 784f, 659f, 784f, 1047f }, 0.1f, 0.35f); break;
            case Kind.ExtraBall: d = Seq(new[] { 523f, 523f, 784f, 784f, 1047f, 1047f, 1568f }, 0.08f, 0.35f); break;
            case Kind.Metal: d = Metal(rng); break;
            case Kind.KeyBeep: d = Tone(0.07f, 1320f, 1320f, 0.28f, 30f); break;
            case Kind.Denied: d = Mix(Tone(0.42f, 180f, 160f, 0.35f, 4f, square: true), Tone(0.42f, 190f, 170f, 0.25f, 4f, square: true)); break;
            case Kind.Accepted: d = Seq(new[] { 880f, 1175f, 1568f }, 0.09f, 0.28f); break;
            case Kind.PowerButton: d = Mix(Noise(rng, 0.05f, 0.6f, 70f), Tone(0.05f, 2400f, 1800f, 0.15f, 80f)); break;
            case Kind.Tick: d = Mix(Noise(rng, 0.015f, 0.35f, 300f), Tone(0.015f, 3200f, 3000f, 0.12f, 300f)); break;
            case Kind.Knob: d = Mix(Noise(rng, 0.04f, 0.5f, 90f), Tone(0.03f, 900f, 700f, 0.15f, 90f)); break;
            case Kind.Locker: d = Mix(Rattle(rng), Metal(rng)); break;
            case Kind.Drawer: d = Mix(Noise(rng, 0.45f, 0.3f, 5f), Tone(0.45f, 90f, 70f, 0.2f, 5f)); break;
            default: d = new float[Rate / 10]; break;
        }
        AudioClip clip = AudioClip.Create("Snd_" + kind, d.Length, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // ------------------------------------------------------------------ building blocks
    private static float[] Tone(float seconds, float f0, float f1, float amp, float decay, bool square = false)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var d = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(f0, f1, i / (float)n);
            phase += 2.0 * Mathf.PI * f / Rate;
            float s = (float)System.Math.Sin(phase);
            if (square) s = s >= 0f ? 0.6f : -0.6f;
            d[i] = s * amp * Mathf.Exp(-t * decay) * Fade(i, n);
        }
        return d;
    }

    // Low-passed noise with an exponential decay.
    private static float[] Noise(System.Random rng, float seconds, float amp, float decay)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.35f;
            d[i] = lp * amp * Mathf.Exp(-t * decay) * Fade(i, n);
        }
        return d;
    }

    // notes one after the other (little arcade jingles)
    private static float[] Seq(float[] notes, float noteTime, float amp)
    {
        var all = new List<float>();
        foreach (float f in notes) all.AddRange(Tone(noteTime, f, f, amp, 8f, square: true));
        return all.ToArray();
    }

    // Adds two sounds (clipped to -1..1).
    private static float[] Mix(float[] a, float[] b)
    {
        var d = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp((i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f), -1f, 1f);
        return d;
    }

    // Short fade in / out against clicks.
    private static float Fade(int i, int n) => Mathf.Clamp01(i / 40f) * Mathf.Clamp01((n - i) / 80f);

    // the coffee machine: grinding hum, then hissing and dripping
    private static float[] Brew(System.Random rng)
    {
        int n = (int)(3.2f * Rate);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * (t < 1.2f ? 0.08f : 0.5f);
            float hum = t < 1.2f ? Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.1f : 0f;
            float hiss = t >= 1.2f ? lp * 0.22f * (0.7f + 0.3f * Mathf.Sin(t * 23f)) : lp * 0.15f;
            float drip = t > 1.6f && (i % (Rate / 5)) < 300 ? Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.15f * Mathf.Exp(-(i % (Rate / 5)) / 60f) : 0f;
            d[i] = (hum + hiss + drip) * Fade(i, n);
        }
        return d;
    }

    // a locked door: three knocks of the handle
    private static float[] Rattle(System.Random rng)
    {
        var all = new List<float>();
        for (int k = 0; k < 3; k++)
        {
            all.AddRange(Mix(Noise(rng, 0.07f, 0.7f, 40f), Tone(0.07f, 160f, 110f, 0.5f, 40f)));
            all.AddRange(new float[(int)(0.06f * Rate)]);
        }
        return all.ToArray();
    }

    // a hit on sealed metal: a low clank with a ringing tail (inharmonic partials)
    private static float[] Metal(System.Random rng)
    {
        int n = (int)(1.1f * Rate);
        var d = new float[n];
        float[] f = { 143f, 387f, 612f, 1010f, 1473f };
        float[] a = { 0.30f, 0.22f, 0.16f, 0.10f, 0.06f };
        float[] k = { 3.2f, 4.5f, 6f, 8f, 11f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int p = 0; p < f.Length; p++) s += Mathf.Sin(2f * Mathf.PI * f[p] * t) * a[p] * Mathf.Exp(-t * k[p]);
            d[i] = s * Fade(i, n);
        }
        return Mix(d, Noise(rng, 0.06f, 0.8f, 50f));
    }

    // Four short descending tones (water glug).
    private static float[] Glug()
    {
        var all = new List<float>();
        float[] f = { 320f, 260f, 300f, 240f };
        foreach (float x in f) all.AddRange(Tone(0.11f, x, x * 0.6f, 0.4f, 12f));
        return all.ToArray();
    }
}
