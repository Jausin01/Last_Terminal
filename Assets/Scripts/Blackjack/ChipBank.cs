using System;
using UnityEngine;

// The operator's money (kept in the save files, carried between scenes):
// chips to play with (every table and machine) and prize coins (won at the cashier, spent on prizes).
public class ChipBank : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string StartingChipsField = nameof(startingChips);

    // Chips the operator has at the start of a new game (a save file replaces it).
    [SerializeField, Min(0)] private int startingChips = 1000;

    // Current chips and prize coins.
    private int chips;
    private int coins;

    public event Action<int> Changed;        // chips
    // Raised when the prize coins change.
    public event Action<int> CoinsChanged;

    // Current amounts (read by HUDs, games and the save system).
    public int Chips => chips;
    public int Coins => coins;

    // A new game starts with the starting chips.
    private void Awake() => chips = startingChips;

    // Sets the chips (never below 0) and tells the listeners.
    public void Set(int value)
    {
        value = Mathf.Max(0, value);
        if (value == chips) return;
        chips = value;
        Changed?.Invoke(chips);
    }

    // Adds (or, negative, takes) chips.
    public void Add(int amount) => Set(chips + amount);

    // Sets the prize coins (never below 0) and tells the listeners.
    public void SetCoins(int value)
    {
        value = Mathf.Max(0, value);
        if (value == coins) return;
        coins = value;
        CoinsChanged?.Invoke(coins);
    }

    // Adds (or takes) prize coins.
    public void AddCoins(int amount) => SetCoins(coins + amount);
}
