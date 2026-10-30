using System;
using UnityEngine;

// The sounds the game synthesizes in code (AudioClip.Create: slot machines, roulette, keypads, elevators, the PC)
// are lost when Unity restarts its audio, which happens when the sound output changes (Windows' default device, or
// the device Windows' Volume mixer gives the game, headphones plugged in...). Reset is raised then: whoever made
// clips makes them again.
public static class GeneratedAudio
{
    // Raised after Unity's audio restarted: clips made by code must be made again.
    public static event Action Reset;

    // Hooks Unity's audio configuration callback (once per play, also with domain reload off in the editor).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Reset = null;
        AudioSettings.OnAudioConfigurationChanged -= OnConfigurationChanged;
        AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
    }

    // Unity reloaded its audio (deviceWasChanged: another output device).
    private static void OnConfigurationChanged(bool deviceWasChanged)
    {
        if (deviceWasChanged) Debug.Log("[Audio] The sound output changed: generated sounds are made again.");
        Reset?.Invoke();
    }
}
