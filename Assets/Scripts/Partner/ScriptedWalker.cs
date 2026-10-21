using System;
using UnityEngine;

// Scripted walk for short exact moves (to a door, ...) while PartnerAnimator is in scripted mode:
// steers toward the target while moving, accelerates / brakes like the agent and feeds the real speed to the walk
// blend (no foot sliding), turns in place with the turn clips when standing and facing well away.
// Plain class: the owner component serializes it (its settings show in the owner's inspector) and calls Bind.
[Serializable]
public class ScriptedWalker
{
    [Tooltip("Max speed (her walk clip is 1.8 m/s).")]
    [SerializeField] private float walkSpeed = 1.3f;
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float deceleration = 3.5f;
    [Tooltip("How fast she steers while walking (deg/s).")]
    [SerializeField] private float steerSpeed = 240f;
    [Tooltip("Standing still, above this angle she turns in place with the turn clip before walking (deg).")]
    [SerializeField] private float clipTurnAngle = 50f;
    [Tooltip("TurnTo: above this angle the turn clip is used (once), the rest is a smooth turn (deg).")]
    [SerializeField] private float faceClipTurnAngle = 30f;
    [SerializeField] private float faceTurnSpeed = 180f;
    [SerializeField] private float arriveTolerance = 0.02f;

    private Transform body;
    private PartnerAnimator partnerAnimator;
    private float speed;
    private bool clipTurned;

    public float Speed => speed;

    public void Bind(Transform movedBody, PartnerAnimator animator)
    {
        body = movedBody;
        partnerAnimator = animator;
    }

    // Starts a scripted move, keeping the speed she already walks at (handoff from the NavMesh walk).
    public void Begin(float currentSpeed)
    {
        speed = Mathf.Clamp(currentSpeed, 0f, walkSpeed);
        clipTurned = false;
        partnerAnimator.SetScriptedSpeed(speed);
    }

    public void ResetTurn() => clipTurned = false;

    public void Stop()
    {
        speed = 0f;
        partnerAnimator.SetScriptedSpeed(0f);
    }

    // True once on the target (stopped). passRadius > 0: no braking, true as soon as she is that close.
    public bool WalkTo(Vector3 target, float passRadius = 0f)
    {
        if (partnerAnimator.IsTurning) return false;

        Vector3 to = Flat(target) - Flat(body.position);
        float dist = to.magnitude;
        if (passRadius > 0f && dist <= passRadius) return true;
        if (dist <= arriveTolerance)
        {
            body.position = new Vector3(target.x, body.position.y, target.z);
            Stop();
            return true;
        }

        float angle = Vector3.SignedAngle(Flat(body.forward), to, Vector3.up);
        if (speed < 0.1f && Mathf.Abs(angle) > clipTurnAngle && partnerAnimator.PlayScriptedTurn(angle)) return false;

        // (not in the last few cm: the direction to the target gets noisy there)
        if (dist > 0.1f) body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(to), steerSpeed * Time.deltaTime);

        float stopDistance = speed * speed / (2f * deceleration);
        bool braking = passRadius <= 0f && dist <= stopDistance + arriveTolerance;
        float wanted = braking ? 0f : walkSpeed * (Mathf.Abs(angle) > 45f ? 0.4f : 1f);
        speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? acceleration : deceleration) * Time.deltaTime);
        if (speed < 0.05f && wanted <= 0f) speed = Mathf.Min(0.15f, dist / Mathf.Max(Time.deltaTime, 0.0001f));   // creep the last cm

        float move = Mathf.Min(speed * Time.deltaTime, dist);
        body.position += to / dist * move;
        partnerAnimator.SetScriptedSpeed(speed);
        return false;
    }

    // Faces a direction: big angles with the turn clip (once per ResetTurn), the rest smoothly. True when aligned.
    public bool TurnTo(Vector3 direction)
    {
        partnerAnimator.SetScriptedSpeed(0f);
        if (partnerAnimator.IsTurning) return false;
        direction = Flat(direction);
        if (direction.sqrMagnitude < 0.0001f) return true;

        float angle = Vector3.SignedAngle(Flat(body.forward), direction, Vector3.up);
        if (!clipTurned && Mathf.Abs(angle) > faceClipTurnAngle)
        {
            clipTurned = partnerAnimator.PlayScriptedTurn(angle);
            if (clipTurned) return false;
        }
        Quaternion want = Quaternion.LookRotation(direction);
        body.rotation = Quaternion.RotateTowards(body.rotation, want, faceTurnSpeed * Time.deltaTime);
        return Quaternion.Angle(body.rotation, want) < 1f;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
