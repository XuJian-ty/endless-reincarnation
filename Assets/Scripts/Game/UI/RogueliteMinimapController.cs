using System.Collections.Generic;
using Game.GameFlow;
using Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class RogueliteMinimapController : MonoBehaviour
    {
        [SerializeField] [Min(20f)] private float _viewRadius = 52f;
        [SerializeField] [Min(40f)] private float _cameraHeight = 120f;
        [SerializeField] [Range(128, 1024)] private int _textureSize = 512;

        private const float MapDiameter = 244f;
        private const float MarkerRadius = 116f;

        private readonly List<MarkerBinding> _gateMarkers = new List<MarkerBinding>();
        private readonly List<MarkerBinding> _chestMarkers = new List<MarkerBinding>();
        private readonly List<RectTransform> _enemyMarkerPool = new List<RectTransform>();

        private Transform _player;
        private Camera _mapCamera;
        private RenderTexture _mapTexture;
        private RectTransform _markerRoot;
        private RectTransform _playerArrow;
        private Canvas _canvas;
        private Sprite _circleSprite;
        private Sprite _arrowSprite;
        private Texture2D _circleTexture;
        private Texture2D _arrowTexture;

        private sealed class MarkerBinding
        {
            public Component target;
            public RectTransform marker;
        }

        private void Start()
        {
            CreateMapCamera();
            CreateMapHud();
            CacheLandmarkMarkers();
        }

        private void LateUpdate()
        {
            _player = LocalPlayerInteractionResolver.ResolvePlayerTransform(gameObject.scene, _player);
            bool hasPlayer = _player != null;
            if (_canvas != null)
                _canvas.enabled = hasPlayer;
            if (!hasPlayer)
                return;

            Vector3 desiredCameraPosition = _player.position + Vector3.up * _cameraHeight;
            float followFactor = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            _mapCamera.transform.position = Vector3.Lerp(_mapCamera.transform.position, desiredCameraPosition, followFactor);
            _playerArrow.localEulerAngles = new Vector3(0f, 0f, -_player.eulerAngles.y);

            UpdateLandmarkMarkers(_gateMarkers, true);
            UpdateLandmarkMarkers(_chestMarkers, false);
            UpdateEnemyMarkers();
        }

        private void OnDestroy()
        {
            if (_mapTexture != null)
            {
                _mapTexture.Release();
                Destroy(_mapTexture);
            }

            Destroy(_circleSprite);
            Destroy(_arrowSprite);
            Destroy(_circleTexture);
            Destroy(_arrowTexture);
        }

        private void CreateMapCamera()
        {
            GameObject cameraObject = new GameObject("小地图俯视相机");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.SetPositionAndRotation(Vector3.up * _cameraHeight, Quaternion.Euler(90f, 0f, 0f));

            _mapCamera = cameraObject.AddComponent<Camera>();
            _mapCamera.orthographic = true;
            _mapCamera.orthographicSize = _viewRadius;
            _mapCamera.clearFlags = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor = new Color(0.025f, 0.045f, 0.065f, 1f);
            _mapCamera.nearClipPlane = 0.3f;
            _mapCamera.farClipPlane = _cameraHeight + 80f;
            _mapCamera.depth = -20f;
            _mapCamera.allowHDR = false;
            _mapCamera.allowMSAA = false;
            _mapCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));

            _mapTexture = new RenderTexture(_textureSize, _textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Level1_Minimap_RT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _mapTexture.Create();
            _mapCamera.targetTexture = _mapTexture;
        }

        private void CreateMapHud()
        {
            _circleSprite = CreateCircleSprite(256, 0f, out _circleTexture);
            _arrowSprite = CreateArrowSprite(out _arrowTexture);

            GameObject canvasObject = new GameObject("右上角小地图", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = CreateUiRect(canvasObject.transform, "小地图面板", new Vector2(286f, 314f));
            panel.anchorMin = Vector2.one;
            panel.anchorMax = Vector2.one;
            panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-26f, -24f);

            Text title = CreateText(panel, "地图标题", "遗迹探路图", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(12f, -36f);
            title.rectTransform.offsetMax = new Vector2(-12f, -6f);
            title.color = new Color(0.76f, 0.9f, 1f, 1f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleOutline.effectDistance = new Vector2(1f, -1f);

            RectTransform shadow = CreateUiRect(panel, "地图阴影", new Vector2(MapDiameter + 14f, MapDiameter + 14f));
            shadow.anchorMin = new Vector2(0.5f, 1f);
            shadow.anchorMax = new Vector2(0.5f, 1f);
            shadow.pivot = new Vector2(0.5f, 1f);
            shadow.anchoredPosition = new Vector2(4f, -41f);
            Image shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = _circleSprite;
            shadowImage.color = new Color(0f, 0f, 0f, 0.38f);
            shadowImage.raycastTarget = false;

            RectTransform viewport = CreateUiRect(panel, "地图圆形视窗", new Vector2(MapDiameter, MapDiameter));
            viewport.anchorMin = new Vector2(0.5f, 1f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = new Vector2(0f, -37f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = _circleSprite;
            viewportImage.color = new Color(0.035f, 0.06f, 0.085f, 1f);
            viewportImage.raycastTarget = false;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            RectTransform mapImageRect = CreateUiRect(viewport, "实时地图画面", Vector2.zero);
            StretchToParent(mapImageRect);
            RawImage mapImage = mapImageRect.gameObject.AddComponent<RawImage>();
            mapImage.texture = _mapTexture;
            mapImage.color = new Color(0.94f, 0.97f, 1f, 1f);
            mapImage.raycastTarget = false;

            _markerRoot = CreateUiRect(viewport, "地图标记", Vector2.zero);
            StretchToParent(_markerRoot);

            _playerArrow = CreateMarker(_markerRoot, "玩家方向", new Vector2(22f, 28f), new Color(0.92f, 1f, 1f, 1f), _arrowSprite);
            _playerArrow.anchoredPosition = Vector2.zero;

            Text north = CreateText(panel, "北向标记", "N", 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            north.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            north.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            north.rectTransform.pivot = new Vector2(0.5f, 1f);
            north.rectTransform.sizeDelta = new Vector2(28f, 24f);
            north.rectTransform.anchoredPosition = new Vector2(0f, -39f);
            north.color = new Color(0.88f, 0.96f, 1f, 1f);

            Text legend = CreateText(panel, "地图图例", "● 敌人    ◆ 宝箱    ▬ 封印门", 13, TextAnchor.MiddleCenter, FontStyle.Normal);
            legend.rectTransform.anchorMin = new Vector2(0f, 0f);
            legend.rectTransform.anchorMax = new Vector2(1f, 0f);
            legend.rectTransform.pivot = new Vector2(0.5f, 0f);
            legend.rectTransform.offsetMin = new Vector2(8f, 4f);
            legend.rectTransform.offsetMax = new Vector2(-8f, 30f);
            legend.color = new Color(0.7f, 0.78f, 0.86f, 0.9f);
            Outline legendOutline = legend.gameObject.AddComponent<Outline>();
            legendOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            legendOutline.effectDistance = new Vector2(1f, -1f);
        }

        private void CacheLandmarkMarkers()
        {
            RogueliteSealGate[] gates = FindObjectsByType<RogueliteSealGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                _gateMarkers.Add(new MarkerBinding
                {
                    target = gates[i],
                    marker = CreateMarker(_markerRoot, "封印门标记", new Vector2(16f, 5f), new Color(0.2f, 0.86f, 1f, 0.96f), null),
                });
            }

            ChestInteractable[] chests = FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                _chestMarkers.Add(new MarkerBinding
                {
                    target = chests[i],
                    marker = CreateMarker(_markerRoot, "宝箱标记", new Vector2(8f, 8f), new Color(1f, 0.72f, 0.16f, 1f), _circleSprite),
                });
            }
        }

        private void UpdateLandmarkMarkers(List<MarkerBinding> markers, bool requireSealedGate)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                MarkerBinding binding = markers[i];
                bool visible = binding.target != null && binding.target.gameObject.activeInHierarchy;
                if (visible && requireSealedGate)
                    visible = binding.target is RogueliteSealGate gate && gate.IsSealed;

                Vector2 mapPosition = Vector2.zero;
                bool showMarker = visible && TryProjectToMap(binding.target.transform.position, out mapPosition);
                binding.marker.gameObject.SetActive(showMarker);
                if (showMarker)
                    binding.marker.anchoredPosition = mapPosition;
            }
        }

        private void UpdateEnemyMarkers()
        {
            IReadOnlyList<EnemyController> enemies = EnemyController.ActiveEnemies;
            int markerIndex = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !TryProjectToMap(enemy.transform.position, out Vector2 mapPosition))
                    continue;

                RectTransform marker = GetEnemyMarker(markerIndex++);
                marker.anchoredPosition = mapPosition;
                marker.gameObject.SetActive(true);
            }

            for (int i = markerIndex; i < _enemyMarkerPool.Count; i++)
                _enemyMarkerPool[i].gameObject.SetActive(false);
        }

        private RectTransform GetEnemyMarker(int index)
        {
            while (_enemyMarkerPool.Count <= index)
            {
                RectTransform marker = CreateMarker(_markerRoot, "敌人标记", new Vector2(6f, 6f), new Color(1f, 0.24f, 0.18f, 0.96f), _circleSprite);
                _enemyMarkerPool.Add(marker);
            }
            return _enemyMarkerPool[index];
        }

        private bool TryProjectToMap(Vector3 worldPosition, out Vector2 mapPosition)
        {
            mapPosition = Vector2.zero;
            if (_player == null)
                return false;

            Vector3 delta = worldPosition - _player.position;
            mapPosition = new Vector2(delta.x, delta.z) * (MarkerRadius / _viewRadius);
            return mapPosition.sqrMagnitude <= MarkerRadius * MarkerRadius;
        }

        private static RectTransform CreateMarker(Transform parent, string name, Vector2 size, Color color, Sprite sprite)
        {
            RectTransform marker = CreateUiRect(parent, name, size);
            marker.anchorMin = new Vector2(0.5f, 0.5f);
            marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            Image image = marker.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return marker;
        }

        private static RectTransform CreateUiRect(Transform parent, string name, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment, FontStyle style)
        {
            RectTransform rect = CreateUiRect(parent, name, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite CreateCircleSprite(int size, float innerRadius, out Texture2D texture)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = innerRadius > 0f ? "MinimapRing" : "MinimapCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float outer = center - 1f;
            float inner = outer * innerRadius * 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01(outer - distance + 1f);
                    float innerAlpha = innerRadius > 0f ? Mathf.Clamp01(distance - inner + 1f) : 1f;
                    byte alpha = (byte)(Mathf.Clamp01(outerAlpha * innerAlpha) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateArrowSprite(out Texture2D texture)
        {
            const int width = 48;
            const int height = 64;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "MinimapPlayerArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float normalizedY = y / (height - 1f);
                float halfWidth = Mathf.Lerp(5f, 22f, 1f - normalizedY);
                for (int x = 0; x < width; x++)
                {
                    float distanceFromCenter = Mathf.Abs(x - (width - 1) * 0.5f);
                    byte alpha = distanceFromCenter <= halfWidth ? (byte)255 : (byte)0;
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.45f), height);
        }
    }
}
