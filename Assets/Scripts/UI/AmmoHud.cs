using TMPro;
using UnityEngine;

// Bottom-right ammo readout while a firearm is in her hand: rounds in the magazine / ammo in the inventory.
// Rebuilds the text only when a value changes (no per-frame string allocations).
public class AmmoHud : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ShooterField = nameof(shooter);
    public const string TextField = nameof(text);

    // The shooter (rounds / reserve) and the text.
    [SerializeField] private PartnerShooter shooter;
    [SerializeField] private TMP_Text text;

    // Last values shown (rebuild only on change).
    private RangedWeapon shownWeapon;
    private int shownRounds = -1;
    private int shownReserve = -1;
    private bool shownReloading;

    // Checks references; hidden at start.
    private void Awake()
    {
        if (shooter == null || text == null)
        {
            Debug.LogError($"{name}: AmmoHud needs a PartnerShooter and a text.", this);
            enabled = false;
            return;
        }
        text.text = "";
    }

    // After everything updated: shows / hides and rebuilds when a value changed.
    private void LateUpdate()
    {
        if (!shooter.enabled) return;
        RangedWeapon weapon = shooter.Current;
        int rounds = shooter.CurrentRounds;
        int reserve = shooter.CurrentReserve;
        bool reloading = shooter.IsReloading;
        if (weapon == shownWeapon && rounds == shownRounds && reserve == shownReserve && reloading == shownReloading) return;

        shownWeapon = weapon;
        shownRounds = rounds;
        shownReserve = reserve;
        shownReloading = reloading;

        if (weapon == null) { text.text = ""; return; }
        string ammoName = weapon.Ammo != null ? weapon.Ammo.DisplayName.ToUpperInvariant() : "";
        string state = reloading ? "  <size=60%>RELOADING</size>" : rounds == 0 ? "  <size=60%>EMPTY</size>" : "";
        text.text = $"<size=55%>{ammoName}</size>\n{rounds:00} <size=60%>/ {reserve:000}</size>{state}";
    }
}
