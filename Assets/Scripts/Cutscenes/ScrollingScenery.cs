using UnityEngine;

// The world going by outside a car that doesn't move: its children slide along -forward of this object and wrap
// around after Length meters (trees, poles, street lights). Runs while the object is active.
public class ScrollingScenery : MonoBehaviour
{
    [Tooltip("Meters per second (60 km/h = 16.7).")]
    [SerializeField] private float speed = 14f;
    [Tooltip("Length of the strip: a child that goes past -Length/2 comes back at +Length/2.")]
    [SerializeField] private float length = 120f;
    [Tooltip("Small up and down bumps of the road, applied to this object (m).")]
    [SerializeField] private float bump = 0f;

    // Rest position (the bumps move around it).
    private Vector3 basePosition;

    // Remembers the rest position.
    private void Awake() => basePosition = transform.localPosition;

    // Slides every child backward, wrapping around; adds road bumps.
    private void Update()
    {
        float step = speed * Time.deltaTime;
        float half = length * 0.5f;
        foreach (Transform child in transform)
        {
            Vector3 p = child.localPosition;
            p.z -= step;
            if (p.z < -half) p.z += length;
            child.localPosition = p;
        }
        if (bump > 0f)
            transform.localPosition = basePosition + Vector3.up * (Mathf.PerlinNoise(Time.time * 3f, 0.7f) - 0.5f) * bump;
    }
}
