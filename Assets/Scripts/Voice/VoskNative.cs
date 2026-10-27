using System;
using System.Runtime.InteropServices;
using System.Text;

// The few calls of the Vosk speech recognizer (libvosk, Apache 2.0) the game uses, straight to the native library
// (Assets/Plugins/Vosk/x86_64/libvosk.dll + its 3 MinGW dlls). Strings go in and come out as UTF-8.
// Only VoskWorker calls these, on its own thread.
internal static class VoskNative
{
    // Native library name.
    private const string Lib = "libvosk";

    // The libvosk C functions (names as in vosk_api.h).
    [DllImport(Lib)] public static extern void vosk_set_log_level(int level);
    [DllImport(Lib)] private static extern IntPtr vosk_model_new(byte[] modelPath);
    [DllImport(Lib)] public static extern void vosk_model_free(IntPtr model);
    [DllImport(Lib)] private static extern int vosk_model_find_word(IntPtr model, byte[] word);
    [DllImport(Lib)] public static extern IntPtr vosk_recognizer_new(IntPtr model, float sampleRate);
    [DllImport(Lib)] private static extern IntPtr vosk_recognizer_new_grm(IntPtr model, float sampleRate, byte[] grammar);
    [DllImport(Lib)] public static extern int vosk_recognizer_accept_waveform_s(IntPtr recognizer, short[] data, int length);
    [DllImport(Lib)] private static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
    [DllImport(Lib)] private static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);
    [DllImport(Lib)] private static extern IntPtr vosk_recognizer_final_result(IntPtr recognizer);
    [DllImport(Lib)] public static extern void vosk_recognizer_reset(IntPtr recognizer);
    [DllImport(Lib)] public static extern void vosk_recognizer_free(IntPtr recognizer);

    // A string as a zero-terminated UTF-8 byte array.
    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s + "\0");

    // the library opens files with narrow (ANSI) paths on Windows: a folder with accents ("Área de Trabalho") is
    // passed by its short 8.3 name, which is plain ASCII
    public static IntPtr ModelNew(string path) => vosk_model_new(Utf8(AsciiPath(path)));

    // Windows: the short (8.3) form of a path.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathNameW(string longPath, StringBuilder shortPath, int size);

    // A path without non-ASCII characters (libvosk can't open those): the short form on Windows.
    private static string AsciiPath(string path)
    {
        bool ascii = true;
        foreach (char c in path) if (c > 127) { ascii = false; break; }
        if (ascii || Environment.OSVersion.Platform != PlatformID.Win32NT) return path;
        try
        {
            var sb = new StringBuilder(1024);
            int n = GetShortPathNameW(path, sb, sb.Capacity);
            return n > 0 && n < sb.Capacity ? sb.ToString() : path;
        }
        catch (Exception) { return path; }
    }
    // Is the word in the model's vocabulary?
    public static bool HasWord(IntPtr model, string word) => vosk_model_find_word(model, Utf8(word)) >= 0;
    // New recognizer, limited to a grammar (JSON list of phrases) when one is given.
    public static IntPtr RecognizerNew(IntPtr model, float rate, string grammarJson) =>
        string.IsNullOrEmpty(grammarJson) ? vosk_recognizer_new(model, rate) : vosk_recognizer_new_grm(model, rate, Utf8(grammarJson));
    // Result JSON of the last utterance / the partial one / the final one.
    public static string Result(IntPtr r) => FromUtf8(vosk_recognizer_result(r));
    public static string PartialResult(IntPtr r) => FromUtf8(vosk_recognizer_partial_result(r));
    public static string FinalResult(IntPtr r) => FromUtf8(vosk_recognizer_final_result(r));

    // a C string (UTF-8, ends with 0) owned by the library
    private static string FromUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";
        int n = 0;
        while (Marshal.ReadByte(p, n) != 0) n++;
        var bytes = new byte[n];
        Marshal.Copy(p, bytes, 0, n);
        return Encoding.UTF8.GetString(bytes);
    }
}
