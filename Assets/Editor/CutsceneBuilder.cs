using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cutscenes in the scenes: the director (with its screen: bars, subtitles, fade), the free shot camera, Helena as an
// actor, the CutsceneA / CutsceneB states that let any clip play. Menus:
// 10 - add all that to the open scene (scenes built before this existed); the scene menus 4 / 8 do it too
// 11 - add the clip states to the selected Animator Controller (other characters)
// 12 - a new cutscene in the open scene, with a few example steps, where the Scene view is looking
internal static class CutsceneBuilder
{
    private const string SlotFolder = "Assets/Data/Cutscenes";
    private static readonly Color HelenaColor = new Color(0.85f, 0.25f, 0.3f);

    // ------------------------------------------------------------------ clip states
    internal static void EnsureSlotClips(out AnimationClip a, out AnimationClip b)
    {
        HelenaSetupMenu.EnsureFolder("Assets", "Data");
        HelenaSetupMenu.EnsureFolder("Assets/Data", "Cutscenes");
        a = SlotClip("CutsceneSlotA");
        b = SlotClip("CutsceneSlotB");
    }

    private static AnimationClip SlotClip(string name)
    {
        string path = $"{SlotFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip { name = name };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // two states with the empty clips on layer 0 (no transitions: entered and left from code)
    internal static void AddSlotStates(AnimatorController ctrl, Vector3 position)
    {
        EnsureSlotClips(out AnimationClip a, out AnimationClip b);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states)
            if (s.state.name == CutsceneClipSlots.StateA || s.state.name == CutsceneClipSlots.StateB) sm.RemoveState(s.state);
        sm.AddState(CutsceneClipSlots.StateA, position).motion = a;
        sm.AddState(CutsceneClipSlots.StateB, position + new Vector3(220f, 0f, 0f)).motion = b;
        EditorUtility.SetDirty(ctrl);
    }

    [MenuItem("Last Terminal/11 - Cutscenes: Add Clip States To Selected Animator Controller")]
    public static void AddSlotsToSelectedController()
    {
        var ctrl = Selection.activeObject as AnimatorController;
        if (ctrl == null) { Debug.LogError("[Cutscene] Select an Animator Controller in the Project window first."); return; }
        AddSlotStates(ctrl, new Vector3(600f, 400f, 0f));
        AssetDatabase.SaveAssets();
        EnsureSlotClips(out AnimationClip a, out AnimationClip b);
        Debug.Log($"[Cutscene] CutsceneA / CutsceneB added to {ctrl.name}. On the character's CutsceneActor set Slot A = {a.name}, Slot B = {b.name}.", ctrl);
    }

    // ------------------------------------------------------------------ the shot camera (before the camera system is wired)
    internal static SecurityCamera CreateShotCamera(Transform parent)
    {
        var go = new GameObject("CutsceneCam");
        go.transform.SetParent(parent, false);
        var cam = go.AddComponent<SecurityCamera>();
        HelenaSetupMenu.SetString(cam, SecurityCamera.LabelField, "CAM ??");
        var so = new SerializedObject(cam);
        so.FindProperty(SecurityCamera.YawRangeField).floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        return cam;
    }

    // ------------------------------------------------------------------ the director + its screen + Helena as an actor
    internal static CutsceneDirector Build(Transform canvas, SecurityCameraSystem cameraSystem, SecurityCamera shotCamera, OperatorStation station,
                                           CombatEncounter encounter, GameObject helena, PartnerAnimator partnerAnimator, PartnerVoice voice,
                                           StoryProgress story, CameraSettings settings, VoiceCommandInput voiceInput, CommandConsole console)
    {
        // her clip states
        EnsureSlotClips(out AnimationClip a, out AnimationClip b);
        HelenaSetupMenu.SetReference(partnerAnimator, PartnerAnimator.CutsceneSlotAField, a);
        HelenaSetupMenu.SetReference(partnerAnimator, PartnerAnimator.CutsceneSlotBField, b);
        var actor = helena.GetComponent<CutsceneActor>();
        if (actor == null) actor = helena.AddComponent<CutsceneActor>();
        HelenaSetupMenu.SetString(actor, CutsceneActor.DisplayNameField, "HELENA");
        HelenaSetupMenu.SetReference(actor, CutsceneActor.AgentField, helena.GetComponent<NavMeshAgent>());
        HelenaSetupMenu.SetReference(actor, CutsceneActor.AnimatorField, helena.GetComponent<Animator>());
        HelenaSetupMenu.SetReference(actor, CutsceneActor.PartnerAnimatorField, partnerAnimator);
        HelenaSetupMenu.SetReference(actor, CutsceneActor.RestField, helena.GetComponent<PartnerRest>());
        var aso = new SerializedObject(actor);
        aso.FindProperty("nameColor").colorValue = HelenaColor;
        aso.ApplyModifiedPropertiesWithoutUndo();

        // the screen
        var root = HelenaSetupMenu.CreateUiObject(canvas, "Cutscene");
        HelenaSetupMenu.Stretch((RectTransform)root.transform);
        var barsGo = HelenaSetupMenu.CreateUiObject(root.transform, "Bars");
        HelenaSetupMenu.Stretch((RectTransform)barsGo.transform);
        var bars = barsGo.AddComponent<CanvasGroup>();
        bars.blocksRaycasts = false;
        Bar(barsGo.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        Bar(barsGo.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        TMP_Text hint = HelenaSetupMenu.CreateText(barsGo.transform, "Skip", 18, TextAlignmentOptions.Right, new Color(0.7f, 0.7f, 0.7f));
        HelenaSetupMenu.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(300f, 30f));

        var subGo = HelenaSetupMenu.CreateUiObject(root.transform, "Subtitle");
        Vector2 bc = new Vector2(0.5f, 0f);
        HelenaSetupMenu.Place((RectTransform)subGo.transform, bc, bc, bc, new Vector2(0f, 96f), new Vector2(1100f, 90f));
        var subGroup = subGo.AddComponent<CanvasGroup>();
        subGroup.blocksRaycasts = false;
        Image band = HelenaSetupMenu.CreateUiObject(subGo.transform, "Band").AddComponent<Image>();
        HelenaSetupMenu.Stretch(band.rectTransform);
        band.color = new Color(0f, 0f, 0f, 0.5f);
        band.raycastTarget = false;
        TMP_Text sub = HelenaSetupMenu.CreateText(subGo.transform, "Text", 28, TextAlignmentOptions.Center, Color.white);
        HelenaSetupMenu.Stretch(sub.rectTransform);
        sub.margin = new Vector4(20f, 6f, 20f, 6f);

        // "say: ..." (the operator's line)
        var promptGo = HelenaSetupMenu.CreateUiObject(root.transform, "Prompt");
        HelenaSetupMenu.Place((RectTransform)promptGo.transform, bc, bc, bc, new Vector2(0f, 290f), new Vector2(1000f, 110f));
        var promptGroup = promptGo.AddComponent<CanvasGroup>();
        promptGroup.blocksRaycasts = false;
        Image promptBack = HelenaSetupMenu.CreateUiObject(promptGo.transform, "Back").AddComponent<Image>();
        HelenaSetupMenu.Stretch(promptBack.rectTransform);
        promptBack.color = new Color(0f, 0.04f, 0.08f, 0.8f);
        promptBack.raycastTarget = false;
        TMP_Text prompt = HelenaSetupMenu.CreateText(promptGo.transform, "Text", 30, TextAlignmentOptions.Center, Color.white);
        HelenaSetupMenu.Stretch(prompt.rectTransform);
        prompt.margin = new Vector4(20f, 8f, 20f, 8f);

        // the thoughts block (top-left, under the camera name)
        ThoughtView thoughts = ThoughtViewBuilder.Build(root.transform, settings);

        Image black = HelenaSetupMenu.CreateUiObject(root.transform, "Fade").AddComponent<Image>();
        HelenaSetupMenu.Stretch(black.rectTransform);
        black.color = Color.black;
        black.raycastTarget = false;
        var fade = black.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;

        // the director
        var go = new GameObject("CutsceneDirector");
        SceneManager.MoveGameObjectToScene(go, helena.scene);
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        var d = go.AddComponent<CutsceneDirector>();
        HelenaSetupMenu.SetReference(d, CutsceneDirector.CameraSystemField, cameraSystem);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ShotCameraField, shotCamera);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.StationField, station);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.EncounterField, encounter);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.HelenaField, actor);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.HelenaAnimatorField, partnerAnimator);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.HelenaVoiceField, voice);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.StoryField, story);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SettingsField, settings);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.AudioField, audio);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.BarsField, bars);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.FadeField, fade);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SubtitleGroupField, subGroup);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SubtitleTextField, sub);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.SkipHintField, hint);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.VoiceInputField, voiceInput);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ConsoleField, console);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.PromptGroupField, promptGroup);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.PromptTextField, prompt);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.ThoughtViewField, thoughts);
        return d;
    }

    private static void Bar(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pivot)
    {
        Image img = HelenaSetupMenu.CreateUiObject(parent, name).AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        HelenaSetupMenu.Place(img.rectTransform, min, max, pivot, Vector2.zero, new Vector2(0f, 70f));
    }

    // ------------------------------------------------------------------ menus on the open scene
    [MenuItem("Last Terminal/10 - Cutscenes: Add Director To Open Scene")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        SecurityCameraSystem cameraSystem = First<SecurityCameraSystem>(scene);
        PartnerAnimator partnerAnimator = First<PartnerAnimator>(scene);
        CommandConsole console = First<CommandConsole>(scene);
        if (cameraSystem == null || partnerAnimator == null || console == null)
        {
            Debug.LogError("[Cutscene] This scene has no camera system / Helena: open a scene built by Last Terminal/4 or 8.");
            return;
        }
        foreach (CutsceneDirector old in All<CutsceneDirector>(scene)) Object.DestroyImmediate(old.gameObject);
        Transform oldScreen = console.transform.parent.Find("Cutscene");
        if (oldScreen != null) Object.DestroyImmediate(oldScreen.gameObject);

        // the shot camera, added to the camera system's list
        SecurityCamera shot = null;
        foreach (SecurityCamera c in All<SecurityCamera>(scene)) if (c.name == "CutsceneCam") shot = c;
        if (shot == null)
        {
            shot = CreateShotCamera(cameraSystem.transform);
            var so = new SerializedObject(cameraSystem);
            SerializedProperty list = so.FindProperty(SecurityCameraSystem.CamerasField);
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        CutsceneDirector d = Build(console.transform.parent, cameraSystem, shot, First<OperatorStation>(scene), First<CombatEncounter>(scene),
                                   partnerAnimator.gameObject, partnerAnimator, First<PartnerVoice>(scene), First<StoryProgress>(scene),
                                   First<CameraSettings>(scene), First<VoiceCommandInput>(scene), console);
        foreach (Cutscene c in All<Cutscene>(scene)) HelenaSetupMenu.SetReference(c, Cutscene.DirectorField, d);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = d.gameObject;
        Debug.Log($"[Cutscene] Director added to {scene.name}. New cutscene: Last Terminal/12. (Run Last Terminal/2 once so her Animator has the clip states.)");
    }

    [MenuItem("Last Terminal/12 - Cutscenes: New Cutscene (where the Scene view looks)")]
    public static void NewCutscene()
    {
        Scene scene = SceneManager.GetActiveScene();
        CutsceneDirector d = First<CutsceneDirector>(scene);
        if (d == null) { Debug.LogError("[Cutscene] No director in this scene: run Last Terminal/10 first."); return; }
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 focus = view != null ? view.pivot : Vector3.zero;
        if (NavMesh.SamplePosition(focus, out NavMeshHit hit, 5f, NavMesh.AllAreas)) focus = hit.position;

        int n = All<Cutscene>(scene).Count + 1;
        var go = new GameObject($"Cutscene_{n:00}");
        Undo.RegisterCreatedObjectUndo(go, "New Cutscene");
        go.transform.position = focus;
        var cutscene = go.AddComponent<Cutscene>();
        HelenaSetupMenu.SetReference(cutscene, Cutscene.DirectorField, d);
        var trigger = go.AddComponent<CutsceneTrigger>();
        HelenaSetupMenu.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
        var tso = new SerializedObject(trigger);
        tso.FindProperty(CutsceneTrigger.WhenField).enumValueIndex = (int)CutsceneTrigger.When.Manual;
        tso.ApplyModifiedPropertiesWithoutUndo();

        // points: where she goes, where the shot is taken from (the Scene view's camera)
        var spot = new GameObject("Spot_Helena").transform;
        spot.SetParent(go.transform, false);
        spot.position = focus;
        var shot = new GameObject("Shot_01").transform;
        shot.SetParent(go.transform, false);
        if (view != null) shot.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
        else shot.SetPositionAndRotation(focus + new Vector3(0f, 2.5f, -4f), Quaternion.Euler(25f, 0f, 0f));
        Vector3 toShot = Vector3.ProjectOnPlane(shot.position - spot.position, Vector3.up);
        if (toShot.sqrMagnitude > 1e-4f) spot.rotation = Quaternion.LookRotation(toShot);   // she ends facing the camera

        // example steps (edit / delete them)
        CutsceneActor helena = d.Helena;
        var so = new SerializedObject(cutscene);
        SerializedProperty steps = so.FindProperty(Cutscene.StepsField);
        Add(steps, new CameraShotStep { shot = shot, label = "CAM ??" });
        Add(steps, new MoveStep { actor = helena, target = spot });
        Add(steps, new SayStep { actor = helena, textPt = "...Tem alguém aí?", textEn = "...Is someone there?" });
        Add(steps, new OperatorLineStep { textPt = "Estou aqui. Pode confiar em mim.", textEn = "I'm here. You can trust me." });
        Add(steps, new SayStep { actor = helena, textPt = "Eu sei que você está me vendo.", textEn = "I know you can see me." });
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        Debug.Log($"[Cutscene] {go.name} created (trigger: Manual - test it with the inspector's Play button in Play mode).", go);
    }

    // ------------------------------------------------------------------ the intro: the first time at the cameras
    [MenuItem("Last Terminal/13 - Cutscenes: Build Intro (Helena in the cell)")]
    public static void BuildIntroInOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        CutsceneDirector d = First<CutsceneDirector>(scene);
        if (d == null) { Debug.LogError("[Cutscene] No director in this scene: run Last Terminal/10 first."); return; }
        Cutscene intro = BuildIntro(scene, d, First<SecurityCameraSystem>(scene), First<OperatorStation>(scene));
        if (intro == null) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = intro.gameObject;
    }

    // Helena sitting in the cell complains nobody talks to her, asks the operator to say something ("Can you hear
    // me?"), recognizes his voice, gets up, faces the camera, complains, calls him a chicken, accepts it, and asks
    // to be taken out of there (he says "yes"). Starts the first time the operator opens the cameras.
    // null when the scene has no padded cell (the casino).
    internal static Cutscene BuildIntro(Scene scene, CutsceneDirector d, SecurityCameraSystem cameraSystem, OperatorStation station)
    {
        Room cell = null;
        foreach (Room r in All<Room>(scene)) if (r.DisplayName == "Padded Cell") cell = r;
        SecurityCamera cellCam = null;
        if (cameraSystem != null)
            foreach (SecurityCamera c in cameraSystem.Cameras) if (c != null && c.Room == cell && cell != null) { cellCam = c; break; }
        if (cell == null || cellCam == null)
        {
            Debug.LogWarning("[Cutscene] No padded cell (or its camera) in this scene: the intro is made in the facility scene.");
            return null;
        }
        foreach (Cutscene old in All<Cutscene>(scene)) if (old.name == "Cutscene_Intro") Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Cutscene_Intro");
        go.transform.position = cell.transform.position;
        var cutscene = go.AddComponent<Cutscene>();
        HelenaSetupMenu.SetReference(cutscene, Cutscene.DirectorField, d);
        HelenaSetupMenu.SetString(cutscene, Cutscene.IdField, "intro_cell");
        var trigger = go.AddComponent<CutsceneTrigger>();
        HelenaSetupMenu.SetReference(trigger, CutsceneTrigger.CutsceneField, cutscene);
        var tso = new SerializedObject(trigger);
        tso.FindProperty(CutsceneTrigger.WhenField).enumValueIndex = (int)CutsceneTrigger.When.SceneStart;
        tso.FindProperty("delay").floatValue = 1f;
        tso.ApplyModifiedPropertiesWithoutUndo();

        CutsceneActor h = d.Helena;
        var so = new SerializedObject(cutscene);
        SerializedProperty steps = so.FindProperty(Cutscene.StepsField);
        Add(steps, new CameraCutStep { camera = cellCam });
        Add(steps, new StateStep { actor = h, state = PartnerAnimator.FloorSitState, blend = 0f, wait = 1.5f });
        Add(steps, Line(h, "...Três dias. Ou quatro. Já perdi a conta.", "...Three days. Maybe four. I've lost count."));
        Add(steps, Line(h, "Essa câmera tá ligada faz uma hora e ninguém fala nada.", "That camera's been on for an hour and nobody says a word."));
        Add(steps, Line(h, "Ei! Eu sei que tem alguém aí. A luzinha tá vermelha.", "Hey! I know someone's there. The little light is red."));
        Add(steps, Line(h, "Fala alguma coisa. Qualquer coisa.", "Say something. Anything."));
        Add(steps, new OperatorLineStep { textPt = "Você consegue me ouvir?", textEn = "Can you hear me?" });
        Add(steps, new WaitStep { seconds = 0.6f });
        Add(steps, new StopAnimationStep { actor = h, blend = 1.1f });   // she gets up
        Add(steps, new WaitStep { seconds = 1.1f });
        Add(steps, new FaceStep { actor = h, lookAt = cellCam.transform, turnSpeed = 160f });
        Add(steps, Line(h, "Espera... essa voz.", "Wait... that voice."));
        Add(steps, Line(h, "Não. Não pode ser. Logo VOCÊ é o meu operador?", "No. No way. YOU are my operator?"));
        Add(steps, Line(h, "De todas as pessoas do mundo... tinha que ser você.", "Out of everyone in the world... it had to be you."));
        Add(steps, new WaitStep { seconds = 2.5f });   // he says nothing
        Add(steps, Line(h, "Nada? Vai ficar quieto? Típico.", "Nothing? You're just gonna stay quiet? Typical."));
        Add(steps, Line(h, "Frango medroso.", "Chicken."));
        Add(steps, Line(h, "...Tá. Eu não tenho escolha. Vou ter que aceitar isso.", "...Fine. I don't have a choice. I'll have to live with it."));
        Add(steps, Line(h, "Vamo logo, me tira daqui.", "Come on, get me out of here."));
        Add(steps, new OperatorLineStep { textPt = "Sim.", textEn = "Yes." });
        so.ApplyModifiedPropertiesWithoutUndo();

        if (station != null) HelenaSetupMenu.SetReference(station, OperatorStation.IntroCutsceneField, cutscene);
        Debug.Log("[Cutscene] Cutscene_Intro built (plays the first time the operator opens the cameras). Edit its steps freely.", go);
        return cutscene;
    }

    private static SayStep Line(CutsceneActor who, string pt, string en) => new SayStep { actor = who, textPt = pt, textEn = en, pauseAfter = 0.35f };

    private static void Add(SerializedProperty list, CutsceneStep step)
    {
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = step;
    }

    // editor only: the scene's objects (inactive too)
    internal static List<T> All<T>(Scene scene) where T : Component
    {
        var found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
        return found;
    }

    internal static T First<T>(Scene scene) where T : Component
    {
        List<T> all = All<T>(scene);
        return all.Count > 0 ? all[0] : null;
    }
}
