using UnityEngine;

// The close camera of the casino games she plays standing (slot machines, roulette), like the blackjack table camera:
// when she gets to the game it appears there (ButtonPanel.TryGetGameView) and the operator's view cuts to it;
// when she leaves, the view goes back to the camera it came from and it disappears. The operator can still switch
// cameras meanwhile (Q / E); it only cuts when she arrives at a game.
public class GameCameraDirector : MonoBehaviour
{
    public const string KeypadUserField = nameof(keypadUser);
    public const string GameCameraField = nameof(gameCamera);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string TargetsField = nameof(targets);

    [SerializeField] private PartnerKeypadUser keypadUser;
    [Tooltip("A SecurityCamera without a model (off until a game starts), one of the camera system's cameras.")]
    [SerializeField] private SecurityCamera gameCamera;
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [Tooltip("Optional: the room of the game (map, \"subject in view\").")]
    [SerializeField] private CommandTargets targets;

    private ButtonPanel showing;
    private SecurityCamera before;

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
