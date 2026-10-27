using UnityEngine;

// A named area: a room of the facility, or a section of a big hall (the casino). Shown on the camera map (in the
// game's language), used to say where she is and as a place to send her ("go to the bar").
// The area is a box on the floor: this transform's position = center, size = (width x, depth z).
// Spot: where she goes when sent there (a clear place on the floor, marked on the map); none = around the center.
// Open To: areas joined to this one without a door (sections of the same hall): she walks between them freely.
// Safe Zone: she can be left alone here (the operator may close the camera program and get up from the PC).
public class Room : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string DisplayNameField = nameof(displayName);
    public const string DisplayNamePtField = nameof(displayNamePt);
    public const string SizeField = nameof(size);
    public const string SpotField = nameof(spot);
    public const string OpenToField = nameof(openTo);
    public const string SafeZoneField = nameof(safeZone);

    [Tooltip("English name: also the lexicon key the commands use.")]
    [SerializeField] private string displayName = "Room";
    // Portuguese name, and the floor area size (m).
    [SerializeField] private string displayNamePt = "";
    [SerializeField] private Vector2 size = new Vector2(8f, 8f);
    [Tooltip("Optional: where she stands when sent here (marked on the map).")]
    [SerializeField] private Transform spot;
    [Tooltip("Areas joined to this one without a door.")]
    [SerializeField] private Room[] openTo = new Room[0];
    [Tooltip("She can be left alone here: the operator can leave the PC (she sits on the floor and waits).")]
    [SerializeField] private bool safeZone;

    // Read-only data.
    public string DisplayName => displayName;
    public Vector2 Size => size;
    public Transform Spot => spot;
    public Room[] OpenTo => openTo;
    public bool SafeZone => safeZone;

    // Name in a language.
    public string NameIn(GameLanguage language) =>
        language == GameLanguage.Portuguese && !string.IsNullOrEmpty(displayNamePt) ? displayNamePt : displayName;

    // floor rectangle in world XZ (rooms are axis aligned)
    public Rect FloorRect => new Rect(transform.position.x - size.x * 0.5f, transform.position.z - size.y * 0.5f, size.x, size.y);

    // Is a point inside the area (floor plane)?
    public bool Contains(Vector3 worldPosition) => FloorRect.Contains(new Vector2(worldPosition.x, worldPosition.z));

    // margin > 0 grows the area (doors on its walls), < 0 keeps away from the walls
    public bool Contains(Vector3 worldPosition, float margin)
    {
        Rect r = FloorRect;
        r.xMin -= margin; r.yMin -= margin; r.xMax += margin; r.yMax += margin;
        return r.Contains(new Vector2(worldPosition.x, worldPosition.z));
    }

    // Gizmo of the area and its spot.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.02f, new Vector3(size.x, 0.02f, size.y));
        if (spot == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(spot.position + Vector3.up * 0.1f, 0.3f);
    }
}
