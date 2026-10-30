using UnityEngine;

// The title camera breathing a little (slow sway and drift around its placed pose), like a held look at the desk.
public class MenuCameraSway : MonoBehaviour
{
    // How far it moves (m) and turns (degrees), and how fast (cycles per second).
    [SerializeField] private float move = 0.012f;
    [SerializeField] private float turn = 0.6f;
    [SerializeField] private float speed = 0.07f;

    private Vector3 basePosition;
    private Quaternion baseRotation;

    // The pose it sways around.
    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    // Two slow sines per axis (never quite repeating).
    private void LateUpdate()
    {
        float t = Time.unscaledTime * speed * Mathf.PI * 2f;
        transform.localPosition = basePosition + new Vector3(Mathf.Sin(t) * move, Mathf.Sin(t * 0.73f + 1f) * move * 0.6f, 0f);
        transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t * 0.61f + 2f) * turn * 0.5f, Mathf.Sin(t * 0.83f) * turn, 0f);
    }
}
