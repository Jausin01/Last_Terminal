using System;
using System.Collections.Generic;
using UnityEngine;

// What she understands, one asset per language: verb phrases and names of things (rooms, doors, items, weapons...).
// Phrases are matched as whole words, without accents or punctuation ("vá para o depósito" = "va para o deposito").
// A word ending in * matches any word starting with it ("abr*" = abre, abra, abrir).
// Name keys: Room / Door display name, item id, weapon object name, or one of the CommandTargets keys.
public enum CommandVerb
{
    None = 0,
    GoTo = 1,
    Stop = 2,
    PickUp = 3,
    Open = 4,
    Close = 5,
    GoThrough = 6,
    Use = 7,
    Heal = 8,
    Draw = 9,
    Holster = 10,
    Attack = 11,
    Shoot = 12,
    Reload = 13,
    Save = 14,
    Status = 15,
    Aim = 16,
    // blackjack (understood at the table; outside a game, Hit = Attack)
    Play = 17,
    Bet = 18,
    Hit = 19,
    Stand = 20,
    Double = 21,
    Split = 22,
    Surrender = 23,
    Insurance = 24,
    Yes = 25,
    No = 26,
    Again = 27,
    Quit = 28,
    Deal = 29,
    // combat moves
    Dodge = 30,
    Duck = 31,
    StandUp = 32,
    BackOff = 33,
    TakeCover = 34,
    Flee = 35,
    HoldFire = 36,
    FireAtWill = 37,
    // outfits
    ChangeClothes = 38,
    // "look at the camera"
    LookAtCamera = 39,
    // "combat position" (she keeps her distance / stays on guard on her own) and "relax"
    CombatStance = 40,
    StandDown = 41,
    // keypads ("type 1 7", "press confirm") and showing herself to the camera ("turn around", "show your left side")
    TypeCode = 42,
    TurnLeftSide = 43,
    TurnRightSide = 44,
    TurnBack = 45,
    TurnFront = 46,
    // casino
    Spin = 47,
    Call = 48,        // Casino Hold'em: pay the call (fold = Surrender)
    Exchange = 49,    // chips -> prize coins at the cashier
    Buy = 50,         // a prize at the counter
    ClearBets = 51,   // roulette: take the bets back
    Examine = 52,     // look at a thing and say something about it
    Advance = 53,     // move forward (toward the threat in a fight)
    TellTag = 54,     // "tell me your tag numbers": she reads what is written on the tag of her outfit
}

// Which one, when several things match: "the other one", "the one on the left"...
public enum CommandModifier
{
    None = 0,
    Other = 1,
    Left = 2,
    Right = 3,
    Nearest = 4,
    Farthest = 5,
}

// The vocabulary of one language (an asset): phrases for every verb, name, modifier and number, plus pronouns.
[CreateAssetMenu(menuName = "Last Terminal/Commands/Command Lexicon", fileName = "CommandLexicon")]
public class CommandLexicon : ScriptableObject
{
    // The phrases that mean one verb.
    [Serializable]
    public class VerbPhrases
    {
        // The verb and the phrases that say it.
        [SerializeField] private CommandVerb verb;
        [SerializeField] private string[] phrases = new string[0];

        public VerbPhrases() { }
        public VerbPhrases(CommandVerb verb, string[] phrases) { this.verb = verb; this.phrases = phrases; }
        // Read by the parser.
        public CommandVerb Verb => verb;
        public IReadOnlyList<string> Phrases => phrases;
    }

    // The phrases that name one thing (a room, a door, an item...: the key).
    [Serializable]
    public class NamePhrases
    {
        // The thing's key and the phrases that name it.
        [SerializeField] private string key;
        [SerializeField] private string[] phrases = new string[0];

        public NamePhrases() { }
        public NamePhrases(string key, string[] phrases) { this.key = key; this.phrases = phrases; }
        // Read by the parser.
        public string Key => key;
        public IReadOnlyList<string> Phrases => phrases;
    }

    // The phrases of one modifier ("the other one", "the left one").
    [Serializable]
    public class ModifierPhrases
    {
        // The modifier and its phrases.
        [SerializeField] private CommandModifier modifier;
        [SerializeField] private string[] phrases = new string[0];

        public ModifierPhrases() { }
        public ModifierPhrases(CommandModifier modifier, string[] phrases) { this.modifier = modifier; this.phrases = phrases; }
        // Read by the parser.
        public CommandModifier Modifier => modifier;
        public IReadOnlyList<string> Phrases => phrases;
    }

    // The spoken forms of one number ("two", "dois").
    [Serializable]
    public class NumberPhrases
    {
        // The number and its spoken forms.
        [SerializeField] private int value;
        [SerializeField] private string[] phrases = new string[0];

        public NumberPhrases() { }
        public NumberPhrases(int value, string[] phrases) { this.value = value; this.phrases = phrases; }
        // Read by the parser.
        public int Value => value;
        public IReadOnlyList<string> Phrases => phrases;
    }

    // The language of this asset and its phrase lists (edited in the Inspector; menu 3 adds the missing defaults).
    [SerializeField] private GameLanguage language;
    [SerializeField] private List<VerbPhrases> verbs = new List<VerbPhrases>();
    [SerializeField] private List<NamePhrases> names = new List<NamePhrases>();
    [SerializeField] private List<ModifierPhrases> modifiers = new List<ModifierPhrases>();
    [Tooltip("Spoken numbers (\"two\", \"dois\"); digits are always understood. Combat targets are numbered.")]
    [SerializeField] private List<NumberPhrases> numbers = new List<NumberPhrases>();
    [Tooltip("Words that mean the last thing she was told about (\"it\", \"isso\").")]
    [SerializeField] private string[] pronouns = new string[0];

    // Read by the parser and the voice recognizer (its word list).
    public GameLanguage Language => language;
    public IReadOnlyList<VerbPhrases> Verbs => verbs;
    public IReadOnlyList<NamePhrases> Names => names;
    public IReadOnlyList<ModifierPhrases> Modifiers => modifiers;
    public IReadOnlyList<NumberPhrases> Numbers => numbers;
    public IReadOnlyList<string> Pronouns => pronouns;

#if UNITY_EDITOR
    // Editor setup (Last Terminal/3): sets the language and adds the verbs / names that are missing, keeping edited ones.
    public void EditorMerge(GameLanguage lang, IEnumerable<VerbPhrases> defaultVerbs, IEnumerable<NamePhrases> defaultNames,
                            IEnumerable<ModifierPhrases> defaultModifiers, IEnumerable<NumberPhrases> defaultNumbers,
                            string[] defaultPronouns)
    {
        language = lang;
        // a verb can have more than one entry (phrases added later, e.g. elevator rides for GoTo): matched by verb + first phrase
        foreach (VerbPhrases v in defaultVerbs)
            if (!verbs.Exists(x => x.Verb == v.Verb && FirstPhrase(x) == FirstPhrase(v))) verbs.Add(v);
        foreach (NamePhrases n in defaultNames)
            if (!names.Exists(x => x.Key == n.Key)) names.Add(n);
        foreach (ModifierPhrases m in defaultModifiers)
            if (!modifiers.Exists(x => x.Modifier == m.Modifier)) modifiers.Add(m);
        foreach (NumberPhrases n in defaultNumbers)
            if (!numbers.Exists(x => x.Value == n.Value)) numbers.Add(n);
        if (pronouns == null || pronouns.Length == 0) pronouns = defaultPronouns;
    }

    // phrases that moved (e.g. "blackjack" stopped being a Play phrase and became a name: the game)
    public void EditorRemoveVerbPhrases(CommandVerb verb, IEnumerable<string> remove)
    {
        var drop = new HashSet<string>(remove);
        for (int i = 0; i < verbs.Count; i++)
        {
            VerbPhrases v = verbs[i];
            if (v.Verb != verb) continue;
            var keep = new List<string>();
            foreach (string p in v.Phrases) if (!drop.Contains(p)) keep.Add(p);
            if (keep.Count != v.Phrases.Count) verbs[i] = new VerbPhrases(verb, keep.ToArray());
        }
    }

    // Removes names that are no longer used (menu 3 cleanup).
    public void EditorRemoveNames(IEnumerable<string> keys)
    {
        foreach (string k in keys) names.RemoveAll(x => x.Key == k);
    }

    // First phrase of a verb entry (its identity when merging defaults).
    private static string FirstPhrase(VerbPhrases v) => v.Phrases.Count > 0 ? v.Phrases[0] : "";
#endif
}
