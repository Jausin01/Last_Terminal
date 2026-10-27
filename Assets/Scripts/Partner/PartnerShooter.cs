using System;
using UnityEngine;

// Firearms: fires the equipped RangedWeapon at a point once she is aimed (aim pose blended in and facing it),
// spends the magazine, reloads from the Inventory (whole magazine, or shell by shell for the shotgun).
// Feedback: tracer + muzzle flash + upper-body recoil (procedural, no shooting clips needed), optional sounds.
// Runs its LateUpdate before WeaponHolder, so the recoil moves her chest and the weapon follows the hands.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Inventory))]
public class PartnerShooter : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartnerAnimatorField = nameof(partnerAnimator);
    public const string WeaponHolderField = nameof(weaponHolder);
    public const string EffectsMaterialField = nameof(effectsMaterial);
    public const string AudioSourceField = nameof(audioSource);

    // Her animator (aim / reload layer, chest bone) and the weapon holder.
    [SerializeField] private PartnerAnimator partnerAnimator;
    [SerializeField] private WeaponHolder weaponHolder;
    [Tooltip("Unlit vertex-color material for tracers and muzzle flash (URP: the pipeline's default particle material).")]
    [SerializeField] private Material effectsMaterial;
    // Plays the fire / reload / empty sounds; what a shot can hit.
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("When she can shoot")]
    [Tooltip("Aim pose blend needed before the shot (0..1).")]
    [SerializeField, Range(0f, 1f)] private float aimReadyBlend = 0.9f;
    [Tooltip("Max angle between her facing and the target (deg).")]
    [SerializeField] private float aimReadyAngle = 12f;
    [Tooltip("A shot that can't happen within this time is dropped (s).")]
    [SerializeField] private float pendingTimeout = 1.5f;
    [Tooltip("Clip-driven reload that never reaches its Animator state is dropped after this long (s).")]
    [SerializeField] private float reloadClipTimeout = 2f;

    // How fast the recoil settles back (1/s).
    [Header("Feedback")]
    [SerializeField] private float recoilReturn = 10f;
    // Tracer: how long it shows (s), width and color.
    [SerializeField] private float tracerTime = 0.06f;
    [SerializeField] private float tracerWidth = 0.015f;
    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.5f, 0.8f);
    // Muzzle flash: how long it shows (s), color, length (m) and its light.
    [SerializeField] private float flashTime = 0.05f;
    [SerializeField] private Color flashColor = new Color(1f, 0.75f, 0.35f, 1f);
    [SerializeField] private float flashLength = 0.18f;
    [SerializeField] private float flashLightRange = 5f;
    [SerializeField] private float flashLightIntensity = 4f;

    // Tracers in the pool; max targets one shot (pellets) can damage.
    private const int TracerCount = 12;
    private const int MaxTargetsPerShot = 8;

    // Ammo reserve, her own health (never shot), the chest bone (recoil) and the rounds in each weapon's magazine.
    private Inventory inventory;
    private Health self;
    private Transform chest;
    private int[] rounds;

    // Reload state: on, which weapon, timer, clip started, magazine done, shell loop counted. Fire cooldown, the shot waiting for her aim, and the current recoil.
    private bool reloading;
    private int reloadIndex = -1;
    private float reloadTimer;
    private bool reloadClipStarted;
    private bool reloadMagDone;
    private int reloadLastLoop;
    private float fireTimer;
    private bool pending;
    private float pendingTimer;
    private Vector3 pendingTarget;
    private float recoilAngle;

    // Effect pool: tracers and their timers, next one to use, the flash line / light and its timer. Per-shot damage buffers.
    private LineRenderer[] tracers;
    private float[] tracerTimers;
    private int nextTracer;
    private LineRenderer flash;
    private Light flashLight;
    private float flashTimer;
    private readonly Health[] shotTargets = new Health[MaxTargetsPerShot];
    private readonly int[] shotDamage = new int[MaxTargetsPerShot];

    // Raised on a shot, an empty trigger, reload start / end.
    public event Action<RangedWeapon> Fired;
    public event Action<RangedWeapon> OutOfAmmo;
    public event Action ReloadStarted;
    public event Action ReloadFinished;
    public event Action AmmoChanged;             // magazine or reserve changed
    public event Action<Health, int> Hit;        // target, damage of the shot

    // Read-only state for combat and the HUD.
    public bool IsReloading => reloading;
    public bool HasPendingShot => pending;
    public bool IsBusy => reloading || pending;

    // Ranged weapon in her hand (null for melee / unarmed).
    public RangedWeapon Current =>
        weaponHolder.IsEquipped && weaponHolder.Current.TryGetComponent(out RangedWeapon r) ? r : null;

    // Rounds in the magazine / ammo left in the inventory for the weapon in hand.
    public int CurrentRounds => Current != null ? rounds[weaponHolder.CurrentIndex] : 0;
    public int CurrentReserve => Current != null && Current.Ammo != null ? inventory.Count(Current.Ammo) : 0;

    // Rounds in a weapon's magazine (save / load).
    public int GetRounds(int loadoutIndex) =>
        loadoutIndex >= 0 && loadoutIndex < rounds.Length ? rounds[loadoutIndex] : 0;

    // Sets a weapon's magazine (save / load), clamped to its size.
    public void SetRounds(int loadoutIndex, int value)
    {
        RangedWeapon r = RangedAt(loadoutIndex);
        if (r == null) return;
        rounds[loadoutIndex] = Mathf.Clamp(value, 0, r.MagazineSize);
        AmmoChanged?.Invoke();
    }

    // Gets the inventory and health; checks references; builds the effects.
    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        self = GetComponent<Health>();
        if (partnerAnimator == null || weaponHolder == null)
        {
            Debug.LogError($"{name}: PartnerShooter needs a PartnerAnimator and a WeaponHolder.", this);
            enabled = false;
            return;
        }
        // full magazines at the start
        rounds = new int[weaponHolder.LoadoutCount];
        for (int i = 0; i < rounds.Length; i++)
        {
            RangedWeapon r = RangedAt(i);
            rounds[i] = r != null ? r.MagazineSize : 0;
        }
        CreateEffects();
    }

    // Starts every magazine full.
    private void Start()
    {
        chest = partnerAnimator.GetBone(HumanBodyBones.Chest);
        if (chest == null) chest = partnerAnimator.GetBone(HumanBodyBones.Spine);
    }

    // Listens to the inventory (HUD ammo count).
    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += OnInventoryChanged;
    }

    // Stops listening.
    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= OnInventoryChanged;
    }

    // Reserve changed: the HUD updates.
    private void OnInventoryChanged() => AmmoChanged?.Invoke();

    // Shoot at this point as soon as she is aimed (the caller makes her aim: PartnerCombat.Fire).
    public bool RequestFire(Vector3 target)
    {
        RangedWeapon r = Current;
        if (!enabled || r == null) return false;
        // shell by shell: a shot interrupts the reload if there is something to fire
        if (reloading && r.ReloadPerRound && CurrentRounds > 0) FinishReload(true);
        if (reloading) return false;
        pending = true;
        pendingTimer = pendingTimeout;
        pendingTarget = target;
        return true;
    }

    // Drops the shot waiting for her aim.
    public void CancelPending() => pending = false;

    // Starts a reload (needs reserve ammo and a magazine not full).
    public bool Reload()
    {
        RangedWeapon r = Current;
        if (!enabled || r == null || reloading) return false;
        int index = weaponHolder.CurrentIndex;
        if (rounds[index] >= r.MagazineSize || r.Ammo == null || !inventory.Has(r.Ammo)) return false;

        pending = false;
        reloading = true;
        reloadIndex = index;
        reloadTimer = r.ReloadTime;
        reloadClipStarted = false;
        reloadMagDone = false;
        reloadLastLoop = -1;
        if (r.Style != RangedWeapon.ReloadMode.Timer)
        {
            partnerAnimator.ClearReloadAbort();
            partnerAnimator.SetReloading(true);
            reloadTimer = reloadClipTimeout;
        }
        Play(r.ReloadSound);
        ReloadStarted?.Invoke();
        return true;
    }

    // Each frame: reload, the pending shot, cooldown and effects.
    private void Update()
    {
        if (fireTimer > 0f) fireTimer -= Time.deltaTime;
        UpdateReload();
        UpdatePending();
    }

    // Clip-driven reload: magazine at the clip's mark, shell by shell for loop reloads.
    private void UpdateReload()
    {
        if (!reloading) return;
        RangedWeapon r = Current;
        if (r == null || weaponHolder.CurrentIndex != reloadIndex) { FinishReload(true); return; }   // switched / holstered

        if (r.Style == RangedWeapon.ReloadMode.Timer) { UpdateTimerReload(r); return; }

        // clip-driven: the Animator's reload state gives the moments
        reloadTimer -= Time.deltaTime;
        if (!partnerAnimator.TryGetReloadTime(out float t))
        {
            if (reloadClipStarted) FinishReload(false);   // clip over
            else if (reloadTimer <= 0f)
            {
                // the clip never started (Animator not rebuilt?): load anyway, like the timer style
                Debug.LogWarning($"{name}: reload clip did not play (run Last Terminal/2), magazine loaded without it.", this);
                int n = Mathf.Min(r.MagazineSize - rounds[reloadIndex], inventory.Count(r.Ammo));
                if (n > 0 && inventory.TryRemove(r.Ammo, n)) rounds[reloadIndex] += n;
                AmmoChanged?.Invoke();
                FinishReload(true);
            }
            return;
        }
        reloadClipStarted = true;

        if (r.Style == RangedWeapon.ReloadMode.Magazine)
        {
            if (!reloadMagDone && t >= r.MagInsert)
            {
                reloadMagDone = true;
                int n = Mathf.Min(r.MagazineSize - rounds[reloadIndex], inventory.Count(r.Ammo));
                if (n > 0 && inventory.TryRemove(r.Ammo, n)) rounds[reloadIndex] += n;
                partnerAnimator.SetReloading(false);   // the clip plays to its end, then back to the weapon pose
                AmmoChanged?.Invoke();
            }
            return;
        }

        // shells: one per loop of the clip, at the push
        int loop = Mathf.FloorToInt(t);
        if (loop > reloadLastLoop && t - loop >= r.ShellInsert)
        {
            reloadLastLoop = loop;
            if (inventory.TryRemove(r.Ammo, 1)) rounds[reloadIndex]++;
            AmmoChanged?.Invoke();
            if (rounds[reloadIndex] >= r.MagazineSize || !inventory.Has(r.Ammo))
                partnerAnimator.SetReloading(false);   // finishes this loop, then leaves
            else Play(r.ReloadSound);
        }
    }

    // Timed reload (no clip): fills after the weapon's reload time.
    private void UpdateTimerReload(RangedWeapon r)
    {
        reloadTimer -= Time.deltaTime;
        if (reloadTimer > 0f) return;

        if (r.ReloadPerRound)
        {
            if (inventory.TryRemove(r.Ammo, 1)) rounds[reloadIndex]++;
            AmmoChanged?.Invoke();
            if (rounds[reloadIndex] < r.MagazineSize && inventory.Has(r.Ammo))
            {
                reloadTimer = r.ReloadTime;
                Play(r.ReloadSound);
                return;
            }
        }
        else
        {
            int n = Mathf.Min(r.MagazineSize - rounds[reloadIndex], inventory.Count(r.Ammo));
            if (n > 0 && inventory.TryRemove(r.Ammo, n)) rounds[reloadIndex] += n;
            AmmoChanged?.Invoke();
        }
        FinishReload(false);
    }

    // abort = leave the reload clip at once (shot, weapon switch)
    private void FinishReload(bool abort)
    {
        if (!reloading) return;
        reloading = false;
        reloadIndex = -1;
        if (abort) partnerAnimator.AbortReload();
        else partnerAnimator.SetReloading(false);
        ReloadFinished?.Invoke();
    }

    // Fires the waiting shot once she is aimed (gives up after a timeout).
    private void UpdatePending()
    {
        if (!pending) return;
        RangedWeapon r = Current;
        pendingTimer -= Time.deltaTime;
        if (r == null || pendingTimer <= 0f) { pending = false; return; }
        if (fireTimer > 0f || reloading || !IsAimedAt(pendingTarget)) return;

        pending = false;
        int index = weaponHolder.CurrentIndex;
        if (rounds[index] <= 0)
        {
            Play(r.EmptySound);
            OutOfAmmo?.Invoke(r);
            Reload();   // automatic when she has ammo for it
            return;
        }
        Shoot(r, index, pendingTarget);
    }

    // Weapon raised and her facing within the aim tolerance of the target?
    private bool IsAimedAt(Vector3 target)
    {
        if (partnerAnimator.AimBlend < aimReadyBlend) return false;
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return true;
        return Vector3.Angle(transform.forward, to) <= aimReadyAngle;
    }

    // Fires: ray(s) from the muzzle, damage summed per target, ammo spent, effects and events.
    private void Shoot(RangedWeapon r, int index, Vector3 target)
    {
        rounds[index]--;
        fireTimer = r.FireInterval;

        Vector3 origin = r.Muzzle.position;
        Vector3 aim = target - origin;
        if (aim.sqrMagnitude < 0.01f) aim = transform.forward;
        aim.Normalize();

        int targetCount = 0;
        for (int p = 0; p < r.Pellets; p++)
        {
            Vector3 dir = Scatter(aim, r.Spread);
            Vector3 end = origin + dir * r.Range;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, r.Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && h != self && !h.IsDead) targetCount = AddDamage(h, r.Damage, targetCount);
            }
            ShowTracer(origin, end);
        }
        // one damage event per target per shot (8 pellets on the same dummy = one big hit)
        for (int i = 0; i < targetCount; i++)
        {
            shotTargets[i].TakeDamage(shotDamage[i]);
            Hit?.Invoke(shotTargets[i], shotDamage[i]);
            shotTargets[i] = null;
        }

        recoilAngle = Mathf.Min(recoilAngle + r.Recoil, 25f);
        r.CyclePump();
        ShowFlash(origin, aim);
        Play(r.FireSound);
        Fired?.Invoke(r);
        AmmoChanged?.Invoke();
    }

    // Adds damage to a target already hit by this shot, or adds the target; returns the new count.
    private int AddDamage(Health target, int damage, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (shotTargets[i] != target) continue;
            shotDamage[i] += damage;
            return count;
        }
        if (count >= MaxTargetsPerShot) return count;
        shotTargets[count] = target;
        shotDamage[count] = damage;
        return count + 1;
    }

    // random direction inside a cone around dir (uniform over the disc)
    private static Vector3 Scatter(Vector3 dir, float coneAngle)
    {
        if (coneAngle <= 0f) return dir;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(dir, Vector3.right);
        side = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), dir) * side.normalized;
        float angle = coneAngle * Mathf.Sqrt(UnityEngine.Random.value);
        return Quaternion.AngleAxis(angle, side) * dir;
    }

    // After the animation: upper-body recoil on her chest.
    private void LateUpdate()
    {
        // recoil: kick the chest up around her right axis, then let it settle
        if (recoilAngle > 0.01f && chest != null)
        {
            chest.rotation = Quaternion.AngleAxis(-recoilAngle, transform.right) * chest.rotation;
            recoilAngle *= Mathf.Exp(-recoilReturn * Time.deltaTime);
        }
        else recoilAngle = 0f;

        UpdateEffects();
    }

    // ------------------------------------------------------------------ effects (built once, reused)
    private void CreateEffects()
    {
        var root = new GameObject("ShotEffects").transform;
        root.SetParent(transform, false);

        tracers = new LineRenderer[TracerCount];
        tracerTimers = new float[TracerCount];
        for (int i = 0; i < TracerCount; i++)
        {
            tracers[i] = CreateLine(root, "Tracer");
            tracers[i].startColor = tracerColor;
            tracers[i].endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0.1f);
        }

        flash = CreateLine(root, "MuzzleFlash");
        flash.startColor = flashColor;
        flash.endColor = new Color(flashColor.r, flashColor.g * 0.6f, flashColor.b * 0.3f, 0f);

        var lightGo = new GameObject("MuzzleLight");
        lightGo.transform.SetParent(root, false);
        flashLight = lightGo.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.range = flashLightRange;
        flashLight.intensity = flashLightIntensity;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
    }

    // Builds one unlit line (tracer / flash).
    private LineRenderer CreateLine(Transform parent, string lineName)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.sharedMaterial = effectsMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    // Shows a tracer from the pool.
    private void ShowTracer(Vector3 from, Vector3 to)
    {
        LineRenderer line = tracers[nextTracer];
        tracerTimers[nextTracer] = tracerTime;
        nextTracer = (nextTracer + 1) % TracerCount;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth * 0.5f;
        line.enabled = true;
    }

    // Shows the muzzle flash and its light.
    private void ShowFlash(Vector3 origin, Vector3 dir)
    {
        flashTimer = flashTime;
        flash.SetPosition(0, origin);
        flash.SetPosition(1, origin + dir * flashLength);
        flash.startWidth = flashLength * 0.7f;
        flash.endWidth = 0.01f;
        flash.enabled = true;
        flashLight.transform.position = origin + dir * 0.1f;
        flashLight.enabled = true;
    }

    // Fades the tracers and flash out.
    private void UpdateEffects()
    {
        for (int i = 0; i < TracerCount; i++)
        {
            if (!tracers[i].enabled) continue;
            tracerTimers[i] -= Time.deltaTime;
            if (tracerTimers[i] <= 0f) tracers[i].enabled = false;
        }
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                flash.enabled = false;
                flashLight.enabled = false;
            }
        }
    }

    // Plays a one-shot sound if there is one.
    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    // The ranged weapon of a loadout slot (null if melee / empty).
    private RangedWeapon RangedAt(int index)
    {
        Weapon w = weaponHolder.GetWeapon(index);
        return w != null && w.TryGetComponent(out RangedWeapon r) ? r : null;
    }
}
