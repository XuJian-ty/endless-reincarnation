using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class FinalBossHealthBarController : MonoBehaviour
    {
        private const string FinalBossBarRootName = "FinalBossHealthBarRoot";
        private const string FillName = "Fill";
        private const string BossNameTextName = "Text_Name";
        private const string BossNameTextFallbackName = "Text_BossName";
        private const string BossValueTextName = "Text_Value";
        private const string BossValueTextFallbackName = "Text_HP";
        private static readonly Color BossBarColor = new Color(0.86f, 0.22f, 0.22f, 0.95f);

        private GameObject _rootObject;
        private Slider _slider;
        private Image _fillImage;
        private Text _nameText;
        private Text _valueText;

        private void Awake()
        {
            EnsureUi();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void LateUpdate()
        {
            EnemyController finalBoss = FindActiveFinalBoss();
            bool shouldShow = finalBoss != null && finalBoss.IsAlive && finalBoss.IsInCombatState;
            SetVisible(shouldShow);
            if (!shouldShow)
                return;

            float maxHp = Mathf.Max(1f, finalBoss.MaxHp);
            float normalizedHp = Mathf.Clamp01(finalBoss.CurrentHp / maxHp);
            if (_slider != null)
                _slider.value = normalizedHp;
            if (_fillImage != null)
                _fillImage.fillAmount = normalizedHp;
            if (_nameText != null)
                _nameText.text = ResolveBossDisplayName(finalBoss);
            if (_valueText != null)
                _valueText.text = $"{finalBoss.CurrentHp:F0}/{maxHp:F0}";
        }

        private void EnsureUi()
        {
            Transform existingRoot = FindNamedDescendant(transform, FinalBossBarRootName);
            if (existingRoot != null)
                _rootObject = existingRoot.gameObject;

            if (_rootObject == null)
                _rootObject = CreateDefaultRoot();

            _slider = _rootObject.GetComponent<Slider>();
            if (_slider == null)
                _slider = _rootObject.GetComponentInChildren<Slider>(true);

            _fillImage = ResolveFillImage();
            _nameText = ResolveNameText();
            _valueText = ResolveValueText();

            if (_fillImage == null)
                _fillImage = CreateFill(_rootObject.transform);

            if (_nameText == null)
                _nameText = CreateText(_rootObject.transform, BossNameTextName, TextAnchor.MiddleLeft);

            if (_valueText == null)
                _valueText = CreateText(_rootObject.transform, BossValueTextName, TextAnchor.MiddleRight);

            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.color = BossBarColor;
            _fillImage.raycastTarget = false;
            if (_slider != null)
            {
                _slider.direction = Slider.Direction.LeftToRight;
                _slider.minValue = 0f;
                _slider.maxValue = 1f;
                _slider.wholeNumbers = false;
                _slider.interactable = false;
            }
            _nameText.raycastTarget = false;
            _valueText.raycastTarget = false;
        }

        private GameObject CreateDefaultRoot()
        {
            GameObject root = new GameObject(FinalBossBarRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(transform, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(560f, 58f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;
            return root;
        }

        private static Image CreateFill(Transform parent)
        {
            GameObject fillObject = new GameObject(FillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(parent, false);

            RectTransform rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);

            return fillObject.GetComponent<Image>();
        }

        private static Text CreateText(Transform parent, string name, TextAnchor alignment)
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (alignment == TextAnchor.MiddleLeft)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(16f, 0f);
                rect.offsetMax = new Vector2(-8f, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(8f, 0f);
                rect.offsetMax = new Vector2(-16f, 0f);
            }

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = alignment == TextAnchor.MiddleLeft ? 22 : 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private void SetVisible(bool visible)
        {
            if (_rootObject != null && _rootObject.activeSelf != visible)
                _rootObject.SetActive(visible);
        }

        private static EnemyController FindActiveFinalBoss()
        {
            IReadOnlyList<EnemyController> activeEnemies = EnemyController.ActiveEnemies;
            if (activeEnemies == null)
                return null;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy != null
                    && enemy.IsAlive
                    && enemy.EnemyCategory == EnemyType.Boss
                    && enemy.GetComponent<LevelBossVisualMarker>() != null)
                {
                    return enemy;
                }
            }

            return null;
        }

        private static string ResolveBossDisplayName(EnemyController enemy)
        {
            if (enemy == null)
                return "最终Boss";

            if (enemy.Archetype != null && !string.IsNullOrWhiteSpace(enemy.Archetype.displayName))
                return enemy.Archetype.displayName;

            return string.IsNullOrWhiteSpace(enemy.EnemyId) ? "最终Boss" : enemy.EnemyId;
        }

        private Image ResolveFillImage()
        {
            Transform fillTransform = FindNamedDescendant(_rootObject.transform, FillName);
            if (fillTransform != null)
            {
                Image namedFill = fillTransform.GetComponent<Image>();
                if (namedFill != null)
                    return namedFill;
            }

            if (_slider != null && _slider.fillRect != null)
            {
                Image sliderFill = _slider.fillRect.GetComponent<Image>();
                if (sliderFill != null)
                    return sliderFill;
            }

            return null;
        }

        private Text ResolveNameText()
        {
            Transform nameTransform = FindNamedDescendant(_rootObject.transform, BossNameTextName);
            if (nameTransform == null)
                nameTransform = FindNamedDescendant(_rootObject.transform, BossNameTextFallbackName);

            if (nameTransform != null)
            {
                Text text = nameTransform.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            return null;
        }

        private Text ResolveValueText()
        {
            Transform valueTransform = FindNamedDescendant(_rootObject.transform, BossValueTextName);
            if (valueTransform == null)
                valueTransform = FindNamedDescendant(_rootObject.transform, BossValueTextFallbackName);

            if (valueTransform != null)
            {
                Text text = valueTransform.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            return null;
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
