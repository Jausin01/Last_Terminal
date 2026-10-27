using UnityEngine;

// The enemy's Animator (lives on the model, like PartnerAnimator): one controller for every enemy (Last Terminal/7),
// an AnimatorOverrideController swaps the clips per type (the killer's upright walk...).
// Locomotion: Gait 0 idle, 1 walk, 2 run, played at MoveSpeed so the feet match the agent.
// Attacks, scream, feeding, getting up and the deaths are single states entered from code (EnemyBrain).
// Root motion is off (applyRootMotion false): the NavMeshAgent moves it, the clips' travel is dropped.
// Getting hit has no clip: the spine / chest / head are bent away from the hit for a moment (procedural flinch),
// which works for every type and every weapon.
[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    // parameters and states (built by Last Terminal/7)
    public const string GaitParam = "Gait";
    public const string MoveSpeedParam = "MoveSpeed";
    public const string AttackSpeedParam = "AttackSpeed";
    public const string LocomotionState = "Locomotion";
    public const string ScreamState = "Scream";
    public const string FeedState = "Feed";
    public const string RiseState = "Rise";
    public const string DeathBackState = "DeathBack";
    public const string DeathFrontState = "DeathFront";
    public static readonly string[] AttackStates = { "Swipe", "Smash", "Bite" };   // EnemyAttackSlot order

    // Hashes of the parameters and states above.
    private static readonly int GaitId = Animator.StringToHash(GaitParam);
    private static readonly int MoveSpeedId = Animator.StringToHash(MoveSpeedParam);
    private static readonly int AttackSpeedId = Animator.StringToHash(AttackSpeedParam);
    private static readonly int LocomotionId = Animator.StringToHash(LocomotionState);
    private static readonly int ScreamId = Animator.StringToHash(ScreamState);
    private static readonly int FeedId = Animator.StringToHash(FeedState);
    private static readonly int RiseId = Animator.StringToHash(RiseState);
    private static readonly int DeathBackId = Animator.StringToHash(DeathBackState);
    private static readonly int DeathFrontId = Animator.StringToHash(DeathFrontState);
    private static readonly int[] AttackIds =
    {
        Animator.StringToHash(AttackStates[0]), Animator.StringToHash(AttackStates[1]), Animator.StringToHash(AttackStates[2]),
    };

    // Crossfade time into states entered from code (s).
    [SerializeField] private float blendTime = 0.2f;
    [Header("Flinch (hit reaction)")]
    [Tooltip("Bend of the upper body for a light hit (deg); heavy hits bend twice as much.")]
    [SerializeField] private float flinchAngle = 14f;
    // Flinch spring: how fast it shakes and how fast it settles.
    [SerializeField] private float flinchFrequency = 3.5f;
    [SerializeField] private float flinchDamping = 7f;

    // The animator and the bones bent by the flinch.
    private Animator animator;
    private Transform spine, chest, head;
    private int playing;          // state entered from code (0 = locomotion)
    private float flinch;         // current bend (deg)
    // Flinch spring speed and the axis it bends around.
    private float flinchVelocity;
    private Vector3 flinchAxis = Vector3.right;

    // The model's Animator (EnemyBrain reads it).
    public Animator Animator => animator;
    public bool IsPlayingState => playing != 0;   // in a state entered from code (attack, scream, rise, feed, death)

    // Gets the animator and the spine / chest / head bones.
    private void Awake()
    {
        animator = GetComponent<Animator>();
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    // Locomotion: gait (0 idle, 1 walk, 2 run) and clip playback speed.
    public void SetGait(float gait, float moveSpeed)
    {
        animator.SetFloat(GaitId, gait);
        animator.SetFloat(MoveSpeedId, moveSpeed);
    }

    // Playback speed of the attack clips.
    public void SetAttackSpeed(float speed) => animator.SetFloat(AttackSpeedId, speed);

    // States entered from code (crossfaded): attack, scream, getting up, feeding, death.
    public void PlayAttack(EnemyAttackSlot slot) => Play(AttackIds[(int)slot], blendTime);
    public void PlayScream() => Play(ScreamId, blendTime);
    public void PlayRise() => Play(RiseId, 0.3f);
    public void PlayFeed() => Play(FeedId, 0.6f);   // back to feeding after giving up (it kneels down in the blend)
    public void PlayDeath(bool backward) => Play(backward ? DeathBackId : DeathFrontId, 0.15f);

    // straight into a pose, no blend (start of the scene, a save loaded)
    public void SnapFeeding() => Snap(FeedId, 0f);
    public void SnapLocomotion() => Snap(LocomotionId, 0f);
    public void SnapDead(bool backward) => Snap(backward ? DeathBackId : DeathFrontId, 1f);

    // Back to locomotion (default blend if negative).
    public void ToLocomotion(float blend = -1f)
    {
        playing = 0;
        animator.CrossFadeInFixedTime(LocomotionId, blend < 0f ? blendTime : blend, 0);
    }

    // normalized time of the state entered from code: -1 while still blending into it
    public float PlayingTime
    {
        get
        {
            if (playing == 0 || animator.IsInTransition(0)) return -1f;
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
            return s.shortNameHash == playing ? s.normalizedTime : -1f;
        }
    }

    // the hit came from this direction (world, toward the enemy): bend away from it
    public void Flinch(Vector3 fromAttacker, bool heavy)
    {
        fromAttacker.y = 0f;
        if (fromAttacker.sqrMagnitude < 0.0001f) fromAttacker = -transform.forward;
        flinchAxis = Vector3.Cross(Vector3.up, fromAttacker.normalized);
        flinchVelocity += (heavy ? 2f : 1f) * flinchAngle * 2f * Mathf.PI * flinchFrequency * 1.4f;   // peak ~ flinchAngle
    }

    // Crossfades into a state entered from code.
    private void Play(int state, float blend)
    {
        playing = state;
        animator.CrossFadeInFixedTime(state, blend, 0);
    }

    // Jumps into a state at a normalized time, evaluated at once.
    private void Snap(int state, float time)
    {
        playing = state == LocomotionId ? 0 : state;
        animator.Play(state, 0, time);
        animator.Update(0f);
    }

    // After the animation: integrates the flinch spring and bends the upper body.
    private void LateUpdate()
    {
        // damped spring back to straight
        float k = Mathf.Pow(2f * Mathf.PI * flinchFrequency, 2f);
        flinchVelocity += (-k * flinch - 2f * flinchDamping * flinchVelocity) * Time.deltaTime;
        flinch += flinchVelocity * Time.deltaTime;
        if (Mathf.Abs(flinch) < 0.01f && Mathf.Abs(flinchVelocity) < 0.01f) { flinch = 0f; flinchVelocity = 0f; return; }
        Bend(spine, 0.35f);
        Bend(chest, 0.4f);
        Bend(head, 0.25f);
    }

    // Bends one bone by its share of the flinch.
    private void Bend(Transform bone, float share)
    {
        if (bone == null) return;
        bone.rotation = Quaternion.AngleAxis(flinch * share, flinchAxis) * bone.rotation;
    }
}
