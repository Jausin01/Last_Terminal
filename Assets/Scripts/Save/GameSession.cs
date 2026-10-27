using System;
using System.Collections.Generic;
using UnityEngine;

// What goes from one scene to the next (the game is split in scenes: the facility, the casino...), shared by the
// scenes through this asset: her state on the way (an elevator ride, a save loaded from another scene) and how each
// scene she left was (what was picked up, who is dead). Runtime only: nothing here is saved in the asset; every play
// session starts empty (a value left from an earlier session in the editor is ignored).
[CreateAssetMenu(menuName = "Last Terminal/Game Session", fileName = "GameSession")]
public class GameSession : ScriptableObject
{
    // Her state for the next scene, where she arrives (stop id), the stop she left, loaded from a save file, and the play id it belongs to.
    [NonSerialized] private SaveData transfer;
    [NonSerialized] private string arrivalStop;
    [NonSerialized] private string originStop;
    [NonSerialized] private bool fromSaveFile;
    [NonSerialized] private int transferStamp;
    // How each scene she left was (by scene name), and its play id.
    [NonSerialized] private Dictionary<string, SceneWorld> worlds;
    [NonSerialized] private int worldsStamp;
    // The operator was at the camera program, and its play id.
    [NonSerialized] private bool operating;
    [NonSerialized] private int operatingStamp;

    // valid only within this play session: every Play counts up (also with Enter Play Mode Options / no domain reload)
    private static int playId;

    // Each Play gets a new id: values from an earlier session are ignored.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void NewPlay() => playId++;

    // Was this value set in this play session?
    private static bool Fresh(int stamp) => stamp != 0 && stamp == playId;

    // There is a state waiting for this scene.
    public bool HasTransfer => transfer != null && Fresh(transferStamp);

    // her state, and where she arrives: an elevator stop id (and the stop she left: the elevator that goes back
    // there is the one she comes out of), or (fromSave) the position in the data
    public void SetTransfer(SaveData data, string arrivalStopId, string originStopId, bool fromSave)
    {
        transfer = data;
        arrivalStop = arrivalStopId;
        originStop = originStopId;
        fromSaveFile = fromSave;
        transferStamp = playId;
    }

    // Takes (and clears) the waiting state.
    public bool TakeTransfer(out SaveData data, out string arrivalStopId, out string originStopId, out bool fromSave)
    {
        data = HasTransfer ? transfer : null;
        arrivalStopId = arrivalStop;
        originStopId = originStop;
        fromSave = fromSaveFile;
        transfer = null;
        transferStamp = 0;
        return data != null;
    }

    // ------------------------------------------------------------------ the operator
    // he was at the camera program when the scene changed (elevator, a save of another scene, a restart): the next
    // scene starts there instead of in his room; a new Play starts in the room
    public bool WasOperating => operating && Fresh(operatingStamp);

    // Remembers whether he was at the camera program.
    public void SetOperating(bool value)
    {
        operating = value;
        operatingStamp = playId;
    }

    // ------------------------------------------------------------------ the scenes she left
    private Dictionary<string, SceneWorld> Worlds
    {
        get
        {
            if (worlds == null || !Fresh(worldsStamp)) { worlds = new Dictionary<string, SceneWorld>(); worldsStamp = playId; }
            return worlds;
        }
    }

    // Stores how a scene was left.
    public void RememberWorld(SceneWorld world)
    {
        if (world != null && !string.IsNullOrEmpty(world.scene)) Worlds[world.scene] = world;
    }

    // How a scene was left, if she has been there.
    public bool TryGetWorld(string scene, out SceneWorld world) => Worlds.TryGetValue(scene, out world);

    // Every scene she left (for the save file).
    public IEnumerable<SceneWorld> AllWorlds => Worlds.Values;

    // Clears the remembered scenes (a new game / a save loaded).
    public void ForgetWorlds() => Worlds.Clear();
}
