using UnityEngine;
using UnityEngine.AI;

// Helena alone: while the operator is away from the camera program she stops where she is and sits on the floor
// (PartnerAnimator.FloorSit), and gets up when he comes back. OperatorStation decides when (only in a safe zone).
public class PartnerRest : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string AgentField = nameof(agent);
    public const string HealthField = nameof(health);

    // Her animator (floor-sit pose), her agent (stopped) and her health.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;
    // Blend times to sit down / stand up (s).
    [SerializeField] private float sitBlend = 0.9f;
    [SerializeField] private float standBlend = 0.7f;

    // Sitting and waiting now.
    private bool resting;

    // Read by the station and the cutscenes.
    public bool IsResting => resting;

    // free to sit: not in the middle of something (a door, a seat, a pick up, an attack, hurt)
    public bool CanRest => partnerAnimator != null && !partnerAnimator.IsScripted && !partnerAnimator.IsDead
                           && !partnerAnimator.IsPickingUp && !partnerAnimator.IsAttacking && !partnerAnimator.IsHitReacting
                           && (health == null || health.Current > 0);

    // immediate: already sitting (a scene that starts with the operator in his room)
    public void Rest(bool immediate)
    {
        if (resting || partnerAnimator == null) return;
        resting = true;
        if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        partnerAnimator.BeginScripted();
        partnerAnimator.PlayFloorSit(immediate ? 0f : sitBlend);
    }

    // someone else takes her from here (a cutscene starts with her still sitting): no longer resting, pose kept
    public void Release() => resting = false;

    // He came back: she stands up and is free again.
    public void Wake()
    {
        if (!resting) return;
        resting = false;
        partnerAnimator.EndFloorSit(standBlend);
        partnerAnimator.EndScripted();
    }
}
