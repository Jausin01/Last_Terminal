using System;
using UnityEngine;

// One elevator: a car behind two sliding doors, a sign above (ElevatorSign) and a call lamp.
// It is at a stop (Here) and goes to another (Destination); PartnerElevatorUser rides it, ElevatorTravel loads the
// destination scene and brings her out of the elevator of that scene whose Here is where she went.
// This transform: on the floor at the doorway, forward = out of the car (into the room).
public class Elevator : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string HereField = nameof(here);
    public const string DestinationField = nameof(destination);
    public const string NumberField = nameof(number);
    public const string LeftDoorField = nameof(leftDoor);
    public const string RightDoorField = nameof(rightDoor);
    public const string InsideField = nameof(inside);
    public const string OutsideField = nameof(outside);
    public const string LampField = nameof(lamp);
    public const string SignField = nameof(sign);
    public const string AudioSourceField = nameof(audioSource);
    public const string WatchCameraField = nameof(watchCamera);
    public const string DoorTravelField = nameof(doorTravel);

    // Where it is and where it goes.
    [SerializeField] private ElevatorStop here;
    [SerializeField] private ElevatorStop destination;
    [Tooltip("\"Elevator 2\" (on its sign too).")]
    [SerializeField, Min(1)] private int number = 1;
    // The two sliding doors.
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [Tooltip("Where she stands in the car (facing out).")]
    [SerializeField] private Transform inside;
    [Tooltip("Where she waits in front of it.")]
    [SerializeField] private Transform outside;
    // Call lamp, sign and sound source.
    [SerializeField] private Renderer lamp;
    [SerializeField] private ElevatorSign sign;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The CCTV camera that sees it: the view goes there when she arrives in it.")]
    [SerializeField] private SecurityCamera watchCamera;
    // Door travel (m) and time (s).
    [SerializeField] private float doorTravel = 0.5f;
    [SerializeField] private float doorTime = 1.1f;

    // Shader ids; closed door positions.
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Vector3 leftClosed, rightClosed;
    private float amount;            // 0 closed .. 1 open
    // Wanted state, lamp property block, the generated ding.
    private bool wantOpen;
    private MaterialPropertyBlock block;
    private AudioClip ding;

    // Raised when the doors start opening / closing.
    public event Action<Elevator> DoorsChanged;

    // Read-only data for her elevator user, travel and the map.
    public ElevatorStop Here => here;
    public ElevatorStop Destination => destination;
    public int Number => number;
    public Transform Inside => inside;
    public Transform Outside => outside;
    public SecurityCamera WatchCamera => watchCamera;
    public bool IsOpen => amount >= 0.999f;
    public bool IsClosed => amount <= 0.001f;
    public ElevatorKey RequiredKey => destination != null ? destination.RequiredKey : null;

    // Stores closed positions; makes the ding; closed lamp.
    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (leftDoor != null) leftClosed = leftDoor.localPosition;
        if (rightDoor != null) rightClosed = rightDoor.localPosition;
        ding = Ding();
        Apply();
        SetLamp(false);
    }

    // Opens the doors (ding).
    public void Open()
    {
        if (wantOpen) return;
        wantOpen = true;
        SetLamp(true);
        if (audioSource != null) audioSource.PlayOneShot(ding);
        DoorsChanged?.Invoke(this);
    }

    // Closes the doors.
    public void Close()
    {
        if (!wantOpen) return;
        wantOpen = false;
        SetLamp(false);
        DoorsChanged?.Invoke(this);
    }

    // shut at once (a scene starts with her already inside)
    public void SnapClosed()
    {
        wantOpen = false;
        amount = 0f;
        Apply();
        SetLamp(false);
    }

    // Moves the doors toward the wanted state.
    private void Update()
    {
        float target = wantOpen ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;
        amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(doorTime, 0.01f));
        Apply();
    }

    // Places the doors.
    private void Apply()
    {
        float s = Mathf.SmoothStep(0f, 1f, amount) * doorTravel;
        if (leftDoor != null) leftDoor.localPosition = leftClosed + Vector3.left * s;
        if (rightDoor != null) rightDoor.localPosition = rightClosed + Vector3.right * s;
    }

    // Lamp on / off.
    private void SetLamp(bool on)
    {
        if (lamp == null) return;
        Color c = on ? new Color(1f, 0.85f, 0.4f) : new Color(0.25f, 0.2f, 0.1f);
        block.SetColor(BaseColorId, c);
        block.SetColor(EmissionColorId, on ? c * 2f : Color.black);
        lamp.SetPropertyBlock(block);
    }

    // Generated "ding" clip.
    private static AudioClip Ding()
    {
        // Sample rate of the generated clip.
        const int rate = 22050;
        int n = (int)(rate * 0.9f);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = t < 0.35f ? 1318.5f : 1046.5f;   // E6 - C6
            float t0 = t < 0.35f ? t : t - 0.35f;
            data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t0 * 5f) * 0.3f;
        }
        AudioClip clip = AudioClip.Create("ElevatorDing", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
