using System;
using UnityEngine;

// Helena's hands at the blackjack table (she is seated by PartnerSaveUser; the seated clips are her dealer clips):
// picks up the deck from the shoe, deals one card at a time (the card sits in her right hand until the release
// point of the clip, then BlackjackGame slides it to its place), peeks at the hole card, puts the deck back.
// Her right hand reaches toward where the card goes (Animator IK) around the release.
public class PartnerBlackjackDealer : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);

    // Her animator: plays the dealer clips and the hand / feet IK.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [Header("Deal clip (normalized time)")]
    [Tooltip("The card leaves her hand here.")]
    [SerializeField, Range(0f, 1f)] private float releaseTime = 0.5f;
    [Tooltip("Reach toward the card's place between these times (IK on the right hand).")]
    [SerializeField] private Vector2 reachWindow = new Vector2(0.28f, 0.72f);
    // How strongly her right hand reaches toward the card's place (IK weight at the middle of the window).
    [SerializeField, Range(0f, 1f)] private float reachWeight = 0.6f;
    [Tooltip("The IK target is kept this close to her shoulder (her arm can't reach the far side of the table).")]
    [SerializeField] private float maxReach = 0.58f;
    [Tooltip("Peek clip: she looks at the hole card here.")]
    [SerializeField, Range(0f, 1f)] private float peekTime = 0.45f;
    [Tooltip("A clip that never starts or ends gives up after this long (s).")]
    [SerializeField] private float timeout = 6f;
    [Header("Legs under the table (seat-local, right side; the left is mirrored)")]
    [Tooltip("Ankle: forward under the table, so the legs stretch and the knees drop well below the top.")]
    [SerializeField] private Vector3 footPlace = new Vector3(0.13f, 0.14f, 0.74f);
    [Tooltip("The knees bend toward this point (up and forward).")]
    [SerializeField] private Vector3 kneeHint = new Vector3(0.17f, 0.9f, 0.6f);
    // Time to blend her legs under the table / back (s).
    [SerializeField] private float feetBlendTime = 0.5f;

    // The clip running now, whether it started, whether its event (release / pickup) happened, time spent waiting.
    private PartnerAnimator.DealerClip clip;
    private bool running;
    private bool entered;
    private bool released;
    private float timer;
    // Normalized time of the clip event and where the card goes.
    private float releaseAt;
    private Vector3 reachTarget;
    // Callbacks: at the clip event, and when the clip ends.
    private Action onRelease;
    private Action onDone;

    // Legs under the table: the seat they are placed from, planted or not, current blend weight.
    private Transform feetSeat;
    private bool feetPlanted;
    private float feetWeight;

    // A clip is still running.
    public bool IsBusy => running;

    // Checks the animator reference.
    private void Awake()
    {
        if (partnerAnimator == null)
        {
            Debug.LogError($"{name}: PartnerBlackjackDealer needs the PartnerAnimator.", this);
            enabled = false;
        }
    }

    // seated at the table: legs placed under it (blends in); released when she gets up (blends out)
    public void PlantFeet(Transform seat)
    {
        feetSeat = seat;
        feetPlanted = seat != null;
    }

    // She gets up: the legs blend back to the clip.
    public void ReleaseFeet() => feetPlanted = false;

    // Idle / holding the deck between deals (looping clips, no callbacks).
    public void Idle() { Stop(); partnerAnimator.PlayDealerClip(PartnerAnimator.DealerClip.Idle); }
    public void Hold() { Stop(); partnerAnimator.PlayDealerClip(PartnerAnimator.DealerClip.Hold); }

    // One-shot clips with an event in the middle: pick up / put back the deck, peek at the hole card.
    public void PickUpDeck(Action atShoe, Action done) => Play(PartnerAnimator.DealerClip.PickDeck, 0.45f, atShoe, done);
    public void PutBackDeck(Action atShoe, Action done) => Play(PartnerAnimator.DealerClip.PutBack, 0.45f, atShoe, done);
    public void Peek(Action looking, Action done) => Play(PartnerAnimator.DealerClip.Check, peekTime, looking, done);

    // Deals one card: the release callback fires when the card leaves her hand, then done at the end.
    public void DealCard(Vector3 target, Action release, Action done)
    {
        reachTarget = target;
        Play(PartnerAnimator.DealerClip.Deal, releaseTime, release, done);
    }

    // Starts a dealer clip and remembers its callbacks.
    private void Play(PartnerAnimator.DealerClip c, float eventTime, Action atEvent, Action done)
    {
        Stop();
        clip = c;
        running = true;
        entered = false;
        released = false;
        timer = 0f;
        releaseAt = eventTime;
        onRelease = atEvent;
        onDone = done;
        partnerAnimator.PlayDealerClip(c);
    }


    // Cancels the clip callbacks and the reach.
    public void Stop()
    {
        running = false;
        onRelease = null;
        onDone = null;
        partnerAnimator.SetDealerHandTarget(Vector3.zero, 0f);
    }

    // where a card in her right hand sits (between the fingers, back facing out of the palm)
    public bool TryGetCardInHand(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!partnerAnimator.TryGetRightHandFrame(out Vector3 origin, out Vector3 fingers, out Vector3 across, out Vector3 normal)) return false;
        position = origin + fingers * 0.03f - normal * 0.012f;
        rotation = Quaternion.LookRotation(fingers, -normal);
        return true;
    }

    // where the deck sits in her left hand
    public bool TryGetDeckInHand(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!partnerAnimator.TryGetLeftHandFrame(out Vector3 wrist, out Vector3 fingers, out Vector3 palm, out float length)) return false;
        position = wrist + fingers * (length * 0.75f) + palm * 0.018f;
        rotation = Quaternion.LookRotation(fingers, palm);
        return true;
    }

    // Follows the running clip: reach weight, event time, end (or timeout if the clip never starts).
    private void Update()
    {
        UpdateFeet();
        if (!running) return;
        timer += Time.deltaTime;
        bool inClip = partnerAnimator.TryGetDealerTime(out PartnerAnimator.DealerClip now, out float t) && now == clip;
        if (!inClip)
        {
            if ((entered || timer > timeout) && running) Finish();
            return;
        }
        entered = true;

        if (clip == PartnerAnimator.DealerClip.Deal)
        {
            float w = Mathf.Sin(Mathf.PI * Mathf.Clamp01(Mathf.InverseLerp(reachWindow.x, reachWindow.y, t))) * reachWeight;
            partnerAnimator.SetDealerHandTarget(ClampedReach(reachTarget), w);
        }
        if (!released && t >= releaseAt)
        {
            released = true;
            Action r = onRelease;
            onRelease = null;
            r?.Invoke();
        }
        if (t >= 0.97f || timer > timeout) Finish();
    }

    // Ends the clip: the event if it was missed, then the done callback.
    private void Finish()
    {
        if (!released) { released = true; Action r = onRelease; onRelease = null; r?.Invoke(); }
        Action d = onDone;
        Stop();
        d?.Invoke();
    }

    // Blends the feet IK in / out (mirrored left / right from the seat).
    private void UpdateFeet()
    {
        float before = feetWeight;
        feetWeight = Mathf.MoveTowards(feetWeight, feetPlanted ? 1f : 0f, Time.deltaTime / Mathf.Max(feetBlendTime, 0.01f));
        if (feetSeat == null || (feetWeight <= 0f && before <= 0f)) return;
        Vector3 mirror = new Vector3(-1f, 1f, 1f);
        float w = Mathf.SmoothStep(0f, 1f, feetWeight);
        partnerAnimator.SetSeatedFeet(feetSeat.TransformPoint(Vector3.Scale(footPlace, mirror)), feetSeat.TransformPoint(footPlace),
                                      feetSeat.TransformPoint(Vector3.Scale(kneeHint, mirror)), feetSeat.TransformPoint(kneeHint), w);
    }

    // The reach target, kept within arm's length of her shoulder.
    private Vector3 ClampedReach(Vector3 target)
    {
        Transform shoulder = partnerAnimator.GetBone(HumanBodyBones.RightUpperArm);
        Vector3 aim = target + Vector3.up * 0.02f;
        if (shoulder == null) return aim;
        Vector3 d = aim - shoulder.position;
        return d.magnitude > maxReach ? shoulder.position + d.normalized * maxReach : aim;
    }
}
