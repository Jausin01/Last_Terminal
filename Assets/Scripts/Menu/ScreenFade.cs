using System.Collections;
using UnityEngine;

// A full-screen black overlay that fades the scene in when it starts and out before a scene change.
// On the CanvasGroup of a black Image covering the canvas (it never blocks clicks).
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFade : MonoBehaviour
{
    [Tooltip("Seconds of one fade (unscaled time).")]
    [SerializeField] private float duration = 0.8f;
    [Tooltip("Start black and fade in when the scene starts.")]
    [SerializeField] private bool fadeInOnStart = true;

    private CanvasGroup group;

    // Black from the first frame (when it fades in).
    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = fadeInOnStart ? 1f : 0f;
    }

    // The scene appears.
    private void Start()
    {
        if (fadeInOnStart) StartCoroutine(FadeTo(0f));
    }

    // To black (yield it before loading a scene).
    public IEnumerator Out() => FadeTo(1f);

    // From the current alpha to the target over `duration`.
    private IEnumerator FadeTo(float target)
    {
        float from = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }
}
