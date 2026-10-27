using System;
using System.Collections.Generic;
using UnityEngine;

// What she is wearing. Every renderer of her model keeps its original materials; an outfit swaps some of them
// and hides some parts. Extras (accessories on her bones) only show with the outfits that list them.
// null = her own clothes. Saved by key (SaveSystem).
// Outfits with a price are prizes: she can only wear them once bought at the casino's prize counter (owned keys
// are saved too). Her own clothes and the free ones (price 0) are always hers.
public class PartnerOutfit : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string PartsField = nameof(parts);
    public const string OutfitsField = nameof(outfits);
    public const string ExtrasField = nameof(extras);
    public const string StartOutfitField = nameof(startOutfit);

    // Key of her own clothes (no outfit).
    public const string DefaultKey = "outfit_default";

    [Tooltip("Every renderer of her model (filled by Last Terminal/4).")]
    [SerializeField] private Renderer[] parts = new Renderer[0];
    // Every outfit she can wear.
    [SerializeField] private OutfitDefinition[] outfits = new OutfitDefinition[0];
    [Tooltip("Accessories on her bones (filled by Last Terminal/4), hidden with her own clothes.")]
    [SerializeField] private Renderer[] extras = new Renderer[0];
    [Tooltip("What she wears when the game starts (the story: the prison uniform). Empty = her own clothes.")]
    [SerializeField] private OutfitDefinition startOutfit;

    // Her parts' original materials and meshes (put back when an outfit doesn't change them).
    private Material[][] originals;
    private Mesh[] originalMeshes;
    // What she wears now (null = her own clothes) and the prizes she owns.
    private OutfitDefinition current;
    private readonly HashSet<string> owned = new HashSet<string>();

    // Raised when she changes clothes / buys a prize.
    public event Action<OutfitDefinition> Changed;
    public event Action OwnedChanged;

    // Can she wear it? (her clothes and free outfits always; prizes once bought)
    public bool IsOwned(OutfitDefinition o) => o == null || o.Price <= 0 || owned.Contains(o.Key);
    // Owned prize keys (saved).
    public IEnumerable<string> OwnedKeys => owned;

    // A prize bought: hers from now on.
    public void Unlock(OutfitDefinition o)
    {
        if (o == null || !owned.Add(o.Key)) return;
        OwnedChanged?.Invoke();
    }

    // loading a save
    public void SetOwned(IEnumerable<string> keys)
    {
        owned.Clear();
        if (keys != null) foreach (string k in keys) if (!string.IsNullOrEmpty(k)) owned.Add(k);
        OwnedChanged?.Invoke();
    }

    // Read by the commands, the HUD and the save system.
    public OutfitDefinition Current => current;
    public string CurrentKey => current != null ? current.Key : DefaultKey;
    public int OutfitCount => outfits.Length;
    public OutfitDefinition GetOutfit(int index) => index >= 0 && index < outfits.Length ? outfits[index] : null;

    // Remembers the original materials / meshes.
    private void Awake() => CacheOriginals();

    // Puts on the start outfit (unless a save already set one).
    private void Start()
    {
        if (startOutfit != null && current == null) Wear(startOutfit);
    }

    // Caches each part's materials and mesh once.
    private void CacheOriginals()
    {
        if (originals != null) return;
        originals = new Material[parts.Length][];
        originalMeshes = new Mesh[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            originals[i] = parts[i] != null ? parts[i].sharedMaterials : new Material[0];
            if (parts[i] is SkinnedMeshRenderer skinned) originalMeshes[i] = skinned.sharedMesh;
        }
    }

    // by lexicon key; DefaultKey = her own clothes. False = no such outfit.
    public bool TryFind(string key, out OutfitDefinition outfit)
    {
        outfit = null;
        if (key == DefaultKey) return true;
        foreach (OutfitDefinition o in outfits)
            if (o != null && o.Key == key) { outfit = o; return true; }
        return false;
    }

    // the one after the current she owns (her clothes -> first outfit -> ... -> her clothes)
    public OutfitDefinition Next()
    {
        int i = current != null ? Array.IndexOf(outfits, current) : -1;
        for (int k = i + 1; k < outfits.Length; k++)
            if (IsOwned(outfits[k])) return outfits[k];
        return null;
    }

    // Puts an outfit on: swapped materials, reshaped meshes, hidden parts, shown accessories.
    public void Wear(OutfitDefinition outfit)
    {
        CacheOriginals();
        current = outfit;
        for (int i = 0; i < parts.Length; i++)
        {
            Renderer r = parts[i];
            if (r == null) continue;
            Material[] mats = (Material[])originals[i].Clone();
            if (outfit != null)
                for (int m = 0; m < mats.Length; m++) mats[m] = outfit.Swap(mats[m]);
            r.sharedMaterials = mats;
            if (r is SkinnedMeshRenderer skinned && originalMeshes[i] != null)
            {
                Mesh reshaped = outfit != null ? outfit.MeshFor(r.gameObject.name) : null;
                skinned.sharedMesh = reshaped != null ? reshaped : originalMeshes[i];
            }
            r.enabled = outfit == null || !outfit.Hides(r.gameObject.name);
        }
        foreach (Renderer e in extras)
            if (e != null) e.enabled = outfit != null && outfit.Shows(e.gameObject.name);
        Changed?.Invoke(current);
    }

    // loading a save
    public void WearKey(string key)
    {
        if (string.IsNullOrEmpty(key) || !TryFind(key, out OutfitDefinition outfit)) outfit = null;
        Wear(outfit);
    }
}
