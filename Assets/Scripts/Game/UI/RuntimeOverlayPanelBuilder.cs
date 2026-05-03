using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal static class RuntimeOverlayPanelBuilder
    {
        private static Font _defaultFont;

        private static Font DefaultFont => _defaultFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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

        public static InputField CreateInputField(
            Transform parent,
            string name,
            string placeholder,
            string defaultText,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor,
            Color textColor,
            bool isPassword = false)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = inputObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = true;

            InputField inputField = inputObject.GetComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.contentType = isPassword
                ? InputField.ContentType.Password
                : InputField.ContentType.Standard;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(inputObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);

            Text text = textObject.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = textColor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = defaultText ?? string.Empty;

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholderObject.transform.SetParent(inputObject.transform, false);
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(18f, 8f);
            placeholderRect.offsetMax = new Vector2(-18f, -8f);

            Text placeholderText = placeholderObject.GetComponent<Text>();
            placeholderText.font = DefaultFont;
            placeholderText.fontSize = 24;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
            placeholderText.supportRichText = false;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholderText.verticalOverflow = VerticalWrapMode.Truncate;
            placeholderText.text = placeholder ?? string.Empty;
            placeholderText.fontStyle = FontStyle.Italic;

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.text = defaultText ?? string.Empty;
            return inputField;
        }

        public static ScrollRect CreateScrollView(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor,
            out RectTransform contentRoot)
        {
            GameObject scrollObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);

            RectTransform rect = scrollObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = scrollObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = true;

            Mask mask = scrollObject.GetComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(scrollObject.transform, false);

            contentRoot = contentObject.GetComponent<RectTransform>();
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.offsetMin = new Vector2(10f, 0f);
            contentRoot.offsetMax = new Vector2(-10f, 0f);
            contentRoot.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 10, 10);

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = rect;
            scrollRect.content = contentRoot;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            return scrollRect;
        }
    }
}
