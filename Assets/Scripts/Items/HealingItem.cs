using UnityEngine;

// Medkit, herb, spray... Heals the user's Health when used.
[CreateAssetMenu(menuName = "Last Terminal/Items/Healing Item", fileName = "NewHealingItem")]
public class HealingItem : ItemDefinition
{
    public const string HealAmountField = nameof(healAmount);

    [SerializeField, Min(1)] private int healAmount = 50;

    public int HealAmount => healAmount;
    public override bool IsUsable => true;

    public override bool TryUse(GameObject user)
    {
        if (!user.TryGetComponent(out Health health)) return false;
        if (health.IsDead || health.IsFull) return false;
        health.Heal(healAmount);
        return true;
    }
}
