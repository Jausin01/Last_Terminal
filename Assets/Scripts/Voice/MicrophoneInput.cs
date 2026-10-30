using System;
using UnityEngine;

// The microphone the game listens to, shared by every scene that is open (his room + a location): one recording,
// started on the device chosen in the settings (CameraSettings.Microphone; empty or unplugged = the system default).
// Users (VoiceCommandInput, the settings' microphone test) Acquire it while they need it and Release it after;
// the recording stops when nobody uses it. The audio is a looping clip: users read it from their own position.
// Also the settings' recognition test: while TestOpen, what the recognizer hears is reported here (Heard) instead of
// being sent to Helena.
public static class MicrophoneInput
{
    // Length of the looping recording (s); the rate the recognizer wants (the device's nearest if it can't).
    public const int ClipSeconds = 4;
    private const int WantedRate = 16000;

    // The device recording now (null = the system default), the device asked for, the recording and its rate.
    private static string device;
    private static string wanted = "";
    private static AudioClip clip;
    private static int rate = WantedRate;
    private static int users;
    // Level meter buffer (the last ~0.1 s).
    private static float[] levelBuffer = new float[0];

    // Raised when the recording restarts (another device, or a new rate): users reset their read position.
    public static event Action Restarted;
    // Raised when the recognition test hears something (the text; empty = nothing understood).
    public static event Action<string> TestHeard;

    // The looping recording (null when off or there is no microphone), its sample rate, and whether it runs.
    public static AudioClip Clip => clip;
    public static int Rate => rate;
    public static bool IsRecording => clip != null && Microphone.IsRecording(device);
    // There is at least one microphone on this machine.
    public static bool HasDevice => Microphone.devices.Length > 0;
    // The chosen device is not plugged in (the default is used meanwhile).
    public static bool WantedMissing => !string.IsNullOrEmpty(wanted) && Array.IndexOf(Microphone.devices, wanted) < 0;
    // Write position in the looping clip (samples).
    public static int Position => clip != null ? Microphone.GetPosition(device) : 0;

    // The settings' recognition test is on screen: VoiceCommandInput listens even with the settings open and
    // reports what it hears here.
    public static bool TestOpen { get; set; }

    // Fresh state on every play (also with domain reload off in the editor).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        device = null; wanted = ""; clip = null; rate = WantedRate; users = 0;
        Restarted = null; TestHeard = null; TestOpen = false;
    }

    // Starts using the microphone (the recording starts with the first user). preferred = device name ("" = default).
    public static void Acquire(string preferred)
    {
        users++;
        wanted = preferred ?? "";
        if (clip == null) StartRecording();
        else if (Resolve(wanted) != device) Restart();
    }

    // Stops using it; the recording stops with the last user.
    public static void Release()
    {
        users = Mathf.Max(0, users - 1);
        if (users == 0) StopRecording();
    }

    // The settings chose another device: the recording moves to it (if anyone is using it).
    public static void Use(string preferred)
    {
        wanted = preferred ?? "";
        if (users > 0 && (clip == null || Resolve(wanted) != device || !Microphone.IsRecording(device))) Restart();
    }

    // The device's level now: the loudest RMS of the last ~0.1 s, 0..1.
    public static float Level()
    {
        if (clip == null) return 0f;
        int n = Mathf.Max(1, rate / 10);
        if (levelBuffer.Length != n) levelBuffer = new float[n];
        int start = ((Position - n) % clip.samples + clip.samples) % clip.samples;
        clip.GetData(levelBuffer, start);
        float sum = 0f;
        foreach (float s in levelBuffer) sum += s * s;
        return Mathf.Clamp01(Mathf.Sqrt(sum / n) * 4f);   // speech sits around 0.05-0.2 RMS: x4 fills the meter
    }

    // The last `seconds` of the recording as a new clip (the settings play it back).
    public static AudioClip Snapshot(float seconds)
    {
        if (clip == null) return null;
        int n = Mathf.Min(clip.samples - 1, Mathf.RoundToInt(seconds * rate));
        var data = new float[n];
        int start = ((Position - n) % clip.samples + clip.samples) % clip.samples;
        clip.GetData(data, start);
        AudioClip copy = AudioClip.Create("MicTest", n, 1, rate, false);
        copy.SetData(data, 0);
        return copy;
    }

    // The recognizer heard this during the test.
    public static void ReportTest(string text) => TestHeard?.Invoke(text ?? "");

    // A device name that exists (null = the system default).
    private static string Resolve(string name) => !string.IsNullOrEmpty(name) && Array.IndexOf(Microphone.devices, name) >= 0 ? name : null;

    // Opens the chosen device (or the default) at the rate the recognizer wants, or the nearest it has.
    private static void StartRecording()
    {
        if (Microphone.devices.Length == 0) { clip = null; return; }
        device = Resolve(wanted);
        Microphone.GetDeviceCaps(device, out int min, out int max);
        rate = min == 0 && max == 0 ? WantedRate : Mathf.Clamp(WantedRate, min, max);
        clip = Microphone.Start(device, true, ClipSeconds, rate);
        if (clip == null) Debug.LogWarning($"[Voice] The microphone '{device ?? "default"}' could not start.");
    }

    // Stops the recording.
    private static void StopRecording()
    {
        if (clip != null && Microphone.IsRecording(device)) Microphone.End(device);
        clip = null;
    }

    // Another device (or it came back): stop, start, tell the users.
    private static void Restart()
    {
        StopRecording();
        StartRecording();
        Restarted?.Invoke();
    }
}
