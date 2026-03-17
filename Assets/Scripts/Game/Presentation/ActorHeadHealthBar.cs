using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ActorHeadHealthBar : MonoBehaviour
    {
        private const string RootName = "HeadHealthBarRoot";
        private const string EnemyHudRootName = "EnemyHUD";
        private const string EnemyBarName = "EnemyHealthBar";
        private const string CloneHudRootName = "PlayerCloneHUD";
        private const string CloneBarName = "PlayerCloneHealthBar";
        private const string FillAreaName = "Fill Area";
        private const string FillName = "Fill";
        private const string ValueTextName = "Text_HP";
        private const string DefaultSortingLayerName = "Default";

        private static readonly Color DefaultBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

        private RectTransform _rootRect;
        private RectTransform _barRect;
        private RectTransform _fillAreaRect;
        private Canvas _canvas;
        private Image _backgroundImage;
        private Image _fillImage;
        private Text _valueText;
        private Color _fillColor = Color.red;
        private float _heightOffset = 2f;
        private bool _initialized;
        private bool _usesExistingHud;

        public void Configure(Color fillColor)
        {
            _fillColor = fillColor;
            EnsureUi();
            RefreshHeightOffset();
            ApplyVisualState();
        }

        public void SetVisible(bool visible)
        {
            EnsureUi();
            if (visible)
                EnsureExistingHudPartsActive();

            if (_rootRect != null && _rootRect.gameObject.activeSelf != visible)
                _rootRect.gameObject.SetActive(visible);
        }

        public void SetNormalized(float ratio)
        {
            EnsureUi();
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(ratio);

            RefreshValueText();
        }

        public void RefreshHeightOffset()
        {
            EnsureUi();
            _heightOffset = ResolveHeightOffset();
            if (_rootRect != null)
                _rootRect.localPosition = new Vector3(0f, _heightOffset, 0f);
        }

        private void Awake()
        {
            EnsureUi();
            RefreshHeightOffset();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_rootRect == null || !_rootRect.gameObject.activeSelf)
                return;

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
                return;

            _rootRect.rotation = worldCamera.transform.rotation;
            ApplyStableScale();
        }

        private void EnsureUi()
        {
            if (_initialized && _rootRect != null && _fillImage != null)
                return;

            ResolveOrCreateRoot();
            ResolveOrCreateBarParts();

            _canvas = _rootRect.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = _rootRect.gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerID = SortingLayer.NameToID(DefaultSortingLayerName);
            _canvas.sortingOrder = 60;

            if (_barRect == null)
                _barRect = _rootRect;

            _backgroundImage = _barRect.GetComponent<Image>();
            if (_backgroundImage == null)
                _backgroundImage = _barRect.gameObject.AddComponent<Image>();

            if (_backgroundImage.sprite == null)
                _backgroundImage.color = DefaultBackgroundColor;
            _backgroundImage.raycastTarget = false;

            if (_fillImage == null)
                _fillImage = CreateFill(_fillAreaRect != null ? _fillAreaRect : _barRect);

            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.raycastTarget = false;

            if (_usesExistingHud)
            {
                ApplyExistingHudLayout();
                EnsureExistingHudPartsActive();
            }

            ApplyVisualState();
            ApplyStableScale();
            RefreshValueText();
            _initialized = true;
        }

        private void ApplyVisualState()
        {
            if (_fillImage != null)
                _fillImage.color = _fillColor;

            if (_valueText != null)
                _valueText.raycastTarget = false;
        }

        private void ApplyStableScale()
        {
            if (_rootRect == null)
                return;

            Vector3 lossyScale = transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x) > 0.0001f ? 1f / Mathf.Abs(lossyScale.x) : 1f;
            float scaleY = Mathf.Abs(lossyScale.y) > 0.0001f ? 1f / Mathf.Abs(lossyScale.y) : 1f;
            float scaleZ = Mathf.Abs(lossyScale.z) > 0.0001f ? 1f / Mathf.Abs(lossyScale.z) : 1f;
            _rootRect.localScale = new Vector3(0.01f * scaleX, 0.01f * scaleY, 0.01f * scaleZ);
        }

        private float ResolveHeightOffset()
        {
            if (TryGetBounds(GetComponentsInChildren<Renderer>(true), out Bounds rendererBounds))
                return Mathf.Max(1.2f, rendererBounds.max.y - transform.position.y + 0.18f);

            if (TryGetBounds(GetComponentsInChildren<Collider>(true), out Bounds colliderBounds))
                return Mathf.Max(1.2f, colliderBounds.max.y - transform.position.y + 0.18f);

            return 2f;
        }

        private static bool TryGetBounds<T>(T[] components, out Bounds bounds) where T : Component
        {
            bounds = default;
            bool hasBounds = false;
            if (components == null)
                return false;

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                Bounds componentBounds = component switch
                {
                    Renderer renderer => renderer.bounds,
                    Collider collider => collider.bounds,
                    _ => default,
                };

                if (!hasBounds)
                {
                    bounds = componentBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(componentBounds);
                }
            }

            return hasBounds;
        }

        private static Camera ResolveWorldCamera()
        {
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            Camera fallback = Object.FindFirstObjectByType<Camera>();
            return fallback != null && fallback.isActiveAndEnabled ? fallback : null;
        }

        private void ResolveOrCreateRoot()
        {
            _rootRect = null;
            _barRect = null;
            _fillAreaRect = null;
            _fillImage = null;
            _valueText = null;
            _usesExistingHud = false;

            Transform existingRoot = transform.Find(RootName);
            if (existingRoot == null)
            {
                existingRoot = FindNamedDescendant(transform, ResolveExistingHudRootName());
                _usesExistingHud = existingRoot != null;
            }

            if (existingRoot != null)
                _rootRect = existingRoot as RectTransform;

            if (_rootRect != null)
                return;

            GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(Image));
            rootObject.transform.SetParent(transform, false);
            _rootRect = rootObject.GetComponent<RectTransform>();
            _rootRect.sizeDelta = new Vector2(88f, 12f);
        }

        private void ResolveOrCreateBarParts()
        {
            if (_rootRect == null)
                return;

            Transform barTransform = _usesExistingHud ? FindNamedDescendant(_rootRect, ResolveExistingBarName()) : _rootRect;
            _barRect = barTransform as RectTransform;
            if (_barRect == null)
                _barRect = _rootRect;

            Transform fillAreaTransform = FindNamedDescendant(_barRect, FillAreaName);
            _fillAreaRect = fillAreaTransform as RectTransform;

            Transform fillTransform = FindNamedDescendant(_barRect, FillName);
            if (fillTransform != null)
                _fillImage = fillTransform.GetComponent<Image>();

            Transform valueTextTransform = FindNamedDescendant(_barRect, ValueTextName);
            if (valueTextTransform != null)
                _valueText = valueTextTransform.GetComponent<Text>();

            if (_usesExistingHud && _valueText == null)
                _valueText = CreateValueText(_barRect);
        }

        private void ApplyExistingHudLayout()
        {
            if (_rootRect == null)
                return;

            _rootRect.sizeDelta = new Vector2(100f, 100f);

            if (_barRect != null && _barRect != _rootRect)
            {
                _barRect.anchorMin = new Vector2(0.5f, 0.5f);
                _barRect.anchorMax = new Vector2(0.5f, 0.5f);
                _barRect.pivot = new Vector2(0.5f, 0.5f);
                _barRect.localRotation = Quaternion.identity;
                _barRect.anchoredPosition = Vector2.zero;
                _barRect.sizeDelta = new Vector2(88f, 12f);
            }

            if (_fillAreaRect != null)
            {
                _fillAreaRect.anchorMin = Vector2.zero;
                _fillAreaRect.anchorMax = Vector2.one;
                _fillAreaRect.offsetMin = new Vector2(1f, 1f);
                _fillAreaRect.offsetMax = new Vector2(-1f, -1f);
            }

            if (_fillImage != null)
            {
                RectTransform fillRect = _fillImage.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            if (_valueText == null)
                return;

            RectTransform textRect = _valueText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 2f);
            textRect.sizeDelta = new Vector2(120f, 16f);

            if (_valueText.font == null)
                _valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _valueText.fontSize = 10;
            _valueText.alignment = TextAnchor.MiddleCenter;
            _valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _valueText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void EnsureExistingHudPartsActive()
        {
            if (!_usesExistingHud)
                return;

            SetGameObjectActive(_barRect);
            SetGameObjectActive(_fillAreaRect);

            if (_fillImage != null)
            {
                _fillImage.enabled = true;
                SetGameObjectActive(_fillImage.rectTransform);
            }

            if (_valueText != null)
            {
                _valueText.enabled = true;
                SetGameObjectActive(_valueText.rectTransform);
            }
        }

        private void RefreshValueText()
        {
            if (_valueText == null)
                return;

            EnemyController enemy = GetComponent<EnemyController>();
            if (enemy != null)
            {
                float maxHp = Mathf.Max(1f, enemy.MaxHp);
                _valueText.text = $"{enemy.CurrentHp:F0}/{maxHp:F0}";
                return;
            }

            PlayerCloneActor clone = GetComponent<PlayerCloneActor>();
            if (clone != null)
            {
                float maxHp = Mathf.Max(1f, clone.MaxHp);
                _valueText.text = $"{clone.CurrentHp:F0}/{maxHp:F0}";
            }
        }

        private static Image CreateFill(RectTransform parent)
        {
            if (parent == null)
                return null;

            GameObject fillObject = new GameObject(FillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(parent, false);

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            return fillObject.GetComponent<Image>();
        }

        private static Text CreateValueText(RectTransform parent)
        {
            if (parent == null)
                return null;

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject textObject = new GameObject(ValueTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Transform FindNamedDescendant(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == name)
                    return children[i];
            }

            return null;
        }

        private string ResolveExistingHudRootName()
        {
            return GetComponent<PlayerCloneActor>() != null ? CloneHudRootName : EnemyHudRootName;
        }

        private string ResolveExistingBarName()
        {
            return GetComponent<PlayerCloneActor>() != null ? CloneBarName : EnemyBarName;
        }

        private static void SetGameObjectActive(Component component)
        {
            if (component != null && component.gameObject != null && !component.gameObject.activeSelf)
                component.gameObject.SetActive(true);
        }
    }
}
