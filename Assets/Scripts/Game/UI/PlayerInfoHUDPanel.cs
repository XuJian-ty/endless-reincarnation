using System.Collections.Generic;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain;
using Game.GameFlow;
using Game.Presentation;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 玩家信息HUD面板：左上角常驻显示，不影响游戏操作。
    /// 优化：从 ILevelUIModel 取数，事件/脏标记驱动刷新，缓存控件引用，避免每帧全量 GetControl。
    /// </summary>
    public class PlayerInfoHUDPanel : BasePanel
    {
        private sealed class RuntimeCombatBar
        {
            public GameObject rootObject;
            public RectTransform rectTransform;
            public Image fillImage;
            public float worldHeightOffset;
        }

        private const string CombatBarsRootName = "CombatHealthBarsRoot";
        private const string HeadBarTemplateName = "HeadBarTemplate";
        private const string FillName = "Fill";
        private static readonly Color EnemyHeadBarColor = new Color(0.86f, 0.22f, 0.22f, 0.95f);
        private static readonly Color CloneHeadBarColor = new Color(0.26f, 0.72f, 1f, 0.95f);

        // ── 缓存控件（Awake 后填充）──────────────────────────────────────
        private Text _playerNameText;
        private Text _levelText;
        private Slider _hpSlider;
        private Text _hpText;
        private Slider _mpSlider;
        private Text _mpText;
        private Slider _expSlider;
        private Text _expText;
        private Text _levelIndexText;
        private Text _difficultyText;

        private bool _dirty = true;
        private float _lastHp, _lastMp, _lastExp, _lastExpToNext;
        private int _lastLevel;
        private int _lastLevelIndex;
        private int _lastDifficulty;
        private string _lastPlayerName;
        private RectTransform _panelRectTransform;
        private Canvas _rootCanvas;
        private RectTransform _combatBarsRoot;
        private RectTransform _headBarTemplate;
        private readonly Dictionary<EnemyController, RuntimeCombatBar> _enemyBars = new Dictionary<EnemyController, RuntimeCombatBar>();
        private readonly Dictionary<PlayerCloneActor, RuntimeCombatBar> _cloneBars = new Dictionary<PlayerCloneActor, RuntimeCombatBar>();
        private readonly List<EnemyController> _enemyBarCleanup = new List<EnemyController>();
        private readonly List<PlayerCloneActor> _cloneBarCleanup = new List<PlayerCloneActor>();

        protected override void Awake()
        {
            base.Awake();
            CacheControls();
            CacheCombatBarControls();
            if (GetComponent<FinalBossHealthBarController>() == null)
                gameObject.AddComponent<FinalBossHealthBarController>();
        }

        private void CacheControls()
        {
            _panelRectTransform = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _playerNameText = GetControl<Text>("Text_PlayerName");
            _levelText      = GetControl<Text>("Text_Level");
            _hpSlider       = GetControl<Slider>("Slider_HP");
            _hpText         = GetControl<Text>("Text_HP");
            _mpSlider       = GetControl<Slider>("Slider_MP");
            _mpText         = GetControl<Text>("Text_MP");
            _expSlider      = GetControl<Slider>("Slider_Exp");
            _expText        = GetControl<Text>("Text_Exp");
            _levelIndexText = GetControl<Text>("Text_LevelIndex");
            _difficultyText = GetControl<Text>("Text_Difficulty");
        }

        private void CacheCombatBarControls()
        {
            _combatBarsRoot = FindNamedDescendant(transform, CombatBarsRootName) as RectTransform;
            _headBarTemplate = FindNamedDescendant(transform, HeadBarTemplateName) as RectTransform;
            if (_headBarTemplate != null)
                _headBarTemplate.gameObject.SetActive(false);
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            _dirty = true;
            RefreshIfDirty();
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
            {
                model.SubscribeInventoryChanged(MarkDirty);
            }
        }

        private void OnDisable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
            {
                model.UnsubscribeInventoryChanged(MarkDirty);
            }

            ClearRuntimeCombatBars();
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            var model = LevelUIModelLocator.Get();
            if (model?.Player == null) return;

            var stats = model.Player.Stats;
            var run   = model.CurrentRun;
            float hp  = model.Player.CurrentHp;
            float mp  = model.Player.CurrentMp;
            float exp = model.Player.Exp.Exp;
            float expToNext = model.Player.Exp.ExpToNext;
            int level = model.Player.Exp.Level;
            int levelIndex = run?.levelIndex ?? 0;
            int difficulty = run?.difficulty ?? 0;
            string playerName = model.CurrentPlayerName ?? "玩家";

            if (!_dirty && _lastHp == hp && _lastMp == mp && _lastExp == exp && _lastExpToNext == expToNext
                && _lastLevel == level && _lastLevelIndex == levelIndex && _lastDifficulty == difficulty
                && _lastPlayerName == playerName)
                return;

            _lastHp = hp; _lastMp = mp; _lastExp = exp; _lastExpToNext = expToNext;
            _lastLevel = level; _lastLevelIndex = levelIndex; _lastDifficulty = difficulty;
            _lastPlayerName = playerName;
            _dirty = false;
            RefreshAll(model);
        }

        private void LateUpdate()
        {
        }

        private void RefreshIfDirty()
        {
            if (!_dirty) return;
            var model = LevelUIModelLocator.Get();
            if (model?.Player == null) return;
            RefreshAll(model);
            _dirty = false;
        }

        private void RefreshAll(ILevelUIModel model)
        {
            var player = model.Player;
            var stats  = player.Stats;
            var run    = model.CurrentRun;

            string displayName = model.CurrentPlayerName ?? "玩家";
            if (_playerNameText != null) _playerNameText.text = displayName;
            if (_levelText != null)      _levelText.text      = $"Lv.{player.Exp.Level}";

            if (_hpSlider != null) { _hpSlider.maxValue = stats.MaxHp; _hpSlider.value = player.CurrentHp; }
            if (_hpText != null)   _hpText.text = $"{player.CurrentHp:F0}/{stats.MaxHp:F0}";

            if (_mpSlider != null) { _mpSlider.maxValue = stats.MaxMp; _mpSlider.value = player.CurrentMp; }
            if (_mpText != null)   _mpText.text = $"{player.CurrentMp:F0}/{stats.MaxMp:F0}";

            int expCap = player.Exp.ExpToNext;
            if (_expSlider != null) { _expSlider.maxValue = expCap; _expSlider.value = player.Exp.Exp; }
            if (_expText != null)   _expText.text = $"{player.Exp.Exp}/{expCap}";

            if (_levelIndexText != null && run != null) _levelIndexText.text = $"关卡： {run.levelIndex}";
            if (_difficultyText != null && run != null) _difficultyText.text = $"难度： {run.difficulty}";
        }

        private void UpdateCombatBars()
        {
            if (!isActiveAndEnabled)
                return;

            EnsureCombatBarUi();
            UpdateEnemyHeadBars();
            UpdateCloneHeadBars();
        }

        private void EnsureCombatBarUi()
        {
            if (_panelRectTransform == null)
                _panelRectTransform = transform as RectTransform;

            if (_rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            if (_combatBarsRoot == null)
                _combatBarsRoot = CreateCombatBarsRoot();

            if (_headBarTemplate == null)
                _headBarTemplate = CreateHeadBarTemplate();
        }

        private void UpdateEnemyHeadBars()
        {
            IReadOnlyList<EnemyController> activeEnemies = EnemyController.ActiveEnemies;

            _enemyBarCleanup.Clear();
            foreach (KeyValuePair<EnemyController, RuntimeCombatBar> pair in _enemyBars)
            {
                EnemyController enemy = pair.Key;
                if (enemy == null || !enemy.IsAlive || IsFinalBoss(enemy))
                    _enemyBarCleanup.Add(enemy);
            }

            for (int i = 0; i < _enemyBarCleanup.Count; i++)
                RemoveEnemyBar(_enemyBarCleanup[i]);

            if (activeEnemies == null)
                return;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive || IsFinalBoss(enemy))
                    continue;

                RuntimeCombatBar bar = GetOrCreateEnemyBar(enemy);
                bool shouldShow = enemy.IsInCombatState;
                UpdateWorldBar(
                    bar,
                    enemy.transform,
                    enemy.CurrentHp,
                    enemy.MaxHp,
                    shouldShow,
                    EnemyHeadBarColor);
            }
        }

        private void UpdateCloneHeadBars()
        {
            IReadOnlyList<PlayerCloneActor> activeClones = PlayerCloneActor.ActiveClones;

            _cloneBarCleanup.Clear();
            foreach (KeyValuePair<PlayerCloneActor, RuntimeCombatBar> pair in _cloneBars)
            {
                PlayerCloneActor clone = pair.Key;
                if (clone == null || !clone.IsAlive)
                    _cloneBarCleanup.Add(clone);
            }

            for (int i = 0; i < _cloneBarCleanup.Count; i++)
                RemoveCloneBar(_cloneBarCleanup[i]);

            if (activeClones == null)
                return;

            for (int i = 0; i < activeClones.Count; i++)
            {
                PlayerCloneActor clone = activeClones[i];
                if (clone == null || !clone.IsAlive)
                    continue;

                RuntimeCombatBar bar = GetOrCreateCloneBar(clone);
                UpdateWorldBar(
                    bar,
                    clone.transform,
                    clone.CurrentHp,
                    clone.MaxHp,
                    true,
                    CloneHeadBarColor);
            }
        }

        private RuntimeCombatBar GetOrCreateEnemyBar(EnemyController enemy)
        {
            if (_enemyBars.TryGetValue(enemy, out RuntimeCombatBar existing) && existing != null)
                return existing;

            RuntimeCombatBar created = CreateRuntimeBar("EnemyHealthBar", EnemyHeadBarColor, enemy != null ? enemy.transform : null);
            _enemyBars[enemy] = created;
            return created;
        }

        private RuntimeCombatBar GetOrCreateCloneBar(PlayerCloneActor clone)
        {
            if (_cloneBars.TryGetValue(clone, out RuntimeCombatBar existing) && existing != null)
                return existing;

            RuntimeCombatBar created = CreateRuntimeBar("CloneHealthBar", CloneHeadBarColor, clone != null ? clone.transform : null);
            _cloneBars[clone] = created;
            return created;
        }

        private RuntimeCombatBar CreateRuntimeBar(string namePrefix, Color fillColor, Transform target)
        {
            EnsureCombatBarUi();
            if (_headBarTemplate == null || _combatBarsRoot == null)
                return null;

            GameObject instance = Instantiate(_headBarTemplate.gameObject, _combatBarsRoot);
            instance.name = $"{namePrefix}_{_combatBarsRoot.childCount}";
            instance.SetActive(true);

            RectTransform rect = instance.GetComponent<RectTransform>();
            Image fillImage = FindNamedDescendant(instance.transform, FillName)?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.color = fillColor;
                fillImage.raycastTarget = false;
            }

            DisableGraphicRaycasts(instance);

            return new RuntimeCombatBar
            {
                rootObject = instance,
                rectTransform = rect,
                fillImage = fillImage,
                worldHeightOffset = CalculateWorldHeightOffset(target),
            };
        }

        private void UpdateWorldBar(RuntimeCombatBar bar, Transform target, float currentHp, float maxHp, bool shouldShow, Color fillColor)
        {
            if (bar == null || bar.rootObject == null || target == null)
                return;

            if (!shouldShow)
            {
                bar.rootObject.SetActive(false);
                return;
            }

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null || _combatBarsRoot == null)
            {
                bar.rootObject.SetActive(false);
                return;
            }

            float heightOffset = bar.worldHeightOffset > 0f ? bar.worldHeightOffset : CalculateWorldHeightOffset(target);
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(target.position + Vector3.up * heightOffset);
            bool onscreen = screenPoint.z > 0f
                && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
            if (!onscreen)
            {
                bar.rootObject.SetActive(false);
                return;
            }

            Camera uiCamera = GetUiCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_combatBarsRoot, screenPoint, uiCamera, out Vector2 localPoint))
            {
                bar.rootObject.SetActive(false);
                return;
            }

            bar.rootObject.SetActive(true);
            if (bar.rectTransform != null)
                bar.rectTransform.anchoredPosition = localPoint;

            if (bar.fillImage != null)
            {
                bar.fillImage.color = fillColor;
                bar.fillImage.fillAmount = Mathf.Clamp01(currentHp / Mathf.Max(1f, maxHp));
            }
        }

        private static bool IsFinalBoss(EnemyController enemy)
        {
            return enemy != null
                && enemy.IsAlive
                && enemy.EnemyCategory == EnemyType.Boss
                && enemy.GetComponent<LevelBossVisualMarker>() != null;
        }

        private RectTransform CreateCombatBarsRoot()
        {
            RectTransform existing = FindNamedDescendant(transform, CombatBarsRootName) as RectTransform;
            if (existing != null)
                return existing;

            GameObject root = new GameObject(CombatBarsRootName, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return rect;
        }

        private RectTransform CreateHeadBarTemplate()
        {
            RectTransform existing = FindNamedDescendant(transform, HeadBarTemplateName) as RectTransform;
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                return existing;
            }

            if (_combatBarsRoot == null)
                _combatBarsRoot = CreateCombatBarsRoot();

            GameObject root = new GameObject(HeadBarTemplateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(_combatBarsRoot, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(88f, 12f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;

            GameObject fill = new GameObject(FillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            Image fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = EnemyHeadBarColor;
            fillImage.raycastTarget = false;

            root.SetActive(false);
            return rect;
        }

        private void RemoveEnemyBar(EnemyController enemy)
        {
            if (!_enemyBars.TryGetValue(enemy, out RuntimeCombatBar bar))
                return;

            DestroyRuntimeBar(bar);
            _enemyBars.Remove(enemy);
        }

        private void RemoveCloneBar(PlayerCloneActor clone)
        {
            if (!_cloneBars.TryGetValue(clone, out RuntimeCombatBar bar))
                return;

            DestroyRuntimeBar(bar);
            _cloneBars.Remove(clone);
        }

        private void ClearRuntimeCombatBars()
        {
            foreach (KeyValuePair<EnemyController, RuntimeCombatBar> pair in _enemyBars)
                DestroyRuntimeBar(pair.Value);
            foreach (KeyValuePair<PlayerCloneActor, RuntimeCombatBar> pair in _cloneBars)
                DestroyRuntimeBar(pair.Value);

            _enemyBars.Clear();
            _cloneBars.Clear();
        }

        private static void DestroyRuntimeBar(RuntimeCombatBar bar)
        {
            if (bar?.rootObject != null)
                Object.Destroy(bar.rootObject);
        }

        private float CalculateWorldHeightOffset(Transform target)
        {
            if (target == null)
                return 2f;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds bounds;
            if (TryEncapsulateBounds(renderers, out bounds))
                return Mathf.Max(1.2f, bounds.max.y - target.position.y + 0.18f);

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            if (TryEncapsulateBounds(colliders, out bounds))
                return Mathf.Max(1.2f, bounds.max.y - target.position.y + 0.18f);

            return 2f;
        }

        private static bool TryEncapsulateBounds<T>(T[] components, out Bounds bounds) where T : Component
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
