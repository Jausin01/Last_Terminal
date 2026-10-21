using System.Collections.Generic;
using System.Globalization;
using System.Text;

// What was understood from one command.
public struct ParsedCommand
{
    public string Text;                  // normalized text
    public CommandVerb Verb;
    public List<string> Keys;            // names found, in order (several keys can share a phrase: "ammo")
    public CommandModifier Modifier;     // which one: other / left / right / nearest / farthest
    public int Number;                   // "enemy 2" (0 = none)
    public bool Pronoun;                 // "it", "isso"... = the last target

    public bool HasKey(string key) => Keys != null && Keys.Contains(key);
}

// Text -> verb + names, using one CommandLexicon. Longest phrases win ("go through" over "go", "get to" over "get");
// the words of the verb aren't reused for names.
public class CommandParser
{
    private class Phrase
    {
        public string[] Words;
        public CommandVerb Verb;
        public CommandModifier Modifier;
        public int Number;
        public string Key;
    }

    private readonly List<Phrase> verbPhrases = new List<Phrase>();
    private readonly List<Phrase> namePhrases = new List<Phrase>();
    private readonly List<Phrase> modifierPhrases = new List<Phrase>();
    private readonly List<Phrase> numberPhrases = new List<Phrase>();
    private readonly HashSet<string> pronouns = new HashSet<string>();

    public CommandLexicon Lexicon { get; }

    public CommandParser(CommandLexicon lexicon)
    {
        Lexicon = lexicon;
        foreach (CommandLexicon.VerbPhrases v in lexicon.Verbs)
            foreach (string p in v.Phrases) Add(verbPhrases, p, v.Verb, null);
        foreach (CommandLexicon.NamePhrases n in lexicon.Names)
            foreach (string p in n.Phrases) Add(namePhrases, p, CommandVerb.None, n.Key);
        foreach (CommandLexicon.ModifierPhrases m in lexicon.Modifiers)
            foreach (string p in m.Phrases) Add(modifierPhrases, p, CommandVerb.None, null, m.Modifier);
        foreach (CommandLexicon.NumberPhrases n in lexicon.Numbers)
            foreach (string p in n.Phrases) Add(numberPhrases, p, CommandVerb.None, null, CommandModifier.None, n.Value);
        foreach (string p in lexicon.Pronouns)
        {
            string w = Normalize(p);
            if (w.Length > 0) pronouns.Add(w);
        }
        // longest first: the first match found on a word wins
        verbPhrases.Sort((a, b) => b.Words.Length.CompareTo(a.Words.Length));
        namePhrases.Sort((a, b) => b.Words.Length.CompareTo(a.Words.Length));
        modifierPhrases.Sort((a, b) => b.Words.Length.CompareTo(a.Words.Length));
        numberPhrases.Sort((a, b) => b.Words.Length.CompareTo(a.Words.Length));
    }

    private static void Add(List<Phrase> list, string text, CommandVerb verb, string key,
                            CommandModifier modifier = CommandModifier.None, int number = 0)
    {
        string n = Normalize(text);
        if (n.Length == 0) return;
        list.Add(new Phrase { Words = n.Split(' '), Verb = verb, Key = key, Modifier = modifier, Number = number });
    }

    public ParsedCommand Parse(string text)
    {
        string normalized = Normalize(text);
        string[] words = normalized.Length > 0 ? normalized.Split(' ') : new string[0];
        bool[] used = new bool[words.Length];
        var result = new ParsedCommand { Text = normalized, Verb = CommandVerb.None, Keys = new List<string>() };

        // verb: the longest phrase found (earliest when the same length)
        Phrase best = null;
        int bestAt = -1;
        foreach (Phrase p in verbPhrases)
        {
            if (best != null && p.Words.Length < best.Words.Length) break;
            int at = Find(words, used, p.Words);
            if (at >= 0 && (best == null || at < bestAt)) { best = p; bestAt = at; }
        }
        if (best != null)
        {
            result.Verb = best.Verb;
            Mark(used, bestAt, best.Words.Length);
        }

        // modifier: the longest phrase found ("mais proximo" before "proximo")
        foreach (Phrase p in modifierPhrases)
        {
            int at = Find(words, used, p.Words);
            if (at < 0) continue;
            result.Modifier = p.Modifier;
            Mark(used, at, p.Words.Length);
            break;
        }

        // names: longest first, every phrase of that length on the same words gives its key
        for (int at = 0; at < words.Length; at++)
        {
            if (used[at]) continue;
            int length = 0;
            foreach (Phrase p in namePhrases)
            {
                if (length > 0 && p.Words.Length < length) break;
                if (!MatchesAt(words, used, p.Words, at)) continue;
                length = p.Words.Length;
                if (!result.Keys.Contains(p.Key)) result.Keys.Add(p.Key);
            }
            if (length > 0) { Mark(used, at, length); at += length - 1; }
        }

        // number: digits ("2", "#2") or a spoken number, on words not used by a name ("1911" is the pistol)
        for (int i = 0; i < words.Length && result.Number == 0; i++)
            if (!used[i] && int.TryParse(words[i], out int n) && n > 0 && n < 100000) { result.Number = n; used[i] = true; }
        if (result.Number == 0)
            foreach (Phrase p in numberPhrases)
            {
                int at = Find(words, used, p.Words);
                if (at < 0) continue;
                result.Number = p.Number;
                Mark(used, at, p.Words.Length);
                break;
            }

        for (int i = 0; i < words.Length; i++)
            if (!used[i] && pronouns.Contains(words[i])) result.Pronoun = true;
        return result;
    }

    private static int Find(string[] words, bool[] used, string[] phrase)
    {
        for (int at = 0; at + phrase.Length <= words.Length; at++)
            if (MatchesAt(words, used, phrase, at)) return at;
        return -1;
    }

    private static bool MatchesAt(string[] words, bool[] used, string[] phrase, int at)
    {
        if (at + phrase.Length > words.Length) return false;
        for (int i = 0; i < phrase.Length; i++)
        {
            if (used[at + i]) return false;
            string p = phrase[i];
            string w = words[at + i];
            if (p.EndsWith("*") ? !w.StartsWith(p.Substring(0, p.Length - 1)) : p != w) return false;
        }
        return true;
    }

    private static void Mark(bool[] used, int at, int length)
    {
        for (int i = at; i < at + length; i++) used[i] = true;
    }

    // lower case, no accents, letters / digits / * only, single spaces
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        bool space = true;
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c) || c == '*') { sb.Append(c); space = false; }
            else if (!space) { sb.Append(' '); space = true; }
        }
        return sb.ToString().Trim();
    }
}
