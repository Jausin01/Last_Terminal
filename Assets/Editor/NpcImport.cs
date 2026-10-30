using UnityEditor;

// The extras (Assets/Characters/NPC/): people for the story scenes (the bar's crowd...).
// <name>/<name>.fbx - Mixamo rigged (mixamorig bones): Humanoid, each with its own avatar; the texture beside it keeps
//                     its original file name (the FBX looks it up by name). Already in metres, rest pose = bind pose
//                     (the PSX pack's 4.4 m files were normalized in Blender: bar/npc_normalize.py), so scale 1.
// Animations/*.fbx  - Mixamo clips without skin (dances...): Humanoid, looping, in place: the walk across the floor (XZ)
//                     goes to root motion, which the dancers don't apply (only the up / down and the turns stay in the pose).
public class NpcImport : AssetPostprocessor
{
    public const string Folder = "Assets/Characters/NPC/";
    public const string AnimFolder = Folder + "Animations/";

    // Bumped when the import settings change: Unity reimports the files.
    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        if (mi.sourceAvatar == null) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = assetPath.StartsWith(AnimFolder);
        mi.useFileScale = true;
        mi.globalScale = 1f;   // (an older build set 0.4 here: the files are in metres now)
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(AnimFolder)) return;
        var mi = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
        if (defaults.Length == 0) return;
        ModelImporterClipAnimation c = defaults[0];
        c.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        c.loopTime = true;
        c.loopPose = true;
        c.lockRootRotation = true;
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = false;    // not baked: the travel goes to root motion (off) - the dancer stays on its spot
        c.keepOriginalPositionXZ = false;
        mi.clipAnimations = new[] { c };
    }
}
