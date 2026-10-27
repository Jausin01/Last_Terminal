using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Asset helpers for the editor builders: folders, the build scene list, the pipeline's lit shader and materials.
internal static class EditorAssets
{
    // Where the plain colored materials go.
    private const string TestMaterialsFolder = "Assets/Data/TestMaterials";

    // The active render pipeline's default lit shader (URP Lit); null (and an error) without a pipeline asset.
    internal static Shader DefaultLitShader()
    {
        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null || rp.defaultShader == null)
        {
            Debug.LogError("[Helena] No render pipeline asset active. Use a Universal 3D (URP) project.");
            return null;
        }
        return rp.defaultShader;
    }

    // A material from a texture (alpha clip optional).
    internal static Material MakeMaterial(string path, Texture2D tex, float smooth, bool clip, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;

        m.SetTexture(ShaderIds.BaseMapId, tex);
        m.SetColor(ShaderIds.BaseColorId, tex != null ? Color.white : new Color(0.03f, 0.03f, 0.035f));
        m.SetFloat(ShaderIds.MetallicId, 0f);
        m.SetFloat(ShaderIds.SmoothnessId, smooth);
        m.DisableKeyword("_EMISSION");

        if (clip)
        {
            m.SetFloat(ShaderIds.AlphaClipId, 1f);
            m.SetFloat(ShaderIds.CutoffId, 0.5f);
            m.SetFloat(ShaderIds.CullId, (float)CullMode.Off);   // hair cards visible from both sides
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // A plain colored material (made once).
    internal static Material GetOrCreateColorMaterial(string name, Color color)
    {
        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null || rp.defaultShader == null) return null;
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "TestMaterials");
        string path = TestMaterialsFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(rp.defaultShader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor(ShaderIds.BaseColorId, color);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Adds a scene to the build settings.
    internal static void AddToBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Creates a folder when missing.
    internal static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
