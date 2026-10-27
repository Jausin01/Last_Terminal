using UnityEngine;

// Scene object helpers for the editor builders: a recursive child search, empty children and plain boxes.
internal static class EditorScene
{
    // An empty child at a local position.
    internal static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        var t = new GameObject(childName).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    // editor-time lookup inside an imported model (names come from our own export pipeline)
    internal static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // A box primitive.
    internal static GameObject CreateBox(Transform parent, Vector3 pos, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Box";
        box.transform.SetParent(parent);
        box.transform.position = pos;
        box.transform.localScale = size;
        return box;
    }
}
