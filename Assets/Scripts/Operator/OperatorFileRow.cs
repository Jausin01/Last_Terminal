using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the file list on his desktop (the template OperatorDesktop copies per file).
public class OperatorFileRow : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string ButtonField = nameof(button);
    public const string LabelField = nameof(label);

    // The row's button and its text.
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    // Read by OperatorDesktop when it fills the list.
    public Button Button => button;
    public TMP_Text Label => label;
}
