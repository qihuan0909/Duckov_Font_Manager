using System;
using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>
    /// Public API for other mods. Safe to call through reflection, e.g.
    /// <c>Type.GetType("DuckovFontManager.FontManagerAPI, DuckovFontManager")</c>.
    /// </summary>
    public static class FontManagerAPI
    {
        /// <summary>Raised after fonts or sizes were changed (on the main thread).</summary>
        public static event Action FontChanged;

        public static bool IsActive => FontManagerCore.Active;

        /// <summary>Key of the font currently in effect ("" = game font).</summary>
        public static string CurrentFontKey => RuleEngine.EffectivePrimaryKey() ?? "";

        /// <summary>The replacement TMP font asset currently in effect, or null when the game font is used.</summary>
        public static TMP_FontAsset CurrentFontAsset => FontLibrary.GetAsset(RuleEngine.EffectivePrimaryKey());

        public static float GlobalScale => ConfigService.Current.globalScale;

        /// <summary>Stop the font manager from touching texts under <paramref name="root"/>.</summary>
        public static void Exclude(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponent<DfmExclude>() == null) root.AddComponent<DfmExclude>();
            FontManagerCore.Bump();
        }

        /// <summary>Undo <see cref="Exclude"/>.</summary>
        public static void Include(GameObject root)
        {
            if (root == null) return;
            var ex = root.GetComponent<DfmExclude>();
            if (ex != null) UnityEngine.Object.DestroyImmediate(ex);
            FontManagerCore.Bump();
        }

        /// <summary>Re-evaluate one text immediately (e.g. after creating it while the manager was busy).</summary>
        public static void Refresh(TMP_Text text)
        {
            if (!FontManagerCore.Active || text == null) return;
            FontApplier.Apply(TextRegistry.GetOrCreate(text), true);
        }

        internal static void RaiseChanged()
        {
            try { FontChanged?.Invoke(); }
            catch (Exception e) { Log.Error("FontChanged subscriber threw", e); }
        }
    }
}
