using System.Collections.Generic;
using System.IO;
using UnityEditor;

// Configures every FBX placed under Assets/Characters/Helena/ automatically:
// Humanoid rig, clip named after the file, loop for locomotion clips.
// Locomotion: root motion baked into pose (the NavMeshAgent moves her).
// Turn clips ("turn" in the file name): root rotation + XZ left free, applied by PartnerAnimator.
// Seat clips (sit down / stand up): XZ left free (she travels onto the chair), rotation baked, root at the center of mass;
// the typing loop is baked with the same center-of-mass basis, so the three clips line up.
// Door clips: same as the seat clips (she walks through the doorway; they start and end facing the same way).
// Dealer clips (seated at the blackjack table, made from the Mixamo dealer clips on the typing seat): baked,
// center-of-mass basis like the typing loop.
// Hit reactions (boxing-guard Mixamo clips, turned ~40 deg): root rotation from the body orientation, so she keeps
// facing where she was. Injured locomotion (male "injured" pack, any Mixamo rig): Humanoid retarget, loops by keyword.
// Some files are cut into specific clips, or mirrored (see Clips).
public class HelenaImport : AssetPostprocessor
{
    public const string Folder = "Assets/Characters/Helena/";
    static readonly string[] LoopKeywords = { "idle", "walk", "run", "jog", "sprint", "strafe" };
    const string TurnKeyword = "turn";
    const float FullRange = -1f;

    enum RootMode { Baked, Turn, Travel }

    // root rotation based on the body orientation (the clip turns her body away from her facing)
    static bool BodyOrientation(string fileName) =>
        fileName.StartsWith("HelenaHit") || fileName == "HelenaStomachHit" || fileName == "HelenaRibHit";

    // root motion XZ free (moved by PartnerAnimator while scripted)
    static readonly string[] TravelFiles = { "HelenaStandToSit", "HelenaSitToStand",
        "HelenaOpenDoorInwards", "HelenaOpenDoorOutwards", "HelenaTryToOpenLockedDoor" };
    // not "in place" (the injured pack walks ~2 m per loop): root XZ extracted instead of baked, so the body stays
    // over her feet; PartnerAnimator drops that motion (the NavMeshAgent moves her)
    static readonly string[] NotInPlaceFiles = { "HelenaInjuredWalk", "HelenaInjuredWalkBackwards" };
    // root XZ based on the body center of mass instead of the file origin
    static readonly string[] CenterOfMassFiles = { "HelenaInjuredWalk", "HelenaInjuredWalkBackwards", "HelenaStandToSit", "HelenaSitToStand", "HelenaComputerTyping",
        "HelenaOpenDoorInwards", "HelenaOpenDoorOutwards", "HelenaTryToOpenLockedDoor",
        "HelenaDealerIdle", "HelenaDealerPickDeck", "HelenaDealerDeal", "HelenaDealerHold", "HelenaDealerPutBack", "HelenaDealerCheck" };

    // file -> clips cut from it (Unity frames start at 0; FullRange = whole file)
    static readonly (string file, string clip, float first, float last, bool loop, bool mirror)[] Clips =
    {
        // aims, then lowers the gun: keep the steady aim at the start, as a loop
        ("HelenaAimWithPistol", "HelenaAimWithPistol", 0f, 20f, true, false),
        // shoulders the shotgun but the arms keep drifting: one frame = static shouldered aim
        ("HelenaShotgunGunplay", "HelenaShotgunGunplay", 40f, 41f, true, false),
        // the swing, plus its first frame (weapon on the right shoulder) as the heavy-weapon guard pose
        ("HelenaBluntStrike", "HelenaBluntStrike", FullRange, FullRange, false, false),
        ("HelenaBluntStrike", "HelenaBluntReady", 0f, 1f, true, false),
        // the stab of this file is done with the left hand: mirrored, it becomes a right-hand stab
        ("HelenaKnifeStabbing2", "HelenaKnifeStabbing2", FullRange, FullRange, false, true),
        // seated typing: whole file, looped ("typing" is not a loop keyword)
        ("HelenaComputerTyping", "HelenaComputerTyping", FullRange, FullRange, true, false),
        // shotgun reload authored in Blender: one shell per loop
        ("HelenaShotgunReloadLoop", "HelenaShotgunReloadLoop", FullRange, FullRange, true, false),
        // dealer: waiting and holding the deck are loops
        ("HelenaDealerIdle", "HelenaDealerIdle", FullRange, FullRange, true, false),
        ("HelenaDealerHold", "HelenaDealerHold", FullRange, FullRange, true, false),
        // keypad: hand up to the panel / one press (finger down and back up) / hand down (PartnerKeypadUser)
        ("HelenaButton_Pushing", "HelenaKeypadRaise", 0f, 36f, false, false),
        ("HelenaButton_Pushing", "HelenaKeypadPress", 36f, 60f, false, false),
        ("HelenaButton_Pushing", "HelenaKeypadLower", 60f, FullRange, false, false),
        // sitting on the floor, knees up (waiting while the operator is away): a held pose, looped
        ("HelenaSittingPose", "HelenaFloorSit", FullRange, FullRange, true, false),
    };

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        RootMode mode = fileName.ToLowerInvariant().Contains(TurnKeyword) ? RootMode.Turn
                      : System.Array.IndexOf(TravelFiles, fileName) >= 0 || System.Array.IndexOf(NotInPlaceFiles, fileName) >= 0 ? RootMode.Travel
                      : RootMode.Baked;
        bool centerOfMass = System.Array.IndexOf(CenterOfMassFiles, fileName) >= 0;

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
            c.mirror = spec.mirror;
            Configure(c, spec.loop, mode, centerOfMass, BodyOrientation(fileName));
            result.Add(c);
        }

        if (result.Count == 0)
        {
            bool loop = HasKeyword(fileName, LoopKeywords);
            for (int i = 0; i < defaults.Length; i++)
            {
                ModelImporterClipAnimation c = defaults[i];
                c.name = defaults.Length == 1 ? fileName : fileName + "_" + i;
                Configure(c, loop, mode, centerOfMass, BodyOrientation(fileName));
                result.Add(c);
            }
        }
        mi.clipAnimations = result.ToArray();
    }

    static void Configure(ModelImporterClipAnimation c, bool loop, RootMode mode, bool centerOfMass, bool bodyOrientation)
    {
        c.loopTime = loop;
        c.loopPose = loop;
        // Root Transform Rotation: baked, free (root motion) for turns
        c.lockRootRotation = mode != RootMode.Turn;
        // hits: based on the body orientation (Unity's default); the rest keep the setting they always had (untouched)
        if (bodyOrientation) c.keepOriginalOrientation = false;
        // Root Transform Position (Y): Bake Into Pose, based on Feet
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = false;
        c.heightFromFeet = true;
        // Root Transform Position (XZ): baked, free for turns and seat travel (no pop at the end)
        c.lockRootPositionXZ = mode == RootMode.Baked;
        c.keepOriginalPositionXZ = !centerOfMass;
    }

    static bool HasKeyword(string fileName, string[] keywords)
    {
        string n = fileName.ToLowerInvariant();
        foreach (string k in keywords)
            if (n.Contains(k)) return true;
        return false;
    }
}
