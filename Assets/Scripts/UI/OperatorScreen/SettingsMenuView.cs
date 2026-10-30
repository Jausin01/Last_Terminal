using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The settings window of the operator's screen (Esc): pan control and speeds, camera filter and strength, language,
// subtitles, the microphone (device, level meter + record test, recognition test line) and the sound output test.
// One row per option, built in code; values come from CameraSettings and are refreshed when they change. The audio
// part is MicrophoneTest (shared with the PC's Settings window).
internal sealed class SettingsMenuView
{
    // Row labels per language, their texts, and the close button label.
    private readonly string[] settingsLabelsEn = new string[SettingsRowCount], settingsLabelsPt = new string[SettingsRowCount];
    private readonly TMP_Text[] settingsLabels = new TMP_Text[SettingsRowCount];
    private TMP_Text closeLabel;
    // Number of rows; each row's value refresher and value text.
    private const int SettingsRowCount = 10;
    private const int MicRow = 8;           // the record test row: its value is the live meter
    private readonly Action[] settingsRefresh = new Action[SettingsRowCount];
    private TMP_Text[] settingsValues;

    // The hud (settings, camera system, widgets, colors, language), the microphone part and its recognition line.
    private readonly SecurityCameraHud hud;
    private readonly MicrophoneTest micTest;
    private TMP_Text heardLine;

    public SettingsMenuView(SecurityCameraHud hud)
    {
        this.hud = hud;
        micTest = new MicrophoneTest(hud.Settings, hud.Audio);
    }

    // Builds the rows and the close button.
    internal void Build()
    {
        if (hud.SettingsRows == null) return;
        settingsValues = new TMP_Text[SettingsRowCount];
        AddRow(0, "PAN CONTROL", "CONTROLE DA CÂMERA", () => hud.Settings.SetPanControl(hud.Settings.Pan == CameraSettings.PanControl.Mouse ? CameraSettings.PanControl.Keyboard : CameraSettings.PanControl.Mouse), null,
               () => hud.Settings.Pan == CameraSettings.PanControl.Mouse ? hud.L("MOUSE (SCREEN EDGES)", "MOUSE (BORDAS DA TELA)") : hud.L("KEYBOARD (A / D)", "TECLADO (A / D)"));
        AddRow(1, "MOUSE PAN SPEED", "VELOCIDADE (MOUSE)", () => hud.Settings.SetMousePanSpeed(hud.Settings.MousePanSpeed + 10f), () => hud.Settings.SetMousePanSpeed(hud.Settings.MousePanSpeed - 10f),
               () => Mathf.RoundToInt(hud.Settings.MousePanSpeed).ToString());
        AddRow(2, "KEYBOARD PAN SPEED", "VELOCIDADE (TECLADO)", () => hud.Settings.SetKeyboardPanSpeed(hud.Settings.KeyboardPanSpeed + 10f), () => hud.Settings.SetKeyboardPanSpeed(hud.Settings.KeyboardPanSpeed - 10f),
               () => Mathf.RoundToInt(hud.Settings.KeyboardPanSpeed).ToString());
        AddRow(3, "CAMERA FILTER", "FILTRO DA CÂMERA", () => hud.Settings.SetFilterEnabled(!hud.Settings.FilterEnabled), null, () => hud.Settings.FilterEnabled ? hud.L("ON", "LIGADO") : hud.L("OFF", "DESLIGADO"));
        AddRow(4, "FILTER STRENGTH", "FORÇA DO FILTRO", () => hud.Settings.SetFilterStrength(hud.Settings.FilterStrength + 0.1f), () => hud.Settings.SetFilterStrength(hud.Settings.FilterStrength - 0.1f),
               () => Mathf.RoundToInt(hud.Settings.FilterStrength * 100f) + "%");
        AddRow(5, "LANGUAGE / IDIOMA", "IDIOMA / LANGUAGE", () => hud.Settings.SetLanguage(hud.Settings.Language == GameLanguage.English ? GameLanguage.Portuguese : GameLanguage.English), null,
               () => hud.Settings.Language == GameLanguage.English ? "ENGLISH" : "PORTUGUÊS");
        AddRow(6, "SUBTITLES", "LEGENDAS", () => hud.Settings.SetSubtitles(!hud.Settings.SubtitlesEnabled), null, () => hud.Settings.SubtitlesEnabled ? hud.L("ON", "LIGADAS") : hud.L("OFF", "DESLIGADAS"));
        AddRow(7, "MICROPHONE", "MICROFONE", micTest.NextDevice, null, () => micTest.DeviceLabel().ToUpperInvariant());
        AddRow(MicRow, "MIC TEST (CLICK: REC 3 S)", "TESTE (CLIQUE: GRAVA 3 S)", micTest.RecordAndListen, null, MicValue);
        AddRow(9, "SOUND OUTPUT", "SAÍDA DE SOM", micTest.PlayTestSound, null, () => hud.L("SYSTEM - CLICK TO TEST", "SISTEMA - CLIQUE P/ TESTAR"));
        heardLine = HudUi.NewText(hud.SettingsRows, "", 14, hud.Phosphor);
        heardLine.rectTransform.sizeDelta = new Vector2(600f, 40f);
        heardLine.rectTransform.anchoredPosition = new Vector2(20f, -SettingsRowCount * 52f + 6f);
        heardLine.textWrappingMode = TextWrappingModes.Normal;
        Button close = HudUi.NewButton(hud.SettingsRows, "CLOSE", 18, () => hud.CameraSystem.SetSettingsOpen(false), out Image closeImg, hud.Phosphor);
        closeLabel = close.GetComponentInChildren<TMP_Text>();
        closeImg.rectTransform.anchoredPosition = new Vector2(0f, -SettingsRowCount * 52f - 50f);
        closeImg.rectTransform.sizeDelta = new Vector2(160f, 36f);
    }

    // a row: label on the left, value in the middle, [-] [+] (or one button that cycles)
    private void AddRow(int index, string label, string labelPt, Action plus, Action minus, Func<string> value)
    {
        float y = -index * 52f;
        TMP_Text l = HudUi.NewText(hud.SettingsRows, label, 18, hud.Phosphor);
        settingsLabels[index] = l;
        settingsLabelsEn[index] = label;
        settingsLabelsPt[index] = labelPt;
        l.alignment = TextAlignmentOptions.Left;
        l.rectTransform.sizeDelta = new Vector2(260f, 40f);
        l.rectTransform.anchoredPosition = new Vector2(-230f, y);

        Button main = HudUi.NewButton(hud.SettingsRows, "", 18, minus == null ? plus : null, out Image mainImg, hud.Phosphor);
        mainImg.rectTransform.sizeDelta = new Vector2(260f, 36f);
        mainImg.rectTransform.anchoredPosition = new Vector2(70f, y);
        if (minus != null) main.transition = Selectable.Transition.None;   // just shows the value
        settingsValues[index] = main.GetComponentInChildren<TMP_Text>();
        settingsValues[index].enableAutoSizing = true;                      // long device names shrink to fit
        settingsValues[index].fontSizeMin = 10f;
        settingsValues[index].fontSizeMax = 18f;

        if (minus != null)
        {
            HudUi.NewButton(hud.SettingsRows, "-", 20, minus, out Image m, hud.Phosphor);
            m.rectTransform.sizeDelta = new Vector2(36f, 36f);
            m.rectTransform.anchoredPosition = new Vector2(230f, y);
            HudUi.NewButton(hud.SettingsRows, "+", 20, plus, out Image p, hud.Phosphor);
            p.rectTransform.sizeDelta = new Vector2(36f, 36f);
            p.rectTransform.anchoredPosition = new Vector2(272f, y);
        }
        settingsRefresh[index] = () => settingsValues[index].text = value();
    }

    // The record test row's value: the live meter, or the test's progress.
    private string MicValue() => micTest.Current == MicrophoneTest.Stage.Idle ? micTest.LevelBars() : micTest.RecordLabel().ToUpperInvariant();

    // The menu opened / closed: the microphone records for the meter and the tests only while it is open.
    internal void SetOpen(bool open)
    {
        if (open) { micTest.Open(); Refresh(); }
        else micTest.Close();
    }

    // Each frame while open: the record test, the meter, the recognition line.
    internal void Tick()
    {
        micTest.Tick(Time.unscaledDeltaTime);
        settingsRefresh[MicRow]?.Invoke();
        if (heardLine != null) heardLine.text = micTest.HeardLabel();
    }

    // Refreshes every settings value.
    internal void Refresh()
    {
        foreach (Action a in settingsRefresh) a?.Invoke();
    }

    // Texts that depend on the language: the row labels, the close button, the title, the values.
    internal void RefreshLanguage()
    {
        for (int i = 0; i < SettingsRowCount; i++)
            if (settingsLabels[i] != null) settingsLabels[i].text = hud.L(settingsLabelsEn[i], settingsLabelsPt[i]);
        if (closeLabel != null) closeLabel.text = hud.L("CLOSE", "FECHAR");
        if (hud.SettingsTitle != null) hud.SettingsTitle.text = "<mspace=0.6em>" + hud.L("SETTINGS", "CONFIGURAÇÕES") + "</mspace>";
        Refresh();
    }
}
