using System;
using UnityEngine;

// Hit points for the partner and enemies. UI, animation and voice feedback listen to the events.
public class Health : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string MaxHealthField = nameof(maxHealth);

    // Hit points when full.
    [SerializeField, Min(1)] private int maxHealth = 100;

    // Hit points now.
    private int current;

    public event Action<int, int> Changed;   // current, max
    // Raised once when the hit points reach 0.
    public event Action Died;
    public event Action<int> Damaged;        // damage actually taken (after evasion), before Died

    // set by whoever is evading (PartnerEvasion): no damage at all / damage scaled (ducked)
    public bool Invulnerable { get; set; }
    public float DamageScale { get; set; } = 1f;

    // State read by the HUD, the AI and the commands.
    public int Current => current;
    public int Max => maxHealth;
    public bool IsFull => current >= maxHealth;
    public bool IsDead => current <= 0;

    // Starts full.
    private void Awake()
    {
        current = maxHealth;
    }

    // Returns how much was actually healed.
    public int Heal(int amount)
    {
        if (IsDead || amount <= 0) return 0;
        int before = current;
        current = Mathf.Min(maxHealth, current + amount);
        int healed = current - before;
        if (healed > 0) Changed?.Invoke(current, maxHealth);
        return healed;
    }

    // Loading a save. Never kills: at least 1.
    public void SetCurrent(int value)
    {
        current = Mathf.Clamp(value, 1, maxHealth);
        Changed?.Invoke(current, maxHealth);
    }

    // Loading a save where this one was already dead: dead again, without Died (nothing is "killed" by a load).
    public void SetDefeated()
    {
        current = 0;
        Changed?.Invoke(current, maxHealth);
    }

    // Back to full health (respawn, training dummy).
    public void Restore()
    {
        current = maxHealth;
        Changed?.Invoke(current, maxHealth);
    }

    // Takes damage (scaled while ducking, none while invulnerable, at least 1); raises Damaged and, at 0, Died.
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || Invulnerable) return;
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(DamageScale, 0f)));
        int before = current;
        current = Mathf.Max(0, current - amount);
        Changed?.Invoke(current, maxHealth);
        Damaged?.Invoke(before - current);
        if (current == 0) Died?.Invoke();
    }
}
