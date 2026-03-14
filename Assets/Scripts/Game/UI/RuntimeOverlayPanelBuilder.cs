using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal static class RuntimeOverlayPanelBuilder
    {
        private static Font _defaultFont;

        private static Font DefaultFont => _defaultFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");

        public static void EnsureFullScreenRoot(RectTransform root)
        {
            if (root == null)
                return;

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            root.localPosition = Vector3.zero;
        }

        public static Image EnsureOverlayBackground(GameObject root, Color color)
        {
            if (root == null)
                return null;

            var image = root.GetComponent<Image>();
            if (image == null)
                image = root.AddComponent<Image>();

            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static RectTransform CreateWindow(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject window = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            window.transform.SetParent(parent, false);

            RectTransform rect = window.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = window.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Text text = textObject.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = fontStyle;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color buttonColor,
            Color textColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = buttonColor;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            CreateText(
                buttonObject.transform,
                "Text",
                label,
                24,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                size,
                textColor,
                FontStyle.Bold);

            return button;
        }
    }
}
