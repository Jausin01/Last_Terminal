using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Last Terminal/2: her Animator Controller (PartnerAnimator.controller), rebuilt in place so scenes keep it.
// Base layer: locomotion (limping blend), turn in place, armed 2D locomotion, pickups, melee swings, seat, doors,
// keypad, and the states entered from code (hits, death, floor sit, cutscene slots, evades).
// Upper body layer: weapon idle / aim by hold type, reloads. Clip paths and measured speeds live here.
internal static class PartnerAnimatorBuilder
{
    // Her clips (FBX files, imported by HelenaImport).
    private const string AnimFolder = HelenaSetupMenu.Root + "/Animations/";
    private const string WalkPath = AnimFolder + "HelenaWalking.fbx";
    private const string TurnLeftPath = AnimFolder + "HelenaTurnLeft.fbx";
    private const string TurnRightPath = AnimFolder + "HelenaTurnRight.fbx";
    private const string TurnLeft180Path = AnimFolder + "HelenaTurnLeft180.fbx";
    private const string TurnRight180Path = AnimFolder + "HelenaTurnRight180.fbx";
    private const string PickupLowPath = AnimFolder + "HelenaPickUpLowerItem.fbx";
    private const string PickupMediumPath = AnimFolder + "HelenaPickUpMediumItem.fbx";
    private const string PistolIdlePath = AnimFolder + "HelenaIdleWithPistol.fbx";
    private const string PistolAimPath = AnimFolder + "HelenaAimWithPistol.fbx";
    private const string PistolStrafeLeftPath = AnimFolder + "HelenaStrafeWithPistolLeft.fbx";
    private const string PistolStrafeRightPath = AnimFolder + "HelenaStrafeWithPistolRight.fbx";
    private const string PistolBackPath = AnimFolder + "HelenaWalkBackwardsWithPistol.fbx";
    private const string ShotgunIdlePath = AnimFolder + "HelenaShotgunIdle.fbx";
    private const string ShotgunAimPath = AnimFolder + "HelenaShotgunGunplay.fbx";
    private const string KnifeIdlePath = AnimFolder + "HelenaKnifeIdle.fbx";
    private const string KnifeStab1Path = AnimFolder + "HelenaKnifeStabbing1.fbx";
    private const string KnifeStab2Path = AnimFolder + "HelenaKnifeStabbing2.fbx";
    private const string BluntStrikePath = AnimFolder + "HelenaBluntStrike.fbx";
    private const string BluntStrikeClip = "HelenaBluntStrike";   // clips cut by HelenaImport
    private const string BluntReadyClip = "HelenaBluntReady";
    private const string ReloadPistolPath = AnimFolder + "HelenaReloadPistol.fbx";
    private const string ReloadShotgunPath = AnimFolder + "HelenaShotgunReloadLoop.fbx";
    // the pistol reload clip is long (3.7 s): played faster
    private const float ReloadPistolSpeed = 1.5f;
    private const string StandToSitPath = AnimFolder + "HelenaStandToSit.fbx";
    private const string FloorSitPath = AnimFolder + "HelenaSittingPose.fbx";
    private const string TypingPath = AnimFolder + "HelenaComputerTyping.fbx";
    private const string SitToStandPath = AnimFolder + "HelenaSitToStand.fbx";
    private const string DoorInwardsPath = AnimFolder + PartnerAnimator.DoorInwardsClip + ".fbx";
    private const string DoorOutwardsPath = AnimFolder + PartnerAnimator.DoorOutwardsClip + ".fbx";
    private const string DoorLockedPath = AnimFolder + PartnerAnimator.DoorLockedClip + ".fbx";
    // seated dealer clips (blackjack), state speeds
    private static readonly float[] DealerSpeeds = { 1f, 1.3f, 1.35f, 1f, 1.3f, 1.9f };
    // evasion clips (dodge, duck), state speeds
    private static readonly float[] EvadeSpeeds = { 1.4f, 1.5f };
    private const string UpperBodyMaskPath = HelenaSetupMenu.Root + "/UpperBody.mask";
    internal const string ControllerPath = HelenaSetupMenu.Root + "/PartnerAnimator.controller";

    // melee clips: exit time = where the swing is back to a guard pose (measured)
    private const float KnifeStab1Exit = 0.55f;
    private const float KnifeStab2Exit = 0.62f;
    private const float BluntStrikeExit = 0.92f;

    // measured from the planted foot of HelenaWalking (in place): ~1.8 m/s
    public const float WalkSpeed = 1.8f;
    // pistol clips, measured the same way (they are faster than the walk: jog cadence)
    private const float PistolStrafeLeftSpeed = 2.39f;
    private const float PistolStrafeRightSpeed = 2.66f;
    private const float PistolBackSpeed = 3.59f;

    // Menu 2: builds her Animator controller (locomotion, turns, pickups, armed layer, attacks, reload, seat, doors, keypad, hits, evades, cutscene slots).
    [MenuItem("Last Terminal/2 - Build Partner Animator")]
    public static void BuildAnimator()
    {
        Clips clips = Clips.Load();
        if (!clips.AllFound)
        {
            Debug.LogError("[Helena] Clips not found (" + MissingClips.Count + "), run 'Last Terminal/0 - Reimport Helena Assets' after fixing:\n" + string.Join("\n", MissingClips));
            return;
        }

        // rebuilt in place (same asset, same GUID): the Animator in the scene keeps its controller after this menu
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ClearController(ctrl);
        AddParameters(ctrl);

        // IK pass on the base layer: hand IK + look-at while picking up
        AnimatorControllerLayer[] layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;

        AddLocomotion(ctrl, clips, out AnimatorState locomotion, out AnimatorState armedLocomotion);
        AddActions(ctrl, clips, locomotion, armedLocomotion);
        AddCodeDrivenStates(ctrl, clips);
        AddUpperBodyLayer(ctrl, clips);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Selection.activeObject = ctrl;
        Debug.Log("[Helena] PartnerAnimator.controller built: Locomotion (Idle 0 -> Walk " + WalkSpeed + ") + Turn (L/R 90, L/R 180) + Pickup (Low, Medium) + Armed (2D) + Melee (2 stabs, blunt strike) + Seat (sit, type, stand) + UpperBody (pistol / shotgun / blade / blunt, reloads).");
    }

    // Every clip of the controller, loaded once (missing ones are listed in MissingClips).
    private sealed class Clips
    {
        // Locomotion, turns and pickups.
        public AnimationClip Idle, Walk, TurnL, TurnR, TurnL180, TurnR180, PickLow, PickMedium;
        // Weapons: pistol / shotgun / knife / blunt holds and moves, the swings and the reloads.
        public AnimationClip PistolIdle, PistolAim, PistolLeft, PistolRight, PistolBack, ShotgunIdle, ShotgunAim;
        public AnimationClip KnifeIdle, KnifeStab1, KnifeStab2, BluntStrike, BluntReady, ReloadPistol, ReloadShotgun;
        // Seat, doors and the dealer clips of the blackjack table.
        public AnimationClip StandToSit, Typing, SitToStand, DoorInwards, DoorOutwards, DoorLocked;
        public AnimationClip[] Dealer;
        // Hurt: limping locomotion, death, fallen, hit reactions (unarmed + one per weapon hold); evasion; keypad.
        public AnimationClip InjuredIdle, InjuredWalk, InjuredBack, Death, Fallen;
        public List<AnimationClip> Hits;
        public AnimationClip[] Evade, Keypad;
        // Every required clip was found.
        public bool AllFound;

        // Loads them all in a fixed order (the error lists the missing ones).
        public static Clips Load()
        {
            MissingClips.Clear();
            var c = new Clips
            {
                Idle = LoadClip(HelenaSetupMenu.ModelPath),
                Walk = LoadClip(WalkPath),
                TurnL = LoadClip(TurnLeftPath),
                TurnR = LoadClip(TurnRightPath),
                TurnL180 = LoadClip(TurnLeft180Path),
                TurnR180 = LoadClip(TurnRight180Path),
                PickLow = LoadClip(PickupLowPath),
                PickMedium = LoadClip(PickupMediumPath),
                PistolIdle = LoadClip(PistolIdlePath),
                PistolAim = LoadClip(PistolAimPath),
                PistolLeft = LoadClip(PistolStrafeLeftPath),
                PistolRight = LoadClip(PistolStrafeRightPath),
                PistolBack = LoadClip(PistolBackPath),
                ShotgunIdle = LoadClip(ShotgunIdlePath),
                ShotgunAim = LoadClip(ShotgunAimPath),
                KnifeIdle = LoadClip(KnifeIdlePath),
                KnifeStab1 = LoadClip(KnifeStab1Path),
                KnifeStab2 = LoadClip(KnifeStab2Path),
                BluntStrike = LoadClip(BluntStrikePath, BluntStrikeClip),
                BluntReady = LoadClip(BluntStrikePath, BluntReadyClip),
                StandToSit = LoadClip(StandToSitPath),
                Typing = LoadClip(TypingPath),
                SitToStand = LoadClip(SitToStandPath),
                ReloadPistol = LoadClip(ReloadPistolPath),
                ReloadShotgun = LoadClip(ReloadShotgunPath),
                DoorInwards = LoadClip(DoorInwardsPath),
                DoorOutwards = LoadClip(DoorOutwardsPath),
                DoorLocked = LoadClip(DoorLockedPath),
            };
            c.Dealer = new AnimationClip[PartnerAnimator.DealerClips.Length];
            bool dealerOk = true;
            for (int i = 0; i < c.Dealer.Length; i++)
            {
                c.Dealer[i] = LoadClip(AnimFolder + PartnerAnimator.DealerClips[i] + ".fbx");
                dealerOk &= c.Dealer[i] != null;
            }
            // hurt: limping locomotion, hit reactions (unarmed + one per weapon hold), death
            c.InjuredIdle = LoadClip(AnimFolder + "HelenaInjuredHurtingIdle.fbx");
            c.InjuredWalk = LoadClip(AnimFolder + "HelenaInjuredWalk.fbx");
            c.InjuredBack = LoadClip(AnimFolder + "HelenaInjuredWalkBackwards.fbx");
            c.Death = LoadClip(AnimFolder + PartnerAnimator.DeathClip + ".fbx");
            c.Fallen = LoadClip(AnimFolder + PartnerAnimator.FallenClip + ".fbx");
            bool hurtOk = c.InjuredIdle != null && c.InjuredWalk != null && c.InjuredBack != null && c.Death != null && c.Fallen != null;
            c.Hits = new List<AnimationClip>();
            foreach (string name in PartnerAnimator.HitClips) c.Hits.Add(LoadClip(AnimFolder + name + ".fbx"));
            foreach (string prefix in PartnerAnimator.ArmedHitPrefixes)
                foreach (string hold in PartnerAnimator.HoldNames) c.Hits.Add(LoadClip(AnimFolder + prefix + "_" + hold + ".fbx"));
            hurtOk &= c.Hits.TrueForAll(h => h != null);
            c.Evade = new AnimationClip[PartnerAnimator.EvadeClips.Length];
            for (int i = 0; i < c.Evade.Length; i++)
            {
                c.Evade[i] = LoadClip(AnimFolder + PartnerAnimator.EvadeClips[i] + ".fbx");
                dealerOk &= c.Evade[i] != null;
            }
            // keypad: three clips cut from HelenaButton_Pushing (HelenaImport)
            c.Keypad = new AnimationClip[PartnerAnimator.KeypadClips.Length];
            for (int i = 0; i < c.Keypad.Length; i++)
            {
                string keypadPath = AnimFolder + PartnerAnimator.KeypadClipFile + ".fbx";
                c.Keypad[i] = LoadClip(keypadPath, PartnerAnimator.KeypadClips[i]);
                if (c.Keypad[i] == null) MissingClips.Add(keypadPath + " (" + PartnerAnimator.KeypadClips[i] + ")");
                dealerOk &= c.Keypad[i] != null;
            }
            c.AllFound = c.Idle != null && c.Walk != null && c.TurnL != null && c.TurnR != null && c.TurnL180 != null && c.TurnR180 != null &&
                         c.PickLow != null && c.PickMedium != null &&
                         c.PistolIdle != null && c.PistolAim != null && c.PistolLeft != null && c.PistolRight != null && c.PistolBack != null &&
                         c.ShotgunIdle != null && c.ShotgunAim != null &&
                         c.KnifeIdle != null && c.KnifeStab1 != null && c.KnifeStab2 != null && c.BluntStrike != null && c.BluntReady != null &&
                         c.StandToSit != null && c.Typing != null && c.SitToStand != null && c.ReloadPistol != null && c.ReloadShotgun != null &&
                         c.DoorInwards != null && c.DoorOutwards != null && c.DoorLocked != null && dealerOk && hurtOk;
            return c;
        }
    }

    // Every parameter PartnerAnimator drives.
    private static void AddParameters(AnimatorController ctrl)
    {
        ctrl.AddParameter(PartnerAnimator.SpeedParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.TurnParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.TurnAngleParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.PickupParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.PickupHeightParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(PartnerAnimator.ArmedParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.AimingParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.MoveXParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.MoveZParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.WeaponTypeParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PartnerAnimator.AttackParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PartnerAnimator.AttackIdParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(PartnerAnimator.SeatedParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.ReloadingParam, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PartnerAnimator.ReloadAbortParam, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.AttackSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });
        ctrl.AddParameter(PartnerAnimator.SeatActivityParam, AnimatorControllerParameterType.Int);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.DoorSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });
        ctrl.AddParameter(PartnerAnimator.InjuredParam, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PartnerAnimator.EvadeSpeedParam,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });
    }

    // Base layer movement: locomotion (with its limping blend), turn in place, armed locomotion and the transitions between them.
    private static void AddLocomotion(AnimatorController ctrl, Clips clips, out AnimatorState locomotion, out AnimatorState armedLocomotion)
    {
        locomotion = ctrl.CreateBlendTreeInController(PartnerAnimator.LocomotionState, out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = PartnerAnimator.SpeedParam;
        tree.useAutomaticThresholds = false;
        // each one blends to its injured version by Injured (0 fine .. 1 limping)
        HurtBlend(tree.CreateBlendTreeChild(0f), clips.Idle, clips.InjuredIdle);
        HurtBlend(tree.CreateBlendTreeChild(WalkSpeed), clips.Walk, clips.InjuredWalk);
        ctrl.layers[0].stateMachine.defaultState = locomotion;

        // turn in place: one state, the clip is picked by TurnAngle (exact thresholds)
        AnimatorState turn = ctrl.CreateBlendTreeInController("Turn", out BlendTree turnTree, 0);
        turn.tag = PartnerAnimator.TurnTag;
        turnTree.blendType = BlendTreeType.Simple1D;
        turnTree.blendParameter = PartnerAnimator.TurnAngleParam;
        turnTree.useAutomaticThresholds = false;
        turnTree.AddChild(clips.TurnL180, PartnerAnimator.SlotLeft180);
        turnTree.AddChild(clips.TurnL, PartnerAnimator.SlotLeft90);
        turnTree.AddChild(clips.TurnR, PartnerAnimator.SlotRight90);
        turnTree.AddChild(clips.TurnR180, PartnerAnimator.SlotRight180);

        // armed locomotion: 2D blend by local velocity, each clip placed at its real speed
        armedLocomotion = ctrl.CreateBlendTreeInController(PartnerAnimator.ArmedLocomotionState, out BlendTree armedTree, 0);
        armedTree.blendType = BlendTreeType.FreeformDirectional2D;
        armedTree.blendParameter = PartnerAnimator.MoveXParam;
        armedTree.blendParameterY = PartnerAnimator.MoveZParam;
        armedTree.AddChild(clips.PistolIdle, Vector2.zero);
        HurtBlend(armedTree.CreateBlendTreeChild(new Vector2(0f, WalkSpeed)), clips.Walk, clips.InjuredWalk);   // no pistol walk: legs walk, the upper-body layer holds the gun
        HurtBlend(armedTree.CreateBlendTreeChild(new Vector2(0f, -PistolBackSpeed)), clips.PistolBack, clips.InjuredBack);
        armedTree.AddChild(clips.PistolLeft, new Vector2(-PistolStrafeLeftSpeed, 0f));
        armedTree.AddChild(clips.PistolRight, new Vector2(PistolStrafeRightSpeed, 0f));

        AnimatorStateTransition toArmed = locomotion.AddTransition(armedLocomotion);
        toArmed.hasExitTime = false;
        toArmed.duration = 0.25f;
        toArmed.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ArmedParam);
        AnimatorStateTransition toUnarmed = armedLocomotion.AddTransition(locomotion);
        toUnarmed.hasExitTime = false;
        toUnarmed.duration = 0.25f;
        toUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.ArmedParam);

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition toTurn = source.AddTransition(turn);
            toTurn.hasExitTime = false;
            toTurn.duration = 0.15f;
            toTurn.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.TurnParam);
        }

        AnimatorStateTransition toLocomotion = turn.AddTransition(locomotion);
        toLocomotion.hasExitTime = true;
        toLocomotion.exitTime = 0.85f;
        toLocomotion.duration = 0.2f;
    }

    // Base layer actions: pickups, melee swings (with the blade combo), the seat, doors and the keypad.
    private static void AddActions(AnimatorController ctrl, Clips clips, AnimatorState locomotion, AnimatorState armedLocomotion)
    {
        // pick up: one state per height, chosen by PickupHeight
        AddPickupState(ctrl, locomotion, armedLocomotion, "PickupLow", clips.PickLow, PickupHeight.Low, new Vector3(300f, 120f, 0f));
        AddPickupState(ctrl, locomotion, armedLocomotion, "PickupMedium", clips.PickMedium, PickupHeight.Medium, new Vector3(300f, 200f, 0f));

        // melee: full-body swings chosen by AttackId, speed driven by AttackSpeed (phases of each weapon)
        AnimatorState stab1 = AddAttackState(ctrl, locomotion, armedLocomotion, "KnifeStab1", clips.KnifeStab1, PartnerMelee.KnifeStab1, new Vector3(550f, 0f, 0f));
        AnimatorState stab2 = AddAttackState(ctrl, locomotion, armedLocomotion, "KnifeStab2", clips.KnifeStab2, PartnerMelee.KnifeStab2, new Vector3(550f, 80f, 0f));
        AnimatorState blunt = AddAttackState(ctrl, locomotion, armedLocomotion, "BluntStrike", clips.BluntStrike, PartnerMelee.BluntStrike, new Vector3(550f, 160f, 0f));
        // blade combo: stab -> other stab directly (PartnerMelee only fires it after the strike), before the exits
        AddComboTransition(stab1, stab2, PartnerMelee.KnifeStab2);
        AddComboTransition(stab2, stab1, PartnerMelee.KnifeStab1);
        AddAttackExit(stab1, armedLocomotion, KnifeStab1Exit);
        AddAttackExit(stab2, armedLocomotion, KnifeStab2Exit);
        AddAttackExit(blunt, armedLocomotion, BluntStrikeExit);

        // seat (save terminal): sit down -> typing loop while Seated -> stand up. Root motion applied by PartnerAnimator.
        AddSeatStates(ctrl, locomotion, armedLocomotion, clips.StandToSit, clips.Typing, clips.SitToStand, clips.Dealer);

        // doors: entered / left from code (PartnerDoorUser via PartnerAnimator), played forward or backward (DoorSpeed)
        AddDoorState(ctrl, PartnerAnimator.DoorInwardsState, clips.DoorInwards, new Vector3(300f, 460f, 0f));
        AddDoorState(ctrl, PartnerAnimator.DoorOutwardsState, clips.DoorOutwards, new Vector3(550f, 460f, 0f));
        AddDoorState(ctrl, PartnerAnimator.DoorLockedState, clips.DoorLocked, new Vector3(800f, 460f, 0f));
        // keypad states, entered from code (PartnerKeypadUser): no transitions, each holds its last frame
        for (int i = 0; i < clips.Keypad.Length; i++)
        {
            AnimatorState k = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.KeypadStates[i], new Vector3(300f + 250f * i, 540f, 0f));
            k.motion = clips.Keypad[i];
            k.tag = PartnerAnimator.KeypadTag;
        }
    }

    // States entered only from code: hit reactions, death, the floor sit (optional), the cutscene slots, fallen and the evades.
    private static void AddCodeDrivenStates(AnimatorController ctrl, Clips clips)
    {
        // hit reactions and death: entered from code (PartnerDamage via PartnerAnimator); hits end from code too
        for (int i = 0; i < clips.Hits.Count; i++)
        {
            AnimatorState hit = ctrl.layers[0].stateMachine.AddState(clips.Hits[i].name, new Vector3(1300f + 220f * (i / 4), 60f + 70f * (i % 4), 0f));
            hit.motion = clips.Hits[i];
            hit.tag = PartnerAnimator.HitTag;
            hit.speed = 1.15f;
        }
        AnimatorState deathState = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.DeathState, new Vector3(1300f, 400f, 0f));
        deathState.motion = clips.Death;
        deathState.tag = PartnerAnimator.DeadTag;
        // sitting on the floor while the operator is away (PartnerRest): optional, from HelenaSittingPose.fbx
        AnimationClip floorSit = LoadClip(FloorSitPath, PartnerAnimator.FloorSitClip);
        if (floorSit != null)
        {
            AnimatorState sitFloor = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.FloorSitState, new Vector3(1300f, 540f, 0f));
            sitFloor.motion = floorSit;
        }
        else Debug.LogWarning("[Helena] " + FloorSitPath + " not found: she stays standing while the operator is away (copy HelenaSittingPose.fbx, run menu 0 and 2 again).");
        // cutscenes: two empty states whose clips are swapped at runtime (any clip plays through them)
        CutsceneBuilder.AddSlotStates(ctrl, new Vector3(1300f, 620f, 0f));
        AnimatorState fallenState = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.FallenState, new Vector3(1300f, 470f, 0f));
        fallenState.motion = clips.Fallen;
        fallenState.tag = PartnerAnimator.DeadTag;

        // evasion: dodge / duck, entered and left from code (PartnerEvasion), the duck played backward to get up (EvadeSpeed)
        for (int i = 0; i < clips.Evade.Length; i++)
        {
            AnimatorState evade = ctrl.layers[0].stateMachine.AddState(PartnerAnimator.EvadeStates[i], new Vector3(1050f, 300f + 80f * i, 0f));
            evade.motion = clips.Evade[i];
            evade.tag = PartnerAnimator.EvadeTag;
            evade.speed = EvadeSpeeds[i];
            evade.speedParameter = PartnerAnimator.EvadeSpeedParam;
            evade.speedParameterActive = true;
        }
    }

    // Upper body: weapon idle / aim over any locomotion (weight set by PartnerAnimator), pose picked by WeaponType; the reloads.
    private static void AddUpperBodyLayer(AnimatorController ctrl, Clips clips)
    {
        AvatarMask mask = CreateUpperBodyMask();
        ctrl.AddLayer(PartnerAnimator.UpperBodyLayer);
        AnimatorControllerLayer[] allLayers = ctrl.layers;
        int upper = allLayers.Length - 1;
        allLayers[upper].avatarMask = mask;
        allLayers[upper].defaultWeight = 0f;
        allLayers[upper].blendingMode = AnimatorLayerBlendingMode.Override;
        ctrl.layers = allLayers;

        AnimatorState weaponIdleState = ctrl.CreateBlendTreeInController("WeaponIdle", out BlendTree idleByWeapon, upper);
        idleByWeapon.blendType = BlendTreeType.Simple1D;
        idleByWeapon.blendParameter = PartnerAnimator.WeaponTypeParam;
        idleByWeapon.useAutomaticThresholds = false;
        idleByWeapon.AddChild(clips.PistolIdle, (float)WeaponHoldType.Pistol);
        idleByWeapon.AddChild(clips.ShotgunIdle, (float)WeaponHoldType.Shotgun);
        idleByWeapon.AddChild(clips.KnifeIdle, (float)WeaponHoldType.Blade);
        idleByWeapon.AddChild(clips.BluntReady, (float)WeaponHoldType.Blunt);

        AnimatorState weaponAimState = ctrl.CreateBlendTreeInController("WeaponAim", out BlendTree aimByWeapon, upper);
        aimByWeapon.blendType = BlendTreeType.Simple1D;
        aimByWeapon.blendParameter = PartnerAnimator.WeaponTypeParam;
        aimByWeapon.useAutomaticThresholds = false;
        aimByWeapon.AddChild(clips.PistolAim, (float)WeaponHoldType.Pistol);
        aimByWeapon.AddChild(clips.ShotgunAim, (float)WeaponHoldType.Shotgun);
        aimByWeapon.AddChild(clips.KnifeIdle, (float)WeaponHoldType.Blade);    // melee: same guard pose while "aiming"
        aimByWeapon.AddChild(clips.BluntReady, (float)WeaponHoldType.Blunt);

        AnimatorStateMachine upperSm = ctrl.layers[upper].stateMachine;
        upperSm.defaultState = weaponIdleState;
        AnimatorStateTransition toAim = weaponIdleState.AddTransition(weaponAimState);
        toAim.hasExitTime = false;
        toAim.duration = 0.15f;
        toAim.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AimingParam);
        AnimatorStateTransition toLowReady = weaponAimState.AddTransition(weaponIdleState);
        toLowReady.hasExitTime = false;
        toLowReady.duration = 0.25f;
        toLowReady.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.AimingParam);

        // reloads (upper body: she can keep walking), clip picked by WeaponType
        AnimatorState reloadPistolState = upperSm.AddState("ReloadPistol", new Vector3(500f, 60f, 0f));
        reloadPistolState.motion = clips.ReloadPistol;
        reloadPistolState.tag = PartnerAnimator.ReloadTag;
        reloadPistolState.speed = ReloadPistolSpeed;
        AnimatorState reloadShotgunState = upperSm.AddState("ReloadShotgun", new Vector3(500f, 140f, 0f));
        reloadShotgunState.motion = clips.ReloadShotgun;
        reloadShotgunState.tag = PartnerAnimator.ReloadTag;
        foreach (AnimatorState from in new[] { weaponIdleState, weaponAimState })
        {
            AnimatorStateTransition toPistol = from.AddTransition(reloadPistolState);
            toPistol.hasExitTime = false;
            toPistol.duration = 0.2f;
            toPistol.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadingParam);
            toPistol.AddCondition(AnimatorConditionMode.Less, 0.5f, PartnerAnimator.WeaponTypeParam);
            AnimatorStateTransition toShotgun = from.AddTransition(reloadShotgunState);
            toShotgun.hasExitTime = false;
            toShotgun.duration = 0.2f;
            toShotgun.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadingParam);
            toShotgun.AddCondition(AnimatorConditionMode.Greater, 0.5f, PartnerAnimator.WeaponTypeParam);
            toShotgun.AddCondition(AnimatorConditionMode.Less, 1.5f, PartnerAnimator.WeaponTypeParam);
        }
        foreach (AnimatorState reload in new[] { reloadPistolState, reloadShotgunState })
        {
            AnimatorStateTransition abort = reload.AddTransition(weaponIdleState);   // shot / weapon switch: leave at once
            abort.hasExitTime = false;
            abort.duration = 0.15f;
            abort.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.ReloadAbortParam);
        }
        // pistol: one magazine, then back (the clip's hands are back on the gun at ~0.92)
        AnimatorStateTransition pistolDone = reloadPistolState.AddTransition(weaponIdleState);
        pistolDone.hasExitTime = true;
        pistolDone.exitTime = 0.92f;
        pistolDone.duration = 0.2f;
        // shotgun: one shell per loop, leaves at the end of the loop once Reloading is off
        AnimatorStateTransition shotgunDone = reloadShotgunState.AddTransition(weaponIdleState);
        shotgunDone.hasExitTime = true;
        shotgunDone.exitTime = 0.98f;
        shotgunDone.duration = 0.15f;
        shotgunDone.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.ReloadingParam);
    }

    // empties a controller asset: no parameters, no layers, no states / trees / transitions left inside it; one empty base layer
    private static void ClearController(AnimatorController ctrl)
    {
        ctrl.layers = new AnimatorControllerLayer[0];
        ctrl.parameters = new AnimatorControllerParameter[0];
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (sub == null || sub == ctrl) continue;
            AssetDatabase.RemoveObjectFromAsset(sub);
            Object.DestroyImmediate(sub, true);
        }
        ctrl.AddLayer("Base Layer");
    }

    private static readonly List<string> MissingClips = new List<string>();   // paths LoadClip could not find (listed in the error)

    // An animation clip from an FBX file.
    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) MissingClips.Add(path);
        return Adjusted(clip);
    }

    // one of several clips cut from the same file (HelenaImport)
    private static AnimationClip LoadClip(string path, string clipName)
    {
        return Adjusted(AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName));
    }

    // with Helena 2: her copy of the clip adjusted to her (weapon clips, Last Terminal/21) when there is one
    private static AnimationClip Adjusted(AnimationClip clip)
    {
        if (clip == null || !HelenaSetupMenu.UseHelena2) return clip;
        var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{HelenaClipBaker.OutFolder}/{clip.name}{HelenaClipBaker.Suffix}.anim");
        return copy != null ? copy : clip;
    }

    // Avatar mask for the upper-body weapon layer.
    private static AvatarMask CreateUpperBodyMask()
    {
        var mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        AvatarMaskBodyPart[] upper =
        {
            AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
        };
        foreach (AvatarMaskBodyPart part in upper) mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.DeleteAsset(UpperBodyMaskPath);
        AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        return mask;
    }

    // Adds the pickup states (low / medium) and their transitions.
    private static void AddPickupState(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                       string stateName, AnimationClip clip, PickupHeight height, Vector3 graphPosition)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.AddState(stateName, graphPosition);
        state.motion = clip;
        state.tag = PartnerAnimator.PickupTag;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.2f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.PickupParam);
            enter.AddCondition(AnimatorConditionMode.Equals, (int)height, PartnerAnimator.PickupHeightParam);
        }

        AnimatorStateTransition exit = state.AddTransition(locomotion);
        exit.hasExitTime = true;
        exit.exitTime = 0.92f;
        exit.duration = 0.25f;
    }

    // Adds a melee attack state and its transitions.
    private static AnimatorState AddAttackState(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                                string stateName, AnimationClip clip, int attackId, Vector3 graphPosition)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.AddState(stateName, graphPosition);
        state.motion = clip;
        state.tag = PartnerAnimator.AttackTag;
        state.speedParameterActive = true;
        state.speedParameter = PartnerAnimator.AttackSpeedParam;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AttackParam);
            enter.AddCondition(AnimatorConditionMode.Equals, attackId, PartnerAnimator.AttackIdParam);
        }
        return state;
    }

    // Combo transition from one attack to the next.
    private static void AddComboTransition(AnimatorState from, AnimatorState to, int attackId)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.12f;
        t.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.AttackParam);
        t.AddCondition(AnimatorConditionMode.Equals, attackId, PartnerAnimator.AttackIdParam);
    }

    // Exit transition of an attack back to armed locomotion.
    private static void AddAttackExit(AnimatorState state, AnimatorState armedLocomotion, float exitTime)
    {
        AnimatorStateTransition exit = state.AddTransition(armedLocomotion);
        exit.hasExitTime = true;
        exit.exitTime = exitTime;
        exit.duration = 0.25f;
    }

    // a 1D child tree on Injured: the normal clip at 0, the hurt one at 1
    private static void HurtBlend(BlendTree child, AnimationClip normal, AnimationClip hurt)
    {
        child.blendType = BlendTreeType.Simple1D;
        child.blendParameter = PartnerAnimator.InjuredParam;
        child.useAutomaticThresholds = false;
        child.AddChild(normal, 0f);
        child.AddChild(hurt, 1f);
    }

    // Adds a door clip state.
    private static void AddDoorState(AnimatorController ctrl, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = ctrl.layers[0].stateMachine.AddState(name, position);
        state.motion = clip;
        state.tag = PartnerAnimator.DoorTag;
        state.speedParameter = PartnerAnimator.DoorSpeedParam;
        state.speedParameterActive = true;
    }

    // Adds the seat states (sit down, typing, stand up, dealer clips).
    private static void AddSeatStates(AnimatorController ctrl, AnimatorState locomotion, AnimatorState armedLocomotion,
                                      AnimationClip standToSit, AnimationClip typing, AnimationClip sitToStand, AnimationClip[] dealerClips)
    {
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sit = sm.AddState(PartnerAnimator.SitDownState, new Vector3(300f, 300f, 0f));
        sit.motion = standToSit;
        sit.tag = PartnerAnimator.SeatTag;
        AnimatorState seated = sm.AddState(PartnerAnimator.SeatedState, new Vector3(550f, 300f, 0f));
        seated.motion = typing;
        seated.tag = PartnerAnimator.SeatTag;
        AnimatorState stand = sm.AddState(PartnerAnimator.StandUpState, new Vector3(550f, 380f, 0f));
        stand.motion = sitToStand;
        stand.tag = PartnerAnimator.SeatTag;

        foreach (AnimatorState source in new[] { locomotion, armedLocomotion })
        {
            AnimatorStateTransition enter = source.AddTransition(sit);
            enter.hasExitTime = false;
            enter.duration = 0.25f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, PartnerAnimator.SeatedParam);
        }
        // seated by ~0.9 of StandToSit (hips settle at 0.72, hands still moving): typing or dealing (SeatActivity)
        AnimatorStateTransition toTyping = sit.AddTransition(seated);
        toTyping.hasExitTime = true;
        toTyping.exitTime = 0.9f;
        toTyping.duration = 0.35f;
        toTyping.AddCondition(AnimatorConditionMode.Equals, (int)PartnerAnimator.SeatAnimation.Typing, PartnerAnimator.SeatActivityParam);

        // dealer (blackjack table): played from code (PartnerBlackjackDealer), back to holding / waiting at the end
        var dealer = new AnimatorState[dealerClips.Length];
        for (int i = 0; i < dealerClips.Length; i++)
        {
            dealer[i] = sm.AddState(PartnerAnimator.DealerStates[i], new Vector3(300f + 200f * (i % 3), 560f + 70f * (i / 3), 0f));
            dealer[i].motion = dealerClips[i];
            dealer[i].tag = PartnerAnimator.SeatTag;
            dealer[i].speed = DealerSpeeds[i];
            AnimatorStateTransition up = dealer[i].AddTransition(stand);   // getting up from any dealer clip
            up.hasExitTime = false;
            up.duration = 0.3f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.SeatedParam);
        }
        AnimatorState dealerIdle = dealer[(int)PartnerAnimator.DealerClip.Idle];
        AnimatorState dealerHold = dealer[(int)PartnerAnimator.DealerClip.Hold];
        AnimatorStateTransition toDealer = sit.AddTransition(dealerIdle);
        toDealer.hasExitTime = true;
        toDealer.exitTime = 0.9f;
        toDealer.duration = 0.35f;
        toDealer.AddCondition(AnimatorConditionMode.Equals, (int)PartnerAnimator.SeatAnimation.Dealer, PartnerAnimator.SeatActivityParam);
        foreach (PartnerAnimator.DealerClip one in new[] { PartnerAnimator.DealerClip.PickDeck, PartnerAnimator.DealerClip.Deal, PartnerAnimator.DealerClip.Check })
        {
            AnimatorStateTransition back = dealer[(int)one].AddTransition(dealerHold);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.2f;
        }
        AnimatorStateTransition putBack = dealer[(int)PartnerAnimator.DealerClip.PutBack].AddTransition(dealerIdle);
        putBack.hasExitTime = true;
        putBack.exitTime = 0.95f;
        putBack.duration = 0.25f;
        AnimatorStateTransition toStand = seated.AddTransition(stand);
        toStand.hasExitTime = false;
        toStand.duration = 0.3f;
        toStand.AddCondition(AnimatorConditionMode.IfNot, 0f, PartnerAnimator.SeatedParam);
        // standing still by ~0.9 of SitToStand
        AnimatorStateTransition toIdle = stand.AddTransition(locomotion);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.9f;
        toIdle.duration = 0.25f;
    }
}
