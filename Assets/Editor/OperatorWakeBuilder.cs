using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Last Terminal/16 (OperatorSceneSetup) -> RoomSystems/WakeUp: after the pig in the van (cutscene 01) he wakes up on
// the cot of the operator room, confused, then angry at how he was brought. A cinema cutscene with its own eyes
// (WakeCamera), the screen and director of the story cutscenes (IniciacaoBuilder.BuildScreenAndDirector), the shots
// made around the cot (wherever it is), and the cutscene (Cutscene_Acordar, id "operator_wake"). OperatorStation
// plays it when the room starts until it has been seen, and puts him at Spot_WakeEnd when it ends (or Esc).
// RoomSystems is rebuilt by the menu: tune the steps / shots after you are happy with the build, or keep a copy.
internal static class OperatorWakeBuilder
{
    internal const string Id = "operator_wake";
    private const string IniAudio = "Assets/Audio/Iniciacao";
    private const float SitEye = 1.12f;
    private const float StandEye = 1.62f;

    private static SerializedProperty steps;

    internal static Cutscene Build(Transform sys, GameObject cot, Vector3 roomCenter, GameObject pc, GameObject door,
                                   CameraSettings settings, StoryProgress story, out Transform wakeEnd)
    {
        wakeEnd = null;
        if (cot == null)
        {
            Debug.LogWarning("[Operator] SM_OpRoom_Cot not found (RoomExtras): no wake-up cutscene. Delete RoomExtras and run the menu again to get the cot.");
            return null;
        }
        var root = new GameObject("WakeUp").transform;
        root.SetParent(sys, false);

        // ---------------------------------------------------------------- his eyes for the cutscene (off outside it)
        var camGo = new GameObject("WakeCamera");
        camGo.transform.SetParent(root, false);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 60f;
        cam.enabled = false;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>().enabled = false;

        // ---------------------------------------------------------------- the shots, from the cot
        // the cot (SM_OpRoom_Cot): 1.9 m along its x, the pillow at -x, the canvas 0.43 m up; the room is on the side
        // of its z that faces the middle of the room
        Transform c = cot.transform;
        Vector3 up = Vector3.up;
        Vector3 toFeet = Flat(c.right);
        Vector3 toRoom = Flat(c.forward) * Mathf.Sign(Vector3.Dot(roomCenter - c.position, c.forward));
        float floor = c.position.y;
        Vector3 pcAt = pc != null ? Center(pc) : roomCenter;
        Vector3 doorAt = door != null ? Center(door) : roomCenter;
        Vector3 head = c.TransformPoint(new Vector3(-0.66f, 0.64f, 0f));
        Vector3 seat = c.TransformPoint(new Vector3(-0.15f, 0f, 0f)) + toRoom * 0.25f;
        Vector3 sitEye = new Vector3(seat.x, floor + SitEye, seat.z);
        Vector3 feet = c.TransformPoint(new Vector3(-0.15f, 0f, 0f)) + toRoom * 0.85f;
        feet.y = floor;
        Vector3 standEye = feet + up * StandEye;

        Transform lying = Shot(root, "Shot_WakeLying", head, up + toFeet * 0.25f, -toFeet);         // the ceiling, its lamps
        Transform turn = Shot(root, "Shot_WakeTurn", head + toRoom * 0.03f, toRoom + up * 0.45f, -toFeet);   // head turned to the room
        Transform sit = Shot(root, "Shot_WakeSit", sitEye, pcAt - sitEye, up);                       // sat up: the desk
        Transform lookDoor = Shot(root, "Shot_WakeDoor", sitEye, doorAt - sitEye, up);
        Vector3 window = c.position - toRoom * 0.45f + up * 1.6f;                                    // over the cot, on the wall
        Transform lookWindow = Shot(root, "Shot_WakeWindow", sitEye + toRoom * 0.05f, window - sitEye, up);
        Transform stand = Shot(root, "Shot_WakeStand", standEye, Flat(pcAt - standEye), up);
        wakeEnd = Shot(root, "Spot_WakeEnd", feet, Flat(pcAt - standEye), up);
        camGo.transform.SetPositionAndRotation(lying.position, lying.rotation);

        // ---------------------------------------------------------------- the screen and the director
        CutsceneDirector d = IniciacaoBuilder.BuildScreenAndDirector(cam, settings, out GameObject canvasGo);
        canvasGo.name = "WakeUI";
        canvasGo.transform.SetParent(root, false);
        canvasGo.GetComponent<Canvas>().sortingOrder = 40;   // over the room's own screen
        d.transform.SetParent(root, false);
        HelenaSetupMenu.SetReference(d, CutsceneDirector.StoryField, story);

        // ---------------------------------------------------------------- the cutscene
        var go = new GameObject("Cutscene_Acordar");
        go.transform.SetParent(root, false);
        var cutscene = go.AddComponent<Cutscene>();
        HelenaSetupMenu.SetReference(cutscene, Cutscene.DirectorField, d);
        HelenaSetupMenu.SetString(cutscene, Cutscene.IdField, Id);
        var so = new SerializedObject(cutscene);
        steps = so.FindProperty(Cutscene.StepsField);
        steps.ClearArray();

        // black; his heart; the head
        Add(new FadeStep { toBlack = true, duration = 0f });
        Add(new CameraShotStep { shot = lying, label = "", fieldOfView = 62f });
        Add(new HandheldStep { amount = 0.6f });
        Add(new SoundStep { clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{IniAudio}/Heartbeat.wav"), volume = 0.7f });
        Add(Wait(1.4f));
        Add(Think("...minha cabeça.", "...my head."));
        // the eyes open: blinks on the ceiling lamps
        Add(new FadeStep { toBlack = false, duration = 0.35f });
        Add(Wait(0.35f));
        Add(new FadeStep { toBlack = true, duration = 0.15f });
        Add(Wait(0.5f));
        Add(new FadeStep { toBlack = false, duration = 0.9f });
        Add(Wait(0.8f));
        Add(Think("Onde... eu tô?", "Where... am I?"));
        Add(Move(turn, 1.8f));
        Add(Think("Isso não é a minha casa.", "This isn't my place."));
        // he sits up, dizzy
        Add(new HandheldStep { amount = 1.8f });
        Add(Move(sit, 2.2f, 60f));
        Add(new HandheldStep { amount = 1.0f });
        Add(Think("A van. O vidro abrindo... aquela máscara de porco.", "The van. The glass sliding open... that pig mask."));
        Add(Move(lookDoor, 1.4f));
        Add(Think("Porta de metal. Trancada, aposto.", "A metal door. Locked, I bet."));
        Add(Move(lookWindow, 1.6f));
        Add(Think("E a janela, lacrada.", "And the window's sealed shut."));
        Add(Move(sit, 1.2f));
        // angry
        Add(new HandheldStep { amount = 2.2f });
        Add(Think("Me apagaram e me largaram aqui feito carga.", "They knocked me out and dumped me here like cargo."));
        Add(new OperatorLineStep { textPt = "Ei! Tem alguém aí?", textEn = "Hey! Is anyone there?", match = 0.5f });
        Add(new HandheldStep { amount = 1.0f });
        Add(Wait(2.2f));
        Add(Think("Nada. Claro.", "Nothing. Of course."));
        Add(Think("\"Essa pessoa vai te levar até um lugar seguro\"... Era isso, Mike?", "\"They'll take you somewhere safe\"... This is it, Mike?"));
        // he gets up
        Add(Move(stand, 1.8f, 66f));
        Add(Think("Quando eu sair daqui, a gente vai ter uma conversa.", "When I get out of here, you and I are going to talk."));
        Add(new HandheldStep { amount = 0.35f });

        so.ApplyModifiedPropertiesWithoutUndo();
        return cutscene;
    }

    private static void Add(CutsceneStep step)
    {
        steps.arraySize++;
        steps.GetArrayElementAtIndex(steps.arraySize - 1).managedReferenceValue = step;
    }

    private static ThoughtStep Think(string pt, string en) => new ThoughtStep { textPt = pt, textEn = en, pauseAfter = 0.3f };
    private static WaitStep Wait(float s) => new WaitStep { seconds = s };
    private static CameraMoveStep Move(Transform to, float s, float fov = 0f) => new CameraMoveStep { to = to, seconds = s, fieldOfView = fov };

    private static Transform Shot(Transform parent, string name, Vector3 position, Vector3 forward, Vector3 up)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
        return t;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static Vector3 Center(GameObject go)
    {
        var rs = new List<Renderer>(go.GetComponentsInChildren<Renderer>());
        if (rs.Count == 0) return go.transform.position;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }
}
