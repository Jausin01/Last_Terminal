using TMPro;
using UnityEngine;

// A prize locker of the casino's gallery (like the coin cages of a survival horror): a glass case with the prize
// inside (her outfit on a mannequin of her), its name and price in coins on the plate. Bought (PrizeCounter.Buy):
// the glass door swings open and the plate says it's hers. She buys it standing in front of it (Spot).
public class PrizeCase : MonoBehaviour
{
    public const string PrizeField = nameof(prize);
    public const string DoorField = nameof(door);
    public const string PlateField = nameof(plate);
    public const string SpotField = nameof(spot);
    public const string OwnerField = nameof(owner);
    public const string SettingsField = nameof(settings);

    [SerializeField] private OutfitDefinition prize;
    [Tooltip("The glass door (turns around its hinge, this transform's up).")]
    [SerializeField] private Transform door;
    [SerializeField] private TMP_Text plate;
    [Tooltip("Where she stands to buy it.")]
    [SerializeField] private Transform spot;
    [Tooltip("Her outfits: what she owns.")]
    [SerializeField] private PartnerOutfit owner;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private float openAngle = -105f;
    [SerializeField] private float openSpeed = 90f;   // deg / s

    private Quaternion closedRotation;
    private float angle;

    public OutfitDefinition Prize => prize;
    public Transform Spot => spot;
    private bool Owned => owner != null && owner.IsOwned(prize);

    private void Awake()
    {
        if (door != null) closedRotation = door.localRotation;
    }

    private void OnEnable()
    {
        if (owner != null) owner.OwnedChanged += Refresh;
        if (settings != null) settings.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (owner != null) owner.OwnedChanged -= Refresh;
        if (settings != null) settings.Changed -= Refresh;
    }

    private void Start()
    {
        Refresh();
        angle = Owned ? openAngle : 0f;   // a save loaded: already open, no swing
        ApplyDoor();
    }

    private void Refresh()
    {
        if (plate == null || prize == null) return;
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        string name = prize.DisplayName(pt ? GameLanguage.Portuguese : GameLanguage.English).ToUpperInvariant();
        plate.text = Owned
            ? $"{name}\n<color=#7CFF8A>{(pt ? "COMPRADO" : "OWNED")}</color>"
            : $"{name}\n<color=#FFD24A>{prize.Price} {(pt ? "MOEDAS" : "COINS")}</color>";
    }

    private void Update()
    {
        float target = Owned ? openAngle : 0f;
        if (Mathf.Approximately(angle, target)) return;
        angle = Mathf.MoveTowards(angle, target, openSpeed * Time.deltaTime);
        ApplyDoor();
    }

    private void ApplyDoor()
    {
        if (door != null) door.localRotation = closedRotation * Quaternion.AngleAxis(angle, Vector3.up);
    }
}
