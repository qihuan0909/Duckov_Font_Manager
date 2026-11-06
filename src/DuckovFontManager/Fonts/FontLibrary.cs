using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace DuckovFontManager
{
    /// <summary>
    /// Discovers font files and turns them into dynamic TMP font assets on demand.
    /// Also tracks the game's original font assets, which are used as fallbacks for missing glyphs.
    /// </summary>
    internal static class FontLibrary
    {
        private static readonly string[] Extensions = { ".ttf", ".otf", ".ttc" };

        private static Dictionary<string, FontEntry> _entries = new Dictionary<string, FontEntry>(StringComparer.OrdinalIgnoreCase);
        private static List<FontEntry> _ordered = new List<FontEntry>();

        private static readonly Dictionary<string, TMP_FontAsset> _assets = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> _ourAssetIds = new HashSet<int>();
        private static readonly List<Object> _pendingDestroy = new List<Object>();

        // Original game fonts used as fallback chain for our assets (main fonts first).
        private static readonly List<TMP_FontAsset> _fallbacks = new List<TMP_FontAsset>();

        public static IReadOnlyList<FontEntry> Entries => _ordered;
        public static int AssetCount => _assets.Count;

        /// <summary>Raised after a rescan changed the list.</summary>
        public static event Action ListChanged;
        /// <summary>Raised when a font asset that may be in use was rebuilt or removed.</summary>
        public static event Action AssetsInvalidated;

        public static bool IsOurs(TMP_FontAsset fa) => fa != null && _ourAssetIds.Contains(fa.GetInstanceID());

        public static FontEntry GetEntry(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            _entries.TryGetValue(key, out var e);
            return e;
        }

        // ------------------------------------------------------------------ discovery

        public static void Rescan()
        {
            var cfg = ConfigService.Current;
            var found = new Dictionary<string, FontEntry>(StringComparer.OrdinalIgnoreCase);
            ScanDirectory(Paths.UserFontDir, FontSource.User, "user/", found);
            ScanDirectory(Paths.ModFontDir, FontSource.Mod, "mod/", found);
            if (cfg.includeSystemFonts)
            {
                try
                {
                    foreach (var p in Font.GetPathsToOSFonts()) AddFile(p, FontSource.System, "sys/", found);
                }
                catch (Exception e) { Log.Error("Failed to enumerate system fonts", e); }
            }

            bool invalidated = false;
            // Carry over metadata; drop assets whose file changed or vanished.
            foreach (var kv in found)
            {
                if (_entries.TryGetValue(kv.Key, out var old))
                {
                    bool changed = old.FileSize != kv.Value.FileSize || old.LastWriteUtc != kv.Value.LastWriteUtc;
                    if (!changed)
                    {
                        kv.Value.MetadataLoaded = old.MetadataLoaded;
                        kv.Value.FamilyName = old.FamilyName;
                        kv.Value.StyleName = old.StyleName;
                        kv.Value.Failed = old.Failed;
                        kv.Value.Error = old.Error;
                    }
                    else if (_assets.ContainsKey(kv.Key))
                    {
                        Log.Info($"Font file changed, reloading: {kv.Value.FileName}");
                        Invalidate(kv.Key);
                        invalidated = true;
                    }
                }
            }
            foreach (var key in _entries.Keys.Where(k => !found.ContainsKey(k)).ToList())
            {
                if (_assets.ContainsKey(key))
                {
                    Log.Info($"Font file removed: {key}");
                    Invalidate(key);
                    invalidated = true;
                }
            }

            _entries = found;
            foreach (var e in _entries.Values)
                if (e.Source != FontSource.System) LoadMetadata(e);

            _ordered = _entries.Values
                .OrderBy(e => (int)e.Source)
                .ThenBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Log.Info($"Font scan: {_ordered.Count(e => e.Source == FontSource.User)} user, " +
                     $"{_ordered.Count(e => e.Source == FontSource.Mod)} bundled, " +
                     $"{_ordered.Count(e => e.Source == FontSource.System)} system fonts.");
            ListChanged?.Invoke();
            if (invalidated) AssetsInvalidated?.Invoke();
        }

        private static void ScanDirectory(string dir, FontSource source, string prefix, Dictionary<string, FontEntry> into)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                foreach (var f in Directory.GetFiles(dir)) AddFile(f, source, prefix, into);
            }
            catch (Exception e) { Log.Error("Failed to scan " + dir, e); }
        }

        private static void AddFile(string path, FontSource source, string prefix, Dictionary<string, FontEntry> into)
        {
            var ext = Path.GetExtension(path);
            if (!Extensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase))) return;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return;
                var key = prefix + fi.Name;
                if (into.ContainsKey(key)) return;
                into[key] = new FontEntry
                {
                    Key = key,
                    FilePath = fi.FullName,
                    FileName = fi.Name,
                    Source = source,
                    FileSize = fi.Length,
                    LastWriteUtc = fi.LastWriteTimeUtc,
                };
            }
            catch { /* unreadable file: skip */ }
        }

        /// <summary>Reads family/style names without building an atlas.</summary>
        public static void LoadMetadata(FontEntry e)
        {
            if (e == null || e.MetadataLoaded) return;
            e.MetadataLoaded = true;
            try
            {
                // Load from memory and unload again so the file is never kept open (users must be able
                // to replace or delete fonts while the game runs).
                FontEngine.InitializeFontEngine();
                var bytes = File.ReadAllBytes(e.FilePath);
                var err = FontEngine.LoadFontFace(bytes);
                if (err != FontEngineError.Success)
                {
                    e.Failed = true;
                    e.Error = err.ToString();
                    return;
                }
                var face = FontEngine.GetFaceInfo();
                e.FamilyName = face.familyName;
                e.StyleName = face.styleName;
                FontEngine.UnloadFontFace();
            }
            catch (Exception ex)
            {
                e.Failed = true;
                e.Error = ex.Message;
            }
        }

        // ------------------------------------------------------------------ assets

        /// <summary>Returns (creating if needed) the TMP font asset for a font key; null if unavailable.</summary>
        public static TMP_FontAsset GetAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_assets.TryGetValue(key, out var existing) && existing != null) return existing;
            if (!_entries.TryGetValue(key, out var e) || e.Failed) return null;

            var cfg = ConfigService.Current;
            Font font = null;
            try
            {
                // Unity keeps the font file open, so user/bundled fonts are loaded from a private copy.
                font = new Font(e.Source == FontSource.System ? e.FilePath : CacheCopy(e));
                font.name = Path.GetFileNameWithoutExtension(e.FileName);
                font.hideFlags = HideFlags.DontUnloadUnusedAsset;

                var fa = TMP_FontAsset.CreateFontAsset(font, cfg.samplingPointSize, cfg.atlasPadding,
                    GlyphRenderMode.SDFAA, cfg.atlasSize, cfg.atlasSize, AtlasPopulationMode.Dynamic, true);
                if (fa == null)
                {
                    e.Failed = true;
                    e.Error = "Unity 无法读取该字体文件";
                    Object.Destroy(font);
                    Log.Warn($"Could not load font face: {e.FilePath}");
                    return null;
                }

                LoadMetadata(e);
                fa.name = "DFM " + e.DisplayName;
                fa.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (fa.material != null)
                {
                    fa.material.name = fa.name + " Material";
                    fa.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                if (fa.atlasTextures != null)
                    foreach (var t in fa.atlasTextures)
                        if (t != null) t.hideFlags = HideFlags.DontUnloadUnusedAsset;
                fa.fallbackFontAssetTable = new List<TMP_FontAsset>(_fallbacks);

                _assets[key] = fa;
                _fonts[key] = font;
                _ourAssetIds.Add(fa.GetInstanceID());
                Log.Info($"Created font asset '{fa.name}' from {e.FilePath}");

                if (cfg.prewarm) Prewarmer.Enqueue(fa);
                return fa;
            }
            catch (Exception ex)
            {
                e.Failed = true;
                e.Error = ex.Message;
                Log.Error($"Failed to create font asset from {e.FilePath}", ex);
                if (font != null) Object.Destroy(font);
                return null;
            }
        }

        private static string CacheDir => Path.Combine(Paths.ConfigDir, "Cache");

        private static string CacheCopy(FontEntry e)
        {
            Directory.CreateDirectory(CacheDir);
            var safe = new string(Path.GetFileNameWithoutExtension(e.FileName)
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            var name = $"{e.Source}_{safe}_{e.FileSize}_{e.LastWriteUtc.Ticks}{Path.GetExtension(e.FileName)}";
            var path = Path.Combine(CacheDir, name);
            if (!File.Exists(path)) File.Copy(e.FilePath, path, true);
            return path;
        }

        /// <summary>Deletes cached font copies that are not locked (i.e. not used by this session).</summary>
        public static void CleanCache()
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return;
                foreach (var f in Directory.GetFiles(CacheDir))
                {
                    try { File.Delete(f); } catch { /* in use */ }
                }
            }
            catch { /* ignore */ }
        }

        /// <summary>Drops the asset for a key. Objects are destroyed later (after texts moved off them).</summary>
        public static void Invalidate(string key)
        {
            if (_assets.TryGetValue(key, out var fa))
            {
                _assets.Remove(key);
                if (fa != null)
                {
                    MaterialMapper.ForgetFont(fa);
                    if (fa.atlasTextures != null) _pendingDestroy.AddRange(fa.atlasTextures.Where(t => t != null));
                    if (fa.material != null) _pendingDestroy.Add(fa.material);
                    _pendingDestroy.Add(fa);
                }
            }
            if (_fonts.TryGetValue(key, out var font))
            {
                _fonts.Remove(key);
                if (font != null) _pendingDestroy.Add(font);
            }
            if (_entries.TryGetValue(key, out var e))
            {
                e.Failed = false;
                e.Error = null;
                e.MetadataLoaded = false;
            }
        }

        /// <summary>Destroys invalidated objects. Call once no registered text references them.</summary>
        public static void FlushPendingDestroy()
        {
            if (_pendingDestroy.Count == 0) return;
            foreach (var o in _pendingDestroy)
                if (o != null) Object.Destroy(o);
            _pendingDestroy.Clear();
        }

        public static void DestroyAll()
        {
            foreach (var key in _assets.Keys.ToList()) Invalidate(key);
            FlushPendingDestroy();
            _ourAssetIds.Clear();
            _fallbacks.Clear();
        }

        // ------------------------------------------------------------------ game fonts

        /// <summary>Registers an original game font as fallback for our assets (glyphs missing from the custom font).</summary>
        public static void RegisterGameFont(TMP_FontAsset original, FontCategory category)
        {
            if (original == null || IsOurs(original) || _fallbacks.Contains(original)) return;
            if (category != FontCategory.Main && category != FontCategory.Title) return;

            if (category == FontCategory.Main) _fallbacks.Insert(0, original);
            else _fallbacks.Add(original);

            foreach (var fa in _assets.Values)
            {
                if (fa == null) continue;
                var table = fa.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
                if (!table.Contains(original))
                {
                    if (category == FontCategory.Main) table.Insert(0, original);
                    else table.Add(original);
                }
                fa.fallbackFontAssetTable = table;
            }
        }

        /// <summary>The game's main UI font (used for the settings panel so it stays readable).</summary>
        public static TMP_FontAsset FindGameMainFont()
        {
            var main = _fallbacks.FirstOrDefault(f => f != null && RuleEngine.Categorize(f) == FontCategory.Main
                                                      && f.name.IndexOf("ResourceHanRounded", StringComparison.OrdinalIgnoreCase) >= 0);
            if (main != null) return main;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                main = all.FirstOrDefault(f => !IsOurs(f) && f.name.IndexOf("ResourceHanRounded", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? all.FirstOrDefault(f => !IsOurs(f) && RuleEngine.Categorize(f) == FontCategory.Main);
            }
            catch { /* ignore */ }
            return main ?? TMP_Settings.defaultFontAsset;
        }
    }
}
