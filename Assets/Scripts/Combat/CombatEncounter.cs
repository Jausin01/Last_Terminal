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
    public const string TargetsField = nameof(targets);
    public const string PartnerField = nameof(partner);

    [SerializeField] private CommandTargets targets;
    [SerializeField] private Transform partner;
    [Tooltip("Outside the rooms only: hostiles also outside the rooms and closer than this join the fight (m).")]
    [SerializeField] private float engageDistance = 10f;
    [Tooltip("Outside the rooms only: engaged hostiles farther than this leave the fight (m).")]
    [SerializeField] private float disengageDistance = 15f;
    [Tooltip("Standing this close outside a room (a door frame) she still counts as in the last room (m).")]
    [SerializeField] private float doorwayMargin = 0.6f;
    [Tooltip("Combat ends this long after the last hostile is gone (s).")]
    [SerializeField] private float endDelay = 2f;

    private readonly List<Health> engaged = new List<Health>();
    private readonly Dictionary<Health, int> numbers = new Dictionary<Health, int>();
    private Health focus;
    private bool inCombat;
    private float endTimer;
    private Room area;
    private bool hasArea;

    public event Action CombatStarted;
    public event Action CombatEnded;
    public event Action TargetsChanged;          // joined / left / renumbered
    public event Action<Health> TargetDown;      // an engaged hostile was defeated
    public event Action<Health> FocusChanged;

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

    private void Awake()
    {
        if (targets == null || partner == null)
        {
            Debug.LogError($"{name}: CombatEncounter needs CommandTargets and her transform.", this);
            enabled = false;
        }
    }

    public int NumberOf(Health target) => target != null && numbers.TryGetValue(target, out int n) ? n : 0;

    public Health Get(int number)
    {
        foreach (KeyValuePair<Health, int> pair in numbers)
            if (pair.Value == number) return pair.Key;
        return null;
    }

    public void SetFocus(Health target)
    {
        if (target != null && !numbers.ContainsKey(target)) target = null;
        if (focus == target) return;
        focus = target;
        FocusChanged?.Invoke(focus);
    }

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
            bool gone = h != null && (!h.gameObject.activeInHierarchy || !SameArea(h, at, disengageDistance));
            if (!dead && !gone) continue;
            engaged.RemoveAt(i);
            if (h != null) numbers.Remove(h);
            if (focus == h) SetFocus(null);
            if (dead && h != null) TargetDown?.Invoke(h);
            changed = true;
        }

        // join
        foreach (CommandTargets.Shootable s in targets.Shootables)
        {
            Health h = s != null ? s.Target : null;
            if (h == null || h.IsDead || !h.gameObject.activeInHierarchy || numbers.ContainsKey(h)) continue;
            if (!SameArea(h, at, engageDistance)) continue;
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

    // in her room; both outside the rooms: close enough
    private bool SameArea(Health h, Vector3 at, float distance)
    {
        Room theirs = targets.RoomOf(h.transform.position);
        if (area != null || theirs != null) return theirs == area;
        return Distance(h, at) <= distance;
    }

    // between two rooms (in a door frame): no room contains her, but one is right there
    private bool InDoorway(Vector3 at)
    {
        foreach (Room r in targets.Rooms)
            if (r != null && r.Contains(at, doorwayMargin)) return true;
        return false;
    }

    private int LowestFreeNumber()
    {
        int n = 1;
        while (numbers.ContainsValue(n)) n++;
        return n;
    }

    private static float Distance(Health h, Vector3 at)
    {
        Vector3 d = h.transform.position - at;
        d.y = 0f;
        return d.magnitude;
    }
}
