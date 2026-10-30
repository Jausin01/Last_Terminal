using UnityEngine;

// A crowd that doesn't move in step: each animator starts one of the states at a random point of its loop and a
// slightly different speed (dancers on a dance floor, people at a bar). Set up by the scene builder.
public class CrowdDancers : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string AnimatorsField = nameof(animators);
    public const string StatesField = nameof(states);

    [SerializeField] private Animator[] animators = new Animator[0];
    [Tooltip("States of their controller, one picked per animator.")]
    [SerializeField] private string[] states = new string[0];
    [Tooltip("Speed range (1 = as authored).")]
    [SerializeField] private Vector2 speed = new Vector2(0.9f, 1.1f);
    [Tooltip("Same crowd every time (0 = different each play).")]
    [SerializeField] private int seed = 7;

    // Each one in its own state, phase and speed.
    private void Start()
    {
        if (states.Length == 0) return;
        var rnd = seed != 0 ? new System.Random(seed) : new System.Random();
        foreach (Animator a in animators)
        {
            if (a == null || a.runtimeAnimatorController == null) continue;
            a.Play(states[rnd.Next(states.Length)], 0, (float)rnd.NextDouble());
            a.speed = Mathf.Lerp(speed.x, speed.y, (float)rnd.NextDouble());
        }
    }
}
