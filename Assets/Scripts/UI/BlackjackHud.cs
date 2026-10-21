using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Small blackjack panel (right side) while a game is on. The table camera already shows the cards, so this is
// text only: chips and bet, the dealer's total (her hole card counted as "?"), each of the operator's hands
// (total, bet, result) and what can be said now. Rebuilt when the game changes.
public class BlackjackHud : MonoBehaviour
{
    public const string GamesField = nameof(games);
    public const string SettingsField = nameof(settings);
    public const string GroupField = nameof(group);
    public const string HeaderField = nameof(header);
    public const string BodyField = nameof(body);
    public const string PromptField = nameof(prompt);

    [SerializeField] private BlackjackGame[] games = new BlackjackGame[0];
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private Color active = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private float fadeTime = 0.25f;

    private readonly List<Card> cards = new List<Card>();
    private readonly List<Card> visible = new List<Card>();
    private readonly List<bool> faceUp = new List<bool>();
    private readonly StringBuilder sb = new StringBuilder();
    private BlackjackGame shown;
    private bool dirty;

    private void Awake()
    {
        if (group == null || header == null || body == null || prompt == null) { enabled = false; return; }
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        foreach (BlackjackGame g in games) if (g != null) g.Changed += MarkDirty;
        if (settings != null) settings.Changed += MarkDirty;
    }

    private void OnDisable()
    {
        foreach (BlackjackGame g in games) if (g != null) g.Changed -= MarkDirty;
        if (settings != null) settings.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        BlackjackGame game = null;
        foreach (BlackjackGame g in games)
            if (g != null && g.IsActive && g.CurrentState != BlackjackGame.State.Joining) { game = g; break; }
        group.alpha = Mathf.MoveTowards(group.alpha, game != null ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
        if (game == null) return;
        if (game != shown) { shown = game; dirty = true; }
        if (!dirty) return;
        dirty = false;
        Rebuild(game);
    }

    private bool Pt => settings != null && settings.Language == GameLanguage.Portuguese;

    private void Rebuild(BlackjackGame game)
    {
        BlackjackEngine e = game.Engine;
        bool pt = Pt;
        int bet = 0;
        if (e != null && e.Phase != RoundPhase.Betting) foreach (BlackjackHand h in e.Hands) bet += h.Bet;
        header.text = $"{(pt ? "FICHAS" : "CHIPS")} {game.Chips}" + (bet > 0 ? $"   <color=#C8C8C8>{(pt ? "APOSTA" : "BET")} {bet}</color>" : "");

        sb.Clear();
        // dealer
        game.GetShownDealer(cards, faceUp);
        visible.Clear();
        for (int i = 0; i < cards.Count; i++) if (faceUp[i]) visible.Add(cards[i]);
        if (visible.Count > 0)
            sb.Append("<color=#C8C8C8>HELENA</color>  ").Append(Total(visible)).Append(faceUp.Contains(false) ? " + ?" : "").Append('\n');

        // hands
        int count = e == null ? 0 : Mathf.Min(game.ShownHandCount, e.Hands.Count);
        for (int i = 0; i < count; i++)
        {
            game.GetShownHand(i, cards);
            BlackjackHand h = e.Hands[i];
            string label = count > 1 ? $"{(pt ? "MÃO" : "HAND")} {i + 1}" : (pt ? "VOCÊ" : "YOU");
            string result = e.Phase == RoundPhase.RoundOver && game.CurrentState != BlackjackGame.State.Playing ? Result(h.Outcome, pt) : "";
            bool isActive = count > 1 && i == game.ActiveHandIndex && game.CurrentState == BlackjackGame.State.Deciding;
            string col = isActive ? "#" + ColorUtility.ToHtmlStringRGB(active) : "#C8C8C8";
            sb.Append($"<color={col}>{(isActive ? "> " : "")}{label}</color>  {(cards.Count > 0 ? Total(cards) : "")}")
              .Append($"  <size=80%><color=#9A9A9A>{h.Bet}{(h.Doubled ? " x2" : "")}</color></size>  {result}\n");
        }
        body.text = sb.ToString();
        prompt.text = Prompt(game, pt);
    }

    private static string Total(List<Card> c)
    {
        int t = BlackjackHand.Value(c, out bool soft);
        if (c.Count == 2 && t == 21) return "BJ";
        return soft && t < 21 ? $"{t - 10}/{t}" : t.ToString();
    }

    private static string Result(HandOutcome o, bool pt)
    {
        switch (o)
        {
            case HandOutcome.Win: return pt ? "<color=#7CFF8A>GANHOU</color>" : "<color=#7CFF8A>WIN</color>";
            case HandOutcome.Blackjack: return "<color=#FFD24A>BLACKJACK</color>";
            case HandOutcome.Push: return pt ? "<color=#BBBBBB>EMPATE</color>" : "<color=#BBBBBB>PUSH</color>";
            case HandOutcome.Bust: return pt ? "<color=#FF6060>ESTOUROU</color>" : "<color=#FF6060>BUST</color>";
            case HandOutcome.Surrender: return pt ? "<color=#FF9F60>DESISTIU</color>" : "<color=#FF9F60>SURRENDER</color>";
            case HandOutcome.Lose: return pt ? "<color=#FF6060>PERDEU</color>" : "<color=#FF6060>LOSE</color>";
            default: return "";
        }
    }

    private string Prompt(BlackjackGame game, bool pt)
    {
        BlackjackEngine e = game.Engine;
        switch (game.CurrentState)
        {
            case BlackjackGame.State.Betting:
                return pt ? $"APOSTA ({game.MinBet}-{game.MaxBet})  ·  MESMA  ·  TUDO  ·  CHEGA"
                          : $"BET ({game.MinBet}-{game.MaxBet})  ·  SAME  ·  ALL IN  ·  I'M DONE";
            case BlackjackGame.State.AskAgain:
                return pt ? "MAIS UMA?  SIM  ·  APOSTA 50  ·  CHEGA" : "ANOTHER?  YES  ·  BET 50  ·  I'M DONE";
            case BlackjackGame.State.Insurance:
                return pt ? "SEGURO?  SIM  ·  NÃO" : "INSURANCE?  YES  ·  NO";
            case BlackjackGame.State.Deciding:
                sb.Clear();
                Add(e.CanHit, pt ? "CARTA" : "HIT");
                Add(e.CanStand, pt ? "FICO" : "STAND");
                Add(e.CanDouble, pt ? "DOBRA" : "DOUBLE");
                Add(e.CanSplit, pt ? "DIVIDE" : "SPLIT");
                Add(e.CanSurrender, pt ? "DESISTO" : "SURRENDER");
                return sb.ToString();
            default:
                return "";
        }
    }

    private void Add(bool can, string word)
    {
        if (!can) return;
        if (sb.Length > 0) sb.Append("  ·  ");
        sb.Append(word);
    }
}
