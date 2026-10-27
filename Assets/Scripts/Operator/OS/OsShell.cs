using System;
using System.Text;
using UnityEngine;

// Start > Run (cmd, explorer, mail, cctv) and the hidden terminal: built-in commands (HELP, DIR, TYPE, WHOAMI,
// DATE, CLS, EXIT) and the custom entries of OperatorOS (which may set story flags).
internal sealed class OsShell
{
    // The terminal's text limit, its text so far, and the prompt.
    private const int TerminalMaxChars = 6000;
    private readonly StringBuilder terminalText = new StringBuilder();
    // The OS whose screen this part drives.
    private readonly OperatorOS os;

    public OsShell(OperatorOS os) { this.os = os; }

    // The prompt (C:\USER>).
    private string Prompt => $"C:\\{os.UserName.ToUpperInvariant()}>";

    // Start > Run: the dialog with an empty input.
    internal void OpenRun()
    {
        os.CloseStart();
        if (os.DialogUi.run == null) return;
        os.DialogUi.run.Open(os.T("Run", "Executar"));
        if (os.DialogUi.runInput != null)
        {
            os.DialogUi.runInput.text = "";
            os.DialogUi.runInput.Select();
            os.DialogUi.runInput.ActivateInputField();
        }
    }

    // Runs what was typed in Run (cmd, explorer, mail, cctv) or says it can't find it.
    internal void SubmitRun()
    {
        if (os.DialogUi.run == null || !os.DialogUi.run.IsOpen) return;
        string what = os.DialogUi.runInput != null ? os.DialogUi.runInput.text.Trim().ToLowerInvariant() : "";
        if (what.EndsWith(".exe")) what = what.Substring(0, what.Length - 4);
        os.DialogUi.run.Close();
        switch (what)
        {
            case "": return;
            case "cmd": case "terminal": case "command": case "prompt":
                OpenTerminal();
                break;
            case "explorer": case "explorador":
                os.Browser.OpenExplorer(null);
                break;
            case "mail": case "email": case "e-mail":
                os.Mailbox.OpenMail();
                break;
            case "cctv":
                os.OnCameras();
                break;
            default:
                OperatorSounds.Play(os.Audio, OperatorSounds.Kind.Denied, 0.4f);
                os.ShowMessage(os.T("Run", "Executar"), os.T($"Cannot find '{what}'.", $"Não foi possível encontrar '{what}'."));
                break;
        }
    }

    // Opens the terminal (banner the first time).
    private void OpenTerminal()
    {
        if (os.TerminalUi.window == null) return;
        if (terminalText.Length == 0)
        {
            terminalText.Append("TERMINAL OS [4.1.0207]\n");
            terminalText.Append(os.T("(C) State Government. Authorized use only.\n", "(C) Governo do Estado. Uso autorizado apenas.\n"));
            terminalText.Append(os.T("Type HELP for the commands.\n\n", "Digite AJUDA para os comandos.\n\n"));
        }
        os.TerminalUi.window.Open("C:\\WINDOWS\\SYSTEM32\\CMD.EXE");
        ShowTerminal();
        if (os.TerminalUi.input != null)
        {
            os.TerminalUi.input.text = "";
            os.TerminalUi.input.Select();
            os.TerminalUi.input.ActivateInputField();
        }
    }

    // Runs a typed terminal line and shows the output.
    internal void SubmitTerminal(string line)
    {
        if (os.TerminalUi.window == null || !os.TerminalUi.window.IsOpen) return;
        string cmd = (line ?? "").Trim();
        terminalText.Append(Prompt).Append(' ').Append(cmd).Append('\n');
        if (cmd.Length > 0) RunCommand(cmd);
        if (os.TerminalUi.input != null)
        {
            os.TerminalUi.input.text = "";
            os.TerminalUi.input.ActivateInputField();
        }
        ShowTerminal();
    }

    // Built-in commands (HELP, DIR, TYPE, WHOAMI, DATE, CLS, EXIT), then the custom entries.
    private void RunCommand(string cmd)
    {
        string verb = cmd;
        string arg = "";
        int space = cmd.IndexOf(' ');
        if (space > 0) { verb = cmd.Substring(0, space); arg = cmd.Substring(space + 1).Trim(); }
        verb = verb.ToLowerInvariant();
        switch (verb)
        {
            case "help": case "ajuda": case "?":
                Print(os.T("HELP      this list\nDIR       the files\nTYPE x    shows a text file\nWHOAMI    who is logged in\nDATE      date and time\nCLS       clears the screen\nEXIT      closes the terminal",
                        "AJUDA     esta lista\nDIR       os arquivos\nTYPE x    mostra um arquivo de texto\nWHOAMI    quem está conectado\nDATA      data e hora\nCLS       limpa a tela\nSAIR      fecha o terminal"));
                return;
            case "dir": case "ls":
                os.Content.CollectFiles();
                var sb = new StringBuilder();
                foreach (string f in os.Content.Folders())
                {
                    sb.Append(' ').Append(f == OsContent.RecycleKey ? os.T("RECYCLED", "LIXEIRA") : f.ToUpperInvariant()).Append("\\\n");
                    foreach (OperatorFile file in os.Content.UnlockedFiles)
                    {
                        bool inHere = f == OsContent.RecycleKey ? file.InRecycleBin : !file.InRecycleBin && file.Folder(os.Language) == f;
                        if (inHere) sb.Append("    ").Append(file.FileName(os.Language)).Append('\n');
                    }
                }
                Print(sb.ToString().TrimEnd('\n'));
                return;
            case "type": case "cat": case "more":
                OperatorFile found = os.Content.FindFile(arg);
                if (found == null) Print(os.T("File not found.", "Arquivo não encontrado."));
                else if (found.FileKind == OperatorFile.Kind.Image) Print(os.T("Binary file. Open it in My Computer.", "Arquivo binário. Abra pelo Meu Computador."));
                else { Print(found.Body(os.Language)); os.Content.MarkRead(OsContent.FileKey(found)); }
                return;
            case "whoami":
                Print(os.UserName.ToLowerInvariant() + "@terminal07");
                return;
            case "date": case "data": case "time": case "hora":
                Print(DateTime.Now.ToString(os.Pt ? "dd/MM/yyyy HH:mm:ss" : "MM/dd/yyyy HH:mm:ss"));
                return;
            case "cls": case "clear":
                terminalText.Length = 0;
                return;
            case "exit": case "sair":
                os.TerminalUi.window.Close();
                return;
        }
        string whole = cmd.ToLowerInvariant();
        foreach (OperatorOS.TerminalEntry e in os.TerminalEntries)
        {
            if (e == null || string.IsNullOrEmpty(e.command)) continue;
            foreach (string name in e.command.Split('|'))
            {
                if (name.Trim().ToLowerInvariant() != whole) continue;
                Print(os.Pt && !string.IsNullOrEmpty(e.responsePt) ? e.responsePt : e.responseEn);
                if (os.Story != null && !string.IsNullOrEmpty(e.setFlag)) os.Story.Set(e.setFlag);
                return;
            }
        }
        Print(os.T($"'{verb}' is not recognized as an internal command.", $"'{verb}' não é reconhecido como um comando interno."));
    }

    // Adds output to the terminal (keeps the last characters only).
    private void Print(string text)
    {
        terminalText.Append(text).Append("\n\n");
        if (terminalText.Length > TerminalMaxChars) terminalText.Remove(0, terminalText.Length - TerminalMaxChars);
    }

    // Shows the terminal text (as plain text) scrolled to the bottom.
    private void ShowTerminal()
    {
        if (os.TerminalUi.output != null) os.TerminalUi.output.text = "<noparse>" + terminalText.ToString().Replace("</noparse>", "") + "</noparse>";
        if (os.TerminalUi.scroll == null) return;
        Canvas.ForceUpdateCanvases();
        os.TerminalUi.scroll.verticalNormalizedPosition = 0f;
    }
}
