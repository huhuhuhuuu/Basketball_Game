using UnityEngine;
using UnityEngine.Rendering;

namespace BasketballCourt
{
    /// <summary>
    /// Helpers for building Built-in-RP "Standard" shader materials from code.
    /// The Standard shader needs its keywords and blend states set explicitly when
    /// materials are created at runtime (the inspector normally does this for you).
    /// All helpers return the material so calls can be chained.
    /// </summary>
    public static class MatKit
    {
        static Shader _standard;
        public static Shader Standard
        {
            get
            {
                if (_standard == null) _standard = Shader.Find("Standard");
                return _standard;
            }
        }

        /// <summary>Opaque Standard material with a flat colour.</summary>
        public static Material Make(string name, Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            var m = new Material(Standard);
            m.name = name;
            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material WithAlbedo(this Material m, Texture2D tex, float tileX = 1f, float tileY = 1f)
        {
            m.mainTexture = tex;
            m.mainTextureScale = new Vector2(tileX, tileY);
            return m;
        }

        public static Material WithNormal(this Material m, Texture2D normal, float scale = 1f)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", scale);
            m.EnableKeyword("_NORMALMAP");
            return m;
        }

        /// <summary>
        /// Detail albedo (multiplied ×2, so mid-grey 0.5 is neutral). Tiling is independent of the
        /// main texture, which is what breaks up repetition on large surfaces such as the asphalt.
        /// </summary>
        public static Material WithDetail(this Material m, Texture2D detailAlbedo, float tileX, float tileY,
            Texture2D detailNormal = null, float detailNormalScale = 1f)
        {
            m.SetTexture("_DetailAlbedoMap", detailAlbedo);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(tileX, tileY));
            m.SetFloat("_UVSec", 0f);
            m.EnableKeyword("_DETAIL_MULX2");
            if (detailNormal != null)
            {
                m.SetTexture("_DetailNormalMap", detailNormal);
                m.SetFloat("_DetailNormalMapScale", detailNormalScale);
            }
            return m;
        }

        public static Material WithSmoothness(this Material m, float smoothness)
        {
            m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        public static Material WithMetallic(this Material m, float metallic)
        {
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material WithColor(this Material m, Color c)
        {
            m.color = c;
            return m;
        }

        public static Material WithEmission(this Material m, Color emission)
        {
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        /// <summary>Alpha-tested. Receives shadows correctly (unlike Fade), so use it for anything lying on the floor.</summary>
        public static Material Cutout(this Material m, float cutoff = 0.5f)
        {
            m.SetFloat("_Mode", 1f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_Cutoff", cutoff);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        /// <summary>Classic alpha blending (fade). Does not receive shadows in the Built-in forward path.</summary>
        public static Material Fade(this Material m)
        {
            m.SetFloat("_Mode", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Physically based transparency (glass, clear plastic). Keeps specular highlights at low alpha.</summary>
        public static Material Transparent(this Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Slightly bump the render queue so a decal draws after whatever it lies on.</summary>
        public static Material QueueOffset(this Material m, int offset)
        {
            m.renderQueue = m.renderQueue + offset;
            return m;
        }
    }
}
