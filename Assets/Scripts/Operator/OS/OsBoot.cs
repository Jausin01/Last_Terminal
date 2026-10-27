using System;
using UnityEngine;

// The PC's full-screen stages: off, BIOS lines typed over time, the logo fading in / out, the login (password,
// hint after a few wrong tries), then the desktop.
internal sealed class OsBoot
{
    // Where the PC is: off, BIOS, logo, login, desktop.
    private enum Stage { Off, Bios, Logo, Login, Desktop }

    // The stage and its timer, the BIOS lines shown, the wrong passwords so far.
    private Stage stage = Stage.Off;
    private float stageTimer;
    private int biosShown;
    private int wrongTries;

    // The OS whose screen this part drives.
    private readonly OperatorOS os;

    public OsBoot(OperatorOS os) { this.os = os; }

    // Powered on: the BIOS starts (wrong tries reset).
    internal void OnPoweredOn()
    {
        wrongTries = 0;
        SetStage(Stage.Bios);
    }

    // Powered off: every window hidden, the screen black.
    internal void OnPoweredOff()
    {
        foreach (OsWindow w in os.Windows) w.Hide();
        os.SetStartMenu(false);
        SetStage(Stage.Off);
    }

    // Switches the full-screen stage (and resets the login).
    private void SetStage(Stage next)
    {
        stage = next;
        stageTimer = 0f;
        biosShown = 0;
        OperatorOS.Show(os.ScreenUi.off, next == Stage.Off);
        OperatorOS.Show(os.ScreenUi.bios, next == Stage.Bios);
        OperatorOS.Show(os.ScreenUi.logo, next == Stage.Logo);
        OperatorOS.Show(os.ScreenUi.loginScreen, next == Stage.Login);
        OperatorOS.Show(os.ScreenUi.desktopScreen, next == Stage.Desktop);
        if (os.ScreenUi.logo != null && next == Stage.Logo) os.ScreenUi.logo.alpha = 0f;
        if (os.ScreenUi.biosText != null && next == Stage.Bios) os.ScreenUi.biosText.text = "";
        if (next == Stage.Login)
        {
            if (os.LoginUi.user != null) os.LoginUi.user.text = os.UserName;
            if (os.LoginUi.message != null) os.LoginUi.message.text = "";
            if (os.LoginUi.passwordInput != null) os.LoginUi.passwordInput.text = "";
            if (os.IsOpen) FocusPassword();
        }
        if (next == Stage.Desktop) os.Refresh();
    }

    // The BIOS text (game language).
    private string[] BiosLines => new[]
    {
        "TERMINAL BIOS v2.07   (C) 1998-2004",
        "CPU: x86 733 MHz",
        os.T("Memory test: 262144K OK", "Teste de memória: 262144K OK"),
        os.T("Detecting drives... C: 40 GB", "Detectando discos... C: 40 GB"),
        os.T("Network: LINKED (1 remote node)", "Rede: CONECTADA (1 nó remoto)"),
        os.T("Camera bus: waiting for signal...", "Barramento de câmeras: aguardando sinal..."),
        "",
        os.T("Starting TERMINAL OS...", "Iniciando TERMINAL OS..."),
    };

    // Types the BIOS lines over time, fades the logo, then the login.
    internal void UpdateBoot()
    {
        stageTimer += Time.unscaledDeltaTime;
        if (stage == Stage.Bios)
        {
            string[] lines = BiosLines;
            int want = Mathf.Min(lines.Length, Mathf.FloorToInt(stageTimer / Mathf.Max(0.1f, os.BiosSeconds) * (lines.Length + 1)));
            if (want != biosShown && os.ScreenUi.biosText != null)
            {
                biosShown = want;
                os.ScreenUi.biosText.text = string.Join("\n", lines, 0, want) + (want < lines.Length ? "\n_" : "");
            }
            if (stageTimer >= os.BiosSeconds) SetStage(Stage.Logo);
        }
        else if (stage == Stage.Logo)
        {
            if (os.ScreenUi.logo != null)
            {
                float a = Mathf.Clamp01(stageTimer / 0.8f) * Mathf.Clamp01((os.LogoSeconds - stageTimer) / 0.6f);
                os.ScreenUi.logo.alpha = a;
            }
            if (stageTimer >= os.LogoSeconds) SetStage(Stage.Login);
        }
    }

    // Puts the cursor in the password field.
    internal void FocusPassword()
    {
        if (os.LoginUi.passwordInput == null) return;
        os.LoginUi.passwordInput.Select();
        os.LoginUi.passwordInput.ActivateInputField();
    }

    // Checks the password: desktop, or an error (and the hint after a few tries).
    internal void TryLogin()
    {
        if (stage != Stage.Login) return;
        string typed = os.LoginUi.passwordInput != null ? os.LoginUi.passwordInput.text : "";
        if (string.IsNullOrEmpty(os.Password) || string.Equals(typed.Trim(), os.Password, StringComparison.OrdinalIgnoreCase))
        {
            OperatorSounds.Play(os.Audio, OperatorSounds.Kind.Accepted, 0.5f);
            if (os.Story != null) os.Story.Set("pc_login");
            SetStage(Stage.Desktop);
            return;
        }
        wrongTries++;
        OperatorSounds.Play(os.Audio, OperatorSounds.Kind.Denied, 0.5f);
        if (os.LoginUi.message != null)
        {
            string msg = os.T("Wrong password.", "Senha incorreta.");
            if (wrongTries >= os.TriesBeforeHint) msg += "\n<size=80%>" + os.T(os.PasswordHintEn, os.PasswordHintPt) + "</size>";
            os.LoginUi.message.text = msg;
        }
        if (os.LoginUi.passwordInput != null) os.LoginUi.passwordInput.text = "";
        FocusPassword();
    }

    // At the login / on the desktop / typing the BIOS or showing the logo.
    internal bool AtLogin => stage == Stage.Login;
    internal bool AtDesktop => stage == Stage.Desktop;
    internal bool IsBooting => stage == Stage.Bios || stage == Stage.Logo;

    // The first stage: the login when the PC is always on, else off (until its power button).
    internal void Start(bool alwaysOn) => SetStage(alwaysOn ? Stage.Login : Stage.Off);
}
