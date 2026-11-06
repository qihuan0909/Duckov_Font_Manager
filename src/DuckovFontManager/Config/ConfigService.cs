using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>Loads, sanitises and saves the config; raises <see cref="Changed"/> on every edit.</summary>
    internal static class ConfigService
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            // Replace (not append to) the default-initialised lists/dictionaries.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented,
        };

        private static float _saveAt = -1f;
        private static float _notifyAt = -1f;

        public static FontManagerConfig Current { get; private set; } = new FontManagerConfig();

        /// <summary>Raised (on the main thread) after any config change.</summary>
        public static event Action Changed;

        public static void Load()
        {
            var path = Paths.ConfigFile;
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<FontManagerConfig>(File.ReadAllText(path), JsonSettings);
                    Current = loaded ?? new FontManagerConfig();
                }
                else
                {
                    Current = new FontManagerConfig();
                }
            }
            catch (Exception e)
            {
                Log.Error("Config file is corrupt, using defaults. A copy was kept as config.json.bad", e);
                try { File.Copy(path, path + ".bad", true); } catch { /* ignore */ }
                Current = new FontManagerConfig();
            }
            Migrate(Current);
            Sanitize(Current);
            Log.DebugEnabled = Current.debugLog;
            SaveNow(); // writes newly added fields back
        }

        private static void Migrate(FontManagerConfig c)
        {
            if (c.schemaVersion < 2)
            {
                // v1 defaulted to F7, which UnityExplorer also uses.
                if (c.togglePanelKey == "F7") c.togglePanelKey = "F8";
            }
            c.schemaVersion = FontManagerConfig.CurrentSchema;
        }

        /// <summary>Call after mutating <see cref="Current"/>. Applies immediately and saves shortly after.</summary>
        public static void NotifyChanged()
        {
            _notifyAt = -1f;
            Sanitize(Current);
            Log.DebugEnabled = Current.debugLog;
            _saveAt = Time.unscaledTime + 0.5f;
            try { Changed?.Invoke(); }
            catch (Exception e) { Log.Error("Config change handler failed", e); }
        }

        /// <summary>Throttled variant for sliders being dragged.</summary>
        public static void NotifyChangedDeferred(float delay = 0.12f)
        {
            if (_notifyAt < 0f) _notifyAt = Time.unscaledTime + delay;
        }

        public static void ResetToDefaults()
        {
            Current = new FontManagerConfig();
            NotifyChanged();
        }

        public static void Tick()
        {
            if (_notifyAt >= 0f && Time.unscaledTime >= _notifyAt) NotifyChanged();
            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            {
                _saveAt = -1f;
                SaveNow();
            }
        }

        public static void SaveNow()
        {
            try
            {
                Paths.EnsureDirectories();
                var path = Paths.ConfigFile;
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Current, JsonSettings));
                // Replace atomically so a crash mid-save never leaves the user without a config.
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Log.Error("Failed to save config", e);
            }
        }

        public static void FlushPendingSave()
        {
            if (_saveAt >= 0f)
            {
                _saveAt = -1f;
                SaveNow();
            }
        }

        private static void Sanitize(FontManagerConfig c)
        {
            c.primaryFont ??= "";
            c.titleFont ??= "";
            c.languageFonts ??= new Dictionary<string, string>();
            c.gameFontPatterns ??= new List<string>();
            c.titleFontPatterns ??= new List<string>();
            c.artFontPatterns ??= new List<string>();
            c.excludedFonts ??= new List<string>();
            c.excludedPaths ??= new List<string>();
            c.togglePanelKey ??= "F8";
            c.cycleFontKey ??= "";
            c.panelLanguage = (c.panelLanguage ?? "auto").Trim().ToLowerInvariant();
            if (Array.IndexOf(L10n.Codes, c.panelLanguage) < 0) c.panelLanguage = "auto";

            c.globalScale = Clamp(c.globalScale, 0.3f, 3f, 1f);
            c.uiScale = Clamp(c.uiScale, 0.3f, 3f, 1f);
            c.worldScale = Clamp(c.worldScale, 0.3f, 3f, 1f);
            c.titleScale = Clamp(c.titleScale, 0.3f, 3f, 1f);
            c.verticalOffset = Clamp(c.verticalOffset, -1f, 1f, 0f);
            c.lineSpacingDelta = Clamp(c.lineSpacingDelta, -100f, 100f, 0f);
            c.samplingPointSize = Mathf.Clamp(c.samplingPointSize, 16, 256);
            c.atlasPadding = Mathf.Clamp(c.atlasPadding, 2, 32);
            c.atlasSize = c.atlasSize <= 512 ? 512 : c.atlasSize <= 1024 ? 1024 : c.atlasSize <= 2048 ? 2048 : 4096;
            c.applyBatchPerFrame = Mathf.Clamp(c.applyBatchPerFrame, 20, 5000);
        }

        private static float Clamp(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return Mathf.Clamp(v, min, max);
        }
    }
}
