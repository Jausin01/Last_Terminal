using System;
using UnityEngine;

// The attack clips every enemy controller has (EnemyAnimator states): what a type uses and how is in its EnemyType.
public enum EnemyAttackSlot { Swipe = 0, Smash = 1, Bite = 2 }

// How the enemy starts (and goes back to after losing her): standing, walking around its room, or crouched feeding.
public enum EnemyStartMode { Idle, Wander, Feeding }

// One kind of enemy (zombie, runner, armed zombie, killer): health, speeds, senses, attacks.
// The clips come from the Animator controller on the prefab (EnemyAnimator); the weapon (if any) adds its damage and reach.
[CreateAssetMenu(menuName = "Last Terminal/Enemy Type", fileName = "EnemyType")]
public class EnemyType : ScriptableObject
{
    [Serializable]
    public class Attack
    {
        public EnemyAttackSlot slot;
        [Tooltip("Chance against the other usable attacks.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("She must be this close to start it (m, plus the weapon reach).")]
        [Min(0.1f)] public float range = 1.1f;
        [Tooltip("Moment of the hit in the clip (normalized time).")]
        [Range(0f, 1f)] public float hitTime = 0.4f;
        [Tooltip("Back to walking at this normalized time (the rest of the clip is recovery).")]
        [Range(0f, 1f)] public float exitTime = 0.7f;
        [Tooltip("Damage without a weapon (an armed enemy adds the weapon's damage for this slot; a weapon with 0 can't do it).")]
        [Min(0)] public int damage = 10;
        [Tooltip("Seconds before the next attack.")]
        [Min(0f)] public float cooldown = 1f;
    }

    public const string AttacksField = nameof(attacks);
    public const string DisplayNameField = nameof(displayName);
    public const string CommandKeyField = nameof(commandKey);
    public const string MaxHealthField = nameof(maxHealth);
    public const string WalkSpeedField = nameof(walkSpeed);
    public const string WalkClipSpeedField = nameof(walkClipSpeed);
    public const string RunsField = nameof(runs);
    public const string RunSpeedField = nameof(runSpeed);
    public const string RunClipSpeedField = nameof(runClipSpeed);
    public const string TurnSpeedField = nameof(turnSpeed);
    public const string StepInTimeField = nameof(stepInTime);
    public const string SightRangeField = nameof(sightRange);
    public const string SightAngleField = nameof(sightAngle);
    public const string HearRangeField = nameof(hearRange);
    public const string FeedingHearRangeField = nameof(feedingHearRange);
    public const string ScreamsOnAlertField = nameof(screamsOnAlert);
    public const string LoseTimeField = nameof(loseTime);
    public const string AttackSpeedField = nameof(attackSpeed);
    public const string StaggerDamageField = nameof(staggerDamage);
    public const string StaggerTimeField = nameof(staggerTime);
    public const string WanderPauseField = nameof(wanderPause);

    [SerializeField] private string displayName = "Zombie";
    [Tooltip("Name the operator can use for it (lexicon key: \"zombie\", \"killer\"); \"enemy\" works for all.")]
    [SerializeField] private string commandKey = "zombie";
    [SerializeField, Min(1)] private int maxHealth = 80;

    [Header("Movement")]
    [Tooltip("Walking speed (m/s).")]
    [SerializeField] private float walkSpeed = 0.45f;
    [Tooltip("Speed the walk clip shows at 1x (m/s): the clip is sped up / slowed down to match walkSpeed.")]
    [SerializeField] private float walkClipSpeed = 0.34f;
    [SerializeField] private bool runs;
    [SerializeField] private float runSpeed = 2.6f;
    [SerializeField] private float runClipSpeed = 3f;
    [Tooltip("Turning in place toward her (deg/s).")]
    [SerializeField] private float turnSpeed = 160f;
    [Tooltip("Seconds from standing to full speed (the legs lead, no sliding).")]
    [SerializeField] private float stepInTime = 0.4f;

    [Header("Senses")]
    [SerializeField] private float sightRange = 9f;
    [Tooltip("Field of view (deg, total).")]
    [SerializeField] private float sightAngle = 140f;
    [Tooltip("Hears her this close, any direction (m).")]
    [SerializeField] private float hearRange = 3f;
    [Tooltip("While feeding it sees nothing and hears only this close (m).")]
    [SerializeField] private float feedingHearRange = 2f;
    [SerializeField] private bool screamsOnAlert = true;
    [Tooltip("She left its room this long ago: it gives up and goes back (s).")]
    [SerializeField] private float loseTime = 5f;

    [Header("Combat")]
    [SerializeField] private Attack[] attacks = new Attack[0];
    [Tooltip("Speed of the attack clips.")]
    [SerializeField, Range(0.5f, 2f)] private float attackSpeed = 1f;
    [Tooltip("A hit of at least this much staggers it (interrupts its attack).")]
    [SerializeField] private int staggerDamage = 30;
    [SerializeField] private float staggerTime = 0.7f;

    [Header("Wander")]
    [SerializeField] private float wanderPause = 3f;

    public string DisplayName => displayName;
    public string CommandKey => commandKey;
    public int MaxHealth => maxHealth;
    public float WalkSpeed => walkSpeed;
    public float WalkClipSpeed => walkClipSpeed;
    public bool Runs => runs;
    public float RunSpeed => runSpeed;
    public float RunClipSpeed => runClipSpeed;
    public float TurnSpeed => turnSpeed;
    public float StepInTime => stepInTime;
    public float SightRange => sightRange;
    public float SightAngle => sightAngle;
    public float HearRange => hearRange;
    public float FeedingHearRange => feedingHearRange;
    public bool ScreamsOnAlert => screamsOnAlert;
    public float LoseTime => loseTime;
    public Attack[] Attacks => attacks;
    public float AttackSpeed => attackSpeed;
    public int StaggerDamage => staggerDamage;
    public float StaggerTime => staggerTime;
    public float WanderPause => wanderPause;
}
