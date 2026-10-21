using TMPro;
using UnityEngine;

// The slot machine panel (right side, like the blackjack one): shows while she is at a machine. Its number, the bet,
// the chips and prize coins, the last result (the middle row) and what to say.
public class CasinoHud : MonoBehaviour
{
    public const string KeypadUserField = nameof(keypadUser);
    public const string BankField = nameof(bank);
    public const string SettingsField = nameof(settings);
    public const string GroupField = nameof(group);
    public const string HeaderField = nameof(header);
    public const string BodyField = nameof(body);
    public const string PromptField = nameof(prompt);

    [SerializeField] private PartnerKeypadUser keypadUser;
    [SerializeField] private ChipBank bank;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private float fadeSpeed = 4f;

    private static readonly string[] SymbolsEn = { "CHERRY", "GRAPES", "COIN", "SEVEN", "CLOWN", "BELL" };
    private static readonly string[] SymbolsPt = { "CEREJA", "UVA", "MOEDA", "SETE", "PALHAÇO", "SINO" };

    private int shownState;

    private void Awake()
    {
        if (keypadUser == null || bank == null || group == null || header == null || body == null)
        {
            Debug.LogError($"{name}: CasinoHud is missing references (run the casino menu).", this);
            enabled = false;
            return;
        }
        group.alpha = 0f;
    }

    private void Update()
    {
        var machine = keypadUser.Current as SlotMachine;
        group.alpha = Mathf.MoveTowards(group.alpha, machine != null ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        if (machine == null) return;

        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        int state = machine.Number;
        state = state * 31 + machine.Bet;
        state = state * 31 + bank.Chips;
        state = state * 31 + bank.Coins;
        state = state * 31 + machine.LastWin;
        state = state * 31 + (machine.IsSpinning ? 1 : 0) + (pt ? 2 : 0);
        if (!machine.IsSpinning) state = state * 31 + (int)machine.Middle(0) * 36 + (int)machine.Middle(1) * 6 + (int)machine.Middle(2);
        if (state == shownState) return;   // rebuilt only when something changed
        shownState = state;

        string[] names = pt ? SymbolsPt : SymbolsEn;
        header.text = pt ? $"CAÇA-NÍQUEL {machine.Number}" : $"SLOT MACHINE {machine.Number}";
        string line = machine.IsSpinning
            ? (pt ? "girando..." : "spinning...")
            : $"{names[(int)machine.Middle(0)]} | {names[(int)machine.Middle(1)]} | {names[(int)machine.Middle(2)]}";
        string win = machine.IsSpinning || machine.LastWin <= 0 ? "" : (pt ? $"\nGANHOU {machine.LastWin}" : $"\nWIN {machine.LastWin}");
        body.text = pt
            ? $"APOSTA {machine.Bet}  ({machine.MinBet}-{machine.MaxBet})\nFICHAS {bank.Chips}   MOEDAS {bank.Coins}\n{line}{win}"
            : $"BET {machine.Bet}  ({machine.MinBet}-{machine.MaxBet})\nCHIPS {bank.Chips}   COINS {bank.Coins}\n{line}{win}";
        if (prompt != null)
            prompt.text = pt ? "\"gira\"  ·  \"aposta 25\"  ·  \"chega\"" : "\"spin\"  ·  \"bet 25\"  ·  \"I'm done\"";
    }
}
