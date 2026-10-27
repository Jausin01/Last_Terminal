using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Test only: H = use a healing item, J = take damage.
// Logs inventory, health and pickup events (event-driven, no per-frame logs).
[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PartnerInteractor))]
public class PartnerTestHarness : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string CameraSystemField = nameof(cameraSystem);

    [Tooltip("Optional: keys ignored while the operator types a command.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    // Damage taken with J, and the H / J test keys.
    [SerializeField] private int testDamage = 25;
    [SerializeField] private InputAction useHeal = new InputAction("UseHeal", InputActionType.Button, "<Keyboard>/h");
    [SerializeField] private InputAction takeDamage = new InputAction("TakeDamage", InputActionType.Button, "<Keyboard>/j");

    // Her inventory, health and interactor (on the same object).
    private Inventory inventory;
    private Health health;
    private PartnerInteractor interactor;

    // Gets the components.
    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        health = GetComponent<Health>();
        interactor = GetComponent<PartnerInteractor>();
    }

    // Enables the keys and the log listeners.
    private void OnEnable()
    {
        useHeal.Enable();
        takeDamage.Enable();
        useHeal.performed += OnUseHeal;
        takeDamage.performed += OnTakeDamage;
        inventory.Changed += LogInventory;
        health.Changed += LogHealth;
        interactor.PickupAttempted += LogPickup;
    }

    // Disables them.
    private void OnDisable()
    {
        useHeal.performed -= OnUseHeal;
        takeDamage.performed -= OnTakeDamage;
        useHeal.Disable();
        takeDamage.Disable();
        inventory.Changed -= LogInventory;
        health.Changed -= LogHealth;
        interactor.PickupAttempted -= LogPickup;
    }

    // The operator is typing a command (keys ignored).
    private bool Blocked => cameraSystem != null && cameraSystem.IsTyping;

    // H: uses the first healing item.
    private void OnUseHeal(InputAction.CallbackContext ctx)
    {
        if (Blocked) return;
        HealingItem heal = inventory.FindFirst<HealingItem>();
        if (heal == null) { Debug.Log("[Test] No healing item in the inventory."); return; }
        if (!inventory.TryUse(heal)) Debug.Log($"[Test] Can't use {heal.DisplayName} (health already full?).");
    }

    // J: takes test damage.
    private void OnTakeDamage(InputAction.CallbackContext ctx)
    {
        if (Blocked) return;
        health.TakeDamage(testDamage);
    }

    // Logs the inventory contents.
    private void LogInventory()
    {
        var sb = new StringBuilder();
        sb.Append($"[Inventory {inventory.UsedSlots}/{inventory.SlotCount}] ");
        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{inventory.Slots[i].Item.DisplayName} x{inventory.Slots[i].Amount}");
        }
        Debug.Log(sb.ToString());
    }

    // Logs the health.
    private void LogHealth(int current, int max)
    {
        Debug.Log($"[Health] {current}/{max}");
    }

    // Logs a pickup result.
    private void LogPickup(ItemDefinition item, int taken, PickupResult result)
    {
        Debug.Log($"[Pickup] {result}: {item.DisplayName} x{taken}");
    }
}
