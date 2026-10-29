using System;
using System.Collections.Generic;
using Game.GameFlow;
using Game.Input;
using Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class MobileGameplayControls : MonoBehaviour
    {
        private const float DefaultLookSensitivity = 0.36f;
        private const float MinimumLookSensitivity = 0.12f;
        private const float MaximumLookSensitivity = 0.72f;
        private const float MinimumControlScale = 0.6f;
        private const float MaximumControlScale = 1.6f;
        private const string LayoutPrefsKey = "MobileGameplayControls.Layout.V1";
        private const string LookSensitivityPrefsKey = "MobileGameplayControls.LookSensitivity.V1";

        private static readonly Color TextColor = new Color(0.95f, 0.97f, 1f, 1f);
        private static readonly Color OutlineColor = new Color(0.02f, 0.025f, 0.03f, 0.9f);
        private static Font _font;

        private readonly List<RectTransform> _touchBlockingRects = new List<RectTransform>(16);
        private readonly List<RectTransform> _lookDragPassthroughRects = new List<RectTransform>(16);
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(16);
        private readonly List<LayoutControl> _layoutControls = new List<LayoutControl>(16);

        private GameObject _canvasObject;
        private RectTransform _safeAreaRoot;
        private PlayerInputHandler _inputHandler;
        private PlayerController _playerController;
        private MobileJoystickControl _joystick;
        private GameObject _layoutEditorRoot;
        private Text _layoutSelectionText;
        private Slider _layoutScaleSlider;
        private Slider _lookSensitivitySlider;
        private List<LayoutEntry> _layoutSnapshot;
        private LayoutControl _selectedLayoutControl;
        private Rect _lastSafeArea;
        private int _lookTouchId = -1;
        private bool _controlsVisible;
        private bool _layoutEditing;
        private float _lookSensitivity = DefaultLookSensitivity;

        [Serializable]
        private sealed class LayoutEntry
        {
            public string id;
            public Vector2 anchoredPosition;
            public float scale;
        }

        [Serializable]
        private sealed class LayoutData
        {
            public List<LayoutEntry> entries = new List<LayoutEntry>();
        }

        private sealed class LayoutControl
        {
            public string Id;
            public string DisplayName;
            public RectTransform Rect;
            public Vector2 DefaultPosition;
            public float DefaultScale;
            public MobileControlLayoutHandle Handle;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            if (FindFirstObjectByType<MobileGameplayControls>() != null)
                return;

            GameObject root = new GameObject(nameof(MobileGameplayControls));
            DontDestroyOnLoad(root);
            root.AddComponent<MobileGameplayControls>();
#endif
        }

        private void Awake()
        {
            _lookSensitivity = Mathf.Clamp(
                PlayerPrefs.GetFloat(LookSensitivityPrefsKey, DefaultLookSensitivity),
                MinimumLookSensitivity,
                MaximumLookSensitivity);
            BuildInterface();
            LoadLayout();
            RefreshSafeArea(true);
            _canvasObject.SetActive(false);
        }

        private void Update()
        {
            RefreshSafeArea(false);

            bool shouldShow = _layoutEditing || ShouldShowControls();
            SetControlsVisible(shouldShow);
            if (!shouldShow)
                return;

            if (_layoutEditing)
                return;

            ResolvePlayerReferences();
            UpdateLookTouch();
        }

        private void OnDestroy()
        {
            ResetMobileInput();
            PlayerPrefs.SetFloat(LookSensitivityPrefsKey, _lookSensitivity);
            PlayerPrefs.Save();
        }

        private static bool IsTouchPlatform()
        {
            if (Application.platform == RuntimePlatform.Android)
                return true;

#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static bool ShouldShowControls()
        {
            if (!IsTouchPlatform())
                return false;

            GameStateMachine gameStateMachine = GameStateMachine.GetInstance();
            return gameStateMachine != null
                   && gameStateMachine.CurrentState == GameStateMachine.State.InLevel
                   && !GameplayUIInputBridge.IsAnyGameplayPanelOpen();
        }

        private void SetControlsVisible(bool visible)
        {
            if (_controlsVisible == visible)
                return;

            if (!visible)
                ResetMobileInput();

            _controlsVisible = visible;
            _canvasObject.SetActive(visible);
        }

        private void ResolvePlayerReferences()
        {
            if (_inputHandler != null && _playerController != null)
                return;

            Transform playerTransform = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (playerTransform != null)
            {
                _inputHandler = playerTransform.GetComponent<PlayerInputHandler>();
                _playerController = playerTransform.GetComponent<PlayerController>();
            }

            if (_inputHandler == null)
                _inputHandler = FindFirstObjectByType<PlayerInputHandler>();
            if (_playerController == null && _inputHandler != null)
                _playerController = _inputHandler.GetComponent<PlayerController>();
        }

        private void ResetMobileInput()
        {
            _lookTouchId = -1;
            _joystick?.ResetStick();
            _inputHandler?.ResetMobileInput();
        }

        private void UpdateLookTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null || _inputHandler == null)
            {
                _lookTouchId = -1;
                return;
            }

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var touch = touchscreen.touches[i];
                int touchId = touch.touchId.ReadValue();
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (_lookTouchId < 0 && !IsTouchBlocked(position))
                        _lookTouchId = touchId;
                    continue;
                }

                if (touchId != _lookTouchId)
                    continue;

                if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    _inputHandler.AddMobileLookDelta(touch.delta.ReadValue() * _lookSensitivity);
                }
                else if (phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    _lookTouchId = -1;
                }
            }
        }

        private bool IsTouchBlocked(Vector2 screenPosition)
        {
            for (int i = 0; i < _lookDragPassthroughRects.Count; i++)
            {
                RectTransform rect = _lookDragPassthroughRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition))
                    return false;
            }

            for (int i = 0; i < _touchBlockingRects.Count; i++)
            {
                RectTransform rect = _touchBlockingRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition))
                    return true;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPosition,
            };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, _uiRaycastResults);
            for (int i = 0; i < _uiRaycastResults.Count; i++)
            {
                GameObject hitObject = _uiRaycastResults[i].gameObject;
                if (hitObject != null && hitObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        private void BuildInterface()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _canvasObject = new GameObject(
                "移动端操作界面",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);
            _canvasObject.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _safeAreaRoot = CreateRect(_canvasObject.transform, "安全区域");
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;

            CreateJoystick();
            CreateCombatButtons();
            CreateUtilityButtons();
            CreatePanelButtons();
            CreateLayoutEditor();
        }

        private void CreateJoystick()
        {
            RectTransform outer = CreateCircle(
                _safeAreaRoot,
                "移动轮盘",
                new Vector2(320f, 320f),
                new Color(0.08f, 0.1f, 0.12f, 0.58f));
            Anchor(outer, Vector2.zero, new Vector2(218f, 198f));

            RectTransform inner = CreateCircle(
                outer,
                "步行区",
                new Vector2(213.333f, 213.333f),
                new Color(0.08f, 0.48f, 0.42f, 0.42f));
            Center(inner, Vector2.zero);
            inner.GetComponent<UICircleGraphic>().raycastTarget = false;

            RectTransform knob = CreateCircle(
                outer,
                "轮盘摇杆",
                new Vector2(82f, 82f),
                new Color(0.78f, 0.9f, 0.88f, 0.92f));
            Center(knob, Vector2.zero);
            knob.GetComponent<UICircleGraphic>().raycastTarget = false;

            _joystick = outer.gameObject.AddComponent<MobileJoystickControl>();
            _joystick.Initialize(outer, knob, HandleJoystickChanged);
            _touchBlockingRects.Add(outer);
            RegisterLayoutControl(outer, "移动轮盘", "移动轮盘");
        }

        private void CreateCombatButtons()
        {
            RectTransform attack = CreateCircleButton(
                "普攻按钮", "普攻", new Vector2(-230f, 170f), 150f,
                new Color(0.68f, 0.16f, 0.12f, 0.92f), null);
            MobileAttackControl attackControl = attack.gameObject.AddComponent<MobileAttackControl>();
            attackControl.Initialize(BeginAttack, EndAttack);

            CreateCircleButton(
                "闪避按钮", "闪避", new Vector2(-92f, 160f), 100f,
                new Color(0.16f, 0.42f, 0.68f, 0.92f), RegisterDodge);
            RectTransform sprint = CreateCircleButton(
                "奔跑加速按钮", "加速", new Vector2(-92f, 52f), 100f,
                new Color(0.62f, 0.38f, 0.12f, 0.92f), null);
            MobileHoldControl sprintControl = sprint.gameObject.AddComponent<MobileHoldControl>();
            sprintControl.Initialize(SetSprint);
            CreateCircleButton(
                "跳跃按钮", "跳跃", new Vector2(-116f, 288f), 100f,
                new Color(0.18f, 0.56f, 0.3f, 0.92f), RegisterJump);
            CreateCircleButton(
                "交互按钮", "交互", new Vector2(-422f, 472f), 88f,
                new Color(0.5f, 0.34f, 0.14f, 0.92f), LocalPlayerInteractionResolver.RequestMobileInteract);

            CreateCircleButton(
                "技能槽1", "技能1", new Vector2(-420f, 90f), 88f,
                new Color(0.48f, 0.28f, 0.62f, 0.92f), () => RegisterSkill(0));
            CreateCircleButton(
                "技能槽2", "技能2", new Vector2(-455f, 185f), 88f,
                new Color(0.38f, 0.32f, 0.66f, 0.92f), () => RegisterSkill(1));
            CreateCircleButton(
                "技能槽3", "技能3", new Vector2(-420f, 285f), 88f,
                new Color(0.28f, 0.4f, 0.68f, 0.92f), () => RegisterSkill(2));
            CreateCircleButton(
                "技能槽4", "技能4", new Vector2(-345f, 360f), 88f,
                new Color(0.18f, 0.48f, 0.64f, 0.92f), () => RegisterSkill(3));
        }

        private void CreateUtilityButtons()
        {
            CreateCircleButton(
                "切换形态按钮", "形态", new Vector2(-312f, 472f), 84f,
                new Color(0.55f, 0.4f, 0.16f, 0.92f), ToggleAttackMode);
            CreateCircleButton(
                "瞄准按钮", "瞄准", new Vector2(-230f, 306f), 84f,
                new Color(0.2f, 0.48f, 0.52f, 0.92f), ToggleAim);
            CreateCircleButton(
                "回血药剂按钮", "回血", new Vector2(-202f, 472f), 84f,
                new Color(0.54f, 0.18f, 0.22f, 0.92f), UseHealthPotion);
            CreateCircleButton(
                "回蓝药剂按钮", "回蓝", new Vector2(-92f, 472f), 84f,
                new Color(0.14f, 0.36f, 0.68f, 0.92f), UseManaPotion);
        }

        private void CreatePanelButtons()
        {
            CreateLookSensitivitySlider();
            CreatePanelButton(
                "设置面板按钮", "设置", new Vector2(-775f, -30f),
                GameplayInputEvents.RequestToggleMenu);
            CreatePanelButton(
                "按键配置面板按钮", "按键", new Vector2(-650f, -30f),
                GameplayInputEvents.RequestToggleKeyConfig);
            CreatePanelButton(
                "背包面板按钮", "背包", new Vector2(-525f, -30f),
                GameplayInputEvents.RequestToggleBackpack);
            CreatePanelButton(
                "技能树面板按钮", "技能树", new Vector2(-400f, -30f),
                GameplayInputEvents.RequestToggleSkillTree);
        }

        private RectTransform CreateCircleButton(
            string name,
            string label,
            Vector2 anchoredPosition,
            float diameter,
            Color color,
            Action onClick)
        {
            RectTransform rect = CreateCircle(
                _safeAreaRoot,
                name,
                new Vector2(diameter, diameter),
                color);
            Anchor(rect, new Vector2(1f, 0f), anchoredPosition);

            UICircleGraphic graphic = rect.GetComponent<UICircleGraphic>();
            AddOutline(graphic);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            CreateLabel(rect, label, diameter >= 120f ? 30 : 23);
            _touchBlockingRects.Add(rect);
            _lookDragPassthroughRects.Add(rect);
            RegisterLayoutControl(rect, name, label);
            return rect;
        }

        private void CreateLookSensitivitySlider()
        {
            GameObject rootObject = new GameObject(
                "视角灵敏度",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            rootObject.transform.SetParent(_safeAreaRoot, false);
            rootObject.layer = LayerMask.NameToLayer("UI");

            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.one;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(250f, 58f);
            root.anchoredPosition = new Vector2(-968f, -30f);

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.1f, 0.13f, 0.16f, 0.9f);

            Outline outline = rootObject.GetComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            CreateEditorText(
                root,
                "灵敏度文字",
                new Vector2(43f, 29f),
                new Vector2(78f, 44f),
                18,
                TextAnchor.MiddleCenter).text = "灵敏度";

            GameObject sliderObject = new GameObject("灵敏度滑杆", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(root, false);
            sliderObject.layer = LayerMask.NameToLayer("UI");

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.zero;
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(166f, 29f);
            sliderRect.sizeDelta = new Vector2(148f, 44f);

            CreateSliderImage(
                sliderRect,
                "轨道",
                new Color(0.22f, 0.26f, 0.29f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, -4f),
                new Vector2(0f, 4f));

            RectTransform fillArea = CreateRect(sliderRect, "填充区域");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(8f, -4f);
            fillArea.offsetMax = new Vector2(-8f, 4f);
            Image fill = CreateSliderImage(
                fillArea,
                "填充",
                new Color(0.2f, 0.68f, 0.72f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            RectTransform handleArea = CreateRect(sliderRect, "滑块区域");
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            Image handle = CreateSliderImage(
                handleArea,
                "滑块",
                new Color(0.9f, 0.98f, 1f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-13f, -13f),
                new Vector2(13f, 13f));

            _lookSensitivitySlider = sliderObject.GetComponent<Slider>();
            _lookSensitivitySlider.minValue = MinimumLookSensitivity;
            _lookSensitivitySlider.maxValue = MaximumLookSensitivity;
            _lookSensitivitySlider.value = _lookSensitivity;
            _lookSensitivitySlider.fillRect = fill.rectTransform;
            _lookSensitivitySlider.handleRect = handle.rectTransform;
            _lookSensitivitySlider.targetGraphic = handle;
            _lookSensitivitySlider.direction = Slider.Direction.LeftToRight;
            _lookSensitivitySlider.onValueChanged.AddListener(OnLookSensitivityChanged);

            _touchBlockingRects.Add(root);
        }

        private void OnLookSensitivityChanged(float value)
        {
            _lookSensitivity = Mathf.Clamp(value, MinimumLookSensitivity, MaximumLookSensitivity);
            PlayerPrefs.SetFloat(LookSensitivityPrefsKey, _lookSensitivity);
        }

        private void CreatePanelButton(string name, string label, Vector2 anchoredPosition, Action onClick)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            buttonObject.transform.SetParent(_safeAreaRoot, false);
            buttonObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(112f, 58f);
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.1f, 0.13f, 0.16f, 0.9f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            CreateLabel(rect, label, 23);
            _touchBlockingRects.Add(rect);
            RegisterLayoutControl(rect, name, label);
        }

        public static bool BeginLayoutEditing()
        {
            MobileGameplayControls controls = FindFirstObjectByType<MobileGameplayControls>();
            if (controls == null)
            {
                Debug.LogWarning("[MobileGameplayControls] 未找到移动端操作界面，无法编辑手机按键布局。");
                return false;
            }

            controls.BeginLayoutEditingInternal();
            return true;
        }

        public static bool ResetLayoutToDefaultsAndSave()
        {
            MobileGameplayControls controls = FindFirstObjectByType<MobileGameplayControls>();
            if (controls == null)
            {
                Debug.LogWarning("[MobileGameplayControls] 未找到移动端操作界面，无法恢复手机按键布局。");
                return false;
            }

            controls.ApplyDefaultLayout();
            controls.SaveLayout();
            return true;
        }

        private void RegisterLayoutControl(RectTransform rect, string id, string displayName)
        {
            GameObject handleObject = new GameObject(
                "布局调整区域",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(MobileControlLayoutHandle));
            handleObject.transform.SetParent(rect, false);
            handleObject.layer = LayerMask.NameToLayer("UI");

            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = new Color(1f, 0.78f, 0.22f, 0.08f);

            Outline outline = handleObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            MobileControlLayoutHandle handle = handleObject.GetComponent<MobileControlLayoutHandle>();
            handle.Initialize(this, id);
            handleObject.SetActive(false);

            _layoutControls.Add(new LayoutControl
            {
                Id = id,
                DisplayName = displayName,
                Rect = rect,
                DefaultPosition = rect.anchoredPosition,
                DefaultScale = rect.localScale.x,
                Handle = handle,
            });
        }

        private void CreateLayoutEditor()
        {
            _layoutEditorRoot = new GameObject(
                "手机按键布局工具栏",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            _layoutEditorRoot.transform.SetParent(_safeAreaRoot, false);
            _layoutEditorRoot.layer = LayerMask.NameToLayer("UI");

            RectTransform rootRect = _layoutEditorRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(900f, 92f);
            rootRect.anchoredPosition = new Vector2(0f, 24f);

            Image rootImage = _layoutEditorRoot.GetComponent<Image>();
            rootImage.color = new Color(0.035f, 0.045f, 0.055f, 0.96f);

            Outline rootOutline = _layoutEditorRoot.GetComponent<Outline>();
            rootOutline.effectColor = new Color(1f, 0.72f, 0.2f, 0.95f);
            rootOutline.effectDistance = new Vector2(2f, -2f);

            _layoutSelectionText = CreateEditorText(
                rootRect,
                "当前按键",
                new Vector2(-342f, 46f),
                new Vector2(180f, 56f),
                24,
                TextAnchor.MiddleLeft);

            _layoutScaleSlider = CreateScaleSlider(rootRect);
            _layoutScaleSlider.onValueChanged.AddListener(OnLayoutScaleChanged);

            CreateEditorButton(rootRect, "恢复默认", "默认", new Vector2(186f, 46f), ApplyDefaultLayout);
            CreateEditorButton(rootRect, "取消调整", "取消", new Vector2(302f, 46f), CancelLayoutEditing);
            CreateEditorButton(rootRect, "保存布局", "保存", new Vector2(418f, 46f), SaveAndCloseLayoutEditing);

            _layoutEditorRoot.SetActive(false);
        }

        private static Text CreateEditorText(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            textObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            return text;
        }

        private static Slider CreateScaleSlider(Transform parent)
        {
            GameObject sliderObject = new GameObject("大小滑杆", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            sliderObject.layer = LayerMask.NameToLayer("UI");

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.zero;
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(-92f, 46f);
            sliderRect.sizeDelta = new Vector2(280f, 44f);

            Image background = CreateSliderImage(
                sliderRect,
                "轨道",
                new Color(0.22f, 0.26f, 0.29f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, -5f),
                new Vector2(0f, 5f));

            RectTransform fillArea = CreateRect(sliderRect, "填充区域");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(8f, -5f);
            fillArea.offsetMax = new Vector2(-8f, 5f);
            Image fill = CreateSliderImage(
                fillArea,
                "填充",
                new Color(0.94f, 0.65f, 0.18f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            RectTransform handleArea = CreateRect(sliderRect, "滑块区域");
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            Image handle = CreateSliderImage(
                handleArea,
                "滑块",
                new Color(0.98f, 0.92f, 0.78f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-15f, -15f),
                new Vector2(15f, 15f));

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = MinimumControlScale;
            slider.maxValue = MaximumControlScale;
            slider.value = 1f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Image CreateSliderImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            imageObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void CreateEditorButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Action onClick)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(102f, 56f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.25f, 1f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.58f, 0.26f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            CreateLabel(rect, label, 23);
        }

        private void BeginLayoutEditingInternal()
        {
            if (_layoutEditing)
                return;

            _layoutSnapshot = CaptureLayout();
            _layoutEditing = true;
            ResetMobileInput();
            SetControlsVisible(true);
            _layoutEditorRoot.SetActive(true);
            SetLayoutHandlesActive(true);
            SelectLayoutControl(_layoutControls.Count > 0 ? _layoutControls[0].Id : null);
        }

        private void SaveAndCloseLayoutEditing()
        {
            SaveLayout();
            CloseLayoutEditing();
        }

        private void CancelLayoutEditing()
        {
            ApplyLayout(_layoutSnapshot);
            CloseLayoutEditing();
        }

        private void CloseLayoutEditing()
        {
            _layoutEditing = false;
            _layoutSnapshot = null;
            _selectedLayoutControl = null;
            _layoutEditorRoot.SetActive(false);
            SetLayoutHandlesActive(false);
            SetControlsVisible(ShouldShowControls());
        }

        private void SetLayoutHandlesActive(bool active)
        {
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                LayoutControl control = _layoutControls[i];
                if (control.Handle != null)
                    control.Handle.gameObject.SetActive(active);
            }
        }

        internal void SelectLayoutControl(string id)
        {
            if (!_layoutEditing || string.IsNullOrEmpty(id))
                return;

            _selectedLayoutControl = FindLayoutControl(id);
            if (_selectedLayoutControl == null)
                return;

            _layoutScaleSlider.SetValueWithoutNotify(_selectedLayoutControl.Rect.localScale.x);
            RefreshLayoutSelection();
        }

        internal void DragLayoutControl(string id, PointerEventData eventData)
        {
            if (!_layoutEditing || eventData == null)
                return;

            SelectLayoutControl(id);
            if (_selectedLayoutControl == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _safeAreaRoot,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            RectTransform rect = _selectedLayoutControl.Rect;
            Vector2 anchorReference = new Vector2(
                Mathf.Lerp(_safeAreaRoot.rect.xMin, _safeAreaRoot.rect.xMax, rect.anchorMin.x),
                Mathf.Lerp(_safeAreaRoot.rect.yMin, _safeAreaRoot.rect.yMax, rect.anchorMin.y));
            rect.anchoredPosition = ClampAnchoredPosition(rect, localPoint - anchorReference);
        }

        private void OnLayoutScaleChanged(float value)
        {
            if (!_layoutEditing || _selectedLayoutControl == null)
                return;

            float scale = Mathf.Clamp(value, MinimumControlScale, MaximumControlScale);
            _selectedLayoutControl.Rect.localScale = new Vector3(scale, scale, 1f);
            _selectedLayoutControl.Rect.anchoredPosition = ClampAnchoredPosition(
                _selectedLayoutControl.Rect,
                _selectedLayoutControl.Rect.anchoredPosition);
            RefreshLayoutSelection();
        }

        private void RefreshLayoutSelection()
        {
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                LayoutControl control = _layoutControls[i];
                if (control.Handle != null)
                    control.Handle.SetSelected(ReferenceEquals(control, _selectedLayoutControl));
            }

            if (_layoutSelectionText == null || _selectedLayoutControl == null)
                return;

            int percent = Mathf.RoundToInt(_selectedLayoutControl.Rect.localScale.x * 100f);
            _layoutSelectionText.text = $"{_selectedLayoutControl.DisplayName}  {percent}%";
        }

        private LayoutControl FindLayoutControl(string id)
        {
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                if (string.Equals(_layoutControls[i].Id, id, StringComparison.Ordinal))
                    return _layoutControls[i];
            }

            return null;
        }

        private Vector2 ClampAnchoredPosition(RectTransform rect, Vector2 anchoredPosition)
        {
            Rect parentRect = _safeAreaRoot.rect;
            if (parentRect.width <= 0f || parentRect.height <= 0f)
                return anchoredPosition;

            float scale = rect.localScale.x;
            float width = rect.rect.width * scale;
            float height = rect.rect.height * scale;
            Vector2 anchorReference = new Vector2(
                Mathf.Lerp(parentRect.xMin, parentRect.xMax, rect.anchorMin.x),
                Mathf.Lerp(parentRect.yMin, parentRect.yMax, rect.anchorMin.y));
            Vector2 pivotPosition = anchorReference + anchoredPosition;
            pivotPosition.x = Mathf.Clamp(
                pivotPosition.x,
                parentRect.xMin + width * rect.pivot.x,
                parentRect.xMax - width * (1f - rect.pivot.x));
            pivotPosition.y = Mathf.Clamp(
                pivotPosition.y,
                parentRect.yMin + height * rect.pivot.y,
                parentRect.yMax - height * (1f - rect.pivot.y));
            return pivotPosition - anchorReference;
        }

        private void ClampAllControls()
        {
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                RectTransform rect = _layoutControls[i].Rect;
                rect.anchoredPosition = ClampAnchoredPosition(rect, rect.anchoredPosition);
            }
        }

        private List<LayoutEntry> CaptureLayout()
        {
            var entries = new List<LayoutEntry>(_layoutControls.Count);
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                LayoutControl control = _layoutControls[i];
                entries.Add(new LayoutEntry
                {
                    id = control.Id,
                    anchoredPosition = control.Rect.anchoredPosition,
                    scale = control.Rect.localScale.x,
                });
            }

            return entries;
        }

        private void ApplyLayout(List<LayoutEntry> entries)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                LayoutEntry entry = entries[i];
                LayoutControl control = FindLayoutControl(entry.id);
                if (control == null)
                    continue;

                float scale = Mathf.Clamp(entry.scale, MinimumControlScale, MaximumControlScale);
                control.Rect.anchoredPosition = entry.anchoredPosition;
                control.Rect.localScale = new Vector3(scale, scale, 1f);
            }

            ClampAllControls();
            if (_selectedLayoutControl != null)
            {
                _layoutScaleSlider.SetValueWithoutNotify(_selectedLayoutControl.Rect.localScale.x);
                RefreshLayoutSelection();
            }
        }

        private void ApplyDefaultLayout()
        {
            for (int i = 0; i < _layoutControls.Count; i++)
            {
                LayoutControl control = _layoutControls[i];
                control.Rect.anchoredPosition = control.DefaultPosition;
                control.Rect.localScale = new Vector3(control.DefaultScale, control.DefaultScale, 1f);
            }

            ClampAllControls();
            if (_selectedLayoutControl != null)
            {
                _layoutScaleSlider.SetValueWithoutNotify(_selectedLayoutControl.Rect.localScale.x);
                RefreshLayoutSelection();
            }
        }

        private void SaveLayout()
        {
            var data = new LayoutData { entries = CaptureLayout() };
            PlayerPrefs.SetString(LayoutPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadLayout()
        {
            if (!PlayerPrefs.HasKey(LayoutPrefsKey))
                return;

            string raw = PlayerPrefs.GetString(LayoutPrefsKey);
            LayoutData data;
            try
            {
                data = JsonUtility.FromJson<LayoutData>(raw);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MobileGameplayControls] 手机按键布局无法解析：{exception.Message}");
                return;
            }

            if (data?.entries == null || data.entries.Count != _layoutControls.Count)
            {
                Debug.LogError("[MobileGameplayControls] 手机按键布局数据不完整，请在按键配置面板中恢复默认布局。");
                return;
            }

            for (int i = 0; i < _layoutControls.Count; i++)
            {
                LayoutControl control = _layoutControls[i];
                LayoutEntry entry = data.entries.Find(item => item.id == control.Id);
                if (entry == null || float.IsNaN(entry.scale) || float.IsInfinity(entry.scale))
                {
                    Debug.LogError($"[MobileGameplayControls] 手机按键布局缺少有效配置：{control.DisplayName}");
                    return;
                }
            }

            ApplyLayout(data.entries);
        }

        private static RectTransform CreateCircle(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject circleObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UICircleGraphic));
            circleObject.transform.SetParent(parent, false);
            circleObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = circleObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            UICircleGraphic graphic = circleObject.GetComponent<UICircleGraphic>();
            graphic.color = color;
            return rect;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            rectObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void CreateLabel(RectTransform parent, string content, int fontSize)
        {
            GameObject labelObject = new GameObject(
                "文字",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Shadow));
            labelObject.transform.SetParent(parent, false);
            labelObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 4f);
            rect.offsetMax = new Vector2(-6f, -4f);

            Text text = labelObject.GetComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = TextColor;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;

            Shadow shadow = labelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void AddOutline(Graphic graphic)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        private static void Center(RectTransform rect, Vector2 position)
        {
            Anchor(rect, new Vector2(0.5f, 0.5f), position);
        }

        private void RefreshSafeArea(bool force)
        {
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == _lastSafeArea)
                return;
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            _lastSafeArea = safeArea;
            _safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            _safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            ClampAllControls();
        }

        private void HandleJoystickChanged(Vector2 movement, bool runRequested)
        {
            ResolvePlayerReferences();
            _inputHandler?.SetMobileMovement(movement, runRequested);
        }

        private void BeginAttack()
        {
            ResolvePlayerReferences();
            _inputHandler?.BeginMobileAttack();
        }

        private void EndAttack()
        {
            ResolvePlayerReferences();
            _inputHandler?.EndMobileAttack();
        }

        private void RegisterDodge()
        {
            ResolvePlayerReferences();
            _inputHandler?.RegisterMobileDodge();
        }

        private void SetSprint(bool requested)
        {
            ResolvePlayerReferences();
            _inputHandler?.SetMobileSprint(requested);
        }

        private void RegisterJump()
        {
            ResolvePlayerReferences();
            _inputHandler?.RegisterMobileJump();
        }

        private void RegisterSkill(int slotIndex)
        {
            ResolvePlayerReferences();
            _inputHandler?.RegisterMobileSkill(slotIndex);
        }

        private void ToggleAttackMode()
        {
            ResolvePlayerReferences();
            _playerController?.ToggleAttackMode();
        }

        private void ToggleAim()
        {
            ResolvePlayerReferences();
            _playerController?.ToggleAimMode();
        }

        private void UseHealthPotion()
        {
            ResolvePlayerReferences();
            _playerController?.UseHealthPotion();
        }

        private void UseManaPotion()
        {
            ResolvePlayerReferences();
            _playerController?.UseManaPotion();
        }
    }

    internal sealed class MobileHoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action<bool> _onChanged;
        private int _pointerId = int.MinValue;

        public void Initialize(Action<bool> onChanged)
        {
            _onChanged = onChanged;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || _pointerId != int.MinValue)
                return;

            _pointerId = eventData.pointerId;
            _onChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId == _pointerId)
                Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (_pointerId == int.MinValue)
                return;

            _pointerId = int.MinValue;
            _onChanged?.Invoke(false);
        }
    }

    internal sealed class MobileControlLayoutHandle : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        private MobileGameplayControls _owner;
        private string _controlId;
        private Image _image;

        public void Initialize(MobileGameplayControls owner, string controlId)
        {
            _owner = owner;
            _controlId = controlId;
            _image = GetComponent<Image>();
        }

        public void SetSelected(bool selected)
        {
            if (_image != null)
            {
                _image.color = selected
                    ? new Color(1f, 0.72f, 0.18f, 0.28f)
                    : new Color(1f, 0.78f, 0.22f, 0.08f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _owner?.SelectLayoutControl(_controlId);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?.DragLayoutControl(_controlId, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        public void OnPointerClick(PointerEventData eventData)
        {
        }
    }

    internal sealed class MobileJoystickControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float DeadZone = 0.08f;
        private const float WalkRadiusFraction = 2f / 3f;

        private RectTransform _background;
        private RectTransform _knob;
        private Action<Vector2, bool> _onChanged;
        private int _pointerId = int.MinValue;

        public void Initialize(RectTransform background, RectTransform knob, Action<Vector2, bool> onChanged)
        {
            _background = background;
            _knob = knob;
            _onChanged = onChanged;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue)
                return;

            _pointerId = eventData.pointerId;
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
                UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
                ResetStick();
        }

        private void OnDisable()
        {
            ResetStick();
        }

        public void ResetStick()
        {
            _pointerId = int.MinValue;
            if (_knob != null)
                _knob.anchoredPosition = Vector2.zero;
            _onChanged?.Invoke(Vector2.zero, false);
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (_background == null || _knob == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _background,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            float radius = Mathf.Min(_background.rect.width, _background.rect.height) * 0.5f;
            if (radius <= 0f)
                return;

            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, radius);
            _knob.anchoredPosition = clampedPoint;

            float normalizedDistance = Mathf.Clamp01(clampedPoint.magnitude / radius);
            if (normalizedDistance <= DeadZone)
            {
                _onChanged?.Invoke(Vector2.zero, false);
                return;
            }

            bool runRequested = normalizedDistance > WalkRadiusFraction;
            float outputMagnitude = runRequested
                ? 1f
                : Mathf.InverseLerp(DeadZone, WalkRadiusFraction, normalizedDistance);
            _onChanged?.Invoke(clampedPoint.normalized * outputMagnitude, runRequested);
        }
    }

    internal sealed class MobileAttackControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action _onPressed;
        private Action _onReleased;
        private int _pointerId = int.MinValue;

        public void Initialize(Action onPressed, Action onReleased)
        {
            _onPressed = onPressed;
            _onReleased = onReleased;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue)
                return;

            _pointerId = eventData.pointerId;
            _onPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
                return;

            _pointerId = int.MinValue;
            _onReleased?.Invoke();
        }

        private void OnDisable()
        {
            _pointerId = int.MinValue;
        }
    }
}
