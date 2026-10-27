using UnityEngine;

// A cork board on his wall: used, its notes are read full screen (click or Esc closes).
public class OperatorNoteBoard : OperatorInteractable
{
    // Title and body of the notes, per language.
    [SerializeField] private string titleEn = "NOTES";
    [SerializeField] private string titlePt = "NOTAS";
    [SerializeField, TextArea(6, 20)] private string bodyEn = "";
    [SerializeField, TextArea(6, 20)] private string bodyPt = "";

    // No arm animation.
    public override HandMotion Motion => HandMotion.None;

    // Shows the notes full screen (paper sound).
    public override void Interact(FirstPersonPlayer player)
    {
        bool pt = player.Language == GameLanguage.Portuguese;
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Paper, 0.7f);
        player.Hud.ShowNote(pt ? titlePt : titleEn, pt ? bodyPt : bodyEn, pt ? "[clique / Esc para fechar]" : "[click / Esc to close]");
    }

#if UNITY_EDITOR
    // Editor: sets the label, title and body (room builder).
    public void EditorSet(string label, string labelPortuguese, string tEn, string tPt, string bEn, string bPt)
    {
        var so = new UnityEditor.SerializedObject(this);
        so.FindProperty(LabelEnField).stringValue = label;
        so.FindProperty(LabelPtField).stringValue = labelPortuguese;
        so.ApplyModifiedPropertiesWithoutUndo();
        titleEn = tEn; titlePt = tPt; bodyEn = bEn; bodyPt = bPt;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
