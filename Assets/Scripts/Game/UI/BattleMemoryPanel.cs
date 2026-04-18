using System;
using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using Game.Saving;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 战斗回忆面板：通关后从关卡内菜单进入，选择要重新挑战的 Boss，纯挑战、无奖励。
    /// </summary>
    public class BattleMemoryPanel : BasePanel
    {
        private const string ContentRootName = "Content";
        private const string EmptyTextName = "Txt_Empty";

        private readonly UiGameObjectPool _rowPool = new UiGameObjectPool();
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, string> _bossDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private RectTransform _contentRoot;
        private Text _emptyText;
        private GameObject _rowPrefab;

        protected override void Awake()
        {
            base.Awake();
            _contentRoot = transform.Find($"Window/Scroll View/Viewport/{ContentRootName}") as RectTransform;
            _emptyText = transform.Find($"Window/Scroll View/Viewport/{EmptyTextName}")?.GetComponent<Text>();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.BattleMemory));
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            RefreshBossList();
        }

        public override void HideMe() => gameObject.SetActive(false);

        private void RefreshBossList()
        {
            ClearRows();
            _bossDisplayNames.Clear();

            if (_contentRoot == null)
                return;

            _rowPrefab ??= Resources.Load<GameObject>("UI/BattleMemoryBossRow");
            if (_rowPrefab == null)
            {
                Debug.LogWarning("[BattleMemoryPanel] 未找到战斗回忆列表项预制体 UI/BattleMemoryBossRow。");
                SetEmptyState("缺少战斗回忆列表项预制体");
                return;
            }

            List<BattleMemoryBossOption> options = BuildBossOptions();
            if (options.Count == 0)
            {
                SetEmptyState("暂无可回忆的 Boss");
                return;
            }

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(false);

            for (int i = 0; i < options.Count; i++)
            {
                BattleMemoryBossOption option = options[i];
                GameObject rowObject = _rowPool.Acquire(_rowPrefab, _contentRoot, $"BossRow_{option.bossId}");
                if (rowObject == null)
                    continue;

                BattleMemoryBossRow row = rowObject.GetComponent<BattleMemoryBossRow>();
                if (row != null)
                    row.Bind(option.bossId, option.displayName, EnterBattleMemory);

                _bossDisplayNames[option.bossId] = option.displayName;
                _spawnedRows.Add(rowObject);
            }
        }

        private void EnterBattleMemory(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return;

            string displayName = _bossDisplayNames.TryGetValue(bossId, out string resolvedName)
                ? resolvedName
                : bossId;
            if (!BattleMemoryRuntimeContext.BeginChallenge(bossId, displayName))
            {
                Debug.LogWarning($"[BattleMemoryPanel] 无法进入战斗回忆：{bossId}");
                return;
            }

            UIManager.GetInstance().HidePanel(PanelNames.BattleMemory);
            UIManager.GetInstance().HidePanel(PanelNames.Menu);
        }

        private List<BattleMemoryBossOption> BuildBossOptions()
        {
            List<BattleMemoryBossOption> options = new List<BattleMemoryBossOption>();
            RunData run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            EnemySpawnVariantCatalog catalog = EnemySpawnRuntime.BuildVariantCatalog();
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (run?.defeatedBossIds != null)
            {
                for (int i = 0; i < run.defeatedBossIds.Count; i++)
                {
                    string bossId = run.defeatedBossIds[i];
                    if (string.IsNullOrWhiteSpace(bossId))
                        continue;

                    bossId = bossId.Trim();
                    if (!visited.Add(bossId))
                        continue;

                    EnemySpawnVariantInfo variant = catalog?.GetVariant(bossId);
                    options.Add(new BattleMemoryBossOption
                    {
                        bossId = bossId,
                        displayName = string.IsNullOrWhiteSpace(variant?.displayName) ? bossId : variant.displayName.Trim(),
                    });
                }
            }

            if (options.Count > 0)
                return options;

            List<EnemySpawnVariantInfo> variants = catalog?.GetVariants(EnemyType.Boss);
            if (variants == null)
                return options;

            for (int i = 0; i < variants.Count; i++)
            {
                EnemySpawnVariantInfo variant = variants[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.spawnId))
                    continue;

                string bossId = variant.spawnId.Trim();
                if (!visited.Add(bossId))
                    continue;

                options.Add(new BattleMemoryBossOption
                {
                    bossId = bossId,
                    displayName = string.IsNullOrWhiteSpace(variant.displayName) ? bossId : variant.displayName.Trim(),
                });
            }

            return options;
        }

        private void SetEmptyState(string message)
        {
            if (_emptyText == null)
                return;

            _emptyText.text = message;
            _emptyText.gameObject.SetActive(true);
        }

        private void ClearRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
                _rowPool.Release(_spawnedRows[i], _contentRoot);

            _spawnedRows.Clear();
        }

        private void OnDestroy()
        {
            _rowPool.Clear();
        }

        private struct BattleMemoryBossOption
        {
            public string bossId;
            public string displayName;
        }
    }
}
