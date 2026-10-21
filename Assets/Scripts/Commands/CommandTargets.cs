using System;
using System.Collections.Generic;
using UnityEngine;

// Everything that can be named in a command, wired by the scene setup (Last Terminal/4), plus how to pick one of them.
// Rooms and doors answer to their display name, pickups to their item id, save stations to ComputerKey,
// shootables to their own key (the training dummies: "dummy").
// When several match, the command's modifier decides: "the other one" (not the last target), left / right
// (as seen on the operator's camera), nearest / farthest; with no modifier, things in her room first, then the nearest.
// Reach: she only goes as far as the next room. Rooms are linked by the doors between them; things in her room or in
// a room next to it can be named, anything farther is "too far" (the operator guides her room by room).
// Doors answer to their number too ("door 2").
public class CommandTargets : MonoBehaviour
{
    public const string RoomsField = nameof(rooms);
    public const string DoorsField = nameof(doors);
    public const string PickupsField = nameof(pickups);
    public const string StationsField = nameof(stations);
    public const string TablesField = nameof(tables);
    public const string KeypadsField = nameof(keypads);
    public const string ElevatorsField = nameof(elevators);
    public const string SlotsField = nameof(slots);
    public const string ShootablesField = nameof(shootables);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string PartnerField = nameof(partner);
    public const string CenterRoomField = nameof(centerRoom);
    public const string HoldemTablesField = nameof(holdemTables);
    public const string RouletteField = nameof(roulette);
    public const string CounterField = nameof(counter);

    // generic names (lexicon keys) that aren't the name of one particular thing
    public const string DoorKey = "door";
    public const string ComputerKey = "computer";
    public const string DummyKey = "dummy";
    public const string EnemyKey = "enemy";   // any shootable ("enemy", "inimigo", "number 2")
    public const string CenterKey = "center"; // "the middle of the room"
    public const string KeypadKey = "keypad";
    public const string ElevatorKey = "elevator";
    public const string SlotKey = "slot";
    public const string StopKeyPrefix = "stop_";     // an elevator stop ("go to the casino"): ElevatorStop.LexiconKey
    public const string ColorKeyPrefix = "color_";   // a key color ("the red elevator"): ElevatorKey.LexiconKey
    // casino games ("let's play poker") and the counter ("go to the cashier")
    public const string BlackjackKey = "game_blackjack";
    public const string PokerKey = "game_poker";
    public const string RouletteKey = "game_roulette";
    public const string CounterKey = "counter";
    // roulette bets (red / black are the color keys: color_red, color_black)
    public const string EvenKey = "rl_even";
    public const string OddKey = "rl_odd";
    public const string LowKey = "rl_low";
    public const string HighKey = "rl_high";
    public const string Dozen1Key = "rl_dozen1";
    public const string Dozen2Key = "rl_dozen2";
    public const string Dozen3Key = "rl_dozen3";
    public const string NumberBetKey = "rl_number";
    public const string ZeroKey = "rl_zero";
    // blackjack bets: "all in", "minimum", "maximum", "same bet"
    public const string AllInKey = "allin";
    public const string MinBetKey = "minbet";
    public const string MaxBetKey = "maxbet";
    public const string SameBetKey = "samebet";

    [Serializable]
    public class Shootable
    {
        public const string KeyField = nameof(key);
        public const string TargetField = nameof(target);

        [SerializeField] private string key = DummyKey;
        [SerializeField] private Health target;

        public string Key => key;
        public Health Target => target;
    }

    [SerializeField] private Room[] rooms = new Room[0];
    [SerializeField] private Door[] doors = new Door[0];
    [SerializeField] private Pickup[] pickups = new Pickup[0];
    [SerializeField] private SaveStation[] stations = new SaveStation[0];
    [SerializeField] private Shootable[] shootables = new Shootable[0];
    [SerializeField] private BlackjackGame[] tables = new BlackjackGame[0];
    [SerializeField] private Keypad[] keypads = new Keypad[0];
    [SerializeField] private Elevator[] elevators = new Elevator[0];
    [SerializeField] private SlotMachine[] slots = new SlotMachine[0];
    [SerializeField] private HoldemGame[] holdemTables = new HoldemGame[0];
    [Tooltip("Optional: the roulette of the scene.")]
    [SerializeField] private RouletteGame roulette;
    [Tooltip("Optional: the cashier / prize counter of the scene.")]
    [SerializeField] private PrizeCounter counter;
    [Tooltip("Left / right are judged on the camera the operator is watching.")]
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [Tooltip("Her transform: left / right from her point of view when the targets aren't on the camera.")]
    [SerializeField] private Transform partner;
    [Tooltip("Optional: \"the center\" with no room named goes here (the middle of a big hall), not to the middle of her room.")]
    [SerializeField] private Room centerRoom;
    [Tooltip("\"The door\" / \"shoot\" with no name: only things within this distance (m).")]
    [SerializeField] private float nearbyDistance = 8f;
    [Tooltip("A door belongs to the rooms within this distance of it; she counts as in a room this close to it (m).")]
    [SerializeField] private float doorwayMargin = 0.6f;
    [Header("Room center")]
    [Tooltip("Spots tried around the center, ring after ring (m).")]
    [SerializeField] private float centerStep = 0.75f;
    [SerializeField] private int centerRings = 4;
    [Tooltip("Spots closer than this to a wall are not used (m).")]
    [SerializeField] private float centerWallMargin = 0.6f;

    private readonly List<Component> candidates = new List<Component>();
    private readonly Dictionary<Room, HashSet<Room>> links = new Dictionary<Room, HashSet<Room>>();
    private readonly List<Vector3> spots = new List<Vector3>();

    // the last search left a matching thing out because it is farther than the next room
    public bool LastSearchOutOfReach { get; private set; }

    private void Awake()
    {
        // rooms linked by the doors between them
        foreach (Room r in rooms) if (r != null && !links.ContainsKey(r)) links.Add(r, new HashSet<Room>());
        foreach (Door d in doors)
        {
            if (d == null) continue;
            foreach (Room a in rooms)
                foreach (Room b in rooms)
                    if (a != null && b != null && a != b && DoorOf(a, d) && DoorOf(b, d)) links[a].Add(b);
        }
        // sections of one hall (Room.OpenTo, no door between them): she walks to any of them from any of them
        var group = new List<Room>();
        var stack = new Stack<Room>();
        foreach (Room start in rooms)
        {
            if (start == null) continue;
            group.Clear();
            stack.Push(start);
            while (stack.Count > 0)
            {
                Room r = stack.Pop();
                if (r == null || group.Contains(r)) continue;
                group.Add(r);
                foreach (Room next in r.OpenTo) if (next != null && links.ContainsKey(next)) stack.Push(next);
            }
            foreach (Room other in group) if (other != start) links[start].Add(other);
        }
    }

    public Room CenterRoom => centerRoom;

    public IReadOnlyList<Room> Rooms => rooms;
    public IReadOnlyList<Door> Doors => doors;
    public IReadOnlyList<Shootable> Shootables => shootables;

    // the table where a game is going on (she can only deal at one)
    public BlackjackGame ActiveTable
    {
        get
        {
            foreach (BlackjackGame t in tables) if (t != null && t.IsActive) return t;
            return null;
        }
    }

    public RouletteGame Roulette => roulette;
    public PrizeCounter Counter => counter;
    public bool HasBlackjack => tables.Length > 0;

    public HoldemGame ActiveHoldem
    {
        get
        {
            foreach (HoldemGame t in holdemTables) if (t != null && t.IsActive) return t;
            return null;
        }
    }

    public HoldemGame FindHoldem(Vector3 from)
    {
        candidates.Clear();
        LastSearchOutOfReach = false;
        foreach (HoldemGame t in holdemTables) if (t != null) AddInReach(t, from);
        return Choose(CommandModifier.None, from, null) as HoldemGame;
    }

    public BlackjackGame FindTable(Vector3 from)
    {
        candidates.Clear();
        LastSearchOutOfReach = false;
        foreach (BlackjackGame t in tables) if (t != null) AddInReach(t, from);
        return Choose(CommandModifier.None, from, null) as BlackjackGame;
    }

    public Room RoomOf(Vector3 position)
    {
        foreach (Room r in rooms)
            if (r != null && r.Contains(position)) return r;
        return null;
    }

    // her room; standing in a door frame: the room right there
    public Room CurrentRoom(Vector3 position)
    {
        Room r = RoomOf(position);
        if (r != null) return r;
        foreach (Room x in rooms)
            if (x != null && x.Contains(position, doorwayMargin)) return x;
        return null;
    }

    public bool DoorOf(Room room, Door door) => room != null && door != null && room.Contains(door.transform.position, doorwayMargin);

    // the same room or the next one (a door between them)
    public bool IsNextTo(Room from, Room to) => from == to || (from != null && to != null && links.TryGetValue(from, out HashSet<Room> l) && l.Contains(to));

    // something at this position can be named from where she is (her room or the next one); outside the rooms: yes
    public bool InReach(Vector3 from, Vector3 position)
    {
        Room here = CurrentRoom(from);
        Room there = CurrentRoom(position);
        return here == null || there == null || IsNextTo(here, there);
    }

    public Door DoorBetween(Room a, Room b)
    {
        foreach (Door d in doors)
            if (DoorOf(a, d) && DoorOf(b, d)) return d;
        return null;
    }

    // A spot in the middle of the room the room's cameras see well: the center, else the closest spot around it that
    // is free (on the NavMesh, not in a table / counter), away from the walls and not hidden from the cameras.
    public bool TryCenterSpot(Room room, out Vector3 spot, Func<Vector3, bool> reachable = null)
    {
        spot = Vector3.zero;
        if (room == null) return false;
        // its marked spot first (the place on the map)
        if (room.Spot != null && UnityEngine.AI.NavMesh.SamplePosition(room.Spot.position, out UnityEngine.AI.NavMeshHit marked, 0.5f, UnityEngine.AI.NavMesh.AllAreas)
            && (reachable == null || reachable(marked.position)))
        {
            spot = marked.position;
            return true;
        }
        Vector3 c = room.transform.position;
        spots.Clear();
        spots.Add(c);
        for (int ring = 1; ring <= centerRings; ring++)
        {
            int n = 8 * ring;
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                spots.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (centerStep * ring));
            }
        }
        foreach (Vector3 p in spots)
        {
            if (!room.Contains(p, -centerWallMargin)) continue;
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 0.3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (!SeenByRoomCamera(room, hit.position)) continue;
            if (reachable != null && !reachable(hit.position)) continue;   // blocked: try the next spot
            spot = hit.position;
            return true;
        }
        return false;
    }

    // seen (head and feet) by at least one camera of the room; a room without cameras: yes
    public bool SeenByRoomCamera(Room room, Vector3 feet)
    {
        if (cameraSystem == null) return true;
        bool any = false;
        foreach (SecurityCamera cam in cameraSystem.Cameras)
        {
            if (cam == null || cam.Room != room || !cam.gameObject.activeInHierarchy) continue;
            any = true;
            Vector3 eye = cam.Lens != null ? cam.Lens.position : cam.transform.position;
            if (Clear(eye, feet + Vector3.up * 1.5f) && Clear(eye, feet + Vector3.up * 0.3f)) return true;
        }
        return !any;
    }

    // nothing solid between the two points (the last few cm are her / what she stands on)
    public static bool Clear(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        return length < 0.01f || !Physics.Raycast(from, d / length, length - 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    public Room FindRoom(ParsedCommand cmd)
    {
        foreach (string key in cmd.Keys)
            foreach (Room r in rooms)
                if (r != null && r.DisplayName == key) return r;
        return null;
    }

    // named door; or, when allowNearby ("the door" / no name), any door within nearbyDistance
    // "door 2" = the door with that number. Reach is checked by the caller (a door of her room).
    public Door FindDoor(ParsedCommand cmd, Vector3 from, bool allowNearby, Component exclude = null)
    {
        candidates.Clear();
        if (cmd.Number > 0 && cmd.HasKey(DoorKey))
        {
            foreach (Door d in doors) if (d != null && d.Number == cmd.Number) return d;
            return null;
        }
        foreach (Door d in doors)
            if (d != null && cmd.HasKey(d.DisplayName)) candidates.Add(d);
        if (candidates.Count == 0 && allowNearby)
        {
            Room here = CurrentRoom(from);
            foreach (Door d in doors)
                if (d != null && Near(d.transform.position, from) && (here == null || DoorOf(here, d))) candidates.Add(d);
        }
        return Choose(cmd.Modifier, from, exclude) as Door;
    }

    // a locked door this key opens, nearest first
    public Door FindDoorFor(KeyItem key, Vector3 from)
    {
        candidates.Clear();
        LastSearchOutOfReach = false;
        Room here = CurrentRoom(from);
        foreach (Door d in doors)
        {
            if (d == null || !d.IsLocked || d.RequiredKey != key) continue;
            if (here == null || DoorOf(here, d)) candidates.Add(d);
            else LastSearchOutOfReach = true;
        }
        return Choose(CommandModifier.None, from, null) as Door;
    }

    // named item on the floor; with allowAny (no name), anything in her room
    public Pickup FindPickup(ParsedCommand cmd, Vector3 from, bool allowAny, Component exclude = null)
    {
        Room here = RoomOf(from);
        candidates.Clear();
        LastSearchOutOfReach = false;
        foreach (Pickup p in pickups)
        {
            if (p == null || !p.gameObject.activeInHierarchy || p.Item == null) continue;
            bool named = cmd.HasKey(p.Item.Id);
            if (named) AddInReach(p, from);
            else if (allowAny && here != null && here.Contains(p.transform.position)) candidates.Add(p);
        }
        return Choose(cmd.Modifier, from, exclude) as Pickup;
    }

    public SaveStation FindStation(Vector3 from)
    {
        candidates.Clear();
        LastSearchOutOfReach = false;
        foreach (SaveStation s in stations)
            if (s != null) AddInReach(s, from);
        return Choose(CommandModifier.None, from, null) as SaveStation;
    }

    // the keypad of her room (else the closest one in reach); null = none (LastSearchOutOfReach: one too far)
    public Keypad FindKeypad(Vector3 from)
    {
        LastSearchOutOfReach = false;
        Room here = CurrentRoom(from);
        Keypad best = null;
        float bestDistance = float.MaxValue;
        foreach (Keypad k in keypads)
        {
            if (k == null) continue;
            if (!InReach(from, k.transform.position)) { LastSearchOutOfReach = true; continue; }
            float d = (k.transform.position - from).sqrMagnitude;
            if (here != null && here.Contains(k.transform.position, 0.6f)) d -= 1000f;   // hers first
            if (d < bestDistance) { bestDistance = d; best = k; }
        }
        return best;
    }

    public IReadOnlyList<Elevator> Elevators => elevators;
    public IReadOnlyList<SlotMachine> Slots => slots;

    // "elevator 2", "the casino elevator" (a stop named), "the red elevator" (its key's color), else the closest one
    // (in reach); null = none (LastSearchOutOfReach: one too far)
    public Elevator FindElevator(ParsedCommand cmd, Vector3 from)
    {
        LastSearchOutOfReach = false;
        Elevator best = null;
        float bestScore = float.MaxValue;
        foreach (Elevator e in elevators)
        {
            if (e == null) continue;
            bool named = (cmd.Number > 0 && cmd.HasKey(ElevatorKey) && e.Number == cmd.Number) ||
                         (e.Destination != null && cmd.HasKey(e.Destination.LexiconKey)) ||
                         (e.RequiredKey != null && cmd.HasKey(e.RequiredKey.LexiconKey));
            bool anyNamed = cmd.Number > 0 && cmd.HasKey(ElevatorKey) || cmd.Keys.Exists(k => k.StartsWith(StopKeyPrefix) || k.StartsWith(ColorKeyPrefix));
            if (anyNamed && !named) continue;
            if (!InReach(from, e.transform.position)) { LastSearchOutOfReach = true; continue; }
            float d = (e.transform.position - from).sqrMagnitude;
            if (d < bestScore) { bestScore = d; best = e; }
        }
        return best;
    }

    // the closest slot machine in her room (in reach), not counting `busy` (the one she is at)
    public SlotMachine FindSlot(Vector3 from, int number = 0)
    {
        LastSearchOutOfReach = false;
        SlotMachine best = null;
        float bestDistance = float.MaxValue;
        foreach (SlotMachine m in slots)
        {
            if (m == null || (number > 0 && m.Number != number)) continue;
            if (!InReach(from, m.transform.position)) { LastSearchOutOfReach = true; continue; }
            float d = (m.transform.position - from).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = m; }
        }
        return best;
    }

    // named target (or, unless requireName, any within nearbyDistance), alive
    public Health FindShootable(ParsedCommand cmd, Vector3 from, bool requireName = false, Component exclude = null)
    {
        bool anyEnemy = cmd.HasKey(EnemyKey);
        bool anyName = anyEnemy;
        foreach (Shootable s in shootables) if (s != null && cmd.HasKey(s.Key)) { anyName = true; break; }
        if (requireName && !anyName) return null;
        candidates.Clear();
        LastSearchOutOfReach = false;
        foreach (Shootable s in shootables)
        {
            if (s == null || s.Target == null || s.Target.IsDead || !s.Target.gameObject.activeInHierarchy) continue;
            bool named = anyEnemy || cmd.HasKey(s.Key);
            if (anyName ? !named : !Near(s.Target.transform.position, from)) continue;
            AddInReach(s.Target, from);
        }
        return Choose(cmd.Modifier, from, exclude) as Health;
    }

    private void AddInReach(Component c, Vector3 from)
    {
        if (InReach(from, c.transform.position)) candidates.Add(c);
        else LastSearchOutOfReach = true;
    }

    // ------------------------------------------------------------------ choosing among candidates
    private Component Choose(CommandModifier modifier, Vector3 from, Component exclude)
    {
        if (modifier == CommandModifier.Other && exclude != null) candidates.Remove(exclude);
        if (candidates.Count == 0) return null;

        Component best = null;
        float bestValue = float.MaxValue;
        Camera view = cameraSystem != null ? cameraSystem.ViewCamera : null;
        bool onCamera = (modifier == CommandModifier.Left || modifier == CommandModifier.Right) && view != null && AnyInFront(view);
        foreach (Component c in candidates)
        {
            Vector3 p = c.transform.position;
            float value;
            switch (modifier)
            {
                case CommandModifier.Left:
                case CommandModifier.Right:
                    float x;
                    if (onCamera)
                    {
                        Vector3 v = view.WorldToViewportPoint(p);
                        if (v.z <= 0f) continue;   // behind the camera
                        x = v.x;
                    }
                    else x = partner != null ? Vector3.Dot(p - from, partner.right) : p.x;
                    value = modifier == CommandModifier.Left ? x : -x;
                    break;
                case CommandModifier.Nearest: value = Flat(p - from).magnitude; break;
                case CommandModifier.Farthest: value = -Flat(p - from).magnitude; break;
                default: value = Score(p, from); break;
            }
            if (value < bestValue) { best = c; bestValue = value; }
        }
        return best;
    }

    private bool AnyInFront(Camera view)
    {
        foreach (Component c in candidates)
            if (view.WorldToViewportPoint(c.transform.position).z > 0f) return true;
        return false;
    }

    private bool Near(Vector3 position, Vector3 from) => Flat(position - from).magnitude <= nearbyDistance;

    // things in her room come first, then the nearest
    private float Score(Vector3 position, Vector3 from)
    {
        Room here = RoomOf(from);
        float d = Flat(position - from).magnitude;
        return here != null && here.Contains(position) ? d : d + 1000f;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
