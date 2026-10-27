using UnityEngine;

// Medkit, herb, spray... Heals the user's Health when used.
[CreateAssetMenu(menuName = "Last Terminal/Items/Healing Item", fileName = "NewHealingItem")]
public class HealingItem : ItemDefinition
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string HealAmountField = nameof(healAmount);

    // Hit points restored.
    [SerializeField, Min(1)] private int healAmount = 50;

    // Read by the HUD; it can be used from the inventory.
    public int HealAmount => healAmount;
    public override bool IsUsable => true;

    // Heals the user (not when dead or already full).
    public override bool TryUse(GameObject user)
    {
        if (!user.TryGetComponent(out Health health)) return false;
        if (health.IsDead || health.IsFull) return false;
        health.Heal(healAmount);
        return true;
    }
}
