using UnityEngine;

// Something in his room he looks at up close (an evidence board, the door keypad): used, the view glides to its
// View Point, the cursor is free, the station hands it every frame (UpdateFocus) until Esc takes him back
// (HandleBack first: true = it handled Esc itself, e.g. zooming back out of a photo).
public abstract class FocusInteractable : OperatorInteractable
{
    public const string StationField = nameof(station);
    public const string ViewPointField = nameof(viewPoint);

    [SerializeField] private OperatorStation station;
    [Tooltip("Where his eyes go (its blue arrow looks at the thing). Move it to frame it better.")]
    [SerializeField] private Transform viewPoint;
    [SerializeField] private float blendTime = 0.55f;

    public Transform ViewPoint => viewPoint;
    public float BlendTime => blendTime;
    protected OperatorStation Station => station;

    public override HandMotion Motion => HandMotion.None;

    public override void Interact(FirstPersonPlayer player)
    {
        if (station != null && viewPoint != null) station.Focus(this);
    }

    // the view arrived: from here on UpdateFocus runs every frame
    public virtual void BeginFocus(FirstPersonPlayer player) { }

    public virtual void UpdateFocus(FirstPersonPlayer player) { }

    // Esc while focused: true = handled here (stay), false = the station takes him back
    public virtual bool HandleBack(FirstPersonPlayer player) => false;

    // leaving (before the view goes back to his eyes)
    public virtual void EndFocus(FirstPersonPlayer player) { }
}
