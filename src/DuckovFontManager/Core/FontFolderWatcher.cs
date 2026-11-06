using System;
using System.IO;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>Watches the user font folder and triggers a (debounced) rescan on the main thread.</summary>
    internal static class FontFolderWatcher
    {
        private static FileSystemWatcher _watcher;
        private static volatile bool _dirty;
        private static float _rescanAt = -1f;

        public static bool Running => _watcher != null;

        public static void Start()
        {
            if (_watcher != null) return;
            try
            {
                Paths.EnsureDirectories();
                _watcher = new FileSystemWatcher(Paths.UserFontDir)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                _watcher.Changed += OnEvent;
                _watcher.Created += OnEvent;
                _watcher.Deleted += OnEvent;
                _watcher.Renamed += OnEvent;
                _watcher.EnableRaisingEvents = true;
                Log.Verbose("Watching " + Paths.UserFontDir);
            }
            catch (Exception e)
            {
                Log.Error("Could not watch the font folder", e);
                Stop();
            }
        }

        public static void Stop()
        {
            if (_watcher == null) return;
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            catch { /* ignore */ }
            _watcher = null;
            _rescanAt = -1f;
        }

        // Called from a worker thread: only set a flag.
        private static void OnEvent(object sender, FileSystemEventArgs e) => _dirty = true;

        public static void Tick()
        {
            if (_dirty)
            {
                _dirty = false;
                _rescanAt = Time.unscaledTime + 1f; // wait for copies to finish
            }
            if (_rescanAt >= 0f && Time.unscaledTime >= _rescanAt)
            {
                _rescanAt = -1f;
                Log.Info("Font folder changed, rescanning.");
                FontLibrary.Rescan();
            }
        }
    }
}
