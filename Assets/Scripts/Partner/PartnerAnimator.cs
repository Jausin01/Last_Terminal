using System;
using UnityEngine;
using UnityEngine.AI;

public enum PickupHeight { Low = 0, Medium = 1 }

// Drives the partner Animator from her NavMeshAgent:
// - "Speed" for the locomotion blend tree
// - turn in place before walking when the path starts far from where she is facing
// - pick-up animations (low / medium), with right-hand IK and head look-at on the item
// - armed locomotion: 2D blend by local velocity (strafe / backpedal) + upper-body weapon layer (idle / aim)
// - melee attacks: full-body clips tagged "Attack", speed driven from outside (PartnerMelee)
// - scripted control (save terminal, doors, blackjack table): the caller drives walk speed, root motion, the seat,
//   dealer and door clips
// - evasion (dodge, duck): full-body clips played from code (PartnerEvasion), the duck held down and played backward
//   to get up; the weapon layer stays on (she keeps her gun up)
// - damage (PartnerDamage): hit reactions (light / heavy; armed versions hold the weapon of her hand: the weapon layer
//   is off meanwhile), injured locomotion blended in by "Injured", death (falls back; from a crouch she crumples)
// Lives on the Animator object (needs OnAnimatorMove / OnAnimatorIK). The agent can be on this object or on a parent.
[RequireComponent(typeof(Animator))]
public class PartnerAnimator : MonoBehaviour
{
    public const string SpeedParam = "Speed";
    public const string TurnParam = "Turn";
    public const string TurnAngleParam = "TurnAngle";
    public const string TurnTag = "Turn";
    public const string PickupParam = "Pickup";
    public const string PickupHeightParam = "PickupHeight";
    public const string PickupTag = "Pickup";
    public const string ArmedParam = "Armed";
    public const string AimingParam = "Aiming";
    public const string MoveXParam = "MoveX";
    public const string MoveZParam = "MoveZ";
    public const string UpperBodyLayer = "UpperBody";
    public const string WeaponTypeParam = "WeaponType";
    public const string AttackParam = "Attack";
    public const string AttackIdParam = "AttackId";
    public const string AttackSpeedParam = "AttackSpeed";
    public const string AttackTag = "Attack";
    public const string ReloadingParam = "Reloading";
    public const string ReloadAbortParam = "ReloadAbort";
    public const string ReloadTag = "Reload";
    public const string SeatedParam = "Seated";
    public const string SeatTag = "Seat";
    public const string SitDownState = "SitDown";
    public const string SeatedState = "Typing";
    public const string StandUpState = "StandUp";
    public const string LocomotionState = "Locomotion";
    public const string ArmedLocomotionState = "ArmedLocomotion";
    public const string DoorSpeedParam = "DoorSpeed";
    public const string DoorTag = "Door";
    public const string DoorInwardsState = "DoorInwards";
    public const string DoorOutwardsState = "DoorOutwards";
    public const string DoorLockedState = "DoorLocked";
    // clip names = FBX file names (HelenaImport)
    public const string DoorInwardsClip = "HelenaOpenDoorInwards";
    public const string DoorOutwardsClip = "HelenaOpenDoorOutwards";
    public const string DoorLockedClip = "HelenaTryToOpenLockedDoor";
    public const string SeatActivityParam = "SeatActivity";
    public const string InjuredParam = "Injured";
    public const string HitTag = "Hit";
    public const string DeadTag = "Dead";
    public const string DeathState = "Death";
    public const string FallenState = "Fallen";
    // keypad (PartnerKeypadUser): the button clip cut in three (HelenaImport)
    public const string FloorSitState = "FloorSit";      // sitting on the floor, waiting (PartnerRest)
    public const string FloorSitClip = "HelenaFloorSit";
    public const string KeypadTag = "Keypad";
    public static readonly string[] KeypadStates = { "KeypadRaise", "KeypadPress", "KeypadLower" };
    public static readonly string[] KeypadClips = { "HelenaKeypadRaise", "HelenaKeypadPress", "HelenaKeypadLower" };
    public const string KeypadClipFile = "HelenaButton_Pushing";
    // hit clips (= state names): unarmed, and the armed versions "<prefix>_<hold>" (WeaponHoldType order)
    public static readonly string[] HitClips = { "HelenaStomachHit", "HelenaRibHit", "HelenaHitReaction", "HelenaHitReaction2" };
    public static readonly string[] ArmedHitPrefixes = { "HelenaHitStomach", "HelenaHitRib", "HelenaHitReaction", "HelenaHitReaction2" };
    public static readonly string[] HoldNames = { "Pistol", "Shotgun", "Blade", "Blunt" };
    public const string DeathClip = "HelenaFallingBackDeath";
    public const string FallenClip = "HelenaFallenIdle";
    public const string EvadeSpeedParam = "EvadeSpeed";
    public const string EvadeTag = "Evade";
    public static readonly string[] EvadeStates = { "Dodge", "Duck" };
    public static readonly string[] EvadeClips = { "HelenaDodgingBack", "HelenaDucking" };   // FBX file names
    // dealer (seated at the blackjack table): states, clip names = FBX file names (HelenaImport)
    public static readonly string[] DealerStates = { "DealerIdle", "DealerPickDeck", "DealerDeal", "DealerHold", "DealerPutBack", "DealerCheck" };
    public static readonly string[] DealerClips = { "HelenaDealerIdle", "HelenaDealerPickDeck", "HelenaDealerDeal", "HelenaDealerHold", "HelenaDealerPutBack", "HelenaDealerCheck" };
    public const string AgentField = nameof(agent);
    public const string CutsceneSlotAField = nameof(cutsceneSlotA);
    public const string CutsceneSlotBField = nameof(cutsceneSlotB);

    // thresholds of the Turn blend tree (Unity yaw: left = negative)
    public const float SlotLeft180 = -180f;
    public const float SlotLeft90 = -90f;
    public const float SlotRight90 = 90f;
    public const float SlotRight180 = 180f;

    [SerializeField] private NavMeshAgent agent;
    [Tooltip("The empty clips of the CutsceneA / CutsceneB states (cutscenes play any clip through them).")]
    [SerializeField] private AnimationClip cutsceneSlotA;
    [SerializeField] private AnimationClip cutsceneSlotB;
    [SerializeField] private float speedDampTime = 0.1f;
    [Tooltip("Seconds from standing to a full walk. The legs lead: she never moves faster than the walk being shown, " +
             "so she steps off instead of sliding off.")]
    [SerializeField] private float stepInTime = 0.45f;

    [Header("Turn in place")]
    [Tooltip("Turn before walking when standing and the path is at least this far (deg).")]
    [SerializeField] private float idleTurnThreshold = 60f;
    [Tooltip("Stop and turn while walking only for very sharp direction changes (deg).")]
    [SerializeField] private float movingTurnThreshold = 135f;
    [Tooltip("Above this angle the 180 clips are used (deg).")]
    [SerializeField] private float bigTurnAngle = 135f;
    [SerializeField] private float turnTimeout = 3f;

    [Header("Real root rotation of each clip (deg, measured from the Mixamo files)")]
    [SerializeField] private float clipLeft90 = -122.6f;
    [SerializeField] private float clipRight90 = 95.6f;
    [SerializeField] private float clipLeft180 = -176.1f;
    [SerializeField] private float clipRight180 = 175.1f;

    [Header("Pick up (grab moment = normalized time of the clip, measured from the Mixamo files)")]
    [SerializeField, Range(0f, 1f)] private float grabTimeLow = 0.204f;
    [SerializeField, Range(0f, 1f)] private float grabTimeMedium = 0.447f;
    [SerializeField] private float pickupTimeout = 10f;
    [Tooltip("Right-hand IK toward the item around the grab moment (0 = off).")]
    [SerializeField, Range(0f, 1f)] private float handIKWeight = 0.9f;
    [SerializeField] private float handIKBlendIn = 0.45f;
    [SerializeField] private float handIKBlendOut = 0.35f;
    [Tooltip("The IK goal is the wrist; the palm is about this much below it when grabbing.")]
    [SerializeField] private float wristAboveItem = 0.06f;
    [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.6f;

    [Header("Armed")]
    [Tooltip("Seconds to blend the upper-body pistol layer in/out.")]
    [SerializeField] private float upperBodyBlendTime = 0.2f;
    [Tooltip("Seconds to go from the idle hold to the aim hold of the weapon (match the Animator transition).")]
    [SerializeField] private float aimBlendTime = 0.15f;

    [Header("Doors")]
    [Tooltip("Blend into / out of the door clips (s).")]
    [SerializeField] private float doorBlendTime = 0.25f;

    [Header("Damage")]
    [Tooltip("Blend into a hit reaction / out of it (s).")]
    [SerializeField] private float hitBlendIn = 0.08f;
    [SerializeField] private float hitBlendOut = 0.3f;
    [Tooltip("Back to standing from this point of a hit clip (normalized; light, heavy).")]
    [SerializeField] private Vector2 hitExit = new Vector2(0.8f, 0.7f);
    [SerializeField] private float hitTimeout = 4f;

    [Header("Melee")]
    [SerializeField] private float attackTimeout = 8f;

    private static readonly int SpeedId = Animator.StringToHash(SpeedParam);
    private static readonly int TurnId = Animator.StringToHash(TurnParam);
    private static readonly int TurnAngleId = Animator.StringToHash(TurnAngleParam);
    private static readonly int TurnTagId = Animator.StringToHash(TurnTag);
    private static readonly int PickupId = Animator.StringToHash(PickupParam);
    private static readonly int PickupHeightId = Animator.StringToHash(PickupHeightParam);
    private static readonly int PickupTagId = Animator.StringToHash(PickupTag);
    private static readonly int ArmedId = Animator.StringToHash(ArmedParam);
    private static readonly int AimingId = Animator.StringToHash(AimingParam);
    private static readonly int MoveXId = Animator.StringToHash(MoveXParam);
    private static readonly int MoveZId = Animator.StringToHash(MoveZParam);
    private static readonly int WeaponTypeId = Animator.StringToHash(WeaponTypeParam);
    private static readonly int AttackId = Animator.StringToHash(AttackParam);
    private static readonly int AttackIdId = Animator.StringToHash(AttackIdParam);
    private static readonly int AttackSpeedId = Animator.StringToHash(AttackSpeedParam);
    private static readonly int AttackTagId = Animator.StringToHash(AttackTag);
    private static readonly int ReloadingId = Animator.StringToHash(ReloadingParam);
    private static readonly int ReloadAbortId = Animator.StringToHash(ReloadAbortParam);
    private static readonly int ReloadTagId = Animator.StringToHash(ReloadTag);
    private static readonly int SeatedId = Animator.StringToHash(SeatedParam);
    private static readonly int SeatTagId = Animator.StringToHash(SeatTag);
    private static readonly int SeatActivityId = Animator.StringToHash(SeatActivityParam);
    private static readonly int[] DealerStateIds = Array.ConvertAll(DealerStates, Animator.StringToHash);
    private static readonly int SitDownId = Animator.StringToHash(SitDownState);
    private static readonly int SeatedStateId = Animator.StringToHash(SeatedState);
    private static readonly int StandUpId = Animator.StringToHash(StandUpState);
    private static readonly int LocomotionId = Animator.StringToHash(LocomotionState);
    private static readonly int ArmedLocomotionId = Animator.StringToHash(ArmedLocomotionState);
    private static readonly int DoorSpeedId = Animator.StringToHash(DoorSpeedParam);
    private static readonly int DoorTagId = Animator.StringToHash(DoorTag);
    private static readonly int KeypadTagId = Animator.StringToHash(KeypadTag);
    private static readonly int FloorSitId = Animator.StringToHash(FloorSitState);
    private static readonly int[] KeypadStateIds = Array.ConvertAll(KeypadStates, Animator.StringToHash);
    private static readonly int DoorInwardsId = Animator.StringToHash(DoorInwardsState);
    private static readonly int DoorOutwardsId = Animator.StringToHash(DoorOutwardsState);
    private static readonly int DoorLockedId = Animator.StringToHash(DoorLockedState);
    private static readonly int EvadeSpeedId = Animator.StringToHash(EvadeSpeedParam);
    private static readonly int InjuredId = Animator.StringToHash(InjuredParam);
    private static readonly int HitTagId = Animator.StringToHash(HitTag);
    private static readonly int DeathId = Animator.StringToHash(DeathState);
    private static readonly int FallenId = Animator.StringToHash(FallenState);
    private static readonly int[] HitIds = Array.ConvertAll(HitClips, Animator.StringToHash);
    private static readonly int[,] ArmedHitIds = BuildArmedHitIds();

    private static int[,] BuildArmedHitIds()
    {
        var ids = new int[ArmedHitPrefixes.Length, HoldNames.Length];
        for (int k = 0; k < ArmedHitPrefixes.Length; k++)
            for (int h = 0; h < HoldNames.Length; h++) ids[k, h] = Animator.StringToHash(ArmedHitPrefixes[k] + "_" + HoldNames[h]);
        return ids;
    }
    private static readonly int EvadeTagId = Animator.StringToHash(EvadeTag);
    private static readonly int[] EvadeStateIds = Array.ConvertAll(EvadeStates, Animator.StringToHash);

    private Animator animator;
    private Transform body;

    private bool turning;
    private bool enteredTurnState;
    private float rotationScale = 1f;
    private float turnTimer;
    private bool hasTurnDestination;
    private Vector3 turnDestination;

    private bool picking;
    private bool enteredPickupState;
    private bool grabbed;
    private float grabNormalized;
    private float pickupTimer;
    private float pickupTime;     // seconds into the pickup clip
    private float pickupLength;
    private Vector3 grabTarget;

    private bool armed;
    private bool aiming;
    private int upperLayer = -1;
    private float upperWeight;
    private float aimBlend;

    private bool attacking;
    private bool enteredAttackState;
    private float attackTimer;
    private float attackProgress = -1f;
    private int currentAttackHash;
    private int previousAttackHash;

    private bool hitPlaying;
    private bool hitEntered;
    private bool hitHeavy;
    private float hitTimer;
    private bool dead;
    private float locomotionScale = 1f;
    private bool holdFacing;
    private float stepSpeed;          // speed the walk is ramping to (m/s), ahead of the agent
    private bool wasLocomotion;

    private bool scripted;
    private float scriptedSpeed;
    private bool scriptedRootMotion;

    private readonly float[] doorClipLength = { 1f, 1f, 1f };   // by DoorClip
    private readonly float[] evadeClipLength = { 1f, 1f };      // by EvadeClip

    public event Action AttackFinished;
    public event Action PickupStarted;
    public event Action PickupGrabbed;
    public event Action PickupFinished;

    public bool IsPickingUp => picking;
    public bool IsTurning => turning;
    public bool IsAttacking => attacking;
    public bool IsScripted => scripted;
    public bool IsHitReacting => hitPlaying;
    public bool IsDead => dead;
    // normalized time of the attack clip (0..1), -1 while the attack state has not started yet
    public float AttackProgress => attackProgress;
    public bool IsArmed => armed;
    public bool IsAiming => aiming;
    public float AimBlend => aimBlend;   // 0 = weapon idle pose, 1 = aim pose (smoothed)
    public Transform RightHand => animator.GetBoneTransform(HumanBodyBones.RightHand);
    public Transform LeftHand => animator.GetBoneTransform(HumanBodyBones.LeftHand);
    public Transform GetBone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

    // Left hand: wrist position, fingers direction, palm direction (out of the palm) and wrist->middle knuckle length.
    public bool TryGetLeftHandFrame(out Vector3 wrist, out Vector3 fingers, out Vector3 palm, out float handLength)
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            wrist = fingers = palm = Vector3.zero;
            handLength = 0f;
            return false;
        }
        wrist = hand.position;
        Vector3 toMiddle = middle.position - hand.position;
        handLength = toMiddle.magnitude;
        fingers = toMiddle.normalized;
        Vector3 a = index.position - little.position;
        Vector3 across = (a - Vector3.Dot(a, fingers) * fingers).normalized;
        palm = -Vector3.Cross(fingers, across).normalized;   // left hand: mirror of the right
        return true;
    }

    // Left hand basis: wrist, fingers, across (toward the index) and out of the palm — the frame reload props are stored in.
    public bool TryGetLeftHandBasis(out Vector3 wrist, out Vector3 fingers, out Vector3 across, out Vector3 palm)
    {
        if (!TryGetLeftHandFrame(out wrist, out fingers, out palm, out _))
        {
            across = Vector3.zero;
            return false;
        }
        across = Vector3.Cross(fingers, palm).normalized;   // palm = -Cross(fingers, across)
        return true;
    }

    // Center of the right palm and the direction it faces (world), from the finger bones.
    public bool TryGetRightPalm(out Vector3 center, out Vector3 normal)
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            center = hand != null ? hand.position : transform.position;
            normal = Vector3.down;
            return false;
        }
        Vector3 fingers = (middle.position - hand.position).normalized;
        Vector3 across = (index.position - little.position).normalized;
        center = Vector3.Lerp(hand.position, middle.position, 0.6f);
        normal = Vector3.Cross(fingers, across).normalized;   // right hand: points out of the palm
        return true;
    }

    // Frame built from the right-hand bones: origin between wrist and middle knuckle,
    // fingers = toward the fingers, across = toward the index (perpendicular), normal = out of the palm.
    public bool TryGetRightHandFrame(out Vector3 origin, out Vector3 fingers, out Vector3 across, out Vector3 normal)
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            origin = fingers = across = normal = Vector3.zero;
            return false;
        }
        fingers = (middle.position - hand.position).normalized;
        Vector3 a = index.position - little.position;
        across = (a - Vector3.Dot(a, fingers) * fingers).normalized;
        normal = Vector3.Cross(fingers, across).normalized;
        origin = Vector3.Lerp(hand.position, middle.position, 0.5f);
        return true;
    }

    public void SetArmed(bool value)
    {
        armed = value;
        if (!armed) aiming = false;
        animator.SetBool(ArmedId, armed);
        animator.SetBool(AimingId, aiming);
    }

    // Picks the upper-body poses of the weapon (WeaponHoldType value: 0 pistol, 1 shotgun, 2 blade, 3 blunt).
    public void SetHoldType(WeaponHoldType holdType)
    {
        animator.SetFloat(WeaponTypeId, (float)holdType);
    }

    public void SetAiming(bool value)
    {
        aiming = armed && value;
        animator.SetBool(AimingId, aiming);
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        upperLayer = animator.GetLayerIndex(UpperBodyLayer);
        if (agent == null)
        {
            Debug.LogError($"{name}: PartnerAnimator has no NavMeshAgent assigned.", this);
            enabled = false;
            return;
        }
        body = agent.transform;
        CacheDoorClipLengths();
        cutsceneClips = new CutsceneClipSlots(animator, cutsceneSlotA, cutsceneSlotB);
    }

    private void CacheDoorClipLengths()
    {
        if (animator.runtimeAnimatorController == null) return;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == DoorInwardsClip) doorClipLength[(int)DoorClip.Inwards] = clip.length;
            else if (clip.name == DoorOutwardsClip) doorClipLength[(int)DoorClip.Outwards] = clip.length;
            else if (clip.name == DoorLockedClip) doorClipLength[(int)DoorClip.Locked] = clip.length;
            else
            {
                int e = Array.IndexOf(EvadeClips, clip.name);
                if (e >= 0) evadeClipLength[e] = clip.length;
            }
        }
    }

    private void Update()
    {
        UpdateArmedLayer();
        if (dead) return;
        bool locomotionFrame = !hitPlaying && !scripted && !attacking && !picking && !turning;
        if (!locomotionFrame) wasLocomotion = false;
        if (hitPlaying)
        {
            animator.SetFloat(SpeedId, 0f, speedDampTime, Time.deltaTime);
            UpdateHit();
            return;
        }
        if (scripted)
        {
            if (turning) { UpdateTurn(); return; }
            animator.SetFloat(SpeedId, scriptedSpeed, speedDampTime, Time.deltaTime);
            return;
        }
        if (attacking)
        {
            animator.SetFloat(SpeedId, 0f, speedDampTime, Time.deltaTime);
            UpdateAttack();
            return;
        }
        if (picking)
        {
            animator.SetFloat(SpeedId, 0f, speedDampTime, Time.deltaTime);
            UpdatePickup();
            return;
        }
        if (turning)
        {
            UpdateTurn();
            return;
        }
        UpdateLocomotion();
        TryStartTurn();
    }

    // The walk starts first and the agent follows it: Speed ramps toward the speed the agent wants (stepInTime to a full
    // walk) and her velocity is held to the speed shown by the legs. Slowing down follows the agent (no sliding either).
    private void UpdateLocomotion()
    {
        float shown = animator.GetFloat(SpeedId) / locomotionScale;
        if (!wasLocomotion) stepSpeed = shown;   // back from a turn / hit / scripted move: go on from what is shown
        wasLocomotion = true;

        float actual = agent.velocity.magnitude;
        float wanted = agent.isStopped || agent.pathPending || !agent.hasPath ? 0f : agent.desiredVelocity.magnitude;
        float target = Mathf.Max(wanted, actual);
        if (target > stepSpeed)
            stepSpeed = Mathf.MoveTowards(stepSpeed, target, agent.speed / Mathf.Max(stepInTime, 0.01f) * Time.deltaTime);
        else stepSpeed = target;

        animator.SetFloat(SpeedId, stepSpeed * locomotionScale, speedDampTime, Time.deltaTime);
        shown = animator.GetFloat(SpeedId) / locomotionScale;
        if (actual > shown + 0.02f) agent.velocity *= shown / actual;
    }

    // Local velocity drives the armed 2D blend tree; the upper-body layer is on while armed (off while picking up).
    private void UpdateArmedLayer()
    {
        Vector3 local = body.InverseTransformDirection(agent.velocity) * locomotionScale;
        animator.SetFloat(MoveXId, local.x, speedDampTime, Time.deltaTime);
        animator.SetFloat(MoveZId, local.z, speedDampTime, Time.deltaTime);

        float aimStep = aimBlendTime > 0f ? Time.deltaTime / aimBlendTime : 1f;
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, aimStep);

        if (upperLayer < 0) return;
        float target = armed && !picking && !attacking && !hitPlaying && !dead ? 1f : 0f;
        float step = upperBodyBlendTime > 0f ? Time.deltaTime / upperBodyBlendTime : 1f;
        upperWeight = Mathf.MoveTowards(upperWeight, target, step);
        animator.SetLayerWeight(upperLayer, upperWeight);
    }

    // ------------------------------------------------------------------ reload (upper-body layer)
    // Reload clips live on the UpperBody layer (she can keep walking). The clip is chosen by WeaponType.
    public void SetReloading(bool value) => animator.SetBool(ReloadingId, value);

    // Leaves the reload clip at once (e.g. the shotgun reload interrupted by a shot).
    public void AbortReload()
    {
        animator.SetBool(ReloadingId, false);
        animator.SetTrigger(ReloadAbortId);
    }

    public void ClearReloadAbort() => animator.ResetTrigger(ReloadAbortId);

    // Normalized time of the reload clip being played (counts loops for the shell loop). False when none.
    public bool TryGetReloadTime(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (upperLayer < 0) return false;
        if (animator.IsInTransition(upperLayer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(upperLayer);
            if (next.tagHash != ReloadTagId) return false;   // blending out: done
            normalizedTime = next.normalizedTime;
            return true;
        }
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(upperLayer);
        if (info.tagHash != ReloadTagId) return false;
        normalizedTime = info.normalizedTime;
        return true;
    }

    public bool IsReloadPlaying => TryGetReloadTime(out _);

    // ------------------------------------------------------------------ melee attack
    // Full-body attack clip (the caller stops the agent). Its playback speed is driven by SetAttackSpeed.
    // Called again during an attack = combo: the Animator goes straight to the next attack state.
    public void PlayAttack(int attackId)
    {
        // chaining: ignore the current swing until the Animator is in the next one
        previousAttackHash = attacking && enteredAttackState ? currentAttackHash : 0;
        attacking = true;
        enteredAttackState = false;
        attackTimer = 0f;
        attackProgress = -1f;
        animator.SetInteger(AttackIdId, attackId);
        animator.SetTrigger(AttackId);
    }

    public void SetAttackSpeed(float speed)
    {
        animator.SetFloat(AttackSpeedId, speed);
    }

    private void UpdateAttack()
    {
        attackTimer += Time.deltaTime;
        bool found = TryGetAttackState(out AnimatorStateInfo info) && info.fullPathHash != previousAttackHash;
        if (found)
        {
            enteredAttackState = true;
            previousAttackHash = 0;
            currentAttackHash = info.fullPathHash;
            attackProgress = info.normalizedTime;
            return;
        }
        // left the attack (exit blend started) or never got there
        if (enteredAttackState || attackTimer > attackTimeout)
        {
            attacking = false;
            enteredAttackState = false;
            previousAttackHash = 0;
            attackProgress = -1f;
            animator.SetFloat(AttackSpeedId, 1f);
            animator.ResetTrigger(AttackId);
            AttackFinished?.Invoke();
        }
    }

    // The attack state being played: the destination while blending into it, the current one otherwise.
    // Blending out of it (to locomotion) counts as finished, so she can move again right away.
    private bool TryGetAttackState(out AnimatorStateInfo info)
    {
        if (animator.IsInTransition(0))
        {
            info = animator.GetNextAnimatorStateInfo(0);
            return info.tagHash == AttackTagId;
        }
        info = animator.GetCurrentAnimatorStateInfo(0);
        return info.tagHash == AttackTagId;
    }

    // ------------------------------------------------------------------ scripted control
    // Turns off the agent-driven logic (speed from the agent, turns). The caller stops the agent,
    // moves her (walk speed for the blend tree) and may let the clips move her (root motion XZ).
    public void BeginScripted()
    {
        scripted = true;
        scriptedSpeed = 0f;
        scriptedRootMotion = false;
        turning = false;
    }

    public void EndScripted()
    {
        scripted = false;
        scriptedRootMotion = false;
        hasTurnDestination = false;
    }

    public void SetScriptedSpeed(float speed) => scriptedSpeed = speed;

    // Turn in place with the turn clips while scripted (signed yaw, deg). IsTurning stays true until it ends.
    public bool PlayScriptedTurn(float angle)
    {
        if (!scripted || turning) return false;
        scriptedSpeed = 0f;
        StartTurn(angle);
        return true;
    }
    public void SetScriptedRootMotion(bool on) => scriptedRootMotion = on;

    // Seat clips: SitDown -> Typing (loop) while true, StandUp when false.
    public void SetSeated(bool seated) => animator.SetBool(SeatedId, seated);

    public enum SeatPhase { None, SittingDown, Seated, StandingUp }

    // Seat clip being played (destination while blending into it) and its normalized time.
    public SeatPhase GetSeatPhase(out float normalizedTime)
    {
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = info.normalizedTime;
        if (info.shortNameHash == SitDownId) return SeatPhase.SittingDown;
        if (info.shortNameHash == StandUpId) return SeatPhase.StandingUp;
        // typing or any dealer clip (every seated state is tagged Seat)
        if (info.shortNameHash == SeatedStateId || info.tagHash == SeatTagId) return SeatPhase.Seated;
        normalizedTime = 0f;
        return SeatPhase.None;
    }

    // ------------------------------------------------------------------ seated activities
    public enum SeatAnimation { Typing = 0, Dealer = 1 }

    // which seated clips follow the sit-down (set before SetSeated(true))
    public void SetSeatAnimation(SeatAnimation seatAnimation) => animator.SetInteger(SeatActivityId, (int)seatAnimation);

    public enum DealerClip { Idle = 0, PickDeck = 1, Deal = 2, Hold = 3, PutBack = 4, Check = 5 }

    public void PlayDealerClip(DealerClip clip) => animator.CrossFadeInFixedTime(DealerStateIds[(int)clip], 0.2f, 0);

    // Dealer clip being played (destination while blending into it) and its normalized time.
    public bool TryGetDealerTime(out DealerClip clip, out float normalizedTime)
    {
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = info.normalizedTime;
        int i = Array.IndexOf(DealerStateIds, info.shortNameHash);
        clip = i >= 0 ? (DealerClip)i : DealerClip.Idle;
        return i >= 0;
    }

    // dealing: her right hand reaches toward where the card goes (weight 0 = off)
    public void SetDealerHandTarget(Vector3 target, float weight)
    {
        dealerHandTarget = target;
        dealerHandWeight = Mathf.Clamp01(weight);
    }

    // her right hand (wrist) reaching for a point, her eyes on it: a keypad button (weight 0 = off; same IK as dealing)
    public void SetRightHandTarget(Vector3 target, float weight) => SetDealerHandTarget(target, weight);

    // seated at a table: feet planted forward under it so her knees stay below the table top (weight 0 = off)
    public void SetSeatedFeet(Vector3 leftFoot, Vector3 rightFoot, Vector3 leftKnee, Vector3 rightKnee, float weight)
    {
        seatedLeftFoot = leftFoot;
        seatedRightFoot = rightFoot;
        seatedLeftKnee = leftKnee;
        seatedRightKnee = rightKnee;
        seatedFeetWeight = Mathf.Clamp01(weight);
    }

    // ------------------------------------------------------------------ doors (scripted)
    public enum DoorClip { Inwards = 0, Outwards = 1, Locked = 2 }

    // Blends into a door clip from startNormalized, forward or backward (backward = the same clip closing the door).
    // Turn scripted root motion on to let it move her.
    public void PlayDoorClip(DoorClip clip, float startNormalized, bool backward)
    {
        int state = clip == DoorClip.Inwards ? DoorInwardsId : clip == DoorClip.Outwards ? DoorOutwardsId : DoorLockedId;
        animator.SetFloat(DoorSpeedId, backward ? -1f : 1f);
        animator.CrossFadeInFixedTime(state, doorBlendTime, 0, Mathf.Clamp01(startNormalized) * doorClipLength[(int)clip]);
    }

    // Door clip being played (destination while blending into it) and its normalized time. False once blending out.
    public bool TryGetDoorTime(out float normalizedTime)
    {
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = info.normalizedTime;
        return info.tagHash == DoorTagId;
    }

    // Leaves the door clip (blend back to standing).
    public void EndDoorClip() => animator.CrossFadeInFixedTime(LocomotionId, doorBlendTime, 0);

    // ------------------------------------------------------------------ keypad (scripted, PartnerKeypadUser)
    public enum KeypadClip { Raise = 0, Press = 1, Lower = 2 }

    // a press right after another one restarts the clip (no blend into itself)
    public void PlayKeypad(KeypadClip clip, float blendTime)
    {
        int state = KeypadStateIds[(int)clip];
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (!animator.IsInTransition(0) && now.shortNameHash == state) animator.Play(state, 0, 0f);
        else animator.CrossFadeInFixedTime(state, blendTime, 0, 0f);
    }

    // keypad clip being played (destination while blending into it) and its normalized time; false = none
    public bool TryGetKeypadTime(out KeypadClip clip, out float normalizedTime)
    {
        bool blending = animator.IsInTransition(0);
        AnimatorStateInfo info = blending ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        int i = Array.IndexOf(KeypadStateIds, info.shortNameHash);
        clip = i >= 0 ? (KeypadClip)i : KeypadClip.Raise;
        normalizedTime = blending ? 0f : info.normalizedTime;
        return i >= 0;
    }

    public void EndKeypad() => animator.CrossFadeInFixedTime(LocomotionId, doorBlendTime, 0);

    // ------------------------------------------------------------------ cutscenes (scripted): any clip, any state
    private CutsceneClipSlots cutsceneClips;

    public bool CanPlayCutsceneClips => cutsceneClips != null && cutsceneClips.Ready;
    public bool IsCutsceneClipDone => cutsceneClips == null || cutsceneClips.IsDone;

    public void PlayCutsceneClip(AnimationClip clip, float blendTime)
    {
        if (cutsceneClips == null || !cutsceneClips.Ready)
        {
            Debug.LogWarning($"{name}: no CutsceneA / CutsceneB states in the Animator (run Last Terminal/2 and the scene menu again).", this);
            return;
        }
        cutsceneClips.Play(clip, blendTime);
    }

    // a state of her Animator by name (e.g. FloorSit); false = no such state
    public bool PlayStateByName(string state, float blendTime)
    {
        int id = Animator.StringToHash(state);
        if (!animator.HasState(0, id)) return false;
        if (cutsceneClips != null) cutsceneClips.Stop();
        animator.CrossFadeInFixedTime(id, blendTime, 0);
        return true;
    }

    // back to standing / walking
    public void EndCutsceneClip(float blendTime)
    {
        if (cutsceneClips != null) cutsceneClips.Stop();
        animator.CrossFadeInFixedTime(armed ? ArmedLocomotionId : LocomotionId, blendTime, 0);
    }

    // ------------------------------------------------------------------ sitting on the floor (PartnerRest, scripted)
    // false when the controller has no FloorSit state (the clip was missing when menu 2 ran)
    public bool HasFloorSit => animator.HasState(0, FloorSitId);

    public void PlayFloorSit(float blendTime)
    {
        if (!HasFloorSit) return;
        if (blendTime <= 0f) animator.Play(FloorSitId, 0, 0f);
        else animator.CrossFadeInFixedTime(FloorSitId, blendTime, 0);
    }

    public bool IsFloorSitting
    {
        get
        {
            AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == FloorSitId;
        }
    }

    public void EndFloorSit(float blendTime) => animator.CrossFadeInFixedTime(armed ? ArmedLocomotionId : LocomotionId, blendTime, 0);

    // ------------------------------------------------------------------ evasion (PartnerEvasion)
    public enum EvadeClip { Dodge = 0, Duck = 1 }

    // Blends into an evade clip from startNormalized, forward or backward (backward duck = getting up).
    public void PlayEvade(EvadeClip clip, float startNormalized, bool backward, float blendTime)
    {
        animator.SetFloat(EvadeSpeedId, backward ? -1f : 1f);
        animator.CrossFadeInFixedTime(EvadeStateIds[(int)clip], blendTime, 0, Mathf.Clamp01(startNormalized) * evadeClipLength[(int)clip]);
    }

    // Evade clip being played (destination while blending into it) and its normalized time. False once blending out.
    public bool TryGetEvadeTime(out EvadeClip clip, out float normalizedTime)
    {
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = info.normalizedTime;
        int i = Array.IndexOf(EvadeStateIds, info.shortNameHash);
        clip = i >= 0 ? (EvadeClip)i : EvadeClip.Dodge;
        return i >= 0 && info.tagHash == EvadeTagId;
    }

    // Leaves the evade clip (blend back to standing / armed locomotion).
    public void EndEvade(float blendTime)
    {
        animator.SetFloat(EvadeSpeedId, 1f);
        animator.CrossFadeInFixedTime(armed ? ArmedLocomotionId : LocomotionId, blendTime, 0);
    }

    // ------------------------------------------------------------------ damage (PartnerDamage)
    public enum HitKind { Stomach = 0, Rib = 1, Reaction = 2, Reaction2 = 3 }

    // Hurt: 0 = fine .. 1 = injured locomotion (limping walk, hurting idle)
    public void SetInjured(float amount) => animator.SetFloat(InjuredId, Mathf.Clamp01(amount));

    // Speed / MoveX / MoveZ are fed scaled: slower legs (injured) still reach the full walk of the blend tree
    // PartnerCombat keeps her facing a point while she moves (no turning in place toward the path)
    public void SetHoldFacing(bool value) => holdFacing = value;

    public void SetLocomotionScale(float scale) => locomotionScale = Mathf.Max(0.01f, scale);

    // A hit reaction; armed, the version holding the weapon of her hand (her hold type). Heavy = Reaction clips.
    public void PlayHit(HitKind kind)
    {
        if (dead) return;
        int k = (int)kind;
        int state = HitIds[k];
        if (armed)
        {
            int hold = Mathf.Clamp(Mathf.RoundToInt(animator.GetFloat(WeaponTypeId)), 0, HoldNames.Length - 1);
            state = ArmedHitIds[k, hold];
        }
        hitPlaying = true;
        hitEntered = false;
        hitHeavy = kind == HitKind.Reaction || kind == HitKind.Reaction2;
        hitTimer = 0f;
        turning = false;
        CancelAttack();
        animator.CrossFadeInFixedTime(state, hitBlendIn, 0);
    }

    private void UpdateHit()
    {
        hitTimer += Time.deltaTime;
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        bool inHit = info.tagHash == HitTagId;
        if (inHit) hitEntered = true;
        float exit = hitHeavy ? hitExit.y : hitExit.x;
        if ((inHit && info.normalizedTime >= exit) || (hitEntered && !inHit) || hitTimer > hitTimeout) EndHit();
    }

    private void EndHit()
    {
        hitPlaying = false;
        animator.CrossFadeInFixedTime(armed ? ArmedLocomotionId : LocomotionId, hitBlendOut, 0);
    }

    // Dead: falls back (from a crouch: crumples on the floor) and stays there.
    public void PlayDeath(bool crouched)
    {
        dead = true;
        hitPlaying = false;
        turning = false;
        CancelAttack();
        if (picking)
        {
            picking = false;
            animator.ResetTrigger(PickupId);
            PickupFinished?.Invoke();
        }
        animator.SetFloat(EvadeSpeedId, 1f);
        animator.CrossFadeInFixedTime(crouched ? FallenId : DeathId, crouched ? 0.5f : 0.15f, 0);
    }

    // a swing cut short (hit / death): whoever waits for it is told it is over
    private void CancelAttack()
    {
        if (!attacking) return;
        attacking = false;
        enteredAttackState = false;
        previousAttackHash = 0;
        attackProgress = -1f;
        animator.SetFloat(AttackSpeedId, 1f);
        animator.ResetTrigger(AttackId);
        AttackFinished?.Invoke();
    }

    // Alive again (a save was loaded): standing, at once.
    public void Revive()
    {
        dead = false;
        hitPlaying = false;
        animator.SetBool(SeatedId, false);
        animator.Play(armed ? ArmedLocomotionState : LocomotionState, 0, 0f);
    }

    // Back to standing locomotion at once (e.g. a save was loaded while she was seated).
    public void SnapToLocomotion()
    {
        animator.SetBool(SeatedId, false);
        animator.Play(LocomotionState, 0, 0f);
    }

    // ------------------------------------------------------------------ pick up
    // Caller stops the agent and faces the item first. Grab target = top of the item (world).
    public void PlayPickup(PickupHeight height, Vector3 itemGrabPosition)
    {
        picking = true;
        enteredPickupState = false;
        grabbed = false;
        pickupTimer = 0f;
        pickupTime = 0f;
        pickupLength = 1f;
        grabNormalized = height == PickupHeight.Medium ? grabTimeMedium : grabTimeLow;
        grabTarget = itemGrabPosition + Vector3.up * wristAboveItem;

        animator.SetInteger(PickupHeightId, (int)height);
        animator.SetTrigger(PickupId);
        PickupStarted?.Invoke();
    }

    private void UpdatePickup()
    {
        pickupTimer += Time.deltaTime;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool inPickup = info.tagHash == PickupTagId;
        if (inPickup)
        {
            enteredPickupState = true;
            pickupLength = info.length;
            pickupTime = info.normalizedTime * info.length;
            if (!grabbed && info.normalizedTime >= grabNormalized) Grab();
        }

        if ((enteredPickupState && !inPickup) || pickupTimer > pickupTimeout)
        {
            if (!grabbed) Grab();
            picking = false;
            animator.ResetTrigger(PickupId);
            PickupFinished?.Invoke();
        }
    }

    private void Grab()
    {
        grabbed = true;
        PickupGrabbed?.Invoke();
    }

    // Needs "IK Pass" on the base layer (set by Last Terminal/2).
    private Vector3 dealerHandTarget;
    private float dealerHandWeight;
    private Vector3 seatedLeftFoot, seatedRightFoot, seatedLeftKnee, seatedRightKnee;
    private float seatedFeetWeight;
    private Vector3 lookTarget;
    private float lookTargetWeight;

    // her head (and a bit of her chest) turned toward a point, e.g. the camera she was told to look at (weight 0 = off)
    public void SetLookTarget(Vector3 target, float weight)
    {
        lookTarget = target;
        lookTargetWeight = Mathf.Clamp01(weight);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        SeatedFeetIK();
        if (dealerHandWeight > 0f)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, dealerHandWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, dealerHandTarget);
            animator.SetLookAtWeight(dealerHandWeight * lookAtWeight, 0.15f, 0.8f);
            animator.SetLookAtPosition(dealerHandTarget);
            return;
        }
        if (!picking || !enteredPickupState)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetLookAtWeight(lookTargetWeight, 0.25f, 0.9f, 0f, 0.6f);
            if (lookTargetWeight > 0f) animator.SetLookAtPosition(lookTarget);
            return;
        }

        float grabSec = grabNormalized * pickupLength;
        float reachIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabSec - handIKBlendIn, grabSec, pickupTime));
        float reachOut = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabSec, grabSec + handIKBlendOut, pickupTime));
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reachIn * (1f - reachOut) * handIKWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, grabTarget);

        float lookIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, grabSec, pickupTime));
        float lookOut = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabSec, grabSec + 0.6f, pickupTime));
        animator.SetLookAtWeight(lookIn * (1f - lookOut) * lookAtWeight, 0.2f, 0.8f);
        animator.SetLookAtPosition(grabTarget);
    }

    private void SeatedFeetIK()
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, seatedFeetWeight);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, seatedFeetWeight);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, seatedFeetWeight);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, seatedFeetWeight);
        if (seatedFeetWeight <= 0f) return;
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, seatedLeftFoot);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, seatedRightFoot);
        animator.SetIKHintPosition(AvatarIKHint.LeftKnee, seatedLeftKnee);
        animator.SetIKHintPosition(AvatarIKHint.RightKnee, seatedRightKnee);
    }

    // ------------------------------------------------------------------ turn in place
    private void TryStartTurn()
    {
        if (aiming || holdFacing) return;   // aiming / facing a threat: PartnerCombat keeps her turned (she backpedals, strafes)
        if (agent.pathPending || !agent.hasPath) return;
        if (agent.remainingDistance <= agent.stoppingDistance + 0.1f) return;
        // one turn per destination: any leftover angle is corrected while walking (no turn loops)
        if (hasTurnDestination && (agent.destination - turnDestination).sqrMagnitude < 0.01f) return;

        Vector3 toTarget = agent.steeringTarget - body.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        float angle = Vector3.SignedAngle(body.forward, toTarget, Vector3.up);
        bool moving = agent.velocity.sqrMagnitude > 0.04f;
        float threshold = moving ? movingTurnThreshold : idleTurnThreshold;
        if (Mathf.Abs(angle) < threshold) return;

        StartTurn(angle);
    }

    private void StartTurn(float angle)
    {
        // (also used by PlayScriptedTurn: the agent is already stopped then, these lines change nothing)
        bool big = Mathf.Abs(angle) > bigTurnAngle;
        float slot;
        float clipAngle;
        if (angle < 0f)
        {
            slot = big ? SlotLeft180 : SlotLeft90;
            clipAngle = big ? clipLeft180 : clipLeft90;
        }
        else
        {
            slot = big ? SlotRight180 : SlotRight90;
            clipAngle = big ? clipRight180 : clipRight90;
        }
        rotationScale = angle / clipAngle;   // same sign -> positive; ends exactly on the path direction

        turning = true;
        enteredTurnState = false;
        hasTurnDestination = true;
        turnDestination = agent.destination;
        turnTimer = 0f;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updatePosition = false;
        agent.updateRotation = false;

        animator.SetFloat(SpeedId, 0f);
        animator.SetFloat(TurnAngleId, slot);
        animator.SetTrigger(TurnId);
    }

    private void UpdateTurn()
    {
        turnTimer += Time.deltaTime;
        // current state stays "Turn" until the exit transition is complete, so the full rotation is applied
        bool inTurn = animator.GetCurrentAnimatorStateInfo(0).tagHash == TurnTagId;
        if (inTurn) enteredTurnState = true;

        if ((enteredTurnState && !inTurn) || turnTimer > turnTimeout)
            EndTurn();
    }

    private void EndTurn()
    {
        turning = false;
        animator.ResetTrigger(TurnId);   // a turn that timed out must not fire later
        if (scripted) return;   // the scripted owner keeps the agent stopped
        agent.nextPosition = body.position;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    // Root motion is only used while turning or when scripted control asks for it (seat clips);
    // other clips are baked and the agent moves her.
    private void OnAnimatorMove()
    {
        if (scripted && !turning)
        {
            if (!scriptedRootMotion) return;
            Vector3 move = animator.deltaPosition;
            move.y = 0f;
            body.position += move;
            return;
        }
        if (!turning) return;

        float yaw = animator.deltaRotation.eulerAngles.y;
        if (yaw > 180f) yaw -= 360f;
        body.rotation = Quaternion.AngleAxis(yaw * rotationScale, Vector3.up) * body.rotation;

        Vector3 delta = animator.deltaPosition;
        delta.y = 0f;
        body.position += delta;
        agent.nextPosition = body.position;
    }
}
