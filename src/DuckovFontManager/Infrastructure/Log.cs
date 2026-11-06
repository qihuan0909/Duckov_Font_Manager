using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckovFontManager
{
    internal static class Log
    {
        private const string Prefix = "[DuckovFontManager] ";
        private static readonly HashSet<string> _once = new HashSet<string>();

        public static bool DebugEnabled;

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
        public static void Error(string message, Exception e) => Debug.LogError(Prefix + message + "\n" + e);

        public static void Verbose(string message)
        {
            if (DebugEnabled) Debug.Log(Prefix + message);
        }

        /// <summary>Logs an error only the first time a given key is seen (for hot paths).</summary>
        public static void ErrorOnce(string key, string message, Exception e)
        {
            if (_once.Add(key)) Error(message, e);
        }
    }
}
