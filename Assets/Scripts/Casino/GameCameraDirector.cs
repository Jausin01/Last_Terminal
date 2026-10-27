using UnityEngine;

// The close camera of the casino games she plays standing (slot machines, roulette), like the blackjack table camera:
// when she gets to the game it appears there (ButtonPanel.TryGetGameView) and the operator's view cuts to it;
// when she leaves, the view goes back to the camera it came from and it disappears. The operator can still switch
// cameras meanwhile (Q / E); it only cuts when she arrives at a game.
public class GameCameraDirector : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string KeypadUserField = nameof(keypadUser);
    public const string GameCameraField = nameof(gameCamera);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string TargetsField = nameof(targets);

    // Her button-panel user (tells which game she is standing at).
    [SerializeField] private PartnerKeypadUser keypadUser;
    [Tooltip("A SecurityCamera without a model (off until a game starts), one of the camera system's cameras.")]
    [SerializeField] private SecurityCamera gameCamera;
    // The operator's camera system (the view cuts to the game camera and back).
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [Tooltip("Optional: the room of the game (map, \"subject in view\").")]
    [SerializeField] private CommandTargets targets;

    // The game the camera shows now, and the camera that was on before.
    private ButtonPanel showing;
    private SecurityCamera before;

    // Checks the references; the game camera starts hidden.
    private void Awake()
    {
        if (keypadUser == null || gameCamera == null || cameraSystem == null)
        {
            Debug.LogError($"{name}: GameCameraDirector is missing references (run the casino menu).", this);
            enabled = false;
            return;
        }
        gameCamera.gameObject.SetActive(false);
    }

    // Every frame (after her movement): she arrived at a game = show it; she left = hide.
    private void LateUpdate()
    {
        ButtonPanel at = keypadUser.IsAtKeypad ? keypadUser.Current : null;
        if (at == showing) return;
        if (at != null && at.TryGetGameView(out Vector3 from, out Vector3 lookAt))
        {
            if (showing == null) before = cameraSystem.Active;
            gameCamera.gameObject.SetActive(true);
            gameCamera.Frame(from, lookAt, targets != null ? targets.RoomOf(at.transform.position) : null);
            showing = at;
            cameraSystem.Switch(gameCamera);
            return;
        }
        Hide();
    }

    // Back to the previous camera; the game camera hides.
    private void Hide()
    {
        if (showing == null) return;
        showing = null;
        if (cameraSystem.Active == gameCamera)
        {
            if (before != null && before != gameCamera) cameraSystem.Switch(before);
            else cameraSystem.Switch(0);
        }
        gameCamera.gameObject.SetActive(false);
    }
}
