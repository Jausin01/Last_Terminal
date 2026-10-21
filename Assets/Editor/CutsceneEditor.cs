using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// The Cutscene inspector: the steps as a list (each with its summary; open one to edit it), move up / down,
// duplicate, delete, "+ Adicionar passo" (menu by category), groups ("Ao mesmo tempo") edited inside. In Play mode:
// a button to play it now. In the Scene view: where each step sends someone and where each shot is taken from.
[CustomEditor(typeof(Cutscene))]
public class CutsceneEditor : Editor
{
    private static readonly Color StepColor = new Color(0.22f, 0.22f, 0.25f);
    private static List<(string menu, int order, Type type)> stepTypes;

    private static List<(string menu, int order, Type type)> StepTypes
    {
        get
        {
            if (stepTypes != null) return stepTypes;
            stepTypes = new List<(string, int, Type)>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom<CutsceneStep>())
            {
                if (t.IsAbstract) continue;
                var info = t.GetCustomAttribute<CutsceneStepInfoAttribute>();
                stepTypes.Add((info != null ? info.Menu : "Outros/" + t.Name, info != null ? info.Order : 999, t));
            }
            stepTypes.Sort((a, b) => a.order.CompareTo(b.order));
            return stepTypes;
        }
    }

    private void OnEnable()
    {
        // a cutscene without its director (made by hand): the scene's one
        var cutscene = (Cutscene)target;
        if (cutscene.Director != null || !cutscene.gameObject.scene.IsValid()) return;
        CutsceneDirector d = CutsceneBuilder.First<CutsceneDirector>(cutscene.gameObject.scene);
        if (d == null) return;
        var so = new SerializedObject(cutscene);
        so.FindProperty(Cutscene.DirectorField).objectReferenceValue = d;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cutscene = (Cutscene)target;

        SerializedProperty it = serializedObject.GetIterator();
        it.NextVisible(true);   // m_Script
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
        while (it.NextVisible(false))
            if (it.name != Cutscene.StepsField) EditorGUILayout.PropertyField(it, true);

        if (cutscene.Director == null)
            EditorGUILayout.HelpBox("Sem diretor: rode Last Terminal/10 nesta cena.", MessageType.Warning);

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            CutsceneDirector d = cutscene.Director;
            string state = d == null ? "sem diretor" : d.IsPlaying ? (d.Current == cutscene ? "tocando" : "outra cutscene tocando")
                         : d.CanStart ? "pronta" : "esperando (operador no PC, sem luta, Helena livre)";
            EditorGUILayout.LabelField("Estado", state);
            using (new EditorGUI.DisabledScope(d == null || !d.CanStart))
                if (GUILayout.Button("▶  Tocar agora", GUILayout.Height(26))) cutscene.PlayAgain();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Passos", EditorStyles.boldLabel);
        DrawList(serializedObject.FindProperty(Cutscene.StepsField), 0);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawList(SerializedProperty list, int depth)
    {
        // changes to the list are made after drawing it (never in the middle of a layout group)
        int op = 0, at = -1;   // 1 up, 2 down, 3 duplicate, 4 delete
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty step = list.GetArrayElementAtIndex(i);
            var value = step.managedReferenceValue as CutsceneStep;
            Rect box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(box.x, box.y, 3f, box.height), depth == 0 ? new Color(0.4f, 0.6f, 1f) : new Color(1f, 0.6f, 0.3f));

            EditorGUILayout.BeginHorizontal();
            string title = value != null ? $"{i + 1}.  {value.Summary}" : $"{i + 1}.  (vazio)";
            step.isExpanded = EditorGUILayout.Foldout(step.isExpanded, title, true);
            using (new EditorGUI.DisabledScope(i == 0))
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24))) { op = 1; at = i; }
            using (new EditorGUI.DisabledScope(i == list.arraySize - 1))
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24))) { op = 2; at = i; }
            using (new EditorGUI.DisabledScope(value == null || value is ParallelStep))
                if (GUILayout.Button(new GUIContent("Dup", "Duplicar"), EditorStyles.miniButtonMid, GUILayout.Width(34))) { op = 3; at = i; }
            if (GUILayout.Button(new GUIContent("X", "Apagar"), EditorStyles.miniButtonRight, GUILayout.Width(22))) { op = 4; at = i; }
            EditorGUILayout.EndHorizontal();

            if (step.isExpanded && value != null)
            {
                EditorGUI.indentLevel++;
                SerializedProperty child = step.Copy();
                SerializedProperty end = step.GetEndProperty();
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    if (value is ParallelStep && child.name == "steps")
                    {
                        EditorGUILayout.LabelField("Passos do grupo", EditorStyles.miniBoldLabel);
                        DrawList(child.Copy(), depth + 1);
                    }
                    else EditorGUILayout.PropertyField(child, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button(depth == 0 ? "+ Adicionar passo" : "+ Adicionar passo ao grupo", GUILayout.Height(depth == 0 ? 24 : 20)))
            AddMenu(list);

        switch (op)
        {
            case 1: list.MoveArrayElement(at, at - 1); break;
            case 2: list.MoveArrayElement(at, at + 1); break;
            case 3: Duplicate(list, at, list.GetArrayElementAtIndex(at).managedReferenceValue as CutsceneStep); break;
            case 4: list.DeleteArrayElementAtIndex(at); break;
        }
    }

    private void AddMenu(SerializedProperty list)
    {
        var menu = new GenericMenu();
        string path = list.propertyPath;
        foreach (var (menuPath, _, type) in StepTypes)
        {
            Type t = type;
            menu.AddItem(new GUIContent(menuPath), false, () =>
            {
                serializedObject.Update();
                SerializedProperty l = serializedObject.FindProperty(path);
                l.arraySize++;
                SerializedProperty e = l.GetArrayElementAtIndex(l.arraySize - 1);
                var step = (CutsceneStep)Activator.CreateInstance(t);
                Prefill(step);
                e.managedReferenceValue = step;
                e.isExpanded = true;
                serializedObject.ApplyModifiedProperties();
            });
        }
        menu.ShowAsContext();
    }

    // a new step about someone starts with Helena
    private void Prefill(CutsceneStep step)
    {
        var cutscene = (Cutscene)target;
        CutsceneActor helena = cutscene.Director != null ? cutscene.Director.Helena : null;
        FieldInfo f = step.GetType().GetField("actor");
        if (helena != null && f != null && f.FieldType == typeof(CutsceneActor)) f.SetValue(step, helena);
    }

    private void Duplicate(SerializedProperty list, int index, CutsceneStep value)
    {
        var copy = (CutsceneStep)Activator.CreateInstance(value.GetType());
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), copy);
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index + 1).managedReferenceValue = copy;
    }

    // ------------------------------------------------------------------ Scene view: the path and the shots
    private void OnSceneGUI()
    {
        var cutscene = (Cutscene)target;
        int n = 0;
        Vector3? last = null;
        foreach (CutsceneStep step in Flatten(cutscene.Steps))
        {
            n++;
            switch (step)
            {
                case MoveStep m when m.target != null:
                    Mark(m.target, $"{n}. {m.Summary}", new Color(0.3f, 0.8f, 1f), ref last);
                    break;
                case TeleportStep t when t.target != null:
                    Mark(t.target, $"{n}. {t.Summary}", new Color(0.6f, 0.6f, 1f), ref last);
                    break;
                case CameraShotStep c when c.shot != null:
                    Handles.color = new Color(1f, 0.85f, 0.3f);
                    Handles.ArrowHandleCap(0, c.shot.position, c.shot.rotation, 1f, EventType.Repaint);
                    Handles.Label(c.shot.position + Vector3.up * 0.2f, $"{n}. {c.Summary}");
                    break;
            }
        }
    }

    private static void Mark(Transform t, string label, Color color, ref Vector3? last)
    {
        Handles.color = color;
        Handles.DrawWireDisc(t.position, Vector3.up, 0.3f);
        Handles.ArrowHandleCap(0, t.position + Vector3.up * 0.05f, Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up) + new Vector3(0f, 0f, 1e-5f)), 0.6f, EventType.Repaint);
        Handles.Label(t.position + Vector3.up * 0.4f, label);
        if (last.HasValue) Handles.DrawDottedLine(last.Value, t.position, 4f);
        last = t.position;
    }

    private static IEnumerable<CutsceneStep> Flatten(IEnumerable<CutsceneStep> steps)
    {
        foreach (CutsceneStep s in steps)
        {
            if (s == null) continue;
            yield return s;
            if (s is ParallelStep p)
                foreach (CutsceneStep c in Flatten(p.steps)) yield return c;
        }
    }
}
