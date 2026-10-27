using UnityEngine;

// An old fluorescent tube: steady most of the time, now and then it stutters (the light and the tube's glow).
public class OperatorLightFlicker : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LampField = nameof(lamp);
    public const string TubeField = nameof(tube);

    // The light that flickers.
    [SerializeField] private Light lamp;
    [Tooltip("Optional: the tube (its emission follows the light).")]
    [SerializeField] private Renderer tube;
    // Emission color of the tube at full light.
    [SerializeField] private Color tubeColor = new Color(0.85f, 0.95f, 1f);
    // How long it stays steady / stutters (random range, s).
    [SerializeField] private Vector2 steadyTime = new Vector2(3f, 9f);
    [SerializeField] private Vector2 stutterTime = new Vector2(0.2f, 0.9f);

    // Emission property, property block, the light's normal intensity, the phase timer and phase.
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private float baseIntensity;
    private float timer;
    private bool stuttering;

    // Remembers the intensity; starts steady.
    private void Awake()
    {
        if (lamp == null) { enabled = false; return; }
        block = new MaterialPropertyBlock();
        baseIntensity = lamp.intensity;
        timer = Random.Range(steadyTime.x, steadyTime.y);
    }

    // Alternates steady / stutter phases; random dips while stuttering.
    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            stuttering = !stuttering;
            timer = stuttering ? Random.Range(stutterTime.x, stutterTime.y) : Random.Range(steadyTime.x, steadyTime.y);
        }
        float k = stuttering ? (Random.value < 0.5f ? Random.Range(0.05f, 0.4f) : 1f) : 1f;
        lamp.intensity = baseIntensity * k;
        if (tube == null) return;
        block.SetColor(EmissionId, tubeColor * (1.5f * k));
        tube.SetPropertyBlock(block);
    }
}
