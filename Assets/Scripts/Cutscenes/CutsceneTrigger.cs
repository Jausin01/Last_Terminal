using UnityEngine;

// Starts a cutscene: when the scene starts, when Helena enters a room or an area (the box of this object, drawn in
// the Scene view), when a story flag is set, or never (Manual: something else calls Cutscene.Play).
// Optional conditions: a flag that must be set / must not be set. Delay: seconds after it could start.
// It waits for the director (the operator at the cameras, no fight) and fires once.
public class CutsceneTrigger : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string CutsceneField = nameof(cutscene);
    public const string WhenField = nameof(when);
    public const string RoomField = nameof(room);
    public const string AreaSizeField = nameof(areaSize);

    // What starts the cutscene.
    public enum When { SceneStart, HelenaEntersRoom, HelenaEntersArea, FlagSet, Manual }

    // The cutscene, the start condition, and the room for HelenaEntersRoom.
    [SerializeField] private Cutscene cutscene;
    [SerializeField] private When when = When.HelenaEntersArea;
    [SerializeField] private Room room;
    [Tooltip("The area (this object's position / rotation, this size).")]
    [SerializeField] private Vector3 areaSize = new Vector3(3f, 2f, 3f);
    // FlagSet: the story flag that starts it.
    [SerializeField] private string flag = "";
    [Tooltip("Only if this flag is set (empty = always).")]
    [SerializeField] private string requiredFlag = "";
    [Tooltip("Never if this flag is set (empty = no such rule).")]
    [SerializeField] private string blockingFlag = "";
    // Seconds to wait once it could start.
    [SerializeField, Min(0f)] private float delay = 0f;

    // Already started, and how long it has been able to start.
    private bool fired;
    private float waited;

    // Starts the cutscene once its conditions hold and the director can play it (after the delay).
    private void Update()
    {
        if (fired || when == When.Manual || cutscene == null || cutscene.Director == null) return;
        CutsceneDirector director = cutscene.Director;
        if (cutscene.Once && cutscene.HasPlayed) { fired = true; return; }
        // the delay counts once it could start (e.g. after the operator opens the cameras)
        if (!Conditions(director) || !director.CanStart) { waited = 0f; return; }
        waited += Time.deltaTime;
        if (waited < delay) return;
        if (cutscene.Play()) fired = true;
    }

    // The start condition and the required / blocking flags.
    private bool Conditions(CutsceneDirector director)
    {
        StoryProgress story = director.Story;
        if (!string.IsNullOrEmpty(requiredFlag) && (story == null || !story.Has(requiredFlag))) return false;
        if (!string.IsNullOrEmpty(blockingFlag) && story != null && story.Has(blockingFlag)) return false;
        Transform her = director.Partner;
        switch (when)
        {
            case When.SceneStart: return true;
            case When.HelenaEntersRoom: return room != null && her != null && room.Contains(her.position);
            case When.HelenaEntersArea: return her != null && InArea(her.position);
            case When.FlagSet: return story != null && !string.IsNullOrEmpty(flag) && story.Has(flag);
            default: return false;
        }
    }

    // Is a world point inside the trigger box?
    private bool InArea(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        Vector3 h = areaSize * 0.5f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.z) <= h.z && p.y >= -0.5f && p.y <= areaSize.y;
    }

    // Draws the trigger box in the Scene view.
    private void OnDrawGizmos()
    {
        if (when != When.HelenaEntersArea) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.25f);
        Gizmos.DrawCube(new Vector3(0f, areaSize.y * 0.5f, 0f), areaSize);
        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, areaSize.y * 0.5f, 0f), areaSize);
    }
}
