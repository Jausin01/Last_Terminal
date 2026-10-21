using UnityEngine;

// A colored elevator key (the plastic cover on a key, like the ones in the casino's elevator hall).
// Each ElevatorStop says which key (if any) takes you there; the key model is tinted with Color.
[CreateAssetMenu(menuName = "Last Terminal/Items/Elevator Key", fileName = "Key_Color")]
public class ElevatorKey : KeyItem
{
    public const string ColorField = nameof(color);
    public const string ColorNameField = nameof(colorName);
    public const string ColorNamePtField = nameof(colorNamePt);

    [SerializeField] private Color color = Color.red;
    [Tooltip("The color as she says it (\"red\" / \"vermelha\"), also the lexicon key: color_<english name>.")]
    [SerializeField] private string colorName = "red";
    [SerializeField] private string colorNamePt = "vermelha";

    public Color Color => color;
    public string ColorName => colorName;
    public string ColorNamePt => colorNamePt;
    public string LexiconKey => "color_" + colorName.ToLowerInvariant();
}
