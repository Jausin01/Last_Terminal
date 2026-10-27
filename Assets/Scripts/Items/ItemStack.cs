using System;
using UnityEngine;

// One inventory slot: an item type and how many units are in it.
[Serializable]
public class ItemStack
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ItemField = nameof(item);
    public const string AmountField = nameof(amount);

    // The item type and how many units.
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    // Read by the inventory and the HUD; Space = how many more fit.
    public ItemDefinition Item => item;
    public int Amount => amount;
    public int Space => item.MaxStack - amount;

    public ItemStack(ItemDefinition item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    // Changes the count (the inventory checks the limits).
    public void Add(int count) => amount += count;
    public void Remove(int count) => amount -= count;
}
