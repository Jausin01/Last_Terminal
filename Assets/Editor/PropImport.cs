using UnityEditor;

// Static props under Assets/Art/Props/: no rig, no animation, no cameras/lights from the FBX.
public class PropImport : AssetPostprocessor
{
    public const string Folder = "Assets/Art/Props/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.isReadable = false;
    }
}
