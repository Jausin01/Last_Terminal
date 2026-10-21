using UnityEngine;

// Keycards, keys, story items. Doors and other locks reference the KeyItem they require.
[CreateAssetMenu(menuName = "Last Terminal/Items/Key Item", fileName = "NewKeyItem")]
public class KeyItem : ItemDefinition
{
    public const string ConsumeOnUseField = nameof(consumeOnUse);

    [Tooltip("Removed from the inventory after opening its lock.")]
    [SerializeField] private bool consumeOnUse = true;

    public bool ConsumeOnUse => consumeOnUse;
}
