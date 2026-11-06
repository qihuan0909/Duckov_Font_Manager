using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    internal enum FontCategory
    {
        /// <summary>The game's regular UI font(s).</summary>
        Main,
        /// <summary>Heavy/bold title font.</summary>
        Title,
        /// <summary>Stylised art fonts (pixel, 7-segment, monospace). Only replaced when forced.</summary>
        Art,
        /// <summary>Fonts we don't know, e.g. shipped by other mods.</summary>
        Unknown,
    }

    /// <summary>Decides what font and size each text should get.</summary>
    internal static class RuleEngine
    {
        private static readonly Dictionary<int, FontCategory> _categoryCache = new Dictionary<int, FontCategory>();
        private static readonly List<Regex> _pathRegexes = new List<Regex>();
        private static int _rulesVersion = -1;

        private static void EnsureRules()
        {
            if (_rulesVersion == FontManagerCore.Version) return;
            _rulesVersion = FontManagerCore.Version;
            _categoryCache.Clear();
            _pathRegexes.Clear();
            foreach (var p in ConfigService.Current.excludedPaths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                // Timeout: a pathological pattern in the config must not freeze the game.
                try { _pathRegexes.Add(new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20))); }
                catch (Exception e) { Log.Warn($"Invalid excludedPaths regex '{p}': {e.Message}"); }
            }
        }

        public static FontCategory Categorize(TMP_FontAsset font)
        {
            if (font == null) return FontCategory.Unknown;
            EnsureRules();
            int id = font.GetInstanceID();
            if (_categoryCache.TryGetValue(id, out var c)) return c;

            var cfg = ConfigService.Current;
            var name = font.name ?? "";
            if (MatchesAny(name, cfg.titleFontPatterns)) c = FontCategory.Title;
            else if (MatchesAny(name, cfg.artFontPatterns)) c = FontCategory.Art;
            else if (MatchesAny(name, cfg.gameFontPatterns)) c = FontCategory.Main;
            else c = FontCategory.Unknown;
            _categoryCache[id] = c;
            return c;
        }

        public static bool IsExcluded(TextState s)
        {
            if (s.ExclusionVersion == FontManagerCore.Version) return s.Excluded;
            EnsureRules();
            s.ExclusionVersion = FontManagerCore.Version;
            s.Excluded = ComputeExcluded(s);
            return s.Excluded;
        }

        private static bool ComputeExcluded(TextState s)
        {
            var t = s.Text;
            if (t == null) return true;
            if (t.GetComponent<DfmForceInclude>() != null) return false;
            if (t.GetComponentInParent<DfmExclude>(true) != null) return true;

            var cfg = ConfigService.Current;
            if (s.OrigFont != null && MatchesAny(s.OrigFont.name, cfg.excludedFonts)) return true;
            if (_pathRegexes.Count > 0)
            {
                var path = HierarchyPath(t.transform);
                foreach (var r in _pathRegexes)
                {
                    try { if (r.IsMatch(path)) return true; }
                    catch (RegexMatchTimeoutException) { Log.ErrorOnce("regex:" + r, $"excludedPaths pattern '{r}' timed out", null); }
                }
            }
            return false;
        }

        /// <summary>The font key in effect right now (language binding wins over the global choice).</summary>
        public static string EffectivePrimaryKey()
        {
            var cfg = ConfigService.Current;
            try
            {
                var lang = LocalizationManager.CurrentLanguage.ToString();
                if (cfg.languageFonts.TryGetValue(lang, out var key) && key != null) return key;
            }
            catch { /* localization not ready */ }
            return cfg.primaryFont;
        }

        /// <summary>Replacement font for a text, or null to keep the original.</summary>
        public static TMP_FontAsset ResolveTarget(TextState s, FontCategory cat)
        {
            var cfg = ConfigService.Current;
            if (!cfg.enabled || s.OrigFont == null) return null;

            bool replace;
            switch (cat)
            {
                case FontCategory.Main: replace = true; break;
                case FontCategory.Title: replace = cfg.replaceTitleFont; break;
                case FontCategory.Art: replace = cfg.forceOverrideArtFonts; break;
                default: replace = cfg.replaceUnknownFonts; break;
            }
            if (!replace) return null;

            string key = EffectivePrimaryKey();
            if (cat == FontCategory.Title && !string.IsNullOrEmpty(cfg.titleFont)) key = cfg.titleFont;
            if (string.IsNullOrEmpty(key)) return null;
            return FontLibrary.GetAsset(key);
        }

        public static float SizeFactor(TextState s, FontCategory cat, TMP_FontAsset target)
        {
            var cfg = ConfigService.Current;
            if (!cfg.enabled) return 1f;
            float f = cfg.globalScale;
            if (s.IsWorld) f *= cfg.worldScale;
            else if (cat == FontCategory.Title) f *= cfg.titleScale;
            else f *= cfg.uiScale;
            if (target != null && cfg.normalizeMetrics) f *= MetricsNormalizer.Ratio(s.OrigFont, target);
            return f;
        }

        public static bool MatchesAny(string name, List<string> patterns)
        {
            if (string.IsNullOrEmpty(name) || patterns == null) return false;
            foreach (var p in patterns)
                if (!string.IsNullOrEmpty(p) && name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static string HierarchyPath(Transform t)
        {
            var sb = new StringBuilder();
            var stack = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) stack.Add(cur.name);
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                sb.Append(stack[i]);
                if (i > 0) sb.Append('/');
            }
            return sb.ToString();
        }
    }
}
