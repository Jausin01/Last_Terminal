using UnityEngine;

// The number plates above a door (both faces): one shared atlas of plates 1..16 (4 x 4, top-left = 1),
// each plate shows its door's cell (MaterialPropertyBlock: the material stays shared).
public class DoorSign : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string DoorField = nameof(door);
    public const string PlatesField = nameof(plates);
    public const int Columns = 4;
    public const int Rows = 4;

    // The door (its number) and the plate renderers.
    [SerializeField] private Door door;
    [SerializeField] private Renderer[] plates = new Renderer[0];

    // Atlas offset property (URP / built-in name).
    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    // Shows the door's cell of the atlas on every plate.
    private void Awake()
    {
        if (door == null || door.Number < 1 || door.Number > Columns * Rows)
        {
            foreach (Renderer r in plates) if (r != null) r.enabled = false;
            return;
        }
        int i = door.Number - 1;
        float w = 1f / Columns, h = 1f / Rows;
        var st = new Vector4(w, h, (i % Columns) * w, 1f - (i / Columns + 1) * h);
        var block = new MaterialPropertyBlock();
        block.SetVector(BaseMapSt, st);
        block.SetVector(MainTexSt, st);
        foreach (Renderer r in plates) if (r != null) r.SetPropertyBlock(block);
    }
}
