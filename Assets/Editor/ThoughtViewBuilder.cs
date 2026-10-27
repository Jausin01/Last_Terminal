using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The thoughts block (ThoughtView) on a screen canvas: top-left, under the CCTV camera name / room
// (those end ~145 px down on the 1280x720 reference), a thin amber line on its left, "THOUGHTS" over the text.
internal static class ThoughtViewBuilder
{
    // Block position and width on the 1280x720 reference; tag color.
    internal static readonly Vector2 Position = new Vector2(50f, -160f);
    internal const float Width = 470f;
    private static readonly Color Amber = new Color(1f, 0.78f, 0.38f);

    // Builds the block under a canvas.
    internal static ThoughtView Build(Transform canvas, CameraSettings settings)
    {
        var go = EditorUi.CreateUiObject(canvas, "Thoughts");
        var rt = (RectTransform)go.transform;
        Vector2 tl = new Vector2(0f, 1f);
        EditorUi.Place(rt, tl, tl, tl, Position, new Vector2(Width, 60f));
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        Image back = go.AddComponent<Image>();
        back.color = new Color(0.02f, 0.02f, 0.03f, 0.62f);
        back.raycastTarget = false;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 14, 8, 10);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image line = EditorUi.CreateUiObject(go.transform, "Line").AddComponent<Image>();
        line.color = new Color(Amber.r, Amber.g, Amber.b, 0.85f);
        line.raycastTarget = false;
        line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        EditorUi.Place(line.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f));

        TMP_Text tag = EditorUi.CreateText(go.transform, "Tag", 13, TextAlignmentOptions.Left, new Color(Amber.r, Amber.g, Amber.b, 0.8f));
        tag.text = "THOUGHTS";
        tag.characterSpacing = 6f;
        tag.fontStyle = FontStyles.Bold;
        TMP_Text body = EditorUi.CreateText(go.transform, "Text", 21, TextAlignmentOptions.TopLeft, new Color(0.93f, 0.93f, 0.9f));
        body.textWrappingMode = TextWrappingModes.Normal;

        var view = go.AddComponent<ThoughtView>();
        EditorSerialized.SetReference(view, ThoughtView.GroupField, group);
        EditorSerialized.SetReference(view, ThoughtView.TagField, tag);
        EditorSerialized.SetReference(view, ThoughtView.BodyField, body);
        EditorSerialized.SetReference(view, ThoughtView.SettingsField, settings);
        return view;
    }
}
