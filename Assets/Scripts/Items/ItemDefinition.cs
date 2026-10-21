using UnityEngine;

// Data for one item type. One asset per item (Create > Last Terminal > Items > ...).
// Runtime state (how many she carries) lives in Inventory, never in this asset.
[CreateAssetMenu(menuName = "Last Terminal/Items/Generic Item", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
{
    public const string IdField = nameof(id);
    public const string DisplayNameField = nameof(displayName);
    public const string MaxStackField = nameof(maxStack);

    [Tooltip("Stable id used by voice commands and saves (e.g. item_medkit). Don't change it once used.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("How many fit in one inventory slot.")]
    [SerializeField, Min(1)] private int maxStack = 1;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int MaxStack => maxStack;

    // Items that do something when used ("use the medkit") override these.
    public virtual bool IsUsable => false;

    // Applies the effect on the user. Returns true when the item was spent (Inventory removes 1).
    public virtual bool TryUse(GameObject user) => false;
}
