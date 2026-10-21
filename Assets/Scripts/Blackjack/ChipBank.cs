using System;
using UnityEngine;

// The operator's money (kept in the save files, carried between scenes):
// chips to play with (every table and machine) and prize coins (won at the cashier, spent on prizes).
public class ChipBank : MonoBehaviour
{
    public const string StartingChipsField = nameof(startingChips);

    [SerializeField, Min(0)] private int startingChips = 1000;

    private int chips;
    private int coins;

    public event Action<int> Changed;        // chips
    public event Action<int> CoinsChanged;

    public int Chips => chips;
    public int Coins => coins;

    private void Awake() => chips = startingChips;

    public void Set(int value)
    {
        value = Mathf.Max(0, value);
        if (value == chips) return;
        chips = value;
        Changed?.Invoke(chips);
    }

    public void Add(int amount) => Set(chips + amount);

    public void SetCoins(int value)
    {
        value = Mathf.Max(0, value);
        if (value == coins) return;
        coins = value;
        CoinsChanged?.Invoke(coins);
    }

    public void AddCoins(int amount) => SetCoins(coins + amount);
}
