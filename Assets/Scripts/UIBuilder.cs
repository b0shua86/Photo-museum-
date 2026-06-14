// Runtime uGUI factory. Builds the whole 2D interface in code (no prefabs to
// hand-author), using the shared runtime font so all text renders without any
// editor import step. Small, composable helpers keep MuseumUI readable.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CameraObscura
{
    public static class UIBuilder
    {
        public static readonly Color Ink = new Color(0.93f, 0.91f, 0.86f);
        public static readonly Color Sub = new Color(0.78f, 0.74f, 0.66f);
        public static readonly Color Panel = new Color(0.08f, 0.075f, 0.07f, 0.92f);
        public static readonly Color Scrim = new Color(0.03f, 0.03f, 0.04f, 0.86f);
        public static readonly Color Chip = new Color(0.16f, 0.15f, 0.13f, 0.95f);

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Rect(parent, "label");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (UIText.Font != null) t.font = UIText.Font;
            t.text = text ?? "";
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = false;
            t.enableWordWrapping = true;
            return t;
        }

        public static Button Button(Transform parent, string text, Color bg, System.Action onClick, float fontSize = 22)
        {
            var rt = Rect(parent, "button");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var label = Label(rt, text, fontSize, Ink, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 6, 2, 6, 2);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var colors = btn.colors; colors.highlightedColor = new Color(bg.r + 0.12f, bg.g + 0.12f, bg.b + 0.12f, Mathf.Max(bg.a, 0.95f));
            colors.pressedColor = new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f, 1f);
            btn.colors = colors;
            return btn;
        }

        /// <summary>A vertical scrolling list; returns the content RectTransform to fill.</summary>
        public static RectTransform ScrollList(Transform parent, out ScrollRect scroll)
        {
            var viewportRt = Rect(parent, "viewport");
            var vpImg = viewportRt.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.001f);
            var mask = viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentRt = Rect(viewportRt, "content");
            contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            var vlg = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = contentRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = viewportRt.gameObject.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewportRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return contentRt;
        }

        public static LayoutElement Height(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            return le;
        }

        public static TMP_InputField Input(Transform parent, string placeholder)
        {
            var rt = Rect(parent, "input");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            var field = rt.gameObject.AddComponent<TMP_InputField>();

            var textArea = Rect(rt, "textarea");
            Stretch(textArea, 12, 6, 12, 6);
            var ph = Label(textArea, placeholder, 26, new Color(0.6f, 0.58f, 0.54f), TextAlignmentOptions.Left);
            Stretch(ph.rectTransform);
            var txt = Label(textArea, "", 26, Ink, TextAlignmentOptions.Left);
            Stretch(txt.rectTransform);

            field.textViewport = textArea;
            field.textComponent = txt;
            field.placeholder = ph;
            field.fontAsset = UIText.Font;
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }
    }
}
