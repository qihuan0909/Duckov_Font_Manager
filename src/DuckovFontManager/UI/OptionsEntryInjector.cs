using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Options.UI;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace DuckovFontManager
{
    /// <summary>Clicking this opens the font manager panel.</summary>
    public sealed class DfmOptionsTab : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            eventData?.Use();
            SettingsPanel.Open();
        }
    }

    /// <summary>
    /// Adds a "字体" tab button next to the tabs of the game's settings panel (cloned from an existing tab so it
    /// matches the game's style). It opens our own panel instead of switching tabs.
    /// </summary>
    internal static class OptionsEntryInjector
    {
        private static readonly FieldInfo TabButtonsField =
            typeof(OptionsPanel).GetField("tabButtons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedIndicatorField =
            typeof(OptionsPanel_TabButton).GetField("selectedIndicator", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly List<GameObject> _injected = new List<GameObject>();
        private static float _next;
        private static bool _failed;

        public static void Tick()
        {
            if (_failed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            _injected.RemoveAll(g => g == null);
            if (_injected.Count > 0) return;

            try
            {
                foreach (var panel in Object.FindObjectsOfType<OptionsPanel>(true))
                {
                    if (panel == null || panel.GetComponentInChildren<DfmOptionsTab>(true) != null) continue;
                    Inject(panel);
                }
            }
            catch (Exception e)
            {
                _failed = true; // don't keep retrying on an incompatible game version
                Log.Error("Could not add the font tab to the game settings (use the hotkey instead)", e);
            }
        }

        private static void Inject(OptionsPanel panel)
        {
            if (TabButtonsField?.GetValue(panel) is not List<OptionsPanel_TabButton> tabs || tabs.Count == 0 || tabs[0] == null) return;
            var source = tabs[0];
            var clone = Object.Instantiate(source.gameObject, source.transform.parent);
            clone.name = "DFM_FontTab";

            var tb = clone.GetComponent<OptionsPanel_TabButton>();
            if (tb != null)
            {
                if (SelectedIndicatorField?.GetValue(tb) is GameObject indicator && indicator != null) indicator.SetActive(false);
                Object.DestroyImmediate(tb);
            }
            foreach (var loc in clone.GetComponentsInChildren<TextLocalizor>(true)) Object.DestroyImmediate(loc);
            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.SetText(IsChinese() ? "字体" : "Fonts");

            clone.AddComponent<DfmOptionsTab>();
            clone.transform.SetAsLastSibling();
            clone.SetActive(true);
            _injected.Add(clone);
            Log.Info("Added the font tab to the game's settings panel.");
        }

        public static void Shutdown()
        {
            foreach (var g in _injected)
                if (g != null) Object.Destroy(g);
            _injected.Clear();
            _failed = false;
        }

        private static bool IsChinese()
        {
            try
            {
                var l = LocalizationManager.CurrentLanguage;
                return l == SystemLanguage.ChineseSimplified || l == SystemLanguage.ChineseTraditional || l == SystemLanguage.Chinese;
            }
            catch { return true; }
        }
    }
}
