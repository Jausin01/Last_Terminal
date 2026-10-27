using UnityEngine;
using UnityEngine.EventSystems;

// The camera program of a location scene (the asylum...) when his room is another scene. Loaded over his room (the
// OperatorLink has a station: the PC's CCTV.EXE loaded it), it starts switched off - no view, no listener, no camera
// keys, no UI, its own EventSystem off (the room has one) - and hands itself to the station, which turns it on when
// he sits at the cameras. Played on its own (no room open), it does nothing: the scene is the camera program at once.
// Built by the location scene menus (Last Terminal/17...).
public class OperatorLocation : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LinkField = nameof(link);
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
    public const string EventSystemField = nameof(eventSystem);

    // The link asset, and what the station turns on / off: view camera, audio listener, the operator UI canvas.
    [SerializeField] private OperatorLink link;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private AudioListener viewListener;
    [SerializeField] private Canvas operatorCanvas;
    [Tooltip("On only in the camera program (camera system, clicks, combat keys, the microphone, the CCTV filter...).")]
    [SerializeField] private Behaviour[] operatorBehaviours = new Behaviour[0];
    // Things the station reads from this scene: cameras, Helena (rest, position, voice), targets, combat.
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private PartnerRest partnerRest;
    [SerializeField] private Transform partner;
    [SerializeField] private CommandTargets targets;
    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private PartnerVoice voice;
    [Tooltip("This scene's EventSystem: off when the room's is there.")]
    [SerializeField] private EventSystem eventSystem;

    // Read by the station.
    public Camera ViewCamera => viewCamera;
    public AudioListener ViewListener => viewListener;
    public Canvas OperatorCanvas => operatorCanvas;
    public Behaviour[] OperatorBehaviours => operatorBehaviours;
    public SecurityCameraSystem CameraSystem => cameraSystem;
    public PartnerRest PartnerRest => partnerRest;
    public Transform Partner => partner;
    public CommandTargets Targets => targets;
    public CombatEncounter Encounter => encounter;
    public PartnerVoice Voice => voice;
    // loaded over his room (else: played on its own)
    public bool Linked { get; private set; }

    // Loaded over his room: everything of the camera program starts off; hands itself to the station.
    private void Awake()
    {
        if (link == null || link.Station == null) return;
        Linked = true;
        foreach (Behaviour b in operatorBehaviours) if (b != null) b.enabled = false;
        if (operatorCanvas != null) operatorCanvas.enabled = false;
        if (viewCamera != null) viewCamera.enabled = false;
        if (viewListener != null) viewListener.enabled = false;
        if (eventSystem != null) eventSystem.gameObject.SetActive(false);
        link.SetLocation(this);
    }

    // Unregisters from the link.
    private void OnDestroy()
    {
        if (link != null) link.ClearLocation(this);
    }
}
