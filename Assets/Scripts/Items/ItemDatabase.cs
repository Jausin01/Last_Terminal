using UnityEngine;

// Every item of the game, so saves can store item ids and get the assets back.
[CreateAssetMenu(menuName = "Last Terminal/Items/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public const string ItemsField = nameof(items);

    [SerializeField] private ItemDefinition[] items = new ItemDefinition[0];

    public ItemDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (ItemDefinition item in items)
            if (item != null && item.Id == id) return item;
        return null;
    }
}
