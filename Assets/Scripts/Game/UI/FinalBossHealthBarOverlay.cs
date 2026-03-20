using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using Game.Presentation;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class FinalBossHealthBarOverlay : BasePanel
    {
        private const string FinalBossBarRootName = "FinalBossHealthBarRoot";
        private const string FillName = "Fill";
        private const string BossNameTextName = "Text_BossName";
        private const string BossValueTextName = "Text_HP";
        private static readonly Color BossBarColor = new Color(0.86f, 0.22f, 0.22f, 0.95f);

        private GameObject _rootObject;
        private Slider _slider;
        private Image _fillImage;
        private Text _nameText;
        private Text _valueText;
        private bool _loggedMissingRootError;

        protected override void Awake()
        {
            base.Awake();
            EnsureUi();
            SetVisible(false);
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            EnsureUi();
            SetVisible(false);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
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
            {
                if (!_loggedMissingRootError)
                {
                    Debug.LogError("[FinalBossHealthBarOverlay] 预制件缺少 FinalBossHealthBarRoot，无法显示最终 Boss 血条。");
                    _loggedMissingRootError = true;
                }

                return;
            }
            if (_rootObject == null)
                return;

            _slider = _rootObject.GetComponent<Slider>();
            if (_slider == null)
                _slider = _rootObject.GetComponentInChildren<Slider>(true);

            _fillImage = ResolveFillImage();
            _nameText = ResolveNameText();
            _valueText = ResolveValueText();

            if (_fillImage != null)
            {
                _fillImage.type = Image.Type.Filled;
                _fillImage.fillMethod = Image.FillMethod.Horizontal;
                _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                _fillImage.color = BossBarColor;
                _fillImage.raycastTarget = false;
            }

            if (_slider != null)
            {
                _slider.direction = Slider.Direction.LeftToRight;
                _slider.minValue = 0f;
                _slider.maxValue = 1f;
                _slider.wholeNumbers = false;
                _slider.interactable = false;
            }
            if (_nameText != null)
                _nameText.raycastTarget = false;
            if (_valueText != null)
                _valueText.raycastTarget = false;

            DisableGraphicRaycasts(_rootObject);
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
            if (valueTransform != null)
            {
                Text text = valueTransform.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            return null;
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
