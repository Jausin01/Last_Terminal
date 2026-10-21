using System;
using UnityEngine;

// One outfit (skin) of hers: the same model and skeleton (every animation still works), other materials,
// and the parts that go away (holster, belt...). Materials are swapped by the original material they replace.
// Extra parts (bunny ears, tail...) are accessories on her bones, shown only with the outfits that list them.
// Key = lexicon name key ("put on the pajamas" -> outfit_pajama). Price = chips at the prize counter (later).
[CreateAssetMenu(menuName = "Last Terminal/Outfit", fileName = "Outfit")]
public class OutfitDefinition : ScriptableObject
{
    [Serializable]
    public class MaterialSwap
    {
        public const string OriginalField = nameof(original);
        public const string ReplacementField = nameof(replacement);

        [SerializeField] private Material original;
        [SerializeField] private Material replacement;

        public MaterialSwap() { }
        public MaterialSwap(Material original, Material replacement) { this.original = original; this.replacement = replacement; }
        public Material Original => original;
        public Material Replacement => replacement;
    }

    [Serializable]
    public class MeshSwap
    {
        public const string PartField = nameof(part);
        public const string MeshField = nameof(mesh);

        [SerializeField] private string part;
        [SerializeField] private Mesh mesh;

        public string Part => part;
        public Mesh Mesh => mesh;
    }

    public const string KeyField = nameof(key);
    public const string DisplayNameField = nameof(displayName);
    public const string DisplayNamePtField = nameof(displayNamePt);
    public const string PriceField = nameof(price);
    public const string SwapsField = nameof(swaps);
    public const string MeshSwapsField = nameof(meshSwaps);
    public const string HiddenPartsField = nameof(hiddenParts);
    public const string ExtraPartsField = nameof(extraParts);
    public const string LineField = nameof(line);

    [SerializeField] private string key = "outfit_";
    [SerializeField] private string displayName = "Outfit";
    [SerializeField] private string displayNamePt = "Roupa";
    [SerializeField, Min(0)] private int price;
    [SerializeField] private MaterialSwap[] swaps = new MaterialSwap[0];
    [Tooltip("Parts of her model (object names) drawn with another mesh: same vertices and bones, reshaped (e.g. smoothed).")]
    [SerializeField] private MeshSwap[] meshSwaps = new MeshSwap[0];
    [Tooltip("Renderers (object names in her model) hidden with this outfit.")]
    [SerializeField] private string[] hiddenParts = new string[0];
    [Tooltip("Accessories (object names under her bones) shown with this outfit.")]
    [SerializeField] private string[] extraParts = new string[0];
    [Tooltip("What she says once she has it on.")]
    [SerializeField] private PartnerLine line = PartnerLine.OutfitChanged;

    public string Key => key;
    public string DisplayName(GameLanguage language) => language == GameLanguage.Portuguese ? displayNamePt : displayName;
    public int Price => price;
    public PartnerLine Line => line;

    public Material Swap(Material original)
    {
        foreach (MaterialSwap s in swaps)
            if (s != null && s.Original == original && s.Replacement != null) return s.Replacement;
        return original;
    }

    // the reshaped mesh of a part, null = keep hers
    public Mesh MeshFor(string partName)
    {
        foreach (MeshSwap m in meshSwaps)
            if (m != null && m.Part == partName && m.Mesh != null) return m.Mesh;
        return null;
    }

    public bool Hides(string partName) => Array.IndexOf(hiddenParts, partName) >= 0;
    public bool Shows(string extraName) => Array.IndexOf(extraParts, extraName) >= 0;
}
