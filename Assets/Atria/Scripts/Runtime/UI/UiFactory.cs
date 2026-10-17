using UnityEngine;
using UnityEngine.UI;

namespace Atria.App.UI
{
    /// <summary>Code-only uGUI construction helpers (no prefabs, no imported fonts or sprites).</summary>
    public static class UiFactory
    {
        public static readonly Color Background = new Color32(244, 246, 248, 255);
        public static readonly Color Surface = new Color32(255, 255, 255, 255);
        public static readonly Color TextPrimary = new Color32(28, 32, 38, 255);
        public static readonly Color TextSecondary = new Color32(96, 104, 116, 255);
        public static readonly Color Accent = new Color32(30, 104, 210, 255);
        public static readonly Color AccentText = Color.white;
        public static readonly Color Warning = new Color32(255, 214, 102, 255);
        public static readonly Color Error = new Color32(190, 40, 40, 255);
        public static readonly Color Selected = new Color32(214, 230, 252, 255);
        public static readonly Color Overlay = new Color32(255, 255, 255, 235);

        public static Font Font;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, int fontSize, Color background, Color textColor, out Text label)
        {
            Image img = Panel(name, parent, background);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            button.colors = colors;
            label = Label("Label", img.transform, text, fontSize, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 24, 24, 0, 0);
            return button;
        }

        public static LayoutElement Size(Component c, float preferredHeight = -1, float flexibleHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            LayoutElement le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup Vertical(Component c, int padding, float spacing, bool controlHeight = true)
        {
            var g = c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.spacing = spacing;
            g.childControlWidth = true;
            g.childControlHeight = controlHeight;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup Horizontal(Component c, int padding, float spacing)
        {
            var g = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.spacing = spacing;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = true;
            return g;
        }

        public static InputField Input(string name, Transform parent, string placeholder, int fontSize)
        {
            Image bg = Panel(name, parent, Surface);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(180, 188, 200, 255);
            outline.effectDistance = new Vector2(2, -2);
            var field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;

            Text text = Label("Text", bg.transform, "", fontSize, TextPrimary);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text.rectTransform, 32, 32, 8, 8);
            Text ph = Label("Placeholder", bg.transform, placeholder, fontSize, TextSecondary, FontStyle.Italic);
            ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(ph.rectTransform, 32, 32, 8, 8);

            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = InputField.ContentType.Standard;
            field.characterLimit = 32;
            field.caretWidth = 3;
            return field;
        }

        public static Slider Slider(string name, Transform parent, float handleWidth)
        {
            RectTransform root = Rect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();

            Image track = Panel("Track", root, new Color32(200, 206, 214, 255));
            Stretch(track.rectTransform);
            track.rectTransform.anchorMin = new Vector2(0, 0.38f);
            track.rectTransform.anchorMax = new Vector2(1, 0.62f);
            track.rectTransform.offsetMin = new Vector2(handleWidth * 0.5f, 0);
            track.rectTransform.offsetMax = new Vector2(-handleWidth * 0.5f, 0);

            RectTransform fillArea = Rect("Fill Area", root);
            Stretch(fillArea);
            fillArea.anchorMin = new Vector2(0, 0.38f);
            fillArea.anchorMax = new Vector2(1, 0.62f);
            fillArea.offsetMin = new Vector2(handleWidth * 0.5f, 0);
            fillArea.offsetMax = new Vector2(-handleWidth * 0.5f, 0);
            Image fill = Panel("Fill", fillArea, Accent);
            Stretch(fill.rectTransform);

            RectTransform handleArea = Rect("Handle Slide Area", root);
            Stretch(handleArea, handleWidth * 0.5f, handleWidth * 0.5f, 0, 0);
            Image handle = Panel("Handle", handleArea, Accent);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(handleWidth, 0);
            var handleOutline = handle.gameObject.AddComponent<Outline>();
            handleOutline.effectColor = Color.white;
            handleOutline.effectDistance = new Vector2(4, -4);

            // The whole slider area (not just the handle) receives touches: a transparent hit target.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.transition = Selectable.Transition.None;
            return slider;
        }
    }
}
