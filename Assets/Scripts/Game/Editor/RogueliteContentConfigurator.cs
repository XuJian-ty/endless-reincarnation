using System;
using System.Collections.Generic;
using System.Text;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RogueliteContentConfigurator
    {
        private const string MutationItemPrefix = "mutation_unlock_";
        private const string MutationMeleeRareIconPath = "Assets/UI图片/变异道具图标/变异核心_近战_精良.png";
        private const string MutationMeleeEpicIconPath = "Assets/UI图片/变异道具图标/变异核心_近战_史诗.png";
        private const string MutationRangedRareIconPath = "Assets/UI图片/变异道具图标/变异核心_远程_精良.png";
        private const string MutationRangedEpicIconPath = "Assets/UI图片/变异道具图标/变异核心_远程_史诗.png";
        private const string MutationPassiveRareIconPath = "Assets/UI图片/变异道具图标/变异核心_被动_精良.png";
        private const string MutationPassiveEpicIconPath = "Assets/UI图片/变异道具图标/变异核心_被动_史诗.png";
        private const string SkillConfigPath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private const string SkillEffectPath = "Assets/Resources/配置/技能效果库.asset";
        private const string PassiveSkillEffectPath = "Assets/Resources/配置/被动技能效果库.asset";
        private const string ItemDisplayPath = "Assets/Resources/配置/物品显示配置.asset";
        private const string LevelConfigPath = "Assets/Resources/配置/关卡配置库.asset";
        private const string LocalSpawnPlanPath = "Assets/Resources/配置/关卡局部生成方案库.asset";
        private const string SpawnTaskDatabasePath = "Assets/Resources/配置/生成任务库.asset";

        private sealed class MutationItemDefinition
        {
            public string itemId;
            public string displayName;
            public string description;
            public Sprite icon;
            public WeaponRarity rarity;
        }

        public static void ApplyAll()
        {
            SkillConfigDatabaseSO skillConfig = LoadRequired<SkillConfigDatabaseSO>(SkillConfigPath);
            SkillEffectDatabaseSO skillEffects = LoadRequired<SkillEffectDatabaseSO>(SkillEffectPath);
            PassiveSkillEffectDatabaseSO passiveEffects = LoadRequired<PassiveSkillEffectDatabaseSO>(PassiveSkillEffectPath);
            ItemDisplayDatabaseSO itemDisplay = LoadRequired<ItemDisplayDatabaseSO>(ItemDisplayPath);
            LevelConfigDatabaseSO levelConfig = LoadRequired<LevelConfigDatabaseSO>(LevelConfigPath);
            LevelLocalEnemySpawnPlanSO localSpawnPlans = LoadRequired<LevelLocalEnemySpawnPlanSO>(LocalSpawnPlanPath);
            EnemySpawnTaskDatabaseSO spawnTasks = LoadRequired<EnemySpawnTaskDatabaseSO>(SpawnTaskDatabasePath);

            List<MutationItemDefinition> mutationItems = ConfigureMutationUnlockItems(skillConfig);
            ConfigureItemDisplay(itemDisplay, mutationItems);
            ConfigureMutationDrops(levelConfig, mutationItems);
            ConfigureRegionSpawnerMode(levelConfig);
            ConfigureRegionSpawnPlans(localSpawnPlans);
            ConfigureRegionEnemyDensity(spawnTasks);

            EditorUtility.SetDirty(skillEffects);
            EditorUtility.SetDirty(passiveEffects);
            EditorUtility.SetDirty(itemDisplay);
            EditorUtility.SetDirty(levelConfig);
            EditorUtility.SetDirty(localSpawnPlans);
            EditorUtility.SetDirty(spawnTasks);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RogueliteContentConfigurator] CONFIG_OK | MutationItems={mutationItems.Count} | Levels={levelConfig.levels.Count} | LocalPlans={localSpawnPlans.plans.Count}");
        }

        private static List<MutationItemDefinition> ConfigureMutationUnlockItems(SkillConfigDatabaseSO skillConfig)
        {
            var result = new List<MutationItemDefinition>();
            var usedItemIds = new HashSet<string>(StringComparer.Ordinal);
            if (skillConfig?.entries == null)
                return result;

            for (int entryIndex = 0; entryIndex < skillConfig.entries.Count; entryIndex++)
            {
                SkillConfigEntry entry = skillConfig.entries[entryIndex];
                if (entry == null)
                    continue;

                if (entry.IsPassiveSkill)
                    ConfigurePassiveMutationItems(entry, result, usedItemIds);
                else
                    ConfigureSharedMutationItems(entry, result, usedItemIds);
            }

            return result;
        }

        private static void ConfigureSharedMutationItems(
            SkillConfigEntry entry,
            List<MutationItemDefinition> result,
            HashSet<string> usedItemIds)
        {
            SkillEffectVariantGroupDefinition variantGroup = entry.ResolveSkillEffectVariantGroup();
            if (variantGroup?.entries == null || variantGroup.entries.Count <= 1)
                return;

            string defaultSkillId = entry.GetResolvedSkillId();
            for (int i = 0; i < variantGroup.entries.Count; i++)
            {
                SharedSkillDefinition definition = variantGroup.entries[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.skillId))
                    continue;

                string skillId = definition.skillId.Trim();
                if (string.Equals(skillId, defaultSkillId, StringComparison.Ordinal))
                {
                    definition.mutationUnlockItemId = string.Empty;
                    continue;
                }

                string itemId = BuildMutationItemId(entry.GetResolvedActionId(), skillId);
                EnsureUniqueItemId(itemId, usedItemIds);
                definition.mutationUnlockItemId = itemId;
                result.Add(BuildMutationItem(entry, skillId, definition.displayName, itemId, i));
            }
        }

        private static void ConfigurePassiveMutationItems(
            SkillConfigEntry entry,
            List<MutationItemDefinition> result,
            HashSet<string> usedItemIds)
        {
            PassiveSkillEffectVariantGroupDefinition variantGroup = entry.ResolvePassiveSkillEffectVariantGroup();
            if (variantGroup?.entries == null || variantGroup.entries.Count <= 1)
                return;

            string defaultSkillId = entry.GetResolvedSkillId();
            for (int i = 0; i < variantGroup.entries.Count; i++)
            {
                PassiveSkillEffectDefinition definition = variantGroup.entries[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.skillId))
                    continue;

                string skillId = definition.skillId.Trim();
                if (string.Equals(skillId, defaultSkillId, StringComparison.Ordinal))
                {
                    definition.mutationUnlockItemId = string.Empty;
                    continue;
                }

                string itemId = BuildMutationItemId(entry.GetResolvedActionId(), skillId);
                EnsureUniqueItemId(itemId, usedItemIds);
                definition.mutationUnlockItemId = itemId;
                result.Add(BuildMutationItem(entry, skillId, definition.displayName, itemId, i));
            }
        }

        private static MutationItemDefinition BuildMutationItem(
            SkillConfigEntry entry,
            string skillId,
            string variantDisplayName,
            string itemId,
            int variantIndex)
        {
            string skillDisplayName = !string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.displayName.Trim()
                : entry.GetResolvedActionId();
            string directionDisplayName = !string.IsNullOrWhiteSpace(variantDisplayName)
                ? variantDisplayName.Trim()
                : skillId;
            WeaponRarity rarity = variantIndex >= 2 ? WeaponRarity.Epic : WeaponRarity.Rare;
            return new MutationItemDefinition
            {
                itemId = itemId,
                displayName = $"{skillDisplayName}·{directionDisplayName}变异核心",
                description = $"用于永久解锁“{skillDisplayName}”的“{directionDisplayName}”变异方向。解锁后可免费自由切换。",
                icon = ResolveMutationItemIcon(entry, rarity),
                rarity = rarity,
            };
        }

        private static Sprite ResolveMutationItemIcon(SkillConfigEntry entry, WeaponRarity rarity)
        {
            bool isEpic = rarity == WeaponRarity.Epic;
            if (entry.IsPassiveSkill)
                return LoadRequired<Sprite>(isEpic ? MutationPassiveEpicIconPath : MutationPassiveRareIconPath);

            string actionId = entry.GetResolvedActionId();
            if (actionId.StartsWith("attack", StringComparison.OrdinalIgnoreCase))
                return LoadRequired<Sprite>(isEpic ? MutationMeleeEpicIconPath : MutationMeleeRareIconPath);
            if (actionId.StartsWith("shoot", StringComparison.OrdinalIgnoreCase))
                return LoadRequired<Sprite>(isEpic ? MutationRangedEpicIconPath : MutationRangedRareIconPath);

            throw new InvalidOperationException($"动作 {actionId} 未配置变异道具图标类别。");
        }

        private static void ConfigureItemDisplay(ItemDisplayDatabaseSO itemDisplay, List<MutationItemDefinition> mutationItems)
        {
            itemDisplay.entries ??= new List<ItemDisplayEntry>();
            itemDisplay.entries.RemoveAll(entry => entry != null
                && !string.IsNullOrWhiteSpace(entry.itemId)
                && entry.itemId.StartsWith(MutationItemPrefix, StringComparison.Ordinal));

            for (int i = 0; i < mutationItems.Count; i++)
            {
                MutationItemDefinition item = mutationItems[i];
                itemDisplay.entries.Add(new ItemDisplayEntry
                {
                    itemId = item.itemId,
                    displayName = item.displayName,
                    icon = item.icon,
                    description = item.description,
                    rarity = item.rarity,
                });
            }
        }

        private static void ConfigureMutationDrops(LevelConfigDatabaseSO levelConfig, List<MutationItemDefinition> mutationItems)
        {
            if (levelConfig?.levels == null)
                return;

            for (int levelIndex = 0; levelIndex < levelConfig.levels.Count; levelIndex++)
            {
                LevelConfigData level = levelConfig.levels[levelIndex];
                if (level == null)
                    continue;

                level.eliteDropEntries ??= new List<DropEntry>();
                level.chestDropEntries ??= new List<DropEntry>();
                RemoveMutationDrops(level.eliteDropEntries);
                RemoveMutationDrops(level.chestDropEntries);
                for (int i = 0; i < mutationItems.Count; i++)
                {
                    level.eliteDropEntries.Add(new DropEntry { itemType = mutationItems[i].itemId, weight = 0.006f });
                    level.chestDropEntries.Add(new DropEntry { itemType = mutationItems[i].itemId, weight = 0.004f });
                }
            }
        }

        private static void RemoveMutationDrops(List<DropEntry> entries)
        {
            entries.RemoveAll(entry => entry != null
                && !string.IsNullOrWhiteSpace(entry.itemType)
                && entry.itemType.StartsWith(MutationItemPrefix, StringComparison.Ordinal));
        }

        private static void ConfigureRegionSpawnerMode(LevelConfigDatabaseSO levelConfig)
        {
            if (levelConfig?.levels == null || levelConfig.levels.Count < 5)
                throw new InvalidOperationException("关卡配置库缺少五关配置，无法切换到区域刷怪模式。");

            for (int i = 0; i < 5; i++)
            {
                if (levelConfig.levels[i] == null)
                    throw new InvalidOperationException($"关卡配置库的 Level_{i + 1} 配置为空。");

                levelConfig.levels[i].globalSpawnPlanLibrary = null;
                levelConfig.levels[i].globalSpawnPlanIndex = 0;
            }
        }

        private static void ConfigureRegionSpawnPlans(LevelLocalEnemySpawnPlanSO localSpawnPlans)
        {
            localSpawnPlans.plans ??= new List<LevelLocalEnemySpawnPlanDefinition>();
            while (localSpawnPlans.plans.Count < 5)
                localSpawnPlans.plans.Add(new LevelLocalEnemySpawnPlanDefinition());

            localSpawnPlans.plans[3] = new LevelLocalEnemySpawnPlanDefinition
            {
                taskIndex = 1,
                spawnMode = LocalEnemySpawnMode.SpawnOnce,
                taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                totalTaskCount = 1,
                taskInterval = 8f,
            };
            localSpawnPlans.plans[4] = new LevelLocalEnemySpawnPlanDefinition
            {
                taskIndex = 0,
                spawnMode = LocalEnemySpawnMode.SpawnOnce,
                taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                totalTaskCount = 1,
                taskInterval = 8f,
            };
        }

        private static void ConfigureRegionEnemyDensity(EnemySpawnTaskDatabaseSO spawnTasks)
        {
            if (spawnTasks?.tasks == null || spawnTasks.tasks.Count < 2)
                throw new InvalidOperationException("生成任务库缺少普通怪和精英怪任务。");

            EnemySpawnTaskDefinition minionTask = spawnTasks.tasks[0];
            EnemySpawnTaskDefinition eliteTask = spawnTasks.tasks[1];
            if (minionTask == null || minionTask.spawnType != EnemySpawnCategory.Minion)
                throw new InvalidOperationException("生成任务库索引 0 必须配置为普通怪任务。");
            if (eliteTask == null || eliteTask.spawnType != EnemySpawnCategory.Elite)
                throw new InvalidOperationException("生成任务库索引 1 必须配置为精英怪任务。");

            minionTask.countMode = EnemySpawnCountMode.RandomRange;
            minionTask.randomMinCount = 4;
            minionTask.randomMaxCount = 6;
            minionTask.spawnRadius = 16f;
            minionTask.spawnMinSpacing = 2.5f;

            eliteTask.countMode = EnemySpawnCountMode.RandomRange;
            eliteTask.randomMinCount = 3;
            eliteTask.randomMaxCount = 4;
            eliteTask.spawnRadius = 14f;
            eliteTask.spawnMinSpacing = 4f;
        }

        private static string BuildMutationItemId(string actionId, string skillId)
        {
            return $"{MutationItemPrefix}{SanitizeId(actionId)}_{SanitizeId(skillId)}";
        }

        private static string SanitizeId(string value)
        {
            var builder = new StringBuilder();
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }
            return builder.ToString();
        }

        private static void EnsureUniqueItemId(string itemId, HashSet<string> usedItemIds)
        {
            if (!usedItemIds.Add(itemId))
                throw new InvalidOperationException($"变异道具 ID 重复：{itemId}");
        }

        private static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
                throw new InvalidOperationException($"缺少配置资源：{assetPath}");
            return asset;
        }
    }
}
