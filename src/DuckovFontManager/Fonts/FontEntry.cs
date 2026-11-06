using System;
using System.IO;

namespace DuckovFontManager
{
    internal enum FontSource { User, Mod, System }

    /// <summary>A font file discovered on disk.</summary>
    internal sealed class FontEntry
    {
        public string Key;          // e.g. "user/MyFont.ttf"
        public string FilePath;
        public string FileName;
        public FontSource Source;
        public long FileSize;
        public DateTime LastWriteUtc;

        public bool MetadataLoaded;
        public string FamilyName;
        public string StyleName;

        public bool Failed;
        public string Error;

        public string DisplayName
        {
            get
            {
                // FreeType sometimes returns non-ASCII family names as '?' (e.g. Chinese-only name tables).
                if (string.IsNullOrEmpty(FamilyName) || FamilyName.IndexOf('?') >= 0 || FamilyName.IndexOf('�') >= 0)
                    return Path.GetFileNameWithoutExtension(FileName);
                if (string.IsNullOrEmpty(StyleName) || StyleName.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                    return FamilyName;
                return FamilyName + " " + StyleName;
            }
        }
    }
}
