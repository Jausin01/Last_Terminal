using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Turns a command (typed now, spoken later: same entry point) into her actions, and makes her answer (PartnerVoice).
// Understood in the language chosen in the settings only. Results that come later (item taken, door locked,
// out of ammo...) are answered from the events of the systems that do the work.
// Combat: hostiles are numbered by CombatEncounter ("shoot enemy 2", "aim at 3"); "aim" keeps her gun on a target
// until it goes down or another command takes her elsewhere; a plain "shoot" fires at the target she is on.
// Casino games: "let's play" takes her to the nearest game ("play poker / blackjack / roulette" picks one): she deals
// blackjack or Casino Hold'em seated at a table, or stands at the roulette wheel as the croupier. While a game is on,
// the commands go to the game (anything else between rounds ends the game; during a hand she wants to finish it).
// The cashier / prize counter (the bar): "exchange my chips", "buy the pajamas" (she goes there first).
// In combat she only does combat: shoot / aim / switch weapons / reload / heal, and the combat moves: dodge, duck /
// get up, back off (away from the threat, in the room), take cover (the farthest spot of the room), run (out the door
// farthest from the threats), hold fire / fire at will, combat position / relax (PartnerCombatStance: keeping the
// distance with a gun, on guard with a melee weapon). Anything else: "not now". A bare number = aim at that enemy.
[RequireComponent(typeof(NavMeshAgent))]
public class PartnerCommands : MonoBehaviour
{
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string InteractorField = nameof(interactor);
    public const string CombatField = nameof(combat);
    public const string ShooterField = nameof(shooter);
    public const string WeaponHolderField = nameof(weaponHolder);
    public const string DoorUserField = nameof(doorUser);
    public const string SaveUserField = nameof(saveUser);
    public const string HealthField = nameof(health);
    public const string TargetsField = nameof(targets);
    public const string VoiceField = nameof(voice);
    public const string SettingsField = nameof(settings);
    public const string EnglishField = nameof(english);
    public const string PortugueseField = nameof(portuguese);
    public const string EncounterField = nameof(encounter);
    public const string EvasionField = nameof(evasion);
    public const string OutfitField = nameof(outfit);
    public const string CameraPoseField = nameof(cameraPose);
    public const string StanceField = nameof(stance);
    public const string KeypadUserField = nameof(keypadUser);
    public const string ElevatorUserField = nameof(elevatorUser);

    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private PartnerInteractor interactor;
    [SerializeField] private PartnerCombat combat;
    [SerializeField] private PartnerShooter shooter;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private PartnerDoorUser doorUser;
    [SerializeField] private PartnerSaveUser saveUser;
    [SerializeField] private Health health;
    [SerializeField] private CommandTargets targets;
    [SerializeField] private PartnerVoice voice;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CommandLexicon english;
    [SerializeField] private CommandLexicon portuguese;
    [Tooltip("Optional: numbered combat targets.")]
    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private PartnerEvasion evasion;
    [Tooltip("Optional: her outfits (\"put on the pajamas\").")]
    [SerializeField] private PartnerOutfit outfit;
    [Tooltip("\"Look at the camera\".")]
    [SerializeField] private PartnerCameraPose cameraPose;
    [Tooltip("\"Combat position\": she keeps her distance / stays on guard on her own.")]
    [SerializeField] private PartnerCombatStance stance;
    [Tooltip("Optional: keypads (\"type 1 7\", \"confirm\").")]
    [SerializeField] private PartnerKeypadUser keypadUser;
    [Tooltip("Optional: elevators (\"take the elevator to the casino\").")]
    [SerializeField] private PartnerElevatorUser elevatorUser;

    [Header("Tuning")]
    [Tooltip("Shots aim this high above the target's feet (m).")]
    [SerializeField] private float aimHeight = 1.2f;
    [Tooltip("Melee: she walks up to the target until this close, then strikes (m).")]
    [SerializeField] private float meleeRange = 1.1f;
    [SerializeField] private float meleeApproachTimeout = 8f;
    [Tooltip("\"Go to the door\": she stops this far in front of it (m).")]
    [SerializeField] private float doorStandOff = 1.0f;
    [Tooltip("Status: above this fraction of health she is fine, above the second one hurt, below badly hurt.")]
    [SerializeField] private Vector2 statusThresholds = new Vector2(0.66f, 0.33f);
    [Header("Combat moves")]
    [Tooltip("\"Back off\": she moves this far away from the threat (m).")]
    [SerializeField] private float backOffDistance = 3f;
    [Tooltip("Back off / take cover: spots this close to the walls are not used (m).")]
    [SerializeField] private float wallMargin = 0.7f;
    [Tooltip("Take cover: spots of the room tried on a grid this size (m).")]
    [SerializeField] private float coverGrid = 1f;
    [Tooltip("Fire at will: time between her shots (s).")]
    [SerializeField] private float fireAtWillInterval = 0.9f;

    private NavMeshAgent agent;
    private NavMeshPath path;
    private readonly Dictionary<GameLanguage, CommandParser> parsers = new Dictionary<GameLanguage, CommandParser>();
    private Component lastTarget;
    private Health meleeTarget;
    private float meleeUntil;
    private Health aimTarget;
    private bool fireAtWill;
    private float nextAutoShot;
    private Health coverFacing;      // backing off / taking cover with a melee weapon: turned toward it while she moves

    public event Action<ParsedCommand> CommandUnderstood;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        if (partnerAnimator == null || interactor == null || combat == null || shooter == null || weaponHolder == null ||
            doorUser == null || saveUser == null || health == null || targets == null || voice == null || settings == null ||
            english == null || portuguese == null || evasion == null)
        {
            Debug.LogError($"{name}: PartnerCommands is missing references (run Last Terminal/4).", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (interactor != null) interactor.PickupAttempted += OnPickupAttempted;
        if (doorUser != null) doorUser.Finished += OnDoorFinished;
        if (shooter != null) shooter.OutOfAmmo += OnOutOfAmmo;
        if (encounter != null) { encounter.CombatStarted += OnCombatStarted; encounter.TargetDown += OnTargetDown; encounter.CombatEnded += OnCombatEnded; }
        if (stance != null) stance.Cornered += OnCornered;
        if (keypadUser != null) keypadUser.KeyPressed += OnKeyPressed;
        if (elevatorUser != null) elevatorUser.Arrived += OnElevatorArrived;
        if (targets != null)
            foreach (SlotMachine m in targets.Slots)
                if (m != null) { m.SpinEnded += OnSpinEnded; m.NotEnoughChips += OnNoChips; }
        if (targets != null && targets.Roulette != null)
        {
            targets.Roulette.SpinStarted += OnRouletteSpin;
            targets.Roulette.Landed += OnRouletteLanded;
            targets.Roulette.NoBets += OnRouletteNoBets;
        }
    }

    private void OnDisable()
    {
        if (interactor != null) interactor.PickupAttempted -= OnPickupAttempted;
        if (doorUser != null) doorUser.Finished -= OnDoorFinished;
        if (shooter != null) shooter.OutOfAmmo -= OnOutOfAmmo;
        if (encounter != null) { encounter.CombatStarted -= OnCombatStarted; encounter.TargetDown -= OnTargetDown; encounter.CombatEnded -= OnCombatEnded; }
        if (stance != null) stance.Cornered -= OnCornered;
        if (keypadUser != null) keypadUser.KeyPressed -= OnKeyPressed;
        if (elevatorUser != null) elevatorUser.Arrived -= OnElevatorArrived;
        if (targets != null)
            foreach (SlotMachine m in targets.Slots)
                if (m != null) { m.SpinEnded -= OnSpinEnded; m.NotEnoughChips -= OnNoChips; }
        if (targets != null && targets.Roulette != null)
        {
            targets.Roulette.SpinStarted -= OnRouletteSpin;
            targets.Roulette.Landed -= OnRouletteLanded;
            targets.Roulette.NoBets -= OnRouletteNoBets;
        }
    }

    // ------------------------------------------------------------------ entry point
    public void Execute(string text)
    {
        if (!enabled || string.IsNullOrWhiteSpace(text) || health.IsDead) return;
        ParsedCommand cmd = ParserFor(settings.Language).Parse(text);
        Debug.Log($"[Command] \"{cmd.Text}\" -> {cmd.Verb} [{string.Join(", ", cmd.Keys)}]{(cmd.Pronoun ? " (it)" : "")}");
        CommandUnderstood?.Invoke(cmd);
        Run(cmd);
    }

    private void Run(ParsedCommand cmd)
    {
        meleeTarget = null;
        if (cameraPose != null && cmd.Verb != CommandVerb.Status) cameraPose.Stop();   // any order ends the pose

        // a game of blackjack is on: the table takes the command
        BlackjackGame game = targets.ActiveTable;
        if (game != null) { aimTarget = null; Blackjack(game, cmd); return; }
        HoldemGame poker = targets.ActiveHoldem;
        if (poker != null) { aimTarget = null; Holdem(poker, cmd); return; }
        if (RouletteOn) { aimTarget = null; Roulette(cmd); return; }

        // the cashier / prize counter ("troca o pijama" is changing clothes, not the cashier)
        if (cmd.Verb == CommandVerb.Exchange && HasOutfitKey(cmd)) cmd.Verb = CommandVerb.ChangeClothes;
        if (cmd.Verb == CommandVerb.Exchange || cmd.Verb == CommandVerb.Buy) { Counter(cmd); return; }

        // an outfit named ("put on the pajamas", "usa o pijama"): changing clothes
        if (HasOutfitKey(cmd) && (cmd.Verb == CommandVerb.None || cmd.Verb == CommandVerb.Use || cmd.Verb == CommandVerb.PickUp ||
                                  cmd.Verb == CommandVerb.Draw || cmd.Verb == CommandVerb.Bet))
            cmd.Verb = CommandVerb.ChangeClothes;

        // a slot machine: she plays it ("let's play the slots", "bet 25", "spin"); anything else first takes her off it
        if (keypadUser != null && !InCombat && HandleSlot(cmd)) return;

        // an elevator ("take elevator 2", "go to the casino", "the red elevator")
        if (elevatorUser != null && !InCombat && IsElevatorCommand(cmd)) { HandleElevator(cmd); return; }

        // a keypad: the code said ("type 1 7", or just "seven" / "confirm" while she is at it); anything else first
        // takes her hand off it
        if (keypadUser != null && !InCombat && HandleKeypad(cmd)) return;

        // in combat: only combat (a bare number = that enemy)
        if (InCombat)
        {
            if (cmd.Verb == CommandVerb.None && cmd.Number > 0) cmd.Verb = CommandVerb.Aim;
            if (!IsCombatCommand(cmd)) { voice.Say(PartnerLine.NotNow); return; }
        }

        // the aim stays on its target while she moves, shoots or reloads; anything else drops it
        if (!KeepsAim(cmd.Verb)) aimTarget = null;
        if (cmd.Verb == CommandVerb.None)
        {
            // just a place / thing: go there
            if (cmd.Keys.Count == 0 && !cmd.Pronoun) { voice.Say(PartnerLine.NotUnderstood); return; }
            cmd.Verb = CommandVerb.GoTo;
        }
        if (cmd.Verb == CommandVerb.Status) { SayStatus(); return; }
        if (IsBusy) { voice.Say(PartnerLine.Busy); return; }
        if (cmd.Verb == CommandVerb.Hit) cmd.Verb = CommandVerb.Attack;   // no game: "hit" = attack
        if (cmd.Verb == CommandVerb.Stand) cmd.Verb = CommandVerb.StandUp;  // no game: "stand (up)"

        // down: what needs her on her feet waits until she is up
        if (evasion.IsCrouched && NeedsToStand(cmd))
        {
            evasion.GetUp(() => Run(cmd));
            return;
        }

        switch (cmd.Verb)
        {
            case CommandVerb.GoTo: GoTo(cmd); break;
            case CommandVerb.Stop: Stop(); break;
            case CommandVerb.PickUp: PickUp(cmd); break;
            case CommandVerb.Open: UseDoor(cmd, DoorAction.Open); break;
            case CommandVerb.Close: UseDoor(cmd, DoorAction.Close); break;
            case CommandVerb.GoThrough: UseDoor(cmd, DoorAction.PassThrough); break;
            case CommandVerb.Use: Use(cmd); break;
            case CommandVerb.Heal: Heal(cmd); break;
            case CommandVerb.Draw: Draw(cmd); break;
            case CommandVerb.Holster: fireAtWill = false; EndStance(); combat.Holster(); voice.Say(PartnerLine.WeaponAway); break;
            case CommandVerb.Attack: Attack(cmd, false); break;
            case CommandVerb.Shoot: Attack(cmd, true); break;
            case CommandVerb.Aim: Aim(cmd); break;
            case CommandVerb.Play: JoinTable(cmd); break;
            case CommandVerb.Reload: Reload(); break;
            case CommandVerb.Save: Save(); break;
            case CommandVerb.Dodge: Dodge(); break;
            case CommandVerb.Duck: Duck(); break;
            case CommandVerb.StandUp: StandUp(); break;
            case CommandVerb.BackOff: BackOff(); break;
            case CommandVerb.TakeCover: TakeCover(); break;
            case CommandVerb.Flee: Flee(); break;
            case CommandVerb.HoldFire: HoldFire(); break;
            case CommandVerb.FireAtWill: FireAtWill(); break;
            case CommandVerb.ChangeClothes: ChangeClothes(cmd); break;
            case CommandVerb.LookAtCamera: LookAtCamera(); break;
            case CommandVerb.CombatStance: CombatStance(); break;
            case CommandVerb.StandDown: StandDown(); break;
            case CommandVerb.TurnLeftSide: TurnToCamera(PartnerCameraPose.Facing.LeftSide); break;
            case CommandVerb.TurnRightSide: TurnToCamera(PartnerCameraPose.Facing.RightSide); break;
            case CommandVerb.TurnBack: TurnToCamera(PartnerCameraPose.Facing.Back); break;
            case CommandVerb.TurnFront: TurnToCamera(PartnerCameraPose.Facing.Front); break;
            case CommandVerb.TypeCode: voice.Say(PartnerLine.NoKeypad); break;   // (in combat: TypeCode never gets here)
            default:
                // table words with no game going on
                voice.Say(cmd.Verb == CommandVerb.Bet || cmd.Verb == CommandVerb.Deal ? PartnerLine.BjNoGame : PartnerLine.NotUnderstood);
                break;
        }
    }

    private bool InCombat => encounter != null && encounter.InCombat;

    // what she still does in combat
    private bool IsCombatCommand(ParsedCommand cmd)
    {
        switch (cmd.Verb)
        {
            case CommandVerb.Shoot: case CommandVerb.Attack: case CommandVerb.Hit: case CommandVerb.Aim:
            case CommandVerb.Reload: case CommandVerb.Heal: case CommandVerb.Draw: case CommandVerb.Holster:
            case CommandVerb.Stop: case CommandVerb.Status:
            case CommandVerb.Dodge: case CommandVerb.Duck: case CommandVerb.StandUp: case CommandVerb.Stand:
            case CommandVerb.BackOff: case CommandVerb.TakeCover: case CommandVerb.Flee:
            case CommandVerb.HoldFire: case CommandVerb.FireAtWill:
            case CommandVerb.CombatStance: case CommandVerb.StandDown:
                return true;
            case CommandVerb.Use:
            case CommandVerb.PickUp:
                // a weapon ("use the shotgun") or a medkit, not doors / items on the floor
                if (TryWeapon(cmd, out _)) return true;
                return cmd.Verb == CommandVerb.Use && interactor.Inventory != null &&
                       cmd.Keys.Exists(k => interactor.Inventory.FindById(k) is HealingItem);
            default:
                return false;
        }
    }

    // verbs that leave her aim on its target
    private static bool KeepsAim(CommandVerb verb)
    {
        switch (verb)
        {
            case CommandVerb.Shoot: case CommandVerb.Attack: case CommandVerb.Aim: case CommandVerb.GoTo:
            case CommandVerb.Reload: case CommandVerb.Status: case CommandVerb.Draw: case CommandVerb.None:
            case CommandVerb.Dodge: case CommandVerb.Duck: case CommandVerb.StandUp: case CommandVerb.Stand:
            case CommandVerb.BackOff: case CommandVerb.TakeCover: case CommandVerb.HoldFire: case CommandVerb.FireAtWill:
            case CommandVerb.CombatStance:
                return true;
            default:
                return false;
        }
    }

    // down, she can still shoot, aim, reload, heal, talk; the rest (walking, a melee swing...) needs her up
    private bool NeedsToStand(ParsedCommand cmd)
    {
        switch (cmd.Verb)
        {
            case CommandVerb.Shoot: case CommandVerb.Aim: case CommandVerb.Reload: case CommandVerb.Heal:
            case CommandVerb.Draw: case CommandVerb.Holster: case CommandVerb.Stop: case CommandVerb.Status:
            case CommandVerb.Duck: case CommandVerb.StandUp: case CommandVerb.HoldFire: case CommandVerb.FireAtWill:
            case CommandVerb.CombatStance: case CommandVerb.StandDown:
                return false;
            case CommandVerb.Attack:
                // a melee swing needs her up (the weapon named, else the one selected)
                return TryWeapon(cmd, out int named) ? !IsRanged(named) : !combat.SelectedIsRanged;
            case CommandVerb.Use:
                return !(TryWeapon(cmd, out _) || cmd.Keys.Exists(k => interactor.Inventory != null && interactor.Inventory.FindById(k) is HealingItem));
            default:
                return true;
        }
    }

    private CommandParser ParserFor(GameLanguage language)
    {
        if (!parsers.TryGetValue(language, out CommandParser parser))
        {
            parser = new CommandParser(language == GameLanguage.Portuguese ? portuguese : english);
            parsers.Add(language, parser);
        }
        return parser;
    }

    // in the middle of something she can't drop (seated, door clip, picking up)
    private bool IsBusy => saveUser.IsBusy || !doorUser.IsInterruptible || interactor.IsBusy || partnerAnimator.IsScripted || evasion.IsBusy ||
                           (elevatorUser != null && elevatorUser.IsBusy);

    // ------------------------------------------------------------------ verbs
    // She only goes as far as the next room (or a door of her room): farther = "too far", the operator guides her.
    private void GoTo(ParsedCommand cmd)
    {
        Vector3 from = transform.position;
        Room here = targets.CurrentRoom(from);
        Room room = targets.FindRoom(cmd);
        if (cmd.HasKey(CommandTargets.CenterKey))
        {
            // "the center" alone: the middle of the hall (when the scene has one), else of her room
            Room center = room != null ? room : targets.CenterRoom != null && (here == null || targets.IsNextTo(here, targets.CenterRoom)) ? targets.CenterRoom : here;
            GoToCenter(center, here);
            return;
        }

        Door door = null;
        if (cmd.HasKey(CommandTargets.DoorKey))
        {
            // "door 2" / "the storage door": the door, not the room
            door = targets.FindDoor(cmd, from, false, lastTarget);
            if (door == null && cmd.Number > 0) { voice.Say(PartnerLine.NothingThere); return; }
            if (door == null && room != null) door = DoorTo(here, room);
            if (door == null) door = targets.FindDoor(cmd, from, true, lastTarget);
            if (door != null && here != null && !targets.DoorOf(here, door)) { voice.Say(PartnerLine.TooFar); return; }
        }
        if (door == null && room != null && here != null && !targets.IsNextTo(here, room)) { voice.Say(PartnerLine.TooFar); return; }

        bool outOfReach = false;
        Pickup pickup = targets.FindPickup(cmd, from, false, lastTarget);
        outOfReach |= targets.LastSearchOutOfReach;
        SaveStation station = null;
        if (cmd.HasKey(CommandTargets.ComputerKey)) { station = targets.FindStation(from); outOfReach |= targets.LastSearchOutOfReach; }
        Health shootable = targets.FindShootable(cmd, from, true, lastTarget);
        outOfReach |= targets.LastSearchOutOfReach;
        Component target = null;
        if (door != null) target = door;
        else if (room != null) target = room;
        else if (pickup != null) target = pickup;
        else if (station != null) target = station;
        else if (shootable != null) target = shootable;
        if (target == null && cmd.Pronoun && lastTarget != null && targets.InReach(from, lastTarget.transform.position)) target = lastTarget;
        if (target == null && outOfReach) { voice.Say(PartnerLine.TooFar); return; }
        if (target == null || !TryDestination(target, out Vector3 destination))
        {
            voice.Say(cmd.Keys.Count > 0 ? PartnerLine.NothingThere : PartnerLine.NotUnderstood);
            return;
        }

        if (!PathExists(destination))
        {
            // the next room behind its closed door: go through the door
            Door entrance = target is Room r ? DoorTo(here, r) : null;
            if (entrance != null && !entrance.IsOpen && doorUser.Use(entrance, DoorAction.PassThrough))
            {
                lastTarget = target;
                voice.Say(PartnerLine.OnMyWay);
                return;
            }
            voice.Say(PartnerLine.CantGetThere);
            return;
        }
        if (!interactor.MoveTo(destination)) { voice.Say(PartnerLine.Busy); return; }
        lastTarget = target;
        voice.Say(PartnerLine.OnMyWay);
    }

    // "the middle of the room": a free spot near the center the room's cameras see, one she can walk to
    private void GoToCenter(Room room, Room here)
    {
        if (room == null) { voice.Say(PartnerLine.NotUnderstood); return; }
        if (here != null && !targets.IsNextTo(here, room)) { voice.Say(PartnerLine.TooFar); return; }
        if (targets.TryCenterSpot(room, out Vector3 spot, PathExists))
        {
            if (!interactor.MoveTo(spot)) { voice.Say(PartnerLine.Busy); return; }
            lastTarget = room;
            voice.Say(PartnerLine.OnMyWay);
            return;
        }
        // the next room behind a closed door: through the door first
        Door entrance = room != here ? targets.DoorBetween(here, room) : null;
        if (entrance != null && !entrance.IsOpen && doorUser.Use(entrance, DoorAction.PassThrough))
        {
            lastTarget = room;
            voice.Say(PartnerLine.OnMyWay);
            return;
        }
        voice.Say(PartnerLine.CantGetThere);
    }

    private void LookAtCamera()
    {
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        combat.StopAiming();
        if (cameraPose == null) { voice.Say(PartnerLine.NotUnderstood); return; }
        switch (cameraPose.Begin())
        {
            case PartnerCameraPose.Result.Ok: voice.Say(PartnerLine.Acknowledge); break;
            case PartnerCameraPose.Result.NoCamera: voice.Say(PartnerLine.NoCamera); break;
            case PartnerCameraPose.Result.TooFar: voice.Say(PartnerLine.NoCamera); break;
            default: voice.Say(PartnerLine.CantGetThere); break;
        }
    }

    // "turn around" / "show your left side": back or a side toward the camera being watched, where she stands
    private void TurnToCamera(PartnerCameraPose.Facing facing)
    {
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        combat.StopAiming();
        if (cameraPose == null) { voice.Say(PartnerLine.NotUnderstood); return; }
        if (cameraPose.Turn(facing) != PartnerCameraPose.Result.Ok) { voice.Say(PartnerLine.NoCamera); return; }
        voice.Say(facing == PartnerCameraPose.Facing.Back ? PartnerLine.TurnBack :
                  facing == PartnerCameraPose.Facing.Front ? PartnerLine.Acknowledge : PartnerLine.TurnSide);
    }

    // she is at a keypad (or on her way): the voice listens mostly for the code
    public bool AtKeypad => keypadUser != null && keypadUser.Current is Keypad;

    // a bare code ("dezessete", "confirma") after she left the keypad: she goes back to it this close (m)
    private const float KeypadReturnDistance = 6f;

    // true = the command was for the keypad (handled)
    private bool HandleKeypad(ParsedCommand cmd)
    {
        string keys = KeypadInput.Keys(cmd.Text, settings.Language, out bool onlyKeys);
        bool named = cmd.Verb == CommandVerb.TypeCode || cmd.HasKey(CommandTargets.KeypadKey);
        // only keys, she already left the keypad she was just at ("dezessete" said late): back to that one
        Keypad back = !named && onlyKeys && !keypadUser.IsBusy && lastTarget is Keypad last && !last.IsOpen &&
                      (Flat(last.transform.position) - Flat(transform.position)).sqrMagnitude <= KeypadReturnDistance * KeypadReturnDistance
                      ? last : null;
        // at the keypad, a sentence with no order in it: the keys heard in it ("sete" + noise), or "what?" - she stays
        if (!named && AtKeypad && keypadUser.IsBusy && cmd.Verb == CommandVerb.None && cmd.Keys.Count == 0 && !cmd.Pronoun)
        {
            if (keys.Length == 0) { voice.Say(PartnerLine.NotUnderstood); return true; }
            named = true;
        }
        if (!named && back == null && !(keypadUser.IsBusy && onlyKeys))
        {
            // at the keypad, something else: hand down first, then that (asking how she is doesn't need it)
            if (!keypadUser.IsBusy || cmd.Verb == CommandVerb.Status) return false;
            keypadUser.Leave(() => Run(cmd));
            return true;
        }

        Keypad keypad = keypadUser.Current as Keypad;
        if (keypadUser.IsBusy && keypad == null) { keypadUser.Leave(() => Run(cmd)); return true; }   // at another panel (a slot)
        if (keypad == null) keypad = back;
        if (keypad == null) keypad = targets.FindKeypad(transform.position);
        if (keypad == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoKeypad); return true; }
        if (keypad.IsOpen && keys.Length > 0) { voice.Say(PartnerLine.DoorAlreadyOpen); return true; }
        bool already = keypadUser.IsBusy;
        if (!already && (IsBusy || evasion.IsCrouched)) { voice.Say(PartnerLine.Busy); return true; }
        Vector3 front = keypad.transform.position + Flat(keypad.transform.forward).normalized * 0.45f;
        bool reachable = NavMesh.SamplePosition(new Vector3(front.x, transform.position.y, front.z), out NavMeshHit spot, 0.5f, NavMesh.AllAreas) &&
                         PathExists(spot.position);
        if (!already && !reachable)
        {
            voice.Say(PartnerLine.CantGetThere);
            return true;
        }
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        if (!keypadUser.Use(keypad, keys)) { voice.Say(PartnerLine.Busy); return true; }
        lastTarget = keypad;
        if (keys.Length == 0) voice.Say(PartnerLine.KeypadWhich);       // "use the keypad": she waits for the numbers
        else if (!already) voice.Say(PartnerLine.Acknowledge);          // at it already: she just types
        return true;
    }

    // ------------------------------------------------------------------ elevators
    private bool IsElevatorCommand(ParsedCommand cmd) =>
        cmd.HasKey(CommandTargets.ElevatorKey) || cmd.Keys.Exists(k => k.StartsWith(CommandTargets.StopKeyPrefix, StringComparison.Ordinal));

    private void HandleElevator(ParsedCommand cmd)
    {
        if (elevatorUser.IsBusy) { voice.Say(PartnerLine.Busy); return; }
        if (IsBusy || evasion.IsCrouched) { voice.Say(PartnerLine.Busy); return; }
        Elevator e = targets.FindElevator(cmd, transform.position);
        if (e == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoElevator); return; }
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        lastTarget = e;
        // "go to the elevator": she only walks up to it; a ride is "take / use the elevator" or a destination ("go to the casino")
        if (cmd.Verb == CommandVerb.GoTo && !cmd.Keys.Exists(k => k.StartsWith(CommandTargets.StopKeyPrefix, StringComparison.Ordinal)))
        {
            voice.Say(interactor.MoveTo(e.Outside.position) ? PartnerLine.OnMyWay : PartnerLine.CantGetThere);
            return;
        }
        switch (elevatorUser.Ride(e))
        {
            case PartnerElevatorUser.Result.Ok: voice.Say(PartnerLine.ElevatorGoing); break;
            case PartnerElevatorUser.Result.NoKey: voice.Say(PartnerLine.ElevatorNeedKey); break;
            case PartnerElevatorUser.Result.OutOfService: voice.Say(PartnerLine.ElevatorOutOfService); break;
            default: voice.Say(PartnerLine.Busy); break;
        }
    }

    private void OnElevatorArrived(Elevator e) => voice.Say(PartnerLine.ElevatorArrived);

    // ------------------------------------------------------------------ slot machines
    // true = handled. At a machine: spin / bet / leave; "play the slots" / "the slot machine": she goes to the closest.
    private bool HandleSlot(ParsedCommand cmd)
    {
        var at = keypadUser.Current as SlotMachine;
        bool named = cmd.HasKey(CommandTargets.SlotKey);
        if (at != null)
        {
            switch (cmd.Verb)
            {
                case CommandVerb.Spin: case CommandVerb.Again: case CommandVerb.Yes: case CommandVerb.Play: case CommandVerb.Deal:
                    if (cmd.Number > 0) at.SetBet(cmd.Number);
                    if (at.IsSpinning) { voice.Say(PartnerLine.Busy); return true; }
                    keypadUser.Use(at, SlotMachine.Spin.ToString());
                    return true;
                case CommandVerb.Bet:
                    if (cmd.Number <= 0) { voice.Say(PartnerLine.BjPlaceBet); return true; }
                    at.SetBet(cmd.Number);
                    voice.Say(PartnerLine.Acknowledge);
                    return true;
                case CommandVerb.Quit: case CommandVerb.Stop: case CommandVerb.No:
                    keypadUser.Leave();
                    voice.Say(PartnerLine.Acknowledge);
                    return true;
                case CommandVerb.Status:
                    return false;
                default:
                    if (cmd.Verb == CommandVerb.None && cmd.Number > 0 && cmd.Keys.Count == 0)
                    {
                        at.SetBet(cmd.Number);   // a bare number at the machine: the bet
                        voice.Say(PartnerLine.Acknowledge);
                        return true;
                    }
                    if (named) return true;
                    keypadUser.Leave(() => Run(cmd));
                    return true;
            }
        }
        // "spin" again after she let go of the machine: the one she played
        if (!named && cmd.Verb == CommandVerb.Spin && lastTarget is SlotMachine last && targets.Slots.Count > 0)
        {
            if (keypadUser.IsBusy || IsBusy) { voice.Say(PartnerLine.Busy); return true; }
            if (keypadUser.Use(last, SlotMachine.Spin.ToString())) return true;
        }
        if (!named || targets.Slots.Count == 0) return false;
        if (keypadUser.IsBusy || IsBusy || evasion.IsCrouched) { voice.Say(PartnerLine.Busy); return true; }
        SlotMachine machine = targets.FindSlot(transform.position);
        if (machine == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return true; }
        if (cmd.Number > 0) machine.SetBet(cmd.Number);
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        if (!keypadUser.Use(machine, cmd.Verb == CommandVerb.Spin ? SlotMachine.Spin.ToString() : "")) { voice.Say(PartnerLine.CantGetThere); return true; }
        lastTarget = machine;
        voice.Say(PartnerLine.SlotReady);
        return true;
    }

    private void OnSpinEnded(SlotMachine machine, int win)
    {
        if (win >= machine.Bet * 25) voice.Say(PartnerLine.SlotJackpot);
        else if (win > machine.Bet) voice.Say(PartnerLine.SlotWin);
    }

    private void OnNoChips(SlotMachine machine) => voice.Say(PartnerLine.SlotNoChips);

    private void OnKeyPressed(ButtonPanel panel, char key)
    {
        if (!(panel is Keypad keypad) || key != Keypad.Enter) return;
        if (keypad.LastResult == Keypad.Result.Accepted) voice.Say(PartnerLine.KeypadOpen);
        else if (keypad.LastResult == Keypad.Result.Rejected) voice.Say(PartnerLine.KeypadWrong);
    }

    private void Stop()
    {
        meleeTarget = null;
        fireAtWill = false;
        EndStance();
        combat.StopAiming();
        if (!evasion.IsCrouched) interactor.MoveTo(transform.position);   // down: she just stays down
        voice.Say(PartnerLine.Stopping);
    }

    private void PickUp(ParsedCommand cmd)
    {
        // "pick up the shotgun" = take the weapon out
        if (TryWeapon(cmd, out int weapon)) { DrawWeapon(weapon); return; }

        Pickup pickup = null;
        if (cmd.Keys.Count == 0 && cmd.Pronoun && lastTarget is Pickup last && last.gameObject.activeInHierarchy) pickup = last;
        if (pickup == null) pickup = targets.FindPickup(cmd, transform.position, cmd.Keys.Count == 0, lastTarget);
        if (pickup == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return; }
        if (!PathExists(pickup.InteractionPosition)) { voice.Say(PartnerLine.CantGetThere); return; }
        if (!interactor.PickUp(pickup)) { voice.Say(PartnerLine.Busy); return; }
        lastTarget = pickup;
        voice.Say(PartnerLine.Acknowledge);
    }

    private void UseDoor(ParsedCommand cmd, DoorAction action)
    {
        Door door = targets.FindDoor(cmd, transform.position, false, lastTarget);
        if (door == null && cmd.Pronoun && lastTarget is Door last) door = last;
        if (door == null && cmd.Number <= 0)
        {
            // a room was named ("open the storage"): the door to it
            Room room = targets.FindRoom(cmd);
            if (room != null) door = DoorTo(targets.CurrentRoom(transform.position), room);
        }
        if (door == null && cmd.Number <= 0) door = targets.FindDoor(cmd, transform.position, true, lastTarget);
        if (door == null) { voice.Say(PartnerLine.NothingThere); return; }
        // only the doors of the room she is in
        Room here = targets.CurrentRoom(transform.position);
        if (here != null && !targets.DoorOf(here, door)) { voice.Say(PartnerLine.TooFar); return; }
        lastTarget = door;

        if (doorUser.Use(door, action)) { voice.Say(PartnerLine.Acknowledge); return; }

        // nothing to do with the door itself
        if (action == DoorAction.Open && door.IsOpen) voice.Say(PartnerLine.DoorAlreadyOpen);
        else if (action == DoorAction.Close && !door.IsOpen) voice.Say(PartnerLine.DoorAlreadyClosed);
        else if (action == DoorAction.Close && door.IsRemoteControlled) voice.Say(PartnerLine.DoorNeedsOperator);
        else if (action == DoorAction.PassThrough && door.IsOpen && WalkThrough(door)) voice.Say(PartnerLine.OnMyWay);
        else voice.Say(PartnerLine.CantGetThere);
    }

    // open door: walk to the other side
    private bool WalkThrough(Door door)
    {
        Transform t = door.transform;
        float side = t.InverseTransformPoint(transform.position).z > 0f ? -1f : 1f;
        Vector3 beyond = t.position + t.forward * (side * 1.2f);
        return NavMesh.SamplePosition(beyond, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && PathExists(hit.position) && interactor.MoveTo(hit.position);
    }

    private void Use(ParsedCommand cmd)
    {
        if (TryWeapon(cmd, out int weapon)) { DrawWeapon(weapon); return; }
        if (cmd.HasKey(CommandTargets.ComputerKey)) { Save(); return; }

        Inventory inventory = interactor.Inventory;
        foreach (string key in cmd.Keys)
        {
            ItemDefinition item = inventory != null ? inventory.FindById(key) : null;
            if (item is HealingItem) { Heal(cmd); return; }
            if (item is KeyItem keyItem)
            {
                Door locked = targets.FindDoorFor(keyItem, transform.position);
                if (locked == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NothingThere); return; }
                lastTarget = locked;
                if (doorUser.Use(locked, DoorAction.PassThrough)) voice.Say(PartnerLine.Acknowledge);
                else voice.Say(PartnerLine.CantGetThere);
                return;
            }
        }
        if (targets.FindDoor(cmd, transform.position, false) != null || (cmd.Pronoun && lastTarget is Door))
        {
            UseDoor(cmd, DoorAction.PassThrough);
            return;
        }
        // an item she doesn't carry ("use the keycard" without it)
        foreach (string key in cmd.Keys)
            if (key.StartsWith(ItemKeyPrefix, StringComparison.Ordinal)) { voice.Say(PartnerLine.DontHaveThat); return; }
        voice.Say(PartnerLine.NotUnderstood);
    }

    private void Heal(ParsedCommand cmd)
    {
        Inventory inventory = interactor.Inventory;
        HealingItem item = null;
        foreach (string key in cmd.Keys)
            if (inventory.FindById(key) is HealingItem named) { item = named; break; }
        if (item == null) item = inventory.FindFirst<HealingItem>();

        if (item == null) voice.Say(PartnerLine.NoHealing);
        else if (health.IsFull) voice.Say(PartnerLine.NotHurt);
        else voice.Say(inventory.TryUse(item) ? PartnerLine.Healed : PartnerLine.NotHurt);
    }

    private void Draw(ParsedCommand cmd)
    {
        if (TryWeapon(cmd, out int weapon)) { DrawWeapon(weapon); return; }
        if (HasUnknownWeapon(cmd)) { voice.Say(PartnerLine.DontHaveThat); return; }
        voice.Say(combat.Draw() ? PartnerLine.WeaponReady : PartnerLine.Busy);
    }

    private void DrawWeapon(int index)
    {
        if (!combat.SelectWeapon(index) || !combat.Draw()) { voice.Say(PartnerLine.Busy); return; }
        voice.Say(PartnerLine.WeaponReady);
    }

    private void Attack(ParsedCommand cmd, bool shoot)
    {
        if (TryWeapon(cmd, out int weapon) && !combat.SelectWeapon(weapon)) { voice.Say(PartnerLine.Busy); return; }

        if (!TryCombatTarget(cmd, out Health target)) return;

        // "shoot" with a melee weapon in hand: the first firearm of the loadout
        if (shoot && !combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { voice.Say(PartnerLine.NoGun); return; }
            if (!combat.SelectWeapon(ranged)) { voice.Say(PartnerLine.Busy); return; }
        }

        if (combat.SelectedIsRanged)
        {
            if (target == null) { voice.Say(PartnerLine.NoTarget); return; }
            SetTarget(target);
            if (aimTarget != null) aimTarget = target;   // aiming: the aim follows the new target
            // an empty gun answers through PartnerShooter.OutOfAmmo
            combat.Fire(AimPoint(target));
            return;
        }

        // melee
        if (target == null) { combat.Attack(); return; }
        SetTarget(target);
        Vector3 to = Flat(target.transform.position - transform.position);
        if (to.magnitude <= meleeRange) { combat.Attack(target.transform.position + Vector3.up * aimHeight); return; }
        Vector3 stand = target.transform.position - to.normalized * (meleeRange * 0.8f);
        if (!NavMesh.SamplePosition(stand, out NavMeshHit hit, 0.8f, NavMesh.AllAreas) || !PathExists(hit.position) || !interactor.MoveTo(hit.position))
        {
            voice.Say(PartnerLine.CantGetThere);
            return;
        }
        combat.Draw();
        meleeTarget = target;
        meleeUntil = Time.time + meleeApproachTimeout;
        voice.Say(PartnerLine.Acknowledge);
    }

    // ------------------------------------------------------------------ combat moves
    private void Dodge()
    {
        if (evasion.IsCrouched) { evasion.GetUp(Dodge); return; }   // from down: up, then the dodge
        if (!evasion.Dodge()) { voice.Say(PartnerLine.Busy); return; }
        meleeTarget = null;
        voice.Say(PartnerLine.Dodging);
    }

    private void Duck()
    {
        if (evasion.IsCrouched) { voice.Say(PartnerLine.Acknowledge); return; }
        meleeTarget = null;
        voice.Say(evasion.Duck() ? PartnerLine.Ducking : PartnerLine.Busy);
    }

    private void StandUp()
    {
        if (!evasion.IsCrouched) { voice.Say(PartnerLine.Acknowledge); return; }
        voice.Say(evasion.GetUp() ? PartnerLine.GettingUp : PartnerLine.Busy);
    }

    // away from the threat, staying in her room (straight away first, then turning a bit, then closer)
    private void BackOff()
    {
        Health threat = Threat();
        if (threat == null) { voice.Say(PartnerLine.NoThreat); return; }
        Vector3 away = Flat(transform.position - threat.transform.position);
        away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
        Room room = encounter != null ? encounter.Area : null;
        foreach (float scale in new[] { 1f, 0.6f })
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                Vector3 wanted = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * away * (backOffDistance * scale);
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (room != null && !room.Contains(hit.position, -wallMargin)) continue;
                if (!PathExists(hit.position) || !interactor.MoveTo(hit.position)) continue;
                CoverWhileMoving(threat);
                voice.Say(PartnerLine.BackingOff);
                return;
            }
        voice.Say(PartnerLine.CantGetThere);
    }

    // the spot of her room farthest from every threat (that she can walk to)
    private void TakeCover()
    {
        Room room = encounter != null ? encounter.Area : null;
        if (Threat() == null || room == null) { voice.Say(PartnerLine.NoThreat); return; }
        Rect r = room.FloorRect;
        float y = transform.position.y;
        float best = encounter.DistanceToThreat(transform.position) + 0.5f;   // must be worth the walk
        Vector3 bestSpot = transform.position;
        bool found = false;
        for (float x = r.xMin + wallMargin; x <= r.xMax - wallMargin; x += coverGrid)
            for (float z = r.yMin + wallMargin; z <= r.yMax - wallMargin; z += coverGrid)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, y, z), out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
                float d = encounter.DistanceToThreat(hit.position);
                if (d <= best || !room.Contains(hit.position, -wallMargin * 0.5f) || !PathExists(hit.position)) continue;
                best = d;
                bestSpot = hit.position;
                found = true;
            }
        if (!found) { voice.Say(PartnerLine.NoWayOut); return; }
        if (!interactor.MoveTo(bestSpot)) { voice.Say(PartnerLine.Busy); return; }
        CoverWhileMoving(Threat());
        voice.Say(PartnerLine.TakingCover);
    }

    // out of the room through the door farthest from the threats (combat ends once she is out)
    private void Flee()
    {
        Room room = encounter != null ? encounter.Area : null;
        if (Threat() == null || room == null) { voice.Say(PartnerLine.NoThreat); return; }
        Door best = null;
        float bestScore = float.MinValue;
        foreach (Door d in targets.Doors)
        {
            if (d == null || !room.Contains(d.transform.position, 1f) || (d.IsLocked && !d.IsOpen)) continue;
            if (!d.IsOpen && d.IsRemoteControlled) continue;   // she can't open it herself
            float score = encounter.DistanceToThreat(d.transform.position) - Flat(d.transform.position - transform.position).magnitude * 0.3f;
            if (score > bestScore) { bestScore = score; best = d; }
        }
        if (best == null) { voice.Say(PartnerLine.NoWayOut); return; }
        bool going = best.IsOpen ? WalkThrough(best) : doorUser.Use(best, DoorAction.PassThrough);
        if (!going) { voice.Say(PartnerLine.NoWayOut); return; }
        aimTarget = null;
        fireAtWill = false;
        meleeTarget = null;
        EndStance();
        combat.StopAiming();
        lastTarget = best;
        voice.Say(PartnerLine.Retreating);
    }

    private void HoldFire()
    {
        fireAtWill = false;
        shooter.CancelPending();
        voice.Say(PartnerLine.HoldingFire);
    }

    // she picks her targets herself (the one she is on, then the closest) and keeps shooting until told to stop
    private void FireAtWill()
    {
        if (!combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { voice.Say(PartnerLine.NoGun); return; }
            if (!combat.SelectWeapon(ranged)) { voice.Say(PartnerLine.Busy); return; }
        }
        if (!combat.Draw()) { voice.Say(PartnerLine.Busy); return; }
        if (Threat() == null) { voice.Say(PartnerLine.NoTarget); return; }
        fireAtWill = true;
        nextAutoShot = 0f;
        voice.Say(PartnerLine.FireAtWill);
    }

    // with a weapon in hand she keeps facing the threat while she moves (she backpedals / strafes):
    // a gun stays up on it, a melee weapon stays on guard toward it until she gets there
    private void CoverWhileMoving(Health threat)
    {
        if (threat == null || !combat.IsArmed) return;
        if (combat.SelectedIsRanged)
        {
            if (!Alive(aimTarget)) { aimTarget = threat; SetTarget(threat); }
            return;
        }
        coverFacing = threat;
        combat.FaceToward(threat.transform.position);
    }

    // "combat position": she keeps her distance with a gun, stays on guard with a melee weapon (PartnerCombatStance)
    private void CombatStance()
    {
        if (stance == null) { voice.Say(PartnerLine.NotUnderstood); return; }
        if (!stance.Begin()) { voice.Say(PartnerLine.Busy); return; }
        voice.Say(combat.SelectedIsRanged ? PartnerLine.StanceRanged : PartnerLine.StanceMelee);
    }

    // "relax": out of the stance, weapon still in hand (lowered)
    private void StandDown()
    {
        fireAtWill = false;
        aimTarget = null;
        EndStance();
        combat.StopAiming();
        voice.Say(PartnerLine.StandingDown);
    }

    private void EndStance()
    {
        coverFacing = null;
        if (stance != null) stance.End();
        combat.StopFacing();
    }

    private void OnCornered() => voice.Say(PartnerLine.Cornered);

    // the hostile she is dealing with: her aim, the focus, else the closest one
    private Health Threat()
    {
        if (Alive(aimTarget)) return aimTarget;
        if (encounter == null || !encounter.InCombat) return null;
        if (Alive(encounter.Focus)) return encounter.Focus;
        return encounter.Nearest(transform.position);
    }

    private void UpdateFireAtWill()
    {
        if (!InCombat) { fireAtWill = false; return; }
        if (!combat.IsArmed || !combat.SelectedIsRanged || partnerAnimator.IsScripted || evasion.IsBusy) return;
        Health target = Threat();
        if (target == null) return;
        if (aimTarget != target) { aimTarget = target; SetTarget(target); }
        if (shooter.IsBusy || Time.time < nextAutoShot) return;
        if (shooter.CurrentRounds <= 0)
        {
            if (shooter.CurrentReserve > 0) { combat.Reload(); voice.Say(PartnerLine.Reloading); }
            else { fireAtWill = false; voice.Say(PartnerLine.OutOfAmmo); }
            nextAutoShot = Time.time + fireAtWillInterval;
            return;
        }
        combat.Fire(AimPoint(target));
        nextAutoShot = Time.time + fireAtWillInterval;
    }

    // ------------------------------------------------------------------ outfits
    // the outfit named, or the next one ("change clothes")
    private void ChangeClothes(ParsedCommand cmd)
    {
        if (outfit == null) { voice.Say(PartnerLine.NotUnderstood); return; }
        string key = cmd.Keys.Find(k => k.StartsWith(OutfitKeyPrefix, StringComparison.Ordinal));
        OutfitDefinition next;
        if (key == null) next = outfit.Next();
        else if (!outfit.TryFind(key, out next)) { voice.Say(PartnerLine.DontHaveThat); return; }
        if (!outfit.IsOwned(next)) { voice.Say(PartnerLine.OutfitLocked); return; }   // a casino prize not bought yet
        if (next == outfit.Current) { voice.Say(PartnerLine.OutfitSame); return; }
        outfit.Wear(next);
        voice.Say(next != null ? next.Line : PartnerLine.OutfitChanged);
    }

    private static bool HasOutfitKey(ParsedCommand cmd) => cmd.Keys.Exists(k => k.StartsWith(OutfitKeyPrefix, StringComparison.Ordinal));

    // ------------------------------------------------------------------ casino games
    // "let's play": the game named ("poker", "blackjack", "roulette"), else the closest one in reach
    private void JoinTable(ParsedCommand cmd)
    {
        Vector3 from = transform.position;
        bool wantsBlackjack = cmd.HasKey(CommandTargets.BlackjackKey);
        bool wantsPoker = cmd.HasKey(CommandTargets.PokerKey);
        bool wantsRoulette = cmd.HasKey(CommandTargets.RouletteKey);
        bool any = !wantsBlackjack && !wantsPoker && !wantsRoulette;
        bool tooFar = false;

        BlackjackGame bj = any || wantsBlackjack ? targets.FindTable(from) : null;
        tooFar |= targets.LastSearchOutOfReach;
        HoldemGame holdem = any || wantsPoker ? targets.FindHoldem(from) : null;
        tooFar |= targets.LastSearchOutOfReach;
        RouletteGame wheel = (any || wantsRoulette) && targets.Roulette != null ? targets.Roulette : null;
        if (wheel != null && !targets.InReach(from, wheel.transform.position)) { wheel = null; tooFar = true; }

        float dBj = bj != null ? (bj.transform.position - from).sqrMagnitude : float.MaxValue;
        float dPoker = holdem != null ? (holdem.transform.position - from).sqrMagnitude : float.MaxValue;
        float dWheel = wheel != null ? (wheel.transform.position - from).sqrMagnitude : float.MaxValue;
        if (wheel != null && dWheel <= dBj && dWheel <= dPoker) { JoinRoulette(wheel); return; }
        if (holdem != null && dPoker <= dBj) { PlayHoldem(holdem); return; }
        if (bj == null && tooFar) { voice.Say(PartnerLine.TooFar); return; }
        PlayAt(bj);
    }

    // "let's play" (or a click on the table): she goes to sit as the dealer
    public void PlayAt(BlackjackGame table)
    {
        if (!enabled || health.IsDead) return;
        if (InCombat) { voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || evasion.IsCrouched) { voice.Say(PartnerLine.Busy); return; }
        if (table == null) { voice.Say(PartnerLine.BjNoTable); return; }
        if (!PathExists(table.Table.Seat.GetWalkInPosition(transform.position))) { voice.Say(PartnerLine.CantGetThere); return; }
        voice.Say(table.Join() ? PartnerLine.BjAccept : PartnerLine.Busy);
    }

    public void PlayHoldem(HoldemGame table)
    {
        if (!enabled || health.IsDead) return;
        if (InCombat) { voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || evasion.IsCrouched) { voice.Say(PartnerLine.Busy); return; }
        if (!PathExists(table.Table.Seat.GetWalkInPosition(transform.position))) { voice.Say(PartnerLine.CantGetThere); return; }
        voice.Say(table.Join() ? PartnerLine.BjAccept : PartnerLine.Busy);
    }

    private void Holdem(HoldemGame game, ParsedCommand cmd)
    {
        int ante = HoldemAmount(game, cmd);
        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); break;
            case CommandVerb.Bet:
                if (ante > 0) game.Bet(ante);
                else if (cmd.HasKey(CommandTargets.SameBetKey)) game.Continue();
                else voice.Say(PartnerLine.BjPlaceBet);
                break;
            case CommandVerb.Play:
            case CommandVerb.Deal:
            case CommandVerb.Yes:
            case CommandVerb.Again:
            case CommandVerb.Draw:
                if (ante > 0 && game.IsBetweenRounds) game.Bet(ante);
                else game.Continue();
                break;
            case CommandVerb.Call: game.Call(); break;
            case CommandVerb.Surrender: game.Fold(); break;   // "fold"
            case CommandVerb.No: game.Decline(); break;
            case CommandVerb.Quit:
            case CommandVerb.Stop: game.Quit(); break;
            case CommandVerb.None:
                if (ante > 0 && game.IsBetweenRounds) game.Bet(ante);
                else voice.Say(PartnerLine.NotUnderstood);
                break;
            default:
                if (game.IsBetweenRounds) game.Quit();
                else voice.Say(PartnerLine.BjFinishHand);
                break;
        }
    }

    private static int HoldemAmount(HoldemGame game, ParsedCommand cmd)
    {
        if (cmd.HasKey(CommandTargets.AllInKey) || cmd.HasKey(CommandTargets.MaxBetKey)) return Mathf.Min(game.Chips / 3, game.MaxBet);
        if (cmd.HasKey(CommandTargets.MinBetKey)) return game.MinBet;
        if (cmd.HasKey(CommandTargets.SameBetKey) && game.Engine != null && game.Engine.LastAnte > 0) return game.Engine.LastAnte;
        return cmd.Number;
    }

    // ------------------------------------------------------------------ roulette
    private bool RouletteOn => targets.Roulette != null && targets.Roulette.IsOpen;

    // "let's play roulette": she goes to the wheel (the croupier's place) and calls for bets
    private void JoinRoulette(RouletteGame wheel)
    {
        if (InCombat) { voice.Say(PartnerLine.NotNow); return; }
        if (IsBusy || evasion.IsCrouched || keypadUser == null) { voice.Say(PartnerLine.Busy); return; }
        EndStance();
        aimTarget = null;
        fireAtWill = false;
        if (!keypadUser.Use(wheel, "")) { voice.Say(PartnerLine.CantGetThere); return; }
        wheel.Open();
        lastTarget = wheel;
        voice.Say(PartnerLine.RouletteBets);
    }

    private void LeaveRoulette(Action then)
    {
        RouletteGame wheel = targets.Roulette;
        wheel.Close();
        if (keypadUser.Current == wheel) keypadUser.Leave(then);
        else then?.Invoke();
    }

    private void Roulette(ParsedCommand cmd)
    {
        RouletteGame wheel = targets.Roulette;
        if (keypadUser.Current != wheel) { wheel.Close(); Run(cmd); return; }   // she left the wheel
        ParseNumbers(cmd, rouletteNumbers);
        RouletteGame.BetKind kind;
        bool hasKind = TryBetKind(cmd, out kind);

        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); return;
            case CommandVerb.Spin:
            case CommandVerb.Again:
            case CommandVerb.Yes:
            case CommandVerb.Deal:
            case CommandVerb.Play:
                if (wheel.IsSpinning) { voice.Say(PartnerLine.Busy); return; }
                if (hasKind && !PlaceRouletteBet(wheel, kind, cmd)) return;
                if (wheel.Bets.Count == 0 && (cmd.Verb == CommandVerb.Again || cmd.HasKey(CommandTargets.SameBetKey)) && wheel.HasLastBets)
                    if (!SayBetResult(wheel.RepeatLastBets(), false)) return;
                if (wheel.Bets.Count == 0) { voice.Say(PartnerLine.RouletteNeedBet); return; }
                keypadUser.Use(wheel, RouletteGame.SpinKey.ToString());
                return;
            case CommandVerb.ClearBets:
                if (wheel.IsSpinning) { voice.Say(PartnerLine.Busy); return; }
                wheel.ClearBets();
                voice.Say(PartnerLine.Acknowledge);
                return;
            case CommandVerb.Quit:
            case CommandVerb.No:
            case CommandVerb.Stop:
                if (wheel.IsSpinning) { voice.Say(PartnerLine.BjFinishHand); return; }
                LeaveRoulette(null);
                voice.Say(PartnerLine.BjGameOver);
                return;
        }

        if (hasKind) { PlaceRouletteBet(wheel, kind, cmd); return; }
        if (cmd.HasKey(CommandTargets.SameBetKey) && wheel.HasLastBets) { SayBetResult(wheel.RepeatLastBets(), true); return; }
        // numbers alone: "bet 25" = the stake; "17" / "10 on 17" = on that number
        if (rouletteNumbers.Count > 0 && (cmd.Verb == CommandVerb.Bet || cmd.Verb == CommandVerb.None))
        {
            if (cmd.Verb == CommandVerb.Bet && rouletteNumbers.Count == 1) { wheel.SetStake(rouletteNumbers[0]); voice.Say(PartnerLine.Acknowledge); return; }
            PlaceRouletteBet(wheel, RouletteGame.BetKind.Straight, cmd);
            return;
        }
        if (cmd.Verb == CommandVerb.Bet) { voice.Say(PartnerLine.RouletteBets); return; }
        // anything else: the game ends, then the order (not while the ball runs)
        if (wheel.IsSpinning) { voice.Say(PartnerLine.BjFinishHand); return; }
        LeaveRoulette(() => Run(cmd));
    }

    private readonly List<int> rouletteNumbers = new List<int>();

    // the numbers said: digits in the text ("10 on 17"), else the spoken one
    private static void ParseNumbers(ParsedCommand cmd, List<int> into)
    {
        into.Clear();
        string t = cmd.Text ?? "";
        int value = -1;
        for (int i = 0; i <= t.Length; i++)
        {
            char c = i < t.Length ? t[i] : ' ';
            if (char.IsDigit(c)) { value = (value < 0 ? 0 : value * 10) + (c - '0'); continue; }
            if (value >= 0) { into.Add(value); value = -1; }
        }
        if (into.Count == 0 && cmd.Number > 0) into.Add(cmd.Number);
    }

    private static bool TryBetKind(ParsedCommand cmd, out RouletteGame.BetKind kind)
    {
        kind = RouletteGame.BetKind.Straight;
        if (cmd.HasKey(CommandTargets.ColorKeyPrefix + "red")) { kind = RouletteGame.BetKind.Red; return true; }
        if (cmd.HasKey(CommandTargets.ColorKeyPrefix + "black")) { kind = RouletteGame.BetKind.Black; return true; }
        if (cmd.HasKey(CommandTargets.EvenKey)) { kind = RouletteGame.BetKind.Even; return true; }
        if (cmd.HasKey(CommandTargets.OddKey)) { kind = RouletteGame.BetKind.Odd; return true; }
        if (cmd.HasKey(CommandTargets.LowKey)) { kind = RouletteGame.BetKind.Low; return true; }
        if (cmd.HasKey(CommandTargets.HighKey)) { kind = RouletteGame.BetKind.High; return true; }
        if (cmd.HasKey(CommandTargets.Dozen1Key)) { kind = RouletteGame.BetKind.Dozen1; return true; }
        if (cmd.HasKey(CommandTargets.Dozen2Key)) { kind = RouletteGame.BetKind.Dozen2; return true; }
        if (cmd.HasKey(CommandTargets.Dozen3Key)) { kind = RouletteGame.BetKind.Dozen3; return true; }
        if (cmd.HasKey(CommandTargets.ZeroKey) || cmd.HasKey(CommandTargets.NumberBetKey)) return true;
        return false;
    }

    // amount and number from what was said: "25 on red" (25), "on red" (the stake), "10 on 17", "number 17" (the stake),
    // "zero" / "5 on zero"
    private bool PlaceRouletteBet(RouletteGame wheel, RouletteGame.BetKind kind, ParsedCommand cmd)
    {
        int amount = wheel.Stake, number = 0;
        if (kind == RouletteGame.BetKind.Straight)
        {
            if (cmd.HasKey(CommandTargets.ZeroKey)) { number = 0; if (rouletteNumbers.Count > 0) amount = rouletteNumbers[0]; }
            else if (rouletteNumbers.Count >= 2) { amount = rouletteNumbers[0]; number = rouletteNumbers[1]; }
            else if (rouletteNumbers.Count == 1) number = rouletteNumbers[0];
            else { voice.Say(PartnerLine.RouletteBets); return false; }
        }
        else if (rouletteNumbers.Count > 0) amount = rouletteNumbers[0];
        return SayBetResult(wheel.PlaceBet(kind, number, amount), true);
    }

    private bool SayBetResult(RouletteGame.BetResult r, bool sayOk)
    {
        switch (r)
        {
            case RouletteGame.BetResult.Ok: if (sayOk) voice.Say(PartnerLine.RouletteBetDown); return true;
            case RouletteGame.BetResult.Spinning: voice.Say(PartnerLine.Busy); return false;
            case RouletteGame.BetResult.NotEnoughChips: voice.Say(PartnerLine.BjNotEnough); return false;
            case RouletteGame.BetResult.NoSuchNumber: voice.Say(PartnerLine.NothingThere); return false;
            default: voice.Say(PartnerLine.BjBetLimits); return false;
        }
    }

    private void OnRouletteSpin(RouletteGame wheel) => voice.Say(PartnerLine.RouletteNoMoreBets);
    private void OnRouletteNoBets(RouletteGame wheel) => voice.Say(PartnerLine.RouletteNeedBet);

    private void OnRouletteLanded(RouletteGame wheel, int result, int won, int staked) =>
        voice.Say(won > staked ? PartnerLine.RouletteWin : won > 0 ? PartnerLine.BjPush : PartnerLine.RouletteLose);

    // ------------------------------------------------------------------ cashier / prize counter
    // "exchange my chips" / "exchange 500", "buy the bunny suit": at the counter now, or when she gets there
    private void Counter(ParsedCommand cmd)
    {
        PrizeCounter counter = targets.Counter;
        if (counter == null) { voice.Say(PartnerLine.NoCounter); return; }
        Action deal;
        OutfitDefinition buying = null;
        if (cmd.Verb == CommandVerb.Exchange)
        {
            int amount = cmd.HasKey(CommandTargets.AllInKey) ? 0 : cmd.Number;
            deal = () => voice.Say(counter.Exchange(amount, out _) == PrizeCounter.Result.Ok ? PartnerLine.CounterExchanged : PartnerLine.CounterNoChips);
        }
        else
        {
            string key = cmd.Keys.Find(k => k.StartsWith(OutfitKeyPrefix, StringComparison.Ordinal));
            if (key == null || outfit == null || !outfit.TryFind(key, out OutfitDefinition prize) || prize == null)
            {
                voice.Say(PartnerLine.NotUnderstood);
                return;
            }
            buying = prize;
            deal = () =>
            {
                switch (counter.Buy(prize))
                {
                    case PrizeCounter.Result.Ok: voice.Say(PartnerLine.PrizeBought); break;
                    case PrizeCounter.Result.TooExpensive: voice.Say(PartnerLine.PrizeTooExpensive); break;
                    case PrizeCounter.Result.AlreadyOwned: voice.Say(PartnerLine.PrizeOwned); break;
                    default: voice.Say(PartnerLine.DontHaveThat); break;
                }
            };
        }
        // exchanging: at the cashier; buying: at the prize's locker
        Vector3 place = cmd.Verb == CommandVerb.Buy && buying != null ? counter.PlaceFor(buying).position : counter.Spot.position;
        if (counter.IsNear(place, transform.position)) { deal(); return; }
        if (IsBusy || evasion.IsCrouched) { voice.Say(PartnerLine.Busy); return; }
        if (!targets.InReach(transform.position, place)) { voice.Say(PartnerLine.TooFar); return; }
        if (!NavMesh.SamplePosition(place, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || !interactor.MoveTo(hit.position))
        {
            voice.Say(PartnerLine.CantGetThere);
            return;
        }
        counter.WhenThere(place, deal);
        lastTarget = counter;
        voice.Say(PartnerLine.OnMyWay);
    }

    private void Blackjack(BlackjackGame game, ParsedCommand cmd)
    {
        int bet = BetAmount(game, cmd);
        switch (cmd.Verb)
        {
            case CommandVerb.Status: SayStatus(); break;
            case CommandVerb.Bet:
                if (bet > 0) game.Bet(bet);
                else if (cmd.HasKey(CommandTargets.SameBetKey)) game.Continue();
                else voice.Say(PartnerLine.BjPlaceBet);
                break;
            case CommandVerb.Play: game.Continue(); break;
            case CommandVerb.Deal:
            case CommandVerb.Yes:
            case CommandVerb.Again:
            case CommandVerb.Draw:    // "ready"
                if (bet > 0 && game.IsBetweenRounds) game.Bet(bet);
                else game.Continue();
                break;
            case CommandVerb.Hit:
            case CommandVerb.Attack:  // "hit" is also an attack word
                game.Decide(BlackjackGame.Decision.Hit); break;
            case CommandVerb.Stand:
            case CommandVerb.Stop: game.Decide(BlackjackGame.Decision.Stand); break;
            case CommandVerb.Double: game.Decide(BlackjackGame.Decision.Double); break;
            case CommandVerb.Split: game.Decide(BlackjackGame.Decision.Split); break;
            case CommandVerb.Surrender: game.Decide(BlackjackGame.Decision.Surrender); break;
            case CommandVerb.Insurance: game.Insurance(true); break;
            case CommandVerb.No: game.Decline(); break;
            case CommandVerb.Quit: game.Quit(); break;
            case CommandVerb.None:
                if (bet > 0 && game.IsBetweenRounds) game.Bet(bet);
                else voice.Say(PartnerLine.NotUnderstood);
                break;
            default:
                // anything else: between rounds she leaves the table (say it again once she is up), mid-hand she finishes first
                if (game.IsBetweenRounds) game.Quit();
                else voice.Say(PartnerLine.BjFinishHand);
                break;
        }
    }

    // chips named in the command ("bet 50", "all in", "minimum", "same bet"); 0 = none
    private static int BetAmount(BlackjackGame game, ParsedCommand cmd)
    {
        if (cmd.HasKey(CommandTargets.AllInKey) || cmd.HasKey(CommandTargets.MaxBetKey)) return Mathf.Min(game.Chips, game.MaxBet);
        if (cmd.HasKey(CommandTargets.MinBetKey)) return game.MinBet;
        if (cmd.HasKey(CommandTargets.SameBetKey) && game.Engine != null && game.Engine.LastBet > 0) return game.Engine.LastBet;
        return cmd.Number;
    }

    // keeps the gun on a target (switching targets: "aim at 2")
    private void Aim(ParsedCommand cmd)
    {
        if (TryWeapon(cmd, out int weapon) && !combat.SelectWeapon(weapon)) { voice.Say(PartnerLine.Busy); return; }
        if (!TryCombatTarget(cmd, out Health target)) return;
        if (target == null) { voice.Say(PartnerLine.NoTarget); return; }
        if (!combat.SelectedIsRanged)
        {
            int ranged = FirstRangedWeapon();
            if (ranged < 0) { voice.Say(PartnerLine.NoGun); return; }
            if (!combat.SelectWeapon(ranged)) { voice.Say(PartnerLine.Busy); return; }
        }
        SetTarget(target);
        aimTarget = target;
        combat.AimAt(AimPoint(target));
        voice.Say(PartnerLine.Aiming);
    }

    // The target of a combat command: "enemy 2" (its number), a name / "the other one" / left / right, "it" = the last one;
    // a plain "shoot" stays on the target she is aiming at / fighting. False = that number isn't in the fight (answered).
    private bool TryCombatTarget(ParsedCommand cmd, out Health target)
    {
        target = null;
        if (cmd.Number > 0 && encounter != null)
        {
            target = encounter.Get(cmd.Number);
            if (target == null) { voice.Say(PartnerLine.NoSuchTarget); return false; }
            return true;
        }
        bool plain = cmd.Modifier == CommandModifier.None && !cmd.Pronoun &&
                     !cmd.Keys.Exists(k => !k.StartsWith(WeaponKeyPrefix, StringComparison.Ordinal));
        if (plain && Alive(aimTarget)) target = aimTarget;
        else if (plain && encounter != null && Alive(encounter.Focus)) target = encounter.Focus;
        if (target == null) target = targets.FindShootable(cmd, transform.position, false, lastTarget);
        if (target == null && cmd.Pronoun && lastTarget is Health last && Alive(last)) target = last;
        return true;
    }

    private void SetTarget(Health target)
    {
        lastTarget = target;
        if (encounter != null) encounter.SetFocus(target);
    }

    private Vector3 AimPoint(Health target) => target.transform.position + Vector3.up * aimHeight;

    private static bool Alive(Health h) => h != null && !h.IsDead && h.gameObject.activeInHierarchy;

    private void Reload()
    {
        RangedWeapon gun = combat.IsArmed ? shooter.Current : null;
        if (gun == null) { voice.Say(PartnerLine.NoGun); return; }
        if (shooter.CurrentRounds >= gun.MagazineSize) { voice.Say(PartnerLine.AlreadyLoaded); return; }
        if (shooter.CurrentReserve <= 0) { voice.Say(PartnerLine.OutOfAmmo); return; }
        voice.Say(combat.Reload() ? PartnerLine.Reloading : PartnerLine.Busy);
    }

    private void Save()
    {
        SaveStation station = targets.FindStation(transform.position);
        if (station == null) { voice.Say(targets.LastSearchOutOfReach ? PartnerLine.TooFar : PartnerLine.NoSavePoint); return; }
        if (!PathExists(station.GetWalkInPosition(transform.position))) { voice.Say(PartnerLine.CantGetThere); return; }
        lastTarget = station;
        voice.Say(saveUser.Use(station) ? PartnerLine.Saving : PartnerLine.Busy);
    }

    private void SayStatus()
    {
        float f = health.Max > 0 ? (float)health.Current / health.Max : 0f;
        voice.Say(f > statusThresholds.x ? PartnerLine.StatusFine : f > statusThresholds.y ? PartnerLine.StatusHurt : PartnerLine.StatusBadlyHurt);
    }

    // ------------------------------------------------------------------ melee approach
    private void Update()
    {
        if (fireAtWill) UpdateFireAtWill();
        // she walked off the wheel (sent away, a fight, a long wait): the roulette closes, the bets come back
        if (RouletteOn && keypadUser.Current != targets.Roulette && !targets.Roulette.IsSpinning) targets.Roulette.Close();

        // aim held on a target until it goes down (or she is put away / busy)
        if (aimTarget != null)
        {
            if (!Alive(aimTarget) || !combat.IsArmed || partnerAnimator.IsScripted)
            {
                aimTarget = null;
                combat.StopAiming();
            }
            else combat.AimAt(AimPoint(aimTarget));
        }

        // melee weapon, backing off / taking cover: facing the threat until she gets there
        if (coverFacing != null)
        {
            bool moving = agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.1f);
            if (!Alive(coverFacing) || !moving || !combat.IsArmed)
            {
                coverFacing = null;
                if (stance == null || !stance.IsActive) combat.StopFacing();
            }
            else combat.FaceToward(coverFacing.transform.position);
        }

        if (meleeTarget == null) return;
        if (meleeTarget.IsDead || Time.time > meleeUntil || partnerAnimator.IsScripted) { meleeTarget = null; return; }
        if (Flat(meleeTarget.transform.position - transform.position).magnitude > meleeRange) return;
        interactor.MoveTo(transform.position);
        combat.Attack(meleeTarget.transform.position + Vector3.up * aimHeight);
        meleeTarget = null;
    }

    // ------------------------------------------------------------------ answers to results
    private void OnPickupAttempted(ItemDefinition item, int amount, PickupResult result)
    {
        switch (result)
        {
            case PickupResult.Collected: voice.Say(PartnerLine.GotIt); break;
            case PickupResult.Partial: voice.Say(PartnerLine.TookSome); break;
            case PickupResult.InventoryFull: voice.Say(PartnerLine.BagFull); break;
            case PickupResult.OutOfReach: voice.Say(PartnerLine.CantReach); break;
        }
    }

    private void OnDoorFinished(Door door, DoorAction action, DoorResult result)
    {
        switch (result)
        {
            case DoorResult.Locked: voice.Say(PartnerLine.DoorLocked); break;
            case DoorResult.NeedsOperator: voice.Say(PartnerLine.DoorNeedsOperator); break;
            case DoorResult.Unlocked: voice.Say(PartnerLine.DoorUnlocked); break;
        }
    }

    private void OnOutOfAmmo(RangedWeapon weapon) => voice.Say(PartnerLine.OutOfAmmo);

    private void OnCombatStarted() => voice.Say(PartnerLine.CombatStart);

    private void OnCombatEnded() => fireAtWill = false;

    private void OnTargetDown(Health target)
    {
        if (target == lastTarget) voice.Say(PartnerLine.TargetDown);
    }

    // ------------------------------------------------------------------ helpers
    private bool TryDestination(Component target, out Vector3 destination)
    {
        Vector3 wanted;
        switch (target)
        {
            case Room room: wanted = room.Spot != null ? room.Spot.position : room.transform.position; break;
            case Door door:
                Transform t = door.transform;
                float side = t.InverseTransformPoint(transform.position).z > 0f ? 1f : -1f;
                wanted = t.position + t.forward * (side * doorStandOff);
                break;
            case Pickup pickup: wanted = pickup.InteractionPosition; break;
            case SaveStation station: wanted = station.GetWalkInPosition(transform.position); break;
            case Health h:
                Vector3 back = Flat(transform.position - h.transform.position);
                wanted = h.transform.position + (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward) * 1.5f;
                break;
            default: destination = transform.position; return false;
        }
        bool found = NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2.5f, NavMesh.AllAreas);
        destination = found ? hit.position : wanted;
        return found;
    }

    private bool PathExists(Vector3 destination)
    {
        if (!agent.isOnNavMesh) return false;
        return NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    // the door from her room to that one (else the one named after it)
    private Door DoorTo(Room here, Room room)
    {
        Door d = targets.DoorBetween(here, room);
        return d != null ? d : FindDoorNamed(room.DisplayName);
    }

    private Door FindDoorNamed(string displayName)
    {
        foreach (Door d in targets.Doors)
            if (d != null && d.DisplayName == displayName) return d;
        return null;
    }

    // a weapon of her loadout named in the command (lexicon key = weapon object name)
    private bool TryWeapon(ParsedCommand cmd, out int index)
    {
        for (int i = 0; i < weaponHolder.LoadoutCount; i++)
        {
            Weapon w = weaponHolder.GetWeapon(i);
            if (w != null && cmd.HasKey(w.name)) { index = i; return true; }
        }
        index = -1;
        return false;
    }

    // a weapon name she doesn't carry ("draw the axe" without an axe)
    private bool HasUnknownWeapon(ParsedCommand cmd)
    {
        foreach (string key in cmd.Keys)
            if (key.StartsWith(WeaponKeyPrefix, StringComparison.Ordinal)) return true;
        return false;
    }

    // lexicon keys of weapons = weapon prefab names; of items = item ids
    public const string WeaponKeyPrefix = "Weapon_";
    public const string ItemKeyPrefix = "item_";
    public const string OutfitKeyPrefix = "outfit_";

    private bool IsRanged(int index)
    {
        Weapon w = weaponHolder.GetWeapon(index);
        return w != null && w.TryGetComponent(out RangedWeapon _);
    }

    private int FirstRangedWeapon()
    {
        for (int i = 0; i < weaponHolder.LoadoutCount; i++)
        {
            Weapon w = weaponHolder.GetWeapon(i);
            if (w != null && w.TryGetComponent(out RangedWeapon _)) return i;
        }
        return -1;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
