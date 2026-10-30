using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// The story cast's materials (Assets/Characters/Cast/): one Lit material M_<name> per model in Models/ with its
// texture T_<name>.png, put in place of whatever material the file names (remapped, kept on reimport).
// Used by Last Terminal/14 for the cutscene's cast; Last Terminal/23 does it for every model in the folder (new cast:
// Mia, Mia_School, Jeremy...).
internal static class CastMaterials
{
    // Cast folders and the material's smoothness (cloth / skin, PS2 look).
    private const string Folder = "Assets/Characters/Cast";
    private const string MaterialFolder = Folder + "/Materials";
    private const string ModelFolder = Folder + "/Models";
    private const float Smoothness = 0.12f;
    private const int TextureSize = 4096;

    // Menu 23: every model of the cast gets its material.
    [MenuItem("Last Terminal/23 - Cast: Materials (every model in Characters/Cast/Models)")]
    private static void ApplyAll()
    {
        Shader lit = EditorAssets.DefaultLitShader();
        var done = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (Apply(name, lit)) done.Add(name);
        }
        Debug.Log($"[Cast] Materials set: {string.Join(", ", done)}.");
    }

    // One model's material (false: no such model). A missing texture leaves the material plain (warned).
    internal static bool Apply(string name, Shader lit)
    {
        string path = $"{ModelFolder}/{name}.fbx";
        if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return false;
        EditorAssets.EnsureFolder(Folder, "Materials");
        // the textures are 4096 (the face gets most of its detail from it): imported at full size
        if (AssetImporter.GetAtPath($"{Folder}/T_{name}.png") is TextureImporter ti && ti.maxTextureSize < TextureSize)
        {
            ti.maxTextureSize = TextureSize;
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/T_{name}.png");
        if (tex == null) Debug.LogWarning($"[Cast] {Folder}/T_{name}.png not found: {name} gets a plain material.");
        Material m = EditorAssets.MakeMaterial($"{MaterialFolder}/M_{name}.mat", tex, Smoothness, false, lit);
        // whatever the file calls its material: ours
        var names = new HashSet<string>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material) names.Add(o.name);
        foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
        foreach (string n in names) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
        mi.SaveAndReimport();
        return true;
    }
}
