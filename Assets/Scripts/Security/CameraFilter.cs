using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Optional "real CCTV" look (settings): a post-processing Volume (grain, vignette, color fringes, lens bulge,
// washed-out colors) and a scanline / noise overlay on the HUD. Strength scales both.
public class CameraFilter : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SettingsField = nameof(settings);
    public const string VolumeField = nameof(volume);
    public const string ScanlinesField = nameof(scanlines);
    public const string NoiseField = nameof(noise);

    // The settings (on / strength), the post-processing volume and the HUD overlays.
    [SerializeField] private CameraSettings settings;
    [SerializeField] private Volume volume;
    [SerializeField] private RawImage scanlines;
    [SerializeField] private RawImage noise;
    // Overlay opacity at full strength.
    [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float noiseAlpha = 0.06f;

    // Follows settings changes.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Apply;
    }

    // Stops following.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Apply;
    }

    // Applies the current settings.
    private void Start() => Apply();

    // Scrolls the noise overlay.
    private void Update()
    {
        // moving grain on the overlay
        if (noise != null && noise.enabled) noise.uvRect = new Rect(Random.value, Random.value, noise.uvRect.width, noise.uvRect.height);
    }

    // Volume weight and overlay alpha from the settings.
    private void Apply()
    {
        if (settings == null) return;
        float k = settings.FilterEnabled ? settings.FilterStrength : 0f;
        if (volume != null) volume.weight = k;
        SetAlpha(scanlines, scanlineAlpha * k);
        SetAlpha(noise, noiseAlpha * k);
    }

    // Sets a RawImage's alpha (hidden at 0).
    private static void SetAlpha(RawImage image, float alpha)
    {
        if (image == null) return;
        image.enabled = alpha > 0.001f;
        Color c = image.color; c.a = alpha; image.color = c;
    }
}
