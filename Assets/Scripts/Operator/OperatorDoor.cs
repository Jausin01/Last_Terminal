using UnityEngine;

// The door of his room: locked from the outside. Every try says something (in order, the last one repeats) and
// rattles the handle.
public class OperatorDoor : OperatorInteractable
{
    public const string LeafField = nameof(leaf);

    [Tooltip("The door leaf (shaken a little).")]
    [SerializeField] private Transform leaf;
    [SerializeField] private string[] linesEn =
    {
        "Locked. From the outside.",
        "Nobody answers.",
        "The handle doesn't even move.",
        "...I'm not getting out through here.",
    };
    [SerializeField] private string[] linesPt =
    {
        "Trancada. Por fora.",
        "Ninguém responde.",
        "A maçaneta nem mexe.",
        "...Por aqui eu não saio.",
    };

    private int tries;
    private float shake;
    private Vector3 rest;

    public override HandMotion Motion => HandMotion.Grab;

    private void Awake()
    {
        if (leaf != null) rest = leaf.localPosition;
    }

    public override void Interact(FirstPersonPlayer player)
    {
        bool pt = player.Language == GameLanguage.Portuguese;
        string[] lines = pt ? linesPt : linesEn;
        if (lines.Length > 0) player.Hud.ShowMessage(lines[Mathf.Min(tries, lines.Length - 1)]);
        tries++;
        shake = 0.45f;
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Rattle);
    }

    private void Update()
    {
        if (leaf == null || shake <= 0f) return;
        shake -= Time.deltaTime;
        float s = shake > 0f ? Mathf.Sin(Time.time * 70f) * 0.004f * shake : 0f;
        leaf.localPosition = rest + new Vector3(s, 0f, 0f);
    }
}
