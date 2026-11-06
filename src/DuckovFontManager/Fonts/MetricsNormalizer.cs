using System.Collections.Generic;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>
    /// Computes a size factor so a replacement font looks as large as the original one.
    /// Uses the rendered height of a representative glyph (CJK ideograph for CJK languages, "H" otherwise),
    /// relative to each font's sampling point size.
    /// </summary>
    internal static class MetricsNormalizer
    {
        private static readonly Dictionary<long, float> _cache = new Dictionary<long, float>();

        public static void Clear() => _cache.Clear();

        public static float Ratio(TMP_FontAsset original, TMP_FontAsset target)
        {
            if (original == null || target == null || original == target) return 1f;
            long key = ((long)original.GetInstanceID() << 32) ^ (uint)target.GetInstanceID();
            if (_cache.TryGetValue(key, out var r)) return r;

            r = 1f;
            uint sample = SampleChar();
            float a = GlyphHeight(original, sample, allowAdd: false);
            float b = GlyphHeight(target, sample, allowAdd: true);
            if ((a <= 0f || b <= 0f) && sample != 'H')
            {
                a = GlyphHeight(original, 'H', allowAdd: false);
                b = GlyphHeight(target, 'H', allowAdd: true);
            }
            if (a > 0f && b > 0f) r = Mathf.Clamp(a / b, 0.6f, 1.6f);
            Log.Verbose($"Metrics ratio {original.name} -> {target.name}: {r:0.###}");
            _cache[key] = r;
            return r;
        }

        private static uint SampleChar()
        {
            try
            {
                switch (LocalizationManager.CurrentLanguage)
                {
                    case SystemLanguage.ChineseSimplified:
                    case SystemLanguage.ChineseTraditional:
                    case SystemLanguage.Chinese:
                    case SystemLanguage.Japanese:
                        return '国';
                    case SystemLanguage.Korean:
                        return '한';
                }
            }
            catch { /* localization not ready */ }
            return 'H';
        }

        /// <summary>Glyph height in em units, or 0 if unavailable.</summary>
        private static float GlyphHeight(TMP_FontAsset fa, uint unicode, bool allowAdd)
        {
            try
            {
                var table = fa.characterLookupTable;
                if (table == null) return 0f;
                if (!table.TryGetValue(unicode, out var ch))
                {
                    if (!allowAdd || fa.atlasPopulationMode != AtlasPopulationMode.Dynamic) return 0f;
                    var missing = FontLibrary.GetMissingSet(fa);
                    if (missing != null && missing.Contains(unicode)) return 0f; // empty glyph

                    if (!fa.TryAddCharacters(new[] { unicode })) return 0f;
                    if (!fa.characterLookupTable.TryGetValue(unicode, out ch)) return 0f;
                }
                var face = fa.faceInfo;
                if (ch?.glyph == null || face.pointSize <= 0) return 0f;
                float scale = face.scale <= 0f ? 1f : face.scale;
                return ch.glyph.metrics.height * scale / face.pointSize;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
