using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using L = PinballLayout;

// The ARCANE QUEST pinball machine in the operator's room. Used, he leans over it (OperatorStation) and plays:
// flippers = A / D, Shift or the mouse buttons, plunger = Space (hold, release), Esc = leave (the game pauses).
// The game (PinballTable) runs here; this shows it: the ball, flippers, targets, plunger, the lights (inserts lit with
// a MaterialPropertyBlock), the score display on the backbox, the sounds. High score kept on this computer.
public class PinballMachine : OperatorInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string StationField = nameof(station);
    public const string StoryField = nameof(story);
    public const string ViewPointField = nameof(viewPoint);
    public const string PlayfieldField = nameof(playfield);
    public const string BallField = nameof(ball);
    public const string FlippersField = nameof(flippers);
    public const string TargetsField = nameof(targets);
    public const string PlungerField = nameof(plunger);
    public const string BumperCapsField = nameof(bumperCaps);
    public const string SlingRubbersField = nameof(slingRubbers);
    public const string LaneLightsField = nameof(laneLights);
    public const string TargetLightsField = nameof(targetLights);
    public const string ManaLightsField = nameof(manaLights);
    public const string MultiplierLightsField = nameof(multiplierLights);
    public const string DragonLightsField = nameof(dragonLights);
    public const string ShootAgainField = nameof(shootAgain);
    public const string ExtraBallField = nameof(extraBall);
    public const string EnchantedField = nameof(enchanted);
    public const string ScoreTextField = nameof(scoreText);
    public const string InfoTextField = nameof(infoText);
    public const string AudioField = nameof(audioSource);
    public const string GlowField = nameof(glow);

    // High score key (PlayerPrefs), and the story flag set when a game reaches the score below.
    public const string HighScoreKey = "ArcaneQuest.HighScore";
    public const string ScoreFlag = "pinball_100k";
    public const long ScoreFlagPoints = 100000;

    // The operator station (leans over / leaves) and the story flags.
    [SerializeField] private OperatorStation station;
    [SerializeField] private StoryProgress story;
    [Tooltip("His eyes while playing.")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Table space origin: bottom left corner of the playfield, x right, z up the table, y off the surface.")]
    [SerializeField] private Transform playfield;
    // Moving parts: ball, flippers, drop targets, plunger.
    [SerializeField] private Transform ball;
    [SerializeField] private Transform[] flippers = new Transform[2];
    [SerializeField] private Transform[] targets = new Transform[3];
    [SerializeField] private Transform plunger;
    // Lit parts (inserts and caps), tinted per frame.
    [SerializeField] private Renderer[] bumperCaps = new Renderer[3];
    [SerializeField] private Renderer[] slingRubbers = new Renderer[2];
    [SerializeField] private Renderer[] laneLights = new Renderer[4];
    [SerializeField] private Renderer[] targetLights = new Renderer[3];
    [SerializeField] private Renderer[] manaLights = new Renderer[10];
    [SerializeField] private Renderer[] multiplierLights = new Renderer[4];
    [SerializeField] private Renderer[] dragonLights = new Renderer[3];
    [SerializeField] private Renderer shootAgain;
    [SerializeField] private Renderer extraBall;
    [SerializeField] private Renderer enchanted;
    [Tooltip("The backbox display.")]
    [SerializeField] private TMP_Text scoreText;
    // Backbox message line and the sound source.
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Optional: the glow over the playfield (purple; gold while enchanted).")]
    [SerializeField] private Light glow;
    // High score shown before any game is played.
    [SerializeField] private long defaultHighScore = 150000;

    // Flipper and plunger buttons.
    [Header("Input (bindings added when empty)")]
    [SerializeField] private InputAction leftFlipper = new InputAction("LeftFlipper", InputActionType.Button);
    [SerializeField] private InputAction rightFlipper = new InputAction("RightFlipper", InputActionType.Button);
    [SerializeField] private InputAction launch = new InputAction("Launch", InputActionType.Button);

    // Shader ids and the insert colors.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
    private static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
    private static readonly Color Violet = new Color(0.7f, 0.35f, 1f);
    private static readonly Color Orange = new Color(1f, 0.5f, 0.15f);
    private static readonly Color Red = new Color(1f, 0.2f, 0.15f);
    private static readonly Color Green = new Color(0.35f, 1f, 0.4f);
    private static readonly Color Pink = new Color(1f, 0.35f, 0.8f);

    // The game, the property block for the lights, rest poses of targets / plunger / caps, playing, high score, message and its timer, glow level, and the ball's last world position.
    private readonly PinballTable table = new PinballTable();
    private MaterialPropertyBlock block;
    private Vector3[] targetRest;
    private Vector3 plungerRest;
    private Vector3[] capScale;
    private bool playing;
    private long highScore;
    private string message;
    private float messageTimer;
    private float glowIntensity;
    private Vector3 lastBallWorld;

    // His eyes while playing; a game in progress.
    public Transform ViewPoint => viewPoint;
    public bool IsPlaying => playing;

    // No hand animation for this station.
    public override HandMotion Motion => HandMotion.None;

    // Prompt text.
    public override string Label(GameLanguage language) => Pick(language, "Pinball - ARCANE QUEST", "Fliperama - ARCANE QUEST");

    // Controls hint shown while playing.
    public string Hint(GameLanguage language) => Pick(language,
        "A / D, Shift or mouse buttons: flippers   ·   SPACE: plunger (hold, release)   ·   ESC: leave",
        "A / D, Shift ou botões do mouse: palhetas   ·   ESPAÇO: lançador (segure, solte)   ·   ESC: sair");

    // Builds the table, binds input, reads the high score, hooks the game events.
    private void Awake()
    {
        if (leftFlipper.bindings.Count == 0)
        {
            leftFlipper.AddBinding("<Keyboard>/a"); leftFlipper.AddBinding("<Keyboard>/leftShift");
            leftFlipper.AddBinding("<Keyboard>/leftArrow"); leftFlipper.AddBinding("<Mouse>/leftButton");
        }
        if (rightFlipper.bindings.Count == 0)
        {
            rightFlipper.AddBinding("<Keyboard>/d"); rightFlipper.AddBinding("<Keyboard>/rightShift");
            rightFlipper.AddBinding("<Keyboard>/rightArrow"); rightFlipper.AddBinding("<Mouse>/rightButton");
        }
        if (launch.bindings.Count == 0)
        {
            launch.AddBinding("<Keyboard>/space"); launch.AddBinding("<Keyboard>/s");
            launch.AddBinding("<Keyboard>/downArrow"); launch.AddBinding("<Keyboard>/enter");
        }
        block = new MaterialPropertyBlock();
        if (playfield == null || ball == null)
        {
            Debug.LogError($"{name}: PinballMachine is missing references (run the Last Terminal scene menu).", this);
            enabled = false;
            return;
        }
        targetRest = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++) if (targets[i] != null) targetRest[i] = targets[i].localPosition;
        if (plunger != null) plungerRest = plunger.localPosition;
        capScale = new Vector3[bumperCaps.Length];
        for (int i = 0; i < bumperCaps.Length; i++) if (bumperCaps[i] != null) capScale[i] = bumperCaps[i].transform.localScale;
        if (glow != null) glowIntensity = glow.intensity;
        highScore = long.TryParse(PlayerPrefs.GetString(HighScoreKey, ""), out long saved) ? saved : defaultHighScore;
        table.Played += OnSound;
        table.Message += OnMessage;
        table.GameEnded += OnGameEnded;
        lastBallWorld = BallWorld();
    }

    // Unhooks the game events and frees the input.
    private void OnDestroy()
    {
        table.Played -= OnSound;
        table.Message -= OnMessage;
        table.GameEnded -= OnGameEnded;
    }

    // Leans over the machine and starts / resumes the game.
    public override void Interact(FirstPersonPlayer player)
    {
        if (station != null) station.PlayPinball();
    }

    // OperatorStation: he's at the machine (the game runs) / he left (paused, flippers down)
    public void SetPlaying(bool value)
    {
        playing = value;
        if (value)
        {
            leftFlipper.Enable(); rightFlipper.Enable(); launch.Enable();
            if (table.GameOver) OnMessage("PRESS SPACE", 3f);
        }
        else
        {
            table.SetFlipper(0, false);
            table.SetFlipper(1, false);
            table.SetLaunch(false);
            leftFlipper.Disable(); rightFlipper.Disable(); launch.Disable();
        }
    }

    // Disables the input when the machine is disabled.
    private void OnDisable()
    {
        leftFlipper.Disable(); rightFlipper.Disable(); launch.Disable();
    }

    // Each frame while playing: input to the table, Esc leaves, the simulation steps.
    private void Update()
    {
        if (playing)
        {
            table.SetFlipper(0, leftFlipper.IsPressed());
            table.SetFlipper(1, rightFlipper.IsPressed());
            table.SetLaunch(launch.IsPressed());
            table.Tick(Time.deltaTime);
            if (story != null && table.Score >= ScoreFlagPoints) story.Set(ScoreFlag);
        }
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;
    }

    // After the simulation: moves the parts, lights, score display and glow.
    private void LateUpdate()
    {
        // ball (rolls), flippers, drop targets, plunger
        ball.gameObject.SetActive(table.BallVisible);
        Vector3 world = BallWorld();
        Vector3 moved = world - lastBallWorld;
        if (moved.sqrMagnitude > 1e-8f && moved.sqrMagnitude < 0.04f)
        {
            Vector3 axis = Vector3.Cross(playfield.up, moved).normalized;
            ball.rotation = Quaternion.AngleAxis(moved.magnitude / L.BallRadius * Mathf.Rad2Deg, axis) * ball.rotation;
        }
        ball.position = world;
        lastBallWorld = world;
        for (int i = 0; i < flippers.Length && i < 2; i++)
            if (flippers[i] != null) flippers[i].localRotation = Quaternion.Euler(0f, -table.FlipperAngles[i], 0f);
        for (int i = 0; i < targets.Length && i < 3; i++)
            if (targets[i] != null) targets[i].localPosition = targetRest[i] + (table.TargetDown[i] ? new Vector3(0f, -0.028f, 0f) : Vector3.zero);
        if (plunger != null) plunger.localPosition = plungerRest + new Vector3(0f, 0f, -table.Plunger * L.PlungerTravel);
        Lights();
        Display();
    }

    // The ball position in world space.
    private Vector3 BallWorld() => playfield.TransformPoint(new Vector3(table.BallPos.x, L.BallRadius, table.BallPos.y));

    // ------------------------------------------------------------------ lights
    private void Lights()
    {
        float t = Time.time;
        bool blink = Mathf.Repeat(t * 3f, 1f) < 0.5f;
        bool spell = table.EnchantedLeft > 0f;
        bool attract = table.GameOver;
        int chase = Mathf.FloorToInt(t * 8f);

        for (int i = 0; i < laneLights.Length; i++)
            SetLight(laneLights[i], Gold, attract ? (chase + i) % 4 == 0 : table.LaneLit[i]);
        for (int i = 0; i < targetLights.Length; i++)
            SetLight(targetLights[i], Cyan, attract ? (chase + i) % 3 == 0 : !table.TargetDown[i] && blink || table.TargetDown[i]);
        int manaLit = Mathf.FloorToInt(table.Mana / (float)PinballTable.ManaMax * manaLights.Length);
        for (int i = 0; i < manaLights.Length; i++)
        {
            Color c = Color.Lerp(Violet, Pink, i / (float)Mathf.Max(1, manaLights.Length - 1));
            bool on = attract ? (chase + i) % 5 == 0 : spell ? (chase + i) % 2 == 0 : i < manaLit || (i == manaLit && blink);
            SetLight(manaLights[i], c, on);
        }
        for (int i = 0; i < multiplierLights.Length; i++)
            SetLight(multiplierLights[i], Orange, attract ? (chase + i) % 4 == 0 : table.Multiplier >= i + 2);
        for (int i = 0; i < dragonLights.Length; i++)
            SetLight(dragonLights[i], Red, attract ? (chase + i) % 3 == 0 : table.Dragons % 3 > i || (table.Dragons % 3 == i && blink && !attract));
        SetLight(shootAgain, Red, !attract && (table.BallSaveLeft > 0f ? blink : table.ExtraBalls > 0));
        SetLight(extraBall, Green, !attract && table.Dragons % 3 == 2 && blink);
        SetLight(enchanted, Violet, spell ? blink || table.EnchantedLeft > 3f : attract && Mathf.Repeat(t * 0.5f, 1f) < 0.5f);
        for (int i = 0; i < bumperCaps.Length; i++)
        {
            if (bumperCaps[i] == null) continue;
            bool hit = table.BumperFlash[i] > 0f;
            SetLight(bumperCaps[i], spell ? Gold : Violet, true, hit ? 3.5f : 1f);
            bumperCaps[i].transform.localScale = capScale[i] * (hit ? 1.12f : 1f);
        }
        for (int i = 0; i < slingRubbers.Length; i++)
        {
            if (slingRubbers[i] == null) continue;
            bool kick = table.SlingFlash[i] > 0f;
            block.SetColor(BaseColorId, kick ? Cyan : new Color(0.92f, 0.92f, 0.9f));
            block.SetColor(EmissionId, kick ? Cyan * 3f : Color.black);
            slingRubbers[i].SetPropertyBlock(block);
        }
        if (glow != null)
        {
            glow.color = Color.Lerp(glow.color, spell ? Gold : Violet, Time.deltaTime * 4f);
            glow.intensity = glowIntensity * (spell ? 1.4f + 0.3f * Mathf.Sin(t * 10f) : 1f);
        }
    }

    // Tints a light on / off through the property block.
    private void SetLight(Renderer r, Color c, bool on, float boost = 1f)
    {
        if (r == null) return;
        // inserts are see-through plastic (the art under them shows): alpha in the color
        block.SetColor(BaseColorId, on ? new Color(c.r, c.g, c.b, 0.65f) : new Color(c.r * 0.2f, c.g * 0.2f, c.b * 0.2f, 0.3f));
        block.SetColor(EmissionId, on ? c * 2.4f * boost : Color.black);
        r.SetPropertyBlock(block);
    }

    // ------------------------------------------------------------------ backbox display
    private void Display()
    {
        if (scoreText == null) return;
        if (table.GameOver)
        {
            int page = Mathf.FloorToInt(Time.time / 2.5f) % 3;
            scoreText.text = page == 0 ? "ARCANE QUEST" : page == 1 ? PinballTable.Format(highScore) : PinballTable.Format(table.Score);
            if (infoText != null)
                infoText.text = messageTimer > 0f ? message : page == 0 ? "THE DRAGON'S LAIR" : page == 1 ? "HIGH SCORE" : playing ? "PRESS SPACE" : "LAST GAME";
            return;
        }
        scoreText.text = PinballTable.Format(table.Score);
        if (infoText == null) return;
        if (messageTimer > 0f) infoText.text = message;
        else if (table.EnchantedLeft > 0f) infoText.text = $"ENCHANTED  {Mathf.CeilToInt(table.EnchantedLeft)}";
        else infoText.text = $"BALL {table.BallNumber}   MANA {table.Mana}%   {table.Multiplier}X";
    }

    // Shows a backbox message for a while.
    private void OnMessage(string text, float seconds)
    {
        message = text;
        messageTimer = seconds;
    }

    // Game over: high score saved, story flag set at 100k.
    private void OnGameEnded(long score)
    {
        if (score <= highScore) return;
        highScore = score;
        PlayerPrefs.SetString(HighScoreKey, score.ToString());
        PlayerPrefs.Save();
        OnMessage("NEW HIGH SCORE!", 4f);
        OperatorSounds.Play(audioSource, OperatorSounds.Kind.ExtraBall);
    }

    // Plays the sound of a table event.
    private void OnSound(PinballTable.Sound sound)
    {
        OperatorSounds.Kind kind;
        switch (sound)
        {
            case PinballTable.Sound.Flipper: kind = OperatorSounds.Kind.Flipper; break;
            case PinballTable.Sound.Bumper: kind = OperatorSounds.Kind.Bumper; break;
            case PinballTable.Sound.Sling: kind = OperatorSounds.Kind.Sling; break;
            case PinballTable.Sound.Lane: kind = OperatorSounds.Kind.Lane; break;
            case PinballTable.Sound.Target: kind = OperatorSounds.Kind.Target; break;
            case PinballTable.Sound.Saucer: kind = OperatorSounds.Kind.Saucer; break;
            case PinballTable.Sound.Kickout: kind = OperatorSounds.Kind.Kickout; break;
            case PinballTable.Sound.Launch: kind = OperatorSounds.Kind.Launch; break;
            case PinballTable.Sound.Drain: kind = OperatorSounds.Kind.Drain; break;
            case PinballTable.Sound.Enchanted: kind = OperatorSounds.Kind.Enchanted; break;
            case PinballTable.Sound.BallSaved: kind = OperatorSounds.Kind.BallSaved; break;
            default: kind = OperatorSounds.Kind.ExtraBall; break;
        }
        OperatorSounds.Play(audioSource, kind, sound == PinballTable.Sound.Flipper ? 0.6f : 0.8f);
    }
}
