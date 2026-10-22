using UnityEditor;

// The story cast (Assets/Characters/Cast/):
// Models/ - Mary, Michael, PigDriver: the Mixamo files as they came (their own rig and weights, Humanoid, each makes
//           its own avatar). PigDriver is ~5 m tall in the file (same pack as K_Burnt): scaled to 1.86 m. Mary's
//           file carries her Mixamo idle (clip "Mary_Idle").
// Animations/ - the cutscene clips made on Helena's skeleton: Humanoid on Helena's avatar once Last Terminal/14 has set
//           it (CopyFromOther, kept on reimport). Root baked into the pose with the height as authored (seated clips
//           keep the hips at the seat height); loops except the one-shots.
public class CastImport : AssetPostprocessor
{
    public const string Folder = "Assets/Characters/Cast/";
    public const string ModelFolder = Folder + "Models/";
    public const string AnimFolder = Folder + "Animations/";
    public const string MaryFile = ModelFolder + "Mary.fbx";
    public const string MaryIdleClip = "Mary_Idle";
    const string PigFile = ModelFolder + "PigDriver.fbx";
    const float PigFileHeight = 4.918f;
    const float PigHeight = 1.86f;
    static readonly string[] OneShots = { "Cast_ArmsOpen", "Cast_PigLunge", "Cast_PigTurn", "Cast_BedSitUp" };
    // lying down (the asylum bed): the body orientation of the first frame points at the ceiling, so the root keeps
    // the orientation of the file (she faces +Z) instead
    static bool OriginalOrientation(string fileName) => fileName.StartsWith("Cast_Bed");

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        if (mi.sourceAvatar == null) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = assetPath.StartsWith(AnimFolder) || assetPath == MaryFile;
        mi.useFileScale = true;
        mi.globalScale = assetPath == PigFile ? PigHeight / PigFileHeight : 1f;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
    }

    void OnPreprocessAnimation()
    {
        bool mary = assetPath == MaryFile;
        if (!assetPath.StartsWith(AnimFolder) && !mary) return;
        var mi = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
        if (defaults.Length == 0) return;
        string fileName = mary ? MaryIdleClip : System.IO.Path.GetFileNameWithoutExtension(assetPath);
        ModelImporterClipAnimation c = defaults[0];
        c.name = fileName;
        bool loop = System.Array.IndexOf(OneShots, fileName) < 0;
        c.loopTime = loop;
        c.loopPose = loop;
        c.lockRootRotation = true;
        c.keepOriginalOrientation = OriginalOrientation(fileName);
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = !mary;   // her idle: feet on the floor
        c.heightFromFeet = mary;
        c.lockRootPositionXZ = true;
        c.keepOriginalPositionXZ = true;
        mi.clipAnimations = new[] { c };
    }
}
