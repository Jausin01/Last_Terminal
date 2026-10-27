using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
// His own room as a scene of its own (Operator.unity): the camera program lives in a location scene (Location Scene),
// loaded over the room the first time CCTV.EXE opens; it hands itself over through the OperatorLink (OperatorLocation)
// and keeps running while he is in the room. In the camera program the location is the active scene (its lighting).
// Settings: a window of his PC (SettingsWindow), also opened from the "close the program?" dialog over the cameras.
// Waking up: the first time in the room (until its cutscene has been seen) he wakes up on the cot - a cinema
// cutscene with its own camera; at its end (or Esc) he stands where it left him (Wake End) and walks around.
public class OperatorStation : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
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
    public const string WakeCutsceneField = nameof(wakeCutscene);
    public const string WakeEndField = nameof(wakeEnd);
    public const string LinkField = nameof(link);
    public const string LocationSceneField = nameof(locationScene);
    public const string SettingsWindowField = nameof(settingsWindow);
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

    // Where the operator is (see the top of the file).
    public enum Mode { Room, Desktop, Operator, Pinball, Focus }

    // The first-person player and his room HUD.
    [Header("The room")]
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private OperatorRoomHud hud;
    [Tooltip("What his PC shows (OperatorDesktop or the fake OS, OperatorOS).")]
    [SerializeField] private DesktopScreen desktop;
    // The room's camera and listener (on everywhere except in the camera program).
    [SerializeField] private Camera roomCamera;
    [SerializeField] private AudioListener roomListener;
    [Tooltip("His eyes sitting at the desk.")]
    [SerializeField] private Transform seatView;
    [Tooltip("Right in front of the monitor (the screen fills the view).")]
    [SerializeField] private Transform screenView;
    // The pinball machine of his room.
    [SerializeField] private PinballMachine pinball;
    [Tooltip("Optional: the PC's power button (off = he can't sit and use it).")]
    [SerializeField] private PcPower pcPower;

    // The camera program's view camera and listener.
    [Header("The camera program")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private AudioListener viewListener;
    [Tooltip("The operator's UI (HUDs, map, commands): drawn only in the camera program.")]
    [SerializeField] private Canvas operatorCanvas;
    [Tooltip("On only in the camera program (camera system, clicks, combat keys, test keys, the CCTV filter...).")]
    [SerializeField] private Behaviour[] operatorBehaviours = new Behaviour[0];
    // The CCTV camera system (Esc in it opens the close dialog).
    [SerializeField] private SecurityCameraSystem cameraSystem;

    // Helena: her rest (sitting while he's away), her position, the rooms, the fight and her voice.
    [Header("Helena")]
    [SerializeField] private PartnerRest partnerRest;
    [SerializeField] private Transform partner;
    [SerializeField] private CommandTargets targets;
    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private PartnerVoice voice;

    // Shared: the play session (was he operating?) and the settings.
    [Header("Shared")]
    [SerializeField] private GameSession session;
    [SerializeField] private CameraSettings settings;
    [Tooltip("Optional: the cutscene of the first time at the cameras. Until it has played, sitting at the PC " +
             "doesn't get her up (the cutscene does).")]
    // The first-time cameras cutscene.
    [SerializeField] private Cutscene introCutscene;

    [Header("Waking up")]
    [Tooltip("Optional: the cutscene of his first time in the room (he wakes up on the cot). Played when the room " +
             "starts, until it has been seen. Its director's Cinema Camera shows it.")]
    // The wake-up cutscene of his first time in the room.
    [SerializeField] private Cutscene wakeCutscene;
    [Tooltip("Where he stands when it ends (on the floor; the blue arrow = where he looks).")]
    [SerializeField] private Transform wakeEnd;

    [Header("The camera program in another scene")]
    [Tooltip("Shared with the location scenes (their OperatorLocation hands itself over through it).")]
    [SerializeField] private OperatorLink link;
    [Tooltip("The location loaded over the room the first time CCTV.EXE opens (in the build profile). Empty = none.")]
    [SerializeField] private string locationScene = "";
    [Tooltip("Optional: the settings window (else the camera program's own settings panel).")]
    [SerializeField] private SettingsWindow settingsWindow;

    // The "close the program?" dialog: panel, texts, buttons and their labels.
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

    // Esc.
    [Header("Input")]
    [SerializeField] private InputAction back = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
    [Tooltip("A click gives the cursor back to the game (the editor frees it with Esc).")]
    [SerializeField] private InputAction relock = new InputAction("Relock", InputActionType.Button, "<Mouse>/leftButton");

    // Current mode, a view transition running, the dialog open, the thing looked at up close.
    private Mode mode = Mode.Room;
    private bool transitioning;
    private bool dialogOpen;
    private FocusInteractable focused;
    // The location scene: being loaded, and the scene once loaded.
    private bool loadingLocation;
    private Scene locationSceneLoaded;

    // Current mode (read by cutscenes and the session).
    public Mode Current => mode;
    // Language helpers.
    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;
    private GameLanguage Language => Pt ? GameLanguage.Portuguese : GameLanguage.English;

    // Checks the references; wires the dialog buttons.
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

    // Inputs, the link (registers itself, waits for a location), the desktop's and camera program's requests.
    private void OnEnable()
    {
        back.Enable(); relock.Enable();
        back.performed += OnBack;
        if (link != null) { link.SetStation(this); link.LocationArrived += Attach; }
        if (settingsWindow != null) settingsWindow.Closed += OnSettingsClosed;
        if (cameraSystem != null) cameraSystem.EscapeRequested += OnOperatorEscape;
        if (desktop != null) { desktop.ProgramRequested += EnterOperator; desktop.StandRequested += StandUp; desktop.ShutdownRequested += OnShutdown; }
    }

    // Stops listening and unregisters.
    private void OnDisable()
    {
        back.performed -= OnBack;
        if (link != null) { link.ClearStation(this); link.LocationArrived -= Attach; }
        if (settingsWindow != null) settingsWindow.Closed -= OnSettingsClosed;
        back.Disable(); relock.Disable();
        if (cameraSystem != null) cameraSystem.EscapeRequested -= OnOperatorEscape;
        if (desktop != null) { desktop.ProgramRequested -= EnterOperator; desktop.StandRequested -= StandUp; desktop.ShutdownRequested -= OnShutdown; }
    }

    // Starts at the PC (came from another scene while operating) or in the room (Helena waits; the wake-up cutscene the first time).
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
            if (wakeCutscene != null && !wakeCutscene.HasPlayed) BeginWake();
        }
    }

    // ------------------------------------------------------------------ waking up (cinema cutscene)
    private void BeginWake()
    {
        CutsceneDirector director = wakeCutscene.Director;
        Camera cinema = director != null ? director.CinemaCamera : null;
        if (cinema == null) return;
        transitioning = true;
        player.SetControl(false);
        player.SetArmsVisible(false);
        hud.SetVisible(false);
        roomCamera.enabled = false;
        if (roomListener != null) roomListener.enabled = false;
        cinema.enabled = true;
        if (cinema.TryGetComponent(out AudioListener ears)) ears.enabled = true;
        LockCursor(true);
        director.Finished += OnWakeFinished;
        if (!wakeCutscene.Play()) OnWakeFinished(wakeCutscene);
    }

    // The wake-up cutscene ended: the room camera back, he stands where it left him.
    private void OnWakeFinished(Cutscene cutscene)
    {
        if (cutscene != wakeCutscene) return;
        CutsceneDirector director = wakeCutscene.Director;
        director.Finished -= OnWakeFinished;
        Camera cinema = director.CinemaCamera;
        cinema.enabled = false;
        if (cinema.TryGetComponent(out AudioListener ears)) ears.enabled = false;
        if (wakeEnd != null) player.PlaceAt(wakeEnd.position, wakeEnd.eulerAngles.y);
        transitioning = false;
        Apply(Mode.Room);
    }

    // The camera program is here (own scene or a loaded location); a location can be loaded.
    private bool HasOperator => cameraSystem != null && viewCamera != null;
    private bool CanLoadLocation => !HasOperator && !string.IsNullOrEmpty(locationScene) && Application.CanStreamedLevelBeLoaded(locationScene);

    // ------------------------------------------------------------------ the location over the room
    private IEnumerator LoadLocation()
    {
        loadingLocation = true;
        transitioning = true;
        desktop.SetStatus(Pt ? "CCTV.EXE: conectando às câmeras..." : "CCTV.EXE: connecting to the cameras...", 30f);
        AsyncOperation load = SceneManager.LoadSceneAsync(locationScene, LoadSceneMode.Additive);
        while (load != null && !load.isDone) yield return null;
        float waited = 0f;
        while (!HasOperator && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
        loadingLocation = false;
        transitioning = false;
        if (!HasOperator)
        {
            desktop.SetStatus(Pt ? "CCTV.EXE: o local não respondeu." : "CCTV.EXE: the location didn't answer.", 6f);
            Debug.LogWarning($"{name}: {locationScene} loaded but has no OperatorLocation (rebuild it with its Last Terminal menu).", this);
            yield break;
        }
        desktop.SetStatus("", 0.01f);
        if (mode == Mode.Desktop) EnterOperator();
    }

    // a location scene handed its camera program over (OperatorLocation, on its Awake)
    private void Attach(OperatorLocation location)
    {
        if (location == null) return;
        if (cameraSystem != null) cameraSystem.EscapeRequested -= OnOperatorEscape;
        viewCamera = location.ViewCamera;
        viewListener = location.ViewListener;
        operatorCanvas = location.OperatorCanvas;
        operatorBehaviours = location.OperatorBehaviours;
        cameraSystem = location.CameraSystem;
        partnerRest = location.PartnerRest;
        partner = location.Partner;
        targets = location.Targets;
        encounter = location.Encounter;
        voice = location.Voice;
        locationSceneLoaded = location.gameObject.scene;
        if (cameraSystem != null) cameraSystem.EscapeRequested += OnOperatorEscape;
        desktop.SetProgramAvailable(true);
    }

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

    // The camera program: loads the location first if needed; Helena gets up (unless the intro cutscene will).
    private void EnterOperator()
    {
        if (!HasOperator)
        {
            if (CanLoadLocation && !loadingLocation) StartCoroutine(LoadLocation());
            return;
        }
        Apply(Mode.Operator);
        bool intro = introCutscene != null && !introCutscene.HasPlayed;
        if (partnerRest != null && partnerRest.IsResting && !intro)
        {
            partnerRest.Wake();
            if (voice != null) voice.Say(PartnerLine.OperatorBack);
        }
    }

    // Desktop -> sits back, then stands: the room.
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

    // Leaves the close-up view: back to his eyes, the room.
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

    // Leaves the pinball: back to his eyes, the room.
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

    // Switches everything to a mode: the operator's behaviours / canvas / camera, the room camera, the desktop, the HUD, the cursor, the active scene.
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
        desktop.SetProgramAvailable(HasOperator || CanLoadLocation);
        // the camera program sees the location with its own lighting (the active scene's ambient / fog)
        if (locationSceneLoaded.IsValid() && locationSceneLoaded.isLoaded)
            SceneManager.SetActiveScene(op ? locationSceneLoaded : gameObject.scene);
        desktop.SetOpen(next == Mode.Desktop);
        hud.SetVisible(next == Mode.Room);
        if (next != Mode.Focus) hud.SetHint(next == Mode.Pinball && pinball != null ? pinball.Hint(Language) : null);
        if (next != Mode.Room) hud.HideNote();
        player.SetControl(next == Mode.Room);
        player.SetArmsVisible(next == Mode.Room);
        LockCursor(next == Mode.Room || next == Mode.Pinball);
        if (session != null) session.SetOperating(op);
    }

    // Locks / frees the mouse cursor.
    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Cursor relock after the editor freed it; forwards frames to a close-up view.
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
        if (settingsWindow != null && settingsWindow.IsOpen) { settingsWindow.Close(); return; }
        SetDialog(!dialogOpen);
    }

    // Opens / closes the close dialog (texts in the current language).
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

    // CLOSE: only if Helena can be left alone; she sits and waits; the desktop.
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

    // SETTINGS: the settings window over the cameras (or the camera program's own panel).
    private void OnSettings()
    {
        SetDialog(false);
        if (settingsWindow != null)
        {
            settingsWindow.Open();
            if (cameraSystem != null) cameraSystem.SetOverlayOpen(true);   // the cameras wait under it
            return;
        }
        if (cameraSystem != null) cameraSystem.SetSettingsOpen(true);
    }

    // The settings window closed: the cameras take input again.
    private void OnSettingsClosed()
    {
        if (mode == Mode.Operator && cameraSystem != null) cameraSystem.SetOverlayOpen(false);
    }

    // CANCEL: closes the dialog.
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
