using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ActorHeadHealthBar : MonoBehaviour
    {
        private const string EnemyHudRootName = "EnemyHUD";
        private const string EnemyBarName = "EnemyHealthBar";
        private const string CloneHudRootName = "PlayerCloneHUD";
        private const string CloneBarName = "PlayerCloneHealthBar";
        private const string FillAreaName = "Fill Area";
        private const string FillName = "Fill";
        private const string ValueTextName = "Text_HP";
        private const float DefaultEnemyHeightOffset = 2f;
        private const float DefaultCloneHeightOffset = 1.88f;

        private RectTransform _rootRect;
        private RectTransform _barRect;
        private RectTransform _fillAreaRect;
        private Canvas _canvas;
        private Image _fillImage;
        private Text _valueText;
        private float _heightOffset = 2f;
        private bool _initialized;
        private bool _usesExistingHud;

        public void Configure()
        {
            EnsureUi();
            RefreshHeightOffset();
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
            if (_rootRect != null && !_usesExistingHud)
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
            if (!_usesExistingHud)
                ApplyStableScale();
        }

        private void EnsureUi()
        {
            if (_initialized && _rootRect != null && _fillImage != null)
                return;

            ResolveOrCreateRoot();
            ResolveOrCreateBarParts();
            if (_rootRect == null || _barRect == null || _fillImage == null)
                return;

            _canvas = _rootRect.GetComponent<Canvas>();
            if (_canvas == null)
                return;

            if (_usesExistingHud)
            {
                EnsureExistingHudPartsActive();
            }

            RefreshValueText();
            _initialized = true;
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
            if (TryResolveCloneHeightOffset(out float cloneHeightOffset))
                return cloneHeightOffset;

            if (TryResolveEnemyHeightOffset(out float enemyHeightOffset))
                return enemyHeightOffset;

            if (TryGetBounds(GetComponentsInChildren<Renderer>(true), out Bounds rendererBounds))
                return Mathf.Max(1.2f, rendererBounds.max.y - transform.position.y);

            if (TryGetBounds(GetComponentsInChildren<Collider>(true), out Bounds colliderBounds))
                return Mathf.Max(1.2f, colliderBounds.max.y - transform.position.y);

            return DefaultEnemyHeightOffset;
        }

        private bool TryResolveCloneHeightOffset(out float heightOffset)
        {
            heightOffset = 0f;
            PlayerCloneActor cloneActor = GetComponent<PlayerCloneActor>();
            if (cloneActor == null)
                return false;

            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                float top = controller.center.y + controller.height * 0.5f;
                heightOffset = Mathf.Max(1.2f, top - 0.12f);
                return true;
            }

            heightOffset = DefaultCloneHeightOffset;
            return true;
        }

        private bool TryResolveEnemyHeightOffset(out float heightOffset)
        {
            heightOffset = 0f;
            EnemyController enemyController = GetComponent<EnemyController>();
            if (enemyController == null)
                return false;

            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null)
            {
                float top = rootCollider.bounds.max.y - transform.position.y;
                heightOffset = Mathf.Max(DefaultEnemyHeightOffset, top);
                return true;
            }

            heightOffset = DefaultEnemyHeightOffset;
            return true;
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

            Transform existingRoot = FindNamedDescendant(transform, ResolveExistingHudRootName());
            if (existingRoot == null)
                return;

            _rootRect = existingRoot as RectTransform;
            _usesExistingHud = _rootRect != null;
        }

        private void ResolveOrCreateBarParts()
        {
            if (_rootRect == null)
                return;

            Transform barTransform = FindNamedDescendant(_rootRect, ResolveExistingBarName());
            _barRect = barTransform as RectTransform;
            if (_barRect == null)
                return;

            _barRect.localRotation = Quaternion.identity;

            Transform fillAreaTransform = FindNamedDescendant(_barRect, FillAreaName);
            _fillAreaRect = fillAreaTransform as RectTransform;

            Transform fillTransform = FindNamedDescendant(_barRect, FillName);
            if (fillTransform != null)
                _fillImage = fillTransform.GetComponent<Image>();

            Transform valueTextTransform = FindNamedDescendant(_barRect, ValueTextName);
            if (valueTextTransform != null)
                _valueText = valueTextTransform.GetComponent<Text>();
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
