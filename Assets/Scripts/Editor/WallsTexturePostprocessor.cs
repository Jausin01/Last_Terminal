using UnityEditor;
using UnityEngine;

public class WallsTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Art/Props/Walls/")) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Repeat;
    }
}
