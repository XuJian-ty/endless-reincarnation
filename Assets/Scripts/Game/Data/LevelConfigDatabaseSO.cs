using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    public enum ShopGoodsType
    {
        StackableItem,
        Weapon,
    }

    [Serializable]
    public class ShopGoodsEntry
    {
        [InspectorLabel("商品类型")]
        public ShopGoodsType goodsType = ShopGoodsType.StackableItem;

        [InspectorLabel("物品ID")]
        [Tooltip("当商品类型为可堆叠物品时使用，例如 potion_hp / potion_mp / nectar。")]
        public string itemId = "";

        [InspectorLabel("武器ID")]
        [Tooltip("当商品类型为武器时使用，例如 sword / gun。")]
        public string weaponId = "";

        [InspectorLabel("武器品质")]
        public WeaponRarity weaponRarity = WeaponRarity.Common;

        [InspectorLabel("刷出概率")]
        [Range(0f, 1f)]
        public float spawnChance = 0.5f;

        [InspectorLabel("最小库存")]
        [Min(1)]
        public int minCount = 1;

        [InspectorLabel("最大库存")]
        [Min(1)]
        public int maxCount = 1;

        public int RollCount(System.Random rng)
        {
            int min = Mathf.Max(1, minCount);
            int max = Mathf.Max(min, maxCount);
            if (rng == null || min >= max)
                return min;

            return rng.Next(min, max + 1);
        }
    }

    /// <summary>
    /// 单关配置：关卡编号、全局生成方案库与方案索引，以及各类掉落表。
    /// </summary>
    [Serializable]
    public class LevelConfigData
    {
        [Header("关卡编号（从 1 开始）")]
        [InspectorLabel("关卡编号")]
        [Min(1)]
        public int levelIndex = 1;

        [Header("生成方案")]
        [InspectorLabel("关卡场景名")]
        [Tooltip("运行时用于加载本关卡的场景名。")]
        [HideInInspector]
        public string sceneName = "";

        [HideInInspector]
        public string sceneGuid = "";

        [FormerlySerializedAs("enemyGlobalSpawnPlanLibrary")]
        [InspectorLabel("全局生成方案库")]
        [Tooltip("必填。全局生成器会从这份方案库中选择一个列表项执行生成任务。")]
        public LevelEnemySpawnPlanSO globalSpawnPlanLibrary;

        [FormerlySerializedAs("enemyGlobalSpawnPlanIndex")]
        [InspectorLabel("全局方案列表项")]
        [Min(0)]
        [Tooltip("当前关卡使用的全局生成方案列表项索引。")]
        public int globalSpawnPlanIndex = 0;

        [Header("小怪掉落")]
        [InspectorLabel("小怪固定金币")]
        [Min(0)]
        [Tooltip("本关小怪击杀后固定获得的金币数量，不参与权重随机。")]
        public int minionGoldReward = 10;

        [InspectorLabel("小怪固定经验")]
        [Min(0)]
        [Tooltip("本关小怪击杀后固定获得的经验数量，不参与权重随机。")]
        public int minionExpReward = 10;

        [Header("精英掉落")]
        [InspectorLabel("精英固定金币")]
        [Min(0)]
        [Tooltip("本关精英怪击杀后固定获得的金币数量，不参与权重随机。")]
        public int eliteGoldReward = 30;

        [InspectorLabel("精英固定经验")]
        [Min(0)]
        [Tooltip("本关精英怪击杀后固定获得的经验数量，不参与权重随机。")]
        public int eliteExpReward = 30;

        [InspectorLabel("精英掉落条目")]
        [Tooltip("本关精英怪掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> eliteDropEntries = new List<DropEntry>();

        [Header("宝箱掉落")]
        [InspectorLabel("宝箱掉落条目")]
        [Tooltip("本关宝箱掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> chestDropEntries = new List<DropEntry>();

        [Header("守卫者掉落")]
        [InspectorLabel("守卫者固定金币")]
        [Min(0)]
        [Tooltip("本关守卫者击杀后固定获得的金币数量，不参与权重随机。")]
        public int guardianGoldReward = 0;

        [InspectorLabel("守卫者固定经验")]
        [Min(0)]
        [Tooltip("本关守卫者击杀后固定获得的经验数量，不参与权重随机。")]
        public int guardianExpReward = 0;

        [InspectorLabel("守卫者固定天赋点")]
        [Min(0)]
        [Tooltip("本关守卫者击杀后固定获得的天赋点数量，不参与权重随机。")]
        public int guardianTalentReward = 1;

        [InspectorLabel("守卫者掉落条目")]
        [Tooltip("本关守卫者掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> guardianDropEntries = new List<DropEntry>();

        [Header("Boss掉落")]
        [InspectorLabel("Boss固定金币")]
        [Min(0)]
        [Tooltip("本关Boss击杀后固定获得的金币数量，不参与权重随机。")]
        public int bossGoldReward = 0;

        [InspectorLabel("Boss固定经验")]
        [Min(0)]
        [Tooltip("本关Boss击杀后固定获得的经验数量，不参与权重随机。")]
        public int bossExpReward = 0;

        [InspectorLabel("Boss掉落条目")]
        [Tooltip("本关Boss掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> bossDropEntries = new List<DropEntry>();

        [Header("商店商品规则")]
        [InspectorLabel("商店商品条目")]
        [Tooltip("本关所有商店共享这份商品规则。每个商店实例会独立按概率随机出自己的商品种类和库存。")]
        public List<ShopGoodsEntry> shopGoodsEntries = new List<ShopGoodsEntry>();

        public string RollEliteDrop(System.Random rng)
        {
            return RollDrop(eliteDropEntries, rng);
        }

        public string RollChestDrop(System.Random rng)
        {
            return RollDrop(chestDropEntries, rng);
        }

        public string RollGuardianDrop(System.Random rng)
        {
            return RollDrop(guardianDropEntries, rng);
        }

        public string RollBossDrop(System.Random rng)
        {
            return RollDrop(bossDropEntries, rng);
        }

        public int GetGoldReward(EnemyType enemyType)
        {
            return enemyType switch
            {
                EnemyType.Elite => Mathf.Max(0, eliteGoldReward),
                EnemyType.Guardian => Mathf.Max(0, guardianGoldReward),
                EnemyType.Boss => Mathf.Max(0, bossGoldReward),
                _ => Mathf.Max(0, minionGoldReward),
            };
        }

        public int GetExpReward(EnemyType enemyType)
        {
            return enemyType switch
            {
                EnemyType.Elite => Mathf.Max(0, eliteExpReward),
                EnemyType.Guardian => Mathf.Max(0, guardianExpReward),
                EnemyType.Boss => Mathf.Max(0, bossExpReward),
                _ => Mathf.Max(0, minionExpReward),
            };
        }

        public int GetTalentPointReward(EnemyType enemyType)
        {
            return enemyType == EnemyType.Guardian
                ? Mathf.Max(0, guardianTalentReward)
                : 0;
        }

        private static string RollDrop(List<DropEntry> dropEntries, System.Random rng)
        {
            if (dropEntries == null || dropEntries.Count == 0 || rng == null)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < dropEntries.Count; i++)
            {
                DropEntry entry = dropEntries[i];
                if (entry == null)
                    continue;

                totalWeight += Mathf.Max(0f, entry.weight);
            }

            if (totalWeight <= 0f)
                return null;

            float roll = (float)rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < dropEntries.Count; i++)
            {
                DropEntry entry = dropEntries[i];
                if (entry == null)
                    continue;

                cumulative += Mathf.Max(0f, entry.weight);
                if (roll <= cumulative)
                    return entry.itemType;
            }

            return dropEntries[dropEntries.Count - 1]?.itemType;
        }
    }

    /// <summary>
    /// 关卡配置库：列表每项对应一关（全局生成方案与掉落配置）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/关卡配置库", fileName = "关卡配置库")]
    public class LevelConfigDatabaseSO : ScriptableObject
    {
        [InspectorLabel("关卡列表")]
        [Tooltip("按关卡编号顺序排列的配置列表，可继续扩展更多关卡。")]
        public List<LevelConfigData> levels = new List<LevelConfigData>();

        public LevelConfigData GetConfigForLevel(int levelIndex)
        {
            if (levels == null || levelIndex < 1) return null;
            int i = levelIndex - 1;
            if (i >= levels.Count) return null;
            return levels[i];
        }

        public string GetSceneNameForLevel(int levelIndex)
        {
            LevelConfigData config = GetConfigForLevel(levelIndex);
            return config != null && !string.IsNullOrWhiteSpace(config.sceneName)
                ? config.sceneName.Trim()
                : string.Empty;
        }
    }
}
