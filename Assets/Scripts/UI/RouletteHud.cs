using System.Text;
using TMPro;
using UnityEngine;

// The roulette panel (right side) while she is the croupier: chips, the stake (what a bet without an amount puts
// down), the bets on the layout, the last number and what was won, and what to say.
public class RouletteHud : MonoBehaviour
{
    public const string GameField = nameof(game);
    public const string SettingsField = nameof(settings);
    public const string GroupField = nameof(group);
    public const string HeaderField = nameof(header);
    public const string BodyField = nameof(body);
    public const string PromptField = nameof(prompt);

    [SerializeField] private RouletteGame game;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private float fadeTime = 0.25f;

    private readonly StringBuilder sb = new StringBuilder();
    private bool dirty = true;
    private int shownChips = -1;

    private void Awake()
    {
        if (game == null || group == null || header == null || body == null || prompt == null) { enabled = false; return; }
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        if (game != null) game.Changed += MarkDirty;
        if (settings != null) settings.Changed += MarkDirty;
    }

    private void OnDisable()
    {
        if (game != null) game.Changed -= MarkDirty;
        if (settings != null) settings.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, game.IsOpen ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
        if (!game.IsOpen) return;
        if (game.Bank.Chips != shownChips) dirty = true;
        if (!dirty) return;
        dirty = false;
        shownChips = game.Bank.Chips;
        Rebuild();
    }

    private void Rebuild()
    {
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        header.text = $"{(pt ? "ROLETA" : "ROULETTE")}   {(pt ? "FICHAS" : "CHIPS")} {game.Bank.Chips}   <color=#C8C8C8>{(pt ? "VALOR" : "STAKE")} {game.Stake}</color>";

        sb.Clear();
        if (game.LastResult >= 0)
        {
            int n = game.LastResult;
            string color = n == 0 ? "#3CC864" : RouletteGame.IsRed(n) ? "#FF5050" : "#E0E0E0";
            sb.Append($"<color={color}><b>{n}</b></color>");
            if (!game.IsSpinning)
                sb.Append(game.LastWin > 0 ? $"   <color=#7CFF8A>{(pt ? "GANHOU" : "WIN")} {game.LastWin}</color>"
                                           : game.LastStake > 0 ? $"   <color=#FF6060>{(pt ? "PERDEU" : "LOST")} {game.LastStake}</color>" : "");
            sb.Append('\n');
        }
        if (game.IsSpinning) sb.Append(pt ? "<color=#FFD24A>GIRANDO...</color>\n" : "<color=#FFD24A>SPINNING...</color>\n");
        int shown = 0;
        foreach (RouletteGame.Bet b in game.Bets)
        {
            if (shown++ == 5) { sb.Append("...\n"); break; }
            sb.Append($"{Name(b, pt)}  <color=#9A9A9A>{b.Amount}</color>\n");
        }
        body.text = sb.ToString();
        prompt.text = game.IsSpinning ? "" : pt
            ? "\"25 NO VERMELHO\" · \"NÚMERO 17\" · \"GIRA\" · \"CHEGA\""
            : "\"25 ON RED\" · \"NUMBER 17\" · \"SPIN\" · \"I'M DONE\"";
    }

    private static string Name(RouletteGame.Bet b, bool pt)
    {
        switch (b.Kind)
        {
            case RouletteGame.BetKind.Straight: return (pt ? "NÚMERO " : "NUMBER ") + b.Number;
            case RouletteGame.BetKind.Red: return pt ? "<color=#FF5050>VERMELHO</color>" : "<color=#FF5050>RED</color>";
            case RouletteGame.BetKind.Black: return pt ? "PRETO" : "BLACK";
            case RouletteGame.BetKind.Even: return pt ? "PAR" : "EVEN";
            case RouletteGame.BetKind.Odd: return pt ? "ÍMPAR" : "ODD";
            case RouletteGame.BetKind.Low: return "1-18";
            case RouletteGame.BetKind.High: return "19-36";
            case RouletteGame.BetKind.Dozen1: return pt ? "1ª DÚZIA" : "1ST 12";
            case RouletteGame.BetKind.Dozen2: return pt ? "2ª DÚZIA" : "2ND 12";
            default: return pt ? "3ª DÚZIA" : "3RD 12";
        }
    }
}
