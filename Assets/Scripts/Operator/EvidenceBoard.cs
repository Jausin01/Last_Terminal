using UnityEngine;
using UnityEngine.InputSystem;

// A cork board on his wall, looked at up close (FocusInteractable): the mouse over a photo / note (EvidenceItem)
// frames it, a click zooms on it (title + text on the right, his thought top-left). Esc: back to the whole board,
// Esc again: back to the room. The main board of the case and the boards of the old case all use this.
public class EvidenceBoard : FocusInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ItemsField = nameof(items);
    public const string PanelField = nameof(panel);

    // The pinned items, the side panel, the zoom time, and the mouse inputs.
    [SerializeField] private EvidenceItem[] items = new EvidenceItem[0];
    [SerializeField] private EvidencePanel panel;
    [SerializeField] private float zoomTime = 0.45f;
    [SerializeField] private InputAction point = new InputAction("Point", InputActionType.Value, "<Pointer>/position");
    [SerializeField] private InputAction click = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");

    // Looking at the board now; the item under the mouse; the item zoomed on.
    private bool focusedNow;
    private EvidenceItem hovered;
    private EvidenceItem zoomed;

    // Enables the mouse inputs.
    private void OnEnable() { point.Enable(); click.Enable(); }
    private void OnDisable() { point.Disable(); click.Disable(); }

    // The hint at the bottom (zoomed or not).
    private string HintText(GameLanguage l, bool zoomedIn) => zoomedIn
        ? Pick(l, "Click another photo   |   Esc: back to the board", "Clique em outra foto   |   Esc: voltar ao quadro")
        : Pick(l, "Click a photo to look closer   |   Esc: back", "Clique numa foto para ver de perto   |   Esc: voltar");

    // Looking at the board: items unlocked by the story become clickable.
    public override void BeginFocus(FirstPersonPlayer player)
    {
        focusedNow = true;
        zoomed = null;
        foreach (EvidenceItem i in items)
            if (i != null) i.SetClickable(player.Story == null || player.Story.Has(i.RequiredFlag));
        player.Hud.SetHint(HintText(player.Language, false));
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Paper, 0.5f);
    }

    // Hover highlight and click to zoom.
    public override void UpdateFocus(FirstPersonPlayer player)
    {
        if (!focusedNow || player.IsBlending) return;
        EvidenceItem over = ItemUnderMouse(player.View);
        if (over != hovered)
        {
            if (hovered != null) hovered.SetHighlight(false);
            hovered = over;
            if (hovered != null && hovered != zoomed) hovered.SetHighlight(true);
        }
        if (click.WasPressedThisFrame() && hovered != null && hovered != zoomed) ZoomTo(player, hovered);
    }

    // The clickable item under the mouse (nearest hit).
    private EvidenceItem ItemUnderMouse(Camera view)
    {
        Ray ray = view.ScreenPointToRay(point.ReadValue<Vector2>());
        EvidenceItem best = null;
        float bestDistance = float.MaxValue;
        foreach (EvidenceItem i in items)
        {
            if (i == null || !i.Box.enabled) continue;
            if (i.Box.Raycast(ray, out RaycastHit hit, 20f) && hit.distance < bestDistance)
            {
                best = i;
                bestDistance = hit.distance;
            }
        }
        return best;
    }

    // Zooms on an item: panel text, his thought, its "seen" flag.
    private void ZoomTo(FirstPersonPlayer player, EvidenceItem item)
    {
        if (hovered != null) hovered.SetHighlight(false);
        zoomed = item;
        GameLanguage l = player.Language;
        item.ZoomPose(out Vector3 pos, out Quaternion rot);
        player.BlendViewTo(pos, rot, zoomTime, null);
        if (panel != null) panel.Show(item.Title(l), item.Text(l));
        string thought = item.Thought(l);
        if (!string.IsNullOrEmpty(thought)) player.Hud.ShowThought(thought);
        if (player.Story != null && !string.IsNullOrEmpty(item.SeenFlag)) player.Story.Set(item.SeenFlag);
        player.Hud.SetHint(HintText(l, true));
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Paper, 0.6f);
    }

    // Esc while zoomed: back to the whole board.
    public override bool HandleBack(FirstPersonPlayer player)
    {
        if (zoomed == null) return false;
        zoomed = null;
        if (panel != null) panel.Hide();
        if (ViewPoint != null) player.BlendViewTo(ViewPoint.position, ViewPoint.rotation, zoomTime, null);
        player.Hud.SetHint(HintText(player.Language, false));
        return true;
    }

    // Leaving the board: nothing clickable.
    public override void EndFocus(FirstPersonPlayer player)
    {
        focusedNow = false;
        zoomed = null;
        hovered = null;
        if (panel != null) panel.Hide();
        foreach (EvidenceItem i in items) if (i != null) i.SetClickable(false);
    }
}
