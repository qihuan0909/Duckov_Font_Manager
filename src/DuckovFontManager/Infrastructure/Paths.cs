using System.IO;
using UnityEngine;

namespace DuckovFontManager
{
    internal static class Paths
    {
        /// <summary>Mod folder (the one containing info.ini), set on setup.</summary>
        public static string ModDir;

        public static string GameRoot => Path.GetDirectoryName(Application.dataPath);
        public static string ConfigDir => Path.Combine(GameRoot, "ModConfigs", "DuckovFontManager");
        public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
        public static string UserFontDir => Path.Combine(ConfigDir, "Fonts");
        public static string ModFontDir => string.IsNullOrEmpty(ModDir) ? null : Path.Combine(ModDir, "Fonts");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(UserFontDir);
        }
    }
}
