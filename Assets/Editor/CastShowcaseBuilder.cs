using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu "Last Terminal/24 - Cast Showcase": a test scene with the main cast side by side (Helena 2 in her clothes and
// in her school uniform, Mia in both outfits, Jeremy) playing the same everyday clip at the same time, every one in
// turn (ModelCompare: Right / Left = clip, Space = pause, Q / E = turn, Up / Down = speed). No combat or injured
// clips. Helena's uniform goes on through her outfits (OutfitBuilder): the scene also checks that the outfit works.
internal static class CastShowcaseBuilder
{
    // The scene, the clip folders and the cast models.
    private const string ScenePath = "Assets/Scenes/CastShowcase.unity";
    private const string HelenaAnimFolder = "Assets/Characters/Helena/Animations";
    private const string CastAnimFolder = "Assets/Characters/Cast/Animations";
    private const string CastModelFolder = "Assets/Characters/Cast/Models";
    private const string SchoolOutfitKey = "outfit_school";
    // Space between two models (m) and how far the camera stands.
    private const float Spacing = 1.0f;
    private const float CameraDistance = 6.6f;

    // Clip names with any of these are not everyday moves (combat, hits, injuries, weapons) or belong to one character.
    private static readonly string[] Excluded =
    {
        "Hit", "Aim", "Shotgun", "Pistol", "Knife", "Blunt", "Blade", "Bat", "Gun", "Death", "Dying", "Dodg", "Duck",
        "Injured", "Hurt", "Fallen", "Reload", "Strike", "Attack", "Stab", "Kick", "Punch", "Fight", "Combat", "Melee",
        "Shoot", "Weapon", "Stagger", "Limp", "Pig",
    };

    // The cast: the model file, its label, the outfit Helena wears (null = none / not Helena).
    private static readonly (string path, string label, string outfit)[] Cast =
    {
        (HelenaCompareBuilder.ModelPath, "Helena", null),
        (HelenaCompareBuilder.ModelPath, "Helena (escola)", SchoolOutfitKey),
        (CastModelFolder + "/Mia.fbx", "Mia", null),
        (CastModelFolder + "/Mia_School.fbx", "Mia (escola)", null),
        (CastModelFolder + "/Jeremy.fbx", "Jeremy", null),
    };

    // Menu 24: builds and saves the scene.
    [MenuItem("Last Terminal/24 - Cast Showcase (main cast, everyday clips)")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AnimationClip[] clips = Clips();
        if (clips.Length == 0) { Debug.LogError($"[Cast] No clips in {HelenaAnimFolder} / {CastAnimFolder}."); return; }
        Shader lit = EditorAssets.DefaultLitShader();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera cam = HelenaCompareBuilder.BuildStage(CameraDistance);

        var animators = new List<Animator>();
        var labels = new List<string>();
        var present = Cast.Where(c => AssetDatabase.LoadAssetAtPath<GameObject>(c.path) != null).ToArray();
        foreach (var c in Cast.Except(present)) Debug.LogWarning($"[Cast] {c.path} not found: {c.label} left out.");
        for (int i = 0; i < present.Length; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(present[i].path);
            float x = (i - (present.Length - 1) * 0.5f) * Spacing;
            Animator a = HelenaCompareBuilder.Place(model, present[i].label, x);
            if (present[i].outfit != null) OutfitBuilder.AddTo(a.gameObject, lit, present[i].outfit);   // worn at Start
            animators.Add(a);
            labels.Add(present[i].label);
        }

        var none = Enumerable.Repeat(-1, clips.Length).ToArray();
        var compare = new GameObject("Showcase").AddComponent<ModelCompare>();
        compare.EditorSet(animators.ToArray(), labels.ToArray(), clips, cam, new Weapon[0], 0, none, new bool[clips.Length], null, false);

        EditorAssets.EnsureFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        Debug.Log($"[Cast] {ScenePath}: {animators.Count} characters, {clips.Length} clips. Play: Right / Left = clip, Space = pause, Q / E = turn.");
    }

    // Helena's clips and the cast's, everyday ones only (by name), sorted.
    private static AnimationClip[] Clips()
    {
        var paths = new List<string>();
        foreach (string folder in new[] { HelenaAnimFolder, CastAnimFolder })
            if (AssetDatabase.IsValidFolder(folder))
                paths.AddRange(AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));
        return paths.Distinct()
                    .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                    .Where(c => !c.name.StartsWith("__preview__") && !Excluded.Any(e => c.name.Contains(e)))
                    .Distinct()
                    .OrderBy(c => c.name)
                    .ToArray();
    }
}
