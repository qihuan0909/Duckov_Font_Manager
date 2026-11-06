using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>
    /// Renders the characters used by the current language's localisation file into a dynamic font
    /// asset a few at a time, so they don't have to be rasterised the first time they appear.
    /// </summary>
    internal static class Prewarmer
    {
        private const int CharsPerFrame = 24;

        private static readonly Queue<TMP_FontAsset> _queue = new Queue<TMP_FontAsset>();
        private static TMP_FontAsset _current;
        private static string _chars;
        private static int _pos;
        private static string _charsetLanguage;
        private static string _charset;

        public static bool Busy => _current != null || _queue.Count > 0;
        public static float Progress => _current == null || string.IsNullOrEmpty(_chars) ? 1f : (float)_pos / _chars.Length;

        public static void Enqueue(TMP_FontAsset fa)
        {
            if (fa != null && fa != _current && !_queue.Contains(fa)) _queue.Enqueue(fa);
        }

        public static void Clear()
        {
            _queue.Clear();
            _current = null;
        }

        public static void Tick()
        {
            if (_current == null)
            {
                if (_queue.Count == 0) return;
                _current = _queue.Dequeue();
                _chars = GetCharset();
                _pos = 0;
                if (_current == null || string.IsNullOrEmpty(_chars)) { _current = null; return; }
            }
            if (_current == null) { _current = null; return; } // destroyed meanwhile

            int len = Math.Min(CharsPerFrame, _chars.Length - _pos);
            if (len > 0)
            {
                try { _current.TryAddCharacters(_chars.Substring(_pos, len)); }
                catch (Exception e) { Log.ErrorOnce("prewarm", "Prewarm failed", e); _current = null; return; }
                _pos += len;
            }
            if (_pos >= _chars.Length)
            {
                Log.Verbose($"Prewarmed {_chars.Length} characters into {_current.name}");
                _current = null;
            }
        }

        private static string GetCharset()
        {
            string lang;
            try { lang = LocalizationManager.CurrentLanguage.ToString(); }
            catch { lang = "English"; }
            if (_charset != null && _charsetLanguage == lang) return _charset;

            var set = new HashSet<char>();
            for (char c = (char)32; c < 127; c++) set.Add(c);
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "Localization", lang + ".csv");
                if (File.Exists(path))
                {
                    foreach (var c in File.ReadAllText(path, Encoding.UTF8))
                        if (c >= 32 && !char.IsSurrogate(c) && !char.IsControl(c)) set.Add(c);
                }
            }
            catch (Exception e) { Log.Error("Could not read localisation charset", e); }

            var sb = new StringBuilder(set.Count);
            foreach (var c in set) sb.Append(c);
            _charsetLanguage = lang;
            _charset = sb.ToString();
            Log.Verbose($"Prewarm charset for {lang}: {_charset.Length} characters");
            return _charset;
        }
    }
}
