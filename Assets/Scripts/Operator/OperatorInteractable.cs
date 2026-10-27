using UnityEngine;

// Something in the operator's room he can use with the left mouse button (FirstPersonPlayer: looked at, within
// reach). Needs a collider on the same GameObject. The label is what shows under the dot.
public abstract class OperatorInteractable : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LabelEnField = nameof(labelEn);
    public const string LabelPtField = nameof(labelPt);

    // The arms' animation when he uses it.
    public enum HandMotion { None, Grab, Push }

    // The label under the crosshair, per language.
    [SerializeField] private string labelEn = "Use";
    [SerializeField] private string labelPt = "Usar";

    // The label in a language (overridden by things whose label changes).
    public virtual string Label(GameLanguage language) => language == GameLanguage.Portuguese ? labelPt : labelEn;

    // the arms' animation when used
    public virtual HandMotion Motion => HandMotion.Push;

    // What happens when he uses it.
    public abstract void Interact(FirstPersonPlayer player);

    // Helper: the text in the given language.
    protected static string Pick(GameLanguage language, string en, string pt) => language == GameLanguage.Portuguese ? pt : en;
}
