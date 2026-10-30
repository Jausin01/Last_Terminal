using System;
using UnityEngine;

// The audio part of the settings screens (the PC's Settings window and the cameras' Esc menu), the same in both:
// - which microphone: cycles the system default and every device plugged in (saved in CameraSettings);
// - its level meter, live while the settings are open;
// - "record and listen": 3 s from the microphone, played back on the screen's AudioSource;
// - recognition test: hold the talk key (V) with the settings open; what the game understood is shown;
// - the sound output: Unity always plays on the system's output, so a "test sound" button and where to route
//   just the game to other speakers / headphones (Windows' Volume mixer).
// Plain C# object owned by the screen: Open / Close with the screen, Tick every frame while open.
public sealed class MicrophoneTest
{
    // Recording length of the playback test (s).
    private const float RecordSeconds = 3f;

    // Where the test is: idle, recording, playing it back.
    public enum Stage { Idle, Recording, Playing }

    // The settings (device choice, language) and the source that plays the recording / test sound.
    private readonly CameraSettings settings;
    private readonly AudioSource source;
    // Open now, stage of the playback test and its timer, the last recognized text and when it came.
    private bool open;
    private Stage stage;
    private float timer;
    private AudioClip recording;
    private string heard = "";
    private bool heardAnything;

    public MicrophoneTest(CameraSettings settings, AudioSource source)
    {
        this.settings = settings;
        this.source = source;
    }

    // Stage of the record-and-listen test, and the meter's level now (0..1).
    public Stage Current => stage;
    public float Level => open ? MicrophoneInput.Level() : 0f;
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private string T(string en, string pt) => Pt ? pt : en;

    // The screen opened: the microphone records (meter, tests) and the recognizer reports here.
    public void Open()
    {
        if (open) return;
        open = true;
        MicrophoneInput.Acquire(settings != null ? settings.MicrophoneDevice : "");
        MicrophoneInput.TestOpen = true;
        MicrophoneInput.TestHeard += OnHeard;
        heard = "";
        heardAnything = false;
    }

    // The screen closed: back to normal (what is said goes to Helena again).
    public void Close()
    {
        if (!open) return;
        open = false;
        MicrophoneInput.TestHeard -= OnHeard;
        MicrophoneInput.TestOpen = false;
        MicrophoneInput.Release();
        StopPlayback();
        DropRecording();
        stage = Stage.Idle;
    }

    // The next microphone: system default, then each device, then the default again.
    public void NextDevice()
    {
        if (settings == null) return;
        string[] devices = Microphone.devices;
        string current = settings.MicrophoneDevice;
        int i = string.IsNullOrEmpty(current) ? -1 : Array.IndexOf(devices, current);
        string next = i + 1 < devices.Length ? devices[i + 1] : "";
        settings.SetMicrophoneDevice(next);
        MicrophoneInput.Use(next);
    }

    // The chosen microphone's name for the screen.
    public string DeviceLabel()
    {
        if (!MicrophoneInput.HasDevice) return T("No microphone found", "Nenhum microfone encontrado");
        string current = settings != null ? settings.MicrophoneDevice : "";
        if (string.IsNullOrEmpty(current)) return T("System default", "Padrão do sistema");
        return MicrophoneInput.WantedMissing ? current + T(" (unplugged: using the default)", " (desconectado: usando o padrão)") : current;
    }

    // "Record and listen": records 3 s, then plays them back.
    public void RecordAndListen()
    {
        if (!open || MicrophoneInput.Clip == null || stage != Stage.Idle) return;
        stage = Stage.Recording;
        timer = RecordSeconds;
    }

    // Plays a short sound on the output (the system's).
    public void PlayTestSound() => OperatorSounds.Play(source, OperatorSounds.Kind.Accepted, 0.9f);

    // Each frame while open: the recording / playback test.
    public void Tick(float dt)
    {
        if (!open) return;
        switch (stage)
        {
            case Stage.Recording:
                timer -= dt;
                if (timer > 0f) break;
                StopPlayback();
                DropRecording();
                recording = MicrophoneInput.Snapshot(RecordSeconds);
                if (recording == null || source == null) { stage = Stage.Idle; break; }
                source.PlayOneShot(recording);
                stage = Stage.Playing;
                timer = recording.length + 0.1f;
                break;
            case Stage.Playing:
                timer -= dt;
                if (timer <= 0f) stage = Stage.Idle;
                break;
        }
    }

    // The record test's button text.
    public string RecordLabel()
    {
        switch (stage)
        {
            case Stage.Recording: return T($"Recording... {Mathf.CeilToInt(timer)}", $"Gravando... {Mathf.CeilToInt(timer)}");
            case Stage.Playing: return T("Playing back...", "Reproduzindo...");
            default: return T("Record 3 s and listen", "Gravar 3 s e ouvir");
        }
    }

    // The recognition test line: what to do, or what the game understood.
    public string HeardLabel()
    {
        if (!heardAnything) return T("Hold V and speak a command: what the game understands shows here.",
                                     "Segure V e fale um comando: o que o jogo entender aparece aqui.");
        return heard.Length > 0 ? T("Understood: ", "Entendido: ") + "\"" + heard + "\""
                                : T("Heard something, but no command words.", "Ouviu algo, mas nenhuma palavra de comando.");
    }

    // Where the sound goes, and how to send just the game elsewhere.
    public string OutputLabel() => T("System output (Windows). To use other speakers / headphones only for the game: Windows Settings > System > Sound > Volume mixer > Last Terminal.",
                                     "Saída do sistema (Windows). Para usar outra caixa de som / fone só no jogo: Configurações do Windows > Sistema > Som > Mixer de volume > Last Terminal.");

    // A meter as text (the cameras' menu): ten bars.
    public string LevelBars()
    {
        int n = Mathf.RoundToInt(Level * 10f);
        return "<mspace=0.6em>" + new string('|', n) + "<alpha=#33>" + new string('|', 10 - n) + "</mspace>";
    }

    // The recognizer finished a sentence during the test.
    private void OnHeard(string text)
    {
        heardAnything = true;
        heard = text;
    }

    // Frees the last recording.
    private void DropRecording()
    {
        if (recording != null) UnityEngine.Object.Destroy(recording);
        recording = null;
    }

    // Stops the playback.
    private void StopPlayback()
    {
        if (source != null && stage == Stage.Playing) source.Stop();
    }
}
