using Game.GameFlow;
using ProjectBase;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    public class PlayerProfilePanel : BasePanel
    {
        private InputField _nameInput;
        private Image _portraitPreview;
        private Text _statusText;
        private Button _uploadButton;
        private Button _saveButton;
        private Button _closeButton;
        private RectTransform _portraitPreviewRect;
        private RectTransform _cropWindow;
        private RectTransform _cropDimImageRect;
        private RectTransform _cropBrightImageRect;
        private Image _cropDimImage;
        private Image _cropBrightImage;
        private string _selectedPortraitId;
        private Texture2D _pendingPortraitTexture;
        private float _portraitZoom = 1f;
        private Vector2 _portraitOffset;
        private bool _draggingCrop;
        private Vector2 _lastCropDragPosition;
        private bool _uiBuilt;

        private const float CropAreaSize = 460f;
        private const float CropCircleDiameter = CropAreaSize;
        private const float CropMinZoom = 1f;
        private const float CropMaxZoom = 6f;
        private static Sprite _circleMaskSprite;
        private static Sprite _circleOutlineSprite;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        public override void ShowMe()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            LoadCurrentProfile();
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_uiBuilt)
                return;

            BindPrefabControls();
            if (_nameInput != null && _portraitPreview != null && _statusText != null
                && _uploadButton != null && _saveButton != null && _closeButton != null)
            {
                AttachButtonListeners();
                _uiBuilt = true;
                return;
            }

            BuildRuntimeControls();
            _uiBuilt = _nameInput != null && _portraitPreview != null && _statusText != null
                && _uploadButton != null && _saveButton != null && _closeButton != null;
        }

        private void BindPrefabControls()
        {
            _nameInput = GetControl<InputField>("Input_PlayerName");
            _portraitPreview = GetControl<Image>("Img_PortraitPreview");
            _statusText = GetControl<Text>("Txt_Status");
            _uploadButton = GetControl<Button>("Btn_UploadPortrait");
            _saveButton = GetControl<Button>("Btn_Save");
            _closeButton = GetControl<Button>("Btn_Close");
            if (_portraitPreview != null)
                _portraitPreviewRect = _portraitPreview.rectTransform;
        }

        private void AttachButtonListeners()
        {
            if (_uploadButton != null)
            {
                _uploadButton.onClick.RemoveAllListeners();
                _uploadButton.onClick.AddListener(OnUploadPortraitClicked);
            }

            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveAllListeners();
                _saveButton.onClick.AddListener(OnSaveClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.PlayerProfile));
            }

        }

        private void LoadCurrentProfile()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm == null)
            {
                SetStatus("当前没有游戏状态机。", new Color(1f, 0.45f, 0.45f, 1f));
                return;
            }

            if (_nameInput != null)
                _nameInput.text = gsm.CurrentPlayerName ?? string.Empty;

            _selectedPortraitId = gsm.CurrentPortraitId;
            ClearPendingPortraitTexture();
            RefreshPortraitPreview();
            SetStatus("点击上传按钮选择 png/jpg，拖动头像可调整显示区域。", new Color(0.75f, 0.86f, 1f, 1f));
        }

        private void OnUploadPortraitClicked()
        {
            string path = OpenPortraitFilePanel();
            if (string.IsNullOrWhiteSpace(path))
            {
#if !UNITY_EDITOR
                SetStatus("当前构建暂未接入运行时文件选择器。", new Color(1f, 0.45f, 0.45f, 1f));
#endif
                return;
            }

            if (!PlayerPortraitUtility.TryLoadExternalPortraitTexture(path, out Texture2D texture, out string error))
            {
                SetStatus(error, new Color(1f, 0.45f, 0.45f, 1f));
                return;
            }

            ClearPendingPortraitTexture();
            _pendingPortraitTexture = texture;
            _portraitZoom = 1f;
            _portraitOffset = Vector2.zero;
            ShowCropWindow();
            SetStatus("在预览窗口拖动头像，滚动鼠标滚轮缩放。", new Color(0.6f, 1f, 0.72f, 1f));
        }

        private void OnSaveClicked()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm == null)
            {
                SetStatus("当前没有游戏状态机。", new Color(1f, 0.45f, 0.45f, 1f));
                return;
            }

            string playerName = _nameInput != null ? _nameInput.text : string.Empty;
            string portraitIdToSave = _selectedPortraitId;
            if (_pendingPortraitTexture != null)
            {
                const int outputSize = 512;
                Vector2 bakeOffset = _portraitOffset * (outputSize / GetPortraitFrameSize());
                if (!PlayerPortraitUtility.TrySaveCroppedPortrait(_pendingPortraitTexture, _portraitZoom, bakeOffset, outputSize, out portraitIdToSave, out string cropError))
                {
                    SetStatus(cropError, new Color(1f, 0.45f, 0.45f, 1f));
                    return;
                }
            }

            if (!gsm.TryUpdateCurrentPlayerProfile(playerName, portraitIdToSave, out string error))
            {
                SetStatus(error, new Color(1f, 0.45f, 0.45f, 1f));
                return;
            }

            _selectedPortraitId = portraitIdToSave;
            ClearPendingPortraitTexture();
            SetStatus("玩家信息已保存。", new Color(0.6f, 1f, 0.72f, 1f));
            UIManager.GetInstance()?.HidePanel(PanelNames.PlayerProfile);
        }

        private void RefreshPortraitPreview()
        {
            if (_portraitPreview == null)
                return;

            if (_pendingPortraitTexture != null)
            {
                _portraitPreview.sprite = Sprite.Create(
                    _pendingPortraitTexture,
                    new Rect(0f, 0f, _pendingPortraitTexture.width, _pendingPortraitTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _portraitPreview.preserveAspect = true;
                if (_portraitPreviewRect != null)
                {
                    _portraitPreviewRect.anchorMin = Vector2.zero;
                    _portraitPreviewRect.anchorMax = Vector2.one;
                    _portraitPreviewRect.offsetMin = Vector2.zero;
                    _portraitPreviewRect.offsetMax = Vector2.zero;
                    _portraitPreviewRect.localScale = Vector3.one;
                    _portraitPreviewRect.anchoredPosition = Vector2.zero;
                }
                return;
            }

            Sprite sprite = PlayerPortraitUtility.LoadPortraitSprite(_selectedPortraitId);
            if (sprite != null)
            {
                _portraitPreview.sprite = sprite;
                _portraitPreview.preserveAspect = true;
                if (_portraitPreviewRect != null)
                {
                    _portraitPreviewRect.anchorMin = Vector2.zero;
                    _portraitPreviewRect.anchorMax = Vector2.one;
                    _portraitPreviewRect.offsetMin = Vector2.zero;
                    _portraitPreviewRect.offsetMax = Vector2.zero;
                    _portraitPreviewRect.localScale = Vector3.one;
                    _portraitPreviewRect.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void BeginCropDrag(BaseEventData eventData)
        {
            PointerEventData pointerData = eventData as PointerEventData;
            if (_pendingPortraitTexture == null || pointerData == null)
                return;

            _draggingCrop = true;
            _lastCropDragPosition = pointerData.position;
        }

        private void DragCrop(BaseEventData eventData)
        {
            PointerEventData pointerData = eventData as PointerEventData;
            if (!_draggingCrop || _pendingPortraitTexture == null || pointerData == null)
                return;

            Vector2 delta = pointerData.position - _lastCropDragPosition;
            _lastCropDragPosition = pointerData.position;
            _portraitOffset += delta;
            ClampPortraitOffset();
            ApplyCropPreviewTransform();
        }

        private void EndCropDrag(BaseEventData eventData)
        {
            _draggingCrop = false;
        }

        private void OnCropScroll(BaseEventData eventData)
        {
            PointerEventData pointerData = eventData as PointerEventData;
            if (_pendingPortraitTexture == null || pointerData == null)
                return;

            float delta = pointerData.scrollDelta.y;
            if (Mathf.Abs(delta) < 0.01f)
                return;

            float previousZoom = _portraitZoom;
            _portraitZoom = Mathf.Clamp(_portraitZoom + delta * 0.4f, CropMinZoom, CropMaxZoom);
            if (!Mathf.Approximately(previousZoom, _portraitZoom))
            {
                ClampPortraitOffset();
                ApplyCropPreviewTransform();
            }
        }

        private void ApplyCropPreviewTransform()
        {
            if (_pendingPortraitTexture == null)
                return;

            float scale = Mathf.Max(CropCircleDiameter / _pendingPortraitTexture.width, CropCircleDiameter / _pendingPortraitTexture.height) * Mathf.Max(CropMinZoom, _portraitZoom);
            Vector2 size = new Vector2(_pendingPortraitTexture.width * scale, _pendingPortraitTexture.height * scale);
            ApplyCropImageTransform(_cropDimImageRect, size);
            ApplyCropImageTransform(_cropBrightImageRect, size);
        }

        private void ApplyCropImageTransform(RectTransform imageRect, Vector2 size)
        {
            if (imageRect == null)
                return;

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = size;
            imageRect.anchoredPosition = _portraitOffset;
            imageRect.localScale = Vector3.one;
        }

        private void ClampPortraitOffset()
        {
            if (_pendingPortraitTexture == null)
            {
                _portraitOffset = Vector2.zero;
                return;
            }

            float frameSize = CropCircleDiameter;
            float scale = Mathf.Max(frameSize / _pendingPortraitTexture.width, frameSize / _pendingPortraitTexture.height) * Mathf.Max(CropMinZoom, _portraitZoom);
            float displayWidth = _pendingPortraitTexture.width * scale;
            float displayHeight = _pendingPortraitTexture.height * scale;
            float maxOffsetX = Mathf.Max(0f, (displayWidth - frameSize) * 0.5f);
            float maxOffsetY = Mathf.Max(0f, (displayHeight - frameSize) * 0.5f);
            _portraitOffset = new Vector2(
                Mathf.Clamp(_portraitOffset.x, -maxOffsetX, maxOffsetX),
                Mathf.Clamp(_portraitOffset.y, -maxOffsetY, maxOffsetY));
        }

        private void ClearPendingPortraitTexture()
        {
            if (_pendingPortraitTexture != null)
            {
                Destroy(_pendingPortraitTexture);
                _pendingPortraitTexture = null;
            }

            _portraitZoom = 1f;
            _portraitOffset = Vector2.zero;
        }

        private float GetPortraitFrameSize()
        {
            return CropCircleDiameter;
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null)
                return;

            _statusText.text = message ?? string.Empty;
            _statusText.color = color;
        }

        private static string OpenPortraitFilePanel()
        {
#if UNITY_EDITOR
            return EditorUtility.OpenFilePanel("选择玩家头像", string.Empty, string.Empty);
#else
            return string.Empty;
#endif
        }

        private void ShowCropWindow()
        {
            EnsureCropWindowBuilt();
            if (_cropWindow == null || _pendingPortraitTexture == null)
                return;

            Sprite sourceSprite = Sprite.Create(
                _pendingPortraitTexture,
                new Rect(0f, 0f, _pendingPortraitTexture.width, _pendingPortraitTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            if (_cropDimImage != null)
            {
                _cropDimImage.sprite = sourceSprite;
                _cropDimImage.preserveAspect = true;
            }

            if (_cropBrightImage != null)
            {
                _cropBrightImage.sprite = sourceSprite;
                _cropBrightImage.preserveAspect = true;
            }

            _portraitZoom = 1f;
            _portraitOffset = Vector2.zero;
            ClampPortraitOffset();
            ApplyCropPreviewTransform();
            _cropWindow.gameObject.SetActive(true);
        }

        private void HideCropWindow()
        {
            if (_cropWindow != null)
                _cropWindow.gameObject.SetActive(false);

            _draggingCrop = false;
        }

        private void OnCropConfirmClicked()
        {
            HideCropWindow();
            RefreshPortraitPreview();
            SetStatus("头像裁剪已确认，点击保存后写入当前存档。", new Color(0.6f, 1f, 0.72f, 1f));
        }

        private void OnCropCancelClicked()
        {
            HideCropWindow();
            ClearPendingPortraitTexture();
            RefreshPortraitPreview();
            SetStatus("已取消头像上传。", new Color(0.75f, 0.86f, 1f, 1f));
        }

        private void EnsureCropWindowBuilt()
        {
            if (_cropWindow != null)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            GameObject overlayObject = new GameObject("CropWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            _cropWindow = overlayObject.GetComponent<RectTransform>();
            _cropWindow.anchorMin = Vector2.zero;
            _cropWindow.anchorMax = Vector2.one;
            _cropWindow.offsetMin = Vector2.zero;
            _cropWindow.offsetMax = Vector2.zero;
            Image overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
            overlayImage.raycastTarget = true;

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                _cropWindow,
                "CropEditor",
                new Vector2(660f, 650f),
                new Color(0.08f, 0.1f, 0.14f, 0.98f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "CropTitle",
                "头像预览",
                30,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 282f),
                new Vector2(300f, 46f),
                Color.white,
                FontStyle.Bold);

            RectTransform cropArea = CreateCropArea(window);
            CreateCropPreviewLayers(cropArea);

            Button confirmButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_CropConfirm",
                "使用头像",
                new Vector2(-80f, -252f),
                new Vector2(150f, 48f),
                new Color(0.25f, 0.62f, 0.36f, 1f),
                Color.white);
            confirmButton.onClick.AddListener(OnCropConfirmClicked);

            Button cancelButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_CropCancel",
                "取消",
                new Vector2(100f, -252f),
                new Vector2(120f, 48f),
                new Color(0.42f, 0.24f, 0.25f, 1f),
                Color.white);
            cancelButton.onClick.AddListener(OnCropCancelClicked);

            _cropWindow.gameObject.SetActive(false);
        }

        private RectTransform CreateCropArea(Transform parent)
        {
            GameObject cropAreaObject = new GameObject("CropArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(Button));
            cropAreaObject.transform.SetParent(parent, false);
            RectTransform cropArea = cropAreaObject.GetComponent<RectTransform>();
            cropArea.anchorMin = new Vector2(0.5f, 0.5f);
            cropArea.anchorMax = new Vector2(0.5f, 0.5f);
            cropArea.pivot = new Vector2(0.5f, 0.5f);
            cropArea.anchoredPosition = new Vector2(0f, 18f);
            cropArea.sizeDelta = new Vector2(CropAreaSize, CropAreaSize);

            Image cropAreaImage = cropAreaObject.GetComponent<Image>();
            cropAreaImage.color = new Color(0.02f, 0.025f, 0.035f, 1f);
            cropAreaImage.raycastTarget = true;

            Button cropButton = cropAreaObject.GetComponent<Button>();
            cropButton.transition = Selectable.Transition.None;
            cropButton.targetGraphic = cropAreaImage;
            UIManager.AddCustomEventListener(cropButton, EventTriggerType.PointerDown, BeginCropDrag);
            UIManager.AddCustomEventListener(cropButton, EventTriggerType.Drag, DragCrop);
            UIManager.AddCustomEventListener(cropButton, EventTriggerType.PointerUp, EndCropDrag);
            UIManager.AddCustomEventListener(cropButton, EventTriggerType.Scroll, OnCropScroll);
            return cropArea;
        }

        private void CreateCropPreviewLayers(RectTransform cropArea)
        {
            GameObject dimObject = new GameObject("Img_CropDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimObject.transform.SetParent(cropArea, false);
            _cropDimImageRect = dimObject.GetComponent<RectTransform>();
            _cropDimImage = dimObject.GetComponent<Image>();
            _cropDimImage.color = new Color(1f, 1f, 1f, 0.42f);
            _cropDimImage.raycastTarget = false;

            GameObject brightMaskObject = new GameObject("BrightCircleMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            brightMaskObject.transform.SetParent(cropArea, false);
            RectTransform brightMaskRect = brightMaskObject.GetComponent<RectTransform>();
            brightMaskRect.anchorMin = new Vector2(0.5f, 0.5f);
            brightMaskRect.anchorMax = new Vector2(0.5f, 0.5f);
            brightMaskRect.pivot = new Vector2(0.5f, 0.5f);
            brightMaskRect.anchoredPosition = Vector2.zero;
            brightMaskRect.sizeDelta = new Vector2(CropCircleDiameter, CropCircleDiameter);
            Image brightMaskImage = brightMaskObject.GetComponent<Image>();
            brightMaskImage.sprite = GetCircleMaskSprite();
            brightMaskImage.color = Color.white;
            brightMaskImage.raycastTarget = false;
            Mask brightMask = brightMaskObject.GetComponent<Mask>();
            brightMask.showMaskGraphic = false;

            GameObject brightObject = new GameObject("Img_CropBright", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            brightObject.transform.SetParent(brightMaskObject.transform, false);
            _cropBrightImageRect = brightObject.GetComponent<RectTransform>();
            _cropBrightImage = brightObject.GetComponent<Image>();
            _cropBrightImage.color = Color.white;
            _cropBrightImage.raycastTarget = false;

            GameObject outlineObject = new GameObject("CircleOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outlineObject.transform.SetParent(cropArea, false);
            RectTransform outlineRect = outlineObject.GetComponent<RectTransform>();
            outlineRect.anchorMin = new Vector2(0.5f, 0.5f);
            outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            outlineRect.anchoredPosition = Vector2.zero;
            outlineRect.sizeDelta = new Vector2(CropCircleDiameter + 6f, CropCircleDiameter + 6f);
            Image outlineImage = outlineObject.GetComponent<Image>();
            outlineImage.sprite = GetCircleOutlineSprite();
            outlineImage.color = Color.white;
            outlineImage.raycastTarget = false;
        }

        private static Sprite GetCircleMaskSprite()
        {
            if (_circleMaskSprite != null)
                return _circleMaskSprite;

            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            _circleMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleMaskSprite;
        }

        private static Sprite GetCircleOutlineSprite()
        {
            if (_circleOutlineSprite != null)
                return _circleOutlineSprite;

            const int size = 256;
            const float thickness = 1.5f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - thickness;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float outer = Mathf.Clamp01(radius + thickness - distance);
                    float inner = Mathf.Clamp01(radius - distance);
                    float alpha = Mathf.Clamp01(outer - inner);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            _circleOutlineSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleOutlineSprite;
        }

        private void BuildRuntimeControls()
        {
            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.55f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(620f, 430f),
                new Color(0.11f, 0.14f, 0.2f, 0.97f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Title",
                "玩家信息设置",
                30,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 178f),
                new Vector2(320f, 46f),
                Color.white,
                FontStyle.Bold);

            GameObject maskObject = new GameObject("PortraitMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(Button));
            maskObject.transform.SetParent(window, false);
            RectTransform maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(-195f, 48f);
            maskRect.sizeDelta = new Vector2(130f, 130f);
            Image maskImage = maskObject.GetComponent<Image>();
            maskImage.color = Color.white;
            maskImage.raycastTarget = true;
            Sprite maskSprite = Resources.Load<Sprite>("SocialPortraits/圆形遮罩");
            if (maskSprite != null)
                maskImage.sprite = maskSprite;
            Button previewButton = maskObject.GetComponent<Button>();
            previewButton.transition = Selectable.Transition.None;
            previewButton.targetGraphic = maskImage;

            GameObject previewObject = new GameObject("Img_PortraitPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewObject.transform.SetParent(maskObject.transform, false);
            RectTransform previewRect = previewObject.GetComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            Image previewImage = previewObject.GetComponent<Image>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
            _portraitPreview = previewImage;
            _portraitPreviewRect = previewRect;

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "NameLabel",
                "玩家名字",
                22,
                TextAnchor.MiddleLeft,
                new Vector2(34f, 96f),
                new Vector2(120f, 34f),
                Color.white,
                FontStyle.Bold);

            _nameInput = RuntimeOverlayPanelBuilder.CreateInputField(
                window,
                "Input_PlayerName",
                "输入玩家名字",
                string.Empty,
                new Vector2(105f, 42f),
                new Vector2(330f, 52f),
                new Color(0.17f, 0.2f, 0.26f, 1f),
                Color.white);

            _uploadButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_UploadPortrait",
                "上传头像",
                new Vector2(-195f, -50f),
                new Vector2(150f, 46f),
                new Color(0.18f, 0.45f, 0.72f, 1f),
                Color.white);

            _saveButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Save",
                "保存",
                new Vector2(80f, -128f),
                new Vector2(140f, 50f),
                new Color(0.25f, 0.62f, 0.36f, 1f),
                Color.white);

            _closeButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Close",
                "关闭",
                new Vector2(235f, -128f),
                new Vector2(120f, 50f),
                new Color(0.36f, 0.36f, 0.38f, 1f),
                Color.white);

            _statusText = RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Status",
                string.Empty,
                18,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -188f),
                new Vector2(520f, 32f),
                new Color(0.75f, 0.86f, 1f, 1f));

            AttachButtonListeners();
        }

    }
}
