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
    [NonSerialized] private SaveData transfer;
    [NonSerialized] private string arrivalStop;
    [NonSerialized] private string originStop;
    [NonSerialized] private bool fromSaveFile;
    [NonSerialized] private int transferStamp;
    [NonSerialized] private Dictionary<string, SceneWorld> worlds;
    [NonSerialized] private int worldsStamp;
    [NonSerialized] private bool operating;
    [NonSerialized] private int operatingStamp;

    // valid only within this play session: every Play counts up (also with Enter Play Mode Options / no domain reload)
    private static int playId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void NewPlay() => playId++;

    private static bool Fresh(int stamp) => stamp != 0 && stamp == playId;

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

    public void RememberWorld(SceneWorld world)
    {
        if (world != null && !string.IsNullOrEmpty(world.scene)) Worlds[world.scene] = world;
    }

    public bool TryGetWorld(string scene, out SceneWorld world) => Worlds.TryGetValue(scene, out world);

    public IEnumerable<SceneWorld> AllWorlds => Worlds.Values;

    public void ForgetWorlds() => Worlds.Clear();
}
