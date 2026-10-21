using UnityEngine;
using UnityEngine.AI;

// "Look at the camera": she walks to the spot of the room the operator's camera sees best (in frame, not too far,
// nothing in between), turns to the camera and looks into it, waiting ("What?"). She stays like that until the next
// command (PartnerCommands calls Stop) or until she is moved.
// "Turn around" / "turn your left (right) side": she turns where she stands (turn clips) to show the camera the operator
// is watching her back or a side (to read the number on her back...), and stays like that the same way.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerCameraPose : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string TargetsField = nameof(targets);
    public const string VoiceField = nameof(voice);

    public enum Result { Ok, NoCamera, TooFar, NoSpot }
    public enum Facing { Front, Back, LeftSide, RightSide }
    private enum State { None, Walking, Facing, Posing, Turning }

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private CommandTargets targets;
    [SerializeField] private PartnerVoice voice;
    [Header("Where she stands")]
    [Tooltip("Best distance from the camera (m).")]
    [SerializeField] private float preferredDistance = 3.2f;
    [Tooltip("Spots tried on a grid this size (m), this far from the walls.")]
    [SerializeField] private float grid = 0.5f;
    [SerializeField] private float wallMargin = 0.6f;
    [Tooltip("Her feet and head must be inside this part of the picture (viewport, 0..1).")]
    [SerializeField] private Vector2 frameMargin = new Vector2(0.1f, 0.06f);
    [Header("Pose")]
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField, Range(0f, 1f)] private float lookWeight = 0.9f;
    [Tooltip("Still posing after this long: she says something again (s).")]
    [SerializeField] private float waitLineDelay = 8f;

    private NavMeshAgent agent;
    private NavMeshPath path;
    private State state;
    private Vector3 eye;
    private float timer;
    private bool saidWait;
    private bool lookAtEye = true;   // posing toward the camera (not when showing her back / a side)
    private Vector3 turnForward;

    public bool IsActive => state != State.None;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        if (partnerAnimator == null || interactor == null || cameraSystem == null || targets == null || voice == null)
        {
            Debug.LogError($"{name}: PartnerCameraPose is missing references (run Last Terminal/4).", this);
            enabled = false;
        }
    }

    public Result Begin()
    {
        Stop();
        SecurityCamera cam = cameraSystem.Active;
        Camera view = cameraSystem.ViewCamera;
        if (cam == null || view == null) return Result.NoCamera;
        Room here = targets.CurrentRoom(transform.position);
        Room room = cam.Room;
        if (room == null) return Result.NoCamera;
        if (here != null && !targets.IsNextTo(here, room)) return Result.TooFar;
        eye = view.transform.position;
        lookAtEye = true;

        if (!FindSpot(room, view, out Vector3 spot)) return Result.NoSpot;
        if (!interactor.MoveTo(spot)) return Result.NoSpot;
        state = State.Walking;
        timer = 0f;
        saidWait = false;
        return Result.Ok;
    }

    // turns where she stands: front / back / a side toward the camera being watched
    public Result Turn(Facing facing)
    {
        Stop();
        Camera view = cameraSystem.ViewCamera;
        if (cameraSystem.Active == null || view == null) return Result.NoCamera;
        eye = view.transform.position;
        Vector3 toCam = Flat(eye - transform.position);
        if (toCam.sqrMagnitude < 0.01f) return Result.NoCamera;
        toCam.Normalize();
        switch (facing)
        {
            case Facing.Back: turnForward = -toCam; break;
            case Facing.LeftSide: turnForward = Quaternion.Euler(0f, 90f, 0f) * toCam; break;    // her left toward the camera
            case Facing.RightSide: turnForward = Quaternion.Euler(0f, -90f, 0f) * toCam; break;
            default: turnForward = toCam; break;
        }
        lookAtEye = facing == Facing.Front;
        if (agent.isOnNavMesh) agent.ResetPath();
        partnerAnimator.BeginScripted();
        float angle = Vector3.SignedAngle(Flat(transform.forward), turnForward, Vector3.up);
        if (Mathf.Abs(angle) > 45f) partnerAnimator.PlayScriptedTurn(angle);
        state = State.Turning;
        timer = 0f;
        saidWait = true;   // no "are you just going to stare" when she shows her back
        return Result.Ok;
    }

    public void Stop()
    {
        if (state == State.None) return;
        if (state == State.Turning) partnerAnimator.EndScripted();
        state = State.None;
        partnerAnimator.SetLookTarget(eye, 0f);
        if (agent != null) agent.updateRotation = true;
    }

    // the best framed spot of the room she can walk to
    private bool FindSpot(Room room, Camera view, out Vector3 best)
    {
        best = Vector3.zero;
        float bestScore = float.MaxValue;
        Rect r = room.FloorRect;
        float y = transform.position.y;
        for (float x = r.xMin + wallMargin; x <= r.xMax - wallMargin; x += grid)
            for (float z = r.yMin + wallMargin; z <= r.yMax - wallMargin; z += grid)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, y, z), out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                Vector3 feet = view.WorldToViewportPoint(p + Vector3.up * 0.1f);
                Vector3 head = view.WorldToViewportPoint(p + Vector3.up * 1.7f);
                if (feet.z < 1.5f || !InFrame(feet) || !InFrame(head)) continue;
                if (!CommandTargets.Clear(eye, p + Vector3.up * 1.5f) || !CommandTargets.Clear(eye, p + Vector3.up * 0.8f)) continue;
                Vector3 body = view.WorldToViewportPoint(p + Vector3.up * 0.9f);
                float score = Mathf.Abs(body.x - 0.5f) * 3f + Mathf.Abs(body.y - 0.45f) * 1.5f +
                              Mathf.Abs(feet.z - preferredDistance) * 0.6f + Flat(p - transform.position).magnitude * 0.05f;
                if (score >= bestScore || !PathExists(p)) continue;
                bestScore = score;
                best = p;
            }
        return bestScore < float.MaxValue;
    }

    private bool InFrame(Vector3 v) =>
        v.x > frameMargin.x && v.x < 1f - frameMargin.x && v.y > frameMargin.y && v.y < 1f - frameMargin.y;

    private bool PathExists(Vector3 to) =>
        agent.isOnNavMesh && NavMesh.CalculatePath(transform.position, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;

    private void Update()
    {
        if (state == State.None) return;
        timer += Time.deltaTime;
        switch (state)
        {
            case State.Turning:
                // the turn clip first (root motion), then the rest smoothly
                if (partnerAnimator.IsTurning) return;
                Quaternion goal = Quaternion.LookRotation(turnForward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, goal) > 2f) return;
                partnerAnimator.EndScripted();
                agent.updateRotation = false;
                state = State.Posing;
                if (lookAtEye) partnerAnimator.SetLookTarget(eye, lookWeight);
                break;
            case State.Walking:
                if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.05f) return;
                state = State.Facing;
                agent.updateRotation = false;
                break;
            case State.Facing:
            case State.Posing:
                if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.1f) { Stop(); return; }   // moved by someone else
                if (!lookAtEye) { transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(turnForward), turnSpeed * Time.deltaTime); break; }
                partnerAnimator.SetLookTarget(eye, lookWeight);
                Vector3 to = Flat(eye - transform.position);
                if (to.sqrMagnitude < 0.0001f) return;
                Quaternion want = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
                if (state == State.Facing && Quaternion.Angle(transform.rotation, want) < 8f)
                {
                    state = State.Posing;
                    timer = 0f;
                    voice.Say(PartnerLine.LookWhat);
                }
                else if (state == State.Posing && !saidWait && timer > waitLineDelay)
                {
                    saidWait = true;
                    voice.Say(PartnerLine.LookWaiting);
                }
                break;
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
