using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// How Helena 2 holds each weapon, computed in Blender on her model with her clips and the weapon models
// (tools/grip.py): the weapon placed like WeaponHolder does, moved so its handle sits in her palm (behind the knuckles,
// resting on the palm, out of the hand), then every finger closed joint by joint until it touches the weapon.
// grip = the weapon's GripPoint in the right-hand frame (aim and idle); fingers = direction of each finger segment
// (thumb, index, middle, ring, little x proximal / intermediate / distal) in the hand frame: right = (fingers, across,
// out of the palm) like the holds; left = (fingers, across, palm) like TryGetLeftHandBasis (two-handed weapons).
internal static class WeaponGrips
{
    // Weapon prefabs folder.
    private const string PrefabFolder = "Assets/Prefabs/Weapons";

    // Per weapon: grip point and finger directions (right / left hand), computed in Blender.
    private static readonly (string name, Vector3 grip, Vector3[] right, Vector3[] left)[] Grips =
    {
        // Pistol1911
        ("Pistol1911", new Vector3(0.022f, 0.018f, 0.022f),
        new[]
        {
            new Vector3(0.6496f, 0.7379f, 0.183f), new Vector3(0.8563f, 0.1125f, 0.5041f), new Vector3(0.759f, 0.5227f, 0.3882f),
            new Vector3(0.999f, -0.0001f, 0.0436f), new Vector3(0.9848f, 0f, 0.1736f), new Vector3(0.9848f, 0f, 0.1736f),
            new Vector3(0.9397f, 0f, 0.342f), new Vector3(0.2588f, -0.0003f, 0.9659f), new Vector3(-0.4617f, 0f, 0.887f),
            new Vector3(0.7661f, -0.0002f, 0.6428f), new Vector3(0.1305f, 0.0002f, 0.9914f), new Vector3(-0.4226f, 0f, 0.9063f),
            new Vector3(0.866f, 0f, 0.5f), new Vector3(0.2164f, -0.0002f, 0.9763f), new Vector3(-0.1305f, 0f, 0.9914f),
        },
        null),
        // DesertEagle
        ("DesertEagle", new Vector3(0.024f, 0.024f, 0.025f),
        new[]
        {
            new Vector3(0.6496f, 0.7379f, 0.183f), new Vector3(0.8563f, 0.1125f, 0.5041f), new Vector3(0.759f, 0.5227f, 0.3882f),
            new Vector3(0.9848f, -0.0001f, 0.1737f), new Vector3(0.9763f, 0f, 0.2164f), new Vector3(0.9763f, 0f, 0.2164f),
            new Vector3(0.3007f, 0f, 0.9537f), new Vector3(-0.0871f, 0f, 0.9962f), new Vector3(-0.8192f, 0f, 0.5736f),
            new Vector3(0.1305f, -0.0001f, 0.9914f), new Vector3(0.0436f, -0.0001f, 0.999f), new Vector3(-0.6428f, 0f, 0.766f),
            new Vector3(0.3827f, 0f, 0.9239f), new Vector3(0.2165f, 0.0002f, 0.9763f), new Vector3(-0.2164f, 0f, 0.9763f),
        },
        null),
        // ShotgunMossberg
        ("ShotgunMossberg", new Vector3(-0.019f, -0.035f, 0.02f),
        new[]
        {
            new Vector3(0.5702f, 0.6322f, 0.5245f), new Vector3(0.8103f, -0.4447f, 0.3816f), new Vector3(0.8878f, -0.318f, 0.3326f),
            new Vector3(-0.0871f, 0f, 0.9962f), new Vector3(-0.9397f, 0.0003f, -0.342f), new Vector3(0.2588f, 0f, -0.9659f),
            new Vector3(0.2589f, 0f, 0.9659f), new Vector3(0.1737f, 0f, 0.9848f), new Vector3(-0.9659f, 0.0001f, 0.2588f),
            new Vector3(0.9397f, -0.0001f, 0.342f), new Vector3(0.9397f, -0.0001f, 0.342f), new Vector3(0.9239f, -0.0001f, 0.3827f),
            new Vector3(0.9848f, -0.0001f, 0.1736f), new Vector3(0.9848f, -0.0001f, 0.1736f), new Vector3(0.9848f, -0.0001f, 0.1736f),
        },
        new[]
        {
            new Vector3(0.681f, 0.6278f, -0.377f), new Vector3(0.75f, 0.6575f, 0.0719f), new Vector3(0.8189f, 0.3976f, -0.4139f),
            new Vector3(1f, 0.0003f, 0f), new Vector3(-0.1305f, 0f, 0.9914f), new Vector3(-0.9397f, 0f, 0.342f),
            new Vector3(1f, 0.0001f, 0.0003f), new Vector3(-0.2588f, 0f, 0.9659f), new Vector3(-0.9659f, 0f, 0.2588f),
            new Vector3(-0.0871f, 0f, 0.9962f), new Vector3(-0.9397f, 0f, 0.342f), new Vector3(-0.9397f, 0f, 0.342f),
            new Vector3(-0.0871f, 0f, 0.9962f), new Vector3(-0.9848f, 0f, 0.1736f), new Vector3(-0.9848f, 0f, 0.1736f),
        }),
        // KitchenKnife
        ("KitchenKnife", new Vector3(0.016f, -0.048f, 0.014f),
        new[]
        {
            new Vector3(0.9772f, 0.1869f, -0.1011f), new Vector3(0.637f, -0.5135f, 0.575f), new Vector3(0.6195f, -0.7331f, 0.2808f),
            new Vector3(-0.0872f, 0.0002f, 0.9962f), new Vector3(-0.9397f, 0f, -0.342f), new Vector3(0.2588f, 0f, -0.9659f),
            new Vector3(0.3007f, 0f, 0.9537f), new Vector3(-0.9659f, 0f, 0.2588f), new Vector3(-0.9962f, 0f, -0.0872f),
            new Vector3(0.3007f, 0f, 0.9537f), new Vector3(-0.5736f, 0.0001f, 0.8191f), new Vector3(-0.9848f, 0f, -0.1737f),
            new Vector3(0.766f, 0.0002f, 0.6428f), new Vector3(-0.4226f, 0f, 0.9063f), new Vector3(-0.8434f, 0f, 0.5373f),
        },
        null),
        // Cleaver
        ("Cleaver", new Vector3(0.027f, 0.014f, 0.02f),
        new[]
        {
            new Vector3(0.8535f, 0.5115f, -0.0995f), new Vector3(0.7832f, -0.2836f, 0.5533f), new Vector3(0.8365f, -0.4868f, 0.2517f),
            new Vector3(0.8434f, 0f, 0.5372f), new Vector3(-0.0872f, 0f, 0.9962f), new Vector3(-0.766f, 0f, 0.6428f),
            new Vector3(0.5373f, 0f, 0.8434f), new Vector3(-0.3007f, 0.0001f, 0.9537f), new Vector3(-0.9763f, 0f, 0.2164f),
            new Vector3(0.5f, 0.0001f, 0.866f), new Vector3(-0.0872f, 0f, 0.9962f), new Vector3(-0.9063f, 0f, 0.4226f),
            new Vector3(0.8433f, 0.0001f, 0.5374f), new Vector3(0.2587f, 0f, 0.9659f), new Vector3(-0.2588f, 0f, 0.9659f),
        },
        null),
        // Machete
        ("Machete", new Vector3(0.03f, -0.022f, 0.014f),
        new[]
        {
            new Vector3(0.8535f, 0.5115f, -0.0995f), new Vector3(0.7832f, -0.2836f, 0.5533f), new Vector3(0.8365f, -0.4868f, 0.2517f),
            new Vector3(0.9537f, 0f, -0.3007f), new Vector3(0.3827f, 0f, 0.9239f), new Vector3(-0.2588f, 0f, 0.9659f),
            new Vector3(0.9914f, 0f, 0.1305f), new Vector3(-0.0436f, 0f, 0.999f), new Vector3(-0.887f, 0f, 0.4617f),
            new Vector3(1f, 0f, 0f), new Vector3(0.3007f, 0f, 0.9537f), new Vector3(-0.6428f, 0f, 0.766f),
            new Vector3(0.9063f, 0f, -0.4226f), new Vector3(0.8434f, 0f, 0.5373f), new Vector3(0.7071f, 0f, 0.7071f),
        },
        null),
        // Hammer
        ("Hammer", new Vector3(0.021f, 0.008f, 0.017f),
        new[]
        {
            new Vector3(0.7437f, 0.6577f, 0.1195f), new Vector3(0.8597f, 0.1048f, 0.4999f), new Vector3(0.8085f, -0.5842f, -0.0709f),
            new Vector3(0.9848f, 0.0002f, 0.1739f), new Vector3(-0.0436f, 0f, 0.999f), new Vector3(-0.6088f, 0f, 0.7933f),
            new Vector3(0.8191f, -0.0001f, 0.5737f), new Vector3(-0.4227f, 0f, 0.9063f), new Vector3(-0.9239f, 0f, 0.3826f),
            new Vector3(0.8869f, -0.0001f, 0.4619f), new Vector3(-0.0437f, 0f, 0.999f), new Vector3(-0.7661f, 0f, 0.6427f),
            new Vector3(0.9763f, -0.0002f, -0.2163f), new Vector3(0.5f, 0f, 0.866f), new Vector3(0.4225f, 0f, 0.9063f),
        },
        null),
        // Bat
        ("Bat", new Vector3(0.021f, 0.006f, 0.02f),
        new[]
        {
            new Vector3(0.7437f, 0.6577f, 0.1195f), new Vector3(0.8597f, 0.1048f, 0.4999f), new Vector3(0.8085f, -0.5842f, -0.0709f),
            new Vector3(0.9914f, -0.0001f, 0.1306f), new Vector3(0.0871f, 0f, 0.9962f), new Vector3(-0.3421f, 0f, 0.9397f),
            new Vector3(0.7933f, -0.0001f, 0.6088f), new Vector3(-0.2165f, 0.0001f, 0.9763f), new Vector3(-0.7072f, 0f, 0.707f),
            new Vector3(0.8434f, -0.0001f, 0.5374f), new Vector3(0.0871f, 0f, 0.9962f), new Vector3(-0.5736f, 0f, 0.8191f),
            new Vector3(1f, 0f, 0f), new Vector3(0.5373f, -0.0001f, 0.8434f), new Vector3(0.2587f, 0f, 0.9659f),
        },
        null),
        // NailBat
        ("NailBat", new Vector3(0.021f, 0.027f, 0.02f),
        new[]
        {
            new Vector3(0.7437f, 0.6577f, 0.1195f), new Vector3(0.8597f, 0.1048f, 0.4999f), new Vector3(0.8085f, -0.5842f, -0.0709f),
            new Vector3(0.9962f, -0.0001f, 0.0872f), new Vector3(-0.0001f, 0f, 1f), new Vector3(-0.3828f, 0f, 0.9238f),
            new Vector3(0.7933f, -0.0001f, 0.6088f), new Vector3(-0.3008f, 0f, 0.9537f), new Vector3(-0.7661f, 0f, 0.6427f),
            new Vector3(0.8191f, 0f, 0.5736f), new Vector3(0.0435f, 0f, 0.9991f), new Vector3(-0.6757f, 0f, 0.7372f),
            new Vector3(0.9915f, 0f, -0.1304f), new Vector3(0.4617f, 0f, 0.887f), new Vector3(0.3006f, 0f, 0.9537f),
        },
        null),
        // Axe
        ("Axe", new Vector3(0.024f, 0.032f, 0.02f),
        new[]
        {
            new Vector3(0.7437f, 0.6577f, 0.1195f), new Vector3(0.8598f, 0.1048f, 0.4998f), new Vector3(0.8085f, -0.5842f, -0.0709f),
            new Vector3(0.9914f, -0.0001f, 0.1306f), new Vector3(0.342f, 0f, 0.9397f), new Vector3(-0.3827f, 0f, 0.9239f),
            new Vector3(0.9063f, -0.0001f, 0.4227f), new Vector3(0.1304f, 0f, 0.9915f), new Vector3(-0.7373f, 0f, 0.6755f),
            new Vector3(0.9397f, 0f, 0.3421f), new Vector3(0.4225f, -0.0001f, 0.9064f), new Vector3(-0.5001f, 0f, 0.866f),
            new Vector3(0.9962f, 0f, -0.0871f), new Vector3(0.866f, -0.0001f, 0.5f), new Vector3(0.4617f, 0f, 0.887f),
        },
        null),
    };

    // the grip of this weapon on its prefab component (false: no data for it)
    internal static bool Apply(Weapon weapon, string weaponName)
    {
        foreach (var g in Grips)
        {
            if (g.name != weaponName) continue;
            weapon.EditorSetGrip(g.grip, g.right, g.left);
            return true;
        }
        return false;
    }

    // every weapon prefab that exists gets its grip (the rest of each prefab is kept)
    [MenuItem("Last Terminal/19 - Helena 2: Weapon Grips (hands fitted to each weapon)")]
    internal static void ApplyAll()
    {
        var done = new List<string>();
        foreach (var g in Grips)
        {
            string path = $"{PrefabFolder}/Weapon_{g.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents.TryGetComponent(out Weapon w) && Apply(w, g.name))
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                done.Add(g.name);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }
        Debug.Log($"[Grips] {done.Count} weapon prefabs: {string.Join(", ", done)}" + (done.Count == 0 ? " (none found: run menu 3 first)" : ""));
    }
}
