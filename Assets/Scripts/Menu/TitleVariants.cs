using UnityEngine;

// The title scene as the story stands: every variant (TitleVariant: objects shown / hidden) whose conditions hold on
// the newest save is applied, in order (later ones win). TitleScreen hands it the newest save (null = no save).
// Edit the variants here, in the Inspector of Title.unity: add the objects to the scene, then a variant that shows them.
public class TitleVariants : MonoBehaviour
{
    // Name of the serialized field below: the editor builder sets it by name (SerializedObject).
    public const string VariantsField = nameof(variants);

    [Tooltip("Checked in order: every one whose conditions hold is applied (later ones win).")]
    [SerializeField] private TitleVariant[] variants = new TitleVariant[0];

    // Sets the scene for this save (null = no save yet).
    public void Apply(SaveData newest)
    {
        foreach (TitleVariant v in variants)
            if (v != null && v.Matches(newest)) v.Apply();
    }
}
