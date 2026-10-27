using System;
using UnityEngine;

// A wall clock: its hands follow the computer's clock (or stand still at a fixed time), with a tick every second.
// Each hand turns around its pivot's Z axis (the pivots' blue arrows point into the wall; 12 o'clock = their up).
public class WallClock : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string HourField = nameof(hour);
    public const string MinuteField = nameof(minute);
    public const string SecondField = nameof(second);
    public const string AudioField = nameof(audioSource);

    // The hands (pivots).
    [SerializeField] private Transform hour;
    [SerializeField] private Transform minute;
    [SerializeField] private Transform second;
    [Tooltip("Optional: the tick (3D, on the clock).")]
    [SerializeField] private AudioSource audioSource;
    // Tick volume.
    [SerializeField, Range(0f, 1f)] private float tickVolume = 0.35f;
    [Tooltip("Stopped at a fixed time instead of the real one.")]
    [SerializeField] private bool stopped;
    // The fixed time when stopped.
    [SerializeField, Range(0, 23)] private int stoppedHour = 3;
    [SerializeField, Range(0, 59)] private int stoppedMinute = 12;

    // Last second ticked.
    private int lastSecond = -1;

    // Sets the hands from the time (real or fixed) and ticks each new second.
    private void Update()
    {
        double h, m, s;
        if (stopped) { h = stoppedHour; m = stoppedMinute; s = 0; }
        else
        {
            DateTime now = DateTime.Now;
            s = now.Second;
            m = now.Minute + s / 60.0;
            h = now.Hour % 12 + m / 60.0;
        }
        Set(hour, (float)(h / 12.0));
        Set(minute, (float)(m / 60.0));
        Set(second, (float)(s / 60.0));
        if (stopped || audioSource == null) return;
        int sec = (int)s;
        if (sec == lastSecond) return;
        lastSecond = sec;
        OperatorSounds.Play(audioSource, OperatorSounds.Kind.Tick, tickVolume);
    }

    // seen from the front (the pivot's Z points away from the viewer), clockwise = negative around Z
    private static void Set(Transform hand, float turn)
    {
        if (hand != null) hand.localRotation = Quaternion.Euler(0f, 0f, -360f * turn);
    }
}
