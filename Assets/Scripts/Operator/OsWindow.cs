using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A window of the fake OS (OperatorOS): a title bar (dragged by OsWindowDrag), an X, a body. Clicked anywhere it
// comes to the front. Hidden with its CanvasGroup (stays where it was dragged).
public class OsWindow : MonoBehaviour, IPointerDownHandler
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GroupField = nameof(group);
    public const string TitleField = nameof(title);
    public const string CloseButtonField = nameof(closeButton);

    // Its CanvasGroup (shown / hidden), title text and close button.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Button closeButton;

    // Raised when it is closed / brought to the front.
    public event Action<OsWindow> Closed;
    public event Action<OsWindow> Focused;

    // Shown now.
    public bool IsOpen => group != null && group.alpha > 0.5f;

    // Wires the X; starts hidden.
    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        SetVisible(false);
    }

    // Opens it (optionally with a new title) on top.
    public void Open(string text = null)
    {
        if (text != null && title != null) title.text = text;
        SetVisible(true);
        transform.SetAsLastSibling();
        Focused?.Invoke(this);
    }

    // Changes the title.
    public void SetTitle(string text)
    {
        if (title != null) title.text = text;
    }

    // Hides it and raises Closed.
    public void Close()
    {
        if (!IsOpen) return;
        SetVisible(false);
        Closed?.Invoke(this);
    }

    // hidden without the Closed event (power off, log off)
    public void Hide() => SetVisible(false);

    // Clicked: to the front.
    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
        Focused?.Invoke(this);
    }

    // Shows / hides it (and its clicks).
    private void SetVisible(bool on)
    {
        if (group == null) return;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}
