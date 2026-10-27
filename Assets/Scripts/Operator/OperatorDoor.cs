using UnityEngine;

// The door of his room: locked from the outside. Every try says something (in order, the last one repeats) and
// rattles the handle.
public class OperatorDoor : OperatorInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string LeafField = nameof(leaf);

    [Tooltip("The door leaf (shaken a little).")]
    [SerializeField] private Transform leaf;
    // What he says at each try, per language (the last one repeats).
    [SerializeField] private string[] linesEn =
    {
        "Locked. From the outside.",
        "Nobody answers.",
        "The handle doesn't even move.",
        "...I'm not getting out through here.",
    };
    // His thoughts in Portuguese.
    [SerializeField] private string[] linesPt =
    {
        "Trancada. Por fora.",
        "Ninguém responde.",
        "A maçaneta nem mexe.",
        "...Por aqui eu não saio.",
    };

    // Tries so far, shake time left, the leaf's rest position.
    private int tries;
    private float shake;
    private Vector3 rest;

    // He grabs the handle.
    public override HandMotion Motion => HandMotion.Grab;

    // Remembers the leaf's rest position.
    private void Awake()
    {
        if (leaf != null) rest = leaf.localPosition;
    }

    // Says the next line, rattles the handle.
    public override void Interact(FirstPersonPlayer player)
    {
        bool pt = player.Language == GameLanguage.Portuguese;
        string[] lines = pt ? linesPt : linesEn;
        if (lines.Length > 0) player.Hud.ShowMessage(lines[Mathf.Min(tries, lines.Length - 1)]);
        tries++;
        shake = 0.45f;
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Rattle);
    }

    // Shakes the leaf while the shake lasts.
    private void Update()
    {
        if (leaf == null || shake <= 0f) return;
        shake -= Time.deltaTime;
        float s = shake > 0f ? Mathf.Sin(Time.time * 70f) * 0.004f * shake : 0f;
        leaf.localPosition = rest + new Vector3(s, 0f, 0f);
    }
}
