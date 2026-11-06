using System;
using System.Collections.Generic;
using System.Linq;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DuckovFontManager
{
    /// <summary>Orchestrates the mod: startup, hot switching, scanning, hotkeys and shutdown.</summary>
    internal static class FontManagerCore
    {
        public static bool Active { get; private set; }

        /// <summary>Incremented whenever the desired state of texts may have changed.</summary>
        public static int Version { get; private set; } = 1;

        private static bool _reapplyRequested;
        private static List<TextState> _pass;
        private static int _passIndex;

        private static float _scanAt = -1f;
        private static float _nextPeriodicScan;
        private static float _nextPrune;

        private static string _atlasParams;
        private static bool _lastIncludeSystemFonts;

        private static readonly Dictionary<string, Key?> _keyCache = new Dictionary<string, Key?>();

        // ------------------------------------------------------------------ lifecycle

        public static void Init(string modDir)
        {
            if (Active) return;
            try
            {
                Paths.ModDir = modDir;
                Paths.EnsureDirectories();
                ConfigService.Load();
                var cfg = ConfigService.Current;
                _atlasParams = AtlasParams(cfg);
                _lastIncludeSystemFonts = cfg.includeSystemFonts;

                TMPro.ShaderUtilities.GetShaderPropertyIDs();
                FontLibrary.CleanCache();
                FontLibrary.Rescan();

                Active = true;
                PatchService.Apply();

                ConfigService.Changed += OnConfigChanged;
                FontLibrary.AssetsInvalidated += Bump;
                SceneManager.sceneLoaded += OnSceneLoaded;
                try { LocalizationManager.OnSetLanguage += OnLanguageChanged; }
                catch (Exception e) { Log.Error("Could not subscribe to language changes", e); }

                if (cfg.watchFontFolder) FontFolderWatcher.Start();
                SettingsPanel.Init();

                // Texts that were already enabled before we loaded (main menu) never hit the OnEnable hook.
                ScanScene();
                Log.Info($"Initialised. Fonts folder: {Paths.UserFontDir}");
            }
            catch (Exception e)
            {
                Log.Error("Initialisation failed", e);
            }
        }

        public static void Shutdown()
        {
            if (!Active) return;
            Active = false;
            try
            {
                PatchService.Remove();
                ConfigService.Changed -= OnConfigChanged;
                FontLibrary.AssetsInvalidated -= Bump;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                try { LocalizationManager.OnSetLanguage -= OnLanguageChanged; } catch { /* ignore */ }
                FontFolderWatcher.Stop();
                ConfigService.FlushPendingSave();

                SettingsPanel.Shutdown();
                OptionsEntryInjector.Shutdown();

                _pass = null;
                _reapplyRequested = false;
                TextRegistry.RestoreAll();
                Prewarmer.Clear();
                MaterialMapper.DestroyAll();
                FontLibrary.DestroyAll();
                MetricsNormalizer.Clear();
                Log.Info("Shut down; all texts restored.");
            }
            catch (Exception e)
            {
                Log.Error("Shutdown failed", e);
            }
        }

        // ------------------------------------------------------------------ per-frame

        public static void Tick()
        {
            if (!Active) return;
            try
            {
                ConfigService.Tick();
                FontFolderWatcher.Tick();
                HandleHotkeys();
                RunReapplyPass();
                Prewarmer.Tick();
                OptionsEntryInjector.Tick();
                SettingsPanel.Tick();

                float now = Time.unscaledTime;
                if (_scanAt >= 0f && now >= _scanAt)
                {
                    _scanAt = -1f;
                    ScanScene();
                }
                if ((!PatchService.HooksActive || ConfigService.Current.periodicScan) && now >= _nextPeriodicScan)
                {
                    _nextPeriodicScan = now + 1.5f;
                    ScanScene();
                }
                if (now >= _nextPrune)
                {
                    _nextPrune = now + 30f;
                    TextRegistry.Prune();
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("tick", "Update loop error", e);
            }
        }

        // ------------------------------------------------------------------ hot switching

        /// <summary>Marks every text as stale and re-applies all of them over the next frames.</summary>
        public static void Bump()
        {
            Version++;
            _reapplyRequested = true;
            FontManagerAPI.RaiseChanged();
        }

        private static void RunReapplyPass()
        {
            if (_reapplyRequested)
            {
                _reapplyRequested = false;
                // Visible texts first so the change is seen immediately.
                _pass = TextRegistry.Snapshot()
                    .OrderBy(s => s.Text != null && s.Text.isActiveAndEnabled ? 0 : 1)
                    .ToList();
                _passIndex = 0;
            }
            if (_pass == null) return;

            int budget = ConfigService.Current.applyBatchPerFrame;
            while (budget-- > 0 && _passIndex < _pass.Count)
            {
                var s = _pass[_passIndex++];
                if (s.Text != null) FontApplier.Apply(s, true);
            }
            if (_passIndex >= _pass.Count)
            {
                Log.Verbose($"Re-applied {_pass.Count} texts (version {Version}).");
                _pass = null;
                FontLibrary.FlushPendingDestroy();
            }
        }

        private static void OnConfigChanged()
        {
            var cfg = ConfigService.Current;

            var atlas = AtlasParams(cfg);
            if (atlas != _atlasParams)
            {
                _atlasParams = atlas;
                foreach (var e in FontLibrary.Entries.ToList()) FontLibrary.Invalidate(e.Key);
                MetricsNormalizer.Clear();
            }
            if (cfg.includeSystemFonts != _lastIncludeSystemFonts)
            {
                _lastIncludeSystemFonts = cfg.includeSystemFonts;
                FontLibrary.Rescan();
            }
            if (cfg.watchFontFolder && !FontFolderWatcher.Running) FontFolderWatcher.Start();
            else if (!cfg.watchFontFolder && FontFolderWatcher.Running) FontFolderWatcher.Stop();

            Bump();
        }

        private static void OnLanguageChanged(SystemLanguage language)
        {
            Log.Info($"Language changed to {language}; re-applying fonts.");
            MetricsNormalizer.Clear();
            Bump();
            if (ConfigService.Current.prewarm)
            {
                var fa = FontLibrary.GetAsset(RuleEngine.EffectivePrimaryKey());
                if (fa != null) Prewarmer.Enqueue(fa);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _scanAt = Time.unscaledTime + 0.2f;
        }

        /// <summary>Registers and applies to every active TMP text (fallback for objects the hooks missed).</summary>
        public static void ScanScene()
        {
            try
            {
                var texts = Object.FindObjectsOfType<TMP_Text>();
                foreach (var t in texts)
                {
                    if (t == null) continue;
                    FontApplier.Apply(TextRegistry.GetOrCreate(t), false);
                }
                Log.Verbose($"Scanned {texts.Length} active texts.");
            }
            catch (Exception e)
            {
                Log.ErrorOnce("scan", "Scene scan failed", e);
            }
        }

        // ------------------------------------------------------------------ hotkeys

        private static void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            var cfg = ConfigService.Current;

            if (Pressed(kb, cfg.togglePanelKey))
            {
                if (SettingsPanel.IsOpen || !IsTyping()) SettingsPanel.Toggle();
            }
            if (Pressed(kb, cfg.cycleFontKey) && !IsTyping())
            {
                CycleFont();
            }
        }

        private static bool Pressed(Keyboard kb, string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;
            if (!_keyCache.TryGetValue(keyName, out var key))
            {
                key = Enum.TryParse<Key>(keyName, true, out var k) && k != Key.None ? k : (Key?)null;
                _keyCache[keyName] = key;
                if (key == null) Log.Warn($"Unknown key name '{keyName}'");
            }
            if (key == null) return false;
            try { return kb[key.Value].wasPressedThisFrame; }
            catch { return false; }
        }

        private static bool IsTyping()
        {
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            if (go == null) return false;
            var tmpInput = go.GetComponent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused) return true;
            var legacy = go.GetComponent<UnityEngine.UI.InputField>();
            return legacy != null && legacy.isFocused;
        }

        /// <summary>Switches the primary font to the next user/bundled font (or back to the game font).</summary>
        public static void CycleFont()
        {
            var keys = new List<string> { "" };
            keys.AddRange(FontLibrary.Entries.Where(e => e.Source != FontSource.System && !e.Failed).Select(e => e.Key));
            var cfg = ConfigService.Current;
            int i = keys.FindIndex(k => string.Equals(k, cfg.primaryFont, StringComparison.OrdinalIgnoreCase));
            cfg.primaryFont = keys[(i + 1) % keys.Count];
            Log.Info($"Cycled font to '{(cfg.primaryFont == "" ? "game font" : cfg.primaryFont)}'");
            ConfigService.NotifyChanged();
        }

        private static string AtlasParams(FontManagerConfig c) => $"{c.samplingPointSize}/{c.atlasPadding}/{c.atlasSize}";
    }
}
