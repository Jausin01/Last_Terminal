using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Turns a command (typed or spoken: same entry point) into her actions, and makes her answer (PartnerVoice).
// Understood in the language chosen in the settings only. Results that come later (item taken, door locked,
// out of ammo...) are answered from the events of the systems that do the work.
// This component is the router: it parses the command, decides who takes it (a game in progress, the counter, a slot
// machine, an elevator, a keypad, the combat filter) and hands it to one of the handlers (plain C# objects, see
// CommandHandler): NavigationCommands, InteractionCommands, CombatCommands, CombatMoveCommands, KeypadCommands,
// ElevatorCommands, SlotCommands, CardTableCommands, RouletteCommands, PrizeCounterCommands, OutfitCommands.
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
    // lexicon keys of weapons = weapon prefab names; of items = item ids
    public const string WeaponKeyPrefix = "Weapon_";
    public const string ItemKeyPrefix = "item_";
    public const string OutfitKeyPrefix = "outfit_";

    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
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
    public const string ExaminerField = nameof(examiner);

    // The systems that carry out her actions (animation, items, combat, doors, seats, health), the targets, her voice, the settings and the two vocabularies.
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
    // Her evasion (dodge / duck / get up).
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
    [Tooltip("Optional: things she looks at and comments on (\"look at the bed\").")]
    [SerializeField] private PartnerExaminer examiner;

    [Header("Tuning")]
    [Tooltip("Shots aim this high above the target's feet (m).")]
    [SerializeField] private float aimHeight = 1.2f;
    [Tooltip("Melee: she walks up to the target until this close, then strikes (m).")]
    [SerializeField] private float meleeRange = 1.1f;
    // Melee: she gives up walking to the target after this long (s).
    [SerializeField] private float meleeApproachTimeout = 8f;
    [Tooltip("\"Go to the door\": she stops this far in front of it (m).")]
    [SerializeField] private float doorStandOff = 1.0f;
    [Tooltip("Status: above this fraction of health she is fine, above the second one hurt, below badly hurt.")]
    [SerializeField] private Vector2 statusThresholds = new Vector2(0.66f, 0.33f);
    [Header("Combat moves")]
    [Tooltip("\"Back off\": she moves this far away from the threat (m).")]
    [SerializeField] private float backOffDistance = 3f;
    [Tooltip("\"Advance\": how far she moves forward (m), and how close she gets to the threat at most (m).")]
    [SerializeField] private float advanceDistance = 3f;
    // "Advance": she stops this close to the threat at most (m).
    [SerializeField] private float advanceStopDistance = 2.5f;
    [Tooltip("Back off / take cover: spots this close to the walls are not used (m).")]
    [SerializeField] private float wallMargin = 0.7f;
    [Tooltip("Take cover: spots of the room tried on a grid this size (m).")]
    [SerializeField] private float coverGrid = 1f;
    [Tooltip("Fire at will: time between her shots (s).")]
    [SerializeField] private float fireAtWillInterval = 0.9f;

    // The tuning, read by the handlers.
    internal float AimHeight => aimHeight;
    internal float MeleeRange => meleeRange;
    internal float MeleeApproachTimeout => meleeApproachTimeout;
    internal float DoorStandOff => doorStandOff;
    internal Vector2 StatusThresholds => statusThresholds;
    internal float BackOffDistance => backOffDistance;
    internal float AdvanceDistance => advanceDistance;
    internal float AdvanceStopDistance => advanceStopDistance;
    internal float WallMargin => wallMargin;
    internal float CoverGrid => coverGrid;
    internal float FireAtWillInterval => fireAtWillInterval;

    // One parser per language (made on first use).
    private readonly Dictionary<GameLanguage, CommandParser> parsers = new Dictionary<GameLanguage, CommandParser>();

    // What the handlers share, and the handlers (made in Awake).
    private CommandContext ctx;
    private NavigationCommands navigation;
    private InteractionCommands interaction;
    private CombatCommands fight;
    private CombatMoveCommands combatMoves;
    private KeypadCommands keypads;
    private ElevatorCommands elevators;
    private SlotCommands slots;
    private CardTableCommands cardTables;
    private RouletteCommands roulette;
    private PrizeCounterCommands prizeCounter;
    private OutfitCommands outfits;

    // Raised with every command understood (the HUD / tutorial listen).
    public event Action<ParsedCommand> CommandUnderstood;

    // Makes the handlers and checks the references.
    private void Awake()
    {
        ctx = new CommandContext(this, Run, partnerAnimator, interactor, combat, shooter, weaponHolder, doorUser, saveUser, health,
                                 targets, voice, settings, encounter, evasion, outfit, cameraPose, stance, keypadUser, elevatorUser, examiner);
        ctx.NavigationCommands = navigation = new NavigationCommands(ctx);
        ctx.InteractionCommands = interaction = new InteractionCommands(ctx);
        ctx.CombatCommands = fight = new CombatCommands(ctx);
        ctx.CombatMoveCommands = combatMoves = new CombatMoveCommands(ctx);
        ctx.KeypadCommands = keypads = new KeypadCommands(ctx);
        ctx.ElevatorCommands = elevators = new ElevatorCommands(ctx);
        ctx.SlotCommands = slots = new SlotCommands(ctx);
        ctx.CardTableCommands = cardTables = new CardTableCommands(ctx);
        ctx.RouletteCommands = roulette = new RouletteCommands(ctx);
        ctx.PrizeCounterCommands = prizeCounter = new PrizeCounterCommands(ctx);
        ctx.OutfitCommands = outfits = new OutfitCommands(ctx);
        if (partnerAnimator == null || interactor == null || combat == null || shooter == null || weaponHolder == null ||
            doorUser == null || saveUser == null || health == null || targets == null || voice == null || settings == null ||
            english == null || portuguese == null || evasion == null)
        {
            Debug.LogError($"{name}: PartnerCommands is missing references (run Last Terminal/4).", this);
            enabled = false;
        }
    }

    // Listens to the results of the systems (item taken, door done, out of ammo, combat, keypad, elevator, casino games).
    private void OnEnable()
    {
        if (interactor != null) interactor.PickupAttempted += interaction.OnPickupAttempted;
        if (doorUser != null) doorUser.Finished += interaction.OnDoorFinished;
        if (shooter != null) shooter.OutOfAmmo += fight.OnOutOfAmmo;
        if (encounter != null) { encounter.CombatStarted += fight.OnCombatStarted; encounter.TargetDown += fight.OnTargetDown; encounter.CombatEnded += fight.OnCombatEnded; }
        if (stance != null) stance.Cornered += fight.OnCornered;
        if (keypadUser != null) keypadUser.KeyPressed += keypads.OnKeyPressed;
        if (elevatorUser != null) elevatorUser.Arrived += elevators.OnElevatorArrived;
        if (targets != null)
            foreach (SlotMachine m in targets.Slots)
                if (m != null) { m.SpinEnded += slots.OnSpinEnded; m.NotEnoughChips += slots.OnNoChips; }
        if (targets != null && targets.Roulette != null)
        {
            targets.Roulette.SpinStarted += roulette.OnRouletteSpin;
            targets.Roulette.Landed += roulette.OnRouletteLanded;
            targets.Roulette.NoBets += roulette.OnRouletteNoBets;
        }
    }

    // Stops listening.
    private void OnDisable()
    {
        if (interactor != null) interactor.PickupAttempted -= interaction.OnPickupAttempted;
        if (doorUser != null) doorUser.Finished -= interaction.OnDoorFinished;
        if (shooter != null) shooter.OutOfAmmo -= fight.OnOutOfAmmo;
        if (encounter != null) { encounter.CombatStarted -= fight.OnCombatStarted; encounter.TargetDown -= fight.OnTargetDown; encounter.CombatEnded -= fight.OnCombatEnded; }
        if (stance != null) stance.Cornered -= fight.OnCornered;
        if (keypadUser != null) keypadUser.KeyPressed -= keypads.OnKeyPressed;
        if (elevatorUser != null) elevatorUser.Arrived -= elevators.OnElevatorArrived;
        if (targets != null)
            foreach (SlotMachine m in targets.Slots)
                if (m != null) { m.SpinEnded -= slots.OnSpinEnded; m.NotEnoughChips -= slots.OnNoChips; }
        if (targets != null && targets.Roulette != null)
        {
            targets.Roulette.SpinStarted -= roulette.OnRouletteSpin;
            targets.Roulette.Landed -= roulette.OnRouletteLanded;
            targets.Roulette.NoBets -= roulette.OnRouletteNoBets;
        }
    }

    // The entry point: a typed or spoken sentence.
    public void Execute(string text)
    {
        if (!enabled || string.IsNullOrWhiteSpace(text) || health.IsDead) return;
        ParsedCommand cmd = ParserFor(settings.Language).Parse(text);
        Debug.Log($"[Command] \"{cmd.Text}\" -> {cmd.Verb} [{string.Join(", ", cmd.Keys)}]{(cmd.Pronoun ? " (it)" : "")}");
        CommandUnderstood?.Invoke(cmd);
        Run(cmd);
    }

    // "let's play" (or a click on the table): she goes to sit as the dealer
    public void PlayAt(BlackjackGame table) => cardTables.PlayAt(table);

    // "Let's play poker": she goes to sit as the dealer of a Hold'em table.
    public void PlayHoldem(HoldemGame table) => cardTables.PlayHoldem(table);

    // she is at a keypad (or on her way): the voice listens mostly for the code
    public bool AtKeypad => keypadUser != null && keypadUser.Current is Keypad;

    // Routes a command: questions first, then an active game / counter / slots / elevator / keypad, then the combat filter, then the verb itself.
    private void Run(ParsedCommand cmd)
    {
        fight.CancelMelee();
        bool talk = cmd.Verb == CommandVerb.Status || cmd.Verb == CommandVerb.TellTag;   // just a question: she keeps doing what she does
        if (cameraPose != null && !talk) cameraPose.Stop();   // any order ends the pose
        if (examiner != null && !talk) examiner.Stop();       // and looking at something
        // "what does your tag say": answered anywhere (a table, a fight...)
        if (cmd.Verb == CommandVerb.TellTag) { outfits.SayTag(); return; }

        // a game of blackjack is on: the table takes the command
        BlackjackGame game = targets.ActiveTable;
        if (game != null) { fight.DropAim(); cardTables.Blackjack(game, cmd); return; }
        HoldemGame poker = targets.ActiveHoldem;
        if (poker != null) { fight.DropAim(); cardTables.Holdem(poker, cmd); return; }
        if (roulette.RouletteOn) { fight.DropAim(); roulette.Roulette(cmd); return; }

        // the cashier / prize counter ("troca o pijama" is changing clothes, not the cashier)
        if (cmd.Verb == CommandVerb.Exchange && OutfitCommands.HasOutfitKey(cmd)) cmd.Verb = CommandVerb.ChangeClothes;
        if (cmd.Verb == CommandVerb.Exchange || cmd.Verb == CommandVerb.Buy) { prizeCounter.Counter(cmd); return; }

        // an outfit named ("put on the pajamas", "usa o pijama"): changing clothes
        if (OutfitCommands.HasOutfitKey(cmd) && (cmd.Verb == CommandVerb.None || cmd.Verb == CommandVerb.Use || cmd.Verb == CommandVerb.PickUp ||
                                                 cmd.Verb == CommandVerb.Draw || cmd.Verb == CommandVerb.Bet))
            cmd.Verb = CommandVerb.ChangeClothes;

        // a slot machine: she plays it ("let's play the slots", "bet 25", "spin"); anything else first takes her off it
        if (keypadUser != null && !ctx.InCombat && slots.HandleSlot(cmd)) return;

        // an elevator ("take elevator 2", "go to the casino", "the red elevator")
        if (elevatorUser != null && !ctx.InCombat && elevators.IsElevatorCommand(cmd)) { elevators.HandleElevator(cmd); return; }

        // a keypad: the code said ("type 1 7", or just "seven" / "confirm" while she is at it); anything else first
        // takes her hand off it
        if (keypadUser != null && !ctx.InCombat && keypads.HandleKeypad(cmd)) return;

        // in combat: only combat (a bare number = that enemy)
        if (ctx.InCombat)
        {
            if (cmd.Verb == CommandVerb.None && cmd.Number > 0) cmd.Verb = CommandVerb.Aim;
            if (!IsCombatCommand(cmd)) { voice.Say(PartnerLine.NotNow); return; }
        }

        // the aim stays on its target while she moves, shoots or reloads; anything else drops it
        if (!KeepsAim(cmd.Verb)) fight.DropAim();
        // just a thing to look at named ("the bed", "vai até a cama"): she looks at it
        if (examiner != null && (cmd.Verb == CommandVerb.None || cmd.Verb == CommandVerb.GoTo) && targets.NamesExaminable(cmd) &&
            targets.FindRoom(cmd) == null && !cmd.HasKey(CommandTargets.DoorKey))
            cmd.Verb = CommandVerb.Examine;
        if (cmd.Verb == CommandVerb.None)
        {
            // just a place / thing: go there
            if (cmd.Keys.Count == 0 && !cmd.Pronoun) { voice.Say(PartnerLine.NotUnderstood); return; }
            cmd.Verb = CommandVerb.GoTo;
        }
        if (cmd.Verb == CommandVerb.Status) { ctx.SayStatus(); return; }
        if (ctx.IsBusy) { voice.Say(PartnerLine.Busy); return; }
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
            case CommandVerb.GoTo: navigation.GoTo(cmd); break;
            case CommandVerb.Examine: navigation.Examine(cmd); break;
            case CommandVerb.Advance: combatMoves.Advance(); break;
            case CommandVerb.Stop: fight.Stop(); break;
            case CommandVerb.PickUp: interaction.PickUp(cmd); break;
            case CommandVerb.Open: interaction.UseDoor(cmd, DoorAction.Open); break;
            case CommandVerb.Close: interaction.UseDoor(cmd, DoorAction.Close); break;
            case CommandVerb.GoThrough: interaction.UseDoor(cmd, DoorAction.PassThrough); break;
            case CommandVerb.Use: interaction.Use(cmd); break;
            case CommandVerb.Heal: interaction.Heal(cmd); break;
            case CommandVerb.Draw: fight.Draw(cmd); break;
            case CommandVerb.Holster: fight.Holster(); break;
            case CommandVerb.Attack: fight.Attack(cmd, false); break;
            case CommandVerb.Shoot: fight.Attack(cmd, true); break;
            case CommandVerb.Aim: fight.Aim(cmd); break;
            case CommandVerb.Play: cardTables.JoinTable(cmd); break;
            case CommandVerb.Reload: fight.Reload(); break;
            case CommandVerb.Save: interaction.Save(); break;
            case CommandVerb.Dodge: combatMoves.Dodge(); break;
            case CommandVerb.Duck: combatMoves.Duck(); break;
            case CommandVerb.StandUp: combatMoves.StandUp(); break;
            case CommandVerb.BackOff: combatMoves.BackOff(); break;
            case CommandVerb.TakeCover: combatMoves.TakeCover(); break;
            case CommandVerb.Flee: combatMoves.Flee(); break;
            case CommandVerb.HoldFire: fight.HoldFire(); break;
            case CommandVerb.FireAtWill: fight.FireAtWill(); break;
            case CommandVerb.ChangeClothes: outfits.ChangeClothes(cmd); break;
            case CommandVerb.LookAtCamera: navigation.LookAtCamera(); break;
            case CommandVerb.CombatStance: fight.CombatStance(); break;
            case CommandVerb.StandDown: fight.StandDown(); break;
            case CommandVerb.TurnLeftSide: navigation.TurnToCamera(PartnerCameraPose.Facing.LeftSide); break;
            case CommandVerb.TurnRightSide: navigation.TurnToCamera(PartnerCameraPose.Facing.RightSide); break;
            case CommandVerb.TurnBack: navigation.TurnToCamera(PartnerCameraPose.Facing.Back); break;
            case CommandVerb.TurnFront: navigation.TurnToCamera(PartnerCameraPose.Facing.Front); break;
            case CommandVerb.TypeCode: voice.Say(PartnerLine.NoKeypad); break;   // (in combat: TypeCode never gets here)
            default:
                // table words with no game going on
                voice.Say(cmd.Verb == CommandVerb.Bet || cmd.Verb == CommandVerb.Deal ? PartnerLine.BjNoGame : PartnerLine.NotUnderstood);
                break;
        }
    }

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
            case CommandVerb.Advance:
                return true;
            case CommandVerb.Use:
            case CommandVerb.PickUp:
                // a weapon ("use the shotgun") or a medkit, not doors / items on the floor
                if (fight.TryWeapon(cmd, out _)) return true;
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
            case CommandVerb.CombatStance: case CommandVerb.Advance:
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
                return fight.TryWeapon(cmd, out int named) ? !fight.IsRanged(named) : !combat.SelectedIsRanged;
            case CommandVerb.Use:
                return !(fight.TryWeapon(cmd, out _) || cmd.Keys.Exists(k => interactor.Inventory != null && interactor.Inventory.FindById(k) is HealingItem));
            default:
                return true;
        }
    }

    // The parser of a language (made once).
    private CommandParser ParserFor(GameLanguage language)
    {
        if (!parsers.TryGetValue(language, out CommandParser parser))
        {
            parser = new CommandParser(language == GameLanguage.Portuguese ? portuguese : english);
            parsers.Add(language, parser);
        }
        return parser;
    }

    // Each frame: fire at will, the roulette closing when she walks off it, then the held aim / guard / melee approach.
    private void Update()
    {
        fight.TickFireAtWill();
        roulette.Tick();
        fight.Tick();
    }
}
