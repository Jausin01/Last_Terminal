using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The operator's own room, built into every location scene (far from the level, at RoomOrigin): the old office
// (SM_OperatorRoom) with his desk and PC, a coffee machine, the ARCANE QUEST pinball (PinballBuilder), cork boards,
// a locked door, junk to look at; him in first person (FirstPersonPlayer + the FP arms); his screen UI (dot, notes,
// the PC desktop with his files, the "close the program?" dialog); and the OperatorStation that switches between
// walking around, the desktop, the camera program and the pinball.
// Art: Assets/Art/Props/Operator (room, props, the coffee machine), Assets/Art/FirstPerson (arms) - materials and
// the arms' animator made by menu 3 (PrepareAssets). His files: Assets/Data/Operator/Files (created once, edit freely).
internal static class OperatorRoomBuilder
{
    // Art, arms, data and file paths.
    internal const string ArtFolder = "Assets/Art/Props/Operator";
    internal const string ArmsFolder = "Assets/Art/FirstPerson";
    private const string ArmsModelPath = ArmsFolder + "/SM_FPArms.fbx";
    private const string ArmsControllerPath = ArmsFolder + "/FPArms.controller";
    private const string DataFolder = "Assets/Data/Operator";
    private const string FilesFolder = DataFolder + "/Files";
    private const float ArmsEye = 1.7432f;   // the arms' camera bone height in the model
    // Where the office is built (far from the level); materials that need alpha clip.
    private static readonly Vector3 RoomOrigin = new Vector3(200f, 0f, 200f);
    private static readonly string[] ClipMaterials = { "M_Op_Plant_01", "M_Op_TrashCan_00" };

    // URP Lit property ids.
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // what the room needs from the rest of the scene (BuildLocation)
    internal class Rig
    {
        // Scene parts the room is wired to: the CCTV view camera, listener and canvas, the behaviours switched with the camera program, the camera system, her rest / transform, targets, encounter, voice, session, settings and story.
        public Camera ViewCamera;
        public AudioListener ViewListener;
        public Canvas OperatorCanvas;
        public readonly List<Behaviour> OperatorBehaviours = new List<Behaviour>();
        public SecurityCameraSystem CameraSystem;
        public PartnerRest Rest;
        public Transform Partner;
        public CommandTargets Targets;
        public CombatEncounter Encounter;
        public PartnerVoice Voice;
        public GameSession Session;
        public CameraSettings Settings;
        public StoryProgress Story;
    }

    // ================================================================== assets (menu 3)
    internal static void PrepareAssets(Shader lit)
    {
        RemapFolder(ArtFolder, lit);
        RemapFolder(ArmsFolder, lit);
        EnsureArmsController();
        EnsureFiles();
        AssetDatabase.SaveAssets();
    }

    // every model of the folder: its materials M_x get T_x.png (made under Materials/, remapped on the model)
    internal static void RemapFolder(string folder, Shader lit)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[Operator] {folder} not found: copy the Art folder of the package into Assets.");
            return;
        }
        EditorAssets.EnsureFolder(folder, "Materials");
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;
            var names = new HashSet<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material) names.Add(o.name);
            foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            bool changed = false;
            foreach (string n in names)
            {
                if (!n.StartsWith("M_")) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/T_{n.Substring(2)}.png");
                Material m = EditorAssets.MakeMaterial($"{folder}/Materials/{n}.mat", tex, 0.15f, System.Array.IndexOf(ClipMaterials, n) >= 0, lit);
                if (n == "M_Op_CoffeeMachine") Glow(m, tex, Color.white * 0.45f);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
                changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
    }

    // Makes a material glow (emission map + color).
    internal static void Glow(Material m, Texture2D map, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor(EmissionColorId, c);
        if (map != null) m.SetTexture(EmissionMapId, map);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
    }

    // a take of the arms file by its name (exact, or ending with it: some exports prefix the takes, "Armature|relax")
    internal static AnimationClip ArmsClip(string clipName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ArmsModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        return clips.FirstOrDefault(c => c.name == clipName) ?? clips.FirstOrDefault(c => c.name.EndsWith(clipName));
    }

    // relax (loop) <- grab.R / push.R (played from code, back to relax at the end)
    private static AnimatorController EnsureArmsController()
    {
        AnimationClip relax = ArmsClip(FirstPersonPlayer.RelaxClip);
        AnimationClip grab = ArmsClip(FirstPersonPlayer.GrabClip);
        AnimationClip push = ArmsClip(FirstPersonPlayer.PushClip);
        if (relax == null || grab == null || push == null)
        {
            Debug.LogWarning($"[Operator] FP arms clips not found in {ArmsModelPath} (relax, grab.R, push.R): the arms won't move.");
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(ArmsControllerPath);
        }
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArmsControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ArmsControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        AnimatorState idle = sm.AddState(FirstPersonPlayer.RelaxState, new Vector3(300f, 100f, 0f));
        idle.motion = relax;
        sm.defaultState = idle;
        int k = 0;
        foreach (var (stateName, clip) in new[] { (FirstPersonPlayer.GrabState, grab), (FirstPersonPlayer.PushState, push) })
        {
            AnimatorState s = sm.AddState(stateName, new Vector3(550f, 40f + 120f * k++, 0f));
            s.motion = clip;
            AnimatorStateTransition back = s.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.15f;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ================================================================== his files (created once, never overwritten)
    private static readonly (string id, string fileEn, string filePt, string titleEn, string titlePt, string flag, string en, string pt)[] Files =
    {
        ("01_readme", "README.txt", "LEIA-ME.txt", "Operator instructions", "Instruções do operador", "",
         "OPERATOR TERMINAL 07 - STANDING INSTRUCTIONS\n\n1. You are the only link between the subject and the outside. Keep the line open.\n2. The subject can hear you. Speak clearly. Do not panic her.\n3. Cameras: Q / E. Map: TAB. Talk: ENTER.\n4. Never close CCTV.EXE while the subject is exposed. Close it only when she is in a SAFE ZONE (the save rooms).\n5. The door of this room is locked for your own protection.\n6. Coffee is free. Please do not ask for anything else.\n\n- Facility Management",
         "TERMINAL DO OPERADOR 07 - INSTRUÇÕES PERMANENTES\n\n1. Você é a única ligação entre a pessoa monitorada e o lado de fora. Mantenha a linha aberta.\n2. Ela consegue ouvir você. Fale com clareza. Não a deixe em pânico.\n3. Câmeras: Q / E. Mapa: TAB. Falar: ENTER.\n4. Nunca feche o CCTV.EXE com ela exposta. Feche apenas quando ela estiver numa ZONA SEGURA (as salas de salvar).\n5. A porta desta sala está trancada para a sua própria proteção.\n6. O café é de graça. Por favor, não peça mais nada.\n\n- Administração das Instalações"),
        ("02_shift_log", "shift_07.log", "turno_07.log", "System log", "Registro do sistema", "",
         "[23:58] Terminal 07 powered on.\n[23:58] Operator badge: ACCEPTED.\n[23:59] Camera grid: 9/12 online. 3 cameras not responding.\n[00:00] Subject located: PADDED CELL. Vital signs: stable. Name on file: HELENA.\n[00:01] Door 07 (operator room): LOCKED by remote command.\n[00:01] Remote command origin: UNKNOWN.\n[00:02] Note to the operator: do not try the door.",
         "[23:58] Terminal 07 ligado.\n[23:58] Crachá do operador: ACEITO.\n[23:59] Rede de câmeras: 9/12 online. 3 câmeras sem resposta.\n[00:00] Pessoa localizada: CELA ACOLCHOADA. Sinais vitais: estáveis. Nome na ficha: HELENA.\n[00:01] Porta 07 (sala do operador): TRANCADA por comando remoto.\n[00:01] Origem do comando: DESCONHECIDA.\n[00:02] Aviso ao operador: não tente a porta."),
        ("03_no_subject", "no_subject.eml", "sem_assunto.eml", "(no subject)", "(sem assunto)", StoryProgress.KeypadSolved,
         "From: operator06@terminal06\nTo: whoever is at terminal 07\n\nIf you're reading this, she got through the first door. Good. That means she trusts you.\n\nI sat where you're sitting. I had a coffee machine and a pinball too. They give you those so you stay.\n\nDon't let her wait too long in the dark. And don't believe everything the README says.\n\n- 06",
         "De: operador06@terminal06\nPara: quem estiver no terminal 07\n\nSe você está lendo isto, ela passou pela primeira porta. Bom. Quer dizer que ela confia em você.\n\nEu sentei onde você está sentado. Eu também tinha uma máquina de café e um fliperama. Eles dão isso pra você ficar.\n\nNão deixe ela esperando muito tempo no escuro. E não acredite em tudo que o LEIA-ME diz.\n\n- 06"),
        ("04_backup", "backup_restore.txt", "restauracao_backup.txt", "Backup restored", "Backup restaurado", StoryProgress.FirstSave,
         "SAVE SYSTEM - BACKUP RESTORED\n\nA previous session was found on this terminal.\nSession owner: [CORRUPTED]\nLast entry: 03:12 - \"she said my name. how does she know my name\"\nSession ended: the operator left the terminal. The subject was NOT in a safe zone.\nOutcome: [DATA REMOVED]",
         "SISTEMA DE SALVAMENTO - BACKUP RESTAURADO\n\nUma sessão anterior foi encontrada neste terminal.\nDono da sessão: [CORROMPIDO]\nÚltima entrada: 03:12 - \"ela disse meu nome. como ela sabe meu nome\"\nFim da sessão: o operador saiu do terminal. Ela NÃO estava numa zona segura.\nResultado: [DADOS REMOVIDOS]"),
        ("05_incident", "incident_report_04.txt", "relatorio_incidente_04.txt", "Incident report", "Relatório de incidente", StoryProgress.FirstKill,
         "INCIDENT REPORT 04 - CONTAINMENT\n\nHostile entities confirmed inside the facility. They do not respond to the lights.\nThe subject defended herself under the operator's guidance.\nRecommendation: keep the operator at the terminal at all times.\nNote: the entities seem to move toward the cameras that are switched on. Unconfirmed.",
         "RELATÓRIO DE INCIDENTE 04 - CONTENÇÃO\n\nEntidades hostis confirmadas dentro das instalações. Elas não reagem às luzes.\nA pessoa monitorada se defendeu com a orientação do operador.\nRecomendação: manter o operador no terminal o tempo todo.\nObservação: as entidades parecem ir na direção das câmeras ligadas. Não confirmado."),
        ("06_casino", "casino_floor.memo", "andar_cassino.memo", "Memo: casino floor", "Memorando: cassino", StoryProgress.VisitedPrefix + "Casino",
         "MEMO - CASINO FLOOR\n\nThe games still work. Nobody remembers switching them on.\nThe chips are real. The prize lockers open with coins.\nManagement reminds every operator: the house always wins. That is the point of a house.",
         "MEMORANDO - ANDAR DO CASSINO\n\nOs jogos ainda funcionam. Ninguém lembra de ter ligado.\nAs fichas são de verdade. Os armários de prêmios abrem com moedas.\nA administração lembra a todos os operadores: a casa sempre ganha. É pra isso que serve uma casa."),
        ("07_coffee", "coffee_machine.log", "maquina_cafe.log", "Maintenance: coffee machine", "Manutenção: máquina de café", "coffee_5",
         "MAINTENANCE LOG - COFFEE MACHINE (ROOM 07)\n\nCups served to operator 07: 5.\nCups served to operator 06: 212.\nOperator 06 didn't sleep either.\nRefill scheduled: never.",
         "REGISTRO DE MANUTENÇÃO - MÁQUINA DE CAFÉ (SALA 07)\n\nCopos servidos ao operador 07: 5.\nCopos servidos ao operador 06: 212.\nO operador 06 também não dormia.\nReabastecimento agendado: nunca."),
        ("08_arcane_quest", "arcane_quest.hi", "arcane_quest.rec", "High scores", "Recordes", PinballMachine.ScoreFlag,
         "ARCANE QUEST - HIGH SCORE TABLE (factory reset: never)\n\n1. ???   the score you just beat\n2. H.    \"for when you get bored of watching me\"\n3. 06\n\nThe second line was written by hand, on the sticker under the glass.",
         "ARCANE QUEST - TABELA DE RECORDES (reset de fábrica: nunca)\n\n1. ???   o recorde que você acabou de bater\n2. H.    \"pra quando você cansar de me vigiar\"\n3. 06\n\nA segunda linha foi escrita à mão, no adesivo debaixo do vidro."),
    };

    // His file assets (made once).
    private static List<OperatorFile> EnsureFiles()
    {
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "Operator");
        EditorAssets.EnsureFolder(DataFolder, "Files");
        for (int i = 0; i < Files.Length; i++)
        {
            var f = Files[i];
            string path = $"{FilesFolder}/{f.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatorFile>(path) != null) continue;
            var file = ScriptableObject.CreateInstance<OperatorFile>();
            file.EditorSet(f.fileEn, f.filePt, f.titleEn, f.titlePt, f.en, f.pt, f.flag, i * 10);
            AssetDatabase.CreateAsset(file, path);
        }
        // every file in the folder (yours too)
        return AssetDatabase.FindAssets("t:OperatorFile", new[] { FilesFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<OperatorFile>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(f => f != null).OrderBy(f => f.Order).ToList();
    }

    // ================================================================== his room as its own scene (OperatorLink)
    internal const string LinkPath = "Assets/Data/OperatorLink.asset";

    // the asset his room and the location scenes share (OperatorStation <-> OperatorLocation)
    internal static OperatorLink EnsureLink()
    {
        var link = AssetDatabase.LoadAssetAtPath<OperatorLink>(LinkPath);
        if (link != null) return link;
        EditorAssets.EnsureFolder("Assets", "Data");
        link = ScriptableObject.CreateInstance<OperatorLink>();
        AssetDatabase.CreateAsset(link, LinkPath);
        return link;
    }

    // a location scene without a room of its own: its camera program, ready to be taken by his room's station
    internal static OperatorLocation AddLocationLink(Scene scene, Rig rig)
    {
        var go = new GameObject("OperatorLocation");
        SceneManager.MoveGameObjectToScene(go, scene);
        var location = go.AddComponent<OperatorLocation>();
        var so = new SerializedObject(location);
        so.FindProperty(OperatorLocation.LinkField).objectReferenceValue = EnsureLink();
        so.FindProperty(OperatorLocation.ViewCameraField).objectReferenceValue = rig.ViewCamera;
        so.FindProperty(OperatorLocation.ViewListenerField).objectReferenceValue = rig.ViewListener;
        so.FindProperty(OperatorLocation.OperatorCanvasField).objectReferenceValue = rig.OperatorCanvas;
        so.FindProperty(OperatorLocation.CameraSystemField).objectReferenceValue = rig.CameraSystem;
        so.FindProperty(OperatorLocation.PartnerRestField).objectReferenceValue = rig.Rest;
        so.FindProperty(OperatorLocation.PartnerField).objectReferenceValue = rig.Partner;
        so.FindProperty(OperatorLocation.TargetsField).objectReferenceValue = rig.Targets;
        so.FindProperty(OperatorLocation.EncounterField).objectReferenceValue = rig.Encounter;
        so.FindProperty(OperatorLocation.VoiceField).objectReferenceValue = rig.Voice;
        so.FindProperty(OperatorLocation.EventSystemField).objectReferenceValue = CutsceneBuilder.First<EventSystem>(scene);
        so.FindProperty(OperatorLocation.StoryField).objectReferenceValue = rig.Story;
        SerializedProperty list = so.FindProperty(OperatorLocation.OperatorBehavioursField);
        List<Behaviour> behaviours = rig.OperatorBehaviours.Where(b => b != null).ToList();
        list.arraySize = behaviours.Count;
        for (int i = 0; i < behaviours.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return location;
    }

    // ================================================================== the room (every location scene)
    internal static OperatorStation Build(Shader lit, Rig rig)
    {
        var root = new GameObject("OperatorRoom");
        root.transform.position = RoomOrigin;
        Transform r = root.transform;

        // the old office, invisible walls (the junk tables along the side walls block him at table height)
        GameObject room = Model(r, "SM_OperatorRoom", Vector3.zero, 0f);
        if (room == null) Debug.LogWarning($"[Operator] {ArtFolder}/SM_OperatorRoom.fbx missing: the room is built without its walls (colliders only).");
        // the model's walls don't quite meet in two corners (north-west, north-east): filled
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>($"{ArtFolder}/Materials/M_Op_Room_13.mat");
        if (room != null && wallMat != null)
        {
            Plain(r, "CornerNW", new Vector3(-3.03f, 1.77f, 4.0f), new Vector3(0.26f, 3.6f, 0.26f), wallMat);
            Plain(r, "CornerNE", new Vector3(4.0f, 1.77f, 4.0f), new Vector3(0.26f, 3.6f, 0.26f), wallMat);
        }
        var colliders = new GameObject("Colliders").transform;
        colliders.SetParent(r, false);
        BoxOnly(colliders, "Floor", new Vector3(0.5f, -0.05f, 0.05f), new Vector3(7.4f, 0.1f, 8.4f));
        BoxOnly(colliders, "WallNorth", new Vector3(0.5f, 1.75f, 4.15f), new Vector3(7.4f, 3.5f, 0.2f));
        BoxOnly(colliders, "WallSouth", new Vector3(0.5f, 1.75f, -4.15f), new Vector3(7.4f, 3.5f, 0.2f));
        BoxOnly(colliders, "WallWest", new Vector3(-3.2f, 1.75f, 0f), new Vector3(0.2f, 3.5f, 8.4f));
        BoxOnly(colliders, "WallEast", new Vector3(4.15f, 1.75f, 0f), new Vector3(0.2f, 3.5f, 8.4f));
        BoxOnly(colliders, "JunkPile", new Vector3(1.35f, 0.3f, -3.6f), new Vector3(1.4f, 0.6f, 0.8f));
        Flavor(BoxOnly(colliders, "TablesWest", new Vector3(-2.55f, 0.625f, 0f), new Vector3(1.1f, 1.25f, 8.2f)), "Old monitors", "Monitores velhos",
            new[] { "None of them turn on.", "Burnt out. All of them.", "...this one is still warm." },
            new[] { "Nenhum liga.", "Queimados. Todos.", "...esse aqui ainda está morno." }, OperatorSounds.Kind.Click, OperatorInteractable.HandMotion.Push);
        Flavor(BoxOnly(colliders, "TablesEast", new Vector3(3.45f, 0.625f, 0f), new Vector3(1.0f, 1.25f, 8.2f)), "Old monitors", "Monitores velhos",
            new[] { "Dead screens. Dozens of them.", "Someone kept every monitor that ever broke here.", "...one of them shows my reflection a second late." },
            new[] { "Telas mortas. Dezenas.", "Alguém guardou todo monitor que quebrou aqui.", "...um deles mostra meu reflexo um segundo atrasado." }, OperatorSounds.Kind.Click, OperatorInteractable.HandMotion.Push);
        Flavor(BoxOnly(colliders, "WindowNorth", new Vector3(0.48f, 2.07f, 4.0f), new Vector3(3.0f, 2.0f, 0.05f)), "Window", "Janela",
            new[] { "The blinds don't open.", "Through the gaps: only black.", "No city lights. Nothing." },
            new[] { "As persianas não abrem.", "Pelas frestas: só escuridão.", "Nenhuma luz de cidade. Nada." }, OperatorSounds.Kind.Click, OperatorInteractable.HandMotion.None);
        Flavor(BoxOnly(colliders, "WindowSouth", new Vector3(0.48f, 2.07f, -4.0f), new Vector3(3.0f, 2.0f, 0.05f)), "Window", "Janela",
            new[] { "Blinds, nailed shut.", "I can hear wind. Or a fan.", "Black. Just black." },
            new[] { "Persianas pregadas.", "Dá pra ouvir vento. Ou um ventilador.", "Preto. Só preto." }, OperatorSounds.Kind.Click, OperatorInteractable.HandMotion.None);

        // the desk: PC (his station), chair, things on it
        Model(r, "SM_OpDesk", new Vector3(0.2f, 0f, 3.25f), 180f, collider: true);
        Model(r, "SM_OpChair", new Vector3(0.0f, 0f, 2.85f), 90f, collider: true);
        GameObject pc = Model(r, "SM_OperatorPC", new Vector3(0.05f, 0.748f, 3.62f), 180f);
        if (pc == null) pc = Placeholder(r, "PC", new Vector3(0.05f, 0.95f, 3.65f), new Vector3(0.5f, 0.4f, 0.1f), lit);
        Model(r, "SM_OpPapers", new Vector3(0.72f, 0.748f, 3.6f), 15f);
        Model(r, "SM_OpStapler", new Vector3(0.95f, 0.748f, 3.55f), 70f);
        Model(r, "SM_OpCoffeeCup", new Vector3(0.45f, 0.748f, 3.47f), 30f);
        Flavor(Model(r, "SM_OpPrinter", new Vector3(2.45f, 0f, -3.5f), 180f, collider: true), "Printer", "Impressora",
            new[] { "Out of paper.", "It prints one line: \"DON'T LEAVE HER ALONE\". Then nothing." },
            new[] { "Sem papel.", "Ela imprime uma linha: \"NÃO DEIXE ELA SOZINHA\". Depois, nada." }, OperatorSounds.Kind.Beep, OperatorInteractable.HandMotion.Push);
        Flavor(Model(r, "SM_OpFilingCabinet", new Vector3(1.75f, 0f, 3.76f), -90f, collider: true), "Filing cabinet", "Arquivo",
            new[] { "Locked.", "The labels were torn off." },
            new[] { "Trancado.", "As etiquetas foram arrancadas." }, OperatorSounds.Kind.Rattle, OperatorInteractable.HandMotion.Grab);
        Model(r, "SM_OpBinders", new Vector3(1.75f, 1.134f, 3.76f), -80f);
        Flavor(Model(r, "SM_OpPlant", new Vector3(2.3f, 0f, 3.7f), 20f, collider: true), "Plant", "Planta",
            new[] { "Plastic.", "Even the plant is fake." },
            new[] { "De plástico.", "Até a planta é falsa." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.None);
        Flavor(Model(r, "SM_OpTrashCan", new Vector3(1.65f, 0f, 2.55f), 0f, collider: true), "Trash", "Lixo",
            new[] { "Coffee cups. A lot of them.", "A crumpled note: \"06 was here\"." },
            new[] { "Copos de café. Muitos.", "Um bilhete amassado: \"06 esteve aqui\"." }, OperatorSounds.Kind.Paper, OperatorInteractable.HandMotion.None);
        Flavor(Model(r, "SM_OpWaterCooler", new Vector3(-0.2f, 0f, -3.72f), 0f, collider: true), "Water cooler", "Bebedouro",
            new[] { "Warm water. Tastes like plastic.", "The jug is almost empty.", "...glug." },
            new[] { "Água morna. Gosto de plástico.", "O galão está quase vazio.", "...glub." }, OperatorSounds.Kind.Glug, OperatorInteractable.HandMotion.Push);

        // the PC: screen (lock screen glow), used = sit down
        Material screenMat = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_Screen.mat",
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Op_Screen.png"), 0.8f, false, lit);
        Glow(screenMat, AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Op_Screen.png"), Color.white * 0.9f);
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Screen";
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.transform.SetParent(pc.transform, false);
        screen.transform.localPosition = new Vector3(0.048f, 0.257f, -0.071f);
        screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        screen.transform.localScale = new Vector3(0.42f, 0.24f, 1f);
        screen.GetComponent<Renderer>().sharedMaterial = screenMat;
        FitCollider(pc);
        var pcUse = pc.AddComponent<OperatorPc>();
        EditorSerialized.SetString(pcUse, OperatorInteractable.LabelEnField, "Computer");
        EditorSerialized.SetString(pcUse, OperatorInteractable.LabelPtField, "Computador");

        // the coffee machine (south wall)
        GameObject coffee = Model(r, "SM_OpCoffeeMachine", new Vector3(-1.0f, 0f, -3.5f), 0f);
        if (coffee == null) coffee = Placeholder(r, "CoffeeMachine", new Vector3(-1.0f, 0.915f, -3.5f), new Vector3(1.0f, 1.83f, 0.8f), lit);
        FitCollider(coffee);
        Material dark = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_Dark.mat", null, 0.4f, false, lit);
        Plain(coffee.transform, "Tray", new Vector3(-0.18f, 0.115f, 0.45f), new Vector3(0.2f, 0.012f, 0.1f), dark);
        GameObject cupOnTray = Model(coffee.transform, "SM_OpCoffeeCup", new Vector3(-0.18f, 0.122f, 0.45f), 0f);
        if (cupOnTray == null) cupOnTray = Plain(coffee.transform, "Cup", new Vector3(-0.18f, 0.167f, 0.45f), new Vector3(0.08f, 0.09f, 0.08f), dark);
        cupOnTray.name = "CupOnTray";
        Light coffeeGlow = PointLight(coffee.transform, "CupLight", new Vector3(-0.18f, 0.3f, 0.55f), new Color(1f, 0.72f, 0.4f), 1.4f, 1.1f);
        var coffeeAudio = coffee.AddComponent<AudioSource>();
        coffeeAudio.playOnAwake = false;
        coffeeAudio.spatialBlend = 0.8f;
        var machine = coffee.AddComponent<CoffeeMachine>();
        EditorSerialized.SetReference(machine, CoffeeMachine.CupOnTrayField, cupOnTray);
        EditorSerialized.SetReference(machine, CoffeeMachine.GlowField, coffeeGlow);
        EditorSerialized.SetReference(machine, CoffeeMachine.AudioField, coffeeAudio);

        // cork boards (east wall, over the junk)
        Board(r, "SM_OpCorkBoardA", new Vector3(3.88f, 1.6f, 1.1f), "WHAT I KNOW", "O QUE EU SEI",
            "- Her name is Helena. She doesn't remember how she got into the cell.\n- I don't remember how I got into this room.\n- Somebody locked the door at 00:01. Not me.\n- Save rooms are safe. Everywhere else is not.\n- The previous operator was \"06\". Where is 06?\n- She trusts my voice. Why?",
            "- O nome dela é Helena. Ela não lembra como foi parar na cela.\n- Eu não lembro como vim parar nesta sala.\n- Alguém trancou a porta às 00:01. Não fui eu.\n- As salas de salvar são seguras. O resto não.\n- O operador anterior era o \"06\". Cadê o 06?\n- Ela confia na minha voz. Por quê?");
        Board(r, "SM_OpCorkBoardB", new Vector3(3.88f, 1.6f, -1.3f), "THE BUILDING", "O PRÉDIO",
            "- Ground floor: the cell, the lobby, an office, storage, a training room.\n- The office elevator goes up to a casino. A CASINO.\n- The elevators need colored keys.\n- Cameras I can't reach: 3. What's in those rooms?\n- The photos on this board: I didn't pin them here.",
            "- Térreo: a cela, a recepção, um escritório, o depósito, uma sala de treino.\n- O elevador do escritório sobe pra um cassino. Um CASSINO.\n- Os elevadores precisam de chaves coloridas.\n- Câmeras que eu não alcanço: 3. O que tem nessas salas?\n- As fotos deste quadro: não fui eu que preguei.");

        // the locked door (north wall, left of the window)
        BuildDoor(r, new Vector3(-1.53f, 0f, 4.0f), lit);

        // lights: two old fluorescent tubes (one stutters), the desk, the monitor
        Material tube = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_Tube.mat", null, 0.5f, false, lit);
        tube.SetColor(BaseColorId, new Color(0.9f, 0.95f, 1f));
        Glow(tube, null, new Color(0.85f, 0.95f, 1f) * 1.5f);
        Tube(r, "TubeA", new Vector3(0.5f, 3.18f, 1.6f), tube, 1.5f, false);
        Tube(r, "TubeB", new Vector3(0.5f, 3.18f, -1.8f), tube, 1.3f, true);
        PointLight(r, "DeskLight", new Vector3(0.2f, 1.8f, 3.0f), new Color(1f, 0.8f, 0.55f), 0.9f, 3f);
        PointLight(r, "MonitorGlow", new Vector3(0.0f, 1.0f, 3.45f), new Color(0.5f, 1f, 0.6f), 0.35f, 1.3f);
        PointLight(r, "CoffeeFront", new Vector3(-1.0f, 1.2f, -2.9f), new Color(1f, 0.9f, 0.75f), 0.5f, 2.2f);

        // him: body, eyes, arms, the cup in his hand
        var body = new GameObject("Operator");
        body.transform.SetParent(r, false);
        body.transform.localPosition = new Vector3(0.6f, 0f, 1.2f);
        body.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
        var cc = body.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.25f;
        cc.center = new Vector3(0f, 0.875f, 0f);
        cc.stepOffset = 0.3f;
        var voiceless = body.AddComponent<AudioSource>();   // his sounds (2D)
        voiceless.playOnAwake = false;
        voiceless.spatialBlend = 0f;
        var eyes = new GameObject("Eyes");
        eyes.transform.SetParent(body.transform, false);
        eyes.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        var roomCam = eyes.AddComponent<Camera>();
        roomCam.fieldOfView = 66f;
        roomCam.nearClipPlane = 0.02f;
        roomCam.farClipPlane = 60f;
        roomCam.enabled = false;
        roomCam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var roomListener = eyes.AddComponent<AudioListener>();
        roomListener.enabled = false;
        Animator arms = BuildArms(eyes.transform, out GameObject handCup);

        // his screen: HUD, notes, desktop, dialog
        BuildUi(rig, out OperatorRoomHud hud, out OperatorDesktop desktop, out DialogRefs dialog);
        var player = body.AddComponent<FirstPersonPlayer>();
        EditorSerialized.SetReference(player, FirstPersonPlayer.ControllerField, cc);
        EditorSerialized.SetReference(player, FirstPersonPlayer.ViewField, roomCam);
        EditorSerialized.SetReference(player, FirstPersonPlayer.ArmsField, arms);
        EditorSerialized.SetReference(player, FirstPersonPlayer.CupField, handCup);
        EditorSerialized.SetReference(player, FirstPersonPlayer.HudField, hud);
        EditorSerialized.SetReference(player, FirstPersonPlayer.SettingsField, rig.Settings);
        EditorSerialized.SetReference(player, FirstPersonPlayer.StoryField, rig.Story);
        EditorSerialized.SetReference(player, FirstPersonPlayer.AudioField, voiceless);
        EditorSerialized.SetReference(desktop, OperatorDesktop.AudioField, voiceless);

        // the station: views, the switch
        var stationGo = new GameObject("OperatorStation");
        stationGo.transform.SetParent(r, false);
        Transform seat = ViewPoint(r, "SeatView", new Vector3(0.002f, 1.2f, 3.0f), new Vector3(0.002f, 1.0f, 3.69f));
        Transform screenView = ViewPoint(r, "ScreenView", new Vector3(0.002f, 1.005f, 3.511f), new Vector3(0.002f, 1.005f, 3.69f));
        var station = stationGo.AddComponent<OperatorStation>();

        PinballMachine pinball = PinballBuilder.Build(r, lit, new Vector3(-1.35f, 0f, -0.6f), -90f, station, rig.Story);
        EditorSerialized.SetReference(pcUse, OperatorPc.StationField, station);

        var so = new SerializedObject(station);
        so.FindProperty(OperatorStation.PlayerField).objectReferenceValue = player;
        so.FindProperty(OperatorStation.HudField).objectReferenceValue = hud;
        so.FindProperty(OperatorStation.DesktopField).objectReferenceValue = desktop;
        so.FindProperty(OperatorStation.RoomCameraField).objectReferenceValue = roomCam;
        so.FindProperty(OperatorStation.RoomListenerField).objectReferenceValue = roomListener;
        so.FindProperty(OperatorStation.SeatViewField).objectReferenceValue = seat;
        so.FindProperty(OperatorStation.ScreenViewField).objectReferenceValue = screenView;
        so.FindProperty(OperatorStation.PinballField).objectReferenceValue = pinball;
        so.FindProperty(OperatorStation.ViewCameraField).objectReferenceValue = rig.ViewCamera;
        so.FindProperty(OperatorStation.ViewListenerField).objectReferenceValue = rig.ViewListener;
        so.FindProperty(OperatorStation.OperatorCanvasField).objectReferenceValue = rig.OperatorCanvas;
        so.FindProperty(OperatorStation.CameraSystemField).objectReferenceValue = rig.CameraSystem;
        so.FindProperty(OperatorStation.PartnerRestField).objectReferenceValue = rig.Rest;
        so.FindProperty(OperatorStation.PartnerField).objectReferenceValue = rig.Partner;
        so.FindProperty(OperatorStation.TargetsField).objectReferenceValue = rig.Targets;
        so.FindProperty(OperatorStation.EncounterField).objectReferenceValue = rig.Encounter;
        so.FindProperty(OperatorStation.VoiceField).objectReferenceValue = rig.Voice;
        so.FindProperty(OperatorStation.SessionField).objectReferenceValue = rig.Session;
        so.FindProperty(OperatorStation.StoryField).objectReferenceValue = rig.Story;
        so.FindProperty(OperatorStation.SettingsField).objectReferenceValue = rig.Settings;
        so.FindProperty(OperatorStation.DialogField).objectReferenceValue = dialog.Group;
        so.FindProperty(OperatorStation.DialogTitleField).objectReferenceValue = dialog.Title;
        so.FindProperty(OperatorStation.DialogTextField).objectReferenceValue = dialog.Text;
        so.FindProperty(OperatorStation.DialogMessageField).objectReferenceValue = dialog.Message;
        so.FindProperty(OperatorStation.CloseButtonField).objectReferenceValue = dialog.Close;
        so.FindProperty(OperatorStation.SettingsButtonField).objectReferenceValue = dialog.Settings;
        so.FindProperty(OperatorStation.CancelButtonField).objectReferenceValue = dialog.Cancel;
        so.FindProperty(OperatorStation.CloseLabelField).objectReferenceValue = dialog.CloseLabel;
        so.FindProperty(OperatorStation.SettingsLabelField).objectReferenceValue = dialog.SettingsLabel;
        so.FindProperty(OperatorStation.CancelLabelField).objectReferenceValue = dialog.CancelLabel;
        SerializedProperty list = so.FindProperty(OperatorStation.OperatorBehavioursField);
        List<Behaviour> behaviours = rig.OperatorBehaviours.Where(b => b != null).ToList();
        list.arraySize = behaviours.Count;
        for (int i = 0; i < behaviours.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return station;
    }

    // ------------------------------------------------------------------ the arms (child of his eyes) + the cup in the right hand
    internal static Animator BuildArms(Transform eyes, out GameObject handCup, RuntimeAnimatorController controller = null)
    {
        handCup = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModelPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Operator] {ArmsModelPath} missing: no first person arms.");
            handCup = Model(eyes, "SM_OpCoffeeCup", new Vector3(0.18f, -0.25f, 0.42f), 0f);   // the cup still shows
            return null;
        }
        var arms = (GameObject)PrefabUtility.InstantiatePrefab(prefab, eyes);
        arms.name = "Arms";
        arms.transform.localPosition = new Vector3(0f, -ArmsEye, 0f);
        Animator animator = arms.GetComponent<Animator>();
        if (animator == null) animator = arms.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller != null ? controller : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ArmsControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Renderer rend in arms.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;

        // the cup sits in the fist of the grab pose (the hand comes up with it when he drinks)
        Transform hand = EditorScene.FindDeep(arms.transform, "hand.R");
        Transform middle = EditorScene.FindDeep(arms.transform, "f_middle.01.R");
        AnimationClip grab = ArmsClip(FirstPersonPlayer.GrabClip);
        AnimationClip relax = ArmsClip(FirstPersonPlayer.RelaxClip);
        if (hand != null && middle != null && grab != null)
        {
            grab.SampleAnimation(arms, grab.length * 0.45f);
            Vector3 grip = Vector3.Lerp(hand.position, middle.position, 0.7f);
            handCup = Model(eyes, "SM_OpCoffeeCup", Vector3.zero, 0f);
            if (handCup != null)
            {
                handCup.name = "CupInHand";
                handCup.transform.position = grip - Vector3.up * 0.04f;
                handCup.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(eyes.forward, Vector3.up), Vector3.up);
                handCup.transform.SetParent(hand, true);
                foreach (Renderer rend in handCup.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (relax != null) relax.SampleAnimation(arms, 0f);
        }
        return animator;
    }

    // ------------------------------------------------------------------ pieces
    internal static GameObject Model(Transform parent, string model, Vector3 pos, float yaw, bool collider = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{model}.fbx");
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (collider) FitCollider(go);
        return go;
    }

    // a box collider around all the meshes of an object (on the object itself: interactables need that)
    internal static void FitCollider(GameObject go)
    {
        if (go == null) return;
        bool any = false;
        var b = new Bounds();
        Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.name == "Screen") continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        if (!any) return;
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
    }

    // A box collider object without a renderer.
    private static GameObject BoxOnly(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    // A box with a material.
    private static GameObject Plain(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // A placeholder box (gray).
    private static GameObject Placeholder(Transform parent, string name, Vector3 pos, Vector3 size, Shader lit)
    {
        Material m = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_Placeholder.mat", null, 0.2f, false, lit);
        return Plain(parent, name, pos, size, m);
    }

    // Makes an object a "look at" thing with his thoughts and a sound.
    private static void Flavor(GameObject go, string label, string labelPt, string[] en, string[] pt, OperatorSounds.Kind sound, OperatorInteractable.HandMotion hand)
    {
        if (go == null) return;
        if (go.GetComponent<Collider>() == null) FitCollider(go);
        go.AddComponent<OperatorFlavor>().EditorSet(label, labelPt, en, pt, sound, hand);
    }

    // A cork board with an evidence item.
    private static void Board(Transform parent, string model, Vector3 pos, string titleEn, string titlePt, string en, string pt)
    {
        GameObject board = Model(parent, model, pos, -90f, collider: true);
        if (board == null) return;
        board.AddComponent<OperatorNoteBoard>().EditorSet("Cork board", "Quadro de cortiça", titleEn, titlePt, en, pt);
    }

    // A point light.
    private static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // A fluorescent tube (light + glowing fixture, optional flicker).
    private static void Tube(Transform parent, string name, Vector3 pos, Material mat, float intensity, bool flicker)
    {
        GameObject t = Plain(parent, name, pos, new Vector3(1.2f, 0.04f, 0.12f), mat);
        t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Light l = PointLight(parent, name + "Light", pos - Vector3.up * 0.2f, new Color(0.82f, 0.92f, 1f), intensity, 6.5f);
        if (!flicker) return;
        var f = t.AddComponent<OperatorLightFlicker>();
        EditorSerialized.SetReference(f, OperatorLightFlicker.LampField, l);
        EditorSerialized.SetReference(f, OperatorLightFlicker.TubeField, t.GetComponent<Renderer>());
    }

    // A view point looking at a target.
    private static Transform ViewPoint(Transform parent, string name, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
        return t;
    }

    // a steel door in its frame, locked (OperatorDoor on the leaf)
    private static void BuildDoor(Transform parent, Vector3 pos, Shader lit)
    {
        Material steel = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_DoorSteel.mat", null, 0.35f, false, lit);
        steel.SetColor(BaseColorId, new Color(0.22f, 0.26f, 0.27f));
        Material frameMat = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_DoorFrame.mat", null, 0.3f, false, lit);
        frameMat.SetColor(BaseColorId, new Color(0.12f, 0.13f, 0.13f));
        Material lamp = EditorAssets.MakeMaterial($"{ArtFolder}/Materials/M_Op_DoorLamp.mat", null, 0.6f, false, lit);
        lamp.SetColor(BaseColorId, new Color(1f, 0.1f, 0.05f));
        Glow(lamp, null, new Color(1f, 0.1f, 0.05f) * 2.5f);
        var door = new GameObject("Door").transform;
        door.SetParent(parent, false);
        door.localPosition = pos;
        Plain(door, "FrameLeft", new Vector3(-0.47f, 1.05f, 0f), new Vector3(0.08f, 2.1f, 0.12f), frameMat);
        Plain(door, "FrameRight", new Vector3(0.47f, 1.05f, 0f), new Vector3(0.08f, 2.1f, 0.12f), frameMat);
        Plain(door, "FrameTop", new Vector3(0f, 2.14f, 0f), new Vector3(1.02f, 0.08f, 0.12f), frameMat);
        GameObject leaf = Plain(door, "Leaf", new Vector3(0f, 1.03f, -0.01f), new Vector3(0.86f, 2.05f, 0.05f), steel);
        leaf.AddComponent<BoxCollider>();
        Plain(leaf.transform, "Handle", new Vector3(0.38f, -0.02f, -0.9f), new Vector3(0.14f, 0.025f, 0.6f), frameMat);
        Plain(leaf.transform, "Window", new Vector3(0f, 0.3f, -0.55f), new Vector3(0.35f, 0.12f, 0.1f), frameMat);
        Plain(door, "LockLamp", new Vector3(0.6f, 1.25f, -0.05f), new Vector3(0.05f, 0.05f, 0.02f), lamp);
        PointLight(door, "LockGlow", new Vector3(0.6f, 1.25f, -0.2f), new Color(1f, 0.15f, 0.05f), 0.3f, 0.8f);
        var use = leaf.AddComponent<OperatorDoor>();
        EditorSerialized.SetString(use, OperatorInteractable.LabelEnField, "Door");
        EditorSerialized.SetString(use, OperatorInteractable.LabelPtField, "Porta");
        EditorSerialized.SetReference(use, OperatorDoor.LeafField, leaf.transform);
    }

    // ================================================================== his screen (UI)
    private static readonly Color WinGray = new Color(0.76f, 0.76f, 0.74f);
    private static readonly Color WinTitle = new Color(0.05f, 0.12f, 0.42f);
    private static readonly Color WinBody = new Color(0.97f, 0.97f, 0.95f);
    private static readonly Color Ink = new Color(0.08f, 0.08f, 0.1f);

    // Parts of the "close the program?" dialog.
    internal class DialogRefs
    {
        // Its group, texts and buttons.
        public CanvasGroup Group;
        public TMP_Text Title, Text, Message, CloseLabel, SettingsLabel, CancelLabel;
        public Button Close, Settings, Cancel;
    }

    // Builds his screen UI (HUD, desktop, dialog).
    private static GameObject BuildUi(Rig rig, out OperatorRoomHud hud, out OperatorDesktop desktop, out DialogRefs dialog)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        var canvasGo = new GameObject("OperatorRoomUI", typeof(RectTransform));
        canvasGo.layer = uiLayer;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;   // over the camera program's UI (its dialog shows over it)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform c = canvasGo.transform;

        hud = BuildRoomHud(c);
        EditorSerialized.SetReference(hud, OperatorRoomHud.ThoughtsField, ThoughtViewBuilder.Build(c, rig.Settings));
        desktop = BuildDesktop(c, rig);
        dialog = BuildDialog(c);
        return canvasGo;
    }

    // His room HUD: dot, notes, thoughts.
    internal static OperatorRoomHud BuildRoomHud(Transform canvas)
    {
        var go = EditorUi.CreateUiObject(canvas, "RoomHud");
        EditorUi.Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        Vector2 mid = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f);
        Image dot = EditorUi.CreateUiObject(go.transform, "Dot").AddComponent<Image>();
        dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        dot.raycastTarget = false;
        EditorUi.Place(dot.rectTransform, mid, mid, mid, Vector2.zero, new Vector2(6f, 6f));
        TMP_Text hover = EditorUi.CreateText(go.transform, "Hover", 22, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.6f));
        EditorUi.Place(hover.rectTransform, mid, mid, mid, new Vector2(0f, -38f), new Vector2(700f, 34f));
        TMP_Text message = EditorUi.CreateText(go.transform, "Message", 26, TextAlignmentOptions.Center, Color.white);
        EditorUi.Place(message.rectTransform, bottom, bottom, bottom, new Vector2(0f, 110f), new Vector2(1100f, 44f));

        // the note being read (cork boards)
        var noteGo = EditorUi.CreateUiObject(canvas, "Note");
        EditorUi.Stretch((RectTransform)noteGo.transform);
        var noteGroup = noteGo.AddComponent<CanvasGroup>();
        noteGroup.blocksRaycasts = false;
        Image shade = EditorUi.CreateUiObject(noteGo.transform, "Shade").AddComponent<Image>();
        EditorUi.Stretch(shade.rectTransform);
        shade.color = new Color(0f, 0f, 0f, 0.8f);
        shade.raycastTarget = false;
        Image paper = EditorUi.CreateUiObject(noteGo.transform, "Paper").AddComponent<Image>();
        EditorUi.Place(paper.rectTransform, mid, mid, mid, Vector2.zero, new Vector2(760f, 540f));
        paper.color = new Color(0.93f, 0.89f, 0.78f);
        paper.raycastTarget = false;
        Vector2 tl = new Vector2(0f, 1f);
        TMP_Text title = EditorUi.CreateText(paper.transform, "Title", 34, TextAlignmentOptions.Top, new Color(0.45f, 0.08f, 0.05f));
        EditorUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(700f, 50f));
        title.fontStyle = FontStyles.Bold | FontStyles.Underline;
        TMP_Text body = EditorUi.CreateText(paper.transform, "Body", 24, TextAlignmentOptions.TopLeft, Ink);
        EditorUi.Place(body.rectTransform, tl, tl, tl, new Vector2(40f, -96f), new Vector2(680f, 390f));
        body.lineSpacing = 12f;
        TMP_Text noteHint = EditorUi.CreateText(paper.transform, "Hint", 16, TextAlignmentOptions.Bottom, new Color(0.35f, 0.3f, 0.25f));
        EditorUi.Place(noteHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(700f, 26f));

        TMP_Text hint = EditorUi.CreateText(canvas, "Hint", 18, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.55f));
        EditorUi.Place(hint.rectTransform, bottom, bottom, bottom, new Vector2(0f, 16f), new Vector2(1240f, 30f));

        var hud = go.AddComponent<OperatorRoomHud>();
        EditorSerialized.SetReference(hud, OperatorRoomHud.GroupField, group);
        EditorSerialized.SetReference(hud, OperatorRoomHud.CrosshairField, dot);
        EditorSerialized.SetReference(hud, OperatorRoomHud.HoverField, hover);
        EditorSerialized.SetReference(hud, OperatorRoomHud.MessageField, message);
        EditorSerialized.SetReference(hud, OperatorRoomHud.NoteGroupField, noteGroup);
        EditorSerialized.SetReference(hud, OperatorRoomHud.NoteTitleField, title);
        EditorSerialized.SetReference(hud, OperatorRoomHud.NoteBodyField, body);
        EditorSerialized.SetReference(hud, OperatorRoomHud.NoteHintField, noteHint);
        EditorSerialized.SetReference(hud, OperatorRoomHud.HintField, hint);
        return hud;
    }

    // The PC desktop with his files.
    private static OperatorDesktop BuildDesktop(Transform canvas, Rig rig)
    {
        var go = EditorUi.CreateUiObject(canvas, "Desktop");
        EditorUi.Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();
        RawImage wall = EditorUi.CreateUiObject(go.transform, "Wallpaper").AddComponent<RawImage>();
        EditorUi.Stretch(wall.rectTransform);
        wall.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/T_Op_Wallpaper.png");
        wall.color = wall.texture != null ? Color.white : new Color(0.02f, 0.18f, 0.2f);

        Button program = Icon(go, "IconProgram", "T_Op_IconProgram", 0, out TMP_Text programLabel);
        Button files = Icon(go, "IconFiles", "T_Op_IconFiles", 1, out TMP_Text filesLabel);
        Button stand = Icon(go, "IconStand", "T_Op_IconStand", 2, out TMP_Text standLabel);

        // taskbar
        Image bar = EditorUi.CreateUiObject(go.transform, "Taskbar").AddComponent<Image>();
        bar.color = WinGray;
        EditorUi.Place(bar.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 40f));
        Image start = EditorUi.CreateUiObject(bar.transform, "Start").AddComponent<Image>();
        start.color = new Color(0.86f, 0.86f, 0.84f);
        start.raycastTarget = false;
        EditorUi.Place(start.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(150f, -10f));
        TMP_Text startText = EditorUi.CreateText(start.transform, "Text", 18, TextAlignmentOptions.Center, Ink);
        EditorUi.Stretch(startText.rectTransform);
        startText.text = "<b>TERMINAL OS</b>";
        TMP_Text status = EditorUi.CreateText(bar.transform, "Status", 17, TextAlignmentOptions.Left, new Color(0.5f, 0.05f, 0.05f));
        EditorUi.Place(status.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(-460f, 0f));
        TMP_Text clock = EditorUi.CreateText(bar.transform, "Clock", 18, TextAlignmentOptions.Center, Ink);
        Vector2 r1 = new Vector2(1f, 0.5f);
        EditorUi.Place(clock.rectTransform, r1, r1, r1, new Vector2(-8f, 0f), new Vector2(90f, 30f));

        // files window + viewer
        RectTransform filesBody = Window(go.transform, "FilesWindow", new Vector2(-230f, 30f), new Vector2(520f, 440f), out CanvasGroup filesGroup, out TMP_Text filesTitle, out Button filesClose);
        RectTransform listContent = ScrollArea(filesBody, out _);
        var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rowGo = EditorUi.CreateUiObject(listContent, "FileRowTemplate");
        Image rowImg = rowGo.AddComponent<Image>();
        rowImg.color = Color.white;
        var rowButton = rowGo.AddComponent<Button>();
        rowButton.targetGraphic = rowImg;
        ColorBlock rcb = rowButton.colors;
        rcb.highlightedColor = new Color(0.75f, 0.85f, 1f);
        rcb.pressedColor = new Color(0.55f, 0.7f, 1f);
        rowButton.colors = rcb;
        rowGo.AddComponent<LayoutElement>().preferredHeight = 32f;
        TMP_Text rowLabel = EditorUi.CreateText(rowGo.transform, "Label", 19, TextAlignmentOptions.Left, Ink);
        EditorUi.Stretch(rowLabel.rectTransform);
        rowLabel.margin = new Vector4(12f, 0f, 8f, 0f);
        var row = rowGo.AddComponent<OperatorFileRow>();
        EditorSerialized.SetReference(row, OperatorFileRow.ButtonField, rowButton);
        EditorSerialized.SetReference(row, OperatorFileRow.LabelField, rowLabel);
        rowGo.SetActive(false);

        RectTransform viewerBody = Window(go.transform, "Viewer", new Vector2(250f, 10f), new Vector2(660f, 500f), out CanvasGroup viewerGroup, out TMP_Text viewerTitle, out Button viewerClose);
        RectTransform textContent = ScrollArea(viewerBody, out ScrollRect viewerScroll);
        TMP_Text viewerText = EditorUi.CreateText(textContent, "Text", 20, TextAlignmentOptions.TopLeft, Ink);
        RectTransform vt = viewerText.rectTransform;
        vt.anchorMin = new Vector2(0f, 1f); vt.anchorMax = new Vector2(1f, 1f); vt.pivot = new Vector2(0.5f, 1f);
        vt.anchoredPosition = Vector2.zero; vt.sizeDelta = Vector2.zero;
        viewerText.margin = new Vector4(14f, 12f, 14f, 12f);
        var textLayout = textContent.gameObject.AddComponent<VerticalLayoutGroup>();
        textLayout.childControlWidth = true; textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true; textLayout.childForceExpandHeight = false;
        textContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var desktop = go.AddComponent<OperatorDesktop>();
        var so = new SerializedObject(desktop);
        so.FindProperty(OperatorDesktop.GroupField).objectReferenceValue = group;
        so.FindProperty(OperatorDesktop.SettingsField).objectReferenceValue = rig.Settings;
        so.FindProperty(OperatorDesktop.StoryField).objectReferenceValue = rig.Story;
        so.FindProperty(OperatorDesktop.ProgramButtonField).objectReferenceValue = program;
        so.FindProperty(OperatorDesktop.FilesButtonField).objectReferenceValue = files;
        so.FindProperty(OperatorDesktop.StandButtonField).objectReferenceValue = stand;
        so.FindProperty(OperatorDesktop.ProgramLabelField).objectReferenceValue = programLabel;
        so.FindProperty(OperatorDesktop.FilesLabelField).objectReferenceValue = filesLabel;
        so.FindProperty(OperatorDesktop.StandLabelField).objectReferenceValue = standLabel;
        so.FindProperty(OperatorDesktop.ClockField).objectReferenceValue = clock;
        so.FindProperty(OperatorDesktop.StatusField).objectReferenceValue = status;
        so.FindProperty(OperatorDesktop.FilesWindowField).objectReferenceValue = filesGroup;
        so.FindProperty(OperatorDesktop.FilesTitleField).objectReferenceValue = filesTitle;
        so.FindProperty(OperatorDesktop.FileListField).objectReferenceValue = listContent;
        so.FindProperty(OperatorDesktop.FileTemplateField).objectReferenceValue = row;
        so.FindProperty(OperatorDesktop.FilesCloseField).objectReferenceValue = filesClose;
        so.FindProperty(OperatorDesktop.ViewerField).objectReferenceValue = viewerGroup;
        so.FindProperty(OperatorDesktop.ViewerTitleField).objectReferenceValue = viewerTitle;
        so.FindProperty(OperatorDesktop.ViewerBodyField).objectReferenceValue = viewerText;
        so.FindProperty(OperatorDesktop.ViewerCloseField).objectReferenceValue = viewerClose;
        so.FindProperty(OperatorDesktop.ViewerScrollField).objectReferenceValue = viewerScroll;
        List<OperatorFile> fileList = EnsureFiles();
        SerializedProperty fp = so.FindProperty(OperatorDesktop.FilesField);
        fp.arraySize = fileList.Count;
        for (int i = 0; i < fileList.Count; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = fileList[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return desktop;
    }

    // A desktop icon.
    private static Button Icon(GameObject go, string name, string tex, int row, out TMP_Text label)
    {
        var b = EditorUi.CreateUiObject(go.transform, name);
        Vector2 tlc = new Vector2(0f, 1f);
        EditorUi.Place((RectTransform)b.transform, tlc, tlc, tlc, new Vector2(24f, -24f - row * 150f), new Vector2(130f, 136f));
        Image hit = b.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        var button = b.AddComponent<Button>();
        button.targetGraphic = hit;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(0.4f, 0.6f, 1f, 0.35f);
        cb.pressedColor = new Color(0.3f, 0.5f, 1f, 0.55f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        button.colors = cb;
        RawImage pic = EditorUi.CreateUiObject(b.transform, "Icon").AddComponent<RawImage>();
        pic.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{tex}.png");
        pic.raycastTarget = false;
        Vector2 top = new Vector2(0.5f, 1f);
        EditorUi.Place(pic.rectTransform, top, top, top, new Vector2(0f, -6f), new Vector2(72f, 72f));
        label = EditorUi.CreateText(b.transform, "Label", 17, TextAlignmentOptions.Top, Color.white);
        EditorUi.Place(label.rectTransform, top, top, top, new Vector2(0f, -82f), new Vector2(130f, 52f));
        return button;
    }

    // a retro window: gray frame, blue title bar with an X; returns the body
    private static RectTransform Window(Transform parent, string name, Vector2 pos, Vector2 size, out CanvasGroup group, out TMP_Text title, out Button close)
    {
        var go = EditorUi.CreateUiObject(parent, name);
        Vector2 mid = new Vector2(0.5f, 0.5f);
        EditorUi.Place((RectTransform)go.transform, mid, mid, mid, pos, size);
        group = go.AddComponent<CanvasGroup>();
        Image frame = go.AddComponent<Image>();
        frame.color = WinGray;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.2f, 0.2f);
        outline.effectDistance = new Vector2(2f, -2f);
        Image bar = EditorUi.CreateUiObject(go.transform, "TitleBar").AddComponent<Image>();
        bar.color = WinTitle;
        EditorUi.Place(bar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-8f, 30f));
        title = EditorUi.CreateText(bar.transform, "Title", 18, TextAlignmentOptions.Left, Color.white);
        EditorUi.Stretch(title.rectTransform);
        title.margin = new Vector4(10f, 0f, 40f, 0f);
        close = UiButton(bar.transform, "Close", new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(28f, 22f), WinGray, "X", 16, Ink, out _);
        var body = EditorUi.CreateUiObject(go.transform, "Body");
        var rt = (RectTransform)body.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = mid;
        rt.offsetMin = new Vector2(6f, 6f); rt.offsetMax = new Vector2(-6f, -40f);
        return rt;
    }

    // scroll area (vertical) filling the body; returns its content
    private static RectTransform ScrollArea(RectTransform body, out ScrollRect scroll)
    {
        var area = EditorUi.CreateUiObject(body, "Scroll");
        EditorUi.Stretch((RectTransform)area.transform);
        Image back = area.AddComponent<Image>();
        back.color = WinBody;
        scroll = area.AddComponent<ScrollRect>();
        var viewport = EditorUi.CreateUiObject(area.transform, "Viewport");
        EditorUi.Stretch((RectTransform)viewport.transform);
        viewport.AddComponent<RectMask2D>();
        var content = EditorUi.CreateUiObject(viewport.transform, "Content");
        var ct = (RectTransform)content.transform;
        ct.anchorMin = new Vector2(0f, 1f); ct.anchorMax = Vector2.one; ct.pivot = new Vector2(0.5f, 1f);
        ct.anchoredPosition = Vector2.zero; ct.sizeDelta = Vector2.zero;
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = ct;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return ct;
    }

    // A UI button with a label.
    private static Button UiButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, string text, float fontSize, Color textColor, out TMP_Text label)
    {
        var go = EditorUi.CreateUiObject(parent, name);
        EditorUi.Place((RectTransform)go.transform, anchor, anchor, anchor, pos, size);
        Image img = go.AddComponent<Image>();
        img.color = color;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.25f, 0.25f);
        outline.effectDistance = new Vector2(1f, -1f);
        label = EditorUi.CreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Center, textColor);
        EditorUi.Stretch(label.rectTransform);
        label.text = text;
        return button;
    }

    // Builds the "close the program?" dialog.
    internal static DialogRefs BuildDialog(Transform canvas)
    {
        var refs = new DialogRefs();
        var go = EditorUi.CreateUiObject(canvas, "CloseDialog");
        EditorUi.Stretch((RectTransform)go.transform);
        refs.Group = go.AddComponent<CanvasGroup>();
        Image shade = go.AddComponent<Image>();   // blocks clicks to the world behind it
        shade.color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform body = Window(go.transform, "Window", Vector2.zero, new Vector2(560f, 230f), out _, out refs.Title, out Button x);
        Object.DestroyImmediate(x.gameObject);   // the dialog has its own buttons
        refs.Text = EditorUi.CreateText(body, "Text", 22, TextAlignmentOptions.Center, Ink);
        EditorUi.Place(refs.Text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(520f, 40f));
        refs.Message = EditorUi.CreateText(body, "Message", 17, TextAlignmentOptions.Center, new Color(0.6f, 0.05f, 0.05f));
        EditorUi.Place(refs.Message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(530f, 50f));
        Vector2 b = new Vector2(0.5f, 0f);
        refs.Close = UiButton(body, "CloseProgram", b, new Vector2(-180f, 32f), new Vector2(160f, 40f), new Color(0.86f, 0.86f, 0.84f), "CLOSE", 18, Ink, out refs.CloseLabel);
        refs.Settings = UiButton(body, "Settings", b, new Vector2(0f, 32f), new Vector2(170f, 40f), new Color(0.86f, 0.86f, 0.84f), "SETTINGS", 18, Ink, out refs.SettingsLabel);
        refs.Cancel = UiButton(body, "Cancel", b, new Vector2(180f, 32f), new Vector2(160f, 40f), new Color(0.86f, 0.86f, 0.84f), "CANCEL", 18, Ink, out refs.CancelLabel);
        return refs;
    }
}
