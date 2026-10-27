using UnityEngine;

// Something in his room that only has something to say (the window, the lockers, the old monitors...): each use
// shows the next line (in a loop) and plays a little sound. As Thought: the line is his thought (top-left block)
// instead of the message line.
public class OperatorFlavor : OperatorInteractable
{
    // Its lines per language, the sound and the arm motion when used.
    [SerializeField] private string[] linesEn = new string[0];
    [SerializeField] private string[] linesPt = new string[0];
    [SerializeField] private OperatorSounds.Kind sound = OperatorSounds.Kind.Click;
    [SerializeField] private HandMotion motion = HandMotion.Push;
    [Tooltip("The lines are his thoughts (the block in the top-left corner).")]
    [SerializeField] private bool asThought;

    // Index of the next line.
    private int next;

    // The arm motion set for it.
    public override HandMotion Motion => motion;

    // Shows the next line (message or thought) and plays the sound.
    public override void Interact(FirstPersonPlayer player)
    {
        string[] lines = player.Language == GameLanguage.Portuguese ? linesPt : linesEn;
        if (lines.Length > 0)
        {
            string line = lines[next % lines.Length];
            if (asThought) player.Hud.ShowThought(line);
            else player.Hud.ShowMessage(line);
        }
        next++;
        OperatorSounds.Play(player.Audio, sound, 0.8f);
    }

#if UNITY_EDITOR
    // Editor: sets everything (room builder).
    public void EditorSet(string label, string labelPortuguese, string[] en, string[] pt, OperatorSounds.Kind kind, HandMotion hand, bool thought = false)
    {
        asThought = thought;
        var so = new UnityEditor.SerializedObject(this);
        so.FindProperty(LabelEnField).stringValue = label;
        so.FindProperty(LabelPtField).stringValue = labelPortuguese;
        so.ApplyModifiedPropertiesWithoutUndo();
        linesEn = en; linesPt = pt; sound = kind; motion = hand;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
