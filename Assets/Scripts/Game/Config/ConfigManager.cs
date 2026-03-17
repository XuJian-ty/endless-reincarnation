using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using ProjectBase;

namespace Game
{
    /// <summary>
    /// Loads ScriptableObject configs from Resources/配置 on demand.
    /// </summary>
    public class ConfigManager : BaseManager<ConfigManager>
    {
        private const string ConfigPathPrefix = "配置/";
        private const string ConfigFolderName = "配置";

        private EnemyStatsDatabaseSO _enemyStatsDatabase;
        private SharedSkillDatabaseSO _sharedSkillDatabase;
        private LevelConfigDatabaseSO _levelConfigDatabase;
        private WeaponDatabaseSO _weaponDatabase;
        private LevelGrowthSO _levelGrowth;
        private DifficultyScalingSO _difficultyScaling;
        private PlayerCloneAndLevelBossVisualConfigSO _playerCloneAndLevelBossVisualConfig;
        private PlayerCloneAIConfigSO _playerCloneAIConfig;
        private ShopPriceConfigSO _shopPriceConfig;
        private PotionConfigSO _potionConfig;
        private SkillConfigDatabaseSO _skillConfigDatabase;
        private CharacterAnimationLibrarySO _characterAnimationLibrary;
        private ItemDisplayDatabaseSO _itemDisplayDatabase;
        private SlotBackgroundConfigSO _slotBackgroundConfig;
        private KeyRebindConfigSO _keyRebindConfig;
        private BackpackUIConfigSO _backpackUIConfig;
        private BuffConfigSO _buffConfig;
        private PlayerStateRuleDatabaseSO _playerStateRuleDatabase;
        private EnemyArchetypeSO[] _enemyArchetypes;
        private Dictionary<string, EnemyArchetypeSO> _enemyArchetypesById;
        private Dictionary<EnemyType, List<EnemyArchetypeSO>> _enemyArchetypesByType;
        private bool _enemyArchetypesCached;

        public EnemyStatsDatabaseSO GetEnemyStatsDatabase()
        {
            if (_enemyStatsDatabase == null)
                _enemyStatsDatabase = Resources.Load<EnemyStatsDatabaseSO>(ConfigPathPrefix + "敌人属性库");
            return _enemyStatsDatabase;
        }

        public EnemyArchetypeSO GetEnemyArchetype(EnemyType type)
        {
            EnsureEnemyArchetypeCache();
            if (_enemyArchetypesByType != null && _enemyArchetypesByType.TryGetValue(type, out var entries) && entries.Count > 0)
                return entries[0];
            return null;
        }

        public EnemyArchetypeSO GetEnemyArchetype(string enemyId)
        {
            EnsureEnemyArchetypeCache();
            if (string.IsNullOrWhiteSpace(enemyId) || _enemyArchetypesById == null)
                return null;

            _enemyArchetypesById.TryGetValue(NormalizeId(enemyId), out var archetype);
            return archetype;
        }

        public int CountEnemyArchetypesByType(EnemyType type)
        {
            EnsureEnemyArchetypeCache();
            return _enemyArchetypesByType != null && _enemyArchetypesByType.TryGetValue(type, out var entries)
                ? entries.Count
                : 0;
        }

        public SharedSkillDatabaseSO GetSkillDatabase()
        {
            if (_sharedSkillDatabase == null)
            {
                _sharedSkillDatabase = Resources.Load<SharedSkillDatabaseSO>(ConfigPathPrefix + "技能库");
                if (_sharedSkillDatabase == null)
                    _sharedSkillDatabase = SharedSkillDatabaseDefaults.CreateRuntimeDefault();
            }
            return _sharedSkillDatabase;
        }

        public SharedSkillDatabaseSO GetSharedSkillDatabase()
        {
            return GetSkillDatabase();
        }

        public LevelConfigDatabaseSO GetLevelConfigDatabase()
        {
            if (_levelConfigDatabase == null)
                _levelConfigDatabase = Resources.Load<LevelConfigDatabaseSO>(ConfigPathPrefix + "关卡配置库");
            return _levelConfigDatabase;
        }

        public WeaponDatabaseSO GetWeaponDatabase()
        {
            if (_weaponDatabase == null)
                _weaponDatabase = Resources.Load<WeaponDatabaseSO>(ConfigPathPrefix + "武器库");
            return _weaponDatabase;
        }

        public LevelGrowthSO GetLevelGrowth()
        {
            if (_levelGrowth == null)
                _levelGrowth = Resources.Load<LevelGrowthSO>(ConfigPathPrefix + "玩家成长属性库");
            return _levelGrowth;
        }

        public DifficultyScalingSO GetDifficultyScaling()
        {
            if (_difficultyScaling == null)
                _difficultyScaling = Resources.Load<DifficultyScalingSO>(ConfigPathPrefix + "难度系数");
            return _difficultyScaling;
        }

        public PlayerCloneAndLevelBossVisualConfigSO GetPlayerCloneAndLevelBossVisualConfig()
        {
            if (_playerCloneAndLevelBossVisualConfig == null)
                _playerCloneAndLevelBossVisualConfig = Resources.Load<PlayerCloneAndLevelBossVisualConfigSO>(ConfigPathPrefix + "玩家分身和最终Boss视觉配置");
            return _playerCloneAndLevelBossVisualConfig;
        }

        public PlayerCloneAIConfigSO GetPlayerCloneAIConfig()
        {
            if (_playerCloneAIConfig == null)
                _playerCloneAIConfig = Resources.Load<PlayerCloneAIConfigSO>(ConfigPathPrefix + "分身AI配置");
            return _playerCloneAIConfig;
        }

        public ShopPriceConfigSO GetShopPriceConfig()
        {
            if (_shopPriceConfig == null)
                _shopPriceConfig = Resources.Load<ShopPriceConfigSO>(ConfigPathPrefix + "商店价格");
            return _shopPriceConfig;
        }

        public PotionConfigSO GetPotionConfig()
        {
            if (_potionConfig == null)
                _potionConfig = Resources.Load<PotionConfigSO>(ConfigPathPrefix + "药水效果");
            return _potionConfig;
        }

        public SkillConfigDatabaseSO GetSkillConfigDatabase()
        {
            if (_skillConfigDatabase == null)
                _skillConfigDatabase = Resources.Load<SkillConfigDatabaseSO>(ConfigPathPrefix + "玩家动作及技能配置库");
            return _skillConfigDatabase;
        }

        public CharacterAnimationLibrarySO GetCharacterAnimationLibrary()
        {
            if (_characterAnimationLibrary == null)
                _characterAnimationLibrary = Resources.Load<CharacterAnimationLibrarySO>(ConfigPathPrefix + "动画库");
            return _characterAnimationLibrary;
        }

        public ItemDisplayDatabaseSO GetItemDisplayDatabase()
        {
            if (_itemDisplayDatabase == null)
                _itemDisplayDatabase = Resources.Load<ItemDisplayDatabaseSO>(ConfigPathPrefix + "物品显示配置");
            return _itemDisplayDatabase;
        }

        public SlotBackgroundConfigSO GetSlotBackgroundConfig()
        {
            if (_slotBackgroundConfig == null)
                _slotBackgroundConfig = Resources.Load<SlotBackgroundConfigSO>(ConfigPathPrefix + "格子背景配置");
            return _slotBackgroundConfig;
        }

        public KeyRebindConfigSO GetKeyRebindConfig()
        {
            if (_keyRebindConfig == null)
                _keyRebindConfig = Resources.Load<KeyRebindConfigSO>(ConfigPathPrefix + "按键重绑定配置");
            return _keyRebindConfig;
        }

        public BackpackUIConfigSO GetBackpackUIConfig()
        {
            if (_backpackUIConfig == null)
                _backpackUIConfig = Resources.Load<BackpackUIConfigSO>(ConfigPathPrefix + "背包UI配置");
            return _backpackUIConfig;
        }

        public BuffConfigSO GetBuffConfig()
        {
            if (_buffConfig == null)
                _buffConfig = Resources.Load<BuffConfigSO>(ConfigPathPrefix + "Buff配置");
            return _buffConfig;
        }

        public PlayerStateRuleDatabaseSO GetPlayerStateRuleDatabase()
        {
            if (_playerStateRuleDatabase == null)
                _playerStateRuleDatabase = Resources.Load<PlayerStateRuleDatabaseSO>(ConfigPathPrefix + "玩家状态规则");
            return _playerStateRuleDatabase;
        }

        private void EnsureEnemyArchetypeCache()
        {
            if (_enemyArchetypesCached)
                return;

            _enemyArchetypesCached = true;
            _enemyArchetypes = Resources.LoadAll<EnemyArchetypeSO>(ConfigFolderName);
            _enemyArchetypesById = new Dictionary<string, EnemyArchetypeSO>(StringComparer.Ordinal);
            _enemyArchetypesByType = new Dictionary<EnemyType, List<EnemyArchetypeSO>>();

            if (_enemyArchetypes == null)
                return;

            for (int i = 0; i < _enemyArchetypes.Length; i++)
            {
                var archetype = _enemyArchetypes[i];
                if (archetype == null)
                    continue;

                string enemyId = NormalizeId(archetype.GetResolvedEnemyId());
                if (!string.IsNullOrEmpty(enemyId))
                {
                    if (_enemyArchetypesById.ContainsKey(enemyId))
                        Debug.LogWarning($"[Config] 检测到重复 enemyId 的敌人行为配置：{enemyId}");
                    else
                        _enemyArchetypesById.Add(enemyId, archetype);
                }

                if (!_enemyArchetypesByType.TryGetValue(archetype.enemyType, out var entries))
                {
                    entries = new List<EnemyArchetypeSO>();
                    _enemyArchetypesByType.Add(archetype.enemyType, entries);
                }

                entries.Add(archetype);
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
