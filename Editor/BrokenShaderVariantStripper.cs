using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Drops third-party shader variants that cannot compile, so a build logs no shader errors. Only variants that
    /// already fail are removed; at runtime Unity falls back to the nearest variant that exists.
    /// </summary>
    public class BrokenShaderVariantStripper : IPreprocessShaders
    {
        #region Rules
        // URP defines its own GBufferData, so every Cast Direct Light variant is a redefinition error.
        private const string VolumetricLightShader = "VolumetricLights/VolumetricLightURP";
        private static readonly string[] VolumetricLightBrokenKeywords = { "VL_CAST_DIRECT_LIGHT_ADDITIVE", "VL_CAST_DIRECT_LIGHT_BLEND" };

        // The grass code reads unity_ObjectToWorld, which DOTS instancing does not declare.
        private const string GrassShader = "BruteForceURP/TJInteractiveGrassLiteURP";
        private const string DotsInstancingKeyword = "DOTS_INSTANCING_ON";

        // Light cookies plus per-object additional lights go past the 16 sampler slots of the non-DOTS fragment target.
        private const string FoliageShader = "Synty/Foliage";
        private const string LightCookiesKeyword = "_LIGHT_COOKIES";
        private const string AdditionalLightsKeyword = "_ADDITIONAL_LIGHTS";
        #endregion

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            switch (shader.name)
            {
                case VolumetricLightShader:
                    StripWhere(shader, data, d => HasAnyKeyword(d, VolumetricLightBrokenKeywords));
                    break;
                case GrassShader:
                    StripWhere(shader, data, d => HasAnyKeyword(d, DotsInstancingKeyword));
                    break;
                case FoliageShader:
                    if (snippet.shaderType == ShaderType.Fragment)
                        StripWhere(shader, data, d => HasAnyKeyword(d, LightCookiesKeyword) && HasAnyKeyword(d, AdditionalLightsKeyword) && !HasAnyKeyword(d, DotsInstancingKeyword));
                    break;
                default:
                    return;
            }
            // The build prints errors stored on the shader by earlier Editor compiles, even for stripped variants.
            ShaderUtil.ClearShaderMessages(shader);
        }

        #region Helpers
        private static void StripWhere(Shader shader, IList<ShaderCompilerData> data, System.Func<ShaderCompilerData, bool> isBroken)
        {
            int stripped = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                if (!isBroken(data[i]))
                    continue;
                data.RemoveAt(i);
                stripped++;
            }
            if (stripped > 0)
                Debug.Log($"[BrokenShaderVariantStripper] Stripped {stripped} variants of {shader.name} that do not compile.");
        }

        private static bool HasAnyKeyword(ShaderCompilerData d, params string[] keywords)
        {
            foreach (UnityEngine.Rendering.ShaderKeyword keyword in d.shaderKeywordSet.GetShaderKeywords())
            {
                if (System.Array.IndexOf(keywords, keyword.name) >= 0)
                    return true;
            }
            return false;
        }
        #endregion
    }
}
