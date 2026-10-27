using UnityEngine;

// Every item of the game, so saves can store item ids and get the assets back.
[CreateAssetMenu(menuName = "Last Terminal/Items/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ItemsField = nameof(items);

    // Every item asset of the game (filled by the builders).
    [SerializeField] private ItemDefinition[] items = new ItemDefinition[0];

    // The item with this id (null = unknown).
    public ItemDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (ItemDefinition item in items)
            if (item != null && item.Id == id) return item;
        return null;
    }
}
