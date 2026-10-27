using TMPro;
using UnityEngine;
using UnityEngine.UI;

// uGUI + TextMeshPro helpers for the editor builders: UI objects, texts, buttons, RectTransform placement.
internal static class EditorUi
{
    // A UI button.
    internal static Button CreateButton(Transform parent, string objectName, string label, Color color, Vector2 position)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        Image back = CreateUiObject(parent, objectName).AddComponent<Image>();
        back.color = Color.white;
        Place(back.rectTransform, c, c, c, position, new Vector2(260f, 56f));
        var outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
        var button = back.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.02f, 0.02f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.05f, 0.04f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.55f, 0.08f, 0.06f, 1f);
        colors.disabledColor = new Color(0.08f, 0.06f, 0.06f, 0.6f);
        button.colors = colors;
        button.targetGraphic = back;
        TMP_Text text = CreateText(back.transform, "Label", 24, TextAlignmentOptions.Center, color);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    // A UI object with a RectTransform.
    internal static GameObject CreateUiObject(Transform parent, string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    // A TextMeshPro text.
    internal static TMP_Text CreateText(Transform parent, string objectName, float size, TextAlignmentOptions align, Color color)
    {
        var text = CreateUiObject(parent, objectName).AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    // Anchors, pivot, position and size of a RectTransform.
    internal static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // Stretches a RectTransform to its parent.
    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
