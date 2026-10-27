using System.Collections.Generic;
using UnityEngine;

// The e-mail window: the inbox (newest first, NEW marks), the open message (header, body, attachment) and its
// voice player (play / pause, progress bar, time).
internal sealed class OsMailbox
{
    // The rows made for the inbox, and the e-mail open.
    private readonly List<GameObject> mailRows = new List<GameObject>();
    private OperatorEmail mailOpen;

    // The OS whose screen this part drives.
    private readonly OperatorOS os;

    public OsMailbox(OperatorOS os) { this.os = os; }

    // Opens the inbox.
    internal void OpenMail()
    {
        os.CloseStart();
        os.Content.CollectMail();
        if (os.MailUi.window != null) os.MailUi.window.Open(os.T("E-mail - Inbox", "E-mail - Caixa de entrada"));
        mailOpen = null;
        ShowMailBody(null);
        BuildMailList();
    }

    // Rebuilds the inbox list (newest first, NEW marks, [VOICE] tag).
    internal void BuildMailList()
    {
        if (os.ExplorerUi.rowTemplate == null || os.MailUi.list == null) return;
        OperatorOS.Clear(mailRows);
        foreach (OperatorEmail m in os.Content.UnlockedMail)
        {
            OperatorEmail e = m;
            bool unread = !os.Content.IsRead(OperatorEmail.ReadPrefix + m.Id);
            string voice = os.MailUi.voiceBar != null ? $"<color=#555555>[{os.T("VOICE", "VOZ")}]</color> " : "";
            string label = voice + (unread ? $"<b><color=#C08A00>{os.T("NEW", "NOVO")}</color> " : "") + m.FromName + "  |  " + m.Subject(os.Language) + (unread ? "</b>" : "");
            if (m == mailOpen) label = "<u>" + label + "</u>";
            mailRows.Add(os.Row(os.MailUi.list, label, () => { os.Click(); ReadMail(e); }));
        }
        if (os.Content.UnlockedMail.Count == 0) ShowMailBody(null);
    }

    // Opens an e-mail (stops another one's voice) and marks it read.
    private void ReadMail(OperatorEmail m)
    {
        if (m != mailOpen) StopVoice();
        mailOpen = m;
        ShowMailBody(m);
        os.Content.MarkRead(OperatorEmail.ReadPrefix + m.Id);   // rebuilds the list (FlagSet)
        BuildMailList();
    }

    // Shows an e-mail's header, body, attachment and voice bar (or the empty / select text).
    private void ShowMailBody(OperatorEmail m)
    {
        if (os.MailUi.header != null)
            os.MailUi.header.text = m == null ? (os.Content.UnlockedMail.Count == 0 ? os.T("No messages.", "Nenhuma mensagem.") : os.T("Select a message.", "Selecione uma mensagem."))
                : $"<b>{os.T("From", "De")}:</b> {m.FromName} <noparse><{m.FromAddress}></noparse>\n<b>{os.T("Subject", "Assunto")}:</b> {m.Subject(os.Language)}\n<b>{os.T("Date", "Data")}:</b> {m.Date}";
        if (os.MailUi.body != null) os.MailUi.body.text = m != null ? m.Body(os.Language) : "";
        if (os.MailUi.bodyScroll != null) os.MailUi.bodyScroll.verticalNormalizedPosition = 1f;
        bool attached = m != null && m.Attachment != null;
        if (os.MailUi.attachment != null) os.MailUi.attachment.gameObject.SetActive(attached);
        if (attached && os.MailUi.attachmentLabel != null) os.MailUi.attachmentLabel.text = os.T("Attachment: ", "Anexo: ") + m.Attachment.FileName(os.Language);
        if (os.MailUi.voiceBar != null) os.MailUi.voiceBar.SetActive(m != null);
        ShowVoice();
    }

    // The recording of the open e-mail in the game language (null = none).
    internal AudioClip MailVoice => mailOpen != null ? mailOpen.Voice(os.Language) : null;

    // Play / pause the e-mail's recording (a buzz when there is none).
    internal void ToggleVoice()
    {
        if (os.MailUi.voiceSource == null || mailOpen == null) return;
        AudioClip clip = MailVoice;
        if (clip == null)
        {
            OperatorSounds.Play(os.Audio, OperatorSounds.Kind.Denied, 0.4f);
            return;
        }
        if (os.MailUi.voiceSource.isPlaying) os.MailUi.voiceSource.Pause();
        else
        {
            if (os.MailUi.voiceSource.clip != clip) { os.MailUi.voiceSource.clip = clip; os.MailUi.voiceSource.time = 0f; }
            else if (os.MailUi.voiceSource.time >= clip.length - 0.05f) os.MailUi.voiceSource.time = 0f;
            os.MailUi.voiceSource.Play();
        }
        ShowVoice();
    }

    // Stops the recording.
    internal void StopVoice()
    {
        if (os.MailUi.voiceSource == null) return;
        os.MailUi.voiceSource.Stop();
        os.MailUi.voiceSource.clip = null;
        ShowVoice();
    }

    // Updates the voice bar: icon, label, progress and time.
    internal void ShowVoice()
    {
        if (os.MailUi.voiceBar == null || mailOpen == null) return;
        AudioClip clip = MailVoice;
        AudioSource src = os.MailUi.voiceSource;
        bool playing = src != null && src.isPlaying && src.clip == clip;
        if (os.MailUi.voiceIcon != null) os.MailUi.voiceIcon.texture = playing ? os.MailUi.pauseTexture : os.MailUi.playTexture;
        if (os.MailUi.voiceLabel != null)
            os.MailUi.voiceLabel.text = clip != null ? os.T("Voice message from ", "Mensagem de voz de ") + mailOpen.FromName
                                                : $"<color=#777777>{os.T("Voice message: no recording yet", "Mensagem de voz: sem gravação ainda")}</color>";
        float played = src != null && src.clip == clip && clip != null ? src.time : 0f;
        float length = clip != null ? clip.length : 0f;
        if (os.MailUi.voiceFill != null) os.MailUi.voiceFill.anchorMax = new Vector2(length > 0f ? Mathf.Clamp01(played / length) : 0f, 1f);
        if (os.MailUi.voiceTime != null) os.MailUi.voiceTime.text = clip != null ? $"{Clock(played)} / {Clock(length)}" : "--:--";
    }

    // Seconds as m:ss.
    private static string Clock(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    // Opens the e-mail's attached file.
    internal void OpenAttachment()
    {
        if (mailOpen != null && mailOpen.Attachment != null) os.Browser.OpenFile(mailOpen.Attachment);
    }
}
