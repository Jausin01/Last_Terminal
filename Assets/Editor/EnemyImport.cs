using System.Collections.Generic;
using System.IO;
using UnityEditor;

// Configures every FBX placed under Assets/Characters/Enemies/ automatically (like HelenaImport for her):
// Humanoid rig, avatar from each file, scale 1.8 m (the K_Burnt rig is ~5 m tall in the file), clip named after the file.
// Root motion (the NavMeshAgent moves the enemy, EnemyAnimator has applyRootMotion off):
// - Baked: everything baked into the pose, based on the file origin (attacks lunge, deaths fall away from the root)
// - Extracted: walks / runs that are not "In Place": XZ left out of the pose (and dropped), so the body stays on the agent
// - Centered: crouched clips (feeding, getting up): XZ baked on the center of mass, so the two line up
public class EnemyImport : AssetPostprocessor
{
    public const string Folder = "Assets/Characters/Enemies/";
    public const float ModelHeight = 5.065f;     // K_Burnt in the file (Mixamo upload)
    public const float TargetHeight = 1.8f;
    static readonly string[] LoopKeywords = { "idle", "walk", "run", "feed" };
    const float FullRange = -1f;

    enum RootMode { Baked, Extracted, Centered }

    // file -> clips cut from it (Unity frames start at 0; FullRange = whole file)
    static readonly (string file, string clip, float first, float last, bool loop, RootMode root)[] Clips =
    {
        ("Zombie_Walk", "Zombie_Walk", FullRange, FullRange, true, RootMode.Extracted),
        ("Zombie_Run", "Zombie_Run", FullRange, FullRange, true, RootMode.Extracted),
        ("K_BurntWalking", "K_BurntWalking", FullRange, FullRange, true, RootMode.Extracted),
        ("Zombie_Feed", "Zombie_Feed", FullRange, FullRange, true, RootMode.Centered),
        // kneels, bites, gets up: only the getting up (from the feeding pose to standing)
        ("Zombie_Biting", "Zombie_Rise", 104f, FullRange, false, RootMode.Centered),
    };

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
        mi.useFileScale = true;
        mi.globalScale = TargetHeight / ModelHeight;
        mi.importCameras = false;
        mi.importLights = false;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
        if (defaults.Length == 0) return;

        var result = new List<ModelImporterClipAnimation>();
        foreach (var spec in Clips)
        {
            if (spec.file != fileName) continue;
            ModelImporterClipAnimation c = mi.defaultClipAnimations[0];   // fresh copy per clip (it is a class)
            c.name = spec.clip;
            if (spec.first != FullRange) c.firstFrame = spec.first;
            if (spec.last != FullRange) c.lastFrame = spec.last;
            Configure(c, spec.loop, spec.root);
            result.Add(c);
        }
        if (result.Count == 0)
        {
            bool loop = HasKeyword(fileName, LoopKeywords);
            for (int i = 0; i < defaults.Length; i++)
            {
                ModelImporterClipAnimation c = defaults[i];
                c.name = defaults.Length == 1 ? fileName : fileName + "_" + i;
                Configure(c, loop, RootMode.Baked);
                result.Add(c);
            }
        }
        mi.clipAnimations = result.ToArray();
    }

    static void Configure(ModelImporterClipAnimation c, bool loop, RootMode mode)
    {
        c.loopTime = loop;
        c.loopPose = loop;
        // rotation: baked (Unity's default basis, body orientation)
        c.lockRootRotation = true;
        // height: baked, feet on the floor
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = false;
        c.heightFromFeet = true;
        // XZ
        c.lockRootPositionXZ = mode != RootMode.Extracted;
        c.keepOriginalPositionXZ = mode == RootMode.Baked;
    }

    static bool HasKeyword(string fileName, string[] keywords)
    {
        string n = fileName.ToLowerInvariant();
        foreach (string k in keywords)
            if (n.Contains(k)) return true;
        return false;
    }
}
