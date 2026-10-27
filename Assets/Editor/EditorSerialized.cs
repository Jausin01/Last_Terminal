using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Sets serialized fields by name (SerializedObject), the way every builder wires components: the scripts expose the
// names as constants (nameof), so a renamed field is a compile error instead of a silent miss.
internal static class EditorSerialized
{
    internal static void SetReferenceArray<T>(Object target, string fieldName, List<T> values) where T : Object
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null || !prop.isArray) { Debug.LogError($"[Helena] Array field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sets an int field by name (SerializedObject).
    internal static void SetInt(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sets an object reference field by name (SerializedObject).
    internal static void SetReference(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sets a string field by name (SerializedObject).
    internal static void SetString(Object target, string fieldName, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogError($"[Helena] Field '{fieldName}' not found on {target.GetType().Name}."); return; }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
