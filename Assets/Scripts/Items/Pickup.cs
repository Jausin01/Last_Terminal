using UnityEngine;

// An item lying in the world: "X units of item Y". One prefab works for every item type.
// Needs a Collider on this same GameObject so it can be clicked / targeted.
public class Pickup : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ItemField = nameof(item);
    public const string AmountField = nameof(amount);
    public const string VisualField = nameof(visual);
    public const string GrabPointField = nameof(grabPoint);
    public const string SaveIdField = nameof(saveId);

    // What lies here and how many units.
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("Where the partner walks to pick it up. Empty = the pickup itself.")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("Where her hand goes (top of the item). Empty = a little above the pivot.")]
    [SerializeField] private Transform grabPoint;
    [Tooltip("The model, shown in her hand while she picks it up. Empty = it just disappears.")]
    [SerializeField] private Transform visual;
    [Tooltip("Unique in the scene: the save system records what is left of this pickup under this id.")]
    [SerializeField] private string saveId;

    // Default height of the grab point above the pivot (m).
    private const float DefaultGrabHeight = 0.05f;

    // Read by the interactor, the commands and the save system.
    public ItemDefinition Item => item;
    public int Amount => amount;
    public Transform Visual => visual;
    public string SaveId => saveId;
    // what is left on the floor (0 once collected)
    public int Remaining => gameObject.activeSelf ? amount : 0;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
    public Vector3 GrabPosition => grabPoint != null ? grabPoint.position : transform.position + Vector3.up * DefaultGrabHeight;

    // Moves what fits into the inventory. Returns how many units were taken.
    // Disabled (not destroyed) when empty, so a save system can record it later.
    public int Collect(Inventory inventory)
    {
        int added = inventory.TryAdd(item, amount);
        amount -= added;
        if (amount <= 0) gameObject.SetActive(false);
        return added;
    }

    // Loading a save: puts back what was left (0 = already collected).
    public void Restore(int remaining)
    {
        amount = Mathf.Max(0, remaining);
        gameObject.SetActive(amount > 0);
    }
}
