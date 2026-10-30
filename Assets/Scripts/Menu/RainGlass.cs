using UnityEngine;

// Rain running down a window: the renderer's texture (T_MenuGlassStreaks) scrolls down slowly, its own speed per
// pane so two panes never move together. MaterialPropertyBlock: the material asset is not touched.
[RequireComponent(typeof(Renderer))]
public class RainGlass : MonoBehaviour
{
    // How many texture heights per second the streaks run down, and how many times the texture repeats on the pane.
    [SerializeField] private float speed = 0.035f;
    [SerializeField] private Vector2 tiling = new Vector2(1.5f, 1.2f);

    // URP Lit / Unlit main texture scale + offset.
    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    private Renderer target;
    private MaterialPropertyBlock block;
    private float offset;

    // Caches the renderer; a random start so the panes differ.
    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        offset = Random.value;
    }

    // Scrolls the streaks down.
    private void Update()
    {
        offset = Mathf.Repeat(offset + speed * Time.unscaledDeltaTime, 1f);
        target.GetPropertyBlock(block);
        block.SetVector(BaseMapSt, new Vector4(tiling.x, tiling.y, 0f, offset));
        target.SetPropertyBlock(block);
    }
}
