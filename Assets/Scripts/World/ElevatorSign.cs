using TMPro;
using UnityEngine;

// The sign above an elevator: its number, where it goes (from the destination's ElevatorStop, in the game's
// language) and a stripe in the color of the key it needs (none = grey). Edit the stop asset to change the text.
public class ElevatorSign : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ElevatorField = nameof(elevator);
    public const string TextField = nameof(text);
    public const string StripeField = nameof(stripe);
    public const string SettingsField = nameof(settings);

    // The elevator, the sign text and the key-color stripe.
    [SerializeField] private Elevator elevator;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Renderer stripe;
    [Tooltip("Optional: the language of the sign follows the settings.")]
    [SerializeField] private CameraSettings settings;

    // Shader ids.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Follows language changes.
    private void OnEnable()
    {
        if (settings != null) settings.Changed += Refresh;
        Refresh();
    }

    // Stops following.
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
    }

    // Number, destination name and key color.
    private void Refresh()
    {
        if (elevator == null) return;
        GameLanguage lang = settings != null ? settings.Language : GameLanguage.English;
        ElevatorStop to = elevator.Destination;
        string where = to != null ? to.NameIn(lang) : "---";
        string floor = to != null && !string.IsNullOrEmpty(to.Floor) ? to.Floor : "";
        if (text != null) text.text = $"<size=70%>{elevator.Number}</size>  {where.ToUpperInvariant()}  <size=60%>{floor}</size>";
        if (stripe != null)
        {
            ElevatorKey key = elevator.RequiredKey;
            Color c = key != null ? key.Color : new Color(0.35f, 0.35f, 0.35f);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionColorId, c * 0.6f);
            stripe.SetPropertyBlock(block);
        }
    }
}
