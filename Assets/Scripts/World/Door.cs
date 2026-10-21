using System;
using UnityEngine;
using UnityEngine.AI;

// A door: hinged (wooden) or sliding (security). Closed doors cut the NavMesh, so she paths around them.
// Remote doors are controlled by the camera operator from the facility map; the others are opened by her
// (PartnerDoorUser drives the leaf with her hand while her door clip plays: BeginManual / SetManualAngle / EndManual).
// A status lamp (optional) shows the state: green open, red closed, amber locked.
// Doors are numbered (plates above them, DoorSign): "open door 2".
public class Door : MonoBehaviour
{
    public enum Kind { Hinged, Sliding }

    public const string DisplayNameField = nameof(displayName);
    public const string KindField = nameof(kind);
    public const string MovingPartField = nameof(movingPart);
    public const string LampField = nameof(lamp);
    public const string ObstacleField = nameof(obstacle);
    public const string RemoteField = nameof(remoteControlled);
    public const string StartOpenField = nameof(startOpen);
    public const string LockedField = nameof(locked);
    public const string RequiredKeyField = nameof(requiredKey);
    public const string OpenAngleField = nameof(openAngle);
    public const string NumberField = nameof(number);

    [SerializeField] private string displayName = "Door";
    [Tooltip("Number on its plates (0 = none).")]
    [SerializeField, Min(0)] private int number;
    [SerializeField] private Kind kind = Kind.Hinged;
    [Tooltip("Leaf (hinged, pivot on the hinge) or panel (sliding).")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private Renderer lamp;
    [SerializeField] private NavMeshObstacle obstacle;
    [Tooltip("The camera operator opens / closes it from the facility map.")]
    [SerializeField] private bool remoteControlled;
    [SerializeField] private bool startOpen;
    [SerializeField] private bool locked;
    [Tooltip("Key that unlocks it (her inventory), if any.")]
    [SerializeField] private KeyItem requiredKey;

    [Header("Motion")]
    [Tooltip("Hinged: how far the leaf opens (deg). 80 = where her door clips leave it.")]
    [SerializeField] private float openAngle = 80f;
    [SerializeField] private float slideDistance = 1.2f;
    [Tooltip("Seconds to open or close.")]
    [SerializeField] private float moveTime = 0.8f;

    [Header("Lamp")]
    [SerializeField] private Color openColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color closedColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] private Color lockedColor = new Color(1f, 0.65f, 0.1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Quaternion closedRotation;
    private Vector3 closedPosition;
    private float amount;          // 0 closed .. 1 open
    private bool wantOpen;
    private MaterialPropertyBlock block;
    private bool manual;
    private float manualAngle;

    public event Action<Door> Changed;

    public string DisplayName => displayName;
    public int Number => number;
    public bool IsOpen => wantOpen;
    public bool IsFullyOpen => amount >= 1f;
    public bool IsLocked => locked;
    public bool IsRemoteControlled => remoteControlled;
    public KeyItem RequiredKey => requiredKey;
    public bool IsHinged => kind == Kind.Hinged && movingPart != null;
    public bool IsManual => manual;
    public float OpenAngle => openAngle;
    // leaf angle now (deg, hinged doors)
    public float LeafAngle => manual ? manualAngle : openAngle * Mathf.SmoothStep(0f, 1f, amount);
    // the hinge (leaf pivot) in this door's local space; +Z = the side the leaf opens toward
    public Vector3 HingeLocal => movingPart != null ? transform.InverseTransformPoint(movingPart.position) : Vector3.zero;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (movingPart != null)
        {
            closedRotation = movingPart.localRotation;
            closedPosition = movingPart.localPosition;
        }
        wantOpen = startOpen && !locked;
        amount = wantOpen ? 1f : 0f;
        Apply();
        UpdateObstacle();
        UpdateLamp();
    }

    public bool Open()
    {
        if (manual) return false;
        if (locked || wantOpen) return !locked && wantOpen;
        wantOpen = true;
        UpdateObstacle();
        UpdateLamp();
        Changed?.Invoke(this);
        return true;
    }

    public bool Close()
    {
        if (manual) return false;
        if (!wantOpen) return true;
        wantOpen = false;
        UpdateObstacle();
        UpdateLamp();
        Changed?.Invoke(this);
        return true;
    }

    public bool Toggle() => wantOpen ? Close() : Open();

    public void SetLocked(bool value)
    {
        if (locked == value) return;
        locked = value;
        if (locked) wantOpen = false;
        UpdateObstacle();
        UpdateLamp();
        Changed?.Invoke(this);
    }

    // a key from her inventory (KeyItem.ConsumeOnUse is handled by the caller)
    public bool TryUnlock(KeyItem key)
    {
        if (!locked || key == null || key != requiredKey) return false;
        SetLocked(false);
        return true;
    }

    // ------------------------------------------------------------------ driven by her hand (hinged only)
    // The leaf follows SetManualAngle until EndManual; the open / closed state doesn't change meanwhile.
    public bool BeginManual()
    {
        if (!IsHinged || manual) return false;
        manualAngle = LeafAngle;
        manual = true;
        return true;
    }

    public void SetManualAngle(float degrees)
    {
        if (!manual) return;
        manualAngle = Mathf.Clamp(degrees, -2f, openAngle);
        movingPart.localRotation = Quaternion.AngleAxis(-manualAngle, Vector3.up) * closedRotation;
    }

    // Lets go of the leaf: it carries on to open / closed from where it is (open is refused while locked).
    public void EndManual(bool open)
    {
        if (!manual) return;
        manual = false;
        amount = InverseSmoothStep(Mathf.Clamp01(manualAngle / Mathf.Max(openAngle, 0.01f)));
        bool changed = wantOpen != (open && !locked);
        wantOpen = open && !locked;
        Apply();
        UpdateObstacle();
        UpdateLamp();
        if (changed) Changed?.Invoke(this);
    }

    // x such that SmoothStep(0, 1, x) = y
    private static float InverseSmoothStep(float y) => 0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * y) / 3f);

    private void Update()
    {
        if (manual) return;
        float target = wantOpen ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;
        float step = moveTime > 0f ? Time.deltaTime / moveTime : 1f;
        amount = Mathf.MoveTowards(amount, target, step);
        Apply();
        if (Mathf.Approximately(amount, target)) UpdateObstacle();
    }

    private void Apply()
    {
        if (movingPart == null) return;
        float t = Mathf.SmoothStep(0f, 1f, amount);
        if (kind == Kind.Hinged) movingPart.localRotation = Quaternion.AngleAxis(-openAngle * t, Vector3.up) * closedRotation;
        else movingPart.localPosition = closedPosition + Vector3.right * (slideDistance * t);
    }

    // the NavMesh hole is there whenever the door isn't fully open
    private void UpdateObstacle()
    {
        if (obstacle != null) obstacle.enabled = !(wantOpen && amount >= 1f);
    }

    private void UpdateLamp()
    {
        if (lamp == null) return;
        Color c = locked ? lockedColor : wantOpen ? openColor : closedColor;
        lamp.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(EmissionColorId, c * 2f);
        lamp.SetPropertyBlock(block);
    }
}
