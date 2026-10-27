using System.Collections.Generic;
using UnityEngine;

// A cutscene of this scene: a list of steps (walk, animate, speak, cut the camera, open a door, set a flag...),
// edited in the inspector ("+ Add step"), played in order by the CutsceneDirector. Started by a
// CutsceneTrigger (or by anything that calls Play). Played once: a story flag (cutscene_<id>) is set at the end
// and saved, so it isn't played again (unless Once is off).
public class Cutscene : MonoBehaviour
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string DirectorField = nameof(director);
    public const string IdField = nameof(id);
    public const string StepsField = nameof(steps);
    public const string ClearScreenAtEndField = nameof(clearScreenAtEnd);

    // The scene's director (plays the steps, owns the screen / letterbox / story flags).
    [SerializeField] private CutsceneDirector director;
    [Tooltip("Saved name (empty = the object's name). Change it and the cutscene counts as never played.")]
    [SerializeField] private string id = "";
    [Tooltip("Played only once (remembered in the save).")]
    [SerializeField] private bool once = true;
    [Tooltip("Esc skips it.")]
    [SerializeField] private bool skippable = true;
    [Tooltip("Black bars at the top and bottom of the screen.")]
    [SerializeField] private bool letterbox = true;
    [Tooltip("At the end, back to the camera the operator had before (off = stays on the last CCTV camera cut to).")]
    [SerializeField] private bool restoreCamera = true;
    [Tooltip("At the end the black screen fades away (off = it ends on black, e.g. a hard cut before the next scene).")]
    [SerializeField] private bool clearScreenAtEnd = true;
    // The steps, in order (each one a CutsceneStep subclass, edited in the Inspector).
    [SerializeReference] private List<CutsceneStep> steps = new List<CutsceneStep>();

    // Settings and state read by the director and the editor.
    public CutsceneDirector Director => director;
    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string PlayedFlag => CutsceneDirector.PlayedPrefix + Id;
    public bool Once => once;
    public bool Skippable => skippable;
    public bool Letterbox => letterbox;
    public bool RestoreCamera => restoreCamera;
    public bool ClearScreenAtEnd => clearScreenAtEnd;
    public IReadOnlyList<CutsceneStep> Steps => steps;
    public bool HasPlayed => director != null && director.Story != null && director.Story.Has(PlayedFlag);

    // false = not now (another one playing, the operator away from the cameras, a fight) or already played
    public bool Play()
    {
        if (director == null) { Debug.LogWarning($"{name}: Cutscene has no director (Last Terminal/10 on this scene).", this); return false; }
        if (once && HasPlayed) return false;
        return director.Play(this);
    }

    // testing (the inspector's button in Play mode): plays even if it was played before
    public bool PlayAgain() => director != null && director.Play(this);
}
