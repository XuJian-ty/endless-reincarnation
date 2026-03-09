using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    internal static class EnemyConfigAuditUtility
    {
        internal readonly struct AuditResult
        {
            public AuditResult(
                int skillCount,
                int duplicateSkillIds,
                int archetypeCount,
                int nullArchetypes,
                int duplicateEnemyIds,
                int duplicateSlotIndices,
                int missingSkillRefs)
            {
                SkillCount = skillCount;
                DuplicateSkillIds = duplicateSkillIds;
                ArchetypeCount = archetypeCount;
                NullArchetypes = nullArchetypes;
                DuplicateEnemyIds = duplicateEnemyIds;
                DuplicateSlotIndices = duplicateSlotIndices;
                MissingSkillRefs = missingSkillRefs;
            }

            public int SkillCount { get; }
            public int DuplicateSkillIds { get; }
            public int ArchetypeCount { get; }
            public int NullArchetypes { get; }
            public int DuplicateEnemyIds { get; }
            public int DuplicateSlotIndices { get; }
            public int MissingSkillRefs { get; }

            public bool HasIssues =>
                DuplicateSkillIds > 0 ||
                NullArchetypes > 0 ||
                DuplicateEnemyIds > 0 ||
                DuplicateSlotIndices > 0 ||
                MissingSkillRefs > 0;
        }

        [MenuItem("游戏/AI/审计敌人配置一致性", false, 11)]
        private static void AuditFromMenu()
        {
            var archetypes = LoadEnemyArchetypes();
            var skillDb = LoadFirstAsset<SharedSkillDatabaseSO>("t:SharedSkillDatabaseSO");
            Audit(archetypes, skillDb, logResult: true);
        }

        internal static AuditResult Audit(IReadOnlyList<EnemyArchetypeSO> archetypes, SharedSkillDatabaseSO skillDb, bool logResult)
        {
            if (archetypes == null || skillDb == null)
            {
                if (logResult)
                    Debug.LogWarning("[AI][Audit] 缺少敌人行为配置或共享技能库，无法执行审计。");
                return new AuditResult(0, 0, 0, 0, 0, 0, 0);
            }

            int duplicateSkillIds = 0;
            int nullArchetypes = 0;
            int duplicateEnemyIds = 0;
            int duplicateSlotIndices = 0;
            int missingSkillRefs = 0;

            var skillIdSet = new HashSet<string>(StringComparer.Ordinal);
            if (skillDb.entries != null)
            {
                for (int i = 0; i < skillDb.entries.Count; i++)
                {
                    var definition = skillDb.entries[i];
                    if (definition == null)
                        continue;

                    string skillId = NormalizeId(definition.skillId);
                    if (string.IsNullOrEmpty(skillId))
                        continue;

                    if (!skillIdSet.Add(skillId))
                        duplicateSkillIds++;
                }
            }

            var enemyIdSet = new HashSet<string>(StringComparer.Ordinal);
            int archetypeCount = archetypes.Count;
            for (int i = 0; i < archetypes.Count; i++)
            {
                var archetype = archetypes[i];
                if (archetype == null)
                {
                    nullArchetypes++;
                    continue;
                }

                string enemyId = NormalizeId(archetype.GetResolvedEnemyId());
                if (!string.IsNullOrEmpty(enemyId) && !enemyIdSet.Add(enemyId))
                    duplicateEnemyIds++;

                if (archetype.skillSlots == null)
                    continue;

                var slotIndexSet = new HashSet<int>();
                for (int slotIdx = 0; slotIdx < archetype.skillSlots.Count; slotIdx++)
                {
                    var slot = archetype.skillSlots[slotIdx];
                    if (slot == null)
                        continue;

                    if (!slotIndexSet.Add(slot.slotIndex))
                        duplicateSlotIndices++;

                    string slotSkillId = NormalizeId(slot.skillId);
                    if (string.IsNullOrEmpty(slotSkillId))
                        continue;

                    if (!skillIdSet.Contains(slotSkillId))
                        missingSkillRefs++;
                }
            }

            var result = new AuditResult(
                skillDb.entries != null ? skillDb.entries.Count : 0,
                duplicateSkillIds,
                archetypeCount,
                nullArchetypes,
                duplicateEnemyIds,
                duplicateSlotIndices,
                missingSkillRefs);

            if (logResult)
            {
                string summary =
                    $"[AI][Audit] SharedSkills={result.SkillCount}, Archetypes={result.ArchetypeCount}, " +
                    $"DuplicateSkillIds={result.DuplicateSkillIds}, NullArchetypes={result.NullArchetypes}, " +
                    $"DuplicateEnemyIds={result.DuplicateEnemyIds}, DuplicateSlotIndices={result.DuplicateSlotIndices}, " +
                    $"MissingSkillRefs={result.MissingSkillRefs}";

                if (result.HasIssues)
                    Debug.LogWarning(summary);
                else
                    Debug.Log(summary);
            }

            return result;
        }

        internal static List<EnemyArchetypeSO> LoadEnemyArchetypes()
        {
            return LoadAssets<EnemyArchetypeSO>("t:EnemyArchetypeSO");
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        internal static T LoadFirstAsset<T>(string filter) where T : UnityEngine.Object
        {
            var assets = LoadAssets<T>(filter);
            return assets.Count > 0 ? assets[0] : null;
        }

        internal static List<T> LoadAssets<T>(string filter) where T : UnityEngine.Object
        {
            var assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(filter);
            if (guids == null || guids.Length == 0)
                return assets;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.StartsWith("Assets/Resources", StringComparison.OrdinalIgnoreCase))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            if (assets.Count > 0)
                return assets;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets;
        }
    }
}
