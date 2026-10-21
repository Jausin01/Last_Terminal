using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the file list on his desktop (the template OperatorDesktop copies per file).
public class OperatorFileRow : MonoBehaviour
{
    public const string ButtonField = nameof(button);
    public const string LabelField = nameof(label);

    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public Button Button => button;
    public TMP_Text Label => label;
}
