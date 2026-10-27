using UnityEngine;

// Test target for melee: flashes and wobbles when hit, falls over at 0 HP and stands up again.
// Damage arrives through its Health (any attacker); the visual is a child, so the collider stays still.
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string VisualField = nameof(visual);
    public const string BodyRendererField = nameof(bodyRenderer);

    // The moving part (tilts) and the renderer that flashes.
    [SerializeField] private Transform visual;
    [SerializeField] private Renderer bodyRenderer;
    // Hit flash color and duration.
    [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private float flashTime = 0.12f;
    [Tooltip("Tilt of the hit wobble (deg).")]
    [SerializeField] private float wobbleAngle = 12f;
    // Wobble speed and how fast it dies out.
    [SerializeField] private float wobbleFrequency = 9f;
    [SerializeField] private float wobbleDamping = 6f;
    // Tilt when knocked down, how fast it falls, and the time before it stands up again.
    [SerializeField] private float fallAngle = 80f;
    [SerializeField] private float fallSpeed = 240f;
    [SerializeField] private float standUpDelay = 2.5f;

    // Shader color property for the flash.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Its health, the property block for the flash and the hit / wobble timers.
    private Health health;
    private MaterialPropertyBlock block;
    private float flashTimer;
    private float wobbleTime = -1f;
    private float fallen;          // current fall tilt (deg)
    // Time left before it stands up again.
    private float deadTimer;

    // Gets its Health and a property block.
    private void Awake()
    {
        health = GetComponent<Health>();
        block = new MaterialPropertyBlock();
    }

    // Listens to its health.
    private void OnEnable()
    {
        health.Changed += OnChanged;
        health.Died += OnDied;
    }

    // Stops listening.
    private void OnDisable()
    {
        health.Changed -= OnChanged;
        health.Died -= OnDied;
    }

    // Hit: flash and wobble.
    private void OnChanged(int current, int max)
    {
        if (current >= max) return;   // restored
        Debug.Log($"[Dummy] {name}: {current}/{max} HP", this);
        flashTimer = flashTime;
        wobbleTime = 0f;
    }

    // Down: starts the stand-up timer.
    private void OnDied()
    {
        Debug.Log($"[Dummy] {name} down.", this);
        deadTimer = standUpDelay;
    }

    // Fall / stand up and the hit wobble.
    private void Update()
    {
        UpdateFlash();
        if (visual == null) return;

        // fall over when dead, stand up after the delay with full health
        if (health.IsDead)
        {
            deadTimer -= Time.deltaTime;
            if (deadTimer <= 0f) health.Restore();
        }
        float targetFall = health.IsDead ? fallAngle : 0f;
        fallen = Mathf.MoveTowards(fallen, targetFall, fallSpeed * Time.deltaTime);

        float wobble = 0f;
        if (wobbleTime >= 0f)
        {
            wobbleTime += Time.deltaTime;
            float amp = Mathf.Exp(-wobbleDamping * wobbleTime);
            wobble = wobbleAngle * amp * Mathf.Sin(wobbleTime * wobbleFrequency * 2f * Mathf.PI);
            if (amp < 0.01f) wobbleTime = -1f;
        }
        visual.localRotation = Quaternion.Euler(fallen + wobble, 0f, 0f);
    }

    // Shows the flash color while the timer runs, then clears it.
    private void UpdateFlash()
    {
        if (bodyRenderer == null || (flashTimer <= 0f && block.isEmpty)) return;
        flashTimer -= Time.deltaTime;
        if (flashTimer > 0f)
        {
            block.SetColor(BaseColorId, flashColor);
            bodyRenderer.SetPropertyBlock(block);
        }
        else
        {
            block.Clear();
            bodyRenderer.SetPropertyBlock(null);
        }
    }
}
