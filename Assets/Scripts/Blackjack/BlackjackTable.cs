using System;
using System.Collections.Generic;
using UnityEngine;

// The physical table: where things go (in this object's space: origin = the dealer's seat, +z toward the player,
// felt at 0.75 m) and the card / chip objects on it. Cards and chips are pooled here and moved with small tweens
// (slide with a little arc, flip). Card faces / chip values are picked on the shared atlases with a
// MaterialPropertyBlock (_BaseMap_ST), so all cards share one material.
public class BlackjackTable : MonoBehaviour
{
    public const string SeatField = nameof(seat);
    public const string CardPrefabField = nameof(cardPrefab);
    public const string ChipPrefabField = nameof(chipPrefab);
    public const string DeckField = nameof(deck);
    public const string CardAtlasField = nameof(cardAtlas);
    public const string TableCameraField = nameof(tableCamera);
    public const string ShoeField = nameof(shoe);
    public const string ChipTrayField = nameof(chipTray);

    // atlases (tools: T_Cards 1024x512, cells 72x100, rows S H D C, columns A..K, back in column 13;
    // T_Chips 512x512, cells 128x160, values below)
    public static readonly int[] ChipValues = { 1000, 500, 100, 25, 5, 1 };
    private static readonly int[] ChipCell = { 5, 4, 3, 2, 1, 0 };     // atlas cell of each value above
    private const float CardCellU = 72f / 1024f, CardCellV = 100f / 512f;
    private const float ChipCellU = 128f / 512f, ChipCellV = 160f / 512f;
    private const float ChipHeight = 0.0046f;
    private const int ChipsPerStack = 12;

    [Tooltip("Her seat (a SaveStation used as a chair: she sits with the save point routine).")]
    [SerializeField] private SaveStation seat;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject chipPrefab;
    [Tooltip("Deck she holds while dealing (shown in her left hand).")]
    [SerializeField] private Transform deck;
    [Tooltip("Card atlas (for the HUD).")]
    [SerializeField] private Texture cardAtlas;
    [Tooltip("Camera at the player's seat, looking at her: switched on (and to) while a game is on.")]
    [SerializeField] private SecurityCamera tableCamera;

    [Header("Layout (local space, measured on SM_BlackjackTable)")]
    [SerializeField] private Vector3 shoe = new Vector3(0.40f, 0.81f, 0.50f);
    [SerializeField] private Vector3 discard = new Vector3(-0.54f, 0.82f, 0.44f);
    [SerializeField] private Vector3 chipTray = new Vector3(-0.23f, 0.772f, 0.38f);
    [SerializeField] private Vector3 dealerCards = new Vector3(-0.04f, 0.751f, 0.50f);
    [SerializeField] private float dealerSpacing = 0.06f;
    [SerializeField] private Vector3 playerCards = new Vector3(0f, 0.751f, 0.92f);
    [SerializeField] private Vector2 cardCascade = new Vector2(0.024f, -0.034f);   // x, z per card of a hand
    [SerializeField] private float handSpacing = 0.21f;
    [SerializeField] private Vector3 betSpot = new Vector3(0f, 0.751f, 1.03f);
    [SerializeField] private Vector3 insuranceSpot = new Vector3(0.20f, 0.751f, 0.81f);
    [Tooltip("Where chips go when they are handed to the player (off the rail).")]
    [SerializeField] private Vector3 playerSide = new Vector3(0f, 0.85f, 1.25f);

    public class CardVisual
    {
        public Card Card;
        public Transform Transform;
        public Renderer Front;
        public bool FaceUp;
        public bool Blank;     // both sides show the back (her hole card until it is revealed)
    }

    private class Tween
    {
        public Transform Target;
        public Vector3 From, To;
        public Quaternion FromRot, ToRot;
        public float Time, Duration, Arc;
        public bool DisableAtEnd;
        public Action Done;
    }

    private readonly List<Tween> tweens = new List<Tween>();
    private readonly List<CardVisual> cardPool = new List<CardVisual>();
    private readonly List<GameObject> chipPool = new List<GameObject>();
    private MaterialPropertyBlock block;
    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    public SaveStation Seat => seat;
    public Texture CardAtlas => cardAtlas;
    public Transform Deck => deck;
    public SecurityCamera TableCamera => tableCamera;
    public bool IsMoving => tweens.Count > 0;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (seat == null || cardPrefab == null || chipPrefab == null)
        {
            Debug.LogError($"{name}: BlackjackTable needs its seat, the card prefab and the chip prefab.", this);
            enabled = false;
        }
    }

    // ------------------------------------------------------------------ places (world)
    public Vector3 World(Vector3 local) => transform.TransformPoint(local);
    public Vector3 ShoePosition => World(shoe);
    public Vector3 DealerCardPosition(int index) => World(dealerCards + new Vector3(index * dealerSpacing, index * 0.0004f, 0f));
    public Vector3 PlayerCardPosition(int hand, int hands, int index) =>
        World(playerCards + new Vector3(HandX(hand, hands) + index * cardCascade.x, index * 0.0004f, index * cardCascade.y));
    public Vector3 BetPosition(int hand, int hands, int stack) => World(betSpot + new Vector3(HandX(hand, hands) + stack * 0.062f, 0f, 0f));
    public Vector3 InsurancePosition => World(insuranceSpot);
    public Vector3 TrayPosition => World(chipTray);
    public Vector3 PlayerSidePosition => World(playerSide);
    public Vector3 DiscardPosition => World(discard);
    private float HandX(int hand, int hands) => (hand - (hands - 1) * 0.5f) * handSpacing;

    // card lying on the felt, readable from the player's side; face down = flipped over its length
    public Quaternion CardRotation(bool faceUp, float yaw = 0f) =>
        transform.rotation * Quaternion.Euler(0f, yaw, faceUp ? 0f : 180f);

    // ------------------------------------------------------------------ cards
    public CardVisual SpawnCard(Card card, Vector3 position, Quaternion rotation, bool faceUp, bool blank = false)
    {
        CardVisual v = cardPool.Find(c => !c.Transform.gameObject.activeSelf);
        if (v == null)
        {
            GameObject go = Instantiate(cardPrefab, transform);
            Transform front = go.transform.Find("Front");
            v = new CardVisual { Transform = go.transform, Front = front != null ? front.GetComponent<Renderer>() : go.GetComponentInChildren<Renderer>() };
            cardPool.Add(v);
        }
        v.Card = card;
        v.FaceUp = faceUp;
        v.Blank = blank;
        v.Transform.SetParent(transform, true);
        v.Transform.SetPositionAndRotation(position, rotation);
        v.Transform.gameObject.SetActive(true);
        SetCardFace(v.Front, blank ? CardBackUv : CardUv(card));
        return v;
    }

    // atlas cell of a card (for the HUD too): x, y, width, height in 0..1
    public static Rect CardUv(Card card) =>
        new Rect((card.Rank - 1) * CardCellU, 1f - ((int)card.Suit + 1) * CardCellV, CardCellU, CardCellV);

    public static Rect CardBackUv => new Rect(13 * CardCellU, 1f - CardCellV, CardCellU, CardCellV);

    private void SetCardFace(Renderer front, Rect r)
    {
        if (front == null) return;
        block.Clear();
        Vector4 st = new Vector4(r.width, r.height, r.x, r.y);
        block.SetVector(BaseMapSt, st);
        block.SetVector(MainTexSt, st);
        front.SetPropertyBlock(block);
    }

    public void MoveCard(CardVisual card, Vector3 to, bool faceUp, float duration, float arc = 0.06f, Action done = null, float yaw = 0f)
    {
        card.FaceUp = faceUp;
        if (faceUp && card.Blank)
        {
            card.Blank = false;   // turned over: now it gets its face
            SetCardFace(card.Front, CardUv(card.Card));
        }
        card.Transform.SetParent(transform, true);
        StartTween(card.Transform, to, CardRotation(faceUp, yaw), duration, arc, false, done);
    }

    public void HideCard(CardVisual card) => card.Transform.gameObject.SetActive(false);

    // ------------------------------------------------------------------ chips
    // A pile of chips for an amount (fewest chips, biggest values first), stacks of 12 side by side.
    public List<GameObject> SpawnChips(int amount, Vector3 position)
    {
        var chips = new List<GameObject>();
        int stack = 0, inStack = 0;
        for (int i = 0; i < ChipValues.Length && amount > 0; i++)
        {
            while (amount >= ChipValues[i])
            {
                amount -= ChipValues[i];
                Vector3 p = position + transform.right * (stack * 0.058f) + Vector3.up * (inStack * ChipHeight);
                chips.Add(SpawnChip(i, p));
                if (++inStack >= ChipsPerStack) { inStack = 0; stack++; }
            }
        }
        return chips;
    }

    private GameObject SpawnChip(int valueIndex, Vector3 position)
    {
        GameObject go = chipPool.Find(c => !c.activeSelf);
        if (go == null)
        {
            go = Instantiate(chipPrefab, transform);
            chipPool.Add(go);
        }
        go.transform.SetParent(transform, true);
        go.transform.SetPositionAndRotation(position, transform.rotation * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        go.SetActive(true);
        PaintChip(go.GetComponentInChildren<Renderer>(), valueIndex, block);
        return go;
    }

    // a chip's face for its value (ChipValues index), on the shared chip atlas (the roulette uses it too)
    public static void PaintChip(Renderer r, int valueIndex, MaterialPropertyBlock block)
    {
        if (r == null) return;
        int cell = ChipCell[valueIndex];
        block.Clear();
        Vector4 st = new Vector4(ChipCellU, ChipCellV, (cell % 4) * ChipCellU, 1f - (cell / 4 + 1) * ChipCellV);
        block.SetVector(BaseMapSt, st);
        block.SetVector(MainTexSt, st);
        r.SetPropertyBlock(block);
    }

    // moves a pile keeping its shape (offset of each chip from the first one)
    public void MoveChips(List<GameObject> chips, Vector3 to, float duration, bool removeAtEnd, Action done = null)
    {
        if (chips == null || chips.Count == 0) { done?.Invoke(); return; }
        Vector3 origin = chips[0].transform.position;
        for (int i = 0; i < chips.Count; i++)
        {
            Transform t = chips[i].transform;
            StartTween(t, to + (t.position - origin), t.rotation, duration, 0.05f, removeAtEnd, i == 0 ? done : null);
        }
    }

    public void RemoveChips(List<GameObject> chips)
    {
        if (chips == null) return;
        foreach (GameObject c in chips) if (c != null) c.SetActive(false);
        chips.Clear();
    }

    // ------------------------------------------------------------------ everything off the table at once
    public void ClearAll()
    {
        tweens.Clear();
        foreach (CardVisual c in cardPool) c.Transform.gameObject.SetActive(false);
        foreach (GameObject c in chipPool) c.SetActive(false);
    }

    // ------------------------------------------------------------------ tweens
    private void StartTween(Transform target, Vector3 to, Quaternion toRot, float duration, float arc, bool disableAtEnd, Action done)
    {
        tweens.RemoveAll(t => t.Target == target);
        tweens.Add(new Tween
        {
            Target = target, From = target.position, To = to, FromRot = target.rotation, ToRot = toRot,
            Duration = Mathf.Max(duration, 0.01f), Arc = arc, DisableAtEnd = disableAtEnd, Done = done,
        });
    }

    private readonly List<Action> finished = new List<Action>();

    private void Update()
    {
        finished.Clear();
        for (int i = tweens.Count - 1; i >= 0; i--)
        {
            Tween t = tweens[i];
            if (t.Target == null) { tweens.RemoveAt(i); continue; }
            t.Time += Time.deltaTime;
            float k = Mathf.Clamp01(t.Time / t.Duration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            t.Target.SetPositionAndRotation(Vector3.Lerp(t.From, t.To, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * t.Arc),
                                            Quaternion.Slerp(t.FromRot, t.ToRot, e));
            if (k < 1f) continue;
            tweens.RemoveAt(i);
            if (t.DisableAtEnd) t.Target.gameObject.SetActive(false);
            if (t.Done != null) finished.Add(t.Done);
        }
        // after the loop: a callback may start new tweens
        foreach (Action done in finished) done();
    }
}
