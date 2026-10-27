using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Test only: G = draw / holster, 1..9, 0 = weapon of the loadout (0 = 10th), hold right mouse = aim at the point under the cursor,
// F = attack toward the point under the cursor (firearm: shoot; blade: press again = combo), R = reload.
// Left click still moves her (ClickToMoveTest), so she strafes / backpedals while aiming. Goes on the camera.
[RequireComponent(typeof(Camera))]
public class CombatTestInput : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string CombatField = nameof(combat);
    public const string CameraSystemField = nameof(cameraSystem);
    private static readonly string[] WeaponKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

    // Her combat (draw, aim, attack, reload).
    [SerializeField] private PartnerCombat combat;
    [Tooltip("Optional: no combat input while the camera map / settings are open.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    // How far the cursor ray goes, and the aim height over the floor.
    [SerializeField] private float maxRayDistance = 200f;
    [SerializeField] private float floorAimHeight = 1.2f;
    // Test keys: G draw / holster, right mouse aim, F attack, R reload.
    [SerializeField] private InputAction toggleWeapon = new InputAction("ToggleWeapon", InputActionType.Button, "<Keyboard>/g");
    [SerializeField] private InputAction aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
    [SerializeField] private InputAction attack = new InputAction("Attack", InputActionType.Button, "<Keyboard>/f");
    [SerializeField] private InputAction reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
    [Tooltip("Keys 1..9, 0 (bindings added automatically when empty). Binding order = loadout index.")]
    [SerializeField] private InputAction selectWeapon = new InputAction("SelectWeapon", InputActionType.PassThrough);
    // Pointer position.
    [SerializeField] private InputAction point = new InputAction("Point", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");

    // The camera the cursor ray is cast from.
    private Camera cam;

    // Gets the camera; default 1..0 bindings.
    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (selectWeapon.bindings.Count == 0)
            foreach (string key in WeaponKeys) selectWeapon.AddBinding("<Keyboard>/" + key);
    }

    // Enables the keys.
    private void OnEnable()
    {
        toggleWeapon.Enable();
        aim.Enable();
        attack.Enable();
        reload.Enable();
        selectWeapon.Enable();
        point.Enable();
        toggleWeapon.performed += OnToggleWeapon;
        aim.canceled += OnAimReleased;
        attack.performed += OnAttack;
        reload.performed += OnReload;
        selectWeapon.performed += OnSelectWeapon;
    }

    // Disables them.
    private void OnDisable()
    {
        toggleWeapon.performed -= OnToggleWeapon;
        aim.canceled -= OnAimReleased;
        attack.performed -= OnAttack;
        reload.performed -= OnReload;
        selectWeapon.performed -= OnSelectWeapon;
        toggleWeapon.Disable();
        aim.Disable();
        attack.Disable();
        reload.Disable();
        selectWeapon.Disable();
        point.Disable();
    }

    // Holding right mouse: aims at the point under the cursor.
    private void Update()
    {
        if (combat == null || !aim.IsPressed() || Blocked) return;
        if (TryGetCursorPoint(out Vector3 p)) combat.AimAt(p);
    }

    // map / settings open, or the operator is typing a command
    private bool Blocked => cameraSystem != null && cameraSystem.BlocksWorldInput;

    // The point under the cursor (chest height over floors / walls).
    private bool TryGetCursorPoint(out Vector3 p)
    {
        Ray ray = cam.ScreenPointToRay(point.ReadValue<Vector2>());
        bool hit = Physics.Raycast(ray, out RaycastHit info, maxRayDistance);
        p = hit ? info.point : Vector3.zero;
        // floor / walls: aim at chest height above that point; targets with Health: exactly where clicked
        if (hit && info.collider.GetComponentInParent<Health>() == null) p += Vector3.up * floorAimHeight;
        return hit;
    }

    // G: draw / holster.
    private void OnToggleWeapon(InputAction.CallbackContext ctx)
    {
        if (combat == null || Blocked) return;
        if (combat.IsArmed) combat.Holster();
        else combat.Draw();
    }

    // binding index = loadout index (1 -> 0, 2 -> 1, ...); only key presses, not releases
    private void OnSelectWeapon(InputAction.CallbackContext ctx)
    {
        if (combat == null || Blocked || !(ctx.control is ButtonControl button) || !button.isPressed) return;
        int index = selectWeapon.GetBindingIndexForControl(ctx.control);
        if (index >= 0) combat.SelectWeapon(index);
    }

    // F: attacks toward the cursor.
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        if (combat == null || Blocked) return;
        if (TryGetCursorPoint(out Vector3 p)) combat.Attack(p);
        else combat.Attack();
    }

    // R: reload.
    private void OnReload(InputAction.CallbackContext ctx)
    {
        if (combat != null && !Blocked) combat.Reload();
    }

    // Right mouse released: stops aiming.
    private void OnAimReleased(InputAction.CallbackContext ctx)
    {
        if (combat != null) combat.StopAiming();
    }
}
