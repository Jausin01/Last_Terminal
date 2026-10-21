using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// Test only: click on the floor = walk there; click on a pickup = go and take it; click on a save station = save;
// click on a closed door = go through it; Shift + click on a door = just open it / close it;
// click on a blackjack table = let's play.
// Stands in for voice commands until they are connected. Goes on the camera.
[RequireComponent(typeof(Camera))]
public class ClickToMoveTest : MonoBehaviour
{
    public const string PartnerField = nameof(partner);
    public const string SaveUserField = nameof(saveUser);
    public const string DoorUserField = nameof(doorUser);
    public const string CommandsField = nameof(commands);
    public const string CameraSystemField = nameof(cameraSystem);

    [SerializeField] private PartnerInteractor partner;
    [SerializeField] private PartnerSaveUser saveUser;
    [SerializeField] private PartnerDoorUser doorUser;
    [Tooltip("Optional: clicking a blackjack table starts a game.")]
    [SerializeField] private PartnerCommands commands;
    [Tooltip("Optional: clicks are ignored while the camera map / settings are open.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private float maxRayDistance = 200f;
    [SerializeField] private InputAction click = new InputAction("Click", InputActionType.Button, "<Pointer>/press");
    [SerializeField] private InputAction point = new InputAction("Point", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");
    [SerializeField] private InputAction doorOnly = new InputAction("DoorOnly", InputActionType.Button, "<Keyboard>/shift");

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        point.Enable();
        doorOnly.Enable();
        click.Enable();
        click.performed += OnClick;
        if (doorUser != null) doorUser.Finished += LogDoor;
    }

    private void OnDisable()
    {
        click.performed -= OnClick;
        click.Disable();
        doorOnly.Disable();
        point.Disable();
        if (doorUser != null) doorUser.Finished -= LogDoor;
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        if (partner == null) return;

        Ray ray = cam.ScreenPointToRay(point.ReadValue<Vector2>());
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance)) return;

        if (cameraSystem != null && cameraSystem.BlocksWorldInput) return;
        if (saveUser != null && saveUser.IsBusy) return;   // seated at a terminal: the save screen has the input
        if (doorUser != null && !doorUser.IsInterruptible) return;   // in the middle of a door
        BlackjackGame table = hit.collider.GetComponentInParent<BlackjackGame>();
        if (table != null && commands != null)
        {
            commands.PlayAt(table);
            return;
        }
        SaveStation station = hit.collider.GetComponentInParent<SaveStation>();
        if (station != null && saveUser != null)
        {
            saveUser.Use(station);
            return;
        }
        Door door = hit.collider.GetComponentInParent<Door>();
        if (door != null && doorUser != null)
        {
            DoorAction action = !doorOnly.IsPressed() ? DoorAction.PassThrough
                              : door.IsOpen ? DoorAction.Close : DoorAction.Open;
            if (doorUser.Use(door, action)) return;
            // nothing to do with it (e.g. already open): walk to where she clicked
        }
        if (hit.collider.TryGetComponent(out Pickup pickup))
        {
            partner.PickUp(pickup);
            return;
        }
        if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            partner.MoveTo(navHit.position);
    }

    private void LogDoor(Door door, DoorAction action, DoorResult result)
    {
        string name = door != null ? door.DisplayName : "door";
        switch (result)
        {
            case DoorResult.Locked: Debug.Log($"[Helena] {name}: it's locked."); break;
            case DoorResult.NeedsOperator: Debug.Log($"[Helena] {name}: it won't open, you have to open it from the cameras."); break;
            case DoorResult.Unlocked: Debug.Log($"[Helena] {name}: unlocked it with the key."); break;
            case DoorResult.Cancelled: Debug.Log($"[Helena] {name}: {action} cancelled."); break;
            default: Debug.Log($"[Helena] {name}: {action} done."); break;
        }
    }
}
