using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Last Terminal/16: turns the open scene (his own operator room, built by hand) into a working room. His objects are
// found by NAME and get their components (added once, then only re-wired: texts and values edited in the Inspector
// are kept); what this menu makes itself is named "[Gen] ..." (children of his objects) or lives in:
// - RoomSystems: rebuilt every time - him (first person + arms), his screen (HUD, thoughts, evidence panel, keypad
//   overlay, the fake OS), the station, settings, story, the room's invisible walls.
// - RoomLights / RoomExtras: the ceiling lamps and the new props - made only when missing (move them freely; delete
//   the root to get it made again).
// The camera program (Helena, the CCTV) is linked in the next step (additive scenes).
internal static class OperatorSceneSetup
{
    private const string PropFolder = "Assets/Art/Props/OperatorRoom";
    private const string OpArt = "Assets/Art/Props/Operator";
    private const string AudioFolder = "Assets/Audio/OperatorRoom";
    private const string ArmsControllerPath = OperatorRoomContent.DataFolder + "/RoomArms.controller";
    private const string SystemsRoot = "RoomSystems";
    private const string ExtrasRoot = "RoomExtras";
    private const string LightsRoot = "RoomLights";
    private const string Gen = "[Gen] ";
    private const float Tan33 = 0.6494f;   // half of the 66 deg view

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Scene scene;
    private static Shader lit;
    private static readonly List<string> missing = new List<string>();
    private static Vector3 roomCenter;
    private static OperatorStation station;
    private static StoryProgress story;
    private static CameraSettings settings;
    private static EvidencePanel panel;
    private static Material highlightMat;

    [MenuItem("Last Terminal/16 - Operator Room: Set Up Open Scene")]
    public static void SetUp()
    {
        scene = SceneManager.GetActiveScene();
        lit = HelenaSetupMenu.DefaultLitShader();
        if (lit == null) return;
        missing.Clear();
        GameObject floor = Find("Floor1");
        GameObject pc = Find("SM_OperatorPC");
        GameObject pcCase = Find("SM_ComputerCase");
        if (floor == null || pc == null || pcCase == null)
        {
            Debug.LogError("[Operator] Open your Operator scene first: Floor1, SM_OperatorPC and SM_ComputerCase are needed (by name).");
            return;
        }

        // ---------------------------------------------------------------- assets
        if (!AssetDatabase.IsValidFolder(PropFolder)) { Debug.LogError($"[Operator] {PropFolder} not found: extract the zip at the project root."); return; }
        OperatorRoomBuilder.RemapFolder(PropFolder, lit);
        List<OperatorFile> files = OperatorRoomContent.EnsureFiles();
        List<OperatorEmail> mails = OperatorRoomContent.EnsureEmails();
        RuntimeAnimatorController armsController = EnsureRoomArms();
        highlightMat = HighlightMaterial();

        // ---------------------------------------------------------------- the room's size
        Bounds floorBounds = WorldBounds(floor);
        GameObject ceilingGo = Find("Ceiling_Office");
        float ceiling = ceilingGo != null ? WorldBounds(ceilingGo).center.y : 3f;
        roomCenter = new Vector3(floorBounds.center.x, 1.5f, floorBounds.center.z);

        // ---------------------------------------------------------------- systems (rebuilt)
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == SystemsRoot) Object.DestroyImmediate(root);
        var sys = new GameObject(SystemsRoot).transform;
        settings = FirstInScene<CameraSettings>() ?? Child(sys, "Settings").gameObject.AddComponent<CameraSettings>();
        story = FirstInScene<StoryProgress>() ?? Child(sys, "Story").gameObject.AddComponent<StoryProgress>();
        BuildWalls(sys, floorBounds, ceiling);
        Darken();

        // him
        FirstPersonPlayer player = BuildPlayer(sys, floorBounds, armsController, out Camera roomCam, out AudioListener roomListener, out AudioSource voice);

        // his screen
        Transform canvas = BuildCanvas(sys);
        OperatorRoomHud hud = OperatorRoomBuilder.BuildRoomHud(canvas);
        panel = OperatorRoomUiBuilder.BuildEvidencePanel(canvas);
        OperatorRoomUiBuilder.BuildKeypadOverlay(canvas, out CanvasGroup kpGroup, out TMP_Text kpDisplay, out Button[] kpDigits, out Button kpClear, out Button kpEnter);
        PcPower power = SetUpCase(pcCase, pc);
        OperatorOS os = OperatorRoomUiBuilder.BuildOS(canvas, settings, story, voice, power, files, mails);
        ThoughtView thoughts = ThoughtViewBuilder.Build(canvas, settings);
        HelenaSetupMenu.SetReference(hud, OperatorRoomHud.ThoughtsField, thoughts);
        EnsureEventSystem(sys);
        Wire(player, FirstPersonPlayer.HudField, hud);

        // the station
        station = Child(sys, "OperatorStation").gameObject.AddComponent<OperatorStation>();

        // ---------------------------------------------------------------- his objects
        SetUpPc(pc, out Transform seatView, out Transform screenView);
        SetUpKeypad(kpGroup, kpDisplay, kpDigits, kpClear, kpEnter);
        SetUpDoor();
        SetUpWindow();
        SetUpCooler();
        SetUpBoards();
        SetUpFlavors();

        // ---------------------------------------------------------------- new things (made once)
        SetUpLights(ceiling);
        PinballMachine pinball = SetUpExtras();

        var so = new SerializedObject(station);
        so.FindProperty(OperatorStation.PlayerField).objectReferenceValue = player;
        so.FindProperty(OperatorStation.HudField).objectReferenceValue = hud;
        so.FindProperty(OperatorStation.DesktopField).objectReferenceValue = os;
        so.FindProperty(OperatorStation.RoomCameraField).objectReferenceValue = roomCam;
        so.FindProperty(OperatorStation.RoomListenerField).objectReferenceValue = roomListener;
        so.FindProperty(OperatorStation.SeatViewField).objectReferenceValue = seatView;
        so.FindProperty(OperatorStation.ScreenViewField).objectReferenceValue = screenView;
        so.FindProperty(OperatorStation.PinballField).objectReferenceValue = pinball;
        so.FindProperty(OperatorStation.PcPowerField).objectReferenceValue = power;
        so.FindProperty(OperatorStation.SettingsField).objectReferenceValue = settings;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = station.gameObject;
        string miss = missing.Count > 0 ? "\nNot found (skipped): " + string.Join(", ", missing) : "";
        Debug.Log($"[Operator] Room set up in {scene.name}: press Play. The PC starts OFF (button on the case). Login password: in OperatorOS (RoomSystems/RoomUI). Keypad code: RoomKeypad on Keypad.{miss}");
        GameObject stray = Find("SM_OpFilingCabinet (1)");
        if (stray != null) Debug.LogWarning("[Operator] 'SM_OpFilingCabinet (1)' (a single drawer) is on the floor in the middle of the room: left as it is.", stray);
    }

    // ================================================================== finding things
    private static GameObject Find(string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == SystemsRoot) continue;
            Transform t = HelenaSetupMenu.FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static GameObject Need(string name)
    {
        GameObject go = Find(name);
        if (go == null && !missing.Contains(name)) missing.Add(name);
        return go;
    }

    private static List<GameObject> FindAllStarting(string prefix)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) list.Add(t.gameObject);
        return list;
    }

    private static T FirstInScene<T>() where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T c = root.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // a "[Gen]" child: the existing one (kept as the user left it) or a new one
    private static Transform GenChild(Transform parent, string name, out bool made)
    {
        Transform t = parent.Find(Gen + name);
        made = t == null;
        if (made) t = Child(parent, Gen + name);
        return t;
    }

    private static T GetOrAdd<T>(GameObject go, out bool added) where T : Component
    {
        T c = go.GetComponent<T>();
        added = c == null;
        return added ? go.AddComponent<T>() : c;
    }

    private static void Wire(Object target, string field, Object value) => HelenaSetupMenu.SetReference(target, field, value);

    private static void Labels(OperatorInteractable i, string en, string pt)
    {
        HelenaSetupMenu.SetString(i, OperatorInteractable.LabelEnField, en);
        HelenaSetupMenu.SetString(i, OperatorInteractable.LabelPtField, pt);
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // the collider on the object itself (an interactable needs it there)
    private static void EnsureCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() == null) OperatorRoomBuilder.FitCollider(go);
    }

    // the direction a wall thing faces: its thinnest horizontal side, toward the middle of the room
    private static Vector3 FacingOut(Bounds b)
    {
        Vector3 toCenter = roomCenter - b.center;
        if (b.size.x < b.size.z) return new Vector3(Mathf.Sign(toCenter.x), 0f, 0f);
        return new Vector3(0f, 0f, Mathf.Sign(toCenter.z));
    }

    private static Transform ViewPoint(Transform parent, string name, Vector3 position, Vector3 lookDirection)
    {
        Transform t = GenChild(parent, name, out bool made);
        if (!made) return t;
        t.SetPositionAndRotation(position, Quaternion.LookRotation(lookDirection, Vector3.up));
        return t;
    }

    private static AudioSource Source(Transform parent, string name, float spatial, float volume, float maxDistance = 8f)
    {
        Transform t = GenChild(parent, name, out _);
        t.localPosition = Vector3.zero;
        AudioSource a = t.GetComponent<AudioSource>();
        if (a == null) a = t.gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = spatial;
        a.volume = volume;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 0.5f;
        a.maxDistance = maxDistance;
        return a;
    }

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");

    private static Light PointLight(Transform parent, string name, Vector3 worldPos, Color color, float intensity, float range, LightShadows shadows = LightShadows.None)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.position = worldPos;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows;
        return l;
    }

    // ================================================================== assets
    // relax (default) <- grab.R / push.R, from the takes of the arms file (by name or ending with it)
    private static RuntimeAnimatorController EnsureRoomArms()
    {
        AnimationClip relax = OperatorRoomBuilder.ArmsClip(FirstPersonPlayer.RelaxClip);
        AnimationClip grab = OperatorRoomBuilder.ArmsClip(FirstPersonPlayer.GrabClip);
        AnimationClip push = OperatorRoomBuilder.ArmsClip(FirstPersonPlayer.PushClip);
        if (relax == null)
        {
            Debug.LogWarning("[Operator] The arms' clips (relax, grab.R, push.R) were not found in Assets/Art/FirstPerson/SM_FPArms.fbx: the arms won't move.");
            return null;
        }
        AssetDatabase.DeleteAsset(ArmsControllerPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ArmsControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = sm.AddState(FirstPersonPlayer.RelaxState, new Vector3(300f, 100f, 0f));
        idle.motion = relax;
        sm.defaultState = idle;
        int k = 0;
        foreach (var (state, clip) in new[] { (FirstPersonPlayer.GrabState, grab), (FirstPersonPlayer.PushState, push) })
        {
            if (clip == null) { Debug.LogWarning($"[Operator] Arms clip '{state}' not found: that hand move is skipped."); continue; }
            AnimatorState s = sm.AddState(state, new Vector3(550f, 40f + 120f * k++, 0f));
            s.motion = clip;
            AnimatorStateTransition back = s.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.15f;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static Material HighlightMaterial()
    {
        string path = $"{PropFolder}/Materials/M_OpRoom_Highlight.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (m == null)
        {
            m = new Material(unlit != null ? unlit : lit);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor(BaseColorId, new Color(1f, 0.75f, 0.25f));
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Mat(string name, string texture, float smooth, Color? glow = null)
    {
        Texture2D tex = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{PropFolder}/{texture}.png") : null;
        Material m = HelenaSetupMenu.MakeMaterial($"{PropFolder}/Materials/{name}.mat", tex, smooth, false, lit);
        if (glow.HasValue) OperatorRoomBuilder.Glow(m, tex, glow.Value);
        return m;
    }

    // ================================================================== the room itself
    // invisible walls / floor / ceiling a little inside the floor's edges (his walls have no colliders)
    private static void BuildWalls(Transform sys, Bounds floor, float ceiling)
    {
        Transform root = Child(sys, "Walls");
        const float inset = 0.18f, thick = 0.4f;
        float x0 = floor.min.x + inset, x1 = floor.max.x - inset, z0 = floor.min.z + inset, z1 = floor.max.z - inset;
        float h = ceiling;
        Box(root, "Floor", new Vector3(floor.center.x, -0.2f, floor.center.z), new Vector3(floor.size.x + 1f, 0.4f, floor.size.z + 1f));
        Box(root, "Ceiling", new Vector3(floor.center.x, h + 0.2f, floor.center.z), new Vector3(floor.size.x + 1f, 0.4f, floor.size.z + 1f));
        Box(root, "North", new Vector3(floor.center.x, h / 2f, z1 + thick / 2f), new Vector3(floor.size.x + 1f, h, thick));
        Box(root, "South", new Vector3(floor.center.x, h / 2f, z0 - thick / 2f), new Vector3(floor.size.x + 1f, h, thick));
        Box(root, "East", new Vector3(x1 + thick / 2f, h / 2f, floor.center.z), new Vector3(thick, h, floor.size.z + 1f));
        Box(root, "West", new Vector3(x0 - thick / 2f, h / 2f, floor.center.z), new Vector3(thick, h, floor.size.z + 1f));
        // his furniture: a box collider where there is none
        foreach (string n in new[] { "SM_OpDesk", "SM_Ini_OfficeChair", "SM_OpFilingCabinet", "SM_OpTrashCan", "SM_OpWaterCooler", "SM_ComputerCase", "Door_Metal" })
        {
            GameObject go = Find(n);
            if (go != null) EnsureCollider(go);
        }
        foreach (GameObject plant in FindAllStarting("SM_OpPlant")) EnsureCollider(plant);
    }

    private static void Box(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        Transform t = Child(parent, name);
        t.position = pos;
        t.gameObject.AddComponent<BoxCollider>().size = size;
    }

    // a dark room lit by its own lamps: the sun off, flat dark ambient, his Main Camera off (he has his own eyes)
    private static void Darken()
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == SystemsRoot) continue;
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Directional && l.gameObject.activeSelf)
                {
                    l.gameObject.SetActive(false);
                    Debug.Log($"[Operator] '{l.name}' turned off (a closed room: the ceiling lamps light it). Turn it back on if you want it.", l);
                }
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
                if (c.name == "Main Camera" && c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(false);
                    Debug.Log("[Operator] 'Main Camera' turned off: the room has his own eyes (RoomSystems/Operator/Eyes).", c);
                }
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.085f);
        RenderSettings.reflectionIntensity = 0.35f;
    }

    // ================================================================== him
    private static FirstPersonPlayer BuildPlayer(Transform sys, Bounds floor, RuntimeAnimatorController arms, out Camera cam, out AudioListener listener, out AudioSource voice)
    {
        var body = new GameObject("Operator");
        body.transform.SetParent(sys, false);
        body.transform.position = new Vector3(floor.center.x + 0.8f, 0.02f, floor.center.z + 1.2f);
        body.transform.rotation = Quaternion.Euler(0f, 215f, 0f);   // toward the desk
        var cc = body.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.25f;
        cc.center = new Vector3(0f, 0.875f, 0f);
        cc.stepOffset = 0.3f;
        voice = body.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        var eyes = new GameObject("Eyes");
        eyes.transform.SetParent(body.transform, false);
        eyes.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        eyes.tag = "MainCamera";
        cam = eyes.AddComponent<Camera>();
        cam.fieldOfView = 66f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 60f;
        cam.enabled = false;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        listener = eyes.AddComponent<AudioListener>();
        listener.enabled = false;
        Animator armsAnimator = OperatorRoomBuilder.BuildArms(eyes.transform, out GameObject cup, arms);
        var player = body.AddComponent<FirstPersonPlayer>();
        Wire(player, FirstPersonPlayer.ControllerField, cc);
        Wire(player, FirstPersonPlayer.ViewField, cam);
        Wire(player, FirstPersonPlayer.ArmsField, armsAnimator);
        Wire(player, FirstPersonPlayer.CupField, cup);
        Wire(player, FirstPersonPlayer.SettingsField, settings);
        Wire(player, FirstPersonPlayer.StoryField, story);
        Wire(player, FirstPersonPlayer.AudioField, voice);
        return player;
    }

    private static Transform BuildCanvas(Transform sys)
    {
        var go = new GameObject("RoomUI", typeof(RectTransform));
        go.transform.SetParent(sys, false);
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    private static void EnsureEventSystem(Transform sys)
    {
        if (FirstInScene<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.transform.SetParent(sys, false);
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ================================================================== the PC and its case
    // the case: power button, red / green light, fan, the jingle; the monitor's screen quad and glow
    private static PcPower SetUpCase(GameObject pcCase, GameObject pc)
    {
        EnsureCollider(pcCase);
        PcPower power = GetOrAdd<PcPower>(pcCase, out _);
        Bounds b = WorldBounds(pcCase);
        Vector3 fwd = pcCase.transform.forward;
        float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z))));
        Vector3 ledPos = b.center + fwd * (depth + 0.003f) + Vector3.up * (b.extents.y * 0.72f);

        Transform led = GenChild(pcCase.transform, "PowerLed", out bool ledMade);
        if (ledMade)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = "Lamp";
            cube.transform.SetParent(led, false);
            cube.transform.localScale = new Vector3(0.012f, 0.012f, 0.006f);
            led.position = ledPos;
            led.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
        Renderer ledRenderer = led.GetComponentInChildren<Renderer>();
        if (ledRenderer != null)
        {
            ledRenderer.sharedMaterial = Mat("M_OpRoom_Led", null, 0.7f, new Color(1f, 0.1f, 0.06f) * 2f);
            ledRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        Light ledLight = led.GetComponentInChildren<Light>();
        if (ledLight == null)
        {
            ledLight = PointLight(led, "Glow", led.position + led.forward * 0.03f, Color.red, 0.25f, 0.45f);
        }

        // the monitor: a quad over its screen (off: black, on: the OS) + the glow on the desk
        Transform screen = GenChild(pc.transform, "Screen", out bool screenMade);
        if (screenMade)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Quad";
            quad.transform.SetParent(screen, false);
            screen.localPosition = new Vector3(0.048f, 0.257f, -0.0705f);
            screen.localRotation = Quaternion.Euler(0f, 180f, 0f);
            screen.localScale = new Vector3(0.42f, 0.24f, 1f);
        }
        Renderer screenRenderer = screen.GetComponentInChildren<Renderer>();
        if (screenRenderer != null) screenRenderer.shadowCastingMode = ShadowCastingMode.Off;
        Material off = Mat("M_OpRoom_ScreenOff", "T_OpRoom_ScreenOff", 0.92f);
        Material on = Mat("M_OpRoom_ScreenOn", "T_OpRoom_ScreenOn", 0.6f, Color.white * 1.1f);
        Transform glowT = GenChild(pc.transform, "ScreenGlow", out bool glowMade);
        Light screenGlow = glowT.GetComponent<Light>();
        if (glowMade || screenGlow == null)
        {
            screenGlow = glowT.gameObject.AddComponent<Light>();
            screenGlow.type = LightType.Point;
            screenGlow.color = new Color(0.55f, 0.85f, 1f);
            screenGlow.intensity = 0.6f;
            screenGlow.range = 2.4f;
            screenGlow.shadows = LightShadows.None;
            glowT.position = pc.transform.TransformPoint(new Vector3(0.048f, 0.257f, 0.35f));
        }

        AudioSource fan = Source(pcCase.transform, "Fan", 1f, 0.22f, 6f);
        fan.clip = Clip("PC_FanHum");
        fan.loop = true;
        AudioSource speaker = Source(pcCase.transform, "Speaker", 0.8f, 0.9f, 14f);

        Wire(power, PcPower.LedField, ledRenderer);
        Wire(power, PcPower.LedLightField, ledLight);
        Wire(power, PcPower.ScreenField, screenRenderer);
        Wire(power, PcPower.ScreenOffField, off);
        Wire(power, PcPower.ScreenOnField, on);
        Wire(power, PcPower.ScreenGlowField, screenGlow);
        Wire(power, PcPower.HumField, fan);
        Wire(power, PcPower.JingleSourceField, speaker);
        Wire(power, PcPower.StartupClipField, Clip("OS_Startup"));
        Wire(power, PcPower.ShutdownClipField, Clip("OS_Shutdown"));
        if (screenRenderer != null) screenRenderer.sharedMaterial = off;
        return power;
    }

    // the PC: used = sit down; where his eyes go (seat, right in front of the screen)
    private static void SetUpPc(GameObject pc, out Transform seatView, out Transform screenView)
    {
        EnsureCollider(pc);
        OperatorPc use = GetOrAdd<OperatorPc>(pc, out bool added);
        if (added) Labels(use, "Computer", "Computador");
        Wire(use, OperatorPc.StationField, station);
        Transform t = pc.transform;
        Vector3 screen = t.TransformPoint(new Vector3(0.048f, 0.257f, -0.071f));
        Vector3 front = t.forward;
        seatView = ViewPoint(t, "SeatView", t.TransformPoint(new Vector3(0.048f, 0.47f, 0.62f)), screen - t.TransformPoint(new Vector3(0.048f, 0.47f, 0.62f)));
        screenView = ViewPoint(t, "ScreenView", screen + front * 0.19f, -front);
    }

    // ================================================================== keypad, door, window, water
    private static void SetUpKeypad(CanvasGroup group, TMP_Text display, Button[] digits, Button clear, Button enter)
    {
        GameObject pad = Need("Keypad");
        if (pad == null) return;
        EnsureCollider(pad);
        RoomKeypad keypad = GetOrAdd<RoomKeypad>(pad, out bool added);
        if (added) Labels(keypad, "Keypad", "Teclado da porta");
        Bounds b = WorldBounds(pad);
        Vector3 n = FacingOut(b);
        Transform view = ViewPoint(pad.transform, "View", b.center + n * 0.42f, -n);
        AudioSource beeper = Source(pad.transform, "Beeper", 0.9f, 0.9f, 8f);
        Wire(keypad, FocusInteractable.StationField, station);
        Wire(keypad, FocusInteractable.ViewPointField, view);
        Wire(keypad, RoomKeypad.AudioField, beeper);
        Wire(keypad, RoomKeypad.GroupField, group);
        Wire(keypad, RoomKeypad.DisplayField, display);
        Wire(keypad, RoomKeypad.ClearButtonField, clear);
        Wire(keypad, RoomKeypad.EnterButtonField, enter);
        HelenaSetupMenu.SetReferenceArray(keypad, RoomKeypad.DigitButtonsField, digits.ToList());
    }

    private static void SetUpDoor()
    {
        GameObject door = Need("Door_Metal");
        if (door == null) return;
        EnsureCollider(door);
        OperatorDoor use = GetOrAdd<OperatorDoor>(door, out bool added);
        if (added) Labels(use, "Door", "Porta");
        Wire(use, OperatorDoor.LeafField, door.transform);
    }

    // the barricaded window: the metal plate (its Wall_Metal part) says something, with a metal clang
    private static void SetUpWindow()
    {
        GameObject wall = Need("Wall_Window_Barricaded");
        if (wall == null) return;
        Transform area = GenChild(wall.transform, "Window", out bool made);
        if (made)
        {
            Bounds plate = SubmeshBounds(wall, "Wall_Metal", out bool found);
            if (!found) plate = new Bounds(new Vector3(-2.0f, 2.25f, 4.9f), new Vector3(1.6f, 1.0f, 0.2f));
            Vector3 n = FacingOut(WorldBounds(wall));
            area.position = plate.center + n * 0.05f;
            area.rotation = Quaternion.identity;
            var box = area.gameObject.AddComponent<BoxCollider>();
            Vector3 size = plate.size;
            if (Mathf.Abs(n.x) > 0.5f) size.x = 0.12f; else size.z = 0.12f;
            Vector3 s = area.lossyScale;
            box.size = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
            area.gameObject.AddComponent<OperatorFlavor>().EditorSet("Window", "Janela",
                new[] { "This thing is completely sealed.", "Welded from the outside.", "Not even light gets through.", "...someone didn't want me to see where I am." },
                new[] { "Isso aqui está completamente selado.", "Soldada por fora.", "Nem a luz passa.", "...alguém não queria que eu visse onde estou." },
                OperatorSounds.Kind.Metal, OperatorInteractable.HandMotion.Push, true);
        }
    }

    // the world bounds of the part of a model that uses a material (by name)
    private static Bounds SubmeshBounds(GameObject go, string material, out bool found)
    {
        found = false;
        var bounds = new Bounds();
        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith(material)) continue;
                Bounds local = mf.sharedMesh.GetSubMesh(i).bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 w = r.transform.TransformPoint(p);
                    if (!found) { bounds = new Bounds(w, Vector3.zero); found = true; }
                    else bounds.Encapsulate(w);
                }
            }
        }
        return bounds;
    }

    private static void SetUpCooler()
    {
        GameObject cooler = Need("SM_OpWaterCooler");
        if (cooler == null) return;
        EnsureCollider(cooler);
        WaterCooler use = GetOrAdd<WaterCooler>(cooler, out _);
        Wire(use, WaterCooler.AudioField, Source(cooler.transform, "Water", 0.9f, 0.9f, 8f));
    }

    // ================================================================== evidence boards
    private enum BoardKind { Main, Old, Generic, Desk }

    private static void SetUpBoards()
    {
        Board("SM_OpCorkBoardA", BoardKind.Main, "Evidence board", "Quadro de evidências");
        Board("SM_OpCorkBoardB", BoardKind.Old, "Old case board", "Quadro do caso antigo");
        Board("psx Cork board for kasujin", BoardKind.Generic, "Old case board", "Quadro do caso antigo");
        Board("SM_Ini_CorkBoard", BoardKind.Desk, "Notes", "Anotações");
    }

    private static void Board(string name, BoardKind kind, string labelEn, string labelPt)
    {
        GameObject go = Need(name);
        if (go == null) return;
        EnsureCollider(go);
        EvidenceBoard board = GetOrAdd<EvidenceBoard>(go, out bool added);
        if (added) Labels(board, labelEn, labelPt);
        Bounds b = WorldBounds(go);
        Vector3 n = FacingOut(b);
        float w = Mathf.Abs(n.x) > 0.5f ? b.size.z : b.size.x;
        float h = b.size.y;
        float d = Mathf.Max(h * 0.5f / Tan33, w * 0.5f / (Tan33 * 1.6f)) * 1.12f;
        Vector3 face = b.center + n * (Mathf.Abs(n.x) > 0.5f ? b.extents.x : b.extents.z);
        Transform view = ViewPoint(go.transform, "View", face + n * Mathf.Max(0.5f, d), -n);

        if (go.GetComponentInChildren<EvidenceItem>(true) == null)
        {
            switch (kind)
            {
                case BoardKind.Main: ItemsFromTexture(go.transform, OperatorRoomContent.MainBoard, OperatorRoomContent.MainTexture); break;
                case BoardKind.Old: ItemsFromTexture(go.transform, OperatorRoomContent.OldBoard, OperatorRoomContent.OldTexture); break;
                case BoardKind.Desk: ItemsLocal(go.transform, OperatorRoomContent.DeskBoard, 0.015f); break;
                default: ItemsOnFace(go.transform, OperatorRoomContent.GenericOldBoard, face, n, w, h); break;
            }
        }
        Wire(board, FocusInteractable.StationField, station);
        Wire(board, FocusInteractable.ViewPointField, view);
        Wire(board, EvidenceBoard.PanelField, panel);
        HelenaSetupMenu.SetReferenceArray(board, EvidenceBoard.ItemsField, go.GetComponentsInChildren<EvidenceItem>(true).ToList());
    }

    // the cork boards of the package (SM_OpCorkBoardA/B): the cork face is x 0.437..-0.437 (u 0..1), y 0.015..0.524 (v 0..1), z -0.011
    private static void ItemsFromTexture(Transform board, OperatorRoomContent.Item[] items, Vector2 texSize)
    {
        foreach (OperatorRoomContent.Item it in items)
        {
            float u0 = it.rect.xMin / texSize.x, u1 = it.rect.xMax / texSize.x;
            float v0 = 1f - it.rect.yMax / texSize.y, v1 = 1f - it.rect.yMin / texSize.y;
            float x0 = 0.437f - u0 * 0.874f, x1 = 0.437f - u1 * 0.874f;
            float y0 = 0.015f + v0 * 0.509f, y1 = 0.015f + v1 * 0.509f;
            LocalItem(board, it, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, -0.008f), new Vector2(Mathf.Abs(x1 - x0), y1 - y0));
        }
    }

    // a board whose items are given in its own local meters (front = +Z)
    private static void ItemsLocal(Transform board, OperatorRoomContent.Item[] items, float z)
    {
        foreach (OperatorRoomContent.Item it in items)
            LocalItem(board, it, new Vector3(it.rect.center.x, it.rect.center.y, z), it.rect.size);
    }

    private static void LocalItem(Transform board, OperatorRoomContent.Item it, Vector3 localCenter, Vector2 localSize)
    {
        var t = Child(board, Gen + "Item " + it.tEn);
        t.localPosition = localCenter;
        t.localRotation = Quaternion.Euler(0f, 180f, 0f);   // its forward looks into the board (the board's front is +Z)
        MakeItem(t, it, new Vector3(localSize.x, localSize.y, 0.01f));
    }

    // a board of unknown layout: on its face (u from the left, v from the top), in world space
    private static void ItemsOnFace(Transform board, OperatorRoomContent.Item[] items, Vector3 face, Vector3 n, float w, float h)
    {
        Vector3 right = Vector3.Cross(Vector3.up, -n).normalized;
        foreach (OperatorRoomContent.Item it in items)
        {
            Vector3 pos = face + n * 0.012f + right * ((it.rect.center.x - 0.5f) * w) + Vector3.up * ((0.5f - it.rect.center.y) * h);
            var t = Child(board, Gen + "Item " + it.tEn);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(-n, Vector3.up));
            float s = Mathf.Max(0.0001f, t.lossyScale.x);
            MakeItem(t, it, new Vector3(it.rect.width * w / s, it.rect.height * h / s, 0.02f / s));
        }
    }

    private static void MakeItem(Transform t, OperatorRoomContent.Item it, Vector3 size)
    {
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        var item = t.gameObject.AddComponent<EvidenceItem>();
        item.EditorSet(it.tEn, it.tPt, it.en, it.pt, it.thEn, it.thPt, "", "evidence_" + it.tEn.ToLowerInvariant().Replace(' ', '_'));
        Vector3 world = Vector3.Scale(size, t.lossyScale);
        float zoom = Mathf.Clamp(Mathf.Max(world.y * 0.5f / Tan33, world.x * 0.5f / (Tan33 * 1.5f)) * 1.35f, 0.3f, 2.2f);
        var so = new SerializedObject(item);
        so.FindProperty(EvidenceItem.ZoomDistanceField).floatValue = zoom;
        so.ApplyModifiedPropertiesWithoutUndo();

        // the frame drawn while the mouse is over it
        var line = new GameObject("Frame").AddComponent<LineRenderer>();
        line.transform.SetParent(t, false);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        float x = size.x * 0.5f, y = size.y * 0.5f, z = -size.z * 0.5f;
        line.SetPositions(new[] { new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z) });
        line.widthMultiplier = 0.012f;
        line.sharedMaterial = highlightMat;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        HelenaSetupMenu.SetReference(item, EvidenceItem.HighlightField, line);
    }

    // ================================================================== things that only have something to say
    private static void SetUpFlavors()
    {
        Flavor(Find("SM_OpFilingCabinet"), "Filing cabinet", "Arquivo",
            new[] { "Locked. No key anywhere.", "The labels were torn off.", "Something rolls inside when I shake it." },
            new[] { "Trancado. Nenhuma chave em lugar nenhum.", "Arrancaram as etiquetas.", "Tem alguma coisa rolando lá dentro quando eu balanço." }, OperatorSounds.Kind.Drawer, OperatorInteractable.HandMotion.Grab);
        Flavor(Find("SM_OpTrashCan"), "Trash can", "Lixeira",
            new[] { "Empty. Someone cleaned up before I got here.", "...one coffee cup. Still warm." },
            new[] { "Vazia. Alguém limpou antes de eu chegar.", "...um copo de café. Ainda morno." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.None);
        Flavor(Find("SM_OpPapers"), "Papers", "Papéis",
            new[] { "Blank forms. All of them stamped by the government.", "A shift schedule. My name is on every night." },
            new[] { "Formulários em branco. Todos com o carimbo do governo.", "Uma escala de turnos. Meu nome em todas as noites." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.Grab);
        foreach (GameObject plant in FindAllStarting("SM_OpPlant"))
            Flavor(plant, "Plant", "Planta", new[] { "Plastic.", "Even the plant is fake." }, new[] { "De plástico.", "Até a planta é falsa." },
                OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.None);
    }

    // OperatorFlavor (thoughts) on an object: added once, the texts are left alone after that
    private static void Flavor(GameObject go, string en, string pt, string[] linesEn, string[] linesPt, OperatorSounds.Kind sound, OperatorInteractable.HandMotion hand)
    {
        if (go == null) return;
        EnsureCollider(go);
        if (go.GetComponent<OperatorInteractable>() != null) return;
        go.AddComponent<OperatorFlavor>().EditorSet(en, pt, linesEn, linesPt, sound, hand, true);
    }

    // ================================================================== ceiling lamps (made once)
    private static void SetUpLights(float ceiling)
    {
        // the diffusers glow (every time: the material is remade with the folder)
        Material diffuser = AssetDatabase.LoadAssetAtPath<Material>($"{PropFolder}/Materials/M_OpRoom_LampDiffuser.mat");
        if (diffuser != null)
            OperatorRoomBuilder.Glow(diffuser, AssetDatabase.LoadAssetAtPath<Texture2D>($"{PropFolder}/T_OpRoom_LampDiffuser.png"), new Color(0.9f, 0.96f, 1f) * 1.6f);
        foreach (GameObject existing in scene.GetRootGameObjects())
            if (existing.name == LightsRoot) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/SM_OpRoom_CeilingLamp.fbx");
        var root = new GameObject(LightsRoot).transform;
        int i = 0;
        foreach (float z in new[] { -2.8f, 0f, 2.8f })
            foreach (float x in new[] { -2.5f, 2.5f })
            {
                Vector3 at = new Vector3(roomCenter.x + x, ceiling - 0.002f, roomCenter.z + z);
                Transform lamp;
                if (prefab != null)
                {
                    lamp = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, root)).transform;
                    lamp.position = at;
                }
                else lamp = Child(root, "Lamp");
                lamp.name = "CeilingLamp" + i;
                Light l = PointLight(lamp, "Light", at - Vector3.up * 0.28f, new Color(0.84f, 0.92f, 1f), 1.5f, 6f, i == 2 || i == 3 ? LightShadows.Soft : LightShadows.None);
                if (i == 5)   // the one that stutters (and buzzes)
                {
                    var flicker = lamp.gameObject.AddComponent<OperatorLightFlicker>();
                    HelenaSetupMenu.SetReference(flicker, OperatorLightFlicker.LampField, l);
                    HelenaSetupMenu.SetReference(flicker, OperatorLightFlicker.TubeField, lamp.GetComponentInChildren<Renderer>());
                    var buzz = lamp.gameObject.AddComponent<AudioSource>();
                    buzz.clip = Clip("Lamp_Buzz");
                    buzz.loop = true;
                    buzz.playOnAwake = true;
                    buzz.spatialBlend = 1f;
                    buzz.volume = 0.12f;
                    buzz.rolloffMode = AudioRolloffMode.Linear;
                    buzz.minDistance = 0.5f;
                    buzz.maxDistance = 5f;
                }
                i++;
            }
    }

    // ================================================================== new props (made once; re-wired every time)
    private static PinballMachine SetUpExtras()
    {
        Transform root = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == ExtrasRoot) root = r.transform;
        if (root != null)
        {
            PinballMachine existing = root.GetComponentInChildren<PinballMachine>(true);
            if (existing != null)
            {
                Wire(existing, PinballMachine.StationField, station);
                Wire(existing, PinballMachine.StoryField, story);
            }
            return existing;
        }
        root = new GameObject(ExtrasRoot).transform;

        // under the window: a cot, a side table with the radio
        Flavor(Prop(root, "SM_OpRoom_Cot", At(-2.4f, 4.42f), 180f), "Cot", "Catre",
            new[] { "Someone slept here. Not me.", "The blanket smells like smoke.", "...the pillow is still warm." },
            new[] { "Alguém dormiu aqui. Não fui eu.", "O cobertor tem cheiro de fumaça.", "...o travesseiro ainda está morno." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.None);
        Prop(root, "SM_OpRoom_SideTable", At(-0.95f, 4.55f), 180f, collider: true);
        GameObject radio = Prop(root, "SM_OpRoom_Radio", At(-0.95f, 4.6f) + Vector3.up * 0.6f, 195f);
        if (radio != null)
        {
            OperatorRoomBuilder.FitCollider(radio);
            var r = radio.AddComponent<RoomRadio>();
            AudioSource a = Source(radio.transform, "Speaker", 1f, 0.45f, 9f);
            Wire(r, RoomRadio.AudioField, a);
            Wire(r, RoomRadio.StaticClipField, Clip("Radio_Static"));
            Wire(r, RoomRadio.StationClipField, Clip("Radio_Station"));
        }

        // north wall: lockers, the clock over them
        Flavor(Prop(root, "SM_OpRoom_Lockers", At(1.3f, 4.6f), 180f), "Lockers", "Armários",
            new[] { "Locked. All three.", "Something knocked in there? ...No. The air.", "A name scratched off the middle one." },
            new[] { "Trancados. Os três.", "Bateu alguma coisa lá dentro? ...Não. Foi o ar.", "Um nome raspado no do meio." }, OperatorSounds.Kind.Locker, OperatorInteractable.HandMotion.Grab);
        BuildClock(root, At(1.3f, 4.84f) + Vector3.up * 2.3f);

        // west wall (north part): shelf with binders
        Flavor(Prop(root, "SM_OpRoom_Shelf", At(-4.62f, 3.55f), 90f), "Shelf", "Estante",
            new[] { "Site reports. Dates from two years ago... until next week?", "Binders. All of them about the same building." },
            new[] { "Relatórios de obra. Datas de dois anos atrás... até semana que vem?", "Pastas. Todas sobre o mesmo prédio." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.Grab);

        // east wall (south part): coffee machine, mini fridge
        BuildCoffee(root, At(4.42f, -1.6f), -90f);
        Flavor(Prop(root, "SM_OpRoom_MiniFridge", At(4.55f, -2.9f), -90f), "Mini fridge", "Frigobar",
            new[] { "Empty. Just a bottle of water and a light that flickers.", "Someone left a note: \"eat something\"." },
            new[] { "Vazio. Só uma garrafa de água e uma luz que pisca.", "Alguém deixou um bilhete: \"come alguma coisa\"." }, OperatorSounds.Kind.Drawer, OperatorInteractable.HandMotion.Grab);

        // east wall (middle): the pinball
        PinballMachine pinball = PinballBuilder.Build(root, lit, At(4.1f, 0.55f), 90f, station, story);
        return pinball;
    }

    // a floor point, from the middle of the room
    private static Vector3 At(float x, float z) => new Vector3(roomCenter.x + x, 0f, roomCenter.z + z);

    private static GameObject Prop(Transform parent, string model, Vector3 pos, float yaw, bool collider = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{model}.fbx");
        if (prefab == null) { missing.Add(model + ".fbx"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (collider) OperatorRoomBuilder.FitCollider(go);
        return go;
    }

    // the wall clock: its hands turn around pivots whose Z looks into the wall
    private static void BuildClock(Transform parent, Vector3 pos)
    {
        GameObject clock = Prop(parent, "SM_OpRoom_WallClock", pos, 180f);
        if (clock == null) return;
        Material dark = AssetDatabase.LoadAssetAtPath<Material>($"{PropFolder}/Materials/M_OpRoom_Dark.mat");
        Material red = Mat("M_OpRoom_Red", "T_OpRoom_Red", 0.3f);
        Transform h = Hand(clock.transform, "HourPivot", 0.085f, 0.012f, 0.002f, dark);
        Transform m = Hand(clock.transform, "MinutePivot", 0.125f, 0.008f, 0.005f, dark);
        Transform s = Hand(clock.transform, "SecondPivot", 0.135f, 0.003f, 0.008f, red);
        var wc = clock.AddComponent<WallClock>();
        Wire(wc, WallClock.HourField, h);
        Wire(wc, WallClock.MinuteField, m);
        Wire(wc, WallClock.SecondField, s);
        Wire(wc, WallClock.AudioField, Source(clock.transform, "Tick", 1f, 0.6f, 5f));
    }

    private static Transform Hand(Transform clock, string name, float length, float width, float offset, Material m)
    {
        var pivot = Child(clock, name);
        pivot.localPosition = new Vector3(0f, 0f, 0.048f + offset);   // in front of the face (the model's front is +Z)
        pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);        // Z into the wall
        var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(hand.GetComponent<Collider>());
        hand.name = "Hand";
        hand.transform.SetParent(pivot, false);
        hand.transform.localPosition = new Vector3(0f, length * 0.4f, 0f);
        hand.transform.localScale = new Vector3(width, length, 0.003f);
        hand.GetComponent<Renderer>().sharedMaterial = m;
        hand.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return pivot;
    }

    // the coffee machine of the package (as in the location rooms): brews a cup, he drinks it
    private static void BuildCoffee(Transform parent, Vector3 pos, float yaw)
    {
        GameObject coffee = OperatorRoomBuilder.Model(parent, "SM_OpCoffeeMachine", Vector3.zero, 0f);
        if (coffee == null) { missing.Add("SM_OpCoffeeMachine.fbx"); return; }
        coffee.transform.position = pos;
        coffee.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        OperatorRoomBuilder.FitCollider(coffee);
        Material dark = HelenaSetupMenu.MakeMaterial($"{OpArt}/Materials/M_Op_Dark.mat", null, 0.4f, false, lit);
        var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(tray.GetComponent<Collider>());
        tray.name = "Tray";
        tray.transform.SetParent(coffee.transform, false);
        tray.transform.localPosition = new Vector3(-0.18f, 0.115f, 0.45f);
        tray.transform.localScale = new Vector3(0.2f, 0.012f, 0.1f);
        tray.GetComponent<Renderer>().sharedMaterial = dark;
        GameObject cup = OperatorRoomBuilder.Model(coffee.transform, "SM_OpCoffeeCup", new Vector3(-0.18f, 0.122f, 0.45f), 0f);
        if (cup != null) cup.name = "CupOnTray";
        Light glow = PointLight(coffee.transform, "CupLight", coffee.transform.TransformPoint(new Vector3(-0.18f, 0.3f, 0.55f)), new Color(1f, 0.72f, 0.4f), 1.4f, 1.1f);
        var audio = coffee.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.8f;
        var machine = coffee.AddComponent<CoffeeMachine>();
        Wire(machine, CoffeeMachine.CupOnTrayField, cup);
        Wire(machine, CoffeeMachine.GlowField, glow);
        Wire(machine, CoffeeMachine.AudioField, audio);
    }
}
