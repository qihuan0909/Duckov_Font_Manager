using System;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>Writes the computed font/material/size onto a text, or restores its originals.</summary>
    internal static class FontApplier
    {
        /// <summary>True while we are writing properties ourselves (patch hooks ignore those writes).</summary>
        public static bool Applying;

        public static float ComputeFactor(TextState s)
        {
            if (RuleEngine.IsExcluded(s)) return 1f;
            var cat = RuleEngine.Categorize(s.OrigFont);
            return RuleEngine.SizeFactor(s, cat, RuleEngine.ResolveTarget(s, cat));
        }

        public static void Apply(TextState s, bool force)
        {
            var t = s.Text;
            if (t == null) return;
            if (!force && s.AppliedVersion == FontManagerCore.Version && (s.AppliedFont == null || t.font == s.AppliedFont))
                return;

            bool prev = Applying;
            Applying = true;
            try
            {
                var cfg = ConfigService.Current;
                bool excluded = RuleEngine.IsExcluded(s);
                var cat = RuleEngine.Categorize(s.OrigFont);
                var target = excluded ? null : RuleEngine.ResolveTarget(s, cat);

                if (target != null)
                {
                    var source = s.OrigMat != null ? s.OrigMat : (s.OrigFont != null ? s.OrigFont.material : null);
                    var mat = MaterialMapper.Get(source, s.OrigFont, target);
                    if (t.font != target) t.font = target;
                    if (mat != null && t.fontSharedMaterial != mat) t.fontSharedMaterial = mat;
                    s.AppliedFont = target;
                    s.AppliedMat = mat;
                }
                else
                {
                    if (s.OrigFont != null && t.font != s.OrigFont) t.font = s.OrigFont;
                    if (s.OrigMat != null && t.fontSharedMaterial != s.OrigMat) t.fontSharedMaterial = s.OrigMat;
                    s.AppliedFont = null;
                    s.AppliedMat = null;
                }

                float factor = excluded ? 1f : RuleEngine.SizeFactor(s, cat, target);
                s.Factor = factor;
                if (!t.enableAutoSizing) SetSize(t, s.OrigSize * factor);
                if (!Approximately(t.fontSizeMin, s.OrigMin * factor)) t.fontSizeMin = s.OrigMin * factor;
                if (!Approximately(t.fontSizeMax, s.OrigMax * factor)) t.fontSizeMax = s.OrigMax * factor;

                float lineDelta = target != null ? cfg.lineSpacingDelta : 0f;
                s.LineSpacingDelta = lineDelta;
                if (!Approximately(t.lineSpacing, s.OrigLineSpacing + lineDelta)) t.lineSpacing = s.OrigLineSpacing + lineDelta;

                float shift = target != null ? cfg.verticalOffset * s.OrigSize * factor : 0f;
                s.MarginShift = shift;
                var margin = s.OrigMargin;
                margin.y -= shift; // positive offset moves text up
                margin.w += shift;
                if (t.margin != margin) t.margin = margin;

                s.AppliedVersion = FontManagerCore.Version;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("apply", $"Failed to apply font to '{(t != null ? t.name : "?")}'", e);
            }
            finally
            {
                Applying = prev;
            }
        }

        public static void Restore(TextState s)
        {
            var t = s.Text;
            if (t == null) return;
            bool prev = Applying;
            Applying = true;
            try
            {
                if (s.OrigFont != null && t.font != s.OrigFont) t.font = s.OrigFont;
                if (s.OrigMat != null && t.fontSharedMaterial != s.OrigMat) t.fontSharedMaterial = s.OrigMat;
                if (!t.enableAutoSizing) SetSize(t, s.OrigSize);
                t.fontSizeMin = s.OrigMin;
                t.fontSizeMax = s.OrigMax;
                t.lineSpacing = s.OrigLineSpacing;
                if (t.margin != s.OrigMargin) t.margin = s.OrigMargin;
                s.AppliedFont = null;
                s.AppliedMat = null;
                s.AppliedVersion = -1;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("restore", "Failed to restore text", e);
            }
            finally
            {
                Applying = prev;
            }
        }

        private static void SetSize(TMP_Text t, float size)
        {
            if (size > 0f && !Approximately(t.fontSize, size)) t.fontSize = size;
        }

        private static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.001f;
    }
}
