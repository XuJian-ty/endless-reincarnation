using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public static class PlayerAimCrosshairRuntime
    {
        private const float BaseCrosshairSize = 84f;
        private const int BaseCrosshairFontSize = 54;

        private static GameObject _rootObject;
        private static RectTransform _crosshairRectTransform;
        private static Text _crosshairText;
        private static GameObject _guideLineObject;
        private static LineRenderer _guideLineRenderer;
        private static Material _guideLineMaterial;
        private static float _currentZoomScale = 1f;

        public static void SetVisible(bool visible)
        {
            if (visible)
                EnsureCreated();

            if (_rootObject != null)
                _rootObject.SetActive(visible);
        }

        public static void SetZoomScale(float zoomScale)
        {
            _currentZoomScale = Mathf.Max(1f, zoomScale);
            ApplyCrosshairScale();
        }

        public static void SetAimGuide(Vector3 start, Vector3 end, bool visible)
        {
            if (!visible)
            {
                if (_guideLineObject != null)
                    _guideLineObject.SetActive(false);
                return;
            }

            EnsureGuideCreated();
            if (_guideLineObject == null || _guideLineRenderer == null)
                return;

            _guideLineObject.SetActive(true);
            _guideLineRenderer.SetPosition(0, start);
            _guideLineRenderer.SetPosition(1, end);
        }

        private static void EnsureCreated()
        {
            if (_rootObject != null && _crosshairText != null)
                return;

            _rootObject = new GameObject("PlayerAimCrosshairCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_rootObject);

            Canvas canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4096;

            CanvasScaler scaler = _rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject textObject = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(_rootObject.transform, false);

            _crosshairRectTransform = textObject.GetComponent<RectTransform>();
            _crosshairRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _crosshairRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshairRectTransform.pivot = new Vector2(0.5f, 0.5f);
            _crosshairRectTransform.anchoredPosition = Vector2.zero;
            _crosshairRectTransform.sizeDelta = new Vector2(BaseCrosshairSize, BaseCrosshairSize);

            _crosshairText = textObject.GetComponent<Text>();
            _crosshairText.text = "+";
            _crosshairText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _crosshairText.fontSize = BaseCrosshairFontSize;
            _crosshairText.fontStyle = FontStyle.Bold;
            _crosshairText.alignment = TextAnchor.MiddleCenter;
            _crosshairText.color = new Color(1f, 0.12f, 0.12f, 0.98f);
            _crosshairText.raycastTarget = false;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            ApplyCrosshairScale();
            _rootObject.SetActive(false);
        }

        private static void EnsureGuideCreated()
        {
            if (_guideLineObject != null && _guideLineRenderer != null)
                return;

            _guideLineObject = new GameObject("PlayerAimGuideLine", typeof(LineRenderer));
            Object.DontDestroyOnLoad(_guideLineObject);

            _guideLineRenderer = _guideLineObject.GetComponent<LineRenderer>();
            _guideLineRenderer.useWorldSpace = true;
            _guideLineRenderer.positionCount = 2;
            _guideLineRenderer.alignment = LineAlignment.View;
            _guideLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _guideLineRenderer.receiveShadows = false;
            _guideLineRenderer.textureMode = LineTextureMode.Stretch;
            _guideLineRenderer.numCapVertices = 4;
            _guideLineRenderer.startWidth = 0.04f;
            _guideLineRenderer.endWidth = 0.02f;
            _guideLineRenderer.startColor = new Color(1f, 0.12f, 0.12f, 0.98f);
            _guideLineRenderer.endColor = new Color(1f, 0.12f, 0.12f, 0.3f);
            _guideLineRenderer.sortingOrder = 4096;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            if (shader != null)
            {
                _guideLineMaterial = new Material(shader)
                {
                    color = new Color(1f, 0.12f, 0.12f, 0.95f)
                };
                _guideLineRenderer.material = _guideLineMaterial;
            }

            _guideLineObject.SetActive(false);
        }

        private static void ApplyCrosshairScale()
        {
            if (_crosshairRectTransform == null || _crosshairText == null)
                return;

            float size = BaseCrosshairSize * _currentZoomScale;
            _crosshairRectTransform.sizeDelta = new Vector2(size, size);
            _crosshairText.fontSize = Mathf.RoundToInt(BaseCrosshairFontSize * _currentZoomScale);
        }
    }
}
