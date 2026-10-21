using System;
using UnityEngine;

// What she is wearing. Every renderer of her model keeps its original materials; an outfit swaps some of them
// and hides some parts. Extras (accessories on her bones) only show with the outfits that list them.
// null = her own clothes. Saved by key (SaveSystem).
// Outfits with a price are prizes: she can only wear them once bought at the casino's prize counter (owned keys
// are saved too). Her own clothes and the free ones (price 0) are always hers.
public class PartnerOutfit : MonoBehaviour
{
    public const string PartsField = nameof(parts);
    public const string OutfitsField = nameof(outfits);
    public const string ExtrasField = nameof(extras);
    public const string StartOutfitField = nameof(startOutfit);

    public const string DefaultKey = "outfit_default";

    [Tooltip("Every renderer of her model (filled by Last Terminal/4).")]
    [SerializeField] private Renderer[] parts = new Renderer[0];
    [SerializeField] private OutfitDefinition[] outfits = new OutfitDefinition[0];
    [Tooltip("Accessories on her bones (filled by Last Terminal/4), hidden with her own clothes.")]
    [SerializeField] private Renderer[] extras = new Renderer[0];
    [Tooltip("What she wears when the game starts (the story: the prison uniform). Empty = her own clothes.")]
    [SerializeField] private OutfitDefinition startOutfit;

    private Material[][] originals;
    private Mesh[] originalMeshes;
    private OutfitDefinition current;
    private readonly System.Collections.Generic.HashSet<string> owned = new System.Collections.Generic.HashSet<string>();

    public event Action<OutfitDefinition> Changed;
    public event Action OwnedChanged;

    public bool IsOwned(OutfitDefinition o) => o == null || o.Price <= 0 || owned.Contains(o.Key);
    public System.Collections.Generic.IEnumerable<string> OwnedKeys => owned;

    public void Unlock(OutfitDefinition o)
    {
        if (o == null || !owned.Add(o.Key)) return;
        OwnedChanged?.Invoke();
    }

    // loading a save
    public void SetOwned(System.Collections.Generic.IEnumerable<string> keys)
    {
        owned.Clear();
        if (keys != null) foreach (string k in keys) if (!string.IsNullOrEmpty(k)) owned.Add(k);
        OwnedChanged?.Invoke();
    }

    public OutfitDefinition Current => current;
    public string CurrentKey => current != null ? current.Key : DefaultKey;
    public int OutfitCount => outfits.Length;
    public OutfitDefinition GetOutfit(int index) => index >= 0 && index < outfits.Length ? outfits[index] : null;

    private void Awake() => CacheOriginals();

    private void Start()
    {
        if (startOutfit != null && current == null) Wear(startOutfit);
    }

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
