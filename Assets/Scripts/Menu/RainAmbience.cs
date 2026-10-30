using UnityEngine;

// The sound of the rain on the window, synthesized (no audio file): a few seconds of filtered noise with soft
// drops, looping on the AudioSource. Made again when the sound output changes (GeneratedAudio).
[RequireComponent(typeof(AudioSource))]
public class RainAmbience : MonoBehaviour
{
    // Loop length (s), sample rate, how much of the high hiss is kept (0..1) and how many drops per second.
    [SerializeField] private float seconds = 6f;
    [SerializeField] private int rate = 22050;
    [SerializeField, Range(0f, 1f)] private float brightness = 0.35f;
    [SerializeField] private float dropsPerSecond = 14f;

    private AudioSource source;

    // Makes the loop and plays it.
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        MakeSounds();
    }

    // Clips made by code are lost when the sound output changes: made again then.
    private void OnEnable() => GeneratedAudio.Reset += MakeSounds;
    private void OnDisable() => GeneratedAudio.Reset -= MakeSounds;

    // Rain: low-passed noise (the hiss) + short bright ticks (drops on the glass), faded at the ends to loop cleanly.
    private void MakeSounds()
    {
        int n = Mathf.RoundToInt(seconds * rate);
        var data = new float[n];
        var rng = new System.Random(17);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * brightness;
            lp2 += (lp - lp2) * 0.5f;
            data[i] = lp2 * 0.5f;
        }
        int drops = Mathf.RoundToInt(dropsPerSecond * seconds);
        for (int d = 0; d < drops; d++)
        {
            int at = rng.Next(n);
            float freq = 1800f + (float)rng.NextDouble() * 2600f, amp = 0.05f + (float)rng.NextDouble() * 0.12f;
            int len = rate / 60;
            for (int k = 0; k < len && at + k < n; k++)
                data[at + k] += amp * Mathf.Sin(2f * Mathf.PI * freq * k / rate) * Mathf.Exp(-k * 12f / len);
        }
        // the loop: the last `edge` samples are crossfaded into the first ones and cut off (no click at the seam)
        int edge = rate / 4;
        var loop = new float[n - edge];
        for (int k = 0; k < loop.Length; k++) loop[k] = data[k];
        for (int k = 0; k < edge; k++)
        {
            float t = (float)k / edge;
            loop[k] = data[k] * t + data[n - edge + k] * (1f - t);
        }
        if (source.clip != null) Destroy(source.clip);
        var clip = AudioClip.Create("Rain", loop.Length, 1, rate, false);
        clip.SetData(loop, 0);
        source.clip = clip;
        source.Play();
    }
}
