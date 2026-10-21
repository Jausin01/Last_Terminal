using UnityEngine;
using UnityEngine.AI;

// Someone a cutscene moves: Helena (her PartnerAnimator) or any character (an Animator, optionally a NavMeshAgent).
// Walk to a point (NavMesh), turn, jump to a place, play any animation clip (through the CutsceneA / CutsceneB
// states) or a state of its Animator by name, and back to normal.
// A character other than Helena that plays clips needs CutsceneA / CutsceneB states in its controller and the two
// empty clips here (Last Terminal/11 adds them to a controller).
// Hands on targets (a steering wheel, a door): SetHandTargets moves the hands there by IK, blended in / out (the
// Animator's layer needs IK Pass on).
public class CutsceneActor : MonoBehaviour
{
    public const string DisplayNameField = nameof(displayName);
    public const string AgentField = nameof(agent);
    public const string AnimatorField = nameof(animator);
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string SlotAField = nameof(slotA);
    public const string SlotBField = nameof(slotB);
    public const string RestField = nameof(rest);

    [Tooltip("Name shown on its subtitles.")]
    [SerializeField] private string displayName = "???";
    [SerializeField] private Color nameColor = new Color(0.85f, 0.25f, 0.3f);
    [Tooltip("Optional: walks on the NavMesh. Without one it glides.")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [Tooltip("Helena: her animator does the clips (leave the slots empty).")]
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private AnimationClip slotA;
    [SerializeField] private AnimationClip slotB;
    [Tooltip("Helena: sitting on the floor while the operator was away - a cutscene can start with her like that.")]
    [SerializeField] private PartnerRest rest;
    [Tooltip("Without a NavMeshAgent: glide speed (m/s).")]
    [SerializeField] private float glideSpeed = 1.4f;
    [Tooltip("Animator float set to the speed while moving (empty = none).")]
    [SerializeField] private string speedParameter = "Speed";

    private CutsceneClipSlots slots;
    private float normalSpeed;
    private bool inCutscene;
    private bool animating;     // a clip / state is playing (scripted)
    private bool gliding;
    private Vector3 glideTarget;
    private int speedParamId;
    private bool hasSpeedParam;
    private int restState;      // another character: the state it was in when the cutscene began
    private Transform ikLeft, ikRight;
    private float ikWeight, ikGoal, ikSpeed = 4f;

    public bool IsResting => rest != null && rest.IsResting;
    public string DisplayName => displayName;
    public Color NameColor => nameColor;

    private void Awake()
    {
        if (partnerAnimator == null && animator != null)
        {
            slots = new CutsceneClipSlots(animator, slotA, slotB);
            speedParamId = Animator.StringToHash(speedParameter);
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == speedParamId && p.type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
        }
        if (agent != null) normalSpeed = agent.speed;
    }

    public void BeginCutscene()
    {
        if (inCutscene) return;
        inCutscene = true;
        if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        if (IsResting)
        {
            // still sitting on the floor (scripted): the cutscene owns that pose now ("Voltar ao normal" gets her up)
            rest.Release();
            animating = true;
        }
        if (partnerAnimator == null && animator != null && animator.isActiveAndEnabled)
            restState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
    }

    public void EndCutscene()
    {
        if (!inCutscene) return;
        inCutscene = false;
        SetHandTargets(null, null, 0f, 0f);
        StopAnimation(0.3f);
        if (agent != null) agent.speed = normalSpeed;
        gliding = false;
    }

    // ------------------------------------------------------------------ moving
    // speedFactor: 1 = her walk, 2 = hurrying
    public void MoveTo(Vector3 point, float speedFactor)
    {
        StopAnimation(0.25f);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = normalSpeed * Mathf.Max(0.1f, speedFactor);
            agent.SetDestination(point);
            return;
        }
        gliding = true;
        glideTarget = point;
    }

    public bool HasArrived(Vector3 point)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            if (agent.pathPending) return false;
            if (!agent.hasPath) return true;
            return agent.remainingDistance <= agent.stoppingDistance + 0.08f && agent.velocity.sqrMagnitude < 0.02f;
        }
        return !gliding;
    }

    public void StopMoving()
    {
        if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        gliding = false;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        StopMoving();
        if (agent != null && agent.isOnNavMesh) agent.Warp(position);
        else transform.position = position;
        transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
    }

    // turns toward a direction; true once facing it
    public bool TurnToward(Vector3 direction, float degreesPerSecond)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-4f) return true;
        Quaternion want = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, want) < 1f;
    }

    // ------------------------------------------------------------------ hands (IK)
    // left / right: where the hands go (null = that hand free); weight 0..1; blend: seconds to get there
    public void SetHandTargets(Transform left, Transform right, float weight, float blend)
    {
        if (left != null || right != null) { ikLeft = left; ikRight = right; }
        ikGoal = (left != null || right != null) ? Mathf.Clamp01(weight) : 0f;
        ikSpeed = blend > 0f ? 1f / blend : 1000f;
        if (blend <= 0f) ikWeight = ikGoal;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || ikWeight <= 0f) return;
        Hand(AvatarIKGoal.LeftHand, ikLeft);
        Hand(AvatarIKGoal.RightHand, ikRight);
    }

    private void Hand(AvatarIKGoal goal, Transform target)
    {
        animator.SetIKPositionWeight(goal, target != null ? ikWeight : 0f);
        if (target != null) animator.SetIKPosition(goal, target.position);
    }

    private void Update()
    {
        if (ikWeight != ikGoal)
        {
            ikWeight = Mathf.MoveTowards(ikWeight, ikGoal, ikSpeed * Time.deltaTime);
            if (ikWeight <= 0f) { ikLeft = null; ikRight = null; }
        }
        if (!gliding) return;
        Vector3 to = glideTarget - transform.position;
        to.y = 0f;
        float step = glideSpeed * Time.deltaTime;
        if (to.magnitude <= step) { transform.position = new Vector3(glideTarget.x, transform.position.y, glideTarget.z); gliding = false; }
        else
        {
            transform.position += to.normalized * step;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
        }
        if (hasSpeedParam) animator.SetFloat(speedParamId, gliding ? glideSpeed : 0f);
    }

    // ------------------------------------------------------------------ animation
    public void PlayClip(AnimationClip clip, float blend)
    {
        if (clip == null) return;
        StopMoving();
        if (partnerAnimator != null)
        {
            if (!animating) partnerAnimator.BeginScripted();
            partnerAnimator.PlayCutsceneClip(clip, blend);
        }
        else if (slots != null && slots.Ready) slots.Play(clip, blend);
        else { Debug.LogWarning($"{name}: no CutsceneA / CutsceneB states for clips (Last Terminal/11 on its controller).", this); return; }
        animating = true;
    }

    public bool PlayState(string state, float blend)
    {
        StopMoving();
        if (partnerAnimator != null)
        {
            if (!animating) partnerAnimator.BeginScripted();
            animating = true;
            if (partnerAnimator.PlayStateByName(state, blend)) return true;
            Debug.LogWarning($"{name}: no state '{state}' in her Animator.", this);
            return false;
        }
        if (animator == null) return false;
        int id = Animator.StringToHash(state);
        if (!animator.HasState(0, id)) { Debug.LogWarning($"{name}: no state '{state}' in its Animator.", this); return false; }
        if (slots != null) slots.Stop();
        animator.CrossFadeInFixedTime(id, blend, 0);
        animating = true;
        return true;
    }

    public bool IsClipDone => partnerAnimator != null ? partnerAnimator.IsCutsceneClipDone : slots == null || slots.IsDone;

    // back to normal (her walk / idle; another character: its default state)
    public void StopAnimation(float blend)
    {
        if (!animating) return;
        animating = false;
        if (partnerAnimator != null)
        {
            partnerAnimator.EndCutsceneClip(blend);
            partnerAnimator.EndScripted();
            return;
        }
        if (slots != null) slots.Stop();
        if (animator != null && animator.isActiveAndEnabled && restState != 0 && animator.HasState(0, restState))
            animator.CrossFadeInFixedTime(restState, blend, 0);
    }
}
