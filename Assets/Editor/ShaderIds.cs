using UnityEngine;

// URP Lit shader property ids used by the editor builders (resolved once).
internal static class ShaderIds
{
    // shader property ids
    internal static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    internal static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    internal static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    internal static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    internal static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    internal static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    internal static readonly int CullId = Shader.PropertyToID("_Cull");
    internal static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    internal static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    internal static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
}
