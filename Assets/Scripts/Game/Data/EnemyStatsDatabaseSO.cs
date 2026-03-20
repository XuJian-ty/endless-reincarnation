using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Single enemy stats entry stored in the shared stats database.
    /// </summary>
    [Serializable]
    public class EnemyStatsEntry
    {
        [InspectorLabel("敌人ID")] public string enemyId;
        [InspectorLabel("显示名")] public string displayName;
        [InspectorLabel("类型")] public EnemyType type = EnemyType.MeleeMinion;

        [Header("难度 1 基础属性")]
        [InspectorLabel("生命")] public float baseHp = 60f;
        [InspectorLabel("攻击")] public float baseAttack = 10f;
        [InspectorLabel("防御")] public float baseDefense = 5f;
        [InspectorLabel("移速")] public float baseMoveSpeed = 3.8f;

        [Header("金币掉落")]
        [InspectorLabel("金币下限")] public int goldMin = 5;
        [InspectorLabel("金币上限")] public int goldMax = 10;

        [Header("击杀经验")]
        [InspectorLabel("经验")] public int baseExp = 10;
    }

    [Serializable]
    public class EnemyStatsGroupDefinition
    {
        [InspectorLabel("分组ID")] public string groupId = "default";
        [InspectorLabel("分组名称")] public string groupName = "默认分组";
        [InspectorLabel("敌人列表")] public List<EnemyStatsEntry> entries = new List<EnemyStatsEntry>();
    }

    /// <summary>
    /// Enemy stats database shared by all enemy variants.
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/敌人属性库", fileName = "敌人属性库")]
    public class EnemyStatsDatabaseSO : ScriptableObject
    {
        [InspectorLabel("属性分组")]
        public List<EnemyStatsGroupDefinition> groups = new List<EnemyStatsGroupDefinition>();

        [HideInInspector]
        public List<EnemyStatsEntry> entries = new List<EnemyStatsEntry>();

        private void OnEnable()
        {
            EnsureGroupsAndFlatEntries();
        }

        private void OnValidate()
        {
            EnsureGroupsAndFlatEntries();
        }

        public EnemyStatsEntry GetEntry(EnemyType type)
        {
            IReadOnlyList<EnemyStatsEntry> allEntries = GetAllEntries();
            if (allEntries == null) return null;

            foreach (var entry in allEntries)
            {
                if (entry != null && entry.type == type)
                    return entry;
            }

            return null;
        }

        public int CountByType(EnemyType type)
        {
            IReadOnlyList<EnemyStatsEntry> allEntries = GetAllEntries();
            if (allEntries == null) return 0;

            int count = 0;
            for (int i = 0; i < allEntries.Count; i++)
            {
                var entry = allEntries[i];
                if (entry != null && entry.type == type)
                    count++;
            }

            return count;
        }

        public EnemyStatsEntry GetEntry(string enemyId)
        {
            IReadOnlyList<EnemyStatsEntry> allEntries = GetAllEntries();
            if (allEntries == null || string.IsNullOrEmpty(enemyId)) return null;

            string normalizedId = NormalizeId(enemyId);

            foreach (var entry in allEntries)
            {
                if (entry != null && NormalizeId(entry.enemyId) == normalizedId)
                    return entry;
            }

            return null;
        }

        public EnemyRuntimeStats GetScaled(EnemyType type, int difficulty, DifficultyScalingSO scaling)
        {
            return BuildScaledStats(GetEntry(type), difficulty, scaling);
        }

        public EnemyRuntimeStats GetScaled(string enemyId, int difficulty, DifficultyScalingSO scaling)
        {
            return BuildScaledStats(GetEntry(enemyId), difficulty, scaling);
        }

        public IReadOnlyList<EnemyStatsEntry> GetAllEntries()
        {
            EnsureGroupsAndFlatEntries();
            return entries;
        }

        public IReadOnlyList<EnemyStatsGroupDefinition> GetGroups()
        {
            EnsureGroupsAndFlatEntries();
            return groups;
        }

        public void SyncFlatEntries()
        {
            EnsureGroupsAndFlatEntries();
        }

        private static EnemyRuntimeStats BuildScaledStats(EnemyStatsEntry entry, int difficulty, DifficultyScalingSO scaling)
        {
            if (entry == null || scaling == null)
                return default;

            int difficultyClamped = Mathf.Max(1, difficulty);
            float multiplierIndex = difficultyClamped - 1;
            return new EnemyRuntimeStats
            {
                enemyId = entry.enemyId,
                type = entry.type,
                maxHp = entry.baseHp * Mathf.Pow(1f + scaling.hpScale, multiplierIndex),
                currentHp = entry.baseHp * Mathf.Pow(1f + scaling.hpScale, multiplierIndex),
                attack = entry.baseAttack * Mathf.Pow(1f + scaling.attackScale, multiplierIndex),
                defense = entry.baseDefense * Mathf.Pow(1f + scaling.defenseScale, multiplierIndex),
                moveSpeed = entry.baseMoveSpeed * Mathf.Pow(1f + scaling.moveSpeedScale, multiplierIndex),
                goldMin = entry.goldMin,
                goldMax = entry.goldMax,
                expReward = entry.baseExp,
            };
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private void EnsureGroupsAndFlatEntries()
        {
            groups ??= new List<EnemyStatsGroupDefinition>();
            entries ??= new List<EnemyStatsEntry>();

            if (groups.Count == 0 && entries.Count > 0)
                BuildGroupsFromFlatEntries();

            RebuildFlatEntriesFromGroups();
        }

        private void BuildGroupsFromFlatEntries()
        {
            groups.Clear();
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                EnemyStatsEntry entry = entries[i];
                if (entry == null)
                    continue;

                groups.Add(new EnemyStatsGroupDefinition
                {
                    groupId = string.IsNullOrWhiteSpace(entry.enemyId) ? $"group_{i}" : entry.enemyId.Trim(),
                    groupName = string.IsNullOrWhiteSpace(entry.displayName) ? entry.enemyId : entry.displayName.Trim(),
                    entries = new List<EnemyStatsEntry> { entry },
                });
            }
        }

        private void RebuildFlatEntriesFromGroups()
        {
            entries.Clear();
            if (groups == null)
                return;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                EnemyStatsGroupDefinition group = groups[groupIndex];
                if (group == null)
                    continue;

                group.entries ??= new List<EnemyStatsEntry>();
                for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                {
                    EnemyStatsEntry entry = group.entries[entryIndex];
                    if (entry != null)
                        entries.Add(entry);
                }
            }
        }
    }
}
