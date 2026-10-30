using UnityEngine;

// Dance floor lights: spots that sweep back and forth over the floor, their colors cycling, and the floor's glow
// pulsing with them (its material's emission). Set up by the scene builder.
public class DanceLights : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LightsField = nameof(lights);
    public const string FloorField = nameof(floor);

    [SerializeField] private Light[] lights = new Light[0];
    [Tooltip("Optional: the dance floor (its material's emission pulses).")]
    [SerializeField] private Renderer floor;
    [Tooltip("How far the spots swing (degrees) and how fast (cycles per second).")]
    [SerializeField] private float swing = 22f;
    [SerializeField] private float rate = 0.23f;
    [Tooltip("Hue cycles per second.")]
    [SerializeField] private float hueRate = 0.07f;
    [SerializeField] private float beatsPerMinute = 124f;

    // Their rest rotations; the floor's emission color and the block that changes it.
    private Quaternion[] rest;
    private Color floorGlow;
    private MaterialPropertyBlock block;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    // Remembers the rest poses and the floor's glow.
    private void Start()
    {
        rest = new Quaternion[lights.Length];
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) rest[i] = lights[i].transform.localRotation;
        if (floor != null && floor.sharedMaterial != null && floor.sharedMaterial.HasProperty(EmissionId))
        {
            floorGlow = floor.sharedMaterial.GetColor(EmissionId);
            block = new MaterialPropertyBlock();
        }
    }

    // Swings, colors, the pulse on the beat.
    private void Update()
    {
        float t = Time.time;
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null) continue;
            float ph = i * 2.1f;
            l.transform.localRotation = rest[i] * Quaternion.Euler(Mathf.Sin(t * rate * 6.283f + ph) * swing, Mathf.Cos(t * rate * 4.1f + ph) * swing, 0f);
            l.color = Color.HSVToRGB(Mathf.Repeat(t * hueRate + i / (float)lights.Length, 1f), 0.75f, 1f);
        }
        if (block == null) return;
        float beat = Mathf.Repeat(t * beatsPerMinute / 60f, 1f);
        float pulse = 0.55f + 0.45f * Mathf.Exp(-beat * 5f);
        block.SetColor(EmissionId, floorGlow * pulse);
        floor.SetPropertyBlock(block);
    }
}
