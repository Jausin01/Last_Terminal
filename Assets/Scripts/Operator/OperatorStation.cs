using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Where the operator is: walking around his room (first person), at the PC's desktop (his files), in the camera
// program (operator mode: the CCTV cameras, the map, the commands to Helena) or playing the pinball.
// - Room: clicking the PC sits him down and opens the camera program directly.
// - Operator: Esc asks "close the program?" (Close / Settings / Cancel). Closing only works with Helena in a safe
//   zone (Room.SafeZone), out of a fight and free; she sits on the floor and waits (PartnerRest). Then: the desktop.
// - Desktop: CCTV.EXE goes back to the cameras (she gets up), Stand up (or Esc) goes back to the room.
// - Pinball: Esc leaves the machine (the game pauses).
// - Focus: something looked at up close (FocusInteractable: an evidence board, the door keypad), cursor free; Esc
//   asks it first (zoomed on a photo = back to the board), then takes him back to the room.
// The PC can have a power button (PcPower): while it is off he doesn't sit, he only thinks about it; the OS's
// "Shut down" turns it off and gets him up.
// A new Play starts in the room (she waits sitting in her cell); a scene entered from another one (elevator, a
// save of another scene, a restart) starts in the camera program (GameSession).
// Every switch turns the operator's things (camera system, inputs, his UI, the CCTV filter, the view camera) on or
// off together; the room has its own camera and listener.
public class OperatorStation : MonoBehaviour
{
    public const string PlayerField = nameof(player);
    public const string HudField = nameof(hud);
    public const string DesktopField = nameof(desktop);
    public const string RoomCameraField = nameof(roomCamera);
    public const string RoomListenerField = nameof(roomListener);
    public const string ViewCameraField = nameof(viewCamera);
    public const string ViewListenerField = nameof(viewListener);
    public const string OperatorCanvasField = nameof(operatorCanvas);
    public const string OperatorBehavioursField = nameof(operatorBehaviours);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string PartnerRestField = nameof(partnerRest);
    public const string PartnerField = nameof(partner);
    public const string TargetsField = nameof(targets);
    public const string EncounterField = nameof(encounter);
    public const string VoiceField = nameof(voice);
    public const string SessionField = nameof(session);
    public const string SettingsField = nameof(settings);
    public const string IntroCutsceneField = nameof(introCutscene);
    public const string SeatViewField = nameof(seatView);
    public const string ScreenViewField = nameof(screenView);
    public const string PinballField = nameof(pinball);
    public const string PcPowerField = nameof(pcPower);
    public const string DialogField = nameof(dialog);
    public const string DialogTitleField = nameof(dialogTitle);
    public const string DialogTextField = nameof(dialogText);
    public const string DialogMessageField = nameof(dialogMessage);
    public const string CloseButtonField = nameof(closeButton);
    public const string SettingsButtonField = nameof(settingsButton);
    public const string CancelButtonField = nameof(cancelButton);
    public const string CloseLabelField = nameof(closeLabel);
    public const string SettingsLabelField = nameof(settingsLabel);
    public const string CancelLabelField = nameof(cancelLabel);

    public enum Mode { Room, Desktop, Operator, Pinball, Focus }

    [Header("The room")]
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private OperatorRoomHud hud;
    [Tooltip("What his PC shows (OperatorDesktop or the fake OS, OperatorOS).")]
    [SerializeField] private DesktopScreen desktop;
    [SerializeField] private Camera roomCamera;
    [SerializeField] private AudioListener roomListener;
    [Tooltip("His eyes sitting at the desk.")]
    [SerializeField] private Transform seatView;
    [Tooltip("Right in front of the monitor (the screen fills the view).")]
    [SerializeField] private Transform screenView;
    [SerializeField] private PinballMachine pinball;
    [Tooltip("Optional: the PC's power button (off = he can't sit and use it).")]
    [SerializeField] private PcPower pcPower;

    [Header("The camera program")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private AudioListener viewListener;
    [Tooltip("The operator's UI (HUDs, map, commands): drawn only in the camera program.")]
    [SerializeField] private Canvas operatorCanvas;
    [Tooltip("On only in the camera program (camera system, clicks, combat keys, test keys, the CCTV filter...).")]
    [SerializeField] private Behaviour[] operatorBehaviours = new Behaviour[0];
    [SerializeField] private SecurityCameraSystem cameraSystem;

    [Header("Helena")]
    [SerializeField] private PartnerRest partnerRest;
    [SerializeField] private Transform partner;
    [SerializeField] private CommandTargets targets;
    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private PartnerVoice voice;

    [Header("Shared")]
    [SerializeField] private GameSession session;
    [SerializeField] private CameraSettings settings;
    [Tooltip("Optional: the cutscene of the first time at the cameras. Until it has played, sitting at the PC " +
             "doesn't get her up (the cutscene does).")]
    [SerializeField] private Cutscene introCutscene;

    [Header("Close dialog")]
    [SerializeField] private CanvasGroup dialog;
    [SerializeField] private TMP_Text dialogTitle;
    [SerializeField] private TMP_Text dialogText;
    [SerializeField] private TMP_Text dialogMessage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text closeLabel;
    [SerializeField] private TMP_Text settingsLabel;
    [SerializeField] private TMP_Text cancelLabel;

    [Header("Input")]
    [SerializeField] private InputAction back = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
    [Tooltip("A click gives the cursor back to the game (the editor frees it with Esc).")]
    [SerializeField] private InputAction relock = new InputAction("Relock", InputActionType.Button, "<Mouse>/leftButton");

    private Mode mode = Mode.Room;
    private bool transitioning;
    private bool dialogOpen;
    private FocusInteractable focused;

    public Mode Current => mode;
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private GameLanguage Language => Pt ? GameLanguage.Portuguese : GameLanguage.English;

    private void Awake()
    {
        if (player == null || hud == null || desktop == null || roomCamera == null)
        {
            Debug.LogError($"{name}: OperatorStation is missing references (run the Last Terminal scene menu).", this);
            enabled = false;
            return;
        }
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseProgram);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        SetDialog(false);
    }

    private void OnEnable()
    {
        back.Enable(); relock.Enable();
        back.performed += OnBack;
        if (cameraSystem != null) cameraSystem.EscapeRequested += OnOperatorEscape;
        if (desktop != null) { desktop.ProgramRequested += EnterOperator; desktop.StandRequested += StandUp; desktop.ShutdownRequested += OnShutdown; }
    }

    private void OnDisable()
    {
        back.performed -= OnBack;
        back.Disable(); relock.Disable();
        if (cameraSystem != null) cameraSystem.EscapeRequested -= OnOperatorEscape;
        if (desktop != null) { desktop.ProgramRequested -= EnterOperator; desktop.StandRequested -= StandUp; desktop.ShutdownRequested -= OnShutdown; }
    }

    private void Start()
    {
        bool atPc = HasOperator && session != null && session.WasOperating;
        if (atPc)
        {
            if (screenView != null) player.SnapViewTo(screenView.position, screenView.rotation);
            Apply(Mode.Operator);
        }
        else
        {
            Apply(Mode.Room);
            if (partnerRest != null) partnerRest.Rest(true);   // she waits in her cell
        }
    }

    private bool HasOperator => cameraSystem != null && viewCamera != null;

    // ------------------------------------------------------------------ switching
    // the PC in his room: sit down, lean in to the screen, the camera program opens
    public void SitAtPc()
    {
        if (transitioning || mode != Mode.Room) return;
        if (pcPower != null && !pcPower.IsPowered)
        {
            hud.ShowThought(Pt ? "Está desligado. O botão deve ficar no gabinete." : "It's off. The button must be on the case.");
            return;
        }
        transitioning = true;
        player.SetControl(false);
        hud.SetVisible(false);
        player.SetArmsVisible(false);
        player.BlendViewTo(seatView.position, seatView.rotation, 0.7f, () =>
            player.BlendViewTo(screenView.position, screenView.rotation, 0.45f, () =>
            {
                transitioning = false;
                if (HasOperator) EnterOperator();
                else Apply(Mode.Desktop);
            }));
    }

    private void EnterOperator()
    {
        if (!HasOperator) return;
        Apply(Mode.Operator);
        bool intro = introCutscene != null && !introCutscene.HasPlayed;
        if (partnerRest != null && partnerRest.IsResting && !intro)
        {
            partnerRest.Wake();
            if (voice != null) voice.Say(PartnerLine.OperatorBack);
        }
    }

    private void StandUp()
    {
        if (transitioning || mode != Mode.Desktop) return;
        transitioning = true;
        desktop.SetOpen(false);
        player.BlendViewTo(seatView.position, seatView.rotation, 0.45f, () =>
            player.BlendViewHome(0.6f, () =>
            {
                transitioning = false;
                Apply(Mode.Room);
            }));
    }

    // "Shut down" on the OS: the PC goes off, he gets up
    private void OnShutdown()
    {
        if (transitioning || mode != Mode.Desktop) return;
        if (pcPower != null) pcPower.PowerOff();
        StandUp();
    }

    // ------------------------------------------------------------------ looking at something up close
    public void Focus(FocusInteractable target)
    {
        if (transitioning || mode != Mode.Room || target == null || target.ViewPoint == null) return;
        transitioning = true;
        player.SetControl(false);
        hud.SetVisible(false);
        player.SetArmsVisible(false);
        Transform view = target.ViewPoint;
        player.BlendViewTo(view.position, view.rotation, target.BlendTime, () =>
        {
            transitioning = false;
            focused = target;
            Apply(Mode.Focus);
            target.BeginFocus(player);
        });
    }

    public void ExitFocus()
    {
        if (transitioning || mode != Mode.Focus) return;
        transitioning = true;
        FocusInteractable was = focused;
        focused = null;
        if (was != null) was.EndFocus(player);
        hud.SetHint(null);
        player.BlendViewHome(0.5f, () =>
        {
            transitioning = false;
            Apply(Mode.Room);
        });
    }

    // the pinball machine in his room
    public void PlayPinball()
    {
        if (transitioning || mode != Mode.Room || pinball == null) return;
        transitioning = true;
        player.SetControl(false);
        hud.SetVisible(false);
        player.SetArmsVisible(false);
        Transform view = pinball.ViewPoint;
        player.BlendViewTo(view.position, view.rotation, 0.6f, () =>
        {
            transitioning = false;
            Apply(Mode.Pinball);
            pinball.SetPlaying(true);
        });
    }

    private void ExitPinball()
    {
        if (transitioning || mode != Mode.Pinball) return;
        transitioning = true;
        pinball.SetPlaying(false);
        hud.SetHint(null);
        player.BlendViewHome(0.5f, () =>
        {
            transitioning = false;
            Apply(Mode.Room);
        });
    }

    private void Apply(Mode next)
    {
        mode = next;
        bool op = next == Mode.Operator;
        foreach (Behaviour b in operatorBehaviours) if (b != null) b.enabled = op;
        if (operatorCanvas != null) operatorCanvas.enabled = op;
        if (viewCamera != null) viewCamera.enabled = op;
        if (viewListener != null) viewListener.enabled = op;
        roomCamera.enabled = !op;
        if (roomListener != null) roomListener.enabled = !op;
        if (!op) SetDialog(false);
        desktop.SetProgramAvailable(HasOperator);
        desktop.SetOpen(next == Mode.Desktop);
        hud.SetVisible(next == Mode.Room);
        if (next != Mode.Focus) hud.SetHint(next == Mode.Pinball && pinball != null ? pinball.Hint(Language) : null);
        if (next != Mode.Room) hud.HideNote();
        player.SetControl(next == Mode.Room);
        player.SetArmsVisible(next == Mode.Room);
        LockCursor(next == Mode.Room || next == Mode.Pinball);
        if (session != null) session.SetOperating(op);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void Update()
    {
        // the editor frees the cursor on Esc: a click takes it back
        if ((mode == Mode.Room || mode == Mode.Pinball) && !transitioning && Cursor.lockState != CursorLockMode.Locked && relock.WasPressedThisFrame())
            LockCursor(true);
        if (mode == Mode.Focus && !transitioning && focused != null) focused.UpdateFocus(player);
    }

    // ------------------------------------------------------------------ Esc
    private void OnBack(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;
        switch (mode)
        {
            case Mode.Room:
                hud.HideNote();
                break;
            case Mode.Desktop:
                if (!desktop.CloseTopWindow()) StandUp();
                break;
            case Mode.Pinball:
                ExitPinball();
                break;
            case Mode.Focus:
                if (focused == null || !focused.HandleBack(player)) ExitFocus();
                break;
        }
    }

    // Esc in the camera program (SecurityCameraSystem): the close dialog
    private void OnOperatorEscape()
    {
        if (mode != Mode.Operator || transitioning) return;
        SetDialog(!dialogOpen);
    }

    private void SetDialog(bool open)
    {
        dialogOpen = open;
        if (cameraSystem != null) cameraSystem.SetOverlayOpen(open);
        if (dialog == null) return;
        dialog.alpha = open ? 1f : 0f;
        dialog.interactable = open;
        dialog.blocksRaycasts = open;
        if (!open) return;
        if (dialogTitle != null) dialogTitle.text = "CCTV.EXE";
        if (dialogText != null) dialogText.text = Pt ? "Fechar o programa de operador?" : "Close the operator program?";
        if (dialogMessage != null) dialogMessage.text = "";
        if (closeLabel != null) closeLabel.text = Pt ? "FECHAR" : "CLOSE";
        if (settingsLabel != null) settingsLabel.text = Pt ? "CONFIGURAÇÕES" : "SETTINGS";
        if (cancelLabel != null) cancelLabel.text = Pt ? "CANCELAR" : "CANCEL";
    }

    private void OnCloseProgram()
    {
        if (!CanLeave(out string reason))
        {
            if (dialogMessage != null) dialogMessage.text = reason;
            OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Beep, 0.6f);
            return;
        }
        SetDialog(false);
        if (partnerRest != null) partnerRest.Rest(false);
        if (voice != null) voice.Say(PartnerLine.OperatorAway);
        Apply(Mode.Desktop);
    }

    private void OnSettings()
    {
        SetDialog(false);
        if (cameraSystem != null) cameraSystem.SetSettingsOpen(true);
    }

    private void OnCancel() => SetDialog(false);

    // she can be left alone: safe zone, no fight, not in the middle of something
    public bool CanLeave(out string reason)
    {
        reason = null;
        if (encounter != null && encounter.InCombat)
        {
            reason = Pt ? "Helena está lutando. Você não pode deixá-la agora." : "Helena is fighting. You can't leave her now.";
            return false;
        }
        Room room = targets != null && partner != null ? targets.RoomOf(partner.position) : null;
        if (room == null || !room.SafeZone)
        {
            reason = Pt ? "Helena não está numa zona segura (as salas de salvar são seguras)." : "Helena isn't in a safe zone (save rooms are safe).";
            return false;
        }
        if (partnerRest != null && !partnerRest.CanRest)
        {
            reason = Pt ? "Helena está ocupada. Espere um pouco." : "Helena is busy. Wait a moment.";
            return false;
        }
        return true;
    }
}
