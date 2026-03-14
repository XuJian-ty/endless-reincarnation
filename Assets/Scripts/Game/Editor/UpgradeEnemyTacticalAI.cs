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
    /// 3) remove redundant slot override placeholders
    /// </summary>
    public static class UpgradeEnemyTacticalAI
    {
        [MenuItem("游戏/AI/一键升级敌人战术AI（行为树+参数+配置清理）", false, 10)]
        public static void Upgrade()
        {
            var archetypes = EnemyConfigAuditUtility.LoadEnemyArchetypes();
            var sharedSkillDb = EnemyConfigAuditUtility.LoadFirstAsset<SharedSkillDatabaseSO>("t:SharedSkillDatabaseSO");

            if (archetypes == null || archetypes.Count == 0)
            {
                Debug.LogWarning("[AI] 未找到 EnemyArchetypeSO，请先执行：游戏/一键创建全部配置（需求书默认数据）。");
                return;
            }

            int updatedArchetypes = 0;
            for (int i = 0; i < archetypes.Count; i++)
            {
                var archetype = archetypes[i];
                if (archetype == null)
                    continue;

                bool dirty = false;
                dirty |= EnsureTacticalDefaults(archetype);
                dirty |= EnsureSkillSlotDefaults(archetype);
                dirty |= NormalizeRedundantSlotOverrides(archetype);

                if (!dirty)
                    continue;

                updatedArchetypes++;
                EditorUtility.SetDirty(archetype);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyConfigAuditUtility.Audit(archetypes, sharedSkillDb, logResult: true);

            Debug.Log($"[AI] 战术 AI 升级完成：更新 {updatedArchetypes} 个敌人行为配置。");
        }

        private static bool EnsureTacticalDefaults(EnemyArchetypeSO archetype)
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

                    float castRange = ResolveCastRange(slot);
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

            if (archetype.reactionMinSeconds <= 0f)
            {
                archetype.reactionMinSeconds = isBossOrRanged ? 0.07f : 0.09f;
                changed = true;
            }

            if (archetype.reactionMaxSeconds < archetype.reactionMinSeconds)
            {
                archetype.reactionMaxSeconds = isBossOrRanged ? 0.16f : 0.2f;
                changed = true;
            }

            if (archetype.decisionCommitSeconds <= 0f)
            {
                archetype.decisionCommitSeconds = isBossOrRanged ? 0.22f : 0.26f;
                changed = true;
            }

            if (archetype.searchMemoryDuration <= 0f)
            {
                archetype.searchMemoryDuration = isBossOrRanged ? 1.9f : 1.4f;
                changed = true;
            }

            if (archetype.aggression <= 0f)
            {
                archetype.aggression = isBossOrRanged ? 0.62f : 0.55f;
                changed = true;
            }

            if (archetype.caution <= 0f)
            {
                archetype.caution = isBossOrRanged ? 0.45f : 0.38f;
                changed = true;
            }

            if (archetype.dodgeBias <= 0f)
            {
                archetype.dodgeBias = isBossOrRanged ? 0.58f : 0.42f;
                changed = true;
            }

            if (archetype.punishBias <= 0f)
            {
                archetype.punishBias = isBossOrRanged ? 0.62f : 0.5f;
                changed = true;
            }

            if (archetype.strafeBias <= 0f)
            {
                archetype.strafeBias = isBossOrRanged ? 0.52f : 0.38f;
                changed = true;
            }

            if (archetype.maxPressureAllies <= 0)
            {
                archetype.maxPressureAllies = archetype.enemyType == EnemyType.MeleeMinion ? 1 : archetype.enemyType == EnemyType.Boss ? 3 : 2;
                changed = true;
            }

            if (archetype.poiseMax <= 0f)
            {
                archetype.poiseMax = archetype.enemyType switch
                {
                    EnemyType.MeleeMinion => 10f,
                    EnemyType.RangedMinion => 8f,
                    EnemyType.Elite => 18f,
                    EnemyType.Guardian => 26f,
                    EnemyType.Boss => 34f,
                    _ => 12f,
                };
                changed = true;
            }

            if (archetype.poiseRecoveryPerSecond <= 0f)
            {
                archetype.poiseRecoveryPerSecond = archetype.enemyType switch
                {
                    EnemyType.MeleeMinion => 7f,
                    EnemyType.RangedMinion => 6f,
                    EnemyType.Elite => 9f,
                    EnemyType.Guardian => 11f,
                    EnemyType.Boss => 13f,
                    _ => 8f,
                };
                changed = true;
            }

            if (archetype.poiseBreakStunDuration <= 0f)
            {
                archetype.poiseBreakStunDuration = archetype.enemyType switch
                {
                    EnemyType.MeleeMinion => 0.28f,
                    EnemyType.RangedMinion => 0.25f,
                    EnemyType.Elite => 0.34f,
                    EnemyType.Guardian => 0.4f,
                    EnemyType.Boss => 0.45f,
                    _ => 0.3f,
                };
                changed = true;
            }

            return changed;
        }

        private static float ResolveCastRange(EnemySkillSlotBinding slot)
        {
            if (slot == null)
                return 3f;

            return slot.castRange > 0.01f ? slot.castRange : 3f;
        }

        private static bool EnsureSkillSlotDefaults(EnemyArchetypeSO archetype)
        {
            if (archetype == null || archetype.skillSlots == null)
                return false;

            bool changed = false;
            for (int i = 0; i < archetype.skillSlots.Count; i++)
            {
                EnemySkillSlotBinding slot = archetype.skillSlots[i];
                if (slot == null)
                    continue;

                float castRange = ResolveCastRange(slot);
                if (slot.idealCastRange <= 0f)
                {
                    slot.idealCastRange = castRange;
                    changed = true;
                }

                if (slot.repeatPenalty <= 0f)
                {
                    slot.repeatPenalty = 0.2f;
                    changed = true;
                }

                if (slot.punishWeight <= 0f)
                {
                    slot.punishWeight = 0.5f;
                    changed = true;
                }

                if (slot.riskWeight <= 0f)
                {
                    slot.riskWeight = 0.35f;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool NormalizeRedundantSlotOverrides(EnemyArchetypeSO archetype)
        {
            return false;
        }
    }
}
