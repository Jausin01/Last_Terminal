using System;
using System.Collections.Generic;
using UnityEngine;

// Everything Helena says: one entry per line, the text in each language (subtitle) and the recorded takes (clips).
// Several takes of the same line = variations, picked at random. A line without clips is shown as a subtitle only.
// Clips are assigned by Last Terminal/5 from Assets/Audio/Voice/Helena/EN and /PT (file name = line id, e.g. DoorLocked_01.wav).
public enum PartnerLine
{
    Acknowledge = 0,
    OnMyWay = 1,
    NotUnderstood = 2,
    Busy = 3,
    CantGetThere = 4,
    NothingThere = 5,
    GotIt = 6,
    TookSome = 7,
    BagFull = 8,
    CantReach = 9,
    DoorLocked = 10,
    DoorNeedsOperator = 11,
    DoorUnlocked = 12,
    DoorAlreadyOpen = 13,
    DoorAlreadyClosed = 14,
    Healed = 15,
    NotHurt = 16,
    NoHealing = 17,
    WeaponReady = 18,
    WeaponAway = 19,
    DontHaveThat = 20,
    NoTarget = 21,
    OutOfAmmo = 22,
    Reloading = 23,
    AlreadyLoaded = 24,
    NoGun = 25,
    Saving = 26,
    NoSavePoint = 27,
    StatusFine = 28,
    StatusHurt = 29,
    StatusBadlyHurt = 30,
    Stopping = 31,
    CombatStart = 32,
    TargetDown = 33,
    Aiming = 34,
    NoSuchTarget = 35,
    // blackjack (she deals)
    BjAccept = 36,
    BjNoTable = 37,
    BjNoGame = 38,
    BjPlaceBet = 39,
    BjBetLimits = 40,
    BjNotEnough = 41,
    BjYourTurn = 42,
    BjInsurance = 43,
    BjPeek = 44,
    BjDealerBlackjack = 45,
    BjPlayerBlackjack = 46,
    BjPlayerBust = 47,
    BjDealerBust = 48,
    BjPlayerWins = 49,
    BjHouseWins = 50,
    BjPush = 51,
    BjCountingCards = 52,
    BjAnotherRound = 53,
    BjFinishHand = 54,
    BjGameOver = 55,
    BjBroke = 56,
    BjIdle = 57,
    BjCantDouble = 58,
    BjCantSplit = 59,
    BjCantSurrender = 60,
    BjShuffle = 61,
    // combat
    NotNow = 62,
    Dodging = 63,
    Ducking = 64,
    GettingUp = 65,
    BackingOff = 66,
    Retreating = 67,
    TakingCover = 68,
    HoldingFire = 69,
    FireAtWill = 70,
    NoThreat = 71,
    NoWayOut = 72,
    // outfits
    OutfitChanged = 73,
    OutfitSame = 74,
    OutfitBunny = 75,
    // moving room by room, the camera
    TooFar = 76,
    LookWhat = 77,
    LookWaiting = 78,
    NoCamera = 79,
    // getting hurt
    Hurt = 80,
    HurtBad = 81,
    Dying = 82,
    StanceRanged = 83,
    StanceMelee = 84,
    StandingDown = 85,
    Cornered = 86,
    KeypadOpen = 87,
    KeypadWrong = 88,
    NoKeypad = 89,
    KeypadWhich = 90,
    TurnSide = 91,
    TurnBack = 92,
    ElevatorGoing = 93,
    ElevatorNeedKey = 94,
    ElevatorOutOfService = 95,
    NoElevator = 96,
    SlotReady = 97,
    SlotWin = 98,
    SlotJackpot = 99,
    SlotNoChips = 100,
    ElevatorArrived = 101,
    RouletteBets = 102,
    RouletteNoMoreBets = 103,
    RouletteWin = 104,
    RouletteLose = 105,
    RouletteNeedBet = 106,
    RouletteBetDown = 107,
    PokerCallOrFold = 108,
    PokerNotQualified = 109,
    PokerFolded = 110,
    CounterExchanged = 111,
    CounterNoChips = 112,
    PrizeBought = 113,
    PrizeTooExpensive = 114,
    PrizeOwned = 115,
    OutfitLocked = 116,
    NoCounter = 117,
    OperatorAway = 118,
    OperatorBack = 119,
    // what her tag says (asked by the operator)
    TagPatient = 120,
    TagPrison = 121,
    NoTag = 122,
}

// The asset holding every line.
[CreateAssetMenu(menuName = "Last Terminal/Voice/Voice Line Bank", fileName = "VoiceLineBank")]
public class VoiceLineBank : ScriptableObject
{
    // One line: id, text per language and the recorded takes.
    [Serializable]
    public class Entry
    {
        // Which line this is.
        [SerializeField] private PartnerLine line;
        [Tooltip("When she says it (for the voice actress).")]
        [SerializeField] private string context;
        // Subtitle text per language and the takes per language.
        [SerializeField, TextArea] private string english;
        [SerializeField, TextArea] private string portuguese;
        [SerializeField] private AudioClip[] englishClips = new AudioClip[0];
        [SerializeField] private AudioClip[] portugueseClips = new AudioClip[0];

        public Entry() { }

        public Entry(PartnerLine line, string context, string english, string portuguese)
        {
            this.line = line;
            this.context = context;
            this.english = english;
            this.portuguese = portuguese;
        }

        // Read-only access.
        public PartnerLine Line => line;
        public string Context => context;
        public string Text(GameLanguage language) => language == GameLanguage.Portuguese ? portuguese : english;
        public AudioClip[] Clips(GameLanguage language) => language == GameLanguage.Portuguese ? portugueseClips : englishClips;

#if UNITY_EDITOR
        // A rewritten default text (the takes are kept).
        public void EditorSetText(string newContext, string newEnglish, string newPortuguese)
        {
            context = newContext; english = newEnglish; portuguese = newPortuguese;
        }

        // Assigns the takes (the editor menu that imports the audio).
        public void EditorSetClips(GameLanguage language, AudioClip[] clips)
        {
            if (language == GameLanguage.Portuguese) portugueseClips = clips;
            else englishClips = clips;
        }
#endif
    }

    // All the lines.
    [SerializeField] private List<Entry> entries = new List<Entry>();

    // Lookup by line id (built on first use).
    private Dictionary<PartnerLine, Entry> lookup;

    // Every entry (editor tools).
    public IReadOnlyList<Entry> Entries => entries;

    // Finds a line's entry.
    public bool TryGet(PartnerLine line, out Entry entry)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<PartnerLine, Entry>();
            foreach (Entry e in entries)
                if (e != null && !lookup.ContainsKey(e.Line)) lookup.Add(e.Line, e);
        }
        return lookup.TryGetValue(line, out entry);
    }

    // The list changed in the inspector: rebuild the lookup.
    private void OnValidate() => lookup = null;

#if UNITY_EDITOR
    // Editor setup (Last Terminal/3): adds the lines that are missing, keeping edited texts and assigned clips.
    public void EditorMerge(IEnumerable<Entry> defaults)
    {
        foreach (Entry d in defaults)
            if (!entries.Exists(e => e.Line == d.Line)) entries.Add(d);
        entries.Sort((a, b) => a.Line.CompareTo(b.Line));
        lookup = null;
    }

    // Editor setup: a default line rewritten (the story changed): replaced only while its English text is still the old
    // default (a text edited by hand is kept).
    public void EditorRewrite(Entry newer, string oldEnglish)
    {
        Entry e = entries.Find(x => x != null && x.Line == newer.Line);
        if (e == null || e.Text(GameLanguage.English) != oldEnglish) return;
        e.EditorSetText(newer.Context, newer.Text(GameLanguage.English), newer.Text(GameLanguage.Portuguese));
        lookup = null;
    }
#endif
}
