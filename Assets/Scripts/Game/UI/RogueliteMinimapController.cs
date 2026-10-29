using System.Collections.Generic;
using Game.GameFlow;
using Game.Input;
using Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float DefaultExpandedViewRadius = 180f;
        private const float MinimumExpandedViewRadius = 55f;
        private const float MaximumExpandedViewRadius = 360f;
        private const float ExpandedViewportSize = 760f;
        private const float ExpandedMarkerRadius = 374f;
        private const float ExpandedPanLimit = 650f;
        private const int CompactSortingOrder = -1;
        private const int ExpandedSortingOrder = 32000;

        private readonly List<MarkerBinding> _gateMarkers = new List<MarkerBinding>();
        private readonly List<MarkerBinding> _chestMarkers = new List<MarkerBinding>();
        private readonly List<RectTransform> _enemyMarkerPool = new List<RectTransform>();
        private readonly List<RectTransform> _expandedEnemyMarkerPool = new List<RectTransform>();

        private Transform _player;
        private Camera _mapCamera;
        private Camera _expandedMapCamera;
        private RenderTexture _mapTexture;
        private RenderTexture _expandedMapTexture;
        private RectTransform _compactPanel;
        private RectTransform _markerRoot;
        private RectTransform _playerArrow;
        private GameObject _expandedRoot;
        private RectTransform _expandedViewport;
        private RectTransform _expandedMarkerRoot;
        private RectTransform _expandedPlayerArrow;
        private Text _expandedCoordinateText;
        private Canvas _canvas;
        private Sprite _circleSprite;
        private Sprite _ringSprite;
        private Sprite _thinRingSprite;
        private Sprite _arrowSprite;
        private Texture2D _circleTexture;
        private Texture2D _ringTexture;
        private Texture2D _thinRingTexture;
        private Texture2D _arrowTexture;
        private Vector3 _expandedMapCenter;
        private float _expandedViewRadius = DefaultExpandedViewRadius;
        private bool _expandedMapOpen;
        private bool _cursorWasVisible;
        private CursorLockMode _previousCursorLockMode;
        private GameplayInputEvents _gameplayInputEvents;

        private sealed class MarkerBinding
        {
            public Component target;
            public RectTransform compactMarker;
            public RectTransform expandedMarker;
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

            if (_expandedMapOpen)
            {
                _expandedMapCenter.y = _player.position.y;
                _expandedMapCamera.transform.position = _expandedMapCenter + Vector3.up * _cameraHeight;
                _expandedPlayerArrow.localEulerAngles = new Vector3(0f, 0f, -_player.eulerAngles.y);
                if (_expandedCoordinateText != null)
                {
                    _expandedCoordinateText.text = $"X {_expandedMapCenter.x:0}   Z {_expandedMapCenter.z:0}";
                }
            }

            UpdateLandmarkMarkers(_gateMarkers, true);
            UpdateLandmarkMarkers(_chestMarkers, false);
            UpdateEnemyMarkers();
        }

        private void OnDestroy()
        {
            if (_expandedMapOpen)
                RestoreGameplayInput();

            ReleaseRenderTexture(_mapTexture);
            ReleaseRenderTexture(_expandedMapTexture);

            Destroy(_circleSprite);
            Destroy(_ringSprite);
            Destroy(_thinRingSprite);
            Destroy(_arrowSprite);
            Destroy(_circleTexture);
            Destroy(_ringTexture);
            Destroy(_thinRingTexture);
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

            _mapTexture = CreateRenderTexture("Level1_Minimap_RT", _textureSize);
            _mapCamera.targetTexture = _mapTexture;

            GameObject expandedCameraObject = new GameObject("展开地图俯视相机");
            expandedCameraObject.transform.SetParent(transform, false);
            expandedCameraObject.transform.SetPositionAndRotation(Vector3.up * _cameraHeight, Quaternion.Euler(90f, 0f, 0f));

            _expandedMapCamera = expandedCameraObject.AddComponent<Camera>();
            _expandedMapCamera.CopyFrom(_mapCamera);
            _expandedMapCamera.orthographicSize = _expandedViewRadius;
            _expandedMapTexture = CreateRenderTexture("Level1_ExpandedMap_RT", Mathf.Clamp(_textureSize * 2, 512, 1024));
            _expandedMapCamera.targetTexture = _expandedMapTexture;
            _expandedMapCamera.enabled = false;
        }

        private void CreateMapHud()
        {
            _circleSprite = CreateCircleSprite(256, 0f, out _circleTexture);
            _ringSprite = CreateCircleSprite(256, 0.43f, out _ringTexture);
            _thinRingSprite = CreateCircleSprite(256, 0.485f, out _thinRingTexture);
            _arrowSprite = CreateArrowSprite(out _arrowTexture);

            GameObject canvasObject = new GameObject(
                "右上角小地图",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CompactSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _compactPanel = CreateUiRect(canvasObject.transform, "小地图面板", new Vector2(304f, 338f));
            _compactPanel.anchorMin = Vector2.one;
            _compactPanel.anchorMax = Vector2.one;
            _compactPanel.pivot = Vector2.one;
            _compactPanel.anchoredPosition = new Vector2(-22f, -18f);

            Text title = CreateText(_compactPanel, "地图标题", "遗迹探索图", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(52f, -38f);
            title.rectTransform.offsetMax = new Vector2(-52f, -8f);
            title.color = new Color(0.82f, 0.94f, 1f, 1f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleOutline.effectDistance = new Vector2(1f, -1f);

            CreateHeaderLine(_compactPanel, "左侧标题线", new Vector2(-91f, -23f));
            CreateHeaderLine(_compactPanel, "右侧标题线", new Vector2(91f, -23f));

            const float mapCenterY = -177f;
            CreateCircleImage(_compactPanel, "地图阴影", 276f, new Vector2(5f, mapCenterY - 5f), new Color(0f, 0f, 0f, 0.48f), _circleSprite);
            CreateCircleImage(_compactPanel, "地图外框底色", 270f, new Vector2(0f, mapCenterY), new Color(0.025f, 0.045f, 0.06f, 0.98f), _circleSprite);
            CreateCircleImage(_compactPanel, "地图金属外环", 266f, new Vector2(0f, mapCenterY), new Color(0.42f, 0.62f, 0.7f, 0.95f), _ringSprite);
            CreateCircleImage(_compactPanel, "地图高光细环", 254f, new Vector2(0f, mapCenterY), new Color(0.38f, 0.9f, 1f, 0.9f), _thinRingSprite);

            RectTransform viewport = CreateUiRect(_compactPanel, "地图圆形视窗", new Vector2(MapDiameter, MapDiameter));
            AnchorAtTopCenter(viewport, new Vector2(0f, mapCenterY));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = _circleSprite;
            viewportImage.color = new Color(0.025f, 0.05f, 0.07f, 1f);
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

            CreateCompassTicks(_compactPanel, mapCenterY);

            Text north = CreateText(_compactPanel, "北向标记", "N", 15, TextAnchor.MiddleCenter, FontStyle.Bold);
            north.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            north.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            north.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            north.rectTransform.sizeDelta = new Vector2(28f, 22f);
            north.rectTransform.anchoredPosition = new Vector2(0f, mapCenterY + 123f);
            north.color = new Color(0.9f, 0.98f, 1f, 1f);

            RectTransform interactionRect = CreateUiRect(viewport, "小地图点击层", Vector2.zero);
            StretchToParent(interactionRect);
            Image interactionImage = interactionRect.gameObject.AddComponent<Image>();
            interactionImage.sprite = _circleSprite;
            interactionImage.color = new Color(0.45f, 0.92f, 1f, 0f);
            Button openButton = interactionRect.gameObject.AddComponent<Button>();
            openButton.targetGraphic = interactionImage;
            openButton.navigation = new Navigation { mode = Navigation.Mode.None };
            SetButtonColors(openButton, new Color(1f, 1f, 1f, 0f), new Color(0.5f, 0.94f, 1f, 0.08f), new Color(0.5f, 0.94f, 1f, 0.15f));
            openButton.onClick.AddListener(OpenExpandedMap);

            RectTransform legendBar = CreateUiRect(_compactPanel, "地图图例底条", new Vector2(270f, 30f));
            legendBar.anchorMin = new Vector2(0.5f, 0f);
            legendBar.anchorMax = new Vector2(0.5f, 0f);
            legendBar.pivot = new Vector2(0.5f, 0f);
            legendBar.anchoredPosition = new Vector2(0f, 2f);
            Image legendBackground = legendBar.gameObject.AddComponent<Image>();
            legendBackground.color = new Color(0.015f, 0.03f, 0.045f, 0.72f);
            legendBackground.raycastTarget = false;
            CreateLegendItem(legendBar, "敌人图例", "敌人", -89f, new Color(1f, 0.24f, 0.18f, 1f));
            CreateLegendItem(legendBar, "宝箱图例", "宝箱", 0f, new Color(1f, 0.72f, 0.16f, 1f));
            CreateLegendItem(legendBar, "封印门图例", "封印门", 91f, new Color(0.2f, 0.86f, 1f, 1f));

            CreateExpandedMapHud(canvasObject.transform);
        }

        private void CreateExpandedMapHud(Transform canvasTransform)
        {
            RectTransform expandedRootRect = CreateUiRect(canvasTransform, "展开地图", Vector2.zero);
            StretchToParent(expandedRootRect);
            _expandedRoot = expandedRootRect.gameObject;

            Image backdrop = expandedRootRect.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.005f, 0.012f, 0.018f, 0.9f);
            Button backdropButton = expandedRootRect.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropButton.onClick.AddListener(CloseExpandedMap);

            Text title = CreateText(expandedRootRect, "展开地图标题", "区域地图", 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(300f, 46f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 468f);
            title.color = new Color(0.82f, 0.94f, 1f, 1f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            titleOutline.effectDistance = new Vector2(1f, -2f);

            RectTransform frameShadow = CreateUiRect(expandedRootRect, "展开地图阴影", new Vector2(840f, 840f));
            AnchorAtCenter(frameShadow, new Vector2(8f, -10f));
            Image frameShadowImage = frameShadow.gameObject.AddComponent<Image>();
            frameShadowImage.color = new Color(0f, 0f, 0f, 0.5f);
            frameShadowImage.raycastTarget = false;

            RectTransform frame = CreateUiRect(expandedRootRect, "展开地图边框", new Vector2(832f, 832f));
            AnchorAtCenter(frame, Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.025f, 0.055f, 0.075f, 0.99f);
            frameImage.raycastTarget = false;
            Outline frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.32f, 0.78f, 0.88f, 0.9f);
            frameOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform innerFrame = CreateUiRect(frame, "展开地图内框", new Vector2(780f, 780f));
            AnchorAtCenter(innerFrame, Vector2.zero);
            Image innerFrameImage = innerFrame.gameObject.AddComponent<Image>();
            innerFrameImage.color = new Color(0.18f, 0.52f, 0.62f, 0.7f);
            innerFrameImage.raycastTarget = false;

            _expandedViewport = CreateUiRect(frame, "展开地图视窗", new Vector2(ExpandedViewportSize, ExpandedViewportSize));
            AnchorAtCenter(_expandedViewport, Vector2.zero);
            Image viewportImage = _expandedViewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.02f, 0.045f, 0.06f, 1f);
            viewportImage.raycastTarget = false;
            _expandedViewport.gameObject.AddComponent<RectMask2D>();

            RectTransform mapImageRect = CreateUiRect(_expandedViewport, "展开地图画面", Vector2.zero);
            StretchToParent(mapImageRect);
            RawImage mapImage = mapImageRect.gameObject.AddComponent<RawImage>();
            mapImage.texture = _expandedMapTexture;
            mapImage.color = new Color(0.95f, 0.98f, 1f, 1f);
            mapImage.raycastTarget = false;

            CreateExpandedMapGrid(_expandedViewport);

            _expandedMarkerRoot = CreateUiRect(_expandedViewport, "展开地图标记", Vector2.zero);
            StretchToParent(_expandedMarkerRoot);
            _expandedPlayerArrow = CreateMarker(
                _expandedMarkerRoot,
                "展开地图玩家方向",
                new Vector2(24f, 31f),
                new Color(0.92f, 1f, 1f, 1f),
                _arrowSprite);

            RectTransform dragRect = CreateUiRect(_expandedViewport, "展开地图拖动层", Vector2.zero);
            StretchToParent(dragRect);
            Image dragImage = dragRect.gameObject.AddComponent<Image>();
            dragImage.color = new Color(1f, 1f, 1f, 0f);
            Button dragButton = dragRect.gameObject.AddComponent<Button>();
            dragButton.targetGraphic = dragImage;
            dragButton.transition = Selectable.Transition.None;
            dragButton.navigation = new Navigation { mode = Navigation.Mode.None };
            ExpandedMapDragSurface dragSurface = dragRect.gameObject.AddComponent<ExpandedMapDragSurface>();
            dragSurface.Initialize(this);

            CreateCompassLabel(frame, "北向", "N", new Vector2(0f, 397f));
            CreateCompassLabel(frame, "东向", "E", new Vector2(397f, 0f));
            CreateCompassLabel(frame, "南向", "S", new Vector2(0f, -397f));
            CreateCompassLabel(frame, "西向", "W", new Vector2(-397f, 0f));

            _expandedCoordinateText = CreateText(frame, "地图坐标", string.Empty, 14, TextAnchor.MiddleLeft, FontStyle.Normal);
            _expandedCoordinateText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _expandedCoordinateText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _expandedCoordinateText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _expandedCoordinateText.rectTransform.sizeDelta = new Vector2(180f, 30f);
            _expandedCoordinateText.rectTransform.anchoredPosition = new Vector2(-376f, 397f);
            _expandedCoordinateText.color = new Color(0.66f, 0.8f, 0.86f, 0.95f);

            Button closeButton = CreateTextButton(frame, "关闭地图", "X", new Vector2(42f, 42f), new Vector2(391f, 391f), 20);
            closeButton.onClick.AddListener(CloseExpandedMap);

            Button locateButton = CreateTextButton(frame, "定位玩家", "定位", new Vector2(84f, 36f), new Vector2(350f, -397f), 15);
            locateButton.onClick.AddListener(CenterExpandedMapOnPlayer);

            RectTransform legendBar = CreateUiRect(frame, "展开地图图例", new Vector2(286f, 32f));
            AnchorAtCenter(legendBar, new Vector2(0f, -397f));
            Image legendBackground = legendBar.gameObject.AddComponent<Image>();
            legendBackground.color = new Color(0.01f, 0.025f, 0.035f, 0.86f);
            legendBackground.raycastTarget = false;
            CreateLegendItem(legendBar, "展开敌人图例", "敌人", -94f, new Color(1f, 0.24f, 0.18f, 1f));
            CreateLegendItem(legendBar, "展开宝箱图例", "宝箱", 0f, new Color(1f, 0.72f, 0.16f, 1f));
            CreateLegendItem(legendBar, "展开封印门图例", "封印门", 96f, new Color(0.2f, 0.86f, 1f, 1f));

            _expandedRoot.SetActive(false);
        }

        private void OpenExpandedMap()
        {
            if (_expandedMapOpen || _player == null)
                return;

            _expandedMapOpen = true;
            _expandedMapCenter = _player.position;
            _expandedViewRadius = DefaultExpandedViewRadius;
            _expandedMapCamera.orthographicSize = _expandedViewRadius;
            _compactPanel.gameObject.SetActive(false);
            _expandedRoot.SetActive(true);
            _canvas.sortingOrder = ExpandedSortingOrder;
            _mapCamera.enabled = false;
            _expandedMapCamera.enabled = true;

            _cursorWasVisible = Cursor.visible;
            _previousCursorLockMode = Cursor.lockState;
            CursorVisibilityController.ShowCursor();

            _gameplayInputEvents = FindFirstObjectByType<GameplayInputEvents>();
            _gameplayInputEvents?.SetGameplayMapEnabled(false);
        }

        private void CloseExpandedMap()
        {
            if (!_expandedMapOpen)
                return;

            _expandedMapOpen = false;
            _expandedRoot.SetActive(false);
            _compactPanel.gameObject.SetActive(true);
            _canvas.sortingOrder = CompactSortingOrder;
            _expandedMapCamera.enabled = false;
            _mapCamera.enabled = true;
            RestoreGameplayInput();
        }

        private void CenterExpandedMapOnPlayer()
        {
            if (_player != null)
                _expandedMapCenter = _player.position;
        }

        internal void PanExpandedMap(Vector2 screenDelta)
        {
            if (!_expandedMapOpen || _player == null || _expandedViewport == null)
                return;

            float viewportPixelWidth = Mathf.Max(1f, _expandedViewport.rect.width * _canvas.scaleFactor);
            float worldUnitsPerPixel = _expandedViewRadius * 2f / viewportPixelWidth;
            _expandedMapCenter.x -= screenDelta.x * worldUnitsPerPixel;
            _expandedMapCenter.z -= screenDelta.y * worldUnitsPerPixel;

            Vector2 offset = new Vector2(
                _expandedMapCenter.x - _player.position.x,
                _expandedMapCenter.z - _player.position.z);
            offset = Vector2.ClampMagnitude(offset, ExpandedPanLimit);
            _expandedMapCenter.x = _player.position.x + offset.x;
            _expandedMapCenter.z = _player.position.z + offset.y;
        }

        internal void ZoomExpandedMap(float zoomFactor)
        {
            if (!_expandedMapOpen || _expandedMapCamera == null || zoomFactor <= 0f)
                return;

            _expandedViewRadius = Mathf.Clamp(
                _expandedViewRadius / zoomFactor,
                MinimumExpandedViewRadius,
                MaximumExpandedViewRadius);
            _expandedMapCamera.orthographicSize = _expandedViewRadius;
        }

        private void RestoreGameplayInput()
        {
            _gameplayInputEvents?.SetGameplayMapEnabled(!GameplayUIInputBridge.IsAnyGameplayPanelOpen());
            _gameplayInputEvents = null;
            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _previousCursorLockMode;
        }

        private void CacheLandmarkMarkers()
        {
            RogueliteSealGate[] gates = FindObjectsByType<RogueliteSealGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                _gateMarkers.Add(new MarkerBinding
                {
                    target = gates[i],
                    compactMarker = CreateMarker(_markerRoot, "封印门标记", new Vector2(16f, 5f), new Color(0.2f, 0.86f, 1f, 0.96f), null),
                    expandedMarker = CreateMarker(_expandedMarkerRoot, "展开地图封印门标记", new Vector2(19f, 6f), new Color(0.2f, 0.86f, 1f, 0.96f), null),
                });
            }

            ChestInteractable[] chests = FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                _chestMarkers.Add(new MarkerBinding
                {
                    target = chests[i],
                    compactMarker = CreateMarker(_markerRoot, "宝箱标记", new Vector2(8f, 8f), new Color(1f, 0.72f, 0.16f, 1f), _circleSprite),
                    expandedMarker = CreateMarker(_expandedMarkerRoot, "展开地图宝箱标记", new Vector2(10f, 10f), new Color(1f, 0.72f, 0.16f, 1f), _circleSprite),
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

                Vector3 worldPosition = visible ? binding.target.transform.position : Vector3.zero;
                UpdateProjectedMarker(binding.compactMarker, visible, worldPosition, _player.position, _viewRadius, MarkerRadius, true);
                UpdateProjectedMarker(
                    binding.expandedMarker,
                    visible && _expandedMapOpen,
                    worldPosition,
                    _expandedMapCenter,
                    _expandedViewRadius,
                    ExpandedMarkerRadius,
                    false);
            }
        }

        private void UpdateEnemyMarkers()
        {
            IReadOnlyList<EnemyController> enemies = EnemyController.ActiveEnemies;
            int compactMarkerIndex = 0;
            int expandedMarkerIndex = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                Vector3 worldPosition = enemy.transform.position;
                if (TryProjectToMap(worldPosition, _player.position, _viewRadius, MarkerRadius, true, out Vector2 mapPosition))
                {
                    RectTransform marker = GetEnemyMarker(_enemyMarkerPool, _markerRoot, compactMarkerIndex++, false);
                    marker.anchoredPosition = mapPosition;
                    marker.gameObject.SetActive(true);
                }

                if (_expandedMapOpen && TryProjectToMap(worldPosition, _expandedMapCenter, _expandedViewRadius, ExpandedMarkerRadius, false, out Vector2 expandedPosition))
                {
                    RectTransform marker = GetEnemyMarker(_expandedEnemyMarkerPool, _expandedMarkerRoot, expandedMarkerIndex++, true);
                    marker.anchoredPosition = expandedPosition;
                    marker.gameObject.SetActive(true);
                }
            }

            for (int i = compactMarkerIndex; i < _enemyMarkerPool.Count; i++)
                _enemyMarkerPool[i].gameObject.SetActive(false);
            for (int i = expandedMarkerIndex; i < _expandedEnemyMarkerPool.Count; i++)
                _expandedEnemyMarkerPool[i].gameObject.SetActive(false);

            UpdateProjectedMarker(
                _expandedPlayerArrow,
                _expandedMapOpen,
                _player.position,
                _expandedMapCenter,
                _expandedViewRadius,
                ExpandedMarkerRadius,
                false);
        }

        private RectTransform GetEnemyMarker(List<RectTransform> pool, Transform root, int index, bool expanded)
        {
            while (pool.Count <= index)
            {
                RectTransform marker = CreateMarker(
                    root,
                    expanded ? "展开地图敌人标记" : "敌人标记",
                    expanded ? new Vector2(8f, 8f) : new Vector2(6f, 6f),
                    new Color(1f, 0.24f, 0.18f, 0.96f),
                    _circleSprite);
                pool.Add(marker);
            }
            return pool[index];
        }

        private static void UpdateProjectedMarker(
            RectTransform marker,
            bool visible,
            Vector3 worldPosition,
            Vector3 center,
            float viewRadius,
            float markerRadius,
            bool circular)
        {
            Vector2 mapPosition = Vector2.zero;
            bool showMarker = visible && TryProjectToMap(worldPosition, center, viewRadius, markerRadius, circular, out mapPosition);
            marker.gameObject.SetActive(showMarker);
            if (showMarker)
                marker.anchoredPosition = mapPosition;
        }

        private static bool TryProjectToMap(
            Vector3 worldPosition,
            Vector3 center,
            float viewRadius,
            float markerRadius,
            bool circular,
            out Vector2 mapPosition)
        {
            Vector3 delta = worldPosition - center;
            mapPosition = new Vector2(delta.x, delta.z) * (markerRadius / viewRadius);
            return circular
                ? mapPosition.sqrMagnitude <= markerRadius * markerRadius
                : Mathf.Abs(mapPosition.x) <= markerRadius && Mathf.Abs(mapPosition.y) <= markerRadius;
        }

        private void CreateCompassTicks(Transform parent, float centerY)
        {
            RectTransform tickRoot = CreateUiRect(parent, "罗盘刻度", Vector2.zero);
            tickRoot.anchorMin = new Vector2(0.5f, 1f);
            tickRoot.anchorMax = new Vector2(0.5f, 1f);
            tickRoot.pivot = new Vector2(0.5f, 0.5f);
            tickRoot.anchoredPosition = new Vector2(0f, centerY);

            const int tickCount = 24;
            for (int i = 0; i < tickCount; i++)
            {
                bool major = i % 6 == 0;
                float angle = i * (360f / tickCount);
                float radians = angle * Mathf.Deg2Rad;
                RectTransform tick = CreateUiRect(
                    tickRoot,
                    major ? "主方位刻度" : "方位刻度",
                    major ? new Vector2(3f, 12f) : new Vector2(2f, 7f));
                tick.anchorMin = new Vector2(0.5f, 0.5f);
                tick.anchorMax = new Vector2(0.5f, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * 132f;
                tick.localEulerAngles = new Vector3(0f, 0f, -angle);
                Image tickImage = tick.gameObject.AddComponent<Image>();
                tickImage.color = major
                    ? new Color(0.72f, 0.94f, 1f, 0.95f)
                    : new Color(0.42f, 0.68f, 0.76f, 0.72f);
                tickImage.raycastTarget = false;
            }
        }

        private static void CreateHeaderLine(Transform parent, string name, Vector2 position)
        {
            RectTransform line = CreateUiRect(parent, name, new Vector2(48f, 1f));
            line.anchorMin = new Vector2(0.5f, 1f);
            line.anchorMax = new Vector2(0.5f, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position;
            Image image = line.gameObject.AddComponent<Image>();
            image.color = new Color(0.36f, 0.76f, 0.86f, 0.65f);
            image.raycastTarget = false;
        }

        private static Image CreateCircleImage(Transform parent, string name, float diameter, Vector2 position, Color color, Sprite sprite)
        {
            RectTransform rect = CreateUiRect(parent, name, new Vector2(diameter, diameter));
            AnchorAtTopCenter(rect, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void CreateLegendItem(Transform parent, string name, string label, float x, Color markerColor)
        {
            RectTransform item = CreateUiRect(parent, name, new Vector2(86f, 24f));
            AnchorAtCenter(item, new Vector2(x, 0f));

            RectTransform marker = CreateUiRect(item, "图例色标", new Vector2(7f, 7f));
            marker.anchorMin = new Vector2(0f, 0.5f);
            marker.anchorMax = new Vector2(0f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = new Vector2(9f, 0f);
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.sprite = _circleSprite;
            markerImage.color = markerColor;
            markerImage.raycastTarget = false;

            Text text = CreateText(item, "图例名称", label, 12, TextAnchor.MiddleLeft, FontStyle.Normal);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(18f, 0f);
            text.rectTransform.offsetMax = Vector2.zero;
            text.color = new Color(0.74f, 0.84f, 0.88f, 0.96f);
        }

        private static void CreateExpandedMapGrid(Transform parent)
        {
            for (int i = 1; i < 4; i++)
            {
                float offset = Mathf.Lerp(-ExpandedViewportSize * 0.5f, ExpandedViewportSize * 0.5f, i / 4f);
                RectTransform vertical = CreateUiRect(parent, "地图纵向网格", new Vector2(1f, ExpandedViewportSize));
                AnchorAtCenter(vertical, new Vector2(offset, 0f));
                Image verticalImage = vertical.gameObject.AddComponent<Image>();
                verticalImage.color = new Color(0.58f, 0.84f, 0.88f, 0.11f);
                verticalImage.raycastTarget = false;

                RectTransform horizontal = CreateUiRect(parent, "地图横向网格", new Vector2(ExpandedViewportSize, 1f));
                AnchorAtCenter(horizontal, new Vector2(0f, offset));
                Image horizontalImage = horizontal.gameObject.AddComponent<Image>();
                horizontalImage.color = new Color(0.58f, 0.84f, 0.88f, 0.11f);
                horizontalImage.raycastTarget = false;
            }
        }

        private static void CreateCompassLabel(Transform parent, string name, string label, Vector2 position)
        {
            Text text = CreateText(parent, name, label, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(28f, 24f);
            text.rectTransform.anchoredPosition = position;
            text.color = new Color(0.76f, 0.92f, 0.96f, 0.95f);
        }

        private static Button CreateTextButton(Transform parent, string name, string label, Vector2 size, Vector2 position, int fontSize)
        {
            RectTransform rect = CreateUiRect(parent, name, size);
            AnchorAtCenter(rect, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.12f, 0.15f, 0.96f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            SetButtonColors(
                button,
                new Color(0.055f, 0.12f, 0.15f, 0.96f),
                new Color(0.12f, 0.3f, 0.35f, 1f),
                new Color(0.08f, 0.46f, 0.52f, 1f));

            Text text = CreateText(rect, "按钮文字", label, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchToParent(text.rectTransform);
            text.color = new Color(0.86f, 0.96f, 1f, 1f);
            return button;
        }

        private static void SetButtonColors(Button button, Color normal, Color highlighted, Color pressed)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static RenderTexture CreateRenderTexture(string name, int size)
        {
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
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

        private static void AnchorAtTopCenter(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        private static void AnchorAtCenter(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
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

    internal sealed class ExpandedMapDragSurface : MonoBehaviour,
        IPointerDownHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerUpHandler,
        IScrollHandler
    {
        private RogueliteMinimapController _owner;
        private readonly Dictionary<int, Vector2> _pointerPositions = new Dictionary<int, Vector2>(2);
        private int _pointerId = int.MinValue;
        private float _pinchDistance;

        public void Initialize(RogueliteMinimapController owner)
        {
            _owner = owner;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            _pointerPositions[eventData.pointerId] = eventData.position;
            if (_pointerPositions.Count >= 2)
            {
                _pointerId = int.MinValue;
                _pinchDistance = GetPinchDistance();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            _pointerPositions[eventData.pointerId] = eventData.position;
            if (_pointerPositions.Count < 2 && _pointerId == int.MinValue)
                _pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            _pointerPositions[eventData.pointerId] = eventData.position;
            if (_pointerPositions.Count >= 2)
            {
                float currentDistance = GetPinchDistance();
                if (_pinchDistance > 0f && currentDistance > 0f)
                    _owner?.ZoomExpandedMap(currentDistance / _pinchDistance);
                _pinchDistance = currentDistance;
                return;
            }

            if (eventData.pointerId == _pointerId)
                _owner?.PanExpandedMap(eventData.delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ReleasePointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleasePointer(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
                return;

            _owner?.ZoomExpandedMap(Mathf.Exp(eventData.scrollDelta.y * 0.15f));
        }

        private float GetPinchDistance()
        {
            if (_pointerPositions.Count < 2)
                return 0f;

            Vector2 first = Vector2.zero;
            Vector2 second = Vector2.zero;
            int index = 0;
            foreach (Vector2 position in _pointerPositions.Values)
            {
                if (index == 0)
                    first = position;
                else
                {
                    second = position;
                    break;
                }
                index++;
            }

            return Vector2.Distance(first, second);
        }

        private void ReleasePointer(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            _pointerPositions.Remove(eventData.pointerId);
            _pinchDistance = 0f;
            _pointerId = int.MinValue;
            if (_pointerPositions.Count == 1)
            {
                foreach (int pointerId in _pointerPositions.Keys)
                {
                    _pointerId = pointerId;
                    break;
                }
            }
        }

        private void OnDisable()
        {
            _pointerId = int.MinValue;
            _pinchDistance = 0f;
            _pointerPositions.Clear();
        }
    }
}
