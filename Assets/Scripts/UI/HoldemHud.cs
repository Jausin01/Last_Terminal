using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// The Casino Hold'em panel (right side) while a game is on: chips, ante and call, the operator's cards, the board,
// her cards once shown, what each hand is, the result and what to say. Rebuilt when the game changes.
public class HoldemHud : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GamesField = nameof(games);
    public const string SettingsField = nameof(settings);
    public const string GroupField = nameof(group);
    public const string HeaderField = nameof(header);
    public const string BodyField = nameof(body);
    public const string PromptField = nameof(prompt);

    // The hold'em tables, the settings (language), the panel and its texts.
    [SerializeField] private HoldemGame[] games = new HoldemGame[0];
    [SerializeField] private CameraSettings settings;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text prompt;
    // Fade time (s).
    [SerializeField] private float fadeTime = 0.25f;

    // Hand category names per language.
    private static readonly string[] CategoryEn = { "HIGH CARD", "PAIR", "TWO PAIR", "THREE OF A KIND", "STRAIGHT", "FLUSH", "FULL HOUSE", "FOUR OF A KIND", "STRAIGHT FLUSH", "ROYAL FLUSH" };
    private static readonly string[] CategoryPt = { "CARTA ALTA", "PAR", "DOIS PARES", "TRINCA", "SEQUÊNCIA", "FLUSH", "FULL HOUSE", "QUADRA", "STRAIGHT FLUSH", "ROYAL FLUSH" };

    // Text builder, card buffers, the game shown and a rebuild pending.
    private readonly StringBuilder sb = new StringBuilder();
    private readonly List<Card> hole = new List<Card>();
    private readonly List<Card> shownBoard = new List<Card>();
    private HoldemGame shown;
    private bool dirty;

    // Checks references; hidden at start.
    private void Awake()
    {
        if (group == null || header == null || body == null || prompt == null) { enabled = false; return; }
        group.alpha = 0f;
    }

    // Listens to every table's changes.
    private void OnEnable()
    {
        foreach (HoldemGame g in games) if (g != null) g.Changed += MarkDirty;
        if (settings != null) settings.Changed += MarkDirty;
    }

    // Stops listening.
    private void OnDisable()
    {
        foreach (HoldemGame g in games) if (g != null) g.Changed -= MarkDirty;
        if (settings != null) settings.Changed -= MarkDirty;
    }

    // Something changed: rebuild on the next LateUpdate.
    private void MarkDirty() => dirty = true;

    // Fades in while a game is on; rebuilds when dirty.
    private void LateUpdate()
    {
        HoldemGame game = null;
        foreach (HoldemGame g in games)
            if (g != null && g.IsActive && g.CurrentState != HoldemGame.State.Joining) { game = g; break; }
        group.alpha = Mathf.MoveTowards(group.alpha, game != null ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
        if (game == null) return;
        if (game != shown) { shown = game; dirty = true; }
        if (!dirty) return;
        dirty = false;
        Rebuild(game);
    }

    // Writes chips, ante / call, the cards, the hands, the result and the prompt.
    private void Rebuild(HoldemGame game)
    {
        bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
        HoldemEngine e = game.Engine;
        bool inRound = e != null && e.CurrentPhase != HoldemEngine.Phase.Betting && game.ShownPlayerCards > 0;
        header.text = $"HOLD'EM   {(pt ? "FICHAS" : "CHIPS")} {game.Chips}" +
                      (inRound ? $"   <color=#C8C8C8>ANTE {e.Ante}{(e.Call > 0 ? $"  CALL {e.Call}" : "")}</color>" : "");
        sb.Clear();
        if (inRound)
        {
            hole.Clear();
            for (int i = 0; i < game.ShownPlayerCards && i < e.Player.Count; i++) hole.Add(e.Player[i]);
            shownBoard.Clear();
            for (int i = 0; i < game.ShownBoardCards && i < e.Board.Count; i++) shownBoard.Add(e.Board[i]);
            sb.Append($"<color=#C8C8C8>{(pt ? "VOCÊ" : "YOU")}</color>  ").Append(Cards(hole));
            if (hole.Count + shownBoard.Count >= 5) sb.Append("  <size=80%><color=#FFD24A>").Append(Name(HoldemEngine.Best(hole, shownBoard), pt)).Append("</color></size>");
            sb.Append('\n');
            sb.Append($"<color=#C8C8C8>{(pt ? "MESA" : "BOARD")}</color>  ").Append(Cards(shownBoard)).Append('\n');
            if (game.DealerShown)
            {
                sb.Append("<color=#C8C8C8>HELENA</color>  ").Append(Cards(e.Dealer));
                sb.Append("  <size=80%><color=#FFD24A>").Append(Name(HoldemEngine.Best(e.Dealer, e.Board), pt)).Append("</color></size>\n");
            }
            if (game.CurrentState == HoldemGame.State.RoundEnd || game.CurrentState == HoldemGame.State.AskAgain) sb.Append(Result(e.Outcome, pt));
        }
        body.text = sb.ToString();
        prompt.text = Prompt(game, pt);
    }

    // Cards as short text.
    private static string Cards(List<Card> cards)
    {
        var s = new StringBuilder();
        foreach (Card c in cards)
        {
            bool red = c.Suit == Suit.Hearts || c.Suit == Suit.Diamonds;
            s.Append(red ? "<color=#FF6A6A>" : "<color=#FFFFFF>").Append(c.ToString()).Append("</color> ");
        }
        return s.ToString();
    }

    // A hand's category name.
    private static string Name(HandValue v, bool pt) => (pt ? CategoryPt : CategoryEn)[(int)v.Category];

    // The result line.
    private static string Result(HoldemOutcome o, bool pt)
    {
        switch (o)
        {
            case HoldemOutcome.Win: return pt ? "<color=#7CFF8A>GANHOU</color>" : "<color=#7CFF8A>WIN</color>";
            case HoldemOutcome.DealerNoQualify: return pt ? "<color=#7CFF8A>ELA NÃO QUALIFICOU: ANTE PAGA</color>" : "<color=#7CFF8A>SHE DOESN'T QUALIFY: ANTE PAYS</color>";
            case HoldemOutcome.Push: return pt ? "<color=#BBBBBB>EMPATE</color>" : "<color=#BBBBBB>PUSH</color>";
            case HoldemOutcome.Fold: return pt ? "<color=#FF9F60>DESISTIU</color>" : "<color=#FF9F60>FOLDED</color>";
            case HoldemOutcome.Lose: return pt ? "<color=#FF6060>PERDEU</color>" : "<color=#FF6060>LOSE</color>";
            default: return "";
        }
    }

    // What can be said now.
    private static string Prompt(HoldemGame game, bool pt)
    {
        switch (game.CurrentState)
        {
            case HoldemGame.State.Betting:
                return pt ? $"APOSTA ({game.MinBet}-{game.MaxBet})  ·  MESMA  ·  CHEGA" : $"BET ({game.MinBet}-{game.MaxBet})  ·  SAME  ·  I'M DONE";
            case HoldemGame.State.AskAgain:
                return pt ? "MAIS UMA?  SIM  ·  APOSTA 20  ·  CHEGA" : "ANOTHER?  YES  ·  BET 20  ·  I'M DONE";
            case HoldemGame.State.Deciding:
                return pt ? "PAGO (2x A ANTE)  ·  DESISTO" : "CALL (2x THE ANTE)  ·  FOLD";
            default:
                return "";
        }
    }
}
