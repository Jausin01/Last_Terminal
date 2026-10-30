using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The loading screen between story scenes: the CCTV signal being reacquired - static, "SIGNAL LOST / RECONNECTING",
// the camera's destination, a running timecode, a blinking REC and a thin red bar that follows the real load.
// Warm (going into a memory): at 100% the static turns amber and fades away over the new scene, as if the picture
// that came back were not a camera. Cold: it just clears. A prefab (Last Terminal/25 makes it) instantiated by
// LocationFlashback / FlashbackReturn; it survives the scene change and destroys itself at the end.
public class SignalLoader : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string GroupField = nameof(group);
    public const string StaticImageField = nameof(staticImage);
    public const string TintField = nameof(tint);
    public const string BarFillField = nameof(barFill);
    public const string TitleField = nameof(title);
    public const string PlaceField = nameof(place);
    public const string TimecodeField = nameof(timecode);
    public const string RecField = nameof(rec);
    public const string AudioField = nameof(audioSource);

    // The whole overlay, the scrolling static, the color over it, the red bar and the texts.
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RawImage staticImage;
    [SerializeField] private Image tint;
    [SerializeField] private Image barFill;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text place;
    [SerializeField] private TMP_Text timecode;
    [SerializeField] private TMP_Text rec;
    [Tooltip("The static hiss (looping clip on it).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Timing")]
    [Tooltip("Shortest time on screen (s), even when the scene loads at once.")]
    [SerializeField] private float minimumTime = 2.6f;
    [SerializeField] private float fadeIn = 0.35f;
    [Tooltip("Warm: the static turning amber at 100% (s).")]
    [SerializeField] private float warmTime = 1.3f;
    [Tooltip("The overlay fading away over the new scene (s).")]
    [SerializeField] private float fadeOut = 1.8f;
    [SerializeField] private Color amber = new Color(1f, 0.62f, 0.25f, 0.55f);

    // Running.
    private bool running;

    // Loads a scene behind the signal screen. warm: into a memory. The texts in the game's language.
    public void Play(string scene, bool warm, string placeText, bool portuguese) =>
        Play(SceneManager.LoadSceneAsync(scene), warm, placeText, portuguese);

    // Same, for a load already started (GameScenes.OpenLocation).
    public void Play(AsyncOperation load, bool warm, string placeText, bool portuguese)
    {
        if (running || load == null) return;
        running = true;
        DontDestroyOnLoad(gameObject);
        title.text = portuguese ? "SINAL PERDIDO — RECONECTANDO" : "SIGNAL LOST — RECONNECTING";
        place.text = placeText;
        StartCoroutine(Run(load, warm));
    }

    private IEnumerator Run(AsyncOperation load, bool warm)
    {
        load.allowSceneActivation = false;
        group.alpha = 0f;
        tint.color = new Color(amber.r, amber.g, amber.b, 0f);
        barFill.fillAmount = 0f;
        if (audioSource != null) audioSource.Play();
        float t = 0f, shown = 0f;
        // in, then the bar follows the load (never faster than the shortest time)
        while (t < fadeIn) { t += Time.unscaledDeltaTime; group.alpha = t / fadeIn; Animate(t); yield return null; }
        group.alpha = 1f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float real = Mathf.Clamp01(load.progress / 0.9f);
            shown = Mathf.MoveTowards(shown, Mathf.Min(real, t / minimumTime), Time.unscaledDeltaTime * 0.8f);
            barFill.fillAmount = shown;
            Animate(t);
            if (shown >= 1f && real >= 1f) break;
            yield return null;
        }
        // warm: the static turns amber (a memory coming up)
        if (warm)
        {
            float w = 0f;
            while (w < warmTime)
            {
                w += Time.unscaledDeltaTime; t += Time.unscaledDeltaTime;
                tint.color = new Color(amber.r, amber.g, amber.b, amber.a * w / warmTime);
                title.alpha = place.alpha = 1f - w / warmTime;
                Animate(t);
                yield return null;
            }
        }
        load.allowSceneActivation = true;
        while (!load.isDone) { t += Time.unscaledDeltaTime; Animate(t); yield return null; }
        // out over the new scene
        float o = 0f;
        float vol = audioSource != null ? audioSource.volume : 0f;
        while (o < fadeOut)
        {
            o += Time.unscaledDeltaTime; t += Time.unscaledDeltaTime;
            group.alpha = 1f - o / fadeOut;
            if (audioSource != null) audioSource.volume = vol * (1f - o / fadeOut);
            Animate(t);
            yield return null;
        }
        Destroy(gameObject);
    }

    // The static scrolls, the timecode runs, REC blinks.
    private void Animate(float t)
    {
        staticImage.uvRect = new Rect(Random.value, Random.value, 1.6f, 0.9f);
        int s = Mathf.FloorToInt(t * 30f);
        timecode.text = $"CAM --   02:{13 + s / 1800 % 60:00}:{s / 30 % 60:00}:{s % 30:00}";
        rec.alpha = Mathf.Repeat(t, 1f) < 0.55f ? 1f : 0.15f;
    }
}
