using System;
using UnityEngine;
using UnityEngine.AI;

// "Look at the bed": she walks up to the thing (Examinable), turns to it and says what she thinks of it.
// Any other command stops it (PartnerCommands calls Stop).
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerExaminer : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string InteractorField = nameof(interactor);
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string VoiceField = nameof(voice);
    public const string StoryField = nameof(story);

    // Her interactor (walks), animator and voice.
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerVoice voice;
    [Tooltip("Optional: the flags of the things she looked at.")]
    [SerializeField] private StoryProgress story;
    [Tooltip("Already this close: she only turns to it (m).")]
    [SerializeField] private float closeEnough = 1.6f;
    // Facing speed (deg/s) and when she gives up getting there (s).
    [SerializeField] private float faceSpeed = 300f;
    [SerializeField] private float giveUpAfter = 15f;

    // Not examining / walking to it / turning to it.
    private enum State { None, Walking, Facing }

    // Her agent, the state, the thing and the time spent.
    private NavMeshAgent agent;
    private State state;
    private Examinable current;
    private float timer;

    // Busy examining; raised when she has commented on something.
    public bool IsBusy => state != State.None;
    public event Action<Examinable> Examined;

    // Checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (interactor == null || partnerAnimator == null || voice == null)
        {
            Debug.LogError($"{name}: PartnerExaminer is missing references (run the Last Terminal scene menu).", this);
            enabled = false;
        }
    }

    // false = she can't go there now (busy) or there is no floor near it
    public bool Begin(Examinable target)
    {
        if (!enabled || target == null) return false;
        Stop();
        current = target;
        timer = 0f;
        if (Flat(target.LookPoint - transform.position).magnitude <= closeEnough && target.StandPoint == null)
        {
            StartFacing();
            return true;
        }
        if (!TryStand(target, out Vector3 stand) || !interactor.MoveTo(stand)) { current = null; return false; }
        state = State.Walking;
        return true;
    }

    // Stops (any other command calls this).
    public void Stop()
    {
        if (state == State.Facing) agent.updateRotation = true;
        state = State.None;
        current = null;
    }

    // Where she stands to look at it (its marked spot, else in front of it on the NavMesh).
    private bool TryStand(Examinable target, out Vector3 stand)
    {
        if (target.StandPoint != null)
        {
            stand = target.StandPoint.position;
            if (!NavMesh.SamplePosition(stand, out NavMeshHit marked, 1f, NavMesh.AllAreas)) return false;
            stand = marked.position;
            return true;
        }
        Vector3 p = target.LookPoint;
        Vector3 side = Flat(transform.position - p);
        if (side.sqrMagnitude < 0.01f) side = Flat(target.transform.forward);
        Vector3 wanted = p + side.normalized * target.StandDistance;
        wanted.y = transform.position.y;
        if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) { stand = hit.position; return true; }
        if (NavMesh.SamplePosition(new Vector3(p.x, transform.position.y, p.z), out hit, 2f, NavMesh.AllAreas)) { stand = hit.position; return true; }
        stand = transform.position;
        return false;
    }

    // Walk, then turn, then comment (gives up if she is taken or it takes too long).
    private void Update()
    {
        if (state == State.None) return;
        if (current == null || !current.isActiveAndEnabled || partnerAnimator.IsDead || partnerAnimator.IsScripted) { Stop(); return; }   // (a cutscene took her)
        timer += Time.deltaTime;
        if (timer > giveUpAfter) { Stop(); return; }
        if (state == State.Walking)
        {
            if (agent.pathPending || partnerAnimator.IsTurning) return;
            bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.02f;
            if (arrived) StartFacing();
            return;
        }
        Vector3 to = Flat(current.LookPoint - transform.position);
        if (to.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, faceSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, want) > 3f) return;
        }
        Comment();
    }

    // Stops the agent and turns her by hand.
    private void StartFacing()
    {
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
        state = State.Facing;
    }

    // Says the thing's next line and sets its seen flag.
    private void Comment()
    {
        Examinable x = current;
        Stop();
        string line = x.NextLine(voice.Language, out AudioClip clip);
        if (!string.IsNullOrEmpty(line)) voice.SayText(line, clip);
        if (story != null && !string.IsNullOrEmpty(x.SeenFlag)) story.Set(x.SeenFlag);
        Examined?.Invoke(x);
    }

    // A vector flattened onto the floor.
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
