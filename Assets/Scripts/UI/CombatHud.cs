using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Combat overlay (shown while CombatEncounter is in combat): red frame + "COMBAT" title, the number and health bar
// of every engaged hostile floating over it on the camera feed (the one she is on highlighted), a list of the
// targets and her own health. The floating labels are created here, one per hostile, and reused.
public class CombatHud : MonoBehaviour
{
    public const string EncounterField = nameof(encounter);
    public const string CameraSystemField = nameof(cameraSystem);
    public const string SettingsField = nameof(settings);
    public const string PartnerHealthField = nameof(partnerHealth);
    public const string GroupField = nameof(group);
    public const string LabelsRootField = nameof(labelsRoot);
    public const string TitleField = nameof(title);
    public const string ListField = nameof(list);
    public const string PartnerBarField = nameof(partnerBar);

    [SerializeField] private CombatEncounter encounter;
    [SerializeField] private SecurityCameraSystem cameraSystem;
    [SerializeField] private CameraSettings settings;
    [SerializeField] private Health partnerHealth;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform labelsRoot;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text list;
    [Tooltip("Fill of her health bar (anchored left, its width is scaled).")]
    [SerializeField] private Image partnerBar;

    [Header("Look")]
    [SerializeField] private Color hostile = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color focusColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private Color healthy = new Color(0.4f, 1f, 0.5f);
    [Tooltip("Labels float this high above the hostile's feet (m).")]
    [SerializeField] private float labelHeight = 2.1f;
    [SerializeField] private Vector2 labelBarSize = new Vector2(60f, 6f);
    [SerializeField] private float fadeTime = 0.3f;
    [SerializeField] private float listRefresh = 0.15f;

    private class Label
    {
        public RectTransform Root;
        public TMP_Text Number;
        public RectTransform Fill;
        public Image FillImage;
    }

    private readonly Dictionary<Health, Label> labels = new Dictionary<Health, Label>();
    private readonly List<Label> pool = new List<Label>();
    private readonly StringBuilder sb = new StringBuilder();
    private readonly List<Health> stale = new List<Health>();
    private float partnerBarWidth;
    private float listTimer;

    private void Awake()
    {
        if (encounter == null || cameraSystem == null || group == null || labelsRoot == null)
        {
            Debug.LogError($"{name}: CombatHud needs CombatEncounter, SecurityCameraSystem, a CanvasGroup and a labels root.", this);
            enabled = false;
            return;
        }
        group.alpha = 0f;
        if (partnerBar != null) partnerBarWidth = partnerBar.rectTransform.sizeDelta.x;
    }

    private void OnEnable()
    {
        if (encounter != null) encounter.TargetsChanged += RefreshList;
        if (encounter != null) encounter.FocusChanged += OnFocusChanged;
    }

    private void OnDisable()
    {
        if (encounter != null) encounter.TargetsChanged -= RefreshList;
        if (encounter != null) encounter.FocusChanged -= OnFocusChanged;
    }

    private void OnFocusChanged(Health focus) => RefreshList();

    private void LateUpdate()
    {
        bool on = encounter.InCombat;
        group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));
        if (group.alpha <= 0f) { HideAllLabels(); return; }

        if (title != null)
        {
            bool pt = settings != null && settings.Language == GameLanguage.Portuguese;
            title.text = pt ? "COMBATE" : "COMBAT";
            title.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
        }
        if (partnerBar != null && partnerHealth != null)
        {
            float f = partnerHealth.Max > 0 ? (float)partnerHealth.Current / partnerHealth.Max : 0f;
            Vector2 size = partnerBar.rectTransform.sizeDelta;
            partnerBar.rectTransform.sizeDelta = new Vector2(partnerBarWidth * f, size.y);
            partnerBar.color = Color.Lerp(hostile, healthy, f);
        }
        listTimer -= Time.unscaledDeltaTime;
        if (listTimer <= 0f) RefreshList();
        UpdateLabels();
    }

    // ------------------------------------------------------------------ floating labels
    private void UpdateLabels()
    {
        Camera view = cameraSystem.ViewCamera;
        IReadOnlyList<Health> engaged = encounter.Engaged;

        // labels of hostiles no longer in the fight go back to the pool
        stale.Clear();
        foreach (Health h in labels.Keys)
            if (encounter.NumberOf(h) == 0) stale.Add(h);
        foreach (Health h in stale)
        {
            Label l = labels[h];
            l.Root.gameObject.SetActive(false);
            pool.Add(l);
            labels.Remove(h);
        }

        foreach (Health h in engaged)
        {
            if (!labels.TryGetValue(h, out Label label))
            {
                label = TakeLabel();
                labels.Add(h, label);
            }
            Vector3 screen = view != null ? view.WorldToScreenPoint(h.transform.position + Vector3.up * labelHeight) : new Vector3(0f, 0f, -1f);
            bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            label.Root.gameObject.SetActive(visible);
            if (!visible) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(labelsRoot, screen, null, out Vector2 local);
            label.Root.localPosition = local;
            bool focused = h == encounter.Focus;
            int n = encounter.NumberOf(h);
            label.Number.text = focused ? $"[ {n} ]" : n.ToString();
            label.Number.color = focused ? focusColor : hostile;
            float f = h.Max > 0 ? (float)h.Current / h.Max : 0f;
            label.Fill.sizeDelta = new Vector2(labelBarSize.x * f, labelBarSize.y);
            label.FillImage.color = focused ? focusColor : hostile;
        }
    }

    private void HideAllLabels()
    {
        foreach (Label l in labels.Values) l.Root.gameObject.SetActive(false);
    }

    private Label TakeLabel()
    {
        if (pool.Count > 0)
        {
            Label reused = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return reused;
        }
        var root = new GameObject("TargetLabel", typeof(RectTransform)).GetComponent<RectTransform>();
        root.gameObject.layer = labelsRoot.gameObject.layer;
        root.SetParent(labelsRoot, false);
        root.sizeDelta = new Vector2(labelBarSize.x + 20f, 50f);

        var number = new GameObject("Number", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        number.rectTransform.SetParent(root, false);
        number.rectTransform.sizeDelta = new Vector2(120f, 36f);
        number.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        number.fontSize = 30f;
        number.fontStyle = FontStyles.Bold;
        number.alignment = TextAlignmentOptions.Center;
        number.raycastTarget = false;

        var back = new GameObject("BarBack", typeof(RectTransform)).AddComponent<Image>();
        back.rectTransform.SetParent(root, false);
        back.rectTransform.sizeDelta = labelBarSize;
        back.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        RectTransform fr = fill.rectTransform;
        fr.SetParent(back.rectTransform, false);
        fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.anchoredPosition = Vector2.zero;
        fr.sizeDelta = labelBarSize;
        fill.raycastTarget = false;

        return new Label { Root = root, Number = number, Fill = fr, FillImage = fill };
    }

    // ------------------------------------------------------------------ side list
    private void RefreshList()
    {
        listTimer = listRefresh;
        if (list == null) return;
        sb.Clear();
        string focusHex = ColorUtility.ToHtmlStringRGB(focusColor);
        foreach (Health h in encounter.Engaged)
        {
            int n = encounter.NumberOf(h);
            int pct = h.Max > 0 ? Mathf.RoundToInt(100f * h.Current / h.Max) : 0;
            if (h == encounter.Focus) sb.Append($"<color=#{focusHex}>> #{n}   {pct,3}%</color>\n");
            else sb.Append($"  #{n}   {pct,3}%\n");
        }
        list.text = sb.ToString();
    }
}
