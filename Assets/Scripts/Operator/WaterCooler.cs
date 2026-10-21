using UnityEngine;

// The water cooler of his room: just a pastime. Used: a paper cup fills (glug), a sip, a thought. Counted in the
// story ("water": flags water_1, water_5...).
public class WaterCooler : OperatorInteractable
{
    public const string AudioField = nameof(audioSource);

    [Tooltip("On the cooler (3D): the glug.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float sipDelay = 1.3f;
    [SerializeField] private string[] thoughtsEn =
    {
        "Warm. Tastes like plastic.",
        "At least the water is real.",
        "The jug is almost empty. Who changes it?",
        "...better than nothing.",
    };
    [SerializeField] private string[] thoughtsPt =
    {
        "Morna. Gosto de plástico.",
        "Pelo menos a água é de verdade.",
        "O galão tá quase vazio. Quem troca isso?",
        "...melhor que nada.",
    };

    private FirstPersonPlayer drinker;
    private float timer;
    private int next;

    public override HandMotion Motion => HandMotion.Grab;

    public override string Label(GameLanguage language) => timer > 0f ? Pick(language, "Water cooler...", "Bebedouro...") : Pick(language, "Water cooler", "Bebedouro");

    public override void Interact(FirstPersonPlayer player)
    {
        if (timer > 0f) return;
        drinker = player;
        timer = sipDelay;
        OperatorSounds.Play(audioSource != null ? audioSource : player.Audio, OperatorSounds.Kind.Glug, 0.8f);
    }

    private void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer > 0f || drinker == null) return;
        OperatorSounds.Play(drinker.Audio, OperatorSounds.Kind.Sip, 0.9f);
        drinker.PlayHand(HandMotion.Grab);
        string[] lines = drinker.Language == GameLanguage.Portuguese ? thoughtsPt : thoughtsEn;
        if (lines.Length > 0) drinker.Hud.ShowThought(lines[next++ % lines.Length]);
        if (drinker.Story != null) drinker.Story.Add("water");
        drinker = null;
    }
}
