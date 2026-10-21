using UnityEngine;

// The coffee machine of his room (free, at least that): used, it brews a cup (sound, the opening lights up); used
// again when it's ready, he takes the cup (FirstPersonPlayer: left button on nothing = a sip). One cup at a time.
public class CoffeeMachine : OperatorInteractable
{
    public const string CupOnTrayField = nameof(cupOnTray);
    public const string GlowField = nameof(glow);
    public const string AudioField = nameof(audioSource);

    private enum State { Idle, Brewing, Ready }

    [Tooltip("The cup that appears in the opening when it's done.")]
    [SerializeField] private GameObject cupOnTray;
    [Tooltip("Light in the opening (on while brewing / ready).")]
    [SerializeField] private Light glow;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float brewTime = 3.4f;

    private State state;
    private float timer;
    private float glowIntensity;

    public override HandMotion Motion => state == State.Ready ? HandMotion.Grab : HandMotion.Push;

    public override string Label(GameLanguage language)
    {
        switch (state)
        {
            case State.Brewing: return Pick(language, "Coffee (brewing...)", "Café (preparando...)");
            case State.Ready: return Pick(language, "Take the coffee", "Pegar o café");
            default: return Pick(language, "Coffee machine", "Máquina de café");
        }
    }

    private void Awake()
    {
        if (cupOnTray != null) cupOnTray.SetActive(false);
        if (glow != null) { glowIntensity = glow.intensity; glow.intensity = 0f; }
    }

    public override void Interact(FirstPersonPlayer player)
    {
        GameLanguage l = player.Language;
        switch (state)
        {
            case State.Idle:
                if (player.HasCup) { player.Hud.ShowMessage(Pick(l, "One coffee at a time.", "Um café de cada vez.")); return; }
                state = State.Brewing;
                timer = brewTime;
                OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Beep, 0.6f);
                if (audioSource != null) audioSource.PlayOneShot(OperatorSounds.Get(OperatorSounds.Kind.Brew));
                player.Hud.ShowMessage(Pick(l, "Black, no sugar.", "Preto, sem açúcar."));
                break;
            case State.Brewing:
                player.Hud.ShowMessage(Pick(l, "Almost...", "Quase..."));
                break;
            case State.Ready:
                if (player.HasCup) { player.Hud.ShowMessage(Pick(l, "Finish the other one first.", "Termina o outro primeiro.")); return; }
                state = State.Idle;
                if (cupOnTray != null) cupOnTray.SetActive(false);
                player.GiveCup();
                player.Hud.ShowMessage(Pick(l, "Hot. [left click: drink]", "Quente. [clique: beber]"));
                break;
        }
    }

    private void Update()
    {
        if (state == State.Brewing)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                state = State.Ready;
                if (cupOnTray != null) cupOnTray.SetActive(true);
                if (audioSource != null) audioSource.PlayOneShot(OperatorSounds.Get(OperatorSounds.Kind.Beep), 0.6f);
            }
        }
        if (glow == null) return;
        float target = state == State.Brewing ? glowIntensity * (0.75f + 0.25f * Mathf.Sin(Time.time * 9f)) : state == State.Ready ? glowIntensity : 0f;
        glow.intensity = Mathf.MoveTowards(glow.intensity, target, Time.deltaTime * glowIntensity * 4f);
    }
}
