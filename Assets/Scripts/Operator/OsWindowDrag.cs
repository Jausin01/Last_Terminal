using UnityEngine;
using UnityEngine.EventSystems;

// On a window's title bar (OsWindow): dragging it moves the window, kept inside the screen.
public class OsWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public const string WindowField = nameof(window);

    [SerializeField] private RectTransform window;

    private Vector2 grab;

    public void OnBeginDrag(PointerEventData e)
    {
        if (window == null) return;
        var parent = (RectTransform)window.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out Vector2 p);
        grab = (Vector2)window.localPosition - p;
        window.SetAsLastSibling();
    }

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
