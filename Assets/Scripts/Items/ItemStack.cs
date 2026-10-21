using System;
using UnityEngine;

// One inventory slot: an item type and how many units are in it.
[Serializable]
public class ItemStack
{
    public const string ItemField = nameof(item);
    public const string AmountField = nameof(amount);

    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemDefinition Item => item;
    public int Amount => amount;
    public int Space => item.MaxStack - amount;

    public ItemStack(ItemDefinition item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void Add(int count) => amount += count;
    public void Remove(int count) => amount -= count;
}
