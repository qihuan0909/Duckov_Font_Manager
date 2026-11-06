using System;
using System.IO;
using UnityEngine;

namespace DuckovFontManager
{
    internal static class Paths
    {
        /// <summary>Mod folder (the one containing info.ini), set on setup.</summary>
        public static string ModDir;

        private static string _configDir;

        public static string GameRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// &lt;game&gt;/ModConfigs/DuckovFontManager, or the per-user data folder if the game folder is not writable.
        /// </summary>
        public static string ConfigDir => _configDir ??= ResolveConfigDir();

        public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
        public static string UserFontDir => Path.Combine(ConfigDir, "Fonts");
        public static string ModFontDir => string.IsNullOrEmpty(ModDir) ? null : Path.Combine(ModDir, "Fonts");

        public static void EnsureDirectories()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                Directory.CreateDirectory(UserFontDir);
            }
            catch (Exception e)
            {
                Log.Error("Could not create the config/font folders", e);
            }
        }

        private static string ResolveConfigDir()
        {
            var preferred = Path.Combine(GameRoot, "ModConfigs", "DuckovFontManager");
            if (IsWritable(preferred)) return preferred;
            var fallback = Path.Combine(Application.persistentDataPath, "DuckovFontManager");
            Log.Warn($"Game folder is not writable, using {fallback}");
            return fallback;
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
