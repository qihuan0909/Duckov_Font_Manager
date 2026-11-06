using System;
using System.Collections.Generic;

namespace DuckovFontManager
{
    /// <summary>Persistent settings (serialized to ModConfigs/DuckovFontManager/config.json).</summary>
    [Serializable]
    public sealed class FontManagerConfig
    {
        public const int CurrentSchema = 2;
        public int schemaVersion = CurrentSchema;

        // ---- Replacement ----
        public bool enabled = true;
        /// <summary>Font key (see FontLibrary) used for all replaced text. Empty = keep the game font.</summary>
        public string primaryFont = "";
        /// <summary>Font key for title text. Empty = follow primary font.</summary>
        public string titleFont = "";
        public bool replaceTitleFont = true;
        /// <summary>Also replace stylised art fonts (pixel, 7-segment, monospace...). Off by default.</summary>
        public bool forceOverrideArtFonts = false;
        /// <summary>Replace fonts the mod does not recognise (e.g. fonts shipped by other mods).</summary>
        public bool replaceUnknownFonts = true;
        /// <summary>Per-language font override: SystemLanguage name -> font key.</summary>
        public Dictionary<string, string> languageFonts = new Dictionary<string, string>();

        // ---- Size ----
        public float globalScale = 1f;
        public float uiScale = 1f;
        public float worldScale = 1f;
        public float titleScale = 1f;
        /// <summary>Scale the replacement font so its glyphs look as large as the original's.</summary>
        public bool normalizeMetrics = true;
        /// <summary>Vertical offset of replaced text, in em (positive = up).</summary>
        public float verticalOffset = 0f;
        /// <summary>Added to TMP lineSpacing of replaced text.</summary>
        public float lineSpacingDelta = 0f;

        // ---- Classification (substring match on TMP font asset name, case-insensitive) ----
        public List<string> gameFontPatterns = new List<string> { "ResourceHanRounded", "Jua-Regular", "LiberationSans" };
        public List<string> titleFontPatterns = new List<string> { "SourceHanSansCN-Heavy" };
        public List<string> artFontPatterns = new List<string> { "pixel", "7segment", "Unifont", "PressStart", "Inconsolata" };

        // ---- Exclusions ----
        /// <summary>Original font names (substring) that are never touched.</summary>
        public List<string> excludedFonts = new List<string>();
        /// <summary>Regex patterns matched against the hierarchy path, e.g. "^SomeModCanvas/".</summary>
        public List<string> excludedPaths = new List<string>();

        // ---- Hot switching ----
        /// <summary>Key name from UnityEngine.InputSystem.Key. F8 (F7 is UnityExplorer's default).</summary>
        public string togglePanelKey = "F8";
        public string cycleFontKey = "";
        public bool watchFontFolder = true;
        public bool includeSystemFonts = true;

        // ---- Advanced ----
        public bool prewarm = true;
        public int samplingPointSize = 72;
        public int atlasPadding = 7;
        public int atlasSize = 2048;
        public int applyBatchPerFrame = 300;
        public bool periodicScan = false;
        /// <summary>Keep the settings panel in the game font so it stays readable whatever font is chosen.</summary>
        public bool safePanelFont = true;
        public bool debugLog = false;
    }
}
