using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>Original and applied values of one TMP text component.</summary>
    internal sealed class TextState
    {
        public TMP_Text Text;
        public DfmTextMarker Marker;
        public bool IsWorld;

        // Originals (what the game/other mods intended).
        public TMP_FontAsset OrigFont;
        public Material OrigMat;
        public float OrigSize;
        public float OrigMin;
        public float OrigMax;
        public float OrigLineSpacing;
        public Vector4 OrigMargin;

        // What we last applied.
        public int AppliedVersion = -1;
        public TMP_FontAsset AppliedFont;
        public Material AppliedMat;
        public float Factor = 1f;
        public float LineSpacingDelta;
        public float MarginShift;

        // Cached exclusion result.
        public int ExclusionVersion = -1;
        public bool Excluded;

        public void SaveToMarker()
        {
            if (Marker == null) return;
            Marker.valid = true;
            Marker.ownerId = Text != null ? Text.GetInstanceID() : 0;
            Marker.origFont = OrigFont;
            Marker.origMat = OrigMat;
            Marker.origSize = OrigSize;
            Marker.origMin = OrigMin;
            Marker.origMax = OrigMax;
            Marker.origLineSpacing = OrigLineSpacing;
            Marker.origMargin = OrigMargin;
        }
    }
}
