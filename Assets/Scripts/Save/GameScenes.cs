using UnityEngine;
using UnityEngine.SceneManagement;

// Opens a location scene of the game (the asylum, the casino...) the way the game runs it: his room (Operator.unity)
// first, the location loaded over it by the room's PC (OperatorStation).
// - atCameras (an elevator, a restart): he is at the cameras at once, the location loads with the room.
// - not (a save loaded): he is standing in his room; the location (Helena where the save left her) loads when he opens
//   CCTV.EXE.
// Without his room in the build (or for the room itself) the scene is opened on its own.
public static class GameScenes
{
    // His room's scene (also where the first cutscene ends).
    public const string Operator = "Operator";

    // Is his room in the build?
    public static bool OperatorAvailable => Application.CanStreamedLevelBeLoaded(Operator);

    // Starts loading a location: his room, told (GameSession) which location to bring up and whether he sits at the
    // cameras. Returns the load of the first scene (the caller may hold it).
    public static AsyncOperation OpenLocation(GameSession session, string location, bool atCameras)
    {
        if (session != null && OperatorAvailable && location != Operator)
        {
            session.SetNextLocation(location);
            session.SetOperating(atCameras);
            return SceneManager.LoadSceneAsync(Operator);
        }
        return SceneManager.LoadSceneAsync(location);
    }
}
