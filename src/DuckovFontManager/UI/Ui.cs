using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovFontManager
{
    /// <summary>Small helpers to build uGUI controls in code (no asset bundle needed).</summary>
    internal static class Ui
    {
        public static TMP_FontAsset Font;

        public static readonly Color Background = new Color(0.08f, 0.09f, 0.11f, 0.97f);
        public static readonly Color Surface = new Color(0.13f, 0.14f, 0.17f, 1f);
        public static readonly Color SurfaceAlt = new Color(0.17f, 0.18f, 0.22f, 1f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.29f, 1f);
        public static readonly Color ButtonHover = new Color(0.30f, 0.33f, 0.40f, 1f);
        public static readonly Color Accent = new Color(0.96f, 0.76f, 0.26f, 1f);
        public static readonly Color AccentDim = new Color(0.55f, 0.43f, 0.15f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.65f, 0.71f, 1f);
        public static readonly Color TextDark = new Color(0.10f, 0.10f, 0.12f, 1f);
        public static readonly Color Danger = new Color(0.90f, 0.40f, 0.36f, 1f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static LayoutElement Layout(GameObject go, float height = -1, float width = -1, float flexWidth = -1, float flexHeight = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0) le.flexibleHeight = flexHeight;
            return le;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
        {
            var rt = Rect("Label", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        /// <summary>Shrinks a single-line label (down to 65%) when a translation is longer than the space.</summary>
        public static void FitText(TextMeshProUGUI label, float size)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Max(10f, size * 0.65f);
        }

        public static HorizontalLayoutGroup HRow(Transform parent, float height, float spacing = 8)
        {
            var rt = Rect("Row", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            Layout(rt.gameObject, height);
            return h;
        }

        public static void SetButtonColors(Button b, Color normal, Color hover)
        {
            var cb = b.colors;
            cb.normalColor = normal;
            cb.highlightedColor = hover;
            cb.selectedColor = normal;
            cb.pressedColor = Color.Lerp(hover, Accent, 0.5f);
            cb.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
        }

        public static Button Button(Transform parent, string text, Action onClick, float width = -1, float height = 36, float fontSize = 20)
        {
            var rt = Rect("Button", parent);
            var img = Image(rt, Color.white);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            SetButtonColors(btn, ButtonColor, ButtonHover);
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var label = Label(rt, text, fontSize, TextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 10, 0, 10, 0);
            FitText(label, fontSize);
            btn.onClick.AddListener(() =>
            {
                try { onClick?.Invoke(); }
                catch (Exception e) { Log.Error("UI action failed", e); }
            });
            Layout(rt.gameObject, height, width, width < 0 ? 1 : -1);
            return btn;
        }

        public static Toggle Toggle(Transform parent, string text, bool value, Action<bool> onChange, float height = 34)
        {
            var row = Rect("Toggle", parent);
            Layout(row.gameObject, height);
            Image(row, new Color(0, 0, 0, 0)); // whole row clickable

            var box = Rect("Box", row);
            box.anchorMin = box.anchorMax = new Vector2(0, 0.5f);
            box.pivot = new Vector2(0, 0.5f);
            box.sizeDelta = new Vector2(24, 24);
            box.anchoredPosition = new Vector2(2, 0);
            var boxImg = Image(box, ButtonColor);

            var check = Rect("Check", box);
            Stretch(check, 5, 5, 5, 5);
            var checkImg = Image(check, Accent, false);

            var label = Label(row, text, 20, TextColor);
            Stretch(label.rectTransform, 38, 0, 0, 0);
            FitText(label, 20);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            var cb = toggle.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.selectedColor = Color.white;
            toggle.colors = cb;
            var nav = toggle.navigation;
            nav.mode = Navigation.Mode.None;
            toggle.navigation = nav;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v =>
            {
                try { onChange?.Invoke(v); }
                catch (Exception e) { Log.Error("UI action failed", e); }
            });
            return toggle;
        }

        public static Slider Slider(Transform parent, string text, float min, float max, float value,
            Func<float, string> format, Action<float> onChange, bool wholeNumbers = false)
        {
            // Everything is positioned by layout groups with centred pivots: TMP texts with a non-centred
            // pivot get wrongly culled inside a RectMask2D (scroll view).
            var row = Rect("Slider", parent);
            Layout(row.gameObject, 60);
            var v = row.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.padding = new RectOffset(2, 4, 0, 4);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var top = HRow(row, 30, 8);
            var label = Label(top.transform, text, 20, TextColor);
            Layout(label.gameObject, -1, -1, 1);
            var valueLabel = Label(top.transform, format(value), 20, Accent, TextAlignmentOptions.MidlineRight);
            Layout(valueLabel.gameObject, -1, 110, 0);

            var trackHolder = Rect("TrackHolder", row);
            Layout(trackHolder.gameObject, 20);
            var sliderRt = Rect("Track", trackHolder);
            Stretch(sliderRt, 10, 2, 10, 2);

            var bg = Rect("Background", sliderRt);
            Stretch(bg, 0, 3, 0, 3);
            Image(bg, ButtonColor);

            var fillArea = Rect("Fill Area", sliderRt);
            Stretch(fillArea, 0, 3, 0, 3);
            var fill = Rect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.sizeDelta = Vector2.zero;
            Image(fill, AccentDim, false);

            var handleArea = Rect("Handle Area", sliderRt);
            Stretch(handleArea);
            var handle = Rect("Handle", handleArea);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(18, 6);
            var handleImg = Image(handle, Accent);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.onValueChanged.AddListener(v =>
            {
                valueLabel.text = format(v);
                try { onChange?.Invoke(v); }
                catch (Exception e) { Log.Error("UI action failed", e); }
            });
            return slider;
        }

        /// <summary>Vertical scroll view; children added to <paramref name="content"/> are laid out top to bottom.</summary>
        public static ScrollRect ScrollView(Transform parent, out RectTransform content, float spacing = 6, int padding = 8)
        {
            var root = Rect("ScrollView", parent);
            Image(root, Surface);

            var viewport = Rect("Viewport", root);
            Stretch(viewport, 0, 0, 14, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Scrollbar
            var sbRt = Rect("Scrollbar", root);
            sbRt.anchorMin = new Vector2(1, 0);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 0.5f);
            sbRt.sizeDelta = new Vector2(10, 0);
            Image(sbRt, SurfaceAlt);
            var slideArea = Rect("Sliding Area", sbRt);
            Stretch(slideArea);
            var sbHandle = Rect("Handle", slideArea);
            Stretch(sbHandle);
            var sbHandleImg = Image(sbHandle, ButtonHover);
            var sb = sbRt.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = sbHandle;
            sb.targetGraphic = sbHandleImg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            var nav = sb.navigation;
            nav.mode = Navigation.Mode.None;
            sb.navigation = nav;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return scroll;
        }

        public static TextMeshProUGUI SectionHeader(Transform parent, string text)
        {
            var label = Label(parent, text, 22, Accent);
            label.fontStyle = FontStyles.Bold;
            Layout(label.gameObject, 36);
            return label;
        }
    }
}
