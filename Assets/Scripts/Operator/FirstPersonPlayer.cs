using System;
using UnityEngine;
using UnityEngine.InputSystem;

// The operator in his room, first person: WASD / arrows walk (Shift = faster), the mouse looks, the left button uses
// what is under the dot (OperatorInteractable, within reach) - only while he is walking around (OperatorStation
// turns it on in the room). His arms (FP arms rig) reach out when he uses something; with a coffee in hand, the left
// button on nothing takes a sip.
// The view can be taken away (BlendViewTo: sitting at the PC, leaning over the pinball) and given back (BlendViewHome).
public class FirstPersonPlayer : MonoBehaviour
{
    public const string ControllerField = nameof(controller);
    public const string ViewField = nameof(view);
    public const string ArmsField = nameof(arms);
    public const string CupField = nameof(cup);
    public const string HudField = nameof(hud);
    public const string SettingsField = nameof(settings);
    public const string StoryField = nameof(story);
    public const string AudioField = nameof(audioSource);
    public const string EyeHeightField = nameof(eyeHeight);

    // the arms' animator states (the Last Terminal menus make them from the FP arms clips). Unity doesn't allow a '.'
    // in a state name (it becomes '_'), so the states have their own names; the clips keep the names of the file.
    public const string RelaxState = "Relax";
    public const string GrabState = "GrabR";
    public const string PushState = "PushR";
    public const string RelaxClip = "relax";
    public const string GrabClip = "grab.R";
    public const string PushClip = "push.R";

    [SerializeField] private CharacterController controller;
    [SerializeField] private Camera view;
    [Tooltip("The first person arms (child of the view).")]
    [SerializeField] private Animator arms;
    [Tooltip("The coffee cup in his right hand (hidden until he takes one).")]
    [SerializeField] private GameObject cup;
    [SerializeField] private OperatorRoomHud hud;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private StoryProgress story;
    [SerializeField] private AudioSource audioSource;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float runSpeed = 2.8f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float eyeHeight = 1.62f;
    [Tooltip("Degrees per pixel of mouse movement.")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float bobAmount = 0.022f;
    [SerializeField] private float bobFrequency = 1.9f;   // steps per meter (roughly)

    [Header("Use")]
    [SerializeField] private float reach = 2.4f;
    [SerializeField] private LayerMask useMask = ~0;
    [SerializeField, Min(1)] private int sipsPerCup = 3;

    [Header("Input (bindings added when empty)")]
    [SerializeField] private InputAction move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
    [SerializeField] private InputAction look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
    [SerializeField] private InputAction use = new InputAction("Use", InputActionType.Button, "<Mouse>/leftButton");
    [SerializeField] private InputAction run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");

    private static readonly int GrabId = Animator.StringToHash(GrabState);
    private static readonly int PushId = Animator.StringToHash(PushState);

    private bool controlActive;
    private float yaw, pitch;
    private float fallSpeed;
    private float bobPhase;
    private OperatorInteractable target;
    private int sips;
    private float drinkTimer;
    private bool sipPending;
    private bool warnedArms;

    // view blend
    private bool blending, blendHome, awayFromHome;
    private Vector3 blendFromPos, blendToPos;
    private Quaternion blendFromRot, blendToRot;
    private float blendTime, blendDuration;
    private Action blendDone;

    public bool HasCup => sips > 0;
    public bool IsControlActive => controlActive;
    public bool IsBlending => blending;
    public GameLanguage Language => settings != null ? settings.Language : GameLanguage.English;
    public OperatorRoomHud Hud => hud;
    public StoryProgress Story => story;
    public AudioSource Audio => audioSource;
    public Camera View => view;

    private Vector3 EyeLocal => new Vector3(0f, eyeHeight, 0f);

    private void Awake()
    {
        if (move.bindings.Count == 0)
        {
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        }
        if (controller == null || view == null || hud == null)
        {
            Debug.LogError($"{name}: FirstPersonPlayer is missing references (run the Last Terminal scene menu).", this);
            enabled = false;
            return;
        }
        yaw = transform.eulerAngles.y;
        pitch = 0f;
        view.transform.localPosition = EyeLocal;
        view.transform.localRotation = Quaternion.identity;
        if (cup != null) cup.SetActive(false);
    }

    private void OnEnable() { move.Enable(); look.Enable(); use.Enable(); run.Enable(); }
    private void OnDisable() { move.Disable(); look.Disable(); use.Disable(); run.Disable(); }

    // walking around, looking, using things (the room); off at the PC, the pinball, while the view blends
    public void SetControl(bool on)
    {
        controlActive = on;
        if (!on)
        {
            target = null;
            if (hud != null) hud.SetHover(null);
        }
    }

    public void SetArmsVisible(bool on)
    {
        if (arms != null) arms.gameObject.SetActive(on);
    }

    private void Update()
    {
        UpdateBlend();
        UpdateDrink();
        if (!controlActive || blending) return;
        if (hud.IsNoteOpen)
        {
            if (use.WasPressedThisFrame()) hud.HideNote();
            return;
        }
        Look();
        Move();
        Aim();
        if (use.WasPressedThisFrame()) Use();
    }

    private void Look()
    {
        Vector2 d = look.ReadValue<Vector2>() * lookSensitivity;
        yaw += d.x;
        pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (!awayFromHome) view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        float speed = run.IsPressed() ? runSpeed : walkSpeed;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;
        fallSpeed = controller.isGrounded ? -1f : fallSpeed - gravity * Time.deltaTime;
        velocity.y = fallSpeed;
        controller.Move(velocity * Time.deltaTime);

        // head bob while walking
        float moving = input.magnitude * speed;
        if (moving > 0.05f) bobPhase += moving * bobFrequency * Time.deltaTime * Mathf.PI;
        else bobPhase = Mathf.MoveTowards(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);
        float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmount;
        if (!awayFromHome) view.transform.localPosition = EyeLocal + new Vector3(Mathf.Cos(bobPhase) * bobAmount * 0.4f, bob - bobAmount * 0.5f, 0f);
    }

    private void Aim()
    {
        target = null;
        Transform cam = view.transform;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, reach, useMask, QueryTriggerInteraction.Collide))
            hit.collider.TryGetComponent(out target);   // the collider is on the interactable itself
        hud.SetHover(target != null ? target.Label(Language) : null);
    }

    private void Use()
    {
        if (target != null)
        {
            PlayHand(target.Motion);
            target.Interact(this);
            return;
        }
        if (HasCup) Drink();
    }

    public void PlayHand(OperatorInteractable.HandMotion motion)
    {
        if (arms == null || !arms.gameObject.activeInHierarchy || motion == OperatorInteractable.HandMotion.None) return;
        int id = motion == OperatorInteractable.HandMotion.Grab ? GrabId : PushId;
        if (!arms.HasState(0, id))
        {
            if (!warnedArms) Debug.LogWarning($"{name}: the arms' Animator has no '{(id == GrabId ? GrabState : PushState)}' state (run the Last Terminal scene menu again).", this);
            warnedArms = true;
            return;
        }
        arms.CrossFadeInFixedTime(id, 0.08f, 0, 0f);
    }

    // ------------------------------------------------------------------ coffee
    // a full cup in his hand (the coffee machine)
    public void GiveCup()
    {
        sips = sipsPerCup;
        if (cup != null) cup.SetActive(true);
    }

    private void Drink()
    {
        if (drinkTimer > 0f) return;
        PlayHand(OperatorInteractable.HandMotion.Grab);   // the hand with the cup comes up
        drinkTimer = 0.75f;
        sipPending = true;
    }

    private void UpdateDrink()
    {
        if (drinkTimer <= 0f) return;
        drinkTimer -= Time.deltaTime;
        if (sipPending && drinkTimer < 0.4f)
        {
            sipPending = false;
            OperatorSounds.Play(audioSource, OperatorSounds.Kind.Sip, 0.8f);
            sips--;
        }
        if (drinkTimer > 0f || sips > 0) return;
        if (cup != null) cup.SetActive(false);
        int total = story != null ? story.Add("coffee") : 0;
        hud.ShowMessage(Language == GameLanguage.Portuguese
            ? (total > 1 ? $"Acabou o café. ({total}º do turno)" : "Acabou o café.")
            : (total > 1 ? $"Coffee's gone. (#{total} this shift)" : "Coffee's gone."));
    }

    // ------------------------------------------------------------------ view blends
    // the view glides to a pose in the world (stays there until BlendViewHome)
    public void BlendViewTo(Vector3 position, Quaternion rotation, float seconds, Action done)
    {
        StartBlend(position, rotation, seconds, false, done);
        awayFromHome = true;
    }

    // back to his eyes
    public void BlendViewHome(float seconds, Action done)
    {
        StartBlend(Vector3.zero, Quaternion.identity, seconds, true, done);
    }

    private void StartBlend(Vector3 position, Quaternion rotation, float seconds, bool home, Action done)
    {
        blendFromPos = view.transform.position;
        blendFromRot = view.transform.rotation;
        blendToPos = position;
        blendToRot = rotation;
        blendHome = home;
        blendDuration = Mathf.Max(0.01f, seconds);
        blendTime = 0f;
        blendDone = done;
        blending = true;
    }

    private void UpdateBlend()
    {
        if (!blending) return;
        blendTime += Time.deltaTime;
        float k = Mathf.Clamp01(blendTime / blendDuration);
        k = k * k * (3f - 2f * k);
        Vector3 toPos = blendHome ? transform.TransformPoint(EyeLocal) : blendToPos;
        Quaternion toRot = blendHome ? transform.rotation * Quaternion.Euler(pitch, 0f, 0f) : blendToRot;
        view.transform.SetPositionAndRotation(Vector3.Lerp(blendFromPos, toPos, k), Quaternion.Slerp(blendFromRot, toRot, k));
        if (blendTime < blendDuration) return;
        blending = false;
        if (blendHome)
        {
            awayFromHome = false;
            view.transform.localPosition = EyeLocal;
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        Action done = blendDone;
        blendDone = null;
        done?.Invoke();
    }

    // places the view right away (a scene that starts at the PC)
    public void SnapViewTo(Vector3 position, Quaternion rotation)
    {
        blending = false;
        awayFromHome = true;
        view.transform.SetPositionAndRotation(position, rotation);
    }
}
