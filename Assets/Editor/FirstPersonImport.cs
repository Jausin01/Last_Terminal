using System.Collections.Generic;
using UnityEditor;

// The operator's first person arms (Assets/Art/FirstPerson/): Generic rig, every take of the file as a clip named
// after its action ("ArmsRig|grab.R" -> "grab.R"); the idle ones loop. Bones kept as GameObjects (the coffee cup
// rides on the right hand).
public class FirstPersonImport : AssetPostprocessor
{
    public const string Folder = "Assets/Art/FirstPerson/";
    static readonly string[] LoopClips = { "relax", "rest", "guard_idle", "knife_idle", "finger_gun_idle", "finger_gun_broken" };

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.optimizeGameObjects = false;
        mi.importAnimation = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
        if (defaults.Length == 0) return;
        var clips = new List<ModelImporterClipAnimation>();
        foreach (ModelImporterClipAnimation c in defaults)
        {
            string take = c.takeName ?? c.name;
            int bar = take.LastIndexOf('|');
            c.name = bar >= 0 ? take.Substring(bar + 1) : take;
            bool loop = System.Array.IndexOf(LoopClips, c.name) >= 0;
            c.loopTime = loop;
            c.loopPose = loop;
            clips.Add(c);
        }
        mi.clipAnimations = clips.ToArray();
    }
}
