using System;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    /// <summary>
    /// One-click tactical AI upgrade:
    /// 1) refresh default behavior tree
    /// 2) ensure tactical parameters
    /// 3) remove redundant skill-slot overrides
    /// 4) apply recommended post-cast idle recovery presets
    /// </summary>
    public static class UpgradeEnemyTacticalAI
    {
        [MenuItem("游戏/AI/一键升级敌人战术AI（行为树+参数+配置清理）", false, 10)]
        public static void Upgrade()
        {
            var defaultTree = CreateDefaultBehaviorTreeAsset.Create();
            var archetypeDb = EnemyConfigAuditUtility.LoadFirstAsset<EnemyArchetypeDatabaseSO>("t:EnemyArchetypeDatabaseSO");
            var skillDb = EnemyConfigAuditUtility.LoadFirstAsset<EnemySkillDatabaseSO>("t:EnemySkillDatabaseSO");

            if (archetypeDb == null)
            {
                Debug.LogWarning("[AI] 未找到 EnemyArchetypeDatabaseSO，请先执行：游戏/一键创建全部配置（需求书默认数据）。");
                return;
            }

            int updatedArchetypes = 0;
            for (int i = 0; i < archetypeDb.entries.Count; i++)
            {
                var archetype = archetypeDb.entries[i];
                if (archetype == null)
                    continue;

                bool dirty = EnsureBehaviorTreeBinding(archetype, defaultTree);
                dirty |= EnsureTacticalDefaults(archetype, skillDb);
                dirty |= NormalizeRedundantSlotOverrides(archetype, skillDb);

                if (!dirty)
                    continue;

                updatedArchetypes++;
                EditorUtility.SetDirty(archetype);
            }

            int updatedRecoverySkills = ApplyRecommendedPostCastRecovery(skillDb);
            if (updatedRecoverySkills > 0 && skillDb != null)
                EditorUtility.SetDirty(skillDb);

            EditorUtility.SetDirty(archetypeDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyConfigAuditUtility.Audit(archetypeDb, skillDb, logResult: true);

            Debug.Log($"[AI] 战术AI升级完成：更新 {updatedArchetypes} 个敌人行为配置，更新 {updatedRecoverySkills} 个技能后摇配置。");
        }

        private static bool EnsureBehaviorTreeBinding(EnemyArchetypeSO archetype, Game.AI.BehaviorTreeAsset defaultTree)
        {
            if (archetype == null || defaultTree == null)
                return false;

            if (archetype.behaviorTreeAsset == null)
            {
                archetype.behaviorTreeAsset = defaultTree;
                return true;
            }

            string currentPath = AssetDatabase.GetAssetPath(archetype.behaviorTreeAsset);
            string defaultPath = AssetDatabase.GetAssetPath(defaultTree);
            if (string.Equals(currentPath, defaultPath, StringComparison.OrdinalIgnoreCase) &&
                archetype.behaviorTreeAsset != defaultTree)
            {
                archetype.behaviorTreeAsset = defaultTree;
                return true;
            }

            return false;
        }

        private static bool EnsureTacticalDefaults(EnemyArchetypeSO archetype, EnemySkillDatabaseSO skillDb)
        {
            bool changed = false;
            if (archetype == null)
                return false;

            float minRange = float.MaxValue;
            float maxRange = 0f;
            if (archetype.skillSlots != null)
            {
                for (int i = 0; i < archetype.skillSlots.Count; i++)
                {
                    var slot = archetype.skillSlots[i];
                    if (slot == null)
                        continue;

                    float castRange = ResolveCastRange(slot, skillDb);
                    minRange = Mathf.Min(minRange, castRange);
                    maxRange = Mathf.Max(maxRange, castRange);
                }
            }

            if (minRange == float.MaxValue)
            {
                minRange = 2.5f;
                maxRange = 6f;
            }

            bool isBossOrRanged = archetype.enemyType == EnemyType.Boss || archetype.enemyType == EnemyType.RangedMinion;
            float suggestedInner = Mathf.Max(1.5f, Mathf.Min(maxRange, minRange + (isBossOrRanged ? 2.5f : 1.2f)));
            float suggestedOuter = Mathf.Max(suggestedInner + 2f, maxRange + (isBossOrRanged ? 3.5f : 2f));

            if (!archetype.enableTacticalCombat)
            {
                archetype.enableTacticalCombat = true;
                changed = true;
            }

            if (archetype.chaseInnerDistance <= 0f)
            {
                archetype.chaseInnerDistance = suggestedInner;
                changed = true;
            }

            if (archetype.chaseOuterDistance <= 0f)
            {
                archetype.chaseOuterDistance = suggestedOuter;
                changed = true;
            }

            if (archetype.chaseOuterDistance <= archetype.chaseInnerDistance)
            {
                archetype.chaseOuterDistance = archetype.chaseInnerDistance + 2f;
                changed = true;
            }

            if (archetype.preferredSafetyDistanceOffset < 0f)
            {
                archetype.preferredSafetyDistanceOffset = isBossOrRanged ? 0.9f : 0.25f;
                changed = true;
            }

            if (archetype.combatDistanceTolerance <= 0f)
            {
                archetype.combatDistanceTolerance = isBossOrRanged ? 1.1f : 0.75f;
                changed = true;
            }

            if (archetype.castRangeTolerance < 0f)
            {
                archetype.castRangeTolerance = 0.25f;
                changed = true;
            }

            if (archetype.retreatStepDistance <= 0f)
            {
                archetype.retreatStepDistance = isBossOrRanged ? 4.2f : 2.6f;
                changed = true;
            }

            if (archetype.tacticalHoldDuration < 0f)
            {
                archetype.tacticalHoldDuration = 0.12f;
                changed = true;
            }

            if (archetype.approachLeadTime < 0f)
            {
                archetype.approachLeadTime = isBossOrRanged ? 0.25f : 0.15f;
                changed = true;
            }

            if (archetype.losProbeInterval <= 0f)
            {
                archetype.losProbeInterval = 0.1f;
                changed = true;
            }

            if (archetype.pathProbeInterval <= 0f)
            {
                archetype.pathProbeInterval = 0.3f;
                changed = true;
            }

            if (archetype.losBlockedPenaltySeconds < 0f)
            {
                archetype.losBlockedPenaltySeconds = 0.35f;
                changed = true;
            }

            if (archetype.perceptionEyeHeight < 0f)
            {
                archetype.perceptionEyeHeight = 1.2f;
                changed = true;
            }

            if (archetype.obstacleMask.value == 0)
            {
                archetype.obstacleMask = Physics.DefaultRaycastLayers;
                changed = true;
            }

            return changed;
        }

        private static float ResolveCastRange(EnemySkillSlotBinding slot, EnemySkillDatabaseSO skillDb)
        {
            if (slot == null)
                return 3f;

            return slot.castRange > 0.01f ? slot.castRange : 3f;
        }

        private static bool NormalizeRedundantSlotOverrides(EnemyArchetypeSO archetype, EnemySkillDatabaseSO skillDb)
        {
            // 新系统中槽位绑定字段为直接值，无需规范化覆盖字段，保留此方法以兼容旧调用。
            return false;
        }

        private static int ApplyRecommendedPostCastRecovery(EnemySkillDatabaseSO skillDb)
        {
            if (skillDb == null || skillDb.entries == null)
                return 0;

            var defaults = EnemySkillDatabaseDefaults.CreateRuntimeDefault();
            if (defaults == null || defaults.entries == null)
                return 0;

            int updated = 0;
            try
            {
                for (int i = 0; i < skillDb.entries.Count; i++)
                {
                    var definition = skillDb.entries[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.skillId))
                        continue;

                    var recommended = defaults.GetEntry(definition.skillId);
                    if (recommended == null)
                        continue;

                    bool shouldEnterRecovery = recommended.enterIdleAfterCast && recommended.postCastIdleDuration > 0f;
                    float recommendedDuration = shouldEnterRecovery
                        ? Mathf.Max(0f, recommended.postCastIdleDuration)
                        : 0f;

                    bool changed = false;
                    if (definition.enterIdleAfterCast != shouldEnterRecovery)
                    {
                        definition.enterIdleAfterCast = shouldEnterRecovery;
                        changed = true;
                    }

                    if (Mathf.Abs(definition.postCastIdleDuration - recommendedDuration) > 0.0001f)
                    {
                        definition.postCastIdleDuration = recommendedDuration;
                        changed = true;
                    }

                    if (changed)
                        updated++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
            }

            return updated;
        }
    }
}
