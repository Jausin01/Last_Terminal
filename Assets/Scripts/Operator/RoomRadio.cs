using UnityEngine;

// The old radio of his room: each use turns the knob - static, a station (if it can be called that), off.
public class RoomRadio : OperatorInteractable
{
    // Names of the serialized fields below: the editor builders set them by name (SerializedObject).
    public const string AudioField = nameof(audioSource);
    public const string StaticClipField = nameof(staticClip);
    public const string StationClipField = nameof(stationClip);

    [Tooltip("Plays the loops (3D, on the radio).")]
    [SerializeField] private AudioSource audioSource;
    // The loops: static, and the "station".
    [SerializeField] private AudioClip staticClip;
    [SerializeField] private AudioClip stationClip;
    // His thoughts on each state, per language.
    [SerializeField] private string staticThoughtEn = "Just static.";
    [SerializeField] private string staticThoughtPt = "Só chiado.";
    [SerializeField] private string stationThoughtEn = "Someone's playing something. Or a record got stuck.";
    [SerializeField] private string stationThoughtPt = "Alguém tocando alguma coisa. Ou um disco que travou.";

    private int state;   // 0 off, 1 static, 2 station

    // He pushes the knob.
    public override HandMotion Motion => HandMotion.Push;

    // Label: shows when it is off.
    public override string Label(GameLanguage language) => state == 0 ? Pick(language, "Radio (off)", "Rádio (desligado)") : Pick(language, "Radio", "Rádio");

    // Next state: off -> static -> station -> off (with his thought).
    public override void Interact(FirstPersonPlayer player)
    {
        OperatorSounds.Play(player.Audio, OperatorSounds.Kind.Knob, 0.8f);
        state = (state + 1) % 3;
        GameLanguage l = player.Language;
        if (audioSource == null) return;
        audioSource.Stop();
        AudioClip clip = state == 1 ? staticClip : state == 2 ? stationClip : null;
        if (clip == null) return;
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
        player.Hud.ShowThought(state == 1 ? Pick(l, staticThoughtEn, staticThoughtPt) : Pick(l, stationThoughtEn, stationThoughtPt));
    }
}
