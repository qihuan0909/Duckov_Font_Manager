using System;
using System.Collections.Generic;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DuckovFontManager
{
    /// <summary>In-game settings window (F7 or the "字体" tab in the game's settings).</summary>
    internal static class SettingsPanel
    {
        private const string PreviewText =
            "逃离鸭科夫 Escape from Duckov\n" +
            "0123456789  ¥12,345  -128  x3\n" +
            "【任务】撤离点已开启，快跑！The quick brown fox.";

        private static GameObject _canvasGo;
        private static RectTransform _window;
        private static RectTransform _fontListContent;
        private static TextMeshProUGUI _status;
        private static TextMeshProUGUI _effective;
        private static TextMeshProUGUI _languageLine;
        private static readonly List<(string key, Button button, TextMeshProUGUI label)> _fontButtons = new List<(string, Button, TextMeshProUGUI)>();
        private static bool _listDirty;
        private static float _nextStatus;

        public static bool IsOpen => _canvasGo != null && _canvasGo.activeSelf;

        public static void Init()
        {
            FontLibrary.ListChanged += OnListChanged;
            try { UIInputManager.OnCancelEarly += OnCancel; }
            catch (Exception e) { Log.Error("Could not hook the cancel key", e); }
        }

        public static void Shutdown()
        {
            FontLibrary.ListChanged -= OnListChanged;
            try { UIInputManager.OnCancelEarly -= OnCancel; } catch { /* ignore */ }
            if (_canvasGo != null)
            {
                ReleaseInput();
                Object.Destroy(_canvasGo);
            }
            _canvasGo = null;
            _fontButtons.Clear();
        }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            try
            {
                if (_canvasGo == null) Build();
                _canvasGo.SetActive(true);
                RefreshFontList();
                RefreshLabels();
                try { InputManager.DisableInput(_canvasGo); } catch { /* not in a level */ }
            }
            catch (Exception e)
            {
                Log.Error("Could not open the settings panel", e);
            }
        }

        public static void Close()
        {
            if (_canvasGo == null) return;
            ReleaseInput();
            _canvasGo.SetActive(false);
            ConfigService.FlushPendingSave();
        }

        private static void ReleaseInput()
        {
            try { InputManager.ActiveInput(_canvasGo); } catch { /* ignore */ }
        }

        private static void OnCancel(UIInputEventData e)
        {
            if (!IsOpen || e == null) return;
            Close();
            e.Use();
        }

        private static void OnListChanged() => _listDirty = true;

        public static void Tick()
        {
            if (!IsOpen) return;
            if (_listDirty) RefreshFontList();
            if (Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + 0.5f;
                RefreshLabels();
            }
        }

        // ------------------------------------------------------------------ build

        private static void Build()
        {
            Ui.Font = FontLibrary.FindGameMainFont();
            var cfg = ConfigService.Current;

            _canvasGo = new GameObject("DuckovFontManager.Settings");
            _canvasGo.SetActive(false); // build while inactive so TMP initialises with our fonts
            Object.DontDestroyOnLoad(_canvasGo);
            _canvasGo.layer = 5;
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            if (cfg.safePanelFont) _canvasGo.AddComponent<DfmExclude>();

            var dim = Ui.Rect("Dim", _canvasGo.transform);
            Ui.Stretch(dim);
            Ui.Image(dim, new Color(0, 0, 0, 0.55f));

            _window = Ui.Rect("Window", _canvasGo.transform);
            _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 0.5f);
            _window.sizeDelta = new Vector2(1240, 820);
            Ui.Image(_window, Ui.Background);

            BuildHeader();
            BuildFontColumn();
            BuildSettingsColumn();
            BuildFooter();
        }

        private static void BuildHeader()
        {
            var header = Ui.Rect("Header", _window);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 60);
            Ui.Image(header, Ui.Surface);

            var title = Ui.Label(header, "字体管理器  <size=70%><color=#9AA0AA>Duckov Font Manager</color></size>", 28, Ui.TextColor);
            title.fontStyle = FontStyles.Bold;
            Ui.Stretch(title.rectTransform, 20, 0, 80, 0);

            var close = Ui.Button(header, "×", Close, 52, 44, 30);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.sizeDelta = new Vector2(52, 44);
            crt.anchoredPosition = new Vector2(-10, 0);
        }

        private static void BuildFontColumn()
        {
            var col = Ui.Rect("FontColumn", _window);
            col.anchorMin = new Vector2(0, 0);
            col.anchorMax = new Vector2(0, 1);
            col.pivot = new Vector2(0, 0.5f);
            col.offsetMin = new Vector2(16, 64);
            col.offsetMax = new Vector2(16 + 420, -76);

            var title = Ui.Label(col, "字体列表（点击即时切换）", 22, Ui.Accent);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 34);

            var scroll = Ui.ScrollView(col, out _fontListContent, 4, 6);
            var srt = (RectTransform)scroll.transform;
            Ui.Stretch(srt, 0, 40, 0, 50);

            var buttons = Ui.Rect("Buttons", col);
            buttons.anchorMin = new Vector2(0, 0);
            buttons.anchorMax = new Vector2(1, 0);
            buttons.pivot = new Vector2(0.5f, 0);
            buttons.sizeDelta = new Vector2(0, 42);
            var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            Ui.Button(buttons, "打开字体目录", OpenFontFolder);
            Ui.Button(buttons, "重新扫描", () => FontLibrary.Rescan());
        }

        private static void BuildSettingsColumn()
        {
            var col = Ui.Rect("SettingsColumn", _window);
            Ui.Stretch(col, 16 + 420 + 16, 76, 16, 64);
            Ui.ScrollView(col, out var c, 6, 14);
            Ui.Stretch((RectTransform)col.GetChild(0), 0, 0, 0, 0);
            var cfg = ConfigService.Current;

            // ---- Preview
            Ui.SectionHeader(c, "预览");
            var previewBox = Ui.Rect("PreviewBox", c);
            Ui.Layout(previewBox.gameObject, 210);
            Ui.Image(previewBox, Ui.SurfaceAlt);
            previewBox.gameObject.AddComponent<RectMask2D>();
            var preview = Ui.Label(previewBox, PreviewText, 30, Ui.TextColor, TextAlignmentOptions.MidlineLeft, true);
            Ui.Stretch(preview.rectTransform, 16, 8, 16, 8);
            preview.gameObject.AddComponent<DfmForceInclude>();

            _effective = Ui.Label(c, "", 20, Ui.TextDim);
            Ui.Layout(_effective.gameObject, 30);
            _languageLine = Ui.Label(c, "", 20, Ui.TextDim);
            Ui.Layout(_languageLine.gameObject, 30);
            var langRow = Ui.HRow(c, 38);
            Ui.Button(langRow.transform, "当前语言使用所选字体", BindLanguage);
            Ui.Button(langRow.transform, "清除当前语言绑定", ClearLanguage);

            // ---- Scope
            Ui.SectionHeader(c, "替换范围");
            Ui.Toggle(c, "启用字体替换", cfg.enabled, v => Set(() => cfg.enabled = v));
            Ui.Toggle(c, "替换标题字体（粗体标题）", cfg.replaceTitleFont, v => Set(() => cfg.replaceTitleFont = v));
            Ui.Toggle(c, "强制覆盖美术字体（像素、数码管、等宽等风格字体）", cfg.forceOverrideArtFonts, v => Set(() => cfg.forceOverrideArtFonts = v));
            Ui.Toggle(c, "替换其他 Mod 自带的字体", cfg.replaceUnknownFonts, v => Set(() => cfg.replaceUnknownFonts = v));
            Ui.Toggle(c, "设置面板保持原版字体（选错字体时也能看清）", cfg.safePanelFont, SetSafePanelFont);

            // ---- Size
            Ui.SectionHeader(c, "字号");
            Ui.Slider(c, "全局字号", 0.5f, 2f, cfg.globalScale, Percent, v => SetDeferred(() => cfg.globalScale = Round2(v)));
            Ui.Slider(c, "界面文字", 0.5f, 2f, cfg.uiScale, Percent, v => SetDeferred(() => cfg.uiScale = Round2(v)));
            Ui.Slider(c, "世界文字（伤害飘字等）", 0.5f, 2f, cfg.worldScale, Percent, v => SetDeferred(() => cfg.worldScale = Round2(v)));
            Ui.Slider(c, "标题文字", 0.5f, 2f, cfg.titleScale, Percent, v => SetDeferred(() => cfg.titleScale = Round2(v)));
            Ui.Toggle(c, "视觉字号归一化（让新字体看起来和原字体一样大）", cfg.normalizeMetrics, v => Set(() => cfg.normalizeMetrics = v));

            // ---- Layout tweaks
            Ui.SectionHeader(c, "排版微调（仅对已替换的文字生效）");
            Ui.Slider(c, "垂直偏移（正值向上）", -0.3f, 0.3f, cfg.verticalOffset, v => v.ToString("+0.00;-0.00;0") + " em",
                v => SetDeferred(() => cfg.verticalOffset = Round2(v)));
            Ui.Slider(c, "行距调整", -40f, 40f, cfg.lineSpacingDelta, v => v.ToString("+0;-0;0"),
                v => SetDeferred(() => cfg.lineSpacingDelta = Mathf.Round(v)), true);

            // ---- Advanced
            Ui.SectionHeader(c, "高级");
            Ui.Toggle(c, "预热常用字（减少首次显示卡顿）", cfg.prewarm, v => Set(() => cfg.prewarm = v));
            Ui.Toggle(c, "在列表中显示系统字体", cfg.includeSystemFonts, v => Set(() => cfg.includeSystemFonts = v));
            Ui.Toggle(c, "自动监视字体目录（放入新字体立即可用）", cfg.watchFontFolder, v => Set(() => cfg.watchFontFolder = v));
            Ui.Toggle(c, "定期扫描（排查个别文字未替换时开启）", cfg.periodicScan, v => Set(() => cfg.periodicScan = v));
            Ui.Toggle(c, "调试日志", cfg.debugLog, v => Set(() => cfg.debugLog = v));
            var hint = Ui.Label(c,
                $"快捷键：{cfg.togglePanelKey} 打开/关闭本面板。更多选项（排除规则、按字体分类、图集参数）见 config.json。",
                18, Ui.TextDim, TextAlignmentOptions.TopLeft, true);
            Ui.Layout(hint.gameObject, 52);
        }

        private static void BuildFooter()
        {
            var footer = Ui.Rect("Footer", _window);
            footer.anchorMin = new Vector2(0, 0);
            footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(0.5f, 0);
            footer.sizeDelta = new Vector2(0, 56);
            Ui.Image(footer, Ui.Surface);

            _status = Ui.Label(footer, "", 18, Ui.TextDim);
            Ui.Stretch(_status.rectTransform, 20, 0, 330, 0);

            var reset = Ui.Button(footer, "恢复默认", ResetDefaults, 150, 40);
            Place(reset, -180);
            var close = Ui.Button(footer, "关闭", Close, 150, 40);
            Place(close, -16);
        }

        private static void Place(Button b, float x)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(150, 40);
            rt.anchoredPosition = new Vector2(x, 0);
        }

        // ------------------------------------------------------------------ font list

        private static void RefreshFontList()
        {
            _listDirty = false;
            if (_fontListContent == null) return;
            for (int i = _fontListContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_fontListContent.GetChild(i).gameObject);
            _fontButtons.Clear();

            AddFontButton("", "游戏原版字体", null);
            FontSource? lastSource = null;
            foreach (var e in FontLibrary.Entries)
            {
                if (lastSource != e.Source)
                {
                    lastSource = e.Source;
                    var header = Ui.Label(_fontListContent, e.Source switch
                    {
                        FontSource.User => "自定义字体（ModConfigs/DuckovFontManager/Fonts）",
                        FontSource.Mod => "Mod 内置字体",
                        _ => "系统字体",
                    }, 17, Ui.TextDim);
                    Ui.Layout(header.gameObject, 30);
                }
                AddFontButton(e.Key, e.DisplayName, e);
            }
            if (FontLibrary.Entries.Count == 0)
            {
                var empty = Ui.Label(_fontListContent, "还没有自定义字体。\n点击“打开字体目录”，放入 .ttf / .otf 文件即可。", 18, Ui.TextDim,
                    TextAlignmentOptions.TopLeft, true);
                Ui.Layout(empty.gameObject, 60);
            }
            UpdateFontSelection();
        }

        private static void AddFontButton(string key, string name, FontEntry entry)
        {
            string text = name;
            if (entry != null && entry.Failed) text += "  <color=#E06A5C>（无法加载）</color>";
            var btn = Ui.Button(_fontListContent, text, () => SelectFont(key), -1, 38, 19);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            _fontButtons.Add((key, btn, label));
        }

        private static void UpdateFontSelection()
        {
            var selected = ConfigService.Current.primaryFont ?? "";
            foreach (var (key, button, label) in _fontButtons)
            {
                if (button == null) continue;
                bool on = string.Equals(key, selected, StringComparison.OrdinalIgnoreCase);
                Ui.SetButtonColors(button, on ? Ui.Accent : Ui.ButtonColor, on ? Ui.Accent : Ui.ButtonHover);
                if (label != null) label.color = on ? Ui.TextDark : Ui.TextColor;
            }
        }

        private static void SelectFont(string key)
        {
            var cfg = ConfigService.Current;
            cfg.primaryFont = key ?? "";
            if (!string.IsNullOrEmpty(key) && FontLibrary.GetAsset(key) == null)
                Log.Warn($"Font '{key}' could not be loaded.");
            ConfigService.NotifyChanged();
            if (FontLibrary.GetEntry(key)?.Failed == true) _listDirty = true;
            UpdateFontSelection();
            RefreshLabels();
        }

        // ------------------------------------------------------------------ actions

        private static void Set(Action change)
        {
            change();
            ConfigService.NotifyChanged();
            RefreshLabels();
        }

        private static void SetDeferred(Action change)
        {
            change();
            ConfigService.NotifyChangedDeferred();
        }

        private static void SetSafePanelFont(bool v)
        {
            ConfigService.Current.safePanelFont = v;
            if (_canvasGo != null)
            {
                var ex = _canvasGo.GetComponent<DfmExclude>();
                if (v && ex == null) _canvasGo.AddComponent<DfmExclude>();
                if (!v && ex != null) Object.DestroyImmediate(ex);
            }
            ConfigService.NotifyChanged();
        }

        private static void BindLanguage()
        {
            var cfg = ConfigService.Current;
            cfg.languageFonts[CurrentLanguage()] = cfg.primaryFont ?? "";
            ConfigService.NotifyChanged();
            RefreshLabels();
        }

        private static void ClearLanguage()
        {
            ConfigService.Current.languageFonts.Remove(CurrentLanguage());
            ConfigService.NotifyChanged();
            RefreshLabels();
        }

        private static void ResetDefaults()
        {
            ConfigService.ResetToDefaults();
            // Rebuild so every control shows the default values.
            bool open = IsOpen;
            if (_canvasGo != null)
            {
                ReleaseInput();
                Object.Destroy(_canvasGo);
                _canvasGo = null;
            }
            if (open) Open();
        }

        private static void OpenFontFolder()
        {
            Paths.EnsureDirectories();
            Application.OpenURL("file:///" + Paths.UserFontDir.Replace('\\', '/'));
        }

        // ------------------------------------------------------------------ labels

        private static void RefreshLabels()
        {
            if (_canvasGo == null) return;
            var cfg = ConfigService.Current;
            if (_effective != null)
            {
                var key = RuleEngine.EffectivePrimaryKey();
                _effective.text = "当前生效字体：<color=#F5C242>" + FontName(key) + "</color>" + (cfg.enabled ? "" : "  <color=#E06A5C>（替换已关闭）</color>");
            }
            if (_languageLine != null)
            {
                var lang = CurrentLanguage();
                string display;
                try { display = LocalizationManager.CurrentLanguageDisplayName; } catch { display = lang; }
                _languageLine.text = cfg.languageFonts.TryGetValue(lang, out var bound)
                    ? $"当前语言：{display}　已绑定字体：<color=#F5C242>{FontName(bound)}</color>"
                    : $"当前语言：{display}　未绑定（使用上面列表中选中的字体）";
            }
            if (_status != null)
            {
                string prewarm = Prewarmer.Busy ? $" · 预热中 {Prewarmer.Progress:P0}" : "";
                string mode = PatchService.HooksActive ? HarmonyShim.Description : "扫描模式";
                _status.text = $"已处理文本 {TextRegistry.Count} · 字体资源 {FontLibrary.AssetCount} · 补丁 {PatchService.PatchCount}（{mode}）{prewarm}";
            }
            UpdateFontSelection();
        }

        private static string FontName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "游戏原版字体";
            var e = FontLibrary.GetEntry(key);
            if (e == null) return key + "（文件不存在）";
            return e.Failed ? e.DisplayName + "（无法加载）" : e.DisplayName;
        }

        private static string CurrentLanguage()
        {
            try { return LocalizationManager.CurrentLanguage.ToString(); }
            catch { return "Unknown"; }
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
        private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
    }
}
