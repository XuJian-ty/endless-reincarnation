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

        private EnemyStatsDatabaseSO _enemyStatsDatabase;
        private EnemyArchetypeDatabaseSO _enemyArchetypeDatabase;
        private EnemySkillDatabaseSO _enemySkillDatabase;
        private SharedSkillDatabaseSO _sharedSkillDatabase;
        private LevelConfigDatabaseSO _levelConfigDatabase;
        private DropTableDatabaseSO _dropTableDatabase;
        private WeaponDatabaseSO _weaponDatabase;
        private LevelGrowthSO _levelGrowth;
        private DifficultyScalingSO _difficultyScaling;
        private ShopPriceConfigSO _shopPriceConfig;
        private PotionConfigSO _potionConfig;
        private SkillConfigDatabaseSO _skillConfigDatabase;
        private AnimationFrameDamageDatabaseSO _animationFrameDamageDatabase;
        private AnimationFrameVfxDatabaseSO _animationFrameVfxDatabase;
        private ItemDisplayDatabaseSO _itemDisplayDatabase;
        private SlotBackgroundConfigSO _slotBackgroundConfig;
        private KeyRebindConfigSO _keyRebindConfig;
        private BackpackUIConfigSO _backpackUIConfig;
        private BuffConfigSO _buffConfig;

        public EnemyStatsDatabaseSO GetEnemyStatsDatabase()
        {
            if (_enemyStatsDatabase == null)
                _enemyStatsDatabase = Resources.Load<EnemyStatsDatabaseSO>(ConfigPathPrefix + "敌人属性库");
            return _enemyStatsDatabase;
        }

        public EnemyArchetypeDatabaseSO GetEnemyArchetypeDatabase()
        {
            if (_enemyArchetypeDatabase == null)
                _enemyArchetypeDatabase = Resources.Load<EnemyArchetypeDatabaseSO>(ConfigPathPrefix + "敌人行为配置库");
            return _enemyArchetypeDatabase;
        }

        public EnemyArchetypeSO GetEnemyArchetype(EnemyType type)
        {
            return GetEnemyArchetypeDatabase() != null ? _enemyArchetypeDatabase.Get(type) : null;
        }

        public EnemyArchetypeSO GetEnemyArchetype(string enemyId)
        {
            return GetEnemyArchetypeDatabase() != null ? _enemyArchetypeDatabase.Get(enemyId) : null;
        }

        public EnemySkillDatabaseSO GetEnemySkillDatabase()
        {
            if (_enemySkillDatabase == null)
            {
                _enemySkillDatabase = Resources.Load<EnemySkillDatabaseSO>(ConfigPathPrefix + "敌人技能库");
                if (_enemySkillDatabase == null)
                    _enemySkillDatabase = EnemySkillDatabaseDefaults.CreateRuntimeDefault();
            }
            return _enemySkillDatabase;
        }

        public SharedSkillDatabaseSO GetSharedSkillDatabase()
        {
            if (_sharedSkillDatabase == null)
                _sharedSkillDatabase = Resources.Load<SharedSkillDatabaseSO>(ConfigPathPrefix + "共享技能库");
            return _sharedSkillDatabase;
        }

        public LevelConfigDatabaseSO GetLevelConfigDatabase()
        {
            if (_levelConfigDatabase == null)
                _levelConfigDatabase = Resources.Load<LevelConfigDatabaseSO>(ConfigPathPrefix + "关卡配置库");
            return _levelConfigDatabase;
        }

        public DropTableDatabaseSO GetDropTableDatabase()
        {
            if (_dropTableDatabase == null)
                _dropTableDatabase = Resources.Load<DropTableDatabaseSO>(ConfigPathPrefix + "掉落表库");
            return _dropTableDatabase;
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
            {
                _levelGrowth = Resources.Load<LevelGrowthSO>(ConfigPathPrefix + "玩家成长属性库");
                if (_levelGrowth == null)
                    _levelGrowth = Resources.Load<LevelGrowthSO>(ConfigPathPrefix + "玩家成长库");
            }
            return _levelGrowth;
        }

        public DifficultyScalingSO GetDifficultyScaling()
        {
            if (_difficultyScaling == null)
                _difficultyScaling = Resources.Load<DifficultyScalingSO>(ConfigPathPrefix + "难度系数");
            return _difficultyScaling;
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
            {
                _skillConfigDatabase = Resources.Load<SkillConfigDatabaseSO>(ConfigPathPrefix + "玩家技能配置库");
                if (_skillConfigDatabase == null)
                    _skillConfigDatabase = Resources.Load<SkillConfigDatabaseSO>(ConfigPathPrefix + "技能配置库");
            }
            return _skillConfigDatabase;
        }

        public AnimationFrameDamageDatabaseSO GetAnimationFrameDamageDatabase()
        {
            if (_animationFrameDamageDatabase == null)
            {
                _animationFrameDamageDatabase = Resources.Load<AnimationFrameDamageDatabaseSO>(ConfigPathPrefix + "技能伤害数据配置库");
                if (_animationFrameDamageDatabase == null)
                    _animationFrameDamageDatabase = Resources.Load<AnimationFrameDamageDatabaseSO>(ConfigPathPrefix + "动画帧伤害数据配置库");
            }
            return _animationFrameDamageDatabase;
        }

        public AnimationFrameVfxDatabaseSO GetAnimationFrameVfxDatabase()
        {
            if (_animationFrameVfxDatabase == null)
                _animationFrameVfxDatabase = Resources.Load<AnimationFrameVfxDatabaseSO>(ConfigPathPrefix + "动画帧特效数据配置库");
            return _animationFrameVfxDatabase;
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
    }
}
