using UnityEngine;

// The white sparkle over something she can take or look at (on the CCTV feed it shows what's interactable):
// two stars facing the operator's camera, twinkling. On an Examinable it goes out once she has looked at it;
// on a pickup it goes with the pickup (its child). Built by the scene menu (Last Terminal/17).
public class InteractSparkle : MonoBehaviour
{
    public const string ViewCameraField = nameof(viewCamera);
    public const string ExaminableField = nameof(examinable);
    public const string StarsField = nameof(stars);

    [Tooltip("The operator's view camera (the stars face it).")]
    [SerializeField] private Transform viewCamera;
    [Tooltip("Optional: the thing to look at; the sparkle goes out once she has looked at it.")]
    [SerializeField] private Examinable examinable;
    [SerializeField] private Transform[] stars = new Transform[0];
    [SerializeField] private float size = 0.22f;
    [Tooltip("Twinkles per second.")]
    [SerializeField] private float speed = 2.2f;

    private float phase;

    private void Awake() => phase = Random.value * 10f;

    private void LateUpdate()
    {
        if (examinable != null && examinable.Seen)
        {
            foreach (Transform s in stars) if (s != null) s.gameObject.SetActive(false);
            enabled = false;
            return;
        }
        if (viewCamera != null) transform.rotation = viewCamera.rotation;
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            float t = Time.time * speed + phase + i * 1.9f;
            float k = Mathf.Max(0f, Mathf.Sin(t));
            float s = size * (i == 0 ? 1f : 0.55f) * (0.2f + 0.8f * k * k);
            stars[i].localScale = new Vector3(s, s, s);
            stars[i].localRotation = Quaternion.Euler(0f, 0f, t * 25f);
        }
    }
}
