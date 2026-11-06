using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>All TMP texts the mod has seen, with their original values.</summary>
    internal static class TextRegistry
    {
        private static readonly Dictionary<int, TextState> _map = new Dictionary<int, TextState>();
        private static readonly List<int> _dead = new List<int>();

        public static int Count => _map.Count;

        public static TextState TryGet(TMP_Text t)
        {
            if (t == null) return null;
            if (_map.TryGetValue(t.GetInstanceID(), out var s) && s.Text == t) return s;
            return null;
        }

        public static TextState GetOrCreate(TMP_Text t)
        {
            var s = TryGet(t);
            if (s != null) return s;
            s = new TextState { Text = t, IsWorld = !(t is TextMeshProUGUI) };
            Capture(s);
            _map[t.GetInstanceID()] = s;
            return s;
        }

        private static void Capture(TextState s)
        {
            var t = s.Text;
            int id = t.GetInstanceID();
            DfmTextMarker marker = null;
            try { marker = t.GetComponent<DfmTextMarker>(); } catch { /* ignore */ }

            if (marker != null && marker.valid && marker.ownerId != id && (marker.origFont != null || marker.origSize > 0f))
            {
                // Clone of a text we had already modified: the marker carries the real originals.
                s.OrigFont = marker.origFont;
                s.OrigMat = marker.origMat;
                s.OrigSize = marker.origSize;
                s.OrigMin = marker.origMin;
                s.OrigMax = marker.origMax;
                s.OrigLineSpacing = marker.origLineSpacing;
                s.OrigMargin = marker.origMargin;
            }
            else
            {
                s.OrigFont = t.font;
                s.OrigMat = t.fontSharedMaterial;
                s.OrigSize = t.fontSize;
                s.OrigMin = t.fontSizeMin;
                s.OrigMax = t.fontSizeMax;
                s.OrigLineSpacing = t.lineSpacing;
                s.OrigMargin = t.margin;

                // Copy of a modified text without usable marker data: reconstruct as well as we can.
                if (FontLibrary.IsOurs(s.OrigFont) || MaterialMapper.IsOurs(s.OrigMat))
                {
                    if (MaterialMapper.TryGetOrigin(s.OrigMat, out var srcMat, out var srcFont))
                    {
                        s.OrigMat = srcMat;
                        if (srcFont != null) s.OrigFont = srcFont;
                    }
                    if (FontLibrary.IsOurs(s.OrigFont))
                    {
                        s.OrigFont = FontLibrary.FindGameMainFont();
                        s.OrigMat = s.OrigFont != null ? s.OrigFont.material : null;
                    }
                    float f = FontApplier.ComputeFactor(s);
                    if (f > 0f && Math.Abs(f - 1f) > 0.0001f)
                    {
                        s.OrigSize /= f;
                        s.OrigMin /= f;
                        s.OrigMax /= f;
                    }
                    s.OrigLineSpacing -= ConfigService.Current.lineSpacingDelta;
                    Log.Verbose($"Reconstructed originals for copied text '{t.name}'");
                }
            }

            if (marker == null)
            {
                try { marker = t.gameObject.AddComponent<DfmTextMarker>(); }
                catch (Exception e) { Log.ErrorOnce("marker", "Could not add DfmTextMarker", e); }
            }
            s.Marker = marker;
            s.SaveToMarker();

            var cat = RuleEngine.Categorize(s.OrigFont);
            FontLibrary.RegisterGameFont(s.OrigFont, cat);
        }

        /// <summary>Removes entries whose component was destroyed.</summary>
        public static void Prune()
        {
            _dead.Clear();
            foreach (var kv in _map)
                if (kv.Value.Text == null) _dead.Add(kv.Key);
            foreach (var id in _dead) _map.Remove(id);
        }

        public static List<TextState> Snapshot()
        {
            Prune();
            return new List<TextState>(_map.Values);
        }

        /// <summary>Puts every text back to its original state and forgets everything.</summary>
        public static void RestoreAll()
        {
            foreach (var s in _map.Values)
            {
                if (s.Text == null) continue;
                FontApplier.Restore(s);
                if (s.Marker != null) UnityEngine.Object.Destroy(s.Marker);
            }
            _map.Clear();
        }

        public static void Clear() => _map.Clear();
    }
}
