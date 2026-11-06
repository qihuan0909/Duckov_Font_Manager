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

            // A marker owned by another instance means this text was copied (Instantiate) from one we
            // had already modified, so its current values are not the originals.
            bool copiedFromTracked = marker != null && marker.ownerId != id;

            if (copiedFromTracked && marker.valid && (marker.origFont != null || marker.origSize > 0f))
            {
                // Unity copied the marker's serialized fields: they carry the real originals.
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

                // Copy of a modified text without usable marker data: undo our changes as well as we can.
                // (The template is kept at the current settings, so the current factor/deltas apply.)
                if (copiedFromTracked || FontLibrary.IsOurs(s.OrigFont) || MaterialMapper.IsOurs(s.OrigMat))
                    Reconstruct(s);
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

        private static void Reconstruct(TextState s)
        {
            if (MaterialMapper.TryGetOrigin(s.OrigMat, out var srcMat, out var srcFont))
            {
                s.OrigMat = srcMat;
                if (srcFont != null) s.OrigFont = srcFont;
            }
            if (FontLibrary.IsOurs(s.OrigFont) || s.OrigFont == null)
            {
                s.OrigFont = FontLibrary.FindGameMainFont();
                s.OrigMat = s.OrigFont != null ? s.OrigFont.material : null;
            }

            var cfg = ConfigService.Current;
            bool replaced = !RuleEngine.IsExcluded(s) && RuleEngine.ResolveTarget(s, RuleEngine.Categorize(s.OrigFont)) != null;
            float f = FontApplier.ComputeFactor(s);
            if (f > 0f && Math.Abs(f - 1f) > 0.0001f)
            {
                s.OrigSize /= f;
                s.OrigMin /= f;
                s.OrigMax /= f;
            }
            if (replaced)
            {
                s.OrigLineSpacing -= cfg.lineSpacingDelta;
                float shift = cfg.verticalOffset * s.OrigSize * f;
                s.OrigMargin.y += shift;
                s.OrigMargin.w -= shift;
            }
            s.ExclusionVersion = -1;
            Log.Verbose($"Reconstructed originals for copied text '{s.Text.name}'");
        }

        /// <summary>
        /// Unregistered scene texts (e.g. inactive copies made from modified templates) can still point at
        /// our fonts/materials. Register them so they are handled like every other text before those
        /// objects get destroyed. Prefab assets are never touched.
        /// </summary>
        public static void AdoptStragglers(bool apply)
        {
            try
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (t == null || !t.gameObject.scene.IsValid() || TryGet(t) != null) continue;
                    if (!FontLibrary.IsOurs(t.font) && !MaterialMapper.IsOurs(t.fontSharedMaterial)) continue;
                    var s = GetOrCreate(t);
                    if (apply) FontApplier.Apply(s, true);
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("stragglers", "Could not scan for unregistered texts", e);
            }
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
            AdoptStragglers(false);
            foreach (var s in _map.Values)
            {
                if (s.Text == null) continue;
                FontApplier.Restore(s);
            }
            _map.Clear();

            // Remove every marker we added (also on copies that were never enabled).
            try
            {
                foreach (var m in Resources.FindObjectsOfTypeAll<DfmTextMarker>())
                    if (m != null && m.gameObject.scene.IsValid()) UnityEngine.Object.Destroy(m);
            }
            catch (Exception e) { Log.ErrorOnce("markers", "Could not remove markers", e); }
        }

        /// <summary>Materials currently applied to live texts (so they are not destroyed while in use).</summary>
        public static HashSet<int> AppliedMaterialIds()
        {
            var set = new HashSet<int>();
            foreach (var s in _map.Values)
                if (s.Text != null && s.AppliedMat != null) set.Add(s.AppliedMat.GetInstanceID());
            return set;
        }

        public static void Clear() => _map.Clear();
    }
}
