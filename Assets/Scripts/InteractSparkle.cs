using UnityEngine;
using UnityEngine.Rendering;

// Brilho de "item interagível" (estrela que pisca), estilo RE4.
// Adicione no objeto do item. Some sozinho quando o item é desativado/destruído.
[DisallowMultipleComponent]
public class InteractSparkle : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Color color = new Color(1f, 0.95f, 0.75f, 1f);
    [SerializeField] float size = 0.5f;
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] float pullTowardCamera = 0.15f; // evita ser escondido pelo próprio item

    [Header("Timing")]
    [SerializeField] float flashDuration = 0.35f;
    [SerializeField] float minInterval = 1.2f;
    [SerializeField] float maxInterval = 2.6f;
    [SerializeField] float spinDegrees = 60f;

    static Sprite sharedSprite;

    Transform sparkle;
    SpriteRenderer sr;
    float waitTimer;
    float nextFlash;
    float flashTime = -1f;
    float angle;
    bool visible = true;

    void Awake()
    {
        if (sharedSprite == null) sharedSprite = BuildStarSprite(128);

        // Fica fora da hierarquia do item para não herdar a escala dele (ex.: keycard 0.3)
        GameObject go = new GameObject(name + "_Sparkle");
        sparkle = go.transform;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sharedSprite;
        sr.color = Color.clear;

        nextFlash = Random.Range(0f, maxInterval); // itens não piscam todos juntos
    }

    void OnEnable()
    {
        if (sparkle != null) sparkle.gameObject.SetActive(visible);
        RenderPipelineManager.beginCameraRendering += FaceCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
        if (sparkle != null) sparkle.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (sparkle != null) Destroy(sparkle.gameObject);
    }

    // Chame com false quando o item deixar de ser interagível (ex.: porta destrancada)
    public void SetVisible(bool value)
    {
        visible = value;
        if (sparkle != null) sparkle.gameObject.SetActive(value && isActiveAndEnabled);
    }

    void Update()
    {
        if (!visible) return;

        if (flashTime < 0f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= nextFlash)
            {
                waitTimer = 0f;
                flashTime = 0f;
            }
            sr.color = Color.clear;
            return;
        }

        flashTime += Time.deltaTime;
        float p = flashTime / flashDuration;

        if (p >= 1f)
        {
            flashTime = -1f;
            nextFlash = Random.Range(minInterval, maxInterval);
            sr.color = Color.clear;
            return;
        }

        float k = Mathf.Sin(p * Mathf.PI); // sobe e desce
        angle = spinDegrees * p;
        sparkle.localScale = Vector3.one * (size * k);
        sr.color = new Color(color.r, color.g, color.b, color.a * k);
    }

    // Billboard por câmera: funciona com várias câmeras fixas sem precisar de referência
    void FaceCamera(ScriptableRenderContext ctx, Camera cam)
    {
        Vector3 basePos = transform.position + worldOffset;
        Vector3 toCam = cam.transform.position - basePos;
        sparkle.position = basePos + toCam.normalized * pullTowardCamera;
        sparkle.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, angle);
    }

    // Estrela de 4 pontas + raios diagonais curtos + halo suave
    static Sprite BuildStarSprite(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[n * n];
        float h = (n - 1) * 0.5f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x - h) / h;
                float dy = (y - h) / h;
                float ax = Mathf.Abs(dx);
                float ay = Mathf.Abs(dy);
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float core = Mathf.Exp(-r * r * 60f);
                float rayH = Mathf.Exp(-ay * 70f) * Mathf.Pow(1f - Mathf.Min(ax, 1f), 2f);
                float rayV = Mathf.Exp(-ax * 70f) * Mathf.Pow(1f - Mathf.Min(ay, 1f), 2f);

                float d1 = Mathf.Abs(dx - dy) * 0.7071f;
                float d2 = Mathf.Abs(dx + dy) * 0.7071f;
                float diagLen = Mathf.Clamp01(1f - r * 2f);
                float rayD = (Mathf.Exp(-d1 * 80f) + Mathf.Exp(-d2 * 80f)) * diagLen * 0.6f;

                float halo = Mathf.Exp(-r * r * 8f) * 0.2f;

                float a = Mathf.Clamp01(core + rayH + rayV + rayD + halo);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
