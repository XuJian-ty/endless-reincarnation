using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public static class PlayerAimCrosshairRuntime
    {
        private static GameObject _rootObject;
        private static Text _crosshairText;
        private static GameObject _guideLineObject;
        private static LineRenderer _guideLineRenderer;
        private static Material _guideLineMaterial;

        public static void SetVisible(bool visible)
        {
            if (visible)
                EnsureCreated();

            if (_rootObject != null)
                _rootObject.SetActive(visible);
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

            GameObject textObject = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(_rootObject.transform, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(48f, 48f);

            _crosshairText = textObject.GetComponent<Text>();
            _crosshairText.text = "+";
            _crosshairText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _crosshairText.fontSize = 28;
            _crosshairText.alignment = TextAnchor.MiddleCenter;
            _crosshairText.color = new Color(1f, 0.96f, 0.86f, 0.95f);
            _crosshairText.raycastTarget = false;

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
            _guideLineRenderer.startWidth = 0.03f;
            _guideLineRenderer.endWidth = 0.015f;
            _guideLineRenderer.startColor = new Color(1f, 0.18f, 0.12f, 0.95f);
            _guideLineRenderer.endColor = new Color(1f, 0.18f, 0.12f, 0.25f);
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
                    color = new Color(1f, 0.18f, 0.12f, 0.9f)
                };
                _guideLineRenderer.material = _guideLineMaterial;
            }

            _guideLineObject.SetActive(false);
        }
    }
}
