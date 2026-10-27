using UnityEngine;

// Keycards, keys, story items. Doors and other locks reference the KeyItem they require.
[CreateAssetMenu(menuName = "Last Terminal/Items/Key Item", fileName = "NewKeyItem")]
public class KeyItem : ItemDefinition
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ConsumeOnUseField = nameof(consumeOnUse);

    [Tooltip("Removed from the inventory after opening its lock.")]
    [SerializeField] private bool consumeOnUse = true;

    // Read by the doors.
    public bool ConsumeOnUse => consumeOnUse;
}
