using UnityEngine;

// The water cooler of his room: just a pastime. Used: a paper cup fills (glug), a sip, a thought. Counted in the
// story ("water": flags water_1, water_5...).
public class WaterCooler : OperatorInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string AudioField = nameof(audioSource);

    [Tooltip("On the cooler (3D): the glug.")]
    [SerializeField] private AudioSource audioSource;
    // Time between the glug and the sip (s).
    [SerializeField] private float sipDelay = 1.3f;
    // His thoughts after a sip, per language.
    [SerializeField] private string[] thoughtsEn =
    {
        "Warm. Tastes like plastic.",
        "At least the water is real.",
        "The jug is almost empty. Who changes it?",
        "...better than nothing.",
    };
    // His thoughts in Portuguese.
    [SerializeField] private string[] thoughtsPt =
    {
        "Morna. Gosto de plástico.",
        "Pelo menos a água é de verdade.",
        "O galão tá quase vazio. Quem troca isso?",
        "...melhor que nada.",
    };

    // The player drinking, time left before the sip, next thought.
    private FirstPersonPlayer drinker;
    private float timer;
    private int next;

    // He grabs the cup.
    public override HandMotion Motion => HandMotion.Grab;

    // Label (with "..." while the cup fills).
    public override string Label(GameLanguage language) => timer > 0f ? Pick(language, "Water cooler...", "Bebedouro...") : Pick(language, "Water cooler", "Bebedouro");

    // Starts filling the cup.
    public override void Interact(FirstPersonPlayer player)
    {
        if (timer > 0f) return;
        drinker = player;
        timer = sipDelay;
        OperatorSounds.Play(audioSource != null ? audioSource : player.Audio, OperatorSounds.Kind.Glug, 0.8f);
    }

    // The sip: sound, hand, thought, and the story counter.
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
