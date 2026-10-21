using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// The recognizer on its own thread (loading a model and decoding take time; the game never waits for it).
// The main thread sends jobs (load a model, audio, end of an utterance) and reads the outputs (loaded / failed,
// partial text, final text as Vosk JSON).
// Grammar: the words of the command lexicon, as the model spells them - the lexicon is written without accents
// and with wildcards ("deposito", "abr*"), so every word is looked up in the model's vocabulary with its accented
// spellings ("depósito") and, for a wildcard, the usual endings ("abre", "abra", "abrir"...). Only those words can
// be heard: commands come out much more reliably. A model that can't take a grammar is used free (any word).
public sealed class VoskWorker : IDisposable
{
    public enum OutputKind { Loaded, Failed, Partial, Final }

    public struct Output
    {
        public OutputKind Kind;
        public string Text;   // Loaded: a description; Failed: the error; Partial / Final: Vosk JSON
    }

    private enum JobKind { Load, Audio, Finish, Reset, Grammar }

    private struct Job
    {
        public JobKind Kind;
        public string Path;
        public string[] Words;
        public string[] Endings;
        public float Rate;
        public short[] Samples;
        public bool Partial;
    }

    private readonly BlockingCollection<Job> jobs = new BlockingCollection<Job>();
    private readonly ConcurrentQueue<Output> outputs = new ConcurrentQueue<Output>();
    private readonly Thread thread;
    private IntPtr model = IntPtr.Zero;
    private IntPtr recognizer = IntPtr.Zero;
    private string[] commandWords, commandEndings;
    private float sampleRate;

    // accented spellings tried for a lexicon word written without them
    private static readonly Dictionary<char, string> Accents = new Dictionary<char, string>
    {
        { 'a', "áàâã" }, { 'e', "éê" }, { 'i', "í" }, { 'o', "óôõ" }, { 'u', "ú" }, { 'c', "ç" },
    };

    public VoskWorker()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Vosk" };
        thread.Start();
    }

    public bool TryGetOutput(out Output output) => outputs.TryDequeue(out output);

    // words: every word of the lexicon (+ fillers); endings: what a "stem*" word can end with
    public void Load(string modelPath, float sampleRate, string[] words, string[] endings) =>
        Add(new Job { Kind = JobKind.Load, Path = modelPath, Rate = sampleRate, Words = words, Endings = endings });

    public void Audio(short[] samples, bool wantPartial) => Add(new Job { Kind = JobKind.Audio, Samples = samples, Partial = wantPartial });

    public void Finish() => Add(new Job { Kind = JobKind.Finish });

    public void Reset() => Add(new Job { Kind = JobKind.Reset });

    // listen only for these words (a line the operator has to say in a cutscene); null = the commands again
    public void UseWords(string[] words, string[] endings = null) => Add(new Job { Kind = JobKind.Grammar, Words = words, Endings = endings });

    private void Add(Job job)
    {
        if (!jobs.IsAddingCompleted) jobs.Add(job);
    }

    public void Dispose()
    {
        jobs.CompleteAdding();
        thread.Join(2000);
    }

    private void Run()
    {
        try
        {
            foreach (Job job in jobs.GetConsumingEnumerable())
            {
                try { Do(job); }
                catch (Exception e) { outputs.Enqueue(new Output { Kind = OutputKind.Failed, Text = e.GetType().Name + ": " + e.Message }); }
            }
        }
        finally
        {
            FreeAll();
        }
    }

    private void Do(Job job)
    {
        switch (job.Kind)
        {
            case JobKind.Load: DoLoad(job); break;
            case JobKind.Grammar: DoGrammar(job.Words, job.Endings); break;
            case JobKind.Audio:
                if (recognizer == IntPtr.Zero) break;
                bool endpoint = VoskNative.vosk_recognizer_accept_waveform_s(recognizer, job.Samples, job.Samples.Length) == 1;
                if (endpoint) outputs.Enqueue(new Output { Kind = OutputKind.Final, Text = VoskNative.Result(recognizer) });
                else if (job.Partial) outputs.Enqueue(new Output { Kind = OutputKind.Partial, Text = VoskNative.PartialResult(recognizer) });
                break;
            case JobKind.Finish:
                if (recognizer == IntPtr.Zero) break;
                outputs.Enqueue(new Output { Kind = OutputKind.Final, Text = VoskNative.FinalResult(recognizer) });
                VoskNative.vosk_recognizer_reset(recognizer);
                break;
            case JobKind.Reset:
                if (recognizer != IntPtr.Zero) VoskNative.vosk_recognizer_reset(recognizer);
                break;
        }
    }

    private void DoLoad(Job job)
    {
        FreeAll();
        try
        {
            VoskNative.vosk_set_log_level(-1);
            model = VoskNative.ModelNew(job.Path);
            if (model == IntPtr.Zero)
            {
                outputs.Enqueue(new Output { Kind = OutputKind.Failed, Text = "model not found or invalid: " + job.Path });
                return;
            }
            commandWords = job.Words;
            commandEndings = job.Endings;
            sampleRate = job.Rate;
            List<string> grammar = BuildGrammar(job.Words, job.Endings);
            string json = grammar.Count >= 10 ? ToJson(grammar) : null;
            recognizer = VoskNative.RecognizerNew(model, job.Rate, json);
            if (recognizer == IntPtr.Zero && json != null)
            {
                json = null;   // the model doesn't take a grammar: free recognition
                recognizer = VoskNative.RecognizerNew(model, job.Rate, null);
            }
            if (recognizer == IntPtr.Zero)
            {
                outputs.Enqueue(new Output { Kind = OutputKind.Failed, Text = "could not create the recognizer" });
                return;
            }
            outputs.Enqueue(new Output { Kind = OutputKind.Loaded, Text = json != null ? $"grammar: {grammar.Count} words" : "free recognition (no grammar)" });
        }
        catch (DllNotFoundException)
        {
            outputs.Enqueue(new Output { Kind = OutputKind.Failed, Text = "libvosk.dll not found (Assets/Plugins/Vosk/x86_64)" });
        }
        catch (EntryPointNotFoundException e)
        {
            outputs.Enqueue(new Output { Kind = OutputKind.Failed, Text = "libvosk.dll is too old: " + e.Message });
        }
    }

    private void DoGrammar(string[] words, string[] endings)
    {
        if (model == IntPtr.Zero) return;
        List<string> grammar = words != null ? BuildGrammar(words, endings ?? new[] { "" }) : BuildGrammar(commandWords, commandEndings);
        string json = words != null ? (grammar.Count > 1 ? ToJson(grammar) : null) : (grammar.Count >= 10 ? ToJson(grammar) : null);
        if (recognizer != IntPtr.Zero) { VoskNative.vosk_recognizer_free(recognizer); recognizer = IntPtr.Zero; }
        recognizer = VoskNative.RecognizerNew(model, sampleRate, json);
        if (recognizer == IntPtr.Zero) recognizer = VoskNative.RecognizerNew(model, sampleRate, null);
    }

    // the lexicon's words as the model spells them (+ "[unk]": anything else)
    private List<string> BuildGrammar(string[] words, string[] endings)
    {
        var found = new HashSet<string>();
        foreach (string raw in words)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string w = raw.Trim().ToLowerInvariant();
            if (w.EndsWith("*"))
            {
                string stem = w.TrimEnd('*');
                foreach (string end in endings) AddSpellings(stem + end, found);
            }
            else AddSpellings(w, found);
        }
        var list = new List<string>(found);
        list.Sort(StringComparer.Ordinal);
        list.Add("[unk]");
        return list;
    }

    // the word itself and its spellings with one accent / cedilla; two only when none of those exist
    private void AddSpellings(string word, HashSet<string> into)
    {
        if (word.Length == 0) return;
        bool any = false;
        if (VoskNative.HasWord(model, word)) { into.Add(word); any = true; }
        var once = new List<string>();
        Variants(word, 0, once);
        foreach (string v in once)
            if (VoskNative.HasWord(model, v)) { into.Add(v); any = true; }
        if (any) return;
        foreach (string v in once)
        {
            var twice = new List<string>();
            Variants(v, 0, twice);
            foreach (string t in twice)
                if (VoskNative.HasWord(model, t)) into.Add(t);
        }
    }

    // every spelling of 'word' with one more accent, at positions from 'start'
    private static void Variants(string word, int start, List<string> into)
    {
        for (int i = start; i < word.Length; i++)
        {
            if (!Accents.TryGetValue(word[i], out string options)) continue;
            foreach (char a in options) into.Add(word.Substring(0, i) + a + word.Substring(i + 1));
        }
    }

    private static string ToJson(List<string> words)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < words.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(words[i].Replace("\\", "").Replace("\"", "")).Append('"');
        }
        return sb.Append(']').ToString();
    }

    private void FreeAll()
    {
        if (recognizer != IntPtr.Zero) { VoskNative.vosk_recognizer_free(recognizer); recognizer = IntPtr.Zero; }
        if (model != IntPtr.Zero) { VoskNative.vosk_model_free(model); model = IntPtr.Zero; }
    }
}
