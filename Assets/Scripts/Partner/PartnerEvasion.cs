using System;
using UnityEngine;
using UnityEngine.AI;

// Her evasive moves (commands "dodge", "get down", "get up"):
// - Dodge: she leaps back out of the way and steps back to where she was (in place). No damage at all during the leap.
// - Duck: she crouches and stays down (less damage) until told to get up; getting up = the same clip backward.
// She keeps her weapon (the upper-body layer stays on): she can shoot and reload while down.
// A move ordered while she is down (or mid-dodge) waits: she gets up first, then goes.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerEvasion : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string HealthField = nameof(health);

    // What she is doing: nothing, a dodge, going down, down, getting up.
    public enum Move { None, Dodging, GoingDown, Down, GettingUp }

    // Her animator (evade clips) and health (invulnerable / reduced damage).
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private Health health;
    [Header("Dodge (normalized time of the clip)")]
    [Tooltip("No damage between these times (the leap back).")]
    [SerializeField] private Vector2 dodgeWindow = new Vector2(0.15f, 0.62f);
    [Tooltip("Back to standing from here.")]
    [SerializeField, Range(0f, 1f)] private float dodgeEnd = 0.93f;
    [Header("Duck")]
    [Tooltip("She counts as down from here (normalized).")]
    [SerializeField, Range(0f, 1f)] private float downAt = 0.6f;
    [Tooltip("Damage taken while down (fraction).")]
    [SerializeField, Range(0f, 1f)] private float downDamageScale = 0.35f;
    [Tooltip("Getting up (clip backward): back to standing here (normalized).")]
    [SerializeField, Range(0f, 1f)] private float upAt = 0.05f;
    // Blend into the evade clips (s).
    [SerializeField] private float blendTime = 0.15f;
    [Tooltip("A clip that never starts gives up after this long (s).")]
    [SerializeField] private float timeout = 4f;

    // Her agent, the move, whether its clip started, time waiting for it.
    private NavMeshAgent agent;
    private Move move;
    private bool entered;
    private float timer;
    // What to do once she is up, and a destination given meanwhile (resumed after).
    private Action afterGettingUp;
    private bool hasPendingDestination;
    private Vector3 pendingDestination;

    // State read by the commands, her damage and the HUD.
    public Move Current => move;
    public bool IsBusy => move == Move.Dodging || move == Move.GoingDown || move == Move.GettingUp;
    public bool IsDown => move == Move.Down;
    public bool IsCrouched => move == Move.GoingDown || move == Move.Down;

    // Checks the references.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (partnerAnimator == null || health == null)
        {
            Debug.LogError($"{name}: PartnerEvasion needs the PartnerAnimator and her Health.", this);
            enabled = false;
        }
    }

    // Disabled: back to standing.
    private void OnDisable() => Cancel();

    // "Dodge": a leap back with an invulnerable window.
    public bool Dodge()
    {
        if (!enabled || move != Move.None || partnerAnimator.IsScripted || partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp) return false;
        Begin(Move.Dodging);
        partnerAnimator.PlayEvade(PartnerAnimator.EvadeClip.Dodge, 0f, false, blendTime);
        return true;
    }

    // "Duck": she crouches and stays down.
    public bool Duck()
    {
        if (!enabled || move != Move.None || partnerAnimator.IsScripted || partnerAnimator.IsAttacking || partnerAnimator.IsPickingUp) return false;
        Begin(Move.GoingDown);
        partnerAnimator.PlayEvade(PartnerAnimator.EvadeClip.Duck, 0f, false, blendTime);
        return true;
    }

    // Gets up (if down), then runs the action (at once if she is standing). False while dodging / already getting up.
    public bool GetUp(Action then = null)
    {
        if (move == Move.None) { then?.Invoke(); return true; }
        if (!IsCrouched) return false;
        float from = partnerAnimator.TryGetEvadeTime(out PartnerAnimator.EvadeClip clip, out float t) && clip == PartnerAnimator.EvadeClip.Duck
            ? Mathf.Clamp01(t) : 1f;
        afterGettingUp = then;
        Begin(Move.GettingUp);
        health.DamageScale = 1f;
        partnerAnimator.PlayEvade(PartnerAnimator.EvadeClip.Duck, from, true, blendTime);
        return true;
    }

    // Back to standing at once (a load, a cutscene...).
    public void Cancel()
    {
        if (move == Move.None) return;
        if (partnerAnimator != null) partnerAnimator.EndEvade(0f);
        Finish(false);
    }

    // Starts a move: holds her in place.
    private void Begin(Move next)
    {
        move = next;
        entered = false;
        timer = 0f;
        Hold();
    }

    // she stays where she is; a destination given meanwhile is kept for later
    private void Hold()
    {
        if (!agent.isOnNavMesh) return;
        if (agent.hasPath || agent.pathPending)
        {
            hasPendingDestination = true;
            pendingDestination = agent.destination;
        }
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    // Follows the clip: invulnerable window, down threshold, getting-up end; a move order gets her up first.
    private void Update()
    {
        if (move == Move.None) return;
        if (agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
        {
            // told to move: down -> she gets up first; otherwise after the clip
            Hold();
            if (move == Move.Down) GetUp();
        }

        timer += Time.deltaTime;
        PartnerAnimator.EvadeClip expected = move == Move.Dodging ? PartnerAnimator.EvadeClip.Dodge : PartnerAnimator.EvadeClip.Duck;
        bool playing = partnerAnimator.TryGetEvadeTime(out PartnerAnimator.EvadeClip clip, out float t) && clip == expected;
        if (!playing)
        {
            if (entered || timer > timeout) Finish(true);
            return;
        }
        entered = true;

        switch (move)
        {
            case Move.Dodging:
                health.Invulnerable = t >= dodgeWindow.x && t <= dodgeWindow.y;
                if (t >= dodgeEnd) { partnerAnimator.EndEvade(0.25f); Finish(true); }
                break;
            case Move.GoingDown:
                if (t >= downAt) { move = Move.Down; health.DamageScale = downDamageScale; }
                break;
            case Move.GettingUp:
                if (t <= upAt) { partnerAnimator.EndEvade(0.2f); Finish(true); }
                break;
        }
    }

    // Move over: damage back to normal, then the destination / action that waited.
    private void Finish(bool resume)
    {
        move = Move.None;
        entered = false;
        health.Invulnerable = false;
        health.DamageScale = 1f;
        Action then = afterGettingUp;
        afterGettingUp = null;
        bool go = resume && hasPendingDestination && agent.isOnNavMesh;
        hasPendingDestination = false;
        if (go) agent.SetDestination(pendingDestination);
        if (resume) then?.Invoke();
    }
}
