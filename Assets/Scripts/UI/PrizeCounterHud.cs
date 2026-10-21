using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// The cashier / prize counter panel (right side) while she is at the counter: chips, coins, the exchange rate,
// every prize with its price (bought ones marked) and what to say.
public class PrizeCounterHud : MonoBehaviour
{
    public const string CounterField = nameof(counter);
    public const string PartnerField = nameof(partner);
    public const string SettingsField = nameof(settings);
    public const string GroupField = nameof(group);
    public const string HeaderField = nameof(header);
    public const string BodyField = nameof(body);
    public const string PromptField = nameof(prompt);

    [SerializeField] private PrizeCounter counter;
    [SerializeField] private Transform partner;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private float fadeTime = 0.25f;

    private readonly List<OutfitDefinition> prizes = new List<OutfitDefinition>();
    private readonly StringBuilder sb = new StringBuilder();
    private bool dirty = true;
    private int shownChips = -1, shownCoins = -1;
    private bool shownCashier;

    private void Awake()
    {
        if (counter == null || partner == null || group == null || header == null || body == null || prompt == null) { enabled = false; return; }
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        if (counter != null) counter.Changed += MarkDirty;
        if (settings != null) settings.Changed += MarkDirty;
    }

    private void OnDisable()
    {
        if (counter != null) counter.Changed -= MarkDirty;
        if (settings != null) settings.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        bool here = counter.IsAtAnyPlace(partner.position);
        group.alpha = Mathf.MoveTowards(group.alpha, here ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
        if (!here) return;
        bool atCashier = counter.IsHere(partner.position);
        if (counter.Bank.Chips != shownChips || counter.Bank.Coins != shownCoins || atCashier != shownCashier) dirty = true;
        shownCashier = atCashier;
        if (!dirty) return;
        dirty = false;
        shownChips = counter.Bank.Chips;
        shownCoins = counter.Bank.Coins;

        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        GameLanguage lang = pt ? GameLanguage.Portuguese : GameLanguage.English;
        bool cashier = counter.IsHere(partner.position);
        string title = cashier ? (pt ? "CAIXA" : "CASHIER") : (pt ? "PRÊMIOS" : "PRIZES");
        header.text = pt ? $"{title}   FICHAS {shownChips}   <color=#FFD24A>MOEDAS {shownCoins}</color>"
                         : $"{title}   CHIPS {shownChips}   <color=#FFD24A>COINS {shownCoins}</color>";
        sb.Clear();
        sb.Append(pt ? $"<color=#C8C8C8>{counter.ChipsPerCoin} FICHA(S) = 1 MOEDA</color>\n" : $"<color=#C8C8C8>{counter.ChipsPerCoin} CHIP(S) = 1 COIN</color>\n");
        counter.GetPrizes(prizes);
        foreach (OutfitDefinition o in prizes)
        {
            string name = o.DisplayName(lang).ToUpperInvariant();
            if (counter.IsOwned(o)) sb.Append($"<color=#7CFF8A>{name}  {(pt ? "COMPRADO" : "OWNED")}</color>\n");
            else sb.Append($"{name}  <color={(shownCoins >= o.Price ? "#FFD24A" : "#9A9A9A")}>{o.Price}</color>\n");
        }
        body.text = sb.ToString();
        prompt.text = pt ? "\"TROCA AS FICHAS\" · \"TROCA 500\" · \"COMPRA O PIJAMA\"" : "\"EXCHANGE MY CHIPS\" · \"EXCHANGE 500\" · \"BUY THE PAJAMAS\"";
    }
}
