using System;
using System.Collections.Generic;
using UnityEngine;

// Slot-based inventory (classic Resident Evil style): each slot holds one item type up to its MaxStack.
// HUD, voice feedback and weapons listen to Changed instead of polling.
public class Inventory : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string SlotCountField = nameof(slotCount);
    public const string StartingItemsField = nameof(startingItems);

    // Number of slots, and what she carries at the start of a new game.
    [SerializeField, Min(1)] private int slotCount = 8;
    [SerializeField] private List<ItemStack> startingItems = new List<ItemStack>();

    // The slots in use.
    private readonly List<ItemStack> slots = new List<ItemStack>();

    // Raised whenever the contents change.
    public event Action Changed;

    // Read by the HUD and the save system.
    public int SlotCount => slotCount;
    public int UsedSlots => slots.Count;
    public IReadOnlyList<ItemStack> Slots => slots;

    // Fills the starting items.
    private void Awake()
    {
        foreach (ItemStack s in startingItems)
            if (s.Item != null) TryAdd(s.Item, s.Amount);
    }

    // Adds as many units as fit (existing stacks first, then free slots). Returns how many were added.
    public int TryAdd(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return 0;

        int remaining = amount;
        foreach (ItemStack s in slots)
        {
            if (s.Item != item) continue;
            int n = Mathf.Min(s.Space, remaining);
            if (n <= 0) continue;
            s.Add(n);
            remaining -= n;
            if (remaining == 0) break;
        }
        while (remaining > 0 && slots.Count < slotCount)
        {
            int n = Mathf.Min(item.MaxStack, remaining);
            slots.Add(new ItemStack(item, n));
            remaining -= n;
        }

        int added = amount - remaining;
        if (added > 0) Changed?.Invoke();
        return added;
    }

    // Empties every slot (loading a save).
    public void Clear()
    {
        slots.Clear();
        Changed?.Invoke();
    }

    // Total units of an item across every slot.
    public int Count(ItemDefinition item)
    {
        int total = 0;
        foreach (ItemStack s in slots)
            if (s.Item == item) total += s.Amount;
        return total;
    }

    // Does she carry at least this many?
    public bool Has(ItemDefinition item, int amount = 1) => item != null && Count(item) >= amount;

    // All or nothing: removes only if she has enough.
    public bool TryRemove(ItemDefinition item, int amount)
    {
        if (amount <= 0 || !Has(item, amount)) return false;

        for (int i = slots.Count - 1; i >= 0 && amount > 0; i--)
        {
            ItemStack s = slots[i];
            if (s.Item != item) continue;
            int n = Mathf.Min(s.Amount, amount);
            s.Remove(n);
            amount -= n;
            if (s.Amount == 0) slots.RemoveAt(i);
        }
        Changed?.Invoke();
        return true;
    }

    // Uses one unit on this GameObject (e.g. a HealingItem heals its Health). False if nothing happened.
    public bool TryUse(ItemDefinition item)
    {
        if (item == null || !item.IsUsable || !Has(item)) return false;
        if (!item.TryUse(gameObject)) return false;
        TryRemove(item, 1);
        return true;
    }

    public T FindFirst<T>() where T : ItemDefinition
    {
        foreach (ItemStack s in slots)
            if (s.Item is T match) return match;
        return null;
    }

    // For voice commands: the lexicon resolves words to an item id.
    public ItemDefinition FindById(string itemId)
    {
        foreach (ItemStack s in slots)
            if (s.Item.Id == itemId) return s.Item;
        return null;
    }
}
