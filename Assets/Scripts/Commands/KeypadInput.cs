using System.Collections.Generic;

// The keys of a keypad said in a command, in order: digits one by one ("um sete", "1 7") or whole numbers
// ("17", "dezessete", "seventeen", "vinte e tres"), mixed as you like,
// "confirm" (Keypad.Enter) and "clear" (Keypad.Clear). Read from the normalized text (CommandParser.Normalize):
// a code is a list of single digits, which the lexicon (one number per command) can't hold.
public static class KeypadInput
{
    // English words for the keypad keys (digits, confirm, clear).
    private static readonly Dictionary<string, char> English = new Dictionary<string, char>
    {
        { "zero", '0' }, { "one", '1' }, { "two", '2' }, { "three", '3' }, { "four", '4' },
        { "five", '5' }, { "six", '6' }, { "seven", '7' }, { "eight", '8' }, { "nine", '9' },
        { "enter", Keypad.Enter }, { "confirm", Keypad.Enter }, { "ok", Keypad.Enter }, { "okay", Keypad.Enter }, { "done", Keypad.Enter },
        { "submit", Keypad.Enter }, { "accept", Keypad.Enter },
        { "clear", Keypad.Clear }, { "delete", Keypad.Clear }, { "erase", Keypad.Clear }, { "cancel", Keypad.Clear }, { "reset", Keypad.Clear },
    };

    // Portuguese words for the keypad keys (digits, confirm, clear).
    private static readonly Dictionary<string, char> Portuguese = new Dictionary<string, char>
    {
        { "zero", '0' }, { "um", '1' }, { "uma", '1' }, { "dois", '2' }, { "duas", '2' }, { "tres", '3' }, { "quatro", '4' },
        { "cinco", '5' }, { "seis", '6' }, { "meia", '6' }, { "sete", '7' }, { "oito", '8' }, { "nove", '9' },
        { "confirma", Keypad.Enter }, { "confirmar", Keypad.Enter }, { "confirme", Keypad.Enter }, { "enter", Keypad.Enter },
        { "ok", Keypad.Enter }, { "entra", Keypad.Enter }, { "pronto", Keypad.Enter }, { "confirmado", Keypad.Enter },
        { "confirmo", Keypad.Enter }, { "envia", Keypad.Enter }, { "enviar", Keypad.Enter },
        { "apaga", Keypad.Clear }, { "apagar", Keypad.Clear }, { "apague", Keypad.Clear }, { "limpa", Keypad.Clear },
        { "limpar", Keypad.Clear }, { "cancela", Keypad.Clear }, { "corrige", Keypad.Clear }, { "corrigir", Keypad.Clear },
    };

    // whole numbers 10..90 said as words: their digits ("dezessete" = 1 7); a ten + a unit ("vinte e tres") = 2 3
    private static readonly Dictionary<string, int> EnglishTens = new Dictionary<string, int>
    {
        { "ten", 10 }, { "eleven", 11 }, { "twelve", 12 }, { "thirteen", 13 }, { "fourteen", 14 }, { "fifteen", 15 },
        { "sixteen", 16 }, { "seventeen", 17 }, { "eighteen", 18 }, { "nineteen", 19 }, { "twenty", 20 }, { "thirty", 30 },
        { "forty", 40 }, { "fifty", 50 }, { "sixty", 60 }, { "seventy", 70 }, { "eighty", 80 }, { "ninety", 90 },
    };

    // Portuguese tens / teens, as whole numbers.
    private static readonly Dictionary<string, int> PortugueseTens = new Dictionary<string, int>
    {
        { "dez", 10 }, { "onze", 11 }, { "doze", 12 }, { "treze", 13 }, { "catorze", 14 }, { "quatorze", 14 }, { "quinze", 15 },
        { "dezesseis", 16 }, { "dezasseis", 16 }, { "dezessete", 17 }, { "dezassete", 17 }, { "dezoito", 18 }, { "dezenove", 19 },
        { "dezanove", 19 }, { "vinte", 20 }, { "trinta", 30 }, { "quarenta", 40 }, { "cinquenta", 50 }, { "sessenta", 60 },
        { "setenta", 70 }, { "oitenta", 80 }, { "noventa", 90 },
    };

    // words that may come with the keys without meaning anything else ("o", "e", "the button")
    private static readonly HashSet<string> Filler = new HashSet<string>
    {
        "o", "a", "e", "no", "na", "de", "do", "da", "botao", "tecla", "numero", "digito", "depois", "agora", "entao",
        "the", "and", "button", "key", "number", "digit", "then", "now",
    };

    // every word read here (the recognizer listens for them while she is at a keypad)
    public static List<string> Words(GameLanguage language)
    {
        bool pt = language == GameLanguage.Portuguese;
        var list = new List<string>(pt ? Portuguese.Keys : English.Keys);
        list.AddRange(pt ? PortugueseTens.Keys : EnglishTens.Keys);
        list.Add(pt ? "e" : "and");
        return list;
    }

    // keys in order; onlyKeys = nothing else was said (a bare "seven" / "confirma" while she is at the keypad)
    public static string Keys(string normalizedText, GameLanguage language, out bool onlyKeys)
    {
        bool pt = language == GameLanguage.Portuguese;
        Dictionary<string, char> words = pt ? Portuguese : English;
        Dictionary<string, int> tens = pt ? PortugueseTens : EnglishTens;
        var keys = new System.Text.StringBuilder();
        onlyKeys = true;
        if (string.IsNullOrEmpty(normalizedText)) { onlyKeys = false; return ""; }
        string[] w = normalizedText.Split(' ');
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i].Length == 0) continue;
            if (tens.TryGetValue(w[i], out int n))
            {
                // "vinte e tres" / "twenty three": one number
                int j = i + 1;
                if (pt && j < w.Length && w[j] == "e") j++;
                if (n >= 20 && n % 10 == 0 && j < w.Length && words.TryGetValue(w[j], out char unit) && unit >= '1' && unit <= '9')
                {
                    n += unit - '0';
                    i = j;
                }
                keys.Append(n);
                continue;
            }
            if (words.TryGetValue(w[i], out char k)) { keys.Append(k); continue; }
            if (IsNumeral(w[i])) { keys.Append(w[i]); continue; }
            if (!Filler.Contains(w[i])) onlyKeys = false;
        }
        if (keys.Length == 0) onlyKeys = false;
        return keys.ToString();
    }

    // Is the word made of digits only?
    private static bool IsNumeral(string w)
    {
        foreach (char c in w)
            if (c < '0' || c > '9') return false;
        return true;
    }
}
