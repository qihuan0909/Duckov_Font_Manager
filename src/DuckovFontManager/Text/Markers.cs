using TMPro;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>
    /// Add to a GameObject to keep the font manager away from every TMP text below it.
    /// Other mods can add it by type name ("DuckovFontManager.DfmExclude") or via <see cref="FontManagerAPI.Exclude"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DfmExclude : MonoBehaviour { }

    /// <summary>Forces a text to be processed even inside a <see cref="DfmExclude"/> subtree (settings preview).</summary>
    [DisallowMultipleComponent]
    public sealed class DfmForceInclude : MonoBehaviour { }

    /// <summary>
    /// Remembers a text's original values on its GameObject. Because Instantiate copies serialized fields,
    /// clones of an already modified text still know the true originals (prevents double scaling).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DfmTextMarker : MonoBehaviour
    {
        [SerializeField] public int ownerId;
        [SerializeField] public bool valid;
        [SerializeField] public TMP_FontAsset origFont;
        [SerializeField] public Material origMat;
        [SerializeField] public float origSize;
        [SerializeField] public float origMin;
        [SerializeField] public float origMax;
        [SerializeField] public float origLineSpacing;
        [SerializeField] public Vector4 origMargin;
    }
}
