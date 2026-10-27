using System;
using System.Collections.Generic;
using UnityEngine;

// Classic operator-style combat: hostiles in the same area as her (her room; outside the rooms, within engageDistance)
// are engaged and get a number (the lowest free one, kept until they go down or are left behind), so the operator
// can say "shoot enemy 2". A wall in between = another room = no fight. Her area sticks to the last room she was in
// while she stands in a doorway (no flicker at the door).
// Combat starts with the first engaged hostile and ends a moment after the last one is gone.
// The focus is the hostile she is fighting now (set by the commands); the combat HUD highlights it.
public class CombatEncounter : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string TargetsField = nameof(targets);
    public const string PartnerField = nameof(partner);
    public const string PartnerHealthField = nameof(partnerHealth);

    // Where the rooms and the shootable hostiles are, and her transform.
    [SerializeField] private CommandTargets targets;
    [SerializeField] private Transform partner;
    [Tooltip("Her health: when she gets hurt, the closest hostile of her area joins the fight right away.")]
    [SerializeField] private Health partnerHealth;
    [Tooltip("Outside the rooms only: hostiles also outside the rooms and closer than this join the fight (m).")]
    [SerializeField] private float engageDistance = 10f;
    [Tooltip("Outside the rooms only: engaged hostiles farther than this leave the fight (m).")]
    [SerializeField] private float disengageDistance = 15f;
    [Tooltip("Standing this close outside a room (a door frame) she still counts as in the last room (m).")]
    [SerializeField] private float doorwayMargin = 0.6f;
    [Tooltip("In a room too: a hostile joins the fight only this close (m) - across a long hall she is still free to move.")]
    [SerializeField] private float roomEngageDistance = 12f;
    [Tooltip("In a room: an engaged hostile farther than this leaves the fight (m).")]
    [SerializeField] private float roomDisengageDistance = 18f;
    [Tooltip("Combat ends this long after the last hostile is gone (s).")]
    [SerializeField] private float endDelay = 2f;

    // Hostiles in the fight (sorted by number) and the number each one got.
    private readonly List<Health> engaged = new List<Health>();
    private readonly Dictionary<Health, int> numbers = new Dictionary<Health, int>();
    private readonly HashSet<Health> downed = new HashSet<Health>();   // TargetDown already sent
    // hostiles that attacked her or that she attacked: in the fight at any distance (while in her area)
    private readonly HashSet<Health> provoked = new HashSet<Health>();
    private readonly Dictionary<Health, Action<int>> hurtHandlers = new Dictionary<Health, Action<int>>();
    private Health focus;
    private bool inCombat;
    private float endTimer;
    private Room area;
    private bool hasArea;

    // Combat begins / ends (music, HUD, her stance listen to these).
    public event Action CombatStarted;
    public event Action CombatEnded;
    public event Action TargetsChanged;          // joined / left / renumbered
    public event Action<Health> TargetDown;      // a hostile was defeated (engaged or not: shot from far away too)
    // The hostile she is fighting now changed.
    public event Action<Health> FocusChanged;

    // State read by the commands and the HUD.
    public bool InCombat => inCombat;
    public IReadOnlyList<Health> Engaged => engaged;
    public Health Focus => focus;
    public Room Area => area;   // her room (null = outside the rooms)

    // the engaged hostile closest to a point (null = none)
    public Health Nearest(Vector3 from)
    {
        Health best = null;
        float bestDistance = float.MaxValue;
        foreach (Health h in engaged)
        {
            if (h == null || h.IsDead) continue;
            float d = Distance(h, from);
            if (d < bestDistance) { bestDistance = d; best = h; }
        }
        return best;
    }

    // distance from a point to the closest engaged hostile (MaxValue = none)
    public float DistanceToThreat(Vector3 from)
    {
        Health h = Nearest(from);
        return h != null ? Distance(h, from) : float.MaxValue;
    }

    // Listens for her getting hurt and for each hostile getting hurt (provokes it).
    private void OnEnable()
    {
        if (partnerHealth != null) partnerHealth.Damaged += OnPartnerHurt;
        if (targets == null) return;
        foreach (CommandTargets.Shootable s in targets.Shootables)
        {
            Health h = s != null ? s.Target : null;
            if (h == null || hurtHandlers.ContainsKey(h)) continue;
            Action<int> handler = _ => Provoke(h);   // shot / hit by her: it fights back now
            hurtHandlers.Add(h, handler);
            h.Damaged += handler;
        }
    }

    // Stops listening.
    private void OnDisable()
    {
        if (partnerHealth != null) partnerHealth.Damaged -= OnPartnerHurt;
        foreach (KeyValuePair<Health, Action<int>> pair in hurtHandlers) if (pair.Key != null) pair.Key.Damaged -= pair.Value;
        hurtHandlers.Clear();
    }

    // she attacks it / it attacks her: the fight starts now, whatever the distance (same area)
    public void Provoke(Health target)
    {
        if (!enabled || target == null || target.IsDead || !target.gameObject.activeInHierarchy) return;
        if (!SameArea(target, partner.position, float.MaxValue, float.MaxValue)) return;
        provoked.Add(target);
        if (!numbers.ContainsKey(target))
        {
            engaged.Add(target);
            numbers.Add(target, LowestFreeNumber());
            engaged.Sort((a, b) => numbers[a].CompareTo(numbers[b]));
            TargetsChanged?.Invoke();
        }
        endTimer = endDelay;
        if (!inCombat) { inCombat = true; CombatStarted?.Invoke(); }
    }

    // she got hurt: the closest living hostile of her area is the one fighting her
    private void OnPartnerHurt(int amount)
    {
        Health best = null;
        float bestDistance = float.MaxValue;
        foreach (CommandTargets.Shootable s in targets.Shootables)
        {
            Health h = s != null ? s.Target : null;
            if (h == null || h.IsDead || !h.gameObject.activeInHierarchy || !SameArea(h, partner.position, float.MaxValue, float.MaxValue)) continue;
            float d = Distance(h, partner.position);
            if (d < bestDistance) { bestDistance = d; best = h; }
        }
        if (best != null) Provoke(best);
    }

    // Checks the references.
    private void Awake()
    {
        if (targets == null || partner == null)
        {
            Debug.LogError($"{name}: CombatEncounter needs CommandTargets and her transform.", this);
            enabled = false;
        }
    }

    // The number the operator uses for a hostile (0 = not in the fight).
    public int NumberOf(Health target) => target != null && numbers.TryGetValue(target, out int n) ? n : 0;

    // The hostile with that number (null = none).
    public Health Get(int number)
    {
        foreach (KeyValuePair<Health, int> pair in numbers)
            if (pair.Value == number) return pair.Key;
        return null;
    }

    // The hostile she is fighting now (only an engaged one).
    public void SetFocus(Health target)
    {
        if (target != null && !numbers.ContainsKey(target)) target = null;
        if (focus == target) return;
        focus = target;
        FocusChanged?.Invoke(focus);
    }

    // Every frame: her area, hostiles leaving (dead / far / gone) and joining the fight, combat start / end.
    private void Update()
    {
        bool changed = false;
        Vector3 at = partner.position;
        Room here = targets.RoomOf(at);
        if (here != null || !hasArea || !InDoorway(at)) { area = here; hasArea = true; }

        // leave: defeated, gone or left behind (not in her area any more)
        for (int i = engaged.Count - 1; i >= 0; i--)
        {
            Health h = engaged[i];
            bool dead = h == null || h.IsDead;
            bool far = h != null && (provoked.Contains(h) ? !SameArea(h, at, float.MaxValue, float.MaxValue) : !SameArea(h, at, disengageDistance, roomDisengageDistance));
            bool gone = h != null && (!h.gameObject.activeInHierarchy || far);
            if (!dead && !gone) continue;
            engaged.RemoveAt(i);
            if (h != null) { numbers.Remove(h); provoked.Remove(h); }
            if (focus == h) SetFocus(null);
            if (dead && h != null && downed.Add(h)) TargetDown?.Invoke(h);
            changed = true;
        }

        // join
        foreach (CommandTargets.Shootable s in targets.Shootables)
        {
            Health h = s != null ? s.Target : null;
            if (h == null) continue;
            // killed without ever joining the fight (shot from across the room): still a kill
            if (h.IsDead) { if (!numbers.ContainsKey(h) && downed.Add(h)) TargetDown?.Invoke(h); continue; }
            if (!h.gameObject.activeInHierarchy || numbers.ContainsKey(h)) continue;
            if (!SameArea(h, at, engageDistance, roomEngageDistance)) continue;
            engaged.Add(h);
            numbers.Add(h, LowestFreeNumber());
            changed = true;
        }
        if (changed)
        {
            engaged.Sort((a, b) => numbers[a].CompareTo(numbers[b]));
            TargetsChanged?.Invoke();
        }

        // combat state
        if (engaged.Count > 0)
        {
            endTimer = endDelay;
            if (!inCombat) { inCombat = true; CombatStarted?.Invoke(); }
        }
        else if (inCombat)
        {
            endTimer -= Time.deltaTime;
            if (endTimer <= 0f) { inCombat = false; CombatEnded?.Invoke(); }
        }
    }

    // in her room and close enough to fight; both outside the rooms: close enough
    private bool SameArea(Health h, Vector3 at, float distance, float roomDistance)
    {
        Room theirs = targets.RoomOf(h.transform.position);
        if (area != null || theirs != null) return theirs == area && Distance(h, at) <= roomDistance;
        return Distance(h, at) <= distance;
    }

    // between two rooms (in a door frame): no room contains her, but one is right there
    private bool InDoorway(Vector3 at)
    {
        foreach (Room r in targets.Rooms)
            if (r != null && r.Contains(at, doorwayMargin)) return true;
        return false;
    }

    // The lowest number no engaged hostile uses.
    private int LowestFreeNumber()
    {
        int n = 1;
        while (numbers.ContainsValue(n)) n++;
        return n;
    }

    // Floor distance from a hostile to a point.
    private static float Distance(Health h, Vector3 at)
    {
        Vector3 d = h.transform.position - at;
        d.y = 0f;
        return d.magnitude;
    }
}
