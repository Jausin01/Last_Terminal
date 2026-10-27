using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds the runtime UI of the operator's screen in code (uGUI + TextMeshPro): images, texts, outlined buttons, crisp
// frames, the generated sprites (circle, ring, camera view wedge), and panel show / hide. Stateless helpers.
internal static class HudUi
{
    // Shows / hides a panel (and its clicks).
    internal static void SetPanel(CanvasGroup panel, bool open)
    {
        if (panel == null) return;
        panel.alpha = open ? 1f : 0f;
        panel.interactable = open;
        panel.blocksRaycasts = open;
    }

    // New UI image under a parent.
    internal static Image NewImage(RectTransform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // New UI text under a parent.
    internal static TMP_Text NewText(RectTransform parent, string text, float size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    // New UI button with a label.
    internal static Button NewButton(RectTransform parent, string text, float size, Action onClick, out Image image, Color tint)
    {
        image = NewImage(parent, "Button_" + text, new Color(tint.r, tint.g, tint.b, 0.15f));
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = tint;
        outline.effectDistance = new Vector2(1f, -1f);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        TMP_Text t = NewText(image.rectTransform, text, size, tint);
        Stretch(t.rectTransform, 0f);
        return button;
    }

    // Stretches a RectTransform to its parent with padding.
    internal static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    // a rect under the previous one in the side panel
    internal static void Top(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(0f, height);
        if (rt.TryGetComponent(out TMP_Text t)) t.alignment = TextAlignmentOptions.Left;
    }

    // four thin edges inside a rect (a crisp border; Outline draws a blurry offset copy)
    internal static Image[] Frame(RectTransform parent, float thickness, Color color)
    {
        var edges = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image e = NewImage(parent, "Edge", color);
            e.raycastTarget = false;
            RectTransform rt = e.rectTransform;
            bool vertical = i >= 2;
            float side = i % 2 == 0 ? 0f : 1f;
            rt.anchorMin = vertical ? new Vector2(side, 0f) : new Vector2(0f, side);
            rt.anchorMax = vertical ? new Vector2(side, 1f) : new Vector2(1f, side);
            rt.pivot = vertical ? new Vector2(side, 0.5f) : new Vector2(0.5f, side);
            rt.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
            rt.anchoredPosition = Vector2.zero;
            edges[i] = e;
        }
        return edges;
    }

    // Generated sprites (circle, ring, view wedge), made once.
    private static Sprite circleSprite, ringSprite, wedgeSprite;

    // A filled circle (camera dots, her blip).
    internal static Sprite Circle => circleSprite != null ? circleSprite : circleSprite = MakeSprite(64, (x, y) => Disc(x, y, 0.5f));

    // A ring (her pulse on the map).
    internal static Sprite Ring => ringSprite != null ? ringSprite : ringSprite = MakeSprite(64, (x, y) => Mathf.Clamp01(Disc(x, y, 0.5f) - Disc(x, y, 0.4f)));

    // field of view: apex at the bottom middle, 70 deg wide, fading out with distance
    internal static Sprite Wedge => wedgeSprite != null ? wedgeSprite : wedgeSprite = MakeSprite(64, (x, y) =>
    {
        float dx = x - 0.5f, dy = y;
        if (dy <= 0f) return 0f;
        float angle = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg;
        float inside = Mathf.Clamp01((35f - angle) / 2f);
        return inside * Mathf.Clamp01(1.05f - Mathf.Sqrt(dx * dx + dy * dy));
    });

    // Alpha of an antialiased disc.
    private static float Disc(float x, float y, float radius)
    {
        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
        return Mathf.Clamp01((radius - d) * 64f);
    }

    // Builds a sprite from an alpha function.
    private static Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha((x + 0.5f) / size, (y + 0.5f) / size) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
