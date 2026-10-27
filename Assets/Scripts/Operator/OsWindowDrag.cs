using UnityEngine;
using UnityEngine.EventSystems;

// On a window's title bar (OsWindow): dragging it moves the window, kept inside the screen.
public class OsWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string WindowField = nameof(window);

    // The window this bar moves.
    [SerializeField] private RectTransform window;

    // Offset from the pointer to the window's pivot when the drag started.
    private Vector2 grab;

    // Drag started: remember the offset; window on top.
    public void OnBeginDrag(PointerEventData e)
    {
        if (window == null) return;
        var parent = (RectTransform)window.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out Vector2 p);
        grab = (Vector2)window.localPosition - p;
        window.SetAsLastSibling();
    }

    // Moves the window with the pointer, keeping the title bar on screen.
    public void OnDrag(PointerEventData e)
    {
        if (window == null) return;
        var parent = (RectTransform)window.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out Vector2 p)) return;
        Vector2 pos = p + grab;
        Rect area = parent.rect;
        Vector2 size = window.rect.size;
        Vector2 pivot = window.pivot;
        // keep at least the title bar on screen
        pos.x = Mathf.Clamp(pos.x, area.xMin - size.x * (1f - pivot.x) + 80f, area.xMax + size.x * pivot.x - 80f);
        pos.y = Mathf.Clamp(pos.y, area.yMin + 70f - size.y * (1f - pivot.y), area.yMax - size.y * (1f - pivot.y));   // over the taskbar
        window.localPosition = new Vector3(pos.x, pos.y, window.localPosition.z);
    }
}
