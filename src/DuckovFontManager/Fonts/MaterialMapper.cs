using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>
    /// Builds materials for a replacement font that keep the look of the original material
    /// (outline, underlay/shadow, glow, dilate, shader variant) while sampling the new atlas.
    /// </summary>
    internal static class MaterialMapper
    {
        private sealed class Info
        {
            public Material Material;
            public Material Source;
            public TMP_FontAsset OriginalFont;
            public TMP_FontAsset Target;
        }

        private static readonly Dictionary<long, Info> _byKey = new Dictionary<long, Info>();
        private static readonly Dictionary<int, Info> _byMaterial = new Dictionary<int, Info>();

        public static bool IsOurs(Material m) => m != null && _byMaterial.ContainsKey(m.GetInstanceID());

        /// <summary>For a material we created, returns the material/font it was derived from.</summary>
        public static bool TryGetOrigin(Material ours, out Material source, out TMP_FontAsset originalFont)
        {
            source = null;
            originalFont = null;
            if (ours == null || !_byMaterial.TryGetValue(ours.GetInstanceID(), out var info)) return false;
            source = info.Source;
            originalFont = info.OriginalFont;
            return source != null || originalFont != null;
        }

        public static Material Get(Material source, TMP_FontAsset originalFont, TMP_FontAsset target)
        {
            if (target == null) return null;
            if (source == null) return target.material;

            long key = ((long)source.GetInstanceID() << 32) ^ (uint)target.GetInstanceID();
            if (_byKey.TryGetValue(key, out var info) && info.Material != null) return info.Material;

            ShaderUtilities.GetShaderPropertyIDs();
            var m = new Material(source)
            {
                name = source.name + " (DFM)",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            m.SetTexture(ShaderUtilities.ID_MainTex, target.atlasTexture);
            if (m.HasProperty(ShaderUtilities.ID_TextureWidth)) m.SetFloat(ShaderUtilities.ID_TextureWidth, target.atlasWidth);
            if (m.HasProperty(ShaderUtilities.ID_TextureHeight)) m.SetFloat(ShaderUtilities.ID_TextureHeight, target.atlasHeight);
            if (m.HasProperty(ShaderUtilities.ID_GradientScale)) m.SetFloat(ShaderUtilities.ID_GradientScale, target.atlasPadding + 1);
            if (m.HasProperty(ShaderUtilities.ID_WeightNormal)) m.SetFloat(ShaderUtilities.ID_WeightNormal, target.normalStyle);
            if (m.HasProperty(ShaderUtilities.ID_WeightBold)) m.SetFloat(ShaderUtilities.ID_WeightBold, target.boldStyle);

            info = new Info { Material = m, Source = source, OriginalFont = originalFont, Target = target };
            _byKey[key] = info;
            _byMaterial[m.GetInstanceID()] = info;
            return m;
        }

        /// <summary>Forget (and schedule destruction of) every material built for a font asset.</summary>
        public static void ForgetFont(TMP_FontAsset target)
        {
            foreach (var kv in _byKey.Where(kv => kv.Value.Target == target).ToList())
            {
                _byKey.Remove(kv.Key);
                if (kv.Value.Material != null)
                {
                    _byMaterial.Remove(kv.Value.Material.GetInstanceID());
                    Object.Destroy(kv.Value.Material);
                }
            }
        }

        public static void DestroyAll()
        {
            foreach (var info in _byKey.Values)
                if (info.Material != null) Object.Destroy(info.Material);
            _byKey.Clear();
            _byMaterial.Clear();
        }
    }
}
