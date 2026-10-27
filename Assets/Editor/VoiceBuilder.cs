using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The operator's microphone (VoiceCommandInput): its indicator over the command line, its wiring, and (menu 3) the
// Vosk native libraries' import settings + a check that the language models are in StreamingAssets/Vosk.
internal static class VoiceBuilder
{
    // The native libraries and the model folders expected.
    private const string PluginFolder = "Assets/Plugins/Vosk/x86_64";
    private static readonly string[] PluginFiles = { "libvosk.dll", "libgcc_s_seh-1.dll", "libstdc++-6.dll", "libwinpthread-1.dll" };
    private static readonly string[] Models = { "Vosk/en", "Vosk/pt" };

    // menu 3: the dlls load in the editor and in Windows builds (64 bit)
    internal static void ConfigurePlugins()
    {
        int found = 0;
        foreach (string file in PluginFiles)
        {
            var pi = AssetImporter.GetAtPath($"{PluginFolder}/{file}") as PluginImporter;
            if (pi == null) continue;
            found++;
            pi.SetCompatibleWithAnyPlatform(false);
            pi.SetCompatibleWithEditor(true);
            pi.SetEditorData("CPU", "x86_64");
            pi.SetEditorData("OS", "Windows");
            pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            pi.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            pi.SaveAndReimport();
        }
        if (found < PluginFiles.Length)
            Debug.LogWarning($"[Voice] Vosk libraries missing in {PluginFolder} ({found}/{PluginFiles.Length}): voice commands off (typing still works).");
        foreach (string m in Models)
        {
            string path = Path.Combine(Application.streamingAssetsPath, m);
            bool ok = Directory.Exists(Path.Combine(path, "am")) || File.Exists(Path.Combine(path, "final.mdl"));
            if (!ok) Debug.LogWarning($"[Voice] No Vosk model in Assets/StreamingAssets/{m} (unzip a model so that {m}/am/final.mdl exists).");
        }
    }

    // Adds the microphone to the scene that is open (no rebuild): finds the operator's things already in it,
    // replaces an older Voice, adds it to what OperatorStation turns on in the camera program, saves the scene.
    [MenuItem("Last Terminal/9 - Add Voice Commands To Open Scene")]
    public static void AddToOpenScene()
    {
        ConfigurePlugins();
        Scene scene = SceneManager.GetActiveScene();
        PartnerCommands commands = First<PartnerCommands>(scene);
        SecurityCameraSystem cameraSystem = First<SecurityCameraSystem>(scene);
        SaveScreen saveScreen = First<SaveScreen>(scene);
        CameraSettings settings = First<CameraSettings>(scene);
        CommandConsole console = First<CommandConsole>(scene);
        OperatorStation station = First<OperatorStation>(scene);
        if (commands == null || cameraSystem == null || console == null)
        {
            Debug.LogError("[Voice] This scene has no Helena / camera system / command console: open a scene built by Last Terminal/4 or 8.");
            return;
        }
        foreach (VoiceCommandInput old in All<VoiceCommandInput>(scene)) Object.DestroyImmediate(old.gameObject);
        VoiceCommandInput voice = Build(console.transform.parent, commands, cameraSystem, saveScreen, settings, console);
        if (station != null)
        {
            var so = new SerializedObject(station);
            SerializedProperty list = so.FindProperty(OperatorStation.OperatorBehavioursField);
            int n = list.arraySize;
            list.arraySize = n + 1;
            list.GetArrayElementAtIndex(n).objectReferenceValue = voice;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = voice.gameObject;
        Debug.Log($"[Voice] Microphone added to {scene.name} (UI/Voice). Play, sit at the PC, hold V and talk.");
    }

    // editor only: the scene's objects (inactive too)
    private static List<T> All<T>(Scene scene) where T : Component
    {
        var found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
        return found;
    }

    private static T First<T>(Scene scene) where T : Component
    {
        List<T> all = All<T>(scene);
        return all.Count > 0 ? all[0] : null;
    }

    // the indicator above the command line + the component (on the console's object)
    internal static VoiceCommandInput Build(Transform canvas, PartnerCommands commands, SecurityCameraSystem cameraSystem, SaveScreen saveScreen,
                                            CameraSettings settings, CommandConsole console)
    {
        Vector2 bc = new Vector2(0.5f, 0f);
        var go = EditorUi.CreateUiObject(canvas, "Voice");
        go.transform.SetSiblingIndex(2);
        EditorUi.Place((RectTransform)go.transform, bc, bc, bc, new Vector2(0f, 240f), new Vector2(900f, 34f));
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TMP_Text text = EditorUi.CreateText(go.transform, "Text", 20, TextAlignmentOptions.Center, Color.white);
        EditorUi.Stretch(text.rectTransform);

        var voice = go.AddComponent<VoiceCommandInput>();
        EditorSerialized.SetReference(voice, VoiceCommandInput.CommandsField, commands);
        EditorSerialized.SetReference(voice, VoiceCommandInput.CameraSystemField, cameraSystem);
        EditorSerialized.SetReference(voice, VoiceCommandInput.SaveScreenField, saveScreen);
        EditorSerialized.SetReference(voice, VoiceCommandInput.SettingsField, settings);
        EditorSerialized.SetReference(voice, VoiceCommandInput.EnglishField, CommandDataBuilder.EnsureLexicon(GameLanguage.English));
        EditorSerialized.SetReference(voice, VoiceCommandInput.PortugueseField, CommandDataBuilder.EnsureLexicon(GameLanguage.Portuguese));
        EditorSerialized.SetReference(voice, VoiceCommandInput.ConsoleField, console);
        EditorSerialized.SetReference(voice, VoiceCommandInput.GroupField, group);
        EditorSerialized.SetReference(voice, VoiceCommandInput.TextField, text);
        return voice;
    }
}
