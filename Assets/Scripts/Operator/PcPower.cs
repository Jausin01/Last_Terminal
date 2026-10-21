using System;
using UnityEngine;

// The PC case of his room: the power button. Off the first time (red light); pressed, it starts (green light, the
// fan hums, the startup jingle plays, the monitor wakes up) and the OS boots (OperatorOS listens to PoweredOn).
// The OS's "Shut down" turns it off again (OperatorStation). Sitting at a PC that is off only gets a thought.
public class PcPower : OperatorInteractable
{
    public const string LedField = nameof(led);
    public const string LedLightField = nameof(ledLight);
    public const string ScreenField = nameof(screen);
    public const string ScreenOffField = nameof(screenOff);
    public const string ScreenOnField = nameof(screenOn);
    public const string ScreenGlowField = nameof(screenGlow);
    public const string HumField = nameof(hum);
    public const string JingleSourceField = nameof(jingleSource);
    public const string StartupClipField = nameof(startupClip);
    public const string ShutdownClipField = nameof(shutdownClip);
    public const string StartOnField = nameof(startOn);

    public enum State { Off, Booting, On }

    [Header("Lights")]
    [Tooltip("The little light on the case (its material glows red / green).")]
    [SerializeField] private Renderer led;
    [SerializeField] private Light ledLight;
    [SerializeField] private Color onColor = new Color(0.15f, 1f, 0.25f);
    [SerializeField] private Color offColor = new Color(1f, 0.1f, 0.06f);
    [SerializeField] private float ledGlow = 3f;

    [Header("Monitor")]
    [Tooltip("The quad over the monitor's screen.")]
    [SerializeField] private Renderer screen;
    [SerializeField] private Material screenOff;
    [SerializeField] private Material screenOn;
    [Tooltip("Optional: the light of the screen on the desk (on while powered).")]
    [SerializeField] private Light screenGlow;

    [Header("Sound")]
    [Tooltip("The fan (loops while on).")]
    [SerializeField] private AudioSource hum;
    [Tooltip("Plays the startup jingle (on the case).")]
    [SerializeField] private AudioSource jingleSource;
    [SerializeField] private AudioClip startupClip;
    [SerializeField] private AudioClip shutdownClip;
    [Tooltip("Seconds from the button to the jingle (with the OS logo: OperatorOS Bios Seconds).")]
    [SerializeField] private float jingleDelay = 3f;
    [Tooltip("Seconds from the button to the BIOS beep.")]
    [SerializeField] private float beepDelay = 0.8f;

    [Header("Start")]
    [Tooltip("On when the scene starts (off = he has to press the button the first time).")]
    [SerializeField] private bool startOn;
    [SerializeField] private float bootSeconds = 7.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private State state;
    private float timer;
    private bool jinglePending, beepPending;
    private float humVolume = 1f;

    public State Current => state;
    public bool IsPowered => state != State.Off;

    public event Action PoweredOn;
    public event Action PoweredOff;

    public override HandMotion Motion => HandMotion.Push;

    public override string Label(GameLanguage language)
    {
        switch (state)
        {
            case State.Off: return Pick(language, "PC case (off)", "Gabinete (desligado)");
            case State.Booting: return Pick(language, "PC case (starting...)", "Gabinete (ligando...)");
            default: return Pick(language, "PC case", "Gabinete");
        }
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (hum != null) { humVolume = hum.volume; hum.loop = true; hum.playOnAwake = false; }
    }

    private void Start()
    {
        if (startOn)
        {
            state = State.On;
            if (hum != null) hum.Play();
            PoweredOn?.Invoke();
        }
        Refresh();
    }

    public override void Interact(FirstPersonPlayer player)
    {
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.PowerButton, 0.9f);
        GameLanguage l = player.Language;
        switch (state)
        {
            case State.Off:
                PowerOn();
                player.Hud.ShowThought(Pick(l, "Still works. Let's see what's in it.", "Ainda funciona. Vamos ver o que tem aí."));
                if (player.Story != null) player.Story.Set("pc_on");
                break;
            case State.Booting:
                player.Hud.ShowThought(Pick(l, "It's starting. Give it a second.", "Tá ligando. Calma."));
                break;
            default:
                player.Hud.ShowThought(Pick(l, "Better not pull the plug. Shut it down from the screen.", "Melhor não puxar da tomada. Desligo pela tela."));
                break;
        }
    }

    public void PowerOn()
    {
        if (state != State.Off) return;
        state = State.Booting;
        timer = bootSeconds;
        jinglePending = true;
        beepPending = true;
        if (hum != null) { hum.volume = 0f; hum.Play(); }
        Refresh();
        PoweredOn?.Invoke();
    }

    public void PowerOff()
    {
        if (state == State.Off) return;
        state = State.Off;
        jinglePending = false;
        if (jingleSource != null)
        {
            jingleSource.Stop();
            if (shutdownClip != null) jingleSource.PlayOneShot(shutdownClip);
        }
        Refresh();
        PoweredOff?.Invoke();
    }

    private void Refresh()
    {
        bool on = state != State.Off;
        Color c = on ? onColor : offColor;
        if (led != null)
        {
            led.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * ledGlow);
            led.SetPropertyBlock(block);
        }
        if (ledLight != null) ledLight.color = c;
        if (screenGlow != null) screenGlow.enabled = on;
        if (screen != null)
        {
            Material m = on ? screenOn : screenOff;
            if (m != null) screen.sharedMaterial = m;
        }
    }

    private void Update()
    {
        if (hum != null)
        {
            float target = state == State.Off ? 0f : humVolume;
            hum.volume = Mathf.MoveTowards(hum.volume, target, Time.deltaTime * humVolume * (state == State.Off ? 1.2f : 0.6f));
            if (state == State.Off && hum.isPlaying && hum.volume <= 0f) hum.Stop();
        }
        if (state != State.Booting) return;
        timer -= Time.deltaTime;
        if (beepPending && timer <= bootSeconds - beepDelay)
        {
            beepPending = false;
            if (jingleSource != null) jingleSource.PlayOneShot(OperatorSounds.Get(OperatorSounds.Kind.Beep), 0.5f);
        }
        if (jinglePending && timer <= bootSeconds - jingleDelay)
        {
            jinglePending = false;
            if (jingleSource != null && startupClip != null) jingleSource.PlayOneShot(startupClip);
        }
        if (timer <= 0f) state = State.On;
    }
}
