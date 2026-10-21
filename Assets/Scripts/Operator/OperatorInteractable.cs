using UnityEngine;

// Something in the operator's room he can use with the left mouse button (FirstPersonPlayer: looked at, within
// reach). Needs a collider on the same GameObject. The label is what shows under the dot.
public abstract class OperatorInteractable : MonoBehaviour
{
    public const string LabelEnField = nameof(labelEn);
    public const string LabelPtField = nameof(labelPt);

    public enum HandMotion { None, Grab, Push }

    [SerializeField] private string labelEn = "Use";
    [SerializeField] private string labelPt = "Usar";

    public virtual string Label(GameLanguage language) => language == GameLanguage.Portuguese ? labelPt : labelEn;

    // the arms' animation when used
    public virtual HandMotion Motion => HandMotion.Push;

    public abstract void Interact(FirstPersonPlayer player);

    protected static string Pick(GameLanguage language, string en, string pt) => language == GameLanguage.Portuguese ? pt : en;
}
