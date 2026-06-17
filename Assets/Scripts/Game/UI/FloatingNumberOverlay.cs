using System.Collections.Generic;
using Game;
using Game.Presentation;
using ProjectBase;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class FloatingNumberOverlay : BasePanel
    {
        private sealed class RuntimeCombatNumber
        {
            public GameObject rootObject;
            public RectTransform rectTransform;
            public TextMeshProUGUI text;
            public CanvasGroup canvasGroup;
            public string templateKey;
            public Vector2 baseAnchoredOffset;
            public Vector3 worldPosition;
            public float elapsed;
            public float lifetime;
            public float startScale;
            public float peakScale;
            public float endScale;
            public float riseDistance;
            public float horizontalDrift;
            public Vector3 baseScale;
        }

        private const string FloatingNumbersRootName = "FloatingNumbersRoot";
        private const string DamageTemplateName = "DamageTemplate";
        private const string CritDamageTemplateName = "CritDamageTemplate";
        private const string HealTemplateName = "HealTemplate";
        private const string ManaTemplateName = "ManaTemplate";
        private const float CombatNumberAppearDuration = 0.16f;

        private RectTransform _panelRectTransform;
        private Canvas _rootCanvas;
        private RectTransform _combatNumbersRoot;
        private RectTransform _damageTemplate;
        private RectTransform _critDamageTemplate;
        private RectTransform _healTemplate;
        private RectTransform _manaTemplate;
        private readonly List<RuntimeCombatNumber> _activeCombatNumbers = new List<RuntimeCombatNumber>();
        private readonly Dictionary<string, Stack<RuntimeCombatNumber>> _pooledCombatNumbers = new Dictionary<string, Stack<RuntimeCombatNumber>>();
        private bool _loggedMissingTemplateError;

        protected override void Awake()
        {
            base.Awake();
            _panelRectTransform = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            CacheTemplateReferences();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventCenter.GetInstance().AddEventListener<CombatNumberRequest>(GameEvents.CombatNumberRequested, OnCombatNumberRequested);
        }

        private void OnDisable()
        {
            EventCenter.GetInstance().RemoveEventListener<CombatNumberRequest>(GameEvents.CombatNumberRequested, OnCombatNumberRequested);
            ClearRuntimeCombatNumbers();
        }

        private void LateUpdate()
        {
            UpdateCombatNumbers();
        }

        private void OnCombatNumberRequested(CombatNumberRequest request)
        {
            if (!isActiveAndEnabled || request == null || request.amount <= 0f)
                return;

            if (!EnsureCombatNumberUi())
                return;

            RectTransform template = ResolveTemplate(request);
            RuntimeCombatNumber number = GetOrCreateCombatNumber(template);
            if (number == null)
                return;

            ConfigureCombatNumber(number, request);
            _activeCombatNumbers.Add(number);
        }

        private void UpdateCombatNumbers()
        {
            if (!isActiveAndEnabled || _activeCombatNumbers.Count <= 0)
                return;

            if (!EnsureCombatNumberUi())
                return;

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null || _combatNumbersRoot == null)
                return;

            Camera uiCamera = GetUiCamera();
            float deltaTime = Time.deltaTime;
            for (int i = _activeCombatNumbers.Count - 1; i >= 0; i--)
            {
                RuntimeCombatNumber number = _activeCombatNumbers[i];
                if (number == null)
                {
                    _activeCombatNumbers.RemoveAt(i);
                    continue;
                }

                number.elapsed += deltaTime;
                if (number.elapsed >= number.lifetime)
                {
                    RecycleCombatNumber(number);
                    _activeCombatNumbers.RemoveAt(i);
                    continue;
                }

                UpdateCombatNumberTransform(number, worldCamera, uiCamera);
                UpdateCombatNumberVisual(number);
            }
        }

        private bool EnsureCombatNumberUi()
        {
            if (_panelRectTransform == null)
                _panelRectTransform = transform as RectTransform;

            if (_rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            CacheTemplateReferences();
            if (_combatNumbersRoot == null ||
                _damageTemplate == null ||
                _critDamageTemplate == null ||
                _healTemplate == null ||
                _manaTemplate == null)
            {
                if (!_loggedMissingTemplateError)
                {
                    Debug.LogError("[FloatingNumberOverlay] 缺少必要的 TMP 飘字模板。需要在预制件中配置 FloatingNumbersRoot、DamageTemplate、CritDamageTemplate、HealTemplate、ManaTemplate。");
                    _loggedMissingTemplateError = true;
                }

                return false;
            }

            return true;
        }

        private void CacheTemplateReferences()
        {
            _combatNumbersRoot = FindNamedDescendant(transform, FloatingNumbersRootName) as RectTransform;
            _damageTemplate = FindNamedDescendant(transform, DamageTemplateName) as RectTransform;
            _critDamageTemplate = FindNamedDescendant(transform, CritDamageTemplateName) as RectTransform;
            _healTemplate = FindNamedDescendant(transform, HealTemplateName) as RectTransform;
            _manaTemplate = FindNamedDescendant(transform, ManaTemplateName) as RectTransform;

            SetTemplateInactive(_damageTemplate);
            SetTemplateInactive(_critDamageTemplate);
            SetTemplateInactive(_healTemplate);
            SetTemplateInactive(_manaTemplate);
        }

        private RectTransform ResolveTemplate(CombatNumberRequest request)
        {
            return request.kind switch
            {
                CombatNumberKind.Damage when request.isCritical => _critDamageTemplate,
                CombatNumberKind.Damage => _damageTemplate,
                CombatNumberKind.Heal => _healTemplate,
                _ => _manaTemplate,
            };
        }

        private RuntimeCombatNumber GetOrCreateCombatNumber(RectTransform template)
        {
            if (template == null || _combatNumbersRoot == null)
                return null;

            string templateKey = template.name;
            if (_pooledCombatNumbers.TryGetValue(templateKey, out Stack<RuntimeCombatNumber> pool) && pool.Count > 0)
            {
                RuntimeCombatNumber pooledNumber = pool.Pop();
                pooledNumber.baseAnchoredOffset = template.anchoredPosition;
                return pooledNumber;
            }

            GameObject instance = Instantiate(template.gameObject, _combatNumbersRoot);
            instance.name = templateKey.Replace("Template", "Number");

            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            TextMeshProUGUI text = instance.GetComponent<TextMeshProUGUI>();
            CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
            if (rectTransform == null || text == null || canvasGroup == null)
            {
                Debug.LogError($"[FloatingNumberOverlay] 模板 {templateKey} 缺少 RectTransform/TextMeshProUGUI/CanvasGroup，无法创建飘字实例。");
                Object.Destroy(instance);
                return null;
            }

            DisableGraphicRaycasts(instance);
            instance.SetActive(false);
            return new RuntimeCombatNumber
            {
                rootObject = instance,
                rectTransform = rectTransform,
                text = text,
                canvasGroup = canvasGroup,
                templateKey = templateKey,
                baseAnchoredOffset = template.anchoredPosition,
                baseScale = rectTransform.localScale,
            };
        }

        private void ConfigureCombatNumber(RuntimeCombatNumber number, CombatNumberRequest request)
        {
            number.worldPosition = request.worldPosition;
            number.elapsed = 0f;
            number.horizontalDrift = Random.Range(-26f, 26f);

            int displayValue = Mathf.Max(1, Mathf.RoundToInt(request.amount));
            number.text.text = request.kind == CombatNumberKind.Damage
                ? displayValue.ToString()
                : $"+{displayValue}";

            switch (request.kind)
            {
                case CombatNumberKind.Damage:
                    number.lifetime = request.isCritical ? 0.9f : 0.78f;
                    number.startScale = 0.62f;
                    number.peakScale = request.isCritical ? 1.18f : 1.08f;
                    number.endScale = request.isCritical ? 0.92f : 0.86f;
                    number.riseDistance = request.isCritical ? 88f : 68f;
                    break;
                case CombatNumberKind.Heal:
                    number.lifetime = 0.74f;
                    number.startScale = 0.64f;
                    number.peakScale = 1.06f;
                    number.endScale = 0.82f;
                    number.riseDistance = 60f;
                    break;
                default:
                    number.lifetime = 0.74f;
                    number.startScale = 0.64f;
                    number.peakScale = 1.04f;
                    number.endScale = 0.82f;
                    number.riseDistance = 58f;
                    break;
            }

            number.canvasGroup.alpha = 0f;
            number.rectTransform.localScale = Vector3.one * number.startScale;
            number.rootObject.SetActive(true);
        }

        private void UpdateCombatNumberTransform(RuntimeCombatNumber number, Camera worldCamera, Camera uiCamera)
        {
            if (number?.rectTransform == null)
                return;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(number.worldPosition);
            if (screenPoint.z <= 0f)
            {
                number.rootObject.SetActive(false);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_combatNumbersRoot, screenPoint, uiCamera, out Vector2 localPoint))
            {
                number.rootObject.SetActive(false);
                return;
            }

            number.rootObject.SetActive(true);
            float rise01 = Mathf.Clamp01(number.elapsed / Mathf.Max(0.01f, number.lifetime));
            float horizontalOffset = number.horizontalDrift * Mathf.Sin(rise01 * Mathf.PI * 0.85f);
            float verticalOffset = Mathf.Lerp(0f, number.riseDistance, EaseOutCubic(rise01));
            number.rectTransform.anchoredPosition = localPoint + number.baseAnchoredOffset + new Vector2(horizontalOffset, verticalOffset);
        }

        private static void UpdateCombatNumberVisual(RuntimeCombatNumber number)
        {
            if (number?.rectTransform == null || number.canvasGroup == null)
                return;

            float appear01 = Mathf.Clamp01(number.elapsed / CombatNumberAppearDuration);
            float fade01 = Mathf.Clamp01((number.elapsed - CombatNumberAppearDuration) / Mathf.Max(0.01f, number.lifetime - CombatNumberAppearDuration));
            float alpha = number.elapsed <= CombatNumberAppearDuration
                ? EaseOutCubic(appear01)
                : 1f - EaseInCubic(fade01);
            float scale = number.elapsed <= CombatNumberAppearDuration
                ? Mathf.Lerp(number.startScale, number.peakScale, EaseOutBack(appear01))
                : Mathf.Lerp(number.peakScale, number.endScale, EaseInCubic(fade01));

            number.canvasGroup.alpha = Mathf.Clamp01(alpha);
            number.rectTransform.localScale = number.baseScale * scale;
        }

        private void RecycleCombatNumber(RuntimeCombatNumber number)
        {
            if (number?.rootObject == null)
                return;

            number.elapsed = 0f;
            number.rootObject.SetActive(false);
            if (!_pooledCombatNumbers.TryGetValue(number.templateKey, out Stack<RuntimeCombatNumber> pool))
            {
                pool = new Stack<RuntimeCombatNumber>();
                _pooledCombatNumbers[number.templateKey] = pool;
            }

            pool.Push(number);
        }

        private void ClearRuntimeCombatNumbers()
        {
            for (int i = 0; i < _activeCombatNumbers.Count; i++)
                DestroyRuntimeCombatNumber(_activeCombatNumbers[i]);

            foreach (KeyValuePair<string, Stack<RuntimeCombatNumber>> pair in _pooledCombatNumbers)
            {
                foreach (RuntimeCombatNumber number in pair.Value)
                    DestroyRuntimeCombatNumber(number);
            }

            _activeCombatNumbers.Clear();
            _pooledCombatNumbers.Clear();
        }

        private static void DestroyRuntimeCombatNumber(RuntimeCombatNumber number)
        {
            if (number?.rootObject != null)
                Object.Destroy(number.rootObject);
        }

        private Camera ResolveWorldCamera()
        {
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            Camera fallback = Object.FindFirstObjectByType<Camera>();
            return fallback != null && fallback.isActiveAndEnabled ? fallback : null;
        }

        private Camera GetUiCamera()
        {
            if (_rootCanvas == null || _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return _rootCanvas.worldCamera;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float overshoot = 1.70158f;
            float p = t - 1f;
            return 1f + (overshoot + 1f) * p * p * p + overshoot * p * p;
        }

        private static void DisableGraphicRaycasts(GameObject root)
        {
            if (root == null)
                return;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }

        private static void SetTemplateInactive(RectTransform template)
        {
            if (template != null)
                template.gameObject.SetActive(false);
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
    }
}
