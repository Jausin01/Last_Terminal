using UnityEngine;

// A failing ceiling lamp: now and then a burst of quick dips (the light and its glowing fixture), sometimes a long
// blackout. Unscaled time: it keeps flickering while the game is paused behind a panel.
public class LightFlicker : MonoBehaviour
{
    public const string LampField = nameof(lamp);
    public const string FixtureField = nameof(fixture);

    [SerializeField] private Light lamp;
    [Tooltip("Optional: the glowing panel of the lamp (its emission follows the light).")]
    [SerializeField] private Renderer fixture;
    [Tooltip("Seconds between bursts (random in this range).")]
    [SerializeField] private Vector2 calm = new Vector2(1.5f, 6f);
    [Tooltip("Length of a burst (s).")]
    [SerializeField] private Vector2 burst = new Vector2(0.15f, 0.8f);
    [Tooltip("Chance that a burst ends in a short blackout.")]
    [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float lowest = 0.05f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private float baseIntensity;
    private Color baseEmission;
    private MaterialPropertyBlock block;
    private float timer;
    private float burstLeft;
    private float blackoutLeft;
    private float level = 1f;

    private void Awake()
    {
        if (lamp == null) { enabled = false; return; }
        baseIntensity = lamp.intensity;
        if (fixture != null && fixture.sharedMaterial != null && fixture.sharedMaterial.HasProperty(EmissionId))
        {
            baseEmission = fixture.sharedMaterial.GetColor(EmissionId);
            block = new MaterialPropertyBlock();
        }
        timer = Random.Range(calm.x, calm.y);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (blackoutLeft > 0f)
        {
            blackoutLeft -= dt;
            Set(lowest);
            return;
        }
        if (burstLeft > 0f)
        {
            burstLeft -= dt;
            // a new random level a few times per second, mostly dark-ish
            if (Random.value < dt * 25f) level = Random.value < 0.5f ? Random.Range(lowest, 0.4f) : Random.Range(0.7f, 1f);
            Set(level);
            if (burstLeft <= 0f)
            {
                if (Random.value < blackoutChance) blackoutLeft = Random.Range(0.3f, 1.5f);
                timer = Random.Range(calm.x, calm.y);
            }
            return;
        }
        Set(1f);
        timer -= dt;
        if (timer <= 0f) burstLeft = Random.Range(burst.x, burst.y);
    }

    private void Set(float k)
    {
        lamp.intensity = baseIntensity * k;
        if (block == null) return;
        fixture.GetPropertyBlock(block);
        block.SetColor(EmissionId, baseEmission * k);
        fixture.SetPropertyBlock(block);
    }
}
