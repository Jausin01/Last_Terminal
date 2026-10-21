using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Her death on the operator's screen: a red flash on every hit, and when she dies the feed goes "SIGNAL LOST"
// after a moment, with LOAD LAST SAVE (back where she saved, alive) and RESTART (the scene from the start).
public class GameOverScreen : MonoBehaviour
{
    public const string DamageField = nameof(damage);
    public const string HealthField = nameof(health);
    public const string SaveSystemField = nameof(saveSystem);
    public const string GroupField = nameof(group);
    public const string FlashField = nameof(flash);
    public const string DetailField = nameof(detail);
    public const string LoadButtonField = nameof(loadButton);
    public const string RestartButtonField = nameof(restartButton);

    [SerializeField] private PartnerDamage damage;
    [SerializeField] private Health health;
    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private CanvasGroup group;
    [Tooltip("Full-screen red, flashed on each hit.")]
    [SerializeField] private Graphic flash;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private float delay = 2.2f;
    [SerializeField] private float fadeTime = 1.2f;
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.35f;
    [SerializeField] private float flashTime = 0.35f;

    private float flashTimer;
    private float showAt = -1f;

    private void Awake()
    {
        if (damage == null || health == null || saveSystem == null || group == null)
        {
            Debug.LogError($"{name}: GameOverScreen is missing references (run Last Terminal/4).", this);
            enabled = false;
            return;
        }
        Show(false, 0f);
        if (flash != null) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0f);
        if (loadButton != null) loadButton.onClick.AddListener(LoadLastSave);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
    }

    private void OnEnable()
    {
        if (damage == null || health == null) return;
        damage.Died += OnDied;
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (damage == null || health == null) return;
        damage.Died -= OnDied;
        health.Damaged -= OnDamaged;
    }

    private void OnDamaged(int amount) => flashTimer = flashTime;

    private void OnDied()
    {
        showAt = Time.unscaledTime + delay;
        if (loadButton != null) loadButton.interactable = saveSystem.LatestSlot() >= 0;
        if (detail != null) detail.text = $"SUBJECT: NO VITAL SIGNS\n<size=70%>{System.DateTime.Now:HH:mm:ss}  -  FEED TERMINATED</size>";
    }

    private void Update()
    {
        if (flash != null)
        {
            flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
            float a = flashTime > 0f ? flashAlpha * flashTimer / flashTime : 0f;
            flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, a);
        }
        if (showAt < 0f) return;
        float t = Mathf.Clamp01((Time.unscaledTime - showAt) / Mathf.Max(fadeTime, 0.01f));
        if (Time.unscaledTime >= showAt) Show(true, t);
    }

    private void Show(bool on, float alpha)
    {
        group.alpha = on ? alpha : 0f;
        group.interactable = on && alpha >= 1f;
        group.blocksRaycasts = on;
    }

    private void LoadLastSave()
    {
        int slot = saveSystem.LatestSlot();
        if (slot < 0 || !saveSystem.Load(slot)) return;
        damage.Revive();
        showAt = -1f;
        Show(false, 0f);
    }

    private void Restart()
    {
        int index = SceneManager.GetActiveScene().buildIndex;
        if (index < 0) { Debug.LogError("[GameOver] The scene is not in the Build Settings (Last Terminal/4 adds it)."); return; }
        SceneManager.LoadScene(index);
    }
}
